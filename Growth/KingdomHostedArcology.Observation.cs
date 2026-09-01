using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	/// <summary>Attended hosted-floor observation and dated exterior consumption. No method in
	/// this shard opens an interior zone.</summary>
	public static partial class KingdomHostedArcology
	{
		internal static bool TryCurrentObservation(GameObject Work, string LotKey,
			out KingdomHostedObservation Observation, out string Failure)
		{
			Observation = null; Failure = null;
			r_KingdomArcology root = Work?.GetPart<r_KingdomArcology>();
			if (!GameObject.Validate(Work) || root == null
				|| string.IsNullOrEmpty(Work.IDIfAssigned) || !IsOperationalPure(Work))
				return ObservationFail("the hosted shell is not currently operational", out Failure);
			KingdomHostedLotReceipt receipt;
			if (!TryReceipt(root, LotKey, out receipt, out Failure) || receipt == null
				|| receipt.Phase != KingdomHostedLotPhase.Active)
				return ObservationFail(Failure ?? "the hosted lot has no active receipt", out Failure);
			string revision = KingdomHostedArcologyRules.ReceiptRevision(receipt);
			if (!DepartureAllows(The.Game?.GetSystem<KingdomSystem>(), Work,
				LotKey, revision, out Failure)) return false;
			if (!TryObservation(root, LotKey, out Observation, out Failure)) return false;
			if (Observation == null) return true;
			if (!TryInteriorZoneIdentity(Work, LotKey, Observation.InteriorZoneId,
				out Failure)) return false;
			if (!KingdomHostedArcologyTopology.TryHostedLotCoordinate(LotKey,
				out KingdomArcologyCoordinate at))
				return ObservationFail("the hosted lot has no exact topology slot", out Failure);
			string anchor = KingdomHostedArcologyRules.StableChildId(Work.IDIfAssigned,
				KingdomHostedArcologyTopology.StableRole(at.X, at.Y, at.Z, "anchor"));
			return KingdomHostedArcologySlateRules.Matches(Observation,
				Work.IDIfAssigned, LotKey,
				revision,
				Observation.InteriorZoneId, anchor, The.Game?.TimeTicks ?? 0L, out Failure);
		}

		internal static bool TryWardPhysical(GameObject Work, out int Roof,
			out int Luxury, out string Failure)
		{
			Roof = 0; Luxury = 0;
			if (!TryCurrentObservation(Work, KingdomHostedArcologyTopology.WardLotKey,
				out KingdomHostedObservation observation, out Failure)) return false;
			if (observation == null || !string.IsNullOrEmpty(observation.Fault)) return true;
			Roof = observation.Roof; Luxury = observation.Luxury; return true;
		}

		internal static bool TryTerracePhysicalFood(GameObject Work, bool FreshWaterAvailable,
			out int Food, out string Failure)
		{
			Food = 0;
			if (!TryCurrentObservation(Work, KingdomHostedArcologyTopology.TerraceLotKey,
				out KingdomHostedObservation observation, out Failure)) return false;
			if (observation == null || !string.IsNullOrEmpty(observation.Fault)) return true;
			r_KingdomArcology root = Work.GetPart<r_KingdomArcology>();
			KingdomHostedLotReceipt receipt;
			if (!TryReceipt(root, KingdomHostedArcologyTopology.TerraceLotKey,
				out receipt, out Failure) || receipt == null) return false;
			if (receipt.RequiresWater && !FreshWaterAvailable) return true;
			Food = observation.Food; return true;
		}

		internal static string ObservationStatus(GameObject Work, string LotKey)
		{
			if (!TryCurrentObservation(Work, LotKey, out KingdomHostedObservation row,
				out string failure)) return "active; physical observation unavailable ("
					+ (failure ?? "unknown mismatch") + ")";
			if (row == null) return "active; physical output unobserved — visit its hosted floor";
			string age = AgeClause(row.ObservedTick);
			if (!string.IsNullOrEmpty(row.Fault))
				return "active; physical output missing (" + row.Fault + "); " + age;
			KingdomHostedLotDefinition definition;
			if (!KingdomHostedArcologyRules.TryHostedLot(LotKey, out definition))
				return "active; physical contract missing; " + age;
			if (LotKey == KingdomHostedArcologyTopology.WardLotKey)
				return "active; physical roof " + AmountStatus(row.Roof, Cap(definition, "roof"))
					+ ", physical luxury " + AmountStatus(row.Luxury, Cap(definition, "luxury"))
					+ "; " + age;
			return "active; physical food " + AmountStatus(row.Food, Cap(definition, "food"))
				+ "; credited only while fresh-water flow reaches it; " + age;
		}

		private static KingdomHostedObservation NewObservation(
			KingdomHostedLiveContext Context, long Tick)
		{
			return new KingdomHostedObservation { RootId = Context.Shell.IDIfAssigned,
				LotKey = Context.Receipt.LotKey, ReceiptRevision = Context.Revision,
				InteriorZoneId = Context.Zone.ZoneID,
				AnchorId = Context.AnchorObject.IDIfAssigned, ObservedTick = Math.Max(0L, Tick) };
		}

		private static int ObserveTerraceFood(KingdomHostedLiveContext Context)
		{
			long rows = 0L;
			for (int i = 0; i < Context.Fixtures.Length; i++)
			{
				KingdomArcologyFixtureSpec fixture = Context.Fixtures[i];
				if (fixture.Blueprint != Context.Definition.PhysicalProducerBlueprint) continue;
				string id = KingdomHostedArcologyVisual.FixtureId(Context.Shell.IDIfAssigned,
					Context.Anchor, fixture);
				GameObject item;
				if (KingdomConstruction.FindExactId(Context.Zone, id, out item)
					!= KingdomPhysicalLookupState.Exact || !GameObject.Validate(item)
					|| item.IsBroken() || item.Blueprint != fixture.Blueprint
					|| item.CurrentCell != Context.Zone.GetCell(fixture.X, fixture.Y)) continue;
				if (int.TryParse(item.GetTag("r_TAF_HostedCropRows", ""), out int amount)
					&& amount > 0) rows += amount;
			}
			int bounded = rows >= int.MaxValue ? int.MaxValue : (int)rows;
			int physical = KingdomHostedArcologyRules.PhysicalFoodForRows(bounded,
				KingdomCropRules.YieldPerRow, KingdomCropRules.CropDays);
			return Math.Min(physical, Cap(Context.Definition,
				KingdomCatalogueRules.SupportFood));
		}

		private static bool TryObservation(r_KingdomArcology Root, string LotKey,
			out KingdomHostedObservation Observation, out string Failure)
		{
			Observation = null;
			if (!TryObservationSlate(Root, out List<KingdomHostedObservation> rows, out Failure))
				return false;
			for (int i = 0; i < rows.Count; i++)
				if (rows[i].LotKey == LotKey) Observation = rows[i].Copy();
			return true;
		}

		private static bool TryObservationSlate(r_KingdomArcology Root,
			out List<KingdomHostedObservation> Observations, out string Failure)
		{
			Observations = null;
			GameObject owner = Root?.ParentObject;
			if (Root == null || Root.LotObservations == null || !GameObject.Validate(owner))
				return ObservationFail("the hosted observation slate has no exact shell",
					out Failure);
			return KingdomHostedArcologySlateRules.TryReadObservations(
				Root.LotObservations, owner.IDIfAssigned, out Observations, out Failure);
		}

		private static bool SetObservation(r_KingdomArcology Root,
			KingdomHostedObservation Observation, out string Failure)
		{
			Failure = null;
			string encoded = KingdomHostedArcologyReceiptCodec.EncodeObservation(Observation);
			if (string.IsNullOrEmpty(encoded)
				|| !TryObservationSlate(Root, out List<KingdomHostedObservation> rows, out Failure))
				return false;
			List<string> next = new List<string>(); bool replaced = false;
			for (int i = 0; i < rows.Count; i++)
				if (rows[i].LotKey == Observation.LotKey)
				{
					next.Add(encoded); replaced = true;
				}
				else next.Add(Root.LotObservations[i]);
			if (!replaced) next.Add(encoded);
			if (next.Count > KingdomHostedArcologyRules.MaxHostedLots)
				return ObservationFail("the hosted observation slate is full", out Failure);
			Root.LotObservations = next;
			return TryObservation(Root, Observation.LotKey,
				out KingdomHostedObservation proved, out Failure) && proved != null
				&& KingdomHostedArcologyReceiptCodec.EncodeObservation(proved) == encoded;
		}

		private static bool PersistObserved(r_KingdomArcology Root,
			KingdomHostedObservation Observation)
		{
			if (!SetObservation(Root, Observation, out string failure))
			{
				Quarantine(Root, failure); KingdomLog.Log("hosted observation failed: " + failure);
				return false;
			}
			return true;
		}

		private static int Cap(KingdomHostedLotDefinition Definition, string Kind)
		{
			return KingdomHostedArcologyRules.ContractCap(Definition, Kind);
		}

		private static string AmountStatus(int Amount, int Cap)
		{
			int missing = Math.Max(0, Cap - Amount);
			return Amount + "/" + Cap + (missing > 0 ? " (missing " + missing + ")" : "");
		}

		private static string AgeClause(long Tick)
		{
			int days = KingdomHostedArcologySlateRules.AgeDays(Tick,
				The.Game?.TimeTicks ?? 0L, KingdomRules.TicksPerDay);
			return days == 0 ? "observed this visit" : "observed " + days
				+ (days == 1 ? " day ago" : " days ago");
		}

		private static string Bound(string Text)
		{
			if (string.IsNullOrEmpty(Text)) return "ambiguous physical observation";
			return Text.Length <= 512 ? Text : Text.Substring(0, 512);
		}

		private static bool ObservationFail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
