#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// W7's closed-form flow solve, the brownout ladder, and the one-accounting proof for the power
	/// lane. LIVING-CITY-ARCHITECTURE &sect;3.11, &sect;0.0(a).
	/// </summary>
	public class KingdomFlowRulesTests
	{
		private const long Day = 1200L;

		private static KingdomFlowDemand[] Demands(params KingdomFlowDemand[] rows)
		{
			return rows;
		}

		private static KingdomFlowDemand Want(int id, KingdomWorkTier tier, int perDay)
		{
			return new KingdomFlowDemand(id, tier, perDay);
		}

		private static int[] Order(KingdomFlowDemand[] demands)
		{
			int[] order = new int[demands.Length];
			KingdomCityFault fault;
			Assert.IsTrue(KingdomFlowRules.TryBrownoutOrder(demands, demands.Length, order, out fault), fault.ToString());
			return order;
		}

		private static KingdomFlowSolution Solve(long supplyPerDay, KingdomFlowDemand[] demands, long level, long capacity, long throughput, long days)
		{
			KingdomFlowSolution solution;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomFlowRules.TrySolve(supplyPerDay, demands, demands.Length, Order(demands),
				level, capacity, throughput, days, out solution, out fault), fault.ToString());
			return solution;
		}

		private static void AssertConserved(KingdomFlowSolution s)
		{
			Assert.AreEqual(s.Generated + s.Discharged, s.Delivered + s.Charged + s.Spilled,
				"flow conservation broke: something was made or lost that the solve cannot account for");
		}

		// ---- Conservation: the identity that makes the solve checkable ------------------------

		/// <summary>
		/// <c>Generated + Discharged == Delivered + Charged + Spilled</c>, in every branch. This is
		/// the whole of "one accounting" as an assertion: there is no fourth destination for a
		/// charge, and nothing arrives from a fifth source.
		/// </summary>
		[TestCase(10000L, 4000, 0L, 24000L, 12000L, 1L)]
		[TestCase(1000L, 4000, 12000L, 24000L, 12000L, 1L)]
		[TestCase(0L, 4000, 0L, 0L, 0L, 5L)]
		[TestCase(50000L, 100, 0L, 1000L, 500L, 3L)]
		[TestCase(2400L, 4000, 500L, 24000L, 12000L, 90L)]
		public void EveryBranchConservesFlow(long supply, int demand, long level, long capacity, long throughput, long days)
		{
			AssertConserved(Solve(supply, Demands(Want(1, KingdomWorkTier.Industry, demand)), level, capacity, throughput, days));
		}

		/// <summary>A surplus with nowhere to put it is SPILLED and reported, never queued. A
		/// settlement that remembered charge it had no store for would be a settlement with a
		/// second, invisible store.</summary>
		[Test]
		public void SurplusWithNoStoreSpillsAndSaysSo()
		{
			KingdomFlowSolution s = Solve(10000L, Demands(Want(1, KingdomWorkTier.Industry, 4000)), 0L, 0L, 0L, 1L);
			Assert.AreEqual(4000L, s.Delivered);
			Assert.AreEqual(0L, s.Charged);
			Assert.AreEqual(6000L, s.Spilled);
			AssertConserved(s);
		}

		/// <summary>The store's throughput is a rate and not a bucket: it can only take in, or give
		/// back, so much a day however much room or contents it has.</summary>
		[Test]
		public void AStoreIsNeverABucketThatFillsInAnInstant()
		{
			KingdomFlowSolution s = Solve(50000L, Demands(), 0L, 24000L, 12000L, 1L);
			Assert.AreEqual(12000L, s.Charged, "a day's pour is a day's pour");
			Assert.AreEqual(38000L, s.Spilled);
			AssertConserved(s);
		}

		// ---- The store is spent before anything stops -----------------------------------------

		/// <summary>
		/// &sect;3.11's ordering, and it is the design rather than an implementation detail:
		/// <i>"stores discharge; when they empty, BROWNOUT"</i>. A city that let its forge go quiet
		/// while the salt was still hot would be telling the founder their store was decorative.
		/// </summary>
		[Test]
		public void TheSaltIsSpentBeforeAnythingGoesQuiet()
		{
			KingdomFlowDemand[] want = Demands(Want(1, KingdomWorkTier.Industry, 4000));
			KingdomFlowSolution s = Solve(0L, want, 12000L, 24000L, 12000L, 1L);
			Assert.AreEqual(0, s.Stopped, "the store could cover it and nothing should have stopped");
			Assert.AreEqual(4000L, s.Delivered);
			Assert.AreEqual(4000L, s.Discharged);
			Assert.IsFalse(s.Brownout);
			AssertConserved(s);
		}

		/// <summary>And when the salt runs out, the lights go down — with the shortfall reported as
		/// its own figure so the telling can say how far short the city ran.</summary>
		[Test]
		public void WhenTheSaltEmptiesTheLightsGoDown()
		{
			KingdomFlowDemand[] want = Demands(Want(1, KingdomWorkTier.Industry, 4000), Want(2, KingdomWorkTier.Watch, 4000));
			KingdomFlowSolution s = Solve(0L, want, 4000L, 24000L, 12000L, 1L);
			Assert.AreEqual(4000L, s.Shortfall);
			Assert.AreEqual(1, s.Stopped);
			Assert.IsTrue(s.Brownout);
			AssertConserved(s);
		}

		// ---- The stated ladder ----------------------------------------------------------------

		/// <summary>
		/// The order §3.11 states, lowest first:
		/// <b>industry &rarr; refining &rarr; amenity &rarr; food &rarr; water &rarr; watch.</b>
		/// A city gives up what it is doing before it gives up what it is.
		/// </summary>
		[Test]
		public void TheLadderStopsIndustryFirstAndTheWatchLast()
		{
			KingdomFlowDemand[] want = Demands(
				Want(10, KingdomWorkTier.Watch, 100),
				Want(20, KingdomWorkTier.Water, 100),
				Want(30, KingdomWorkTier.Food, 100),
				Want(40, KingdomWorkTier.Amenity, 100),
				Want(50, KingdomWorkTier.Refining, 100),
				Want(60, KingdomWorkTier.Industry, 100));
			int[] order = Order(want);
			Assert.AreEqual(KingdomWorkTier.Industry, want[order[0]].Tier);
			Assert.AreEqual(KingdomWorkTier.Refining, want[order[1]].Tier);
			Assert.AreEqual(KingdomWorkTier.Amenity, want[order[2]].Tier);
			Assert.AreEqual(KingdomWorkTier.Food, want[order[3]].Tier);
			Assert.AreEqual(KingdomWorkTier.Water, want[order[4]].Tier);
			Assert.AreEqual(KingdomWorkTier.Watch, want[order[5]].Tier);
		}

		/// <summary>
		/// Lodging is <see cref="KingdomWorkTier.Amenity"/>, and that is a ruling rather than an
		/// oversight. It stops AFTER the forges and BEFORE the food: a roof needs no charge to keep
		/// the rain off, so what browns out is comfort, and whether a household keeps its home is
		/// the roof brink's question and not the grid's.
		/// </summary>
		[Test]
		public void LodgingStopsAfterIndustryAndBeforeFood()
		{
			Assert.AreEqual(KingdomWorkTier.Amenity, KingdomFlowRules.TierOfCategory("housing"));
			Assert.Greater((int)KingdomFlowRules.TierOfCategory("housing"), (int)KingdomFlowRules.TierOfCategory("craft"));
			Assert.Less((int)KingdomFlowRules.TierOfCategory("housing"), (int)KingdomFlowRules.TierOfCategory("food"));
		}

		/// <summary>A category the catalogue does not know — a third party's own, arriving through
		/// the extension API — lands on the middle rung. Neither the first thing this city gives up
		/// nor the last, which is the only honest default when we do not know what it does.</summary>
		[TestCase("")]
		[TestCase(null)]
		[TestCase("teleportarium")]
		public void AnUnknownCategoryLandsOnTheMiddleRung(string category)
		{
			Assert.AreEqual(KingdomWorkTier.Amenity, KingdomFlowRules.TierOfCategory(category));
		}

		/// <summary>Within one rung the higher work id goes first. Stable, stored, reload-proof,
		/// and needing no draw — a brownout is arithmetic and a ladder, never chance.</summary>
		[Test]
		public void WithinOneRungTheHigherIdGoesFirst()
		{
			KingdomFlowDemand[] want = Demands(
				Want(3, KingdomWorkTier.Industry, 100),
				Want(9, KingdomWorkTier.Industry, 100),
				Want(5, KingdomWorkTier.Industry, 100));
			int[] order = Order(want);
			Assert.AreEqual(9, want[order[0]].WorkId);
			Assert.AreEqual(5, want[order[1]].WorkId);
			Assert.AreEqual(3, want[order[2]].WorkId);
		}

		/// <summary>Ordering the same set twice gives the same answer, and ordering a shuffle of it
		/// gives that same answer too. That is what "deterministic" has to mean for a reload to
		/// reproduce a brownout.</summary>
		[Test]
		public void TheLadderIsTheSameAfterAShuffle()
		{
			KingdomFlowDemand[] one = Demands(
				Want(1, KingdomWorkTier.Food, 10), Want(2, KingdomWorkTier.Industry, 10), Want(3, KingdomWorkTier.Industry, 10));
			KingdomFlowDemand[] other = Demands(
				Want(3, KingdomWorkTier.Industry, 10), Want(1, KingdomWorkTier.Food, 10), Want(2, KingdomWorkTier.Industry, 10));
			int[] a = Order(one);
			int[] b = Order(other);
			for (int i = 0; i < a.Length; i++)
			{
				Assert.AreEqual(one[a[i]].WorkId, other[b[i]].WorkId, "the ladder depended on array order");
			}
		}

		/// <summary>Works stop WHOLE. A half-lit forge is not a thing a founder can see or reason
		/// about, and it would make a stated order unreadable.</summary>
		[Test]
		public void WorksStopWholeAndNeverInPart()
		{
			KingdomFlowDemand[] want = Demands(Want(1, KingdomWorkTier.Industry, 4000), Want(2, KingdomWorkTier.Watch, 4000));
			// Short by a single unit: one whole work still goes.
			KingdomFlowSolution s = Solve(7999L, want, 0L, 0L, 0L, 1L);
			Assert.AreEqual(1, s.Stopped);
			Assert.AreEqual(4000L, s.Delivered);
			Assert.AreEqual(3999L, s.Spilled, "what the stopped work would have drunk is spare, and with no store it spills");
			AssertConserved(s);
		}

		/// <summary>Everything can stop, and the solve still balances. A city with nothing at all
		/// is a state, not an error.</summary>
		[Test]
		public void EverythingCanStopAndTheBooksStillBalance()
		{
			KingdomFlowDemand[] want = Demands(Want(1, KingdomWorkTier.Industry, 4000), Want(2, KingdomWorkTier.Watch, 4000));
			KingdomFlowSolution s = Solve(0L, want, 0L, 0L, 0L, 1L);
			Assert.AreEqual(2, s.Stopped);
			Assert.AreEqual(0L, s.Delivered);
			AssertConserved(s);
		}

		// ---- No term in the elapsed -----------------------------------------------------------

		/// <summary>
		/// &sect;0.0(a)'s identity, for this lane: a one-day span and a ninety-day span differ in
		/// the numbers and not in the work. Days multiply the rates once and appear nowhere else,
		/// so the solve is the same arithmetic either way.
		/// </summary>
		[Test]
		public void ASeasonCostsTheSameArithmeticAsADay()
		{
			KingdomFlowDemand[] want = Demands(Want(1, KingdomWorkTier.Industry, 1000));
			KingdomFlowSolution one = Solve(2400L, want, 0L, 240000L, 120000L, 1L);
			KingdomFlowSolution season = Solve(2400L, want, 0L, 240000L, 120000L, 90L);
			Assert.AreEqual(one.Generated * 90L, season.Generated);
			Assert.AreEqual(one.Delivered * 90L, season.Delivered);
			Assert.AreEqual(one.Charged * 90L, season.Charged);
			Assert.AreEqual(one.Stopped, season.Stopped);
			AssertConserved(season);
		}

		/// <summary>
		/// Additive across a split, exactly where it must be: with the store neither filling nor
		/// emptying inside the span, running [a,c] equals running [a,b] then [b,c]. The two moments
		/// where that fails ARE the breakpoints, which is what makes the breakpoint loop the correct
		/// integration of this rather than an approximation of it.
		/// </summary>
		[Test]
		public void SolvingASpanWholeEqualsSolvingItInPiecesWhenNoStoreCrosses()
		{
			KingdomFlowDemand[] want = Demands(Want(1, KingdomWorkTier.Industry, 1000));
			long capacity = 1000000L;
			long throughput = 500000L;
			KingdomFlowSolution whole = Solve(2400L, want, 0L, capacity, throughput, 10L);
			KingdomFlowSolution first = Solve(2400L, want, 0L, capacity, throughput, 4L);
			KingdomFlowSolution second = Solve(2400L, want, first.Charged, capacity, throughput, 6L);
			Assert.AreEqual(whole.Generated, first.Generated + second.Generated);
			Assert.AreEqual(whole.Delivered, first.Delivered + second.Delivered);
			Assert.AreEqual(whole.Charged, first.Charged + second.Charged);
		}

		/// <summary>The store's fill and empty are proposed as breakpoints through the SAME
		/// crossing solver every stock row in the model uses. Two answers to "when does this level
		/// hit a bound" is exactly what W7 exists to prevent.</summary>
		[Test]
		public void TheStoreCrossingIsTheModelsOwnCrossingSolver()
		{
			KingdomBreakpoint fills;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomFlowRules.TryStoreCrossing(0L, 1000L, 100L, Day, 5000L, out fills, out fault), fault.ToString());
			Assert.AreEqual(KingdomBreakpointKind.StockFull, fills.Kind);
			Assert.AreEqual(5000L + 10L * Day, fills.Tick);
			KingdomBreakpoint empties;
			Assert.IsTrue(KingdomFlowRules.TryStoreCrossing(500L, 1000L, -100L, Day, 0L, out empties, out fault), fault.ToString());
			Assert.AreEqual(KingdomBreakpointKind.StockEmpty, empties.Kind);
			Assert.AreEqual(5L * Day, empties.Tick);
			// A level going nowhere proposes nothing, and says so with false and no fault.
			KingdomBreakpoint still;
			Assert.IsFalse(KingdomFlowRules.TryStoreCrossing(500L, 1000L, 0L, Day, 0L, out still, out fault));
			Assert.AreEqual(KingdomCityFault.None, fault);
		}

		// ---- Refusals -------------------------------------------------------------------------

		/// <summary>A span that would overflow refuses rather than saturating. A saturated flow
		/// figure would be a lie the conservation identity could not catch, which is worse than a
		/// refusal.</summary>
		[Test]
		public void ASpanThatWouldOverflowRefusesRatherThanSaturating()
		{
			KingdomFlowSolution solution;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomFlowRules.TrySolve(long.MaxValue / 2L, new KingdomFlowDemand[0], 0, new int[0],
				0L, 0L, 0L, 1000L, out solution, out fault));
			Assert.AreEqual(KingdomCityFault.ArithmeticOverflow, fault);
		}

		/// <summary>A store holding more than it can hold is a refusal, not a clamp: the solve is
		/// handed a reading and must not quietly correct one.</summary>
		[Test]
		public void AnImpossibleStoreIsRefused()
		{
			KingdomFlowSolution solution;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomFlowRules.TrySolve(0L, new KingdomFlowDemand[0], 0, new int[0],
				2000L, 1000L, 500L, 1L, out solution, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidCapacity, fault);
		}

		/// <summary>Zero days is a no-op and not a fault: reckoning twice at the same tick must
		/// cost nothing rather than refusing.</summary>
		[Test]
		public void ZeroDaysMovesNothingAndIsNotAFault()
		{
			KingdomFlowSolution s = Solve(2400L, Demands(Want(1, KingdomWorkTier.Industry, 1000)), 500L, 1000L, 500L, 0L);
			Assert.AreEqual(0L, s.Generated);
			Assert.AreEqual(0L, s.Delivered);
			Assert.AreEqual(0, s.Stopped);
		}

		// ---- The power lane's one-accounting proof --------------------------------------------

		/// <summary>
		/// <b>One accounting, asserted.</b> <c>KingdomPowerRules.ChargeForDays</c> is the power
		/// lane's own name for "a day's making over a span" and the flow solve is where that number
		/// is now actually produced. They are the same number, at every span, or the migration
		/// moved a day.
		/// </summary>
		[TestCase(2400, 1)]
		[TestCase(2400, 90)]
		[TestCase(4800, 7)]
		[TestCase(0, 90)]
		public void ChargeForDaysIsExactlyWhatTheFlowSolveGenerates(int daily, int days)
		{
			KingdomFlowSolution s = Solve(daily, new KingdomFlowDemand[0], 0L, 0L, 0L, days);
			Assert.AreEqual(KingdomPowerRules.ChargeForDays(daily, days), s.Generated);
		}

		/// <summary>
		/// The same, for the store's intake clamp. <c>Absorbable</c> is the rules layer's name for
		/// what the solve's charge cap does, and the two must not be allowed to drift: that drift
		/// IS the second accounting this wave retired.
		/// </summary>
		[TestCase(0, 24000, 1)]
		[TestCase(12000, 24000, 1)]
		[TestCase(0, 24000, 5)]
		[TestCase(23000, 24000, 3)]
		[TestCase(0, 0, 4)]
		public void TheSolvesChargeCapIsTheRulesOwnAbsorbable(int stored, int capacity, int days)
		{
			// A surplus and no demand at all, so the only thing that can limit the charge is the
			// store itself -- which is exactly the question Absorbable answers.
			int perDay = 100000;
			KingdomFlowSolution s = Solve(perDay, new KingdomFlowDemand[0], stored, capacity,
				KingdomPowerRules.ThroughputForDays(capacity, 1), days);
			Assert.AreEqual(KingdomPowerRules.Absorbable(perDay * days, stored, capacity, days), s.Charged);
			AssertConserved(s);
		}

		/// <summary>
		/// And for the store's outflow clamp. The demand is exactly what the store can give, so
		/// nothing stops and the discharge is the cap itself &mdash; which is what
		/// <c>Releasable</c> names.
		/// </summary>
		[TestCase(0, 24000)]
		[TestCase(6000, 24000)]
		[TestCase(12000, 24000)]
		[TestCase(23000, 24000)]
		public void TheSolvesDischargeCapIsTheRulesOwnReleasable(int stored, int capacity)
		{
			int releasable = KingdomPowerRules.Releasable(stored, capacity, 1);
			KingdomFlowDemand[] want = Demands(Want(1, KingdomWorkTier.Industry, releasable));
			KingdomFlowSolution s = Solve(0L, want, stored, capacity, KingdomPowerRules.ThroughputForDays(capacity, 1), 1L);
			Assert.AreEqual(0, s.Stopped, "the store could cover exactly this demand, so nothing should have gone quiet");
			Assert.AreEqual(releasable, s.Discharged);
			AssertConserved(s);
		}

		/// <summary>One dram past what the store can give, and the ladder fires. The boundary is
		/// where a rule is worth testing.</summary>
		[Test]
		public void OneUnitPastWhatTheStoreCanGiveIsWhereTheLadderFires()
		{
			int releasable = KingdomPowerRules.Releasable(12000, 24000, 1);
			KingdomFlowDemand[] want = Demands(Want(1, KingdomWorkTier.Industry, releasable), Want(2, KingdomWorkTier.Watch, 1));
			KingdomFlowSolution s = Solve(0L, want, 12000, 24000, KingdomPowerRules.ThroughputForDays(24000, 1), 1L);
			Assert.AreEqual(1L, s.Shortfall);
			Assert.AreEqual(1, s.Stopped);
			Assert.AreEqual(KingdomWorkTier.Industry, want[Order(want)[0]].Tier, "the forge goes before the watch");
			AssertConserved(s);
		}

		// ---- A line runs downhill and stops level ---------------------------------------------

		private static KingdomCityState Zones(params long[] levelsAndCaps)
		{
			int count = levelsAndCaps.Length / 2;
			KingdomZoneRow[] rows = new KingdomZoneRow[count];
			for (int i = 0; i < count; i++)
			{
				rows[i] = new KingdomZoneRow("Z" + i, 0, 100L,
					new KingdomStocks(new KingdomStockPair(levelsAndCaps[i * 2], levelsAndCaps[i * 2 + 1]),
						new KingdomStockPair(0L, 0L), new KingdomStockPair(0L, 0L)),
					0, 0, 0, 0, 0, 0, 0);
			}
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityState.TryCreate(1, 1, "seat", 100L, default(KingdomStocks), rows,
				new KingdomWorkRow[0], new KingdomResidentRow[0], new KingdomClockRow[0], out state, out fault), fault.ToString());
			return state;
		}

		private static void Downhill(KingdomCityState state, int[] members, long budget, out int from, out int to, out long amount)
		{
			KingdomCityFault fault;
			Assert.IsTrue(KingdomFlowRules.TryChooseDownhill(state, KingdomStockKind.Water, members, members.Length, budget,
				out from, out to, out amount, out fault), fault.ToString());
		}

		/// <summary>Fullest gives, emptiest takes, and the amount is the one that levels them —
		/// solved, not stepped, so it cannot overshoot into an inverted pair.</summary>
		[Test]
		public void ALineRunsFromTheFullestToTheEmptiestAndStopsLevel()
		{
			KingdomCityState state = Zones(1000L, 1000L, 0L, 1000L);
			int from;
			int to;
			long amount;
			Downhill(state, new int[2] { 0, 1 }, 10000L, out from, out to, out amount);
			Assert.AreEqual(0, from);
			Assert.AreEqual(1, to);
			Assert.AreEqual(500L, amount, "two vessels of equal size level at half the difference");
		}

		/// <summary>Capacities differ, and the line levels the FILL rather than the contents: a
		/// small cistern and a great one come to the same fraction, which is what water does.</summary>
		[Test]
		public void ALineLevelsTheFillAndNotTheContents()
		{
			KingdomCityState state = Zones(300L, 300L, 0L, 900L);
			int from;
			int to;
			long amount;
			Downhill(state, new int[2] { 0, 1 }, 10000L, out from, out to, out amount);
			Assert.AreEqual(225L, amount);
			// 75/300 and 225/900 are both one quarter.
			Assert.AreEqual((300L - amount) * 900L, amount * 300L, "the two ends did not come to the same fill");
		}

		/// <summary>A line already level runs nothing, and a line running uphill runs nothing. Both
		/// report zero rather than refusing: a settled main is the ordinary case.</summary>
		[Test]
		public void ALevelLineRunsNothing()
		{
			int from;
			int to;
			long amount;
			Downhill(Zones(500L, 1000L, 500L, 1000L), new int[2] { 0, 1 }, 10000L, out from, out to, out amount);
			Assert.AreEqual(0L, amount);
			Downhill(Zones(0L, 1000L, 0L, 1000L), new int[2] { 0, 1 }, 10000L, out from, out to, out amount);
			Assert.AreEqual(0L, amount);
		}

		/// <summary>The line's own bottleneck is the ceiling: a narrow main takes longer to level
		/// two cisterns, which is the whole reason a segment has a capacity at all.</summary>
		[Test]
		public void TheLinesBottleneckCapsWhatOnePassCanRun()
		{
			KingdomCityState state = Zones(1000L, 1000L, 0L, 1000L);
			int from;
			int to;
			long amount;
			Downhill(state, new int[2] { 0, 1 }, 60L, out from, out to, out amount);
			Assert.AreEqual(60L, amount);
		}

		/// <summary>A zone on the line with no vessels for this kind holds nothing on it, and that
		/// is not a fault — a length of main across bare ground is an ordinary thing to lay.</summary>
		[Test]
		public void AZoneWithNoVesselsIsOnTheLineAndHoldsNothingOnIt()
		{
			KingdomCityState state = Zones(1000L, 1000L, 0L, 0L, 0L, 1000L);
			int from;
			int to;
			long amount;
			Downhill(state, new int[3] { 0, 1, 2 }, 10000L, out from, out to, out amount);
			Assert.AreEqual(0, from);
			Assert.AreEqual(2, to);
			Assert.AreEqual(500L, amount);
		}

		/// <summary>One zone is not a network. Nothing runs and nothing refuses.</summary>
		[Test]
		public void OneZoneIsNotALine()
		{
			int from;
			int to;
			long amount;
			Downhill(Zones(1000L, 1000L), new int[1] { 0 }, 10000L, out from, out to, out amount);
			Assert.AreEqual(-1, from);
			Assert.AreEqual(0L, amount);
		}

		// ---- The telling ----------------------------------------------------------------------

		/// <summary>A brownout says what went quiet, by name, once. STANDARDS 7b: applicable but
		/// blocked announces, and announces once.</summary>
		[Test]
		public void TheBrownoutTellingNamesWhatWentQuiet()
		{
			StringAssert.Contains("crank mill", KingdomFlowRules.BrownoutNotice("crank mill"));
			StringAssert.Contains("crank mill", KingdomFlowRules.BrownoutTelling("crank mill", "Kavvat"));
			StringAssert.Contains("Kavvat", KingdomFlowRules.BrownoutTelling("crank mill", "Kavvat"));
			Assert.IsNotEmpty(KingdomFlowRules.BrownoutNotice(null), "a nameless work still owes the founder a sentence");
		}

		/// <summary>The ladder line is composed from the enum in the enum's own order, so the
		/// sentence a founder reads and the order the code runs cannot drift apart.</summary>
		[Test]
		public void TheLadderLineNamesEveryRungInOrder()
		{
			string line = KingdomFlowRules.LadderLine();
			int at = -1;
			for (int tier = 0; tier <= (int)KingdomWorkTier.Watch; tier++)
			{
				int found = line.IndexOf(KingdomFlowRules.TierName((KingdomWorkTier)tier), System.StringComparison.Ordinal);
				Assert.Greater(found, at, "the ladder line got out of step with the ladder");
				at = found;
			}
		}
	}
}
#endif
