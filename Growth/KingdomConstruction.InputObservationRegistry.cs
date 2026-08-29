using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		internal const string InputObservationStateKey =
			"$ThousandAndFirst_ConstructionInputObservations";

		internal static bool TryReadInputObservations(KingdomSystem system,
			out List<KingdomConstructionInputZoneObservation> observations, out string failure)
		{
			observations = null; failure = null;
			KingdomConstructionInputObservationBook book;
			if (!TryReadInputObservationBook(system, out book, out failure)) return false;
			observations = new List<KingdomConstructionInputZoneObservation>(book.ZoneCount);
			for (int i = 0; i < book.ZoneCount; i++) observations.Add(book.ZoneAt(i));
			return true;
		}

		private static bool CaptureInputObservation(KingdomSystem system, Zone zone,
			KingdomSurvey survey, out string failure)
		{
			failure = null;
			if (!ActiveInputGround(zone, survey) || system == null || !system.Founded
				|| system.ClaimedZones == null || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				failure = "Construction input can observe only current attended realm ground.";
				return false;
			}
			string settlement = system.SettlementIdForOwnedZone(zone.ZoneID);
			int daily = KingdomRules.PolicyUpkeep(
				KingdomRules.UpkeepDrams(system.Population, system.Stage), system.Stores);
			if (string.IsNullOrEmpty(settlement) || daily < 0)
			{
				failure = "The attended source has no exact settlement authority.";
				return false;
			}
			List<KingdomConstructionInputObservationLine> lines =
				new List<KingdomConstructionInputObservationLine>();
			HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
			if (!ObserveInputWater(zone, survey, lines, identities, out failure)
				|| !ObserveInputMaterials(zone, survey, lines, identities, out failure))
			{
				ForgetInputObservation(system, zone.ZoneID); return false;
			}
			int cells;
			try { cells = checked(zone.Width * zone.Height); }
			catch { cells = 0; }
			if (cells <= 0 || cells > KingdomConstructionInputObservationRules.MaxCells)
			{
				failure = "The attended source route ground exceeds its exact observation bound.";
				ForgetInputObservation(system, zone.ZoneID); return false;
			}
			byte[] passable = new byte[cells], paved = new byte[cells];
			for (int y = 0; y < zone.Height; y++)
			for (int x = 0; x < zone.Width; x++)
			{
				int at = y * zone.Width + x; Cell cell = zone.GetCell(x, y);
				bool walks = KingdomRoads.Walkable(cell);
				passable[at] = walks ? (byte)1 : (byte)0;
				paved[at] = walks && KingdomRoads.AppliedState(cell)
					== KingdomRoadRules.WearState.Paved ? (byte)1 : (byte)0;
			}
			KingdomConstructionInputZoneObservation observed =
				new KingdomConstructionInputZoneObservation(settlement, zone.ZoneID,
					The.Game == null ? 0L : The.Game.TimeTicks, daily, zone.Width, zone.Height,
					passable, paved, lines.ToArray());
			if (!KingdomConstructionInputObservationRules.Valid(observed)
				|| !PublishInputObservation(system, observed, out failure))
			{
				failure = failure ?? "The attended source observation is malformed.";
				ForgetInputObservation(system, zone.ZoneID); return false;
			}
			return true;
		}

		private static bool ObserveInputWater(Zone zone, KingdomSurvey survey,
			List<KingdomConstructionInputObservationLine> into, HashSet<string> identities,
			out string failure)
		{
			failure = null;
			for (int i = 0; i < survey.Stores.Count; i++)
			{
				LiquidVolume water = survey.Stores[i];
				GameObject owner = water == null ? null : water.ParentObject;
				if (!GameObject.Validate(owner) || owner.CurrentCell == null
					|| owner.CurrentZone != zone || owner.GetIntProperty("KingdomStores") != 1
					|| !ReferenceEquals(owner.GetPart<LiquidVolume>(), water)
					|| KingdomPurpose.HasProtectedCargoEvidence(owner)
					|| owner.HasStringProperty(InputMarkerProperty)
					|| owner.HasIntProperty(InputMarkerProperty)
					|| !KingdomLiquids.HasFreshWater(water) || water.Volume <= 0) continue;
				string id = owner.IDIfAssigned;
				if (!AddObservedIdentity(id, id, identities, out failure)) return false;
				into.Add(new KingdomConstructionInputObservationLine(
					KingdomConstructionInputKind.Water,
					KingdomConstructionInputRules.WaterClassification, id, id,
					KingdomConstructionInputTopology.LiquidVessel, owner.CurrentCell.X,
					owner.CurrentCell.Y, owner.Blueprint, water.Volume, i, false, false));
				if (into.Count > KingdomConstructionInputObservationRules.MaxLines)
					return ObservationBound(out failure);
			}
			return true;
		}

		private static bool ObserveInputMaterials(Zone zone, KingdomSurvey survey,
			List<KingdomConstructionInputObservationLine> into, HashSet<string> identities,
			out string failure)
		{
			failure = null;
			for (int i = 0; i < survey.MaterialStockpiles.Count; i++)
			{
				GameObject holder = survey.MaterialStockpiles[i];
				if (!GameObject.Validate(holder) || holder.Inventory == null
					|| holder.CurrentCell == null || holder.CurrentZone != zone
					|| !KingdomMaterials.IsStockpile(holder)) continue;
				List<GameObject> held = new List<GameObject>(holder.Inventory.Objects);
				for (int j = 0; j < held.Count; j++)
				{
					GameObject item = held[j];
					bool valid = GameObject.Validate(item), protectedCargo = valid
						&& KingdomPurpose.HasProtectedCargoEvidence(item);
						if (!valid || item.Count <= 0 || item.IsImportant() || item.Equipped != null
							|| !item.IsTakeable() || item.HasTag("AlwaysStack")
							|| !KingdomOrdinaryCustody.TryProveEmpty(item, out string _)
						|| item.HasStringProperty(InputMarkerProperty)
						|| item.HasIntProperty(InputMarkerProperty)
						|| !ReferenceEquals(item.InInventory, holder)
						|| !protectedCargo && item.GetIntProperty("NeverStack") != 0
						|| !TryInputClassification(item, out KingdomConstructionInputKind kind,
							out string classification)) continue;
					string holderId = holder.IDIfAssigned, itemId = item.IDIfAssigned;
					if (!AddObservedIdentity(holderId, itemId, identities, out failure)) return false;
					into.Add(new KingdomConstructionInputObservationLine(kind, classification,
						holderId, itemId, KingdomConstructionInputTopology.ContainerInventory,
						holder.CurrentCell.X, holder.CurrentCell.Y, item.Blueprint, item.Count,
						i, item.HasTag("AlwaysStack"), protectedCargo));
					if (into.Count > KingdomConstructionInputObservationRules.MaxLines)
						return ObservationBound(out failure);
				}
			}
			return true;
		}

		private static bool AddObservedIdentity(string holder, string source,
			HashSet<string> identities, out string failure)
		{
			failure = null;
			if (string.IsNullOrEmpty(holder) || string.IsNullOrEmpty(source)
				|| !identities.Add(holder + "\0" + source))
			{
				failure = "Attended construction-input source identity is absent or ambiguous.";
				return false;
			}
			return true;
		}

		private static bool ObservationBound(out string failure)
		{
			failure = "Attended construction-input sources exceed their exact observation bound.";
			return false;
		}

		private static bool ActiveInputGround(Zone zone, KingdomSurvey survey)
		{
			return zone != null && survey != null && ReferenceEquals(survey.Ground, zone)
				&& The.ZoneManager != null && ReferenceEquals(The.ZoneManager.ActiveZone, zone)
				&& KingdomSurvey.ActiveFor(zone) == survey;
		}

		private static bool TryReadInputObservationBook(KingdomSystem system,
			out KingdomConstructionInputObservationBook book, out string failure)
		{
			book = null; failure = null;
			if (The.Game == null || system == null || !system.Founded
				|| string.IsNullOrEmpty(system.RealmId))
			{
				failure = "The durable construction-input observation owner is unavailable.";
				return false;
			}
			string raw = The.Game.GetStringGameState(InputObservationStateKey, "");
			if (string.IsNullOrEmpty(raw))
			{
				book = EmptyInputObservationBook(system); return true;
			}
			if (!KingdomConstructionInputObservationCodec.TryDecode(raw, out book))
			{
				failure = "The durable construction-input observation ledger is malformed.";
				return false;
			}
			if (book.RealmId != system.RealmId || book.RealmEpoch != system.FoundedTick)
				book = EmptyInputObservationBook(system);
			return true;
		}

		private static KingdomConstructionInputObservationBook EmptyInputObservationBook(
			KingdomSystem system)
		{
			return new KingdomConstructionInputObservationBook(
				KingdomConstructionInputObservationRules.Schema, system.RealmId,
				system.FoundedTick, new KingdomConstructionInputZoneObservation[0]);
		}

		private static bool PublishInputObservation(KingdomSystem system,
			KingdomConstructionInputZoneObservation observed, out string failure)
		{
			failure = null;
			KingdomConstructionInputObservationBook book;
			if (!TryReadInputObservationBook(system, out book, out failure)) return false;
			List<KingdomConstructionInputZoneObservation> zones =
				new List<KingdomConstructionInputZoneObservation>();
			for (int i = 0; i < book.ZoneCount; i++)
				if (book.ZoneAt(i).ZoneId != observed.ZoneId) zones.Add(book.ZoneAt(i));
			zones.Add(observed);
			zones.Sort((a, b) => string.CompareOrdinal(a.ZoneId, b.ZoneId));
			KingdomConstructionInputObservationBook next =
				new KingdomConstructionInputObservationBook(book.Schema, book.RealmId,
					book.RealmEpoch, zones.ToArray());
			if (!KingdomConstructionInputObservationCodec.TryEncode(next, out string encoded))
			{
				failure = "The construction-input observation ledger exceeds its durable bound.";
				return false;
			}
			The.Game.SetStringGameState(InputObservationStateKey, encoded);
			if (The.Game.GetStringGameState(InputObservationStateKey, "") != encoded)
			{
				failure = "The construction-input observation ledger write did not persist.";
				return false;
			}
			return true;
		}

		private static void ForgetInputObservation(KingdomSystem system, string zoneId)
		{
			KingdomConstructionInputObservationBook book; string ignored;
			if (!TryReadInputObservationBook(system, out book, out ignored)) return;
			List<KingdomConstructionInputZoneObservation> zones =
				new List<KingdomConstructionInputZoneObservation>();
			for (int i = 0; i < book.ZoneCount; i++)
				if (book.ZoneAt(i).ZoneId != zoneId) zones.Add(book.ZoneAt(i));
			KingdomConstructionInputObservationBook next =
				new KingdomConstructionInputObservationBook(book.Schema, book.RealmId,
					book.RealmEpoch, zones.ToArray());
			if (KingdomConstructionInputObservationCodec.TryEncode(next, out string encoded))
				The.Game.SetStringGameState(InputObservationStateKey, encoded);
		}
	}
}
