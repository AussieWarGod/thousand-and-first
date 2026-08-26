using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The catch-up counter: a quantity of owed work, never a queue of dated jobs.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.5 takes three properties from <c>ZoneRepair</c> outright —
	/// the counter is a quantity so a season of absence and a day of absence differ in an integer
	/// and never in shape; <c>Math.Max(1, ...)</c> guarantees forward progress so a debt cannot
	/// stall on a rounding edge; a drained counter costs literally nothing — and refuses the
	/// fourth: <c>ZoneRepair</c> applies its whole backlog in one activation, and we spend ours on
	/// a per-turn budget instead. That single change is the whole of the amortisation.
	/// </para>
	/// <para>
	/// Pure and engine-free, and the intake is derived from two stamps rather than accumulated from
	/// events, which is why recomputing it at every activation is free and idempotent.
	/// </para>
	/// </summary>
	internal static class KingdomCatchUpRules
	{
		/// <summary>LIVING-CITY-ARCHITECTURE §0.0(b): a light unit is a third of a unit, so the
		/// whole budget is counted in thirds and nothing anywhere rounds.</summary>
		internal const int ThirdsPerUnit = 3;

		/// <summary>LIVING-CITY-ARCHITECTURE §0.0(b): eight units a turn.</summary>
		internal const int BudgetThirdsPerTurn = KingdomBudgetRules.ReifyUnitsPerTurn * ThirdsPerUnit;

		/// <summary>
		/// LIVING-CITY-ARCHITECTURE §0.0(b): one legal City zone can hold 220 commissioned
		/// root containers plus 24 water and eight food vessels dedicated before them, and sixty
		/// resident bodies. Derived from live rails, not the retired forty-work envelope.
		/// </summary>
		internal static readonly int WorstBacklogUnits =
			KingdomRules.MaxCivicContainersPerZone + KingdomRules.MaxPopulation;

		/// <summary>LIVING-CITY-ARCHITECTURE §0.1 / §0.0(b): the grace window vanilla keeps a
		/// departed zone live for. The worst backlog must drain inside it.</summary>
		internal const int GraceWindowTurns = 40;

		/// <summary>The weight of one unit, in thirds. LIVING-CITY-ARCHITECTURE §0.0(b).</summary>
		internal static int WeightThirds(KingdomUnitWeight weight)
		{
			return (weight == KingdomUnitWeight.Light) ? 1 : ThirdsPerUnit;
		}

		/// <summary>Which way a unit moves goods, as a sign. LIVING-CITY-ARCHITECTURE §3.9.</summary>
		internal static int Sign(KingdomUnitDirection direction)
		{
			return (direction == KingdomUnitDirection.Draw) ? -1 : 1;
		}

		/// <summary>
		/// The debt one zone walks in owing, from the two stamps that describe it.
		/// <para>
		/// <c>ZoneRepair</c>'s shape exactly (<c>D/XRL/World/ZoneParts/ZoneRepair.cs:51-58</c>):
		/// nothing is owed below one unit's worth of elapsed, and above it the count is the floor
		/// of the division with <c>Math.Max(1, ...)</c> standing guard over the rounding edge.
		/// Derived, not accumulated — so recomputing it at <c>ZoneActivatedEvent</c> after a
		/// <c>ZoneThawedEvent</c> intake produces the same number rather than twice the debt.
		/// </para>
		/// </summary>
		internal static bool TryIntakeUnits(long lastReadTick, long processedThroughTick, long ticksPerUnit, out long unitsOwed, out KingdomCityFault fault)
		{
			unitsOwed = 0L;
			if (ticksPerUnit <= 0L)
			{
				fault = KingdomCityFault.InvalidInterval;
				return false;
			}
			KernelFaultCode kernelFault;
			if (!TickMath.TryValidateAdvance(lastReadTick, processedThroughTick, out kernelFault))
			{
				fault = KingdomCityFaults.FromKernel(kernelFault);
				return false;
			}
			long elapsed = processedThroughTick - lastReadTick;
			fault = KingdomCityFault.None;
			if (elapsed < ticksPerUnit)
			{
				return true;
			}
			long units = elapsed / ticksPerUnit;
			unitsOwed = (units < 1L) ? 1L : units;
			return true;
		}

		/// <summary>Weighted thirds for a bag of units of each tier.</summary>
		internal static bool TryWeigh(int heavy, int medium, int light, out int thirds, out KingdomCityFault fault)
		{
			thirds = 0;
			if (heavy < 0 || medium < 0 || light < 0)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			long total = (long)heavy * ThirdsPerUnit + (long)medium * ThirdsPerUnit + (long)light;
			if (total > int.MaxValue)
			{
				fault = KingdomCityFault.ArithmeticOverflow;
				return false;
			}
			thirds = (int)total;
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// Turns to drain a weighted backlog at the per-turn budget. LIVING-CITY-ARCHITECTURE
		/// &sect;0.0(b): the live 312-unit envelope is 39 turns, still inside the 40 turns vanilla
		/// keeps a departed zone live.
		/// </summary>
		internal static bool TryTurnsToDrain(int owedThirds, out int turns, out KingdomCityFault fault)
		{
			turns = 0;
			if (owedThirds < 0)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			fault = KingdomCityFault.None;
			if (owedThirds == 0)
			{
				return true;
			}
			turns = ((owedThirds - 1) / BudgetThirdsPerTurn) + 1;
			return true;
		}

		/// <summary>
		/// One turn's spend: visible cells first, then the rest, stopping at the budget and not at
		/// the debt. LIVING-CITY-ARCHITECTURE &sect;3.5.
		/// <para>
		/// The heavy cap is spent across both halves rather than per half, because four body mints
		/// is a frame-cost ceiling and not an ordering preference.
		/// </para>
		/// </summary>
		internal static bool TryPlanTurn(KingdomReifyDemand demand, out KingdomReifySpend spend, out KingdomCityFault fault)
		{
			return TryPlanTurn(demand, BudgetThirdsPerTurn, KingdomBudgetRules.ReifyHeavyMintsPerTurn, out spend, out fault);
		}

		/// <summary>
		/// The same plan against an allowance that is already partly spent.
		/// <para>
		/// The budget is <b>per turn</b>, not per call site (&sect;0.0): the homecoming pass, the
		/// pump and the prefetch all reify on the same turn, and three call sites each taking a full
		/// eight units would be twenty-four. So the turn's remainder is handed in, and what is left
		/// after this spend is the caller's to carry to the next zone.
		/// </para>
		/// </summary>
		internal static bool TryPlanTurn(KingdomReifyDemand demand, int thirdsAvailable, int heavyAvailable, out KingdomReifySpend spend, out KingdomCityFault fault)
		{
			spend = default(KingdomReifySpend);
			if (thirdsAvailable < 0 || heavyAvailable < 0 || thirdsAvailable > BudgetThirdsPerTurn
				|| heavyAvailable > KingdomBudgetRules.ReifyHeavyMintsPerTurn)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (demand.VisibleHeavy < 0 || demand.VisibleMedium < 0 || demand.VisibleLight < 0
				|| demand.RestHeavy < 0 || demand.RestMedium < 0 || demand.RestLight < 0)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			int thirds = thirdsAvailable;
			int heavyLeft = heavyAvailable;
			int heavy = 0;
			int medium = 0;
			int light = 0;
			int visible = 0;

			int take = Take(demand.VisibleHeavy, Math.Min(heavyLeft, thirds / ThirdsPerUnit));
			heavy += take;
			heavyLeft -= take;
			thirds -= take * ThirdsPerUnit;
			visible += take;

			take = Take(demand.VisibleMedium, thirds / ThirdsPerUnit);
			medium += take;
			thirds -= take * ThirdsPerUnit;
			visible += take;

			take = Take(demand.VisibleLight, thirds);
			light += take;
			thirds -= take;
			visible += take;

			take = Take(demand.RestHeavy, Math.Min(heavyLeft, thirds / ThirdsPerUnit));
			heavy += take;
			heavyLeft -= take;
			thirds -= take * ThirdsPerUnit;

			take = Take(demand.RestMedium, thirds / ThirdsPerUnit);
			medium += take;
			thirds -= take * ThirdsPerUnit;

			take = Take(demand.RestLight, thirds);
			light += take;
			thirds -= take;

			spend = new KingdomReifySpend(heavy, medium, light, visible, thirdsAvailable - thirds);
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// A unit leaves the debt at the instant it lands, never at the instant it is scheduled —
		/// <c>ZoneRepair</c>'s "debt cleared on read", applied per unit instead of per batch. That
		/// is what makes re-entering, reloading or re-activating unable to re-land a unit.
		/// </summary>
		internal static bool TrySettle(KingdomCatchUpCounter counter, KingdomUnitDirection direction, KingdomUnitWeight weight, out KingdomCatchUpCounter next, out KingdomCityFault fault)
		{
			next = counter;
			if (counter.LandThirds < 0 || counter.DrawThirds < 0)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			int cost = WeightThirds(weight);
			if (direction == KingdomUnitDirection.Land)
			{
				if (counter.LandThirds < cost)
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				next = new KingdomCatchUpCounter(counter.LandThirds - cost, counter.DrawThirds);
			}
			else
			{
				if (counter.DrawThirds < cost)
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				next = new KingdomCatchUpCounter(counter.LandThirds, counter.DrawThirds - cost);
			}
			fault = KingdomCityFault.None;
			return true;
		}

		private static int Take(int wanted, int affordable)
		{
			if (wanted <= 0 || affordable <= 0)
			{
				return 0;
			}
			return (wanted < affordable) ? wanted : affordable;
		}
	}
}
