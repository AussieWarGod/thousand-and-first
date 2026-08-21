#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// W6's load-bearing arithmetic: the one clock days are counted off, and the one place a made
	/// dram is written down. LIVING-CITY-ARCHITECTURE &sect;2.3, &sect;3.9, &sect;7.4, invariant I1.
	/// </summary>
	public class KingdomProductionRulesTests
	{
		private const long Day = 1200L;

		private static long Days(long from, long to)
		{
			long days;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomProductionRules.TryDaysBetween(from, to, Day, out days, out fault), fault.ToString());
			return days;
		}

		private static KingdomProductionStep Produce(long level, long capacity, int owed, long rate, long days)
		{
			KingdomProductionStep step;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomProductionRules.TryProduce(level, capacity, owed, rate, days, out step, out fault), fault.ToString());
			return step;
		}

		// ---- The clock ------------------------------------------------------------------------

		/// <summary>
		/// The property the whole integration rests on: splitting a span at any breakpoint pays
		/// exactly the same number of days as running it whole. An "elapsed / TicksPerDay" count
		/// does NOT have it — that is what would let a reckon lose a day, or a second reckon over
		/// an overlapping span bill one twice.
		/// </summary>
		[Test]
		public void DaysAreAdditiveAcrossEverySplitOfTheSameSpan()
		{
			long from = 3L * Day + 700L;
			long to = 11L * Day + 200L;
			long whole = Days(from, to);
			for (long cut = from; cut <= to; cut += 137L)
			{
				Assert.AreEqual(whole, Days(from, cut) + Days(cut, to),
					"splitting the span at " + cut + " changed what it is worth");
			}
		}

		/// <summary>A horizon that lands mid-day loses nothing: the remainder is still inside the
		/// same day and is paid the moment the boundary is crossed.</summary>
		[Test]
		public void AHorizonInsideADayPaysNothingAndTheNextOnePaysItAll()
		{
			Assert.AreEqual(0L, Days(0L, Day - 1L));
			Assert.AreEqual(1L, Days(0L, Day));
			Assert.AreEqual(1L, Days(Day - 1L, Day));
			// Twice a day for a week, and the week is still seven days.
			long total = 0L;
			for (int half = 0; half < 14; half++)
			{
				total += Days(half * (Day / 2L), (half + 1) * (Day / 2L));
			}
			Assert.AreEqual(7L, total, "a founder who walks in twice a day must not stop the fields");
		}

		[Test]
		public void TheClockRefusesRatherThanRunningBackwards()
		{
			long days;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomProductionRules.TryDaysBetween(2L * Day, Day, Day, out days, out fault));
			Assert.AreEqual(KingdomCityFault.ClockRegression, fault);
			Assert.IsFalse(KingdomProductionRules.TryDaysBetween(0L, Day, 0L, out days, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidInterval, fault);
			Assert.IsFalse(KingdomProductionRules.TryDaysBetween(-1L, Day, Day, out days, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidTick, fault);
		}

		// ---- The ledger: I1 ------------------------------------------------------------------

		/// <summary>
		/// <b>I1</b>: <c>model total == ground total + counter-owed</c>. Production is not a
		/// transfer, so the only way it can satisfy the identity is by moving the level and the
		/// debt by the same amount — which is what makes the ground, <c>level - owed</c>,
		/// unchanged by anything the works did while nobody was watching.
		/// </summary>
		[Test]
		public void ProductionMovesTheLevelAndTheDebtByTheSameAmount()
		{
			KingdomProductionStep step = Produce(40L, 400L, 0, 12L, 7L);
			Assert.AreEqual(124L, step.NextLevel);
			Assert.AreEqual(84, step.NextOwed);
			Assert.AreEqual(84L, step.Landed);
			Assert.AreEqual(40L - 0L, step.NextLevel - step.NextOwed, "the ground did not change, so level - owed may not");
		}

		/// <summary>A lane that consumes drains the level and the debt together, so the identity
		/// holds in both directions and one sign is not a special case of the other.</summary>
		[Test]
		public void ConsumptionMovesBothTheOtherWay()
		{
			KingdomProductionStep step = Produce(100L, 400L, 0, -15L, 4L);
			Assert.AreEqual(40L, step.NextLevel);
			Assert.AreEqual(-60, step.NextOwed);
			Assert.AreEqual(100L, step.NextLevel - step.NextOwed);
		}

		/// <summary>
		/// A full granary makes nothing more, and the difference is REPORTED rather than absorbed.
		/// This is <c>KingdomGrowth.StoreHarvest</c>'s own rule kept when the arithmetic moved onto
		/// the model: loss, not a queue.
		/// </summary>
		[Test]
		public void AFullStoreTakesWhatFitsAndSpillsTheRest()
		{
			KingdomProductionStep step = Produce(90L, 100L, 0, 20L, 30L);
			Assert.AreEqual(100L, step.NextLevel);
			Assert.AreEqual(10, step.NextOwed);
			Assert.AreEqual(590L, step.Spilled, "600 made, 10 fitted");
			Assert.AreEqual(90L, step.NextLevel - step.NextOwed, "and the identity survives the spill");
		}

		[Test]
		public void AStoreWithNoRoomAtAllProducesNothingRatherThanRefusing()
		{
			KingdomProductionStep step = Produce(0L, 0L, 0, 30L, 90L);
			Assert.AreEqual(0L, step.NextLevel);
			Assert.AreEqual(0, step.NextOwed);
			Assert.AreEqual(2700L, step.Spilled);
		}

		[Test]
		public void AZeroRateAndAZeroSpanAreBothNoOps()
		{
			Assert.AreEqual(0L, Produce(40L, 400L, 3, 0L, 90L).Landed);
			Assert.AreEqual(3, Produce(40L, 400L, 3, 0L, 90L).NextOwed);
			Assert.AreEqual(0L, Produce(40L, 400L, 3, 25L, 0L).Landed);
		}

		[Test]
		public void ProductionRefusesRatherThanOverflowingTheDebt()
		{
			KingdomProductionStep step;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomProductionRules.TryProduce(0L, long.MaxValue, 0, 1L, long.MaxValue, out step, out fault));
			Assert.AreEqual(KingdomCityFault.ArithmeticOverflow, fault);
			Assert.IsFalse(KingdomProductionRules.TryProduce(0L, 10L, 0, 1L, -1L, out step, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidTick, fault);
			Assert.IsFalse(KingdomProductionRules.TryProduce(20L, 10L, 0, 1L, 1L, out step, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidCapacity, fault);
		}

		// ---- The reconcile: the audit made exact ---------------------------------------------

		private static KingdomProductionStep Trued(long ground, long capacity, int owed)
		{
			KingdomProductionStep step;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomProductionRules.TryReconcile(ground, capacity, owed, out step, out fault), fault.ToString());
			return step;
		}

		/// <summary>
		/// The audit invariant, asserted where it is MADE rather than where it is printed: after a
		/// reconcile, <c>level - owed == ground</c> for every combination of ground, room and
		/// standing claim. Before W6 the reconcile wrote the ground over the level and left the
		/// debt alone, which is the same statement only when the debt is zero.
		/// </summary>
		[Test]
		public void AfterAReconcileTheModelMinusItsDebtIsExactlyTheGround()
		{
			for (long ground = 0L; ground <= 100L; ground += 25L)
			{
				for (long capacity = ground; capacity <= 100L; capacity += 25L)
				{
					for (int owed = -120; owed <= 120; owed += 20)
					{
						KingdomProductionStep step = Trued(ground, capacity, owed);
						Assert.AreEqual(ground, step.NextLevel - step.NextOwed,
							"ground=" + ground + " cap=" + capacity + " owed=" + owed);
						Assert.IsTrue(step.NextLevel >= 0L && step.NextLevel <= capacity,
							"a level outside its own capacity is not a level");
					}
				}
			}
		}

		/// <summary>The ground wins for what is physical: a founder who poured water in is believed,
		/// and the outstanding claim is neither cancelled by it nor doubled.</summary>
		[Test]
		public void TheGroundWinsAndTheStandingClaimSurvivesIt()
		{
			KingdomProductionStep step = Trued(70L, 400L, 30);
			Assert.AreEqual(100L, step.NextLevel);
			Assert.AreEqual(30, step.NextOwed, "what the works made and nobody poured is still owed");
			Assert.AreEqual(0L, step.Spilled);
		}

		/// <summary>A claim bigger than the room left is dropped, not carried — the same rule a
		/// harvest with a full larder gets — and it is named.</summary>
		[Test]
		public void AClaimTheContainersCanNoLongerHoldIsDroppedAndNamed()
		{
			KingdomProductionStep step = Trued(95L, 100L, 40);
			Assert.AreEqual(100L, step.NextLevel);
			Assert.AreEqual(5, step.NextOwed);
			Assert.AreEqual(35L, step.Spilled);
			Assert.AreEqual(95L, step.NextLevel - step.NextOwed);
		}

		[Test]
		public void AReconcileRefusesAnImpossibleGroundRatherThanRepairingIt()
		{
			KingdomProductionStep step;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomProductionRules.TryReconcile(101L, 100L, 0, out step, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidCapacity, fault);
		}

		/// <summary>
		/// W7 repair, the caller's half of the rule above.
		/// <para>
		/// A vessel holding more than the books said it could hold is REACHABLE &mdash; a founder
		/// who hand-stuffs a dedicated larder past its counted capacity, or a design retuned
		/// downward under a full one. The refusal above is correct (the rules layer must not
		/// quietly correct a reading it was handed), and it used to abandon the whole pass:
		/// water and food were joined by <c>||</c>, so an over-stuffed larder suppressed the water
		/// reconcile AND stranded a stale rate on the row.
		/// </para>
		/// <para>
		/// &sect;3.1 settles which way it must be normalised: <b>the ground wins for anything
		/// physical.</b> The CEILING is raised to the reading, never the level clamped to the
		/// ceiling &mdash; clamping would destroy real drams a founder can walk up to. This pins
		/// that the normalisation always produces a reading the reconcile accepts, and that it
		/// leaves the ground exactly where it was.
		/// </para>
		/// </summary>
		[TestCase(101L, 100L)]
		[TestCase(5000L, 0L)]
		[TestCase(1L, 1L)]
		[TestCase(0L, 100L)]
		public void RaisingTheCeilingToTheReadingAlwaysGivesAReconcileItAccepts(long ground, long capacity)
		{
			long ceiling = (capacity < ground) ? ground : capacity;
			KingdomProductionStep step;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomProductionRules.TryReconcile(ground, ceiling, 0, out step, out fault), fault.ToString());
			Assert.AreEqual(ground, step.NextLevel, "the ground moved, and the ground is what wins");
			Assert.AreEqual(0, step.NextOwed);
			Assert.AreEqual(0L, step.Spilled, "nothing was destroyed to make the reading fit");
		}

		/// <summary>
		/// One state, one span, and both things that can happen to it: a zone makes water and then
		/// a main carries some of it to a drier quarter. Invariant I1 has to survive the pair, not
		/// only each of them &mdash; which is the case the reader named as untested.
		/// </summary>
		[Test]
		public void ProductionAndACarryOverOneStateBothKeepTheLedgerIdentity()
		{
			KingdomZoneRow[] zones = new KingdomZoneRow[2]
			{
				new KingdomZoneRow("A", 0, 100L, new KingdomStocks(new KingdomStockPair(200L, 1000L), new KingdomStockPair(0L, 0L), new KingdomStockPair(0L, 0L)), 0, 0, 0, 0, 0, 0, 0),
				new KingdomZoneRow("B", 0, 100L, new KingdomStocks(new KingdomStockPair(0L, 1000L), new KingdomStockPair(0L, 0L), new KingdomStockPair(0L, 0L)), 0, 0, 0, 0, 0, 0, 0)
			};
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityState.TryCreate(1, 1, "seat", 0L, default(KingdomStocks), zones,
				new KingdomWorkRow[0], new KingdomResidentRow[0], new KingdomClockRow[0], out state, out fault), fault.ToString());
			// Zone A's works make sixty drams a day for five days. The ground has not moved: the
			// level and the debt rise together.
			KingdomProductionStep made = Produce(200L, 1000L, 0, 60L, 5L);
			Assert.AreEqual(500L, made.NextLevel);
			Assert.AreEqual(300, made.NextOwed);
			KingdomZoneRow rowA;
			Assert.IsTrue(state.TryZone(0, out rowA));
			KingdomCityState produced;
			Assert.IsTrue(state.TryWithZone(0,
				rowA.WithReading(rowA.LastReadTick,
					new KingdomStocks(new KingdomStockPair(made.NextLevel, 1000L), rowA.Stocks.Food, rowA.Stocks.Materials),
					0, 0, 60, 0).WithOwed(made.NextOwed, 0, 0),
				out produced, out fault), fault.ToString());
			// Then a main runs a hundred of it to B. A transfer is a carry: level and debt again.
			KingdomCityState carried;
			long moved;
			Assert.IsTrue(KingdomNetworkRules.TryPostTransfer(produced, KingdomStockKind.Water, 0, 1, 100L, out carried, out moved, out fault), fault.ToString());
			Assert.AreEqual(100L, moved);
			KingdomZoneRow a;
			KingdomZoneRow b;
			Assert.IsTrue(carried.TryZone(0, out a));
			Assert.IsTrue(carried.TryZone(1, out b));
			// I1, per row: level - owed is the GROUND, and no ground has been touched by either
			// step, so both rows still read what they physically held at the start.
			Assert.AreEqual(200L, a.Stocks.Water.Level - a.OwedWater);
			Assert.AreEqual(0L, b.Stocks.Water.Level - b.OwedWater);
			// And in total: three hundred made, none of it poured yet, none of it lost in transit.
			Assert.AreEqual(500L, a.Stocks.Water.Level + b.Stocks.Water.Level);
			Assert.AreEqual(300, a.OwedWater + b.OwedWater);
		}
	}
}
#endif
