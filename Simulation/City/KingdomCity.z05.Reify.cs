using System;
using System.Collections.Generic;
using System.Diagnostics;

using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomCity
	{
		/// <summary>
		/// Model to ground: as much of what this zone owes as one turn's budget buys, paid onto
		/// real containers in dedication order, visible cells first.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.9, invariant I4, with &sect;3.5's budget over the top of
		/// it. A unit leaves the debt at the instant it LANDS, never at the instant it is scheduled,
		/// so re-entering, reloading or re-activating cannot pay the same debt twice. What the
		/// containers could not cover stays on the row and is told &mdash; never silently forgiven,
		/// and never silently repaired.
		/// </para>
		/// <para>
		/// <b>Visible cells first</b> is what makes the guarantee perceptual rather than merely
		/// amortised: what the founder is looking at catches up first, and the rest fills in behind
		/// them as they walk. Visibility is the engine's own answer &mdash; <c>Cell.IsVisible()</c>
		/// is <c>ParentZone.GetVisibility(X, Y)</c> (<c>D/XRL/World/Cell.cs:3490-3496</c>), the
		/// player's real field of view.
		/// </para>
		/// </summary>
		private static KingdomCityState Reify(KingdomSystem System, Zone Z, KingdomSurvey Survey, KingdomCityState state, int index, long TimeTicks, bool announce, out KingdomReifySpend spend)
		{
			spend = default(KingdomReifySpend);
			KingdomZoneRow row;
			if (!state.TryZone(index, out row))
			{
				return state;
			}
			ContainerGround ground = ContainerGround.Take(Survey);
			KingdomContainerDemandReceipt measured;
			KingdomCityFault fault;
			if (!KingdomContainerCatchUpRules.TryMeasure(ground.Rows, ground.Rows.Length,
				row.OwedWater, row.OwedFood, row.OwedMaterials, out measured, out fault))
			{
				Refuse("reify containers", fault);
				return state;
			}
			if (announce)
			{
				// Capacity beyond every real eligible container is named once and remains on the row.
				Tell(System, 0, 0,
					SignedRemainder(row.OwedWater, measured.WaterBlocked),
					SignedRemainder(row.OwedFood, measured.FoodBlocked));
			}
			Dictionary<int, GameObject> stations = KingdomStations.Index(Z);
			List<GameObject> posted = Posted(Z, Survey, stations);
			int visibleHeavyWanted = VisibleCount(posted);
			KingdomReifyDemand demand = new KingdomReifyDemand(
				visibleHeavyWanted,
				measured.VisibleUnits,
				0,
				posted.Count - visibleHeavyWanted,
				measured.RestUnits,
				0);
			if (demand.IsEmpty)
			{
				return state;
			}
			KingdomReifySpend planned;
			if (!KingdomCatchUpRules.TryPlanTurn(demand, Allowance(System, TimeTicks), HeavyAllowance(System, TimeTicks), out planned, out fault))
			{
				Refuse("reify", fault);
				return state;
			}
			int heavyVisible = (planned.Heavy < demand.VisibleHeavy) ? planned.Heavy : demand.VisibleHeavy;
			int mediumVisible = (planned.Medium < demand.VisibleMedium) ? planned.Medium : demand.VisibleMedium;
			int visibleHeavySpent = Anchor(Z, posted, stations, heavyVisible, 0, TimeTicks);
			int restHeavySpent = 0;
			KingdomContainerSettlement apply = delegate(int source, KingdomStockKind kind,
				KingdomUnitDirection direction, int offered, out int applied)
			{
				return SettleContainer(System, Survey, ground, source, kind, direction, offered, out applied);
			};
			KingdomContainerSettlementReceipt visibleSettlement;
			if (!KingdomContainerCatchUpRules.TrySettle(ground.Rows, ground.Rows.Length,
				row.OwedWater, row.OwedFood, row.OwedMaterials,
				mediumVisible, 0, apply, out visibleSettlement, out fault))
			{
				Refuse("reify visible containers", fault);
				return state;
			}
			int heavyRest = planned.Heavy - heavyVisible;
			if (!visibleSettlement.CallbackFailed)
			{
				restHeavySpent = Anchor(Z, posted, stations, 0, heavyRest, TimeTicks);
			}
			KingdomContainerSettlementReceipt restSettlement = visibleSettlement;
			if (!visibleSettlement.CallbackFailed
				&& !KingdomContainerCatchUpRules.TrySettle(ground.Rows, ground.Rows.Length,
					visibleSettlement.OwedWater, visibleSettlement.OwedFood,
					visibleSettlement.OwedMaterials, 0, planned.Medium - mediumVisible,
					apply, out restSettlement, out fault))
			{
				Refuse("reify containers", fault);
				return state;
			}
			int mediumSpent = visibleSettlement.UnitsSpent + restSettlement.UnitsSpent;
			// The second receipt is for its own call only; when it ran, replace rather than add the
			// first call's carried debt but add both measured unit counts.
			if (visibleSettlement.CallbackFailed)
			{
				restSettlement = visibleSettlement;
				mediumSpent = visibleSettlement.UnitsSpent;
			}
			int heavySpent = visibleHeavySpent + restHeavySpent;
			int visibleSpent = visibleHeavySpent + visibleSettlement.VisibleSpent;
			spend = new KingdomReifySpend(heavySpent, mediumSpent, 0, visibleSpent,
				(heavySpent + mediumSpent) * KingdomCatchUpRules.ThirdsPerUnit);
			Charge(System, TimeTicks, spend);
			int water = restSettlement.OwedWater;
			int food = restSettlement.OwedFood;
			int materials = restSettlement.OwedMaterials;
			int fetched = row.OwedWater - water;
			if (fetched > 0) System.Ledger.Fetched += fetched;
			KingdomCityState written;
			if (!state.TryWithZone(index, row.WithOwed(water, food, materials), out written, out fault))
			{
				Refuse("reify", fault);
				return state;
			}
			// A debt that is DRAINING is not a shortfall, and saying so on each of the thirty-nine
			// turns a full backlog takes would fill the founder's report with the sound of the
			// thing working (STANDARDS 7b). What could not be covered was already said above, once.
			Tell(System, row.OwedWater - water, row.OwedFood - food, 0, 0);
			return written;
		}

	}
}
