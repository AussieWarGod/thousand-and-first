using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRoads
	{
		// --- Reading and writing the zone's own record ------------------------------------

		/// <summary>The worn-ground tally of a zone. Never null; an unreadable or absent tally
		/// reads as an empty one and says so in the log.</summary>
		/// <param name="Z">The zone. Null yields an empty tally.</param>
		public static List<KingdomRoadRules.WornCell> ReadTally(Zone Z)
		{
			if (Z == null)
			{
				return new List<KingdomRoadRules.WornCell>();
			}
			if (!KingdomRoadRules.TryDecode(Z.GetZoneProperty(TallyProperty, null), out var cells, out var error) && error != null)
			{
				KingdomLog.Log(error);
			}
			return cells;
		}

		/// <summary>Writes a tally back to the zone. An empty tally writes the empty string, so a
		/// settlement nobody walks costs one short property and no bookkeeping.</summary>
		/// <param name="Z">The zone. Null does nothing.</param>
		/// <param name="Cells">The tally. Null writes the empty string.</param>
		public static void WriteTally(Zone Z, IList<KingdomRoadRules.WornCell> Cells)
		{
			if (Z == null)
			{
				return;
			}
			Z.SetZoneProperty(TallyProperty, KingdomRoadRules.Encode(Cells));
		}

		private static long ReadTick(Zone Z, string Property)
		{
			if (Z == null)
			{
				return 0L;
			}
			return long.TryParse(Z.GetZoneProperty(Property, null), out var ticks) ? ticks : 0L;
		}

		private static void WriteTick(Zone Z, string Property, long Ticks)
		{
			Z?.SetZoneProperty(Property, Ticks.ToString());
		}

		private static KingdomElapsedOptionDecision ObserveOption(KingdomSystem System,
			Zone Z, long Now)
		{
			string settlementId = KingdomChronicle.SettlementId(System);
			string realmId = System.CurrentRealmId;
			if (The.Game == null || !KingdomIdentityRules.IsSettlementId(settlementId)
				|| !KingdomIdentityRules.IsRealmId(realmId))
			{
				return KingdomElapsedOptionRules.Observe(
					KingdomElapsedOptionRecord.Unobserved, Enabled,
					System.MasterAppliedResumeToken, Now);
			}

			string globalKey = GlobalOptionStatePrefix + realmId;
			KingdomElapsedOptionRecord globalPrior;
			bool globalDecoded = KingdomElapsedOptionRules.TryDecode(
				The.Game.GetStringGameState(globalKey, ""), out globalPrior);
			if (!globalDecoded) globalPrior = KingdomElapsedOptionRecord.Unobserved;
			KingdomElapsedOptionDecision global = KingdomElapsedOptionRules.Observe(globalPrior,
				Enabled, System.MasterAppliedResumeToken, Now);
			if (!global.Valid)
			{
				global = KingdomElapsedOptionRules.Observe(
					KingdomElapsedOptionRecord.Unobserved, Enabled,
					System.MasterAppliedResumeToken, Now);
				globalDecoded = false;
			}
			string current = global.Valid
				? KingdomElapsedOptionRules.Encode(global.Record) : null;
			if (global.Valid && current != null && (!globalDecoded
				|| global.Transition != KingdomElapsedOptionTransition.None))
				The.Game.SetStringGameState(globalKey, current);

			bool ownerMatches = global::System.String.Equals(
				Z.GetZoneProperty(OptionOwnerProperty, null), settlementId,
				global::System.StringComparison.Ordinal);
			string encoded = ownerMatches
				? Z.GetZoneProperty(OptionStateProperty, null) : null;
			KingdomElapsedOptionRecord prior = KingdomElapsedOptionRecord.Unobserved;
			bool zoneDecoded = ownerMatches
				&& KingdomElapsedOptionRules.TryDecode(encoded, out prior);
			bool zoneMatches = zoneDecoded && global.Valid
				&& prior.State == global.Record.State
				&& prior.ObservedTick == global.Record.ObservedTick
				&& prior.MasterResumeToken == global.Record.MasterResumeToken;
			if (!zoneMatches && global.Valid && current != null)
			{
				return new KingdomElapsedOptionDecision(true, global.Record,
					global.Transition, Enabled ? KingdomElapsedOptionAction.AnchorEnabled
						: KingdomElapsedOptionAction.AnchorDisabled);
			}
			return global;
		}

		private static void CommitOption(KingdomSystem System, Zone Z,
			KingdomElapsedOptionRecord Record)
		{
			string settlementId = KingdomChronicle.SettlementId(System);
			if (Z == null || !KingdomIdentityRules.IsSettlementId(settlementId)) return;
			string current = KingdomElapsedOptionRules.Encode(Record);
			if (current == null) return;
			// The owned clock is written before this helper is called. State then owner:
			// a cut between these writes leaves a foreign owner and reanchors again.
			Z.SetZoneProperty(OptionStateProperty, current);
			Z.SetZoneProperty(OptionOwnerProperty, settlementId);
		}

		// --- Reading ground ---------------------------------------------------------------

		/// <summary>The floor this system laid on a cell, or null when it laid none.</summary>
		/// <param name="C">The cell. Null answers null.</param>
		public static GameObject OurFloor(Cell C)
		{
			GameObject floor;
			return FindOurFloor(C, out floor) == KingdomPhysicalLookupState.Exact ? floor : null;
		}

		/// <summary>Counts every loaded road-floor identity; duplicates and malformed shapes
		/// are ambiguous, never an absent floor that may be replaced.</summary>
		public static KingdomPhysicalLookupState FindOurFloor(Cell C, out GameObject Floor)
		{
			Floor = null;
			if (C == null) return KingdomPhysicalLookupState.Absent;
			int count = 0;
			bool exactShape = false;
			foreach (GameObject item in C.GetObjects())
			{
				if (item != null && item.GetIntProperty(PathStateProperty) > 0)
				{
					count++;
					if (count == 1)
					{
						Floor = item;
						int state = item.GetIntProperty(PathStateProperty);
						exactShape = GameObject.Validate(item) && item.CurrentCell == C
							&& state >= (int)KingdomRoadRules.WearState.Trodden
							&& state <= (int)KingdomRoadRules.WearState.Paved;
					}
				}
			}
			KingdomPhysicalLookupState result = KingdomConstructionRules.PhysicalLookupState(
				count, exactShape);
			if (result == KingdomPhysicalLookupState.Exact)
			{
				GameObject global;
				if (KingdomConstruction.FindExactId(C.ParentZone, Floor.ID, out global)
						!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(global, Floor))
					result = KingdomPhysicalLookupState.Ambiguous;
			}
			if (result != KingdomPhysicalLookupState.Exact) Floor = null;
			return result;
		}

		/// <summary>The rung a cell has already been brought to by this system.</summary>
		public static KingdomRoadRules.WearState AppliedState(Cell C)
		{
			GameObject floor;
			if (FindOurFloor(C, out floor) != KingdomPhysicalLookupState.Exact)
			{
				return KingdomRoadRules.WearState.Untouched;
			}
			int state = floor.GetIntProperty(PathStateProperty);
			if (state < (int)KingdomRoadRules.WearState.Untouched || state > (int)KingdomRoadRules.WearState.Paved)
			{
				return KingdomRoadRules.WearState.Untouched;
			}
			return (KingdomRoadRules.WearState)state;
		}

		/// <summary>
		/// Whether feet may be allowed to show on this cell at all.
		/// <para>
		/// Deliberately stricter than "empty": the ground must be what
		/// <c>KingdomPlots.ReadGround</c> calls bare &mdash; open ground, or a floor, or this
		/// system's own earlier work &mdash; it must hold no liquid, nothing on it may be owned
		/// by anybody, and it must lie outside every plot, because the floor inside a building
		/// belongs to the building. Anything else and the ground is left exactly as it is.
		/// </para>
		/// </summary>
		/// <param name="C">The cell. Null is never wearable.</param>
		/// <param name="Plots">Plots laid in this zone, from <c>KingdomPlots.ReadPlots</c>. Null
		/// reads as none laid.</param>
		public static bool Wearable(Cell C, IList<KingdomPlotRules.PlotRect> Plots)
		{
			if (C == null)
			{
				return false;
			}
			// Cheapest question first: the ground inside a plot belongs to the building standing
			// on it, and no cell of a plot is ever worn however many people cross it.
			if (Plots != null)
			{
				for (int i = 0; i < Plots.Count; i++)
				{
					if (Plots[i].Contains(C.X, C.Y))
					{
						return false;
					}
				}
			}
			if (KingdomPlots.ReadGround(C, out _) != KingdomPlotRules.GroundKind.Bare)
			{
				return false;
			}
			foreach (GameObject item in C.GetObjects())
			{
				if (item == null || item.IsCreature)
				{
					continue;
				}
				if (item.GetIntProperty(PathStateProperty) == (int)KingdomRoadRules.WearState.Paved)
				{
					// Paved ground is finished ground. It never accrues again, and nothing is
					// ever laid on top of it.
					return false;
				}
				if (item.IsOwned())
				{
					// ReadGround reads a floor before it reads ownership, so a floor somebody
					// else laid would come back bare. Nothing anyone's name is on is walked over.
					return false;
				}
			}
			return true;
		}

		/// <summary>Whether an errand may be walked through a cell. Solid things turn feet aside;
		/// people do not, because a settler standing in a lane is standing in a lane and will move
		/// off it.</summary>
		public static bool Walkable(Cell C)
		{
			if (C == null)
			{
				return false;
			}
			if (C.HasObjectWithPart("LiquidVolume"))
			{
				return false;
			}
			return C.IsPassable(null, IncludeCombatObjects: false);
		}

	}
}
