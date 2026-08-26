#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The catch-up counter and the per-turn budget. LIVING-CITY-ARCHITECTURE §3.5 and §0.0(b):
	/// entry must cost O(budget) and never O(elapsed), and the counter is a quantity of owed work
	/// rather than a queue of dated jobs — so a season of absence and a day of absence differ in
	/// an integer and never in shape.
	/// </summary>
	public class KingdomCatchUpRulesTests
	{
		private const long Day = ThousandAndFirst.KingdomRules.TicksPerDay;

		/// <summary>ZoneRepair's own shape: nothing owed below one unit's worth of elapsed, and the
		/// floor of the division above it.</summary>
		[TestCase(0L, 0L, 100L, 0L)]
		[TestCase(0L, 99L, 100L, 0L)]
		[TestCase(0L, 100L, 100L, 1L)]
		[TestCase(0L, 999L, 100L, 9L)]
		[TestCase(500L, 1500L, 100L, 10L)]
		[TestCase(0L, 108000L, 1200L, 90L)]
		public void IntakeIsTheElapsedDividedByAUnitsWorth(long lastRead, long processedThrough, long ticksPerUnit, long expected)
		{
			long owed;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCatchUpRules.TryIntakeUnits(lastRead, processedThrough, ticksPerUnit, out owed, out fault));
			Assert.AreEqual(expected, owed);
		}

		/// <summary>
		/// Derived from two stamps, never accumulated from events — which is exactly why §3.5 can
		/// recompute it at ZoneActivatedEvent after a ZoneThawedEvent intake and get the same
		/// number rather than twice the debt.
		/// </summary>
		[Test]
		public void IntakeIsIdempotentBecauseItIsDerivedAndNotAccumulated()
		{
			long first;
			long second;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCatchUpRules.TryIntakeUnits(1000L, 61000L, 600L, out first, out fault));
			Assert.IsTrue(KingdomCatchUpRules.TryIntakeUnits(1000L, 61000L, 600L, out second, out fault));
			Assert.AreEqual(first, second);
			Assert.AreEqual(100L, first);
		}

		[Test]
		public void IntakeRefusesABackwardClockAndAZeroUnit()
		{
			long owed;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomCatchUpRules.TryIntakeUnits(5000L, 4000L, 100L, out owed, out fault));
			Assert.AreEqual(KingdomCityFault.ClockRegression, fault);
			Assert.IsFalse(KingdomCatchUpRules.TryIntakeUnits(0L, 4000L, 0L, out owed, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidInterval, fault);
			Assert.AreEqual(0L, owed);
		}

		/// <summary>LIVING-CITY-ARCHITECTURE §0.0(b): a light unit is a third, and everything else
		/// is whole. A light tier that weighed a whole unit would put a farm's eighty plants at
		/// ten turns instead of four.</summary>
		[TestCase((int)KingdomUnitWeight.Heavy, 3)]
		[TestCase((int)KingdomUnitWeight.Medium, 3)]
		[TestCase((int)KingdomUnitWeight.Light, 1)]
		public void TheWeightsAreTheConstitutionsOwn(int weight, int expected)
		{
			Assert.AreEqual(expected, KingdomCatchUpRules.WeightThirds((KingdomUnitWeight)weight));
		}

		[TestCase((int)KingdomUnitDirection.Land, 1)]
		[TestCase((int)KingdomUnitDirection.Draw, -1)]
		public void TheCounterIsSigned(int direction, int expected)
		{
			Assert.AreEqual(expected, KingdomCatchUpRules.Sign((KingdomUnitDirection)direction));
		}

		/// <summary>
		/// The figures §0.0(b) actually argues with: a farm's eighty plants in four turns, a
		/// Joppa-scale three hundred in thirteen, and the live worst backlog a zone can owe — 220
		/// root containers plus the 24+8 manual allowances and sixty bodies — in thirty-nine,
		/// inside vanilla's forty-turn
		/// zone live.
		/// </summary>
		[TestCase(0, 0, 80, 4)]
		[TestCase(0, 0, 300, 13)]
		[TestCase(312, 0, 0, 39)]
		[TestCase(0, 8, 0, 1)]
		[TestCase(0, 9, 0, 2)]
		[TestCase(0, 0, 0, 0)]
		public void ABacklogDrainsInTheTurnsTheConstitutionCounted(int heavy, int medium, int light, int expectedTurns)
		{
			int thirds;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCatchUpRules.TryWeigh(heavy, medium, light, out thirds, out fault));
			int turns;
			Assert.IsTrue(KingdomCatchUpRules.TryTurnsToDrain(thirds, out turns, out fault));
			Assert.AreEqual(expectedTurns, turns);
		}

		[Test]
		public void TheWorstBacklogDrainsInsideTheGraceWindow()
		{
			int thirds;
			int turns;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCatchUpRules.TryWeigh(KingdomCatchUpRules.WorstBacklogUnits, 0, 0, out thirds, out fault));
			Assert.IsTrue(KingdomCatchUpRules.TryTurnsToDrain(thirds, out turns, out fault));
			Assert.AreEqual(39, turns);
			Assert.Less(turns, KingdomCatchUpRules.GraceWindowTurns, "the worst backlog outlived vanilla's own grace window");
			Assert.AreEqual(KingdomBudgetVerdict.Within, KingdomBudgetRules.JudgeCount(KingdomBudgetLane.CatchUpDrain, turns));
			Assert.AreEqual(KingdomBudgetVerdict.Warn, KingdomBudgetRules.JudgeCount(KingdomBudgetLane.CatchUpDrain, 41L));
		}

		[Test]
		public void ANegativeBagIsRefusedRatherThanWeighedAsNothing()
		{
			int thirds;
			int turns;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomCatchUpRules.TryWeigh(-1, 0, 0, out thirds, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidIndex, fault);
			Assert.IsFalse(KingdomCatchUpRules.TryTurnsToDrain(-1, out turns, out fault));
		}

		// ---- The per-turn spend ----------------------------------------------------------

		/// <summary>Eight units a turn, of which at most four may be body mints. LIVING-CITY-
		/// ARCHITECTURE §0.0(b).</summary>
		[Test]
		public void TheTurnSpendsEightUnitsOfWhichAtMostFourAreMints()
		{
			KingdomReifySpend spend;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCatchUpRules.TryPlanTurn(new KingdomReifyDemand(20, 20, 20, 20, 20, 20), out spend, out fault));
			Assert.AreEqual(KingdomBudgetRules.ReifyHeavyMintsPerTurn, spend.Heavy, "the heavy cap moved");
			Assert.AreEqual(KingdomCatchUpRules.BudgetThirdsPerTurn, spend.ThirdsSpent, "the turn did not spend its whole budget");
			Assert.AreEqual(KingdomBudgetRules.ReifyUnitsPerTurn, spend.Heavy + spend.Medium, "heavy plus medium overran the unit budget");
			Assert.AreEqual(KingdomBudgetVerdict.Within, KingdomBudgetRules.JudgeCount(KingdomBudgetLane.Reify, spend.ThirdsSpent / KingdomCatchUpRules.ThirdsPerUnit));
		}

		/// <summary>Twenty-four light units a turn, so a home farm's eighty plants materialise in
		/// four turns. LIVING-CITY-ARCHITECTURE §0.0(b).</summary>
		[Test]
		public void APureLightBacklogSpendsTwentyFourUnitsATurn()
		{
			KingdomReifySpend spend;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCatchUpRules.TryPlanTurn(new KingdomReifyDemand(0, 0, 0, 0, 0, 80), out spend, out fault));
			Assert.AreEqual(KingdomBudgetRules.ReifyLightUnitsPerTurn, spend.Light);
			Assert.AreEqual(0, spend.Heavy);
			Assert.AreEqual(KingdomCatchUpRules.BudgetThirdsPerTurn, spend.ThirdsSpent);
		}

		/// <summary>What the founder is looking at catches up first, and the rest fills in behind
		/// them as they walk. LIVING-CITY-ARCHITECTURE §3.5.</summary>
		[Test]
		public void VisibleCellsAreSpentBeforeAnythingElse()
		{
			KingdomReifySpend spend;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCatchUpRules.TryPlanTurn(new KingdomReifyDemand(0, 5, 0, 0, 50, 0), out spend, out fault));
			Assert.AreEqual(5, spend.Visible, "the visible half was not spent first");
			Assert.AreEqual(8, spend.Medium);
		}

		[Test]
		public void TheSpendStopsAtTheBudgetAndNotAtTheDebt()
		{
			KingdomReifySpend spend;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCatchUpRules.TryPlanTurn(new KingdomReifyDemand(0, 0, 0, 0, 1000, 0), out spend, out fault));
			Assert.AreEqual(KingdomBudgetRules.ReifyUnitsPerTurn, spend.Units);
		}

		[Test]
		public void AnEmptyDemandSpendsNothingAtAll()
		{
			KingdomReifyDemand demand = new KingdomReifyDemand(0, 0, 0, 0, 0, 0);
			Assert.IsTrue(demand.IsEmpty);
			KingdomReifySpend spend;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCatchUpRules.TryPlanTurn(demand, out spend, out fault));
			Assert.AreEqual(0, spend.Units);
			Assert.AreEqual(0, spend.ThirdsSpent, "a caught-up zone costs literally nothing");
		}

		[Test]
		public void ANegativeDemandIsRefused()
		{
			KingdomReifySpend spend;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomCatchUpRules.TryPlanTurn(new KingdomReifyDemand(0, -1, 0, 0, 0, 0), out spend, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidIndex, fault);
		}

		// ---- Settling a unit -------------------------------------------------------------

		/// <summary>A unit leaves the debt at the instant it lands, not the instant it is
		/// scheduled — so re-entering, reloading or re-activating cannot re-land it.</summary>
		[Test]
		public void SettlingAUnitTakesItOffTheCounterInItsOwnDirection()
		{
			KingdomCatchUpCounter counter = new KingdomCatchUpCounter(9, 6);
			Assert.AreEqual(15, counter.OwedThirds);
			Assert.AreEqual(3, counter.Net);
			KingdomCatchUpCounter next;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCatchUpRules.TrySettle(counter, KingdomUnitDirection.Land, KingdomUnitWeight.Medium, out next, out fault));
			Assert.AreEqual(6, next.LandThirds);
			Assert.AreEqual(6, next.DrawThirds, "settling a land touched the draws");
			Assert.IsTrue(KingdomCatchUpRules.TrySettle(next, KingdomUnitDirection.Draw, KingdomUnitWeight.Light, out next, out fault));
			Assert.AreEqual(5, next.DrawThirds);
			Assert.AreEqual(11, next.OwedThirds);
		}

		[Test]
		public void SettlingMoreThanIsOwedIsRefusedRatherThanGoingNegative()
		{
			KingdomCatchUpCounter counter = new KingdomCatchUpCounter(1, 0);
			KingdomCatchUpCounter next;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomCatchUpRules.TrySettle(counter, KingdomUnitDirection.Land, KingdomUnitWeight.Heavy, out next, out fault));
			Assert.AreEqual(1, next.LandThirds, "a refused settle moved the counter anyway");
			Assert.IsFalse(KingdomCatchUpRules.TrySettle(counter, KingdomUnitDirection.Draw, KingdomUnitWeight.Light, out next, out fault));
		}

		[Test]
		public void ADrainedCounterSaysSo()
		{
			Assert.IsTrue(new KingdomCatchUpCounter(0, 0).IsSettled);
			Assert.IsFalse(new KingdomCatchUpCounter(0, 1).IsSettled);
			Assert.IsFalse(new KingdomCatchUpCounter(1, 0).IsSettled);
		}

		/// <summary>Landing and drawing the same amount nets to nothing and still owes both halves:
		/// the net is what the zone row persists, and the split is what I1 is a statement about.</summary>
		[Test]
		public void TheNetIsNotTheDebt()
		{
			KingdomCatchUpCounter counter = new KingdomCatchUpCounter(12, 12);
			Assert.AreEqual(0, counter.Net);
			Assert.AreEqual(24, counter.OwedThirds);
			Assert.IsFalse(counter.IsSettled, "a netted-out counter reported itself drained");
		}

		/// <summary>
		/// The budget is per TURN and not per call site. The homecoming pass, the pump and the
		/// prefetch all reify on the same turn, so a plan that ignored what was already spent would
		/// let three call sites take twenty-four units and still report eight.
		/// </summary>
		[Test]
		public void TheTurnsAllowanceIsSharedAcrossEveryCallSite()
		{
			KingdomReifyDemand demand = new KingdomReifyDemand(0, 8, 0, 0, 8, 0);
			KingdomReifySpend first;
			KingdomReifySpend second;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCatchUpRules.TryPlanTurn(demand, out first, out fault));
			Assert.AreEqual(KingdomBudgetRules.ReifyUnitsPerTurn, first.Units);
			Assert.AreEqual(KingdomCatchUpRules.BudgetThirdsPerTurn, first.ThirdsSpent);

			int left = KingdomCatchUpRules.BudgetThirdsPerTurn - first.ThirdsSpent;
			Assert.IsTrue(KingdomCatchUpRules.TryPlanTurn(demand, left, 0, out second, out fault));
			Assert.AreEqual(0, second.Units, "a second call site on the same turn gets what is left, which is nothing");

			KingdomReifySpend partial;
			Assert.IsTrue(KingdomCatchUpRules.TryPlanTurn(new KingdomReifyDemand(0, 3, 0, 0, 0, 0), out partial, out fault));
			Assert.AreEqual(3, partial.Units);
			KingdomReifySpend remainder;
			Assert.IsTrue(KingdomCatchUpRules.TryPlanTurn(demand,
				KingdomCatchUpRules.BudgetThirdsPerTurn - partial.ThirdsSpent,
				KingdomBudgetRules.ReifyHeavyMintsPerTurn - partial.Heavy,
				out remainder, out fault));
			Assert.AreEqual(KingdomBudgetRules.ReifyUnitsPerTurn, partial.Units + remainder.Units,
				"the two spends together are exactly one turn's budget");
		}

		/// <summary>The body-mint ceiling is carried across call sites too, because four mints is a
		/// frame cost rather than an ordering preference (§0.0(b)).</summary>
		[Test]
		public void TheHeavyCeilingIsCarriedAcrossCallSitesToo()
		{
			KingdomReifySpend spend;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCatchUpRules.TryPlanTurn(new KingdomReifyDemand(9, 0, 0, 0, 0, 0),
				KingdomCatchUpRules.BudgetThirdsPerTurn, 1, out spend, out fault));
			Assert.AreEqual(1, spend.Heavy);
			Assert.IsTrue(KingdomCatchUpRules.TryPlanTurn(new KingdomReifyDemand(9, 0, 0, 0, 0, 0),
				KingdomCatchUpRules.BudgetThirdsPerTurn, 0, out spend, out fault));
			Assert.AreEqual(0, spend.Heavy);
		}

		/// <summary>An allowance bigger than the turn's own budget is a refusal, not a bonus: the
		/// constitution's number is a ceiling and a caller cannot raise it by asking.</summary>
		[Test]
		public void AnAllowanceOverTheBudgetIsRefused()
		{
			KingdomReifySpend spend;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomCatchUpRules.TryPlanTurn(new KingdomReifyDemand(0, 8, 0, 0, 0, 0),
				KingdomCatchUpRules.BudgetThirdsPerTurn + 1, KingdomBudgetRules.ReifyHeavyMintsPerTurn, out spend, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidIndex, fault);
			Assert.IsFalse(KingdomCatchUpRules.TryPlanTurn(new KingdomReifyDemand(0, 8, 0, 0, 0, 0),
				KingdomCatchUpRules.BudgetThirdsPerTurn, KingdomBudgetRules.ReifyHeavyMintsPerTurn + 1, out spend, out fault));
			Assert.IsFalse(KingdomCatchUpRules.TryPlanTurn(new KingdomReifyDemand(0, 8, 0, 0, 0, 0), -1, 0, out spend, out fault));
		}

		/// <summary>
		/// §3.5's catch-up invariant, in the half W3 makes true: <i>the model is authoritative for
		/// exactly the part of the debt that has not been reified, the ground is authoritative for
		/// exactly the part that has, and the counter is the boundary between them.</i>
		/// <para>
		/// One turn's spend lands one container's worth, so a debt bigger than one container is
		/// still owed afterwards and the row still says so. A founder who walks out at unit 40 of
		/// 132 walks back in owing 92 — because the row was never told the debt was paid.
		/// </para>
		/// </summary>
		[Test]
		public void APartiallyPaidDebtIsStillOwedAndTheRowSaysSo()
		{
			KingdomZoneRow row = new KingdomZoneRow("z", 0, 5000L, default(KingdomStocks), 0, 0, 0, 0, 400, 0, 0);
			Assert.AreEqual(KingdomCatchUpRules.ThirdsPerUnit, KingdomCityRules.CounterFor(row).LandThirds);
			Assert.IsFalse(KingdomCityRules.CounterFor(row).IsSettled);

			// One container's worth landed; the rest is still the model's.
			KingdomZoneRow after = row.WithOwed(300, 0, 0);
			Assert.AreEqual(KingdomCatchUpRules.ThirdsPerUnit, KingdomCityRules.CounterFor(after).LandThirds,
				"a debt is owed until it is nothing, not until it is smaller");
			Assert.IsFalse(KingdomCityRules.CounterFor(after).IsSettled);

			KingdomZoneRow paid = after.WithOwed(0, 0, 0);
			Assert.IsTrue(KingdomCityRules.CounterFor(paid).IsSettled, "and a caught-up zone costs nothing");
		}

		/// <summary>
		/// A landing and a draw stand on one row at once — the ordinary case for a granary zone the
		/// city has been drinking out of — and the counter reports both without netting them, which
		/// is exactly why §0.0(c) rejected one net figure.
		/// </summary>
		[Test]
		public void ALandingAndADrawStandAtOnceAndAreNeverNetted()
		{
			KingdomZoneRow row = new KingdomZoneRow("z", 0, 5000L, default(KingdomStocks), 0, 0, 0, 0, -60, 40, 0);
			KingdomCatchUpCounter counter = KingdomCityRules.CounterFor(row);
			Assert.AreEqual(KingdomCatchUpRules.ThirdsPerUnit, counter.DrawThirds);
			Assert.AreEqual(KingdomCatchUpRules.ThirdsPerUnit, counter.LandThirds);
			Assert.AreEqual(2 * KingdomCatchUpRules.ThirdsPerUnit, counter.OwedThirds);
			Assert.AreEqual(0, counter.Net, "the net is zero and the zone still owes two units of work");
			Assert.IsFalse(counter.IsSettled);
		}
	}
}
#endif
