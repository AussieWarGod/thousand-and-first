#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The city book's arithmetic: what the rows total to, what a zone owes, where a deficit is
	/// taken from, and what a reckoning costs. LIVING-CITY-ARCHITECTURE §1.2, §2.3, §3.9, §0.0(a).
	/// </summary>
	public class KingdomCityRulesTests
	{
		private static KingdomStocks Stocks(long water, long waterCap, long food, long foodCap)
		{
			return new KingdomStocks(
				new KingdomStockPair(water, waterCap),
				new KingdomStockPair(food, foodCap),
				new KingdomStockPair(0L, 0L));
		}

		private static KingdomZoneRow Zone(string id, long lastRead, long water, long waterCap, long food, long foodCap, int owedWater = 0)
		{
			return new KingdomZoneRow(id, 0, lastRead, Stocks(water, waterCap, food, foodCap), 0, 0, 0, 0, owedWater, 0, 0);
		}

		private static KingdomCityState City(params KingdomZoneRow[] zones)
		{
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
				"taf:city:kavvat", 0L, default(KingdomStocks), zones, null, null, null, out state, out fault), fault.ToString());
			return state;
		}

		// ---- Stocks are city-level (§1.2(a)) --------------------------------------------------

		/// <summary>Water raised in the mine and food grown on the terrace are one set of rows.</summary>
		[Test]
		public void TheCitysStocksAreTheSumOfItsZoneRows()
		{
			KingdomStocks stocks;
			Assert.IsTrue(KingdomCityRules.TryCityStocks(City(
				Zone("a", 100L, 40L, 100L, 5L, 20L),
				Zone("b", 200L, 11L, 60L, 2L, 12L)), out stocks));
			Assert.AreEqual(51L, stocks.Water.Level);
			Assert.AreEqual(160L, stocks.Water.Capacity);
			Assert.AreEqual(7L, stocks.Food.Level);
			Assert.AreEqual(32L, stocks.Food.Capacity);
		}

		/// <summary>A zone nobody has ever stood in contributes nothing. Nothing is invented for
		/// ground the game has never looked at.</summary>
		[Test]
		public void AZoneNobodyHasStoodInIsNotCounted()
		{
			KingdomStocks stocks;
			Assert.IsTrue(KingdomCityRules.TryCityStocks(City(
				Zone("a", 100L, 40L, 100L, 0L, 0L),
				Zone("b", 0L, 999L, 999L, 999L, 999L)), out stocks));
			Assert.AreEqual(40L, stocks.Water.Level);
		}

		// ---- The signed counter (§3.5, §3.9; W0 finding (d)) ----------------------------------

		/// <summary>
		/// The finding W0 left open: one NET counter cannot say that a zone owes a food landing and
		/// a water draw at once, which is the ordinary case for a granary zone the city has been
		/// drinking out of. Three signed per-kind figures can, and the weighted counter is derived
		/// from them rather than stored beside them.
		/// </summary>
		[Test]
		public void AZoneCanOweALandingAndADrawAtOnce()
		{
			KingdomZoneRow row = new KingdomZoneRow("a", 0, 100L, default(KingdomStocks), 0, 0, 0, 0, -30, 12, 0);
			KingdomCatchUpCounter counter = KingdomCityRules.CounterFor(row);
			Assert.AreEqual(KingdomCatchUpRules.WeightThirds(KingdomUnitWeight.Medium), counter.LandThirds);
			Assert.AreEqual(KingdomCatchUpRules.WeightThirds(KingdomUnitWeight.Medium), counter.DrawThirds);
			Assert.AreEqual(0, counter.Net, "the net of a landing and a draw of the same weight is zero, which is exactly why one net figure is not enough");
			Assert.AreEqual(6, counter.OwedThirds, "two units are owed even though the net is nothing");
			Assert.IsFalse(counter.IsSettled);
		}

		[Test]
		public void ASettledZoneOwesNothing()
		{
			Assert.IsTrue(KingdomCityRules.CounterFor(Zone("a", 100L, 5L, 10L, 0L, 0L)).IsSettled);
		}

		[Test]
		public void TheCitysOwedIsTheSumOfItsZonesOwed()
		{
			KingdomCatchUpCounter counter = KingdomCityRules.CityCounter(City(
				Zone("a", 100L, 5L, 10L, 0L, 0L, -4),
				Zone("b", 100L, 5L, 10L, 0L, 0L, -9)));
			Assert.AreEqual(6, counter.DrawThirds);
			Assert.AreEqual(0, counter.LandThirds);
		}

		// ---- The carry (§1.2(a) + §3.9) -------------------------------------------------------

		/// <summary>Oldest dedication first, and never out of the zone the founder is standing in:
		/// that zone has just been counted from the ground.</summary>
		[Test]
		public void TheCarryTakesFromTheOldestZoneFirstAndNeverFromTheSeat()
		{
			KingdomCityState state = City(
				Zone("seat", 100L, 0L, 100L, 0L, 0L),
				Zone("old", 100L, 20L, 100L, 0L, 0L),
				Zone("new", 100L, 20L, 100L, 0L, 0L));
			long[] moved = new long[state.ZoneCount];
			long total;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityRules.TryPlanTransfer(state, "seat", KingdomStockKind.Water, 25L, 100L, moved, out total, out fault), fault.ToString());
			Assert.AreEqual(0L, moved[0], "the seated zone is never a source");
			Assert.AreEqual(20L, moved[1], "the oldest dedication goes first");
			Assert.AreEqual(5L, moved[2]);
			Assert.AreEqual(25L, total);
		}

		/// <summary>Nothing is created. What the rows do not hold is not moved, and the shortfall
		/// stays a shortfall.</summary>
		[Test]
		public void TheCarryNeverMovesMoreThanTheRowsHold()
		{
			KingdomCityState state = City(
				Zone("seat", 100L, 0L, 100L, 0L, 0L),
				Zone("far", 100L, 7L, 100L, 0L, 0L));
			long[] moved = new long[state.ZoneCount];
			long total;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityRules.TryPlanTransfer(state, "seat", KingdomStockKind.Water, 500L, 500L, moved, out total, out fault));
			Assert.AreEqual(7L, total);
		}

		/// <summary>Capped by the room the near vessels actually have: water with nowhere to go is
		/// not carried anywhere.</summary>
		[Test]
		public void TheCarryIsCappedByTheRoomWhereItIsGoing()
		{
			KingdomCityState state = City(
				Zone("seat", 100L, 0L, 100L, 0L, 0L),
				Zone("far", 100L, 90L, 100L, 0L, 0L));
			long[] moved = new long[state.ZoneCount];
			long total;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityRules.TryPlanTransfer(state, "seat", KingdomStockKind.Water, 90L, 6L, moved, out total, out fault));
			Assert.AreEqual(6L, total);
		}

		/// <summary>A zone already owing a draw has that much of its level spoken for: the vessels
		/// have not paid it yet, so it may not be given away twice.</summary>
		[Test]
		public void AZoneAlreadyOwingADrawCannotGiveThatWaterAwayTwice()
		{
			KingdomCityState state = City(
				Zone("seat", 100L, 0L, 100L, 0L, 0L),
				Zone("far", 100L, 30L, 100L, 0L, 0L, -25));
			long[] moved = new long[state.ZoneCount];
			long total;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityRules.TryPlanTransfer(state, "seat", KingdomStockKind.Water, 30L, 100L, moved, out total, out fault));
			Assert.AreEqual(5L, total);
		}

		[Test]
		public void AnUnreadZoneIsNeverASource()
		{
			KingdomCityState state = City(
				Zone("seat", 100L, 0L, 100L, 0L, 0L),
				Zone("unseen", 0L, 400L, 400L, 0L, 0L));
			long[] moved = new long[state.ZoneCount];
			long total;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityRules.TryPlanTransfer(state, "seat", KingdomStockKind.Water, 50L, 100L, moved, out total, out fault));
			Assert.AreEqual(0L, total);
		}

		[TestCase(0L)]
		[TestCase(-5L)]
		public void ACarryOfNothingMovesNothing(long demand)
		{
			KingdomCityState state = City(Zone("seat", 100L, 0L, 100L, 0L, 0L), Zone("far", 100L, 50L, 100L, 0L, 0L));
			long[] moved = new long[state.ZoneCount];
			long total;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityRules.TryPlanTransfer(state, "seat", KingdomStockKind.Water, demand, 100L, moved, out total, out fault));
			Assert.AreEqual(0L, total);
		}

		[Test]
		public void ATransferRefusesRatherThanOverrunningTheCallersArray()
		{
			KingdomCityState state = City(Zone("seat", 100L, 0L, 100L, 0L, 0L), Zone("far", 100L, 50L, 100L, 0L, 0L));
			long total;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomCityRules.TryPlanTransfer(state, "seat", KingdomStockKind.Water, 10L, 100L, new long[1], out total, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidIndex, fault);
			Assert.IsFalse(KingdomCityRules.TryPlanTransfer(null, "seat", KingdomStockKind.Water, 10L, 100L, new long[2], out total, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
		}

		// ---- I1 across the carry (§0.0(g), §3.5) ----------------------------------------------

		private static long Held(KingdomCityState state, KingdomStockKind kind)
		{
			long total = 0L;
			for (int i = 0; i < state.ZoneCount; i++)
			{
				KingdomZoneRow row;
				Assert.IsTrue(state.TryZone(i, out row));
				KingdomStockPair pair;
				Assert.IsTrue(row.Stocks.TryGet(kind, out pair));
				total += pair.Level;
			}
			return total;
		}

		private static long Owed(KingdomCityState state, KingdomStockKind kind)
		{
			long total = 0L;
			for (int i = 0; i < state.ZoneCount; i++)
			{
				KingdomZoneRow row;
				Assert.IsTrue(state.TryZone(i, out row));
				total += row.OwedOf(kind);
			}
			return total;
		}

		/// <summary>
		/// **I1**: <c>model total == ground total + counter-owed</c>, per stock kind, at every
		/// instant. A carry lowers a row's level and raises that row's debt against its own vessels
		/// in the same step, so the two move together and the city's books are never richer or
		/// poorer than its ground for having carried water across it.
		/// </summary>
		[Test]
		public void ACarryLowersARowAndRaisesItsDebtByTheSameAmount()
		{
			KingdomCityState state = City(
				Zone("seat", 100L, 0L, 100L, 0L, 0L),
				Zone("far", 100L, 40L, 100L, 0L, 0L));
			long groundBefore = Held(state, KingdomStockKind.Water) - Owed(state, KingdomStockKind.Water);
			long[] moved = new long[state.ZoneCount];
			long total;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityRules.TryPlanTransfer(state, "seat", KingdomStockKind.Water, 25L, 100L, moved, out total, out fault));

			KingdomCityState after;
			long applied;
			Assert.IsTrue(KingdomCityRules.TryApplyTransfer(state, KingdomStockKind.Water, moved, total, out after, out applied, out fault), fault.ToString());
			Assert.AreEqual(25L, applied);
			Assert.AreEqual(15L, Held(after, KingdomStockKind.Water), "the far row gave up what was carried");
			Assert.AreEqual(-25L, Owed(after, KingdomStockKind.Water), "and owes its own vessels exactly that");
			Assert.AreEqual(groundBefore, Held(after, KingdomStockKind.Water) - Owed(after, KingdomStockKind.Water),
				"model total == ground total + counter-owed must hold across the carry");
		}

		/// <summary>The near containers took less than the plan asked for, so less is posted. What
		/// did not land is not owed by anybody.</summary>
		[Test]
		public void OnlyWhatActuallyLandedIsPostedAgainstTheRowsItCameFrom()
		{
			KingdomCityState state = City(
				Zone("seat", 100L, 0L, 100L, 0L, 0L),
				Zone("far", 100L, 40L, 100L, 0L, 0L));
			long[] moved = new long[state.ZoneCount];
			long total;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityRules.TryPlanTransfer(state, "seat", KingdomStockKind.Water, 30L, 100L, moved, out total, out fault));
			KingdomCityState after;
			long applied;
			Assert.IsTrue(KingdomCityRules.TryApplyTransfer(state, KingdomStockKind.Water, moved, 11L, out after, out applied, out fault));
			Assert.AreEqual(11L, applied);
			Assert.AreEqual(29L, Held(after, KingdomStockKind.Water));
			Assert.AreEqual(-11L, Owed(after, KingdomStockKind.Water));
		}

		[Test]
		public void ACarryThatLandedNothingLeavesTheBookByteIdentical()
		{
			KingdomCityState state = City(Zone("seat", 100L, 0L, 100L, 0L, 0L), Zone("far", 100L, 40L, 100L, 0L, 0L));
			KingdomCityState after;
			long applied;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityRules.TryApplyTransfer(state, KingdomStockKind.Water, new long[2] { 0L, 20L }, 0L, out after, out applied, out fault));
			Assert.AreSame(state, after);
			Assert.AreEqual(0L, applied);
		}

		[Test]
		public void ATransferApplicationRefusesRatherThanOverrunningTheCallersArray()
		{
			KingdomCityState state = City(Zone("seat", 100L, 0L, 100L, 0L, 0L), Zone("far", 100L, 40L, 100L, 0L, 0L));
			KingdomCityState after;
			long applied;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomCityRules.TryApplyTransfer(state, KingdomStockKind.Water, new long[1], 5L, out after, out applied, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidIndex, fault);
			Assert.IsFalse(KingdomCityRules.TryApplyTransfer(state, KingdomStockKind.Water, null, 5L, out after, out applied, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
		}

		// ---- The drain at reify (§3.9, I4) -----------------------------------------------------

		/// <summary>An unstamped container has not been counted yet and sorts LAST. Sorting it
		/// first would let a vessel the city has never seen jump the whole queue.</summary>
		[TestCase(1, 1)]
		[TestCase(97, 97)]
		[TestCase(0, int.MaxValue)]
		[TestCase(-4, int.MaxValue)]
		public void AnUncountedContainerSortsLastInTheDrain(int stamped, int expected)
		{
			Assert.AreEqual(expected, KingdomCityRules.DrainOrdinal(stamped));
		}

		/// <summary>
		/// **I4**, composed the way the reify actually composes it: a zone's standing draw is spread
		/// across its dedicated vessels oldest dedication first, the brine one is passed over rather
		/// than partly drained, the uncounted one is reached only after the counted ones, and what
		/// nothing could cover comes back as a named shortfall.
		/// </summary>
		[Test]
		public void AStandingDrawEmptiesTheOldestVesselsFirstAndNamesWhatIsLeft()
		{
			KingdomVesselRow[] vessels = new KingdomVesselRow[4]
			{
				new KingdomVesselRow(10, KingdomCityRules.DrainOrdinal(0), KingdomStockKind.Water, 100L, 100L, true),
				new KingdomVesselRow(11, KingdomCityRules.DrainOrdinal(2), KingdomStockKind.Water, 30L, 60L, true),
				new KingdomVesselRow(12, KingdomCityRules.DrainOrdinal(1), KingdomStockKind.Water, 20L, 60L, true),
				new KingdomVesselRow(13, KingdomCityRules.DrainOrdinal(3), KingdomStockKind.Water, 90L, 90L, false)
			};
			long[] drawn = new long[4];
			long shortfall;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomDrainRules.TryApportion(vessels, 4, KingdomStockKind.Water, 45L, drawn, out shortfall, out fault), fault.ToString());
			Assert.AreEqual(20L, drawn[2], "the oldest dedication goes first");
			Assert.AreEqual(25L, drawn[1], "then the next oldest");
			Assert.AreEqual(0L, drawn[3], "a drain may never launder brine into the books");
			Assert.AreEqual(0L, drawn[0], "the uncounted vessel is only reached after every counted one");
			Assert.AreEqual(0L, shortfall);

			Assert.IsTrue(KingdomDrainRules.TryApportion(vessels, 4, KingdomStockKind.Water, 400L, drawn, out shortfall, out fault));
			Assert.AreEqual(100L, drawn[0], "with the counted vessels dry, the uncounted one pays");
			Assert.AreEqual(250L, shortfall, "and what nothing could cover is named, never forgiven");
			StringAssert.Contains("250 drams", KingdomCityRules.ShortfallNote(-250, 0));
		}

		/// <summary>A unit leaves the debt at the instant it LANDS, never at the instant it is
		/// scheduled — so re-entering or reloading cannot pay the same debt twice.</summary>
		[Test]
		public void SettlingAUnitTakesItOffTheCountAndOnlyOnce()
		{
			KingdomZoneRow row = new KingdomZoneRow("a", 0, 100L, default(KingdomStocks), 0, 0, 0, 0, -9, 0, 0);
			KingdomCatchUpCounter counter = KingdomCityRules.CounterFor(row);
			Assert.AreEqual(3, counter.DrawThirds);
			KingdomCatchUpCounter next;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCatchUpRules.TrySettle(counter, KingdomUnitDirection.Draw, KingdomUnitWeight.Medium, out next, out fault));
			Assert.IsTrue(next.IsSettled);
			Assert.IsFalse(KingdomCatchUpRules.TrySettle(next, KingdomUnitDirection.Draw, KingdomUnitWeight.Medium, out next, out fault),
				"a debt already paid cannot be paid again");
			Assert.IsTrue(KingdomCityRules.CounterFor(row.WithOwed(0, 0, 0)).IsSettled);
		}

		// ---- The reckoning (§2.3, §0.0(a)) ----------------------------------------------------

		private static KingdomAdvanceOutcome<KingdomCityState> Run(KingdomCityState state, int[] waterRates, long days)
		{
			KingdomAdvanceOutcome<KingdomCityState> outcome;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomAdvanceRules.TryRun(
				new KingdomCityAdvanceable(KingdomRules.TicksPerDay, waterRates, null),
				state,
				state.ProcessedThroughTick,
				state.ProcessedThroughTick + days * KingdomRules.TicksPerDay,
				out outcome,
				out fault), fault.ToString());
			return outcome;
		}

		/// <summary>
		/// LIVING-CITY-ARCHITECTURE §0.0(a), the identity the whole design turns on: not one term
		/// in the reckoning contains the elapsed. At W1's own rates a day away and a season away
		/// are the same arithmetic exactly; the assertion is here so that the moment a later wave
		/// gives a lane a per-day term, this fails.
		/// </summary>
		[Test]
		public void ADayAwayAndASeasonAwayCostTheSameReckoning()
		{
			KingdomCityState state = City(Zone("a", 100L, 10L, 100L, 0L, 0L), Zone("b", 100L, 10L, 100L, 0L, 0L));
			KingdomAdvanceOutcome<KingdomCityState> day = Run(state, null, 1L);
			KingdomAdvanceOutcome<KingdomCityState> season = Run(state, null, 90L);
			Assert.AreEqual(day.Steps, season.Steps);
			Assert.AreEqual(day.RowVisits, season.RowVisits);
			Assert.AreEqual(2L * state.RowCount, day.RowVisits, "one pass is one propose and one apply over every row");
		}

		/// <summary>
		/// With a rate running, the count is bounded by the MODEL and not by the span: a stock can
		/// only cross its ceiling once, so ninety days and nine hundred cost the same.
		/// </summary>
		[Test]
		public void AStockThatFillsCostsTheSameOverNinetyDaysAndOverNineHundred()
		{
			KingdomCityState state = City(Zone("a", 100L, 0L, 100L, 0L, 0L));
			KingdomAdvanceOutcome<KingdomCityState> ninety = Run(state, new int[1] { 50 }, 90L);
			KingdomAdvanceOutcome<KingdomCityState> nineHundred = Run(state, new int[1] { 50 }, 900L);
			Assert.AreEqual(ninety.Steps, nineHundred.Steps);
			Assert.AreEqual(ninety.RowVisits, nineHundred.RowVisits);
			KingdomZoneRow row;
			Assert.IsTrue(nineHundred.State.TryZone(0, out row));
			Assert.AreEqual(100L, row.Stocks.Water.Level, "a stock integrates to its ceiling and clamps there");
		}

		/// <summary>A stock running down stops at empty, and stopping is a breakpoint rather than
		/// a clamp discovered afterwards.</summary>
		[Test]
		public void AStockRunningDownStopsAtEmpty()
		{
			KingdomCityState state = City(Zone("a", 100L, 60L, 100L, 0L, 0L));
			KingdomAdvanceOutcome<KingdomCityState> outcome = Run(state, new int[1] { -20 }, 90L);
			KingdomZoneRow row;
			Assert.IsTrue(outcome.State.TryZone(0, out row));
			Assert.AreEqual(0L, row.Stocks.Water.Level);
			Assert.IsFalse(outcome.Overflowed, "three days of draining is not a breakpoint overflow");
		}

		/// <summary>The model is advanced by whole units consumed with the remainder kept, never
		/// re-anchored to now, so running the same span twice is idempotent.</summary>
		[Test]
		public void ReckoningTheSameSpanTwiceChangesNothingTheSecondTime()
		{
			KingdomCityState state = City(Zone("a", 100L, 0L, 100L, 0L, 0L));
			KingdomAdvanceOutcome<KingdomCityState> first = Run(state, new int[1] { 10 }, 3L);
			KingdomAdvanceOutcome<KingdomCityState> second = Run(first.State, new int[1] { 10 }, 0L);
			KingdomZoneRow after;
			Assert.IsTrue(second.State.TryZone(0, out after));
			Assert.AreEqual(30L, after.Stocks.Water.Level);
			Assert.AreEqual(first.ProcessedThroughTick, second.ProcessedThroughTick);
		}

		[Test]
		public void AReckoningRefusesABackwardClockRatherThanRepairingIt()
		{
			KingdomCityState state = City(Zone("a", 100L, 0L, 100L, 0L, 0L));
			KingdomCityState advanced;
			KingdomCityFault fault;
			Assert.IsTrue(state.TryWithProcessedThroughTick(5000L, out advanced, out fault));
			KingdomAdvanceOutcome<KingdomCityState> outcome;
			Assert.IsFalse(KingdomAdvanceRules.TryRun(
				new KingdomCityAdvanceable(KingdomRules.TicksPerDay, null, null), advanced, 5000L, 4000L, out outcome, out fault));
			Assert.AreEqual(KingdomCityFault.ClockRegression, fault);
		}

		/// <summary>The reckon job is what actually crosses the executor, so its boundary is what
		/// §2.5's reflection test has to be clean about.</summary>
		[Test]
		public void TheReckonJobsBoundaryCarriesNoEngineTypeAndNothingMutable()
		{
			KingdomComputeRefusal refusal;
			string offender;
			Assert.IsTrue(KingdomComputeSeam.TryValidateBoundary(typeof(KingdomReckonInput), typeof(KingdomCityState), out refusal, out offender),
				"the reckon boundary is not clean: " + refusal + " at " + offender);
		}

		/// <summary>One city, one pass, through the executor and nowhere else — with a receipt the
		/// tester can read against §0.0's table.</summary>
		[Test]
		public void AReckoningThroughTheSeamPublishesAndLeavesAReceipt()
		{
			KingdomComputeJournalRing journal = new KingdomComputeJournalRing();
			KingdomExecutor executor = new KingdomExecutor(new ScriptedClock(new long[2] { 1000L, 1600L }), journal);
			KingdomCityState state = City(Zone("a", 100L, 0L, 100L, 0L, 0L), Zone("b", 100L, 0L, 100L, 0L, 0L));
			KingdomComputeResult<KingdomCityState> result = executor.Submit(
				new KingdomReckonInput(state, 90L * KingdomRules.TicksPerDay),
				new KingdomReckonJob("taf:city:kavvat", new KingdomCityAdvanceable(KingdomRules.TicksPerDay, null, null)));
			Assert.AreEqual(KingdomComputeStatus.Ok, result.Status);
			Assert.AreEqual(90L * KingdomRules.TicksPerDay, result.Value.ProcessedThroughTick);
			Assert.AreEqual(KingdomBudgetLane.Reckon, result.Receipt.Lane);
			Assert.AreEqual(600L, result.Receipt.Microseconds);
			Assert.AreEqual(1, result.Receipt.Counters.BreakpointSteps);
			Assert.AreEqual(2L * state.RowCount, result.Receipt.Counters.RowVisits);
			Assert.AreEqual(0, result.Receipt.Counters.Draws, "not one draw anywhere in a reckoning");
			Assert.AreEqual(KingdomBudgetVerdict.Within, result.Receipt.Verdict);
			Assert.AreEqual(1, journal.Count);
		}

		/// <summary>The receipt line the log-watcher greps for, in §6.5's own shape, with the tag
		/// KingdomLog stamps kept out of the body so it is never written twice.</summary>
		[Test]
		public void TheReceiptLineReadsTheWayTheConstitutionWroteIt()
		{
			KingdomPerfReceipt receipt = new KingdomPerfReceipt(
				KingdomBudgetLane.Reckon, "taf:city:kavvat", 1400L,
				new KingdomComputeCounters(41, 4756L, 118, 0, 0L), 118L,
				KingdomBudgetVerdict.Within, KingdomBudgetVerdict.Within);
			Assert.AreEqual("perf reckon label=taf:city:kavvat steps=41 rows=4756 draws=118 ms=1.4",
				KingdomBudgetRules.FormatReceiptBody(receipt));
			Assert.AreEqual(KingdomBudgetRules.LogPrefix + KingdomBudgetRules.FormatReceiptBody(receipt),
				KingdomBudgetRules.FormatReceipt(receipt));
		}

		// ---- The seed (W0's deferral) ---------------------------------------------------------

		/// <summary>Minted once, from the world seed, the realm's name and the tick the water was
		/// poured: the same realm across a reload gets the same seed, and two realms in one world
		/// do not.</summary>
		[Test]
		public void TheSeedIsDeterministicAndSeparatedByRealm()
		{
			KernelSeed128 first;
			KernelSeed128 again;
			KernelSeed128 other;
			KernelSeed128 later;
			KernelSeed128 elsewhere;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityRules.TryMintSeed(1234, "Kavvat", 5000L, out first, out fault));
			Assert.IsTrue(KingdomCityRules.TryMintSeed(1234, "Kavvat", 5000L, out again, out fault));
			Assert.IsTrue(KingdomCityRules.TryMintSeed(1234, "Ptoh", 5000L, out other, out fault));
			Assert.IsTrue(KingdomCityRules.TryMintSeed(1234, "Kavvat", 5001L, out later, out fault));
			Assert.IsTrue(KingdomCityRules.TryMintSeed(9999, "Kavvat", 5000L, out elsewhere, out fault));
			Assert.IsTrue(first.Equals(again), "the same realm must mint the same seed twice");
			Assert.IsFalse(first.Equals(other), "two realms in one world must not share a seed");
			Assert.IsFalse(first.Equals(later));
			Assert.IsFalse(first.Equals(elsewhere));
			Assert.AreNotEqual(0UL, first.High);
			Assert.AreNotEqual(first.High, first.Low, "the two halves must be separated by their own basis");
		}

		[Test]
		public void TheSeedRefusesNonsenseRatherThanMintingIt()
		{
			KernelSeed128 seed;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomCityRules.TryMintSeed(1, null, 0L, out seed, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
			Assert.IsFalse(KingdomCityRules.TryMintSeed(1, "Kavvat", -1L, out seed, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidTick, fault);
		}

		// ---- Districts, ids, and the lines the founder reads -----------------------------------

		[Test]
		public void EveryDistrictCodeRoundTripsToItsOwnKey()
		{
			for (int i = 0; i < KingdomRules.Districts.Length; i++)
			{
				int code = KingdomCityRules.DistrictCode(KingdomRules.Districts[i]);
				Assert.AreNotEqual(KingdomCityRules.NoDistrict, code, KingdomRules.Districts[i] + " has no code");
				Assert.AreEqual(KingdomRules.Districts[i], KingdomCityRules.DistrictKey(code));
			}
			Assert.AreEqual(KingdomCityRules.NoDistrict, KingdomCityRules.DistrictCode("scriptorium-of-nowhere"));
			Assert.AreEqual(KingdomCityRules.NoDistrict, KingdomCityRules.DistrictCode(null));
			Assert.IsNull(KingdomCityRules.DistrictKey(KingdomCityRules.NoDistrict));
			Assert.IsNull(KingdomCityRules.DistrictKey(9999));
		}

		[Test]
		public void AStableIdIsStableNonNegativeAndDistinct()
		{
			Assert.AreEqual(KingdomCityRules.StableId("taf:work:cistern"), KingdomCityRules.StableId("taf:work:cistern"));
			Assert.AreNotEqual(KingdomCityRules.StableId("taf:work:cistern"), KingdomCityRules.StableId("taf:work:granary"));
			Assert.GreaterOrEqual(KingdomCityRules.StableId("taf:work:cistern"), 0);
			Assert.AreEqual(0, KingdomCityRules.StableId(null));
			Assert.AreEqual(0, KingdomCityRules.StableId(""));
		}

		/// <summary>The ground wins for anything physical, and the difference is attributed and
		/// told rather than silently repaired (§3.1 step 4).</summary>
		[Test]
		public void AReconcileSaysNothingWhenTheBooksAndTheStoresAgree()
		{
			Assert.IsNull(KingdomCityRules.ReconcileNote(0L, 0L));
		}

		[TestCase(-12L, 0L, "12 drams fewer")]
		[TestCase(4L, 0L, "4 drams more")]
		[TestCase(0L, -3L, "3 fewer servings")]
		[TestCase(-2L, 5L, "and")]
		public void AReconcileNamesWhatMovedAndWhichWay(long water, long food, string expected)
		{
			string note = KingdomCityRules.ReconcileNote(water, food);
			Assert.IsNotNull(note);
			StringAssert.Contains(expected, note);
		}

		/// <summary>What the containers could not cover is named, never silently forgiven.</summary>
		[Test]
		public void AShortfallIsNamedRatherThanForgiven()
		{
			Assert.IsNull(KingdomCityRules.ShortfallNote(0, 0));
			StringAssert.Contains("9 drams", KingdomCityRules.ShortfallNote(-9, 0));
			StringAssert.Contains("no room", KingdomCityRules.ShortfallNote(4, 0));
		}

		/// <summary>
		/// The audit invariant, as the line a tester greps for: model total == ground total after an
		/// attended pass, and a mismatch says so rather than being repaired.
		/// </summary>
		[Test]
		public void TheAuditLineNamesAMismatchAndStaysQuietWhenThereIsNone()
		{
			StringAssert.DoesNotContain("MISMATCH", KingdomCityRules.AuditNote(40L, 40L, 6L, 6L, 0));
			StringAssert.Contains("MISMATCH", KingdomCityRules.AuditNote(40L, 31L, 6L, 6L, 0));
			StringAssert.Contains("owed=6/3", KingdomCityRules.AuditNote(40L, 40L, 6L, 6L, 6));
		}

		[Test]
		public void ACarryIsAnnouncedInTheRegisterTheLedgerUses()
		{
			StringAssert.Contains("oldest casks first", KingdomCityRules.CarryNote(KingdomStockKind.Water, 12L, "Kavvat"));
			StringAssert.Contains("pantries", KingdomCityRules.CarryNote(KingdomStockKind.Food, 3L, "Kavvat"));
			Assert.IsNull(KingdomCityRules.CarryNote(KingdomStockKind.Water, 0L, "Kavvat"));
			Assert.IsNull(KingdomCityRules.CarryNote(KingdomStockKind.Materials, 5L, "Kavvat"));
		}
	}
}
#endif
