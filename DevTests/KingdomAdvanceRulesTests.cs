#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomAdvanceAbiTests
	{
		[Test]
		public void BreakpointKindsKeepExactByteOrder()
		{
			Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(KingdomBreakpointKind)));
			Assert.AreEqual("0:None,1:StockEmpty,2:StockFull,3:CropStage,4:ClockDue,5:BrinkExpiry,6:SubsidenceRung,7:StageChange,8:Horizon",
				string.Join(",", Array.ConvertAll((KingdomBreakpointKind[])Enum.GetValues(
					typeof(KingdomBreakpointKind)), value => ((byte)value) + ":" + value)));
		}
	}

	/// <summary>
	/// A toy city with exactly two rate sources: one fixed-period clock, folded O(1) the way
	/// <c>TickMath.TryCountFixedPeriodDue</c>'s own doc-comment demands, and one stock draining at
	/// a constant rate. Immutable, engine-free, and just rich enough to make the O(model) claim
	/// falsifiable.
	/// </summary>
	internal sealed class ToyCityState
	{
		internal readonly long ThroughTick;

		internal readonly long Level;

		internal readonly long Capacity;

		internal readonly long RatePerDay;

		internal readonly long ClockNextDueTick;

		internal readonly long ClockIntervalTicks;

		/// <summary>Occurrences folded in. A run that came and went unwitnessed is one dated line,
		/// not a queue standing since spring -- KingdomRules.PassagesThrough's own discipline.</summary>
		internal readonly long ClockOccurrences;

		/// <summary>One dated line per fold, so a draw is per happening and never per day.</summary>
		internal readonly int Draws;

		internal readonly bool AtFixedPoint;

		internal ToyCityState(long throughTick, long level, long capacity, long ratePerDay, long clockNextDueTick, long clockIntervalTicks, long clockOccurrences, int draws, bool atFixedPoint)
		{
			ThroughTick = throughTick;
			Level = level;
			Capacity = capacity;
			RatePerDay = ratePerDay;
			ClockNextDueTick = clockNextDueTick;
			ClockIntervalTicks = clockIntervalTicks;
			ClockOccurrences = clockOccurrences;
			Draws = draws;
			AtFixedPoint = atFixedPoint;
		}
	}

	/// <summary>
	/// A toy city with exactly two rate sources. The stock's crossing IS a rate change, so it
	/// proposes a breakpoint. The clock is a fixed-period lane and proposes NOTHING -- its firing
	/// changes no rate, so its occurrences are folded O(1) inside whichever segment contains them,
	/// exactly as TickMath.TryCountFixedPeriodDue's doc-comment demands of a consumer. That
	/// distinction is the whole of O(model) rather than O(days).
	/// </summary>
	internal sealed class ToyCityModel : IKingdomAdvanceable<ToyCityState>
	{
		internal const long TicksPerDay = ThousandAndFirst.KingdomRules.TicksPerDay;

		private readonly int rows;

		internal ToyCityModel(int rows)
		{
			this.rows = rows;
		}

		public int RowCount(ToyCityState state)
		{
			return rows;
		}

		public bool TryProposeNext(ToyCityState state, long fromTick, long horizonTick, out KingdomBreakpoint breakpoint, out KingdomCityFault fault)
		{
			breakpoint = KingdomBreakpoint.None;
			KingdomBreakpoint[] candidates = new KingdomBreakpoint[1];
			int count = 0;
			long ticksUntil;
			KingdomBreakpointKind kind;
			if (KingdomAdvanceRules.TryCrossingTicks(state.Level, state.Capacity, state.RatePerDay, TicksPerDay, out ticksUntil, out kind, out fault))
			{
				candidates[count] = new KingdomBreakpoint(kind, fromTick + ticksUntil, 1);
				count++;
			}
			else if (fault != KingdomCityFault.None)
			{
				return false;
			}
			if (!KingdomAdvanceRules.TryEarliest(candidates, count, fromTick, horizonTick, out breakpoint, out fault))
			{
				breakpoint = KingdomBreakpoint.None;
				return fault == KingdomCityFault.None;
			}
			return true;
		}

		public bool TryApply(ToyCityState state, KingdomBreakpoint breakpoint, out ToyCityState next, out KingdomCityFault fault)
		{
			next = state;
			long through = breakpoint.Tick;
			if (through < state.ThroughTick)
			{
				fault = KingdomCityFault.ClockRegression;
				return false;
			}
			long level;
			if (!KingdomAdvanceRules.TryIntegrateSegment(state.Level, state.Capacity, state.RatePerDay, through - state.ThroughTick, TicksPerDay, out level, out fault))
			{
				return false;
			}
			long occurrences = state.ClockOccurrences;
			long nextDue = state.ClockNextDueTick;
			int draws = state.Draws;
			if (state.ClockIntervalTicks > 0L && through >= state.ClockNextDueTick)
			{
				ulong due;
				long following;
				KernelFaultCode kernelFault;
				if (!TickMath.TryCountFixedPeriodDue(through, state.ClockNextDueTick, state.ClockIntervalTicks, out due, out following, out kernelFault))
				{
					fault = KingdomCityFaults.FromKernel(kernelFault);
					return false;
				}
				occurrences += (long)due;
				nextDue = following;
				if (due > 0uL)
				{
					draws++;
				}
			}
			long rate = state.RatePerDay;
			if (breakpoint.Kind == KingdomBreakpointKind.StockEmpty || breakpoint.Kind == KingdomBreakpointKind.StockFull)
			{
				level = (breakpoint.Kind == KingdomBreakpointKind.StockEmpty) ? 0L : state.Capacity;
				rate = 0L;
			}
			next = new ToyCityState(through, level, state.Capacity, rate, nextDue, state.ClockIntervalTicks, occurrences, draws, state.AtFixedPoint);
			fault = KingdomCityFault.None;
			return true;
		}

		public bool TryJumpToFixedPoint(ToyCityState state, long throughTick, out ToyCityState next, out KingdomCityFault fault)
		{
			next = new ToyCityState(throughTick, state.Level, state.Capacity, 0L, throughTick + state.ClockIntervalTicks, state.ClockIntervalTicks,
				state.ClockOccurrences, state.Draws, atFixedPoint: true);
			fault = KingdomCityFault.None;
			return true;
		}
	}

	/// <summary>A model that never runs out of structural changes, so the step budget has to be the
	/// thing that stops it.</summary>
	internal sealed class EndlessModel : IKingdomAdvanceable<int>
	{
		public int RowCount(int state)
		{
			return 10;
		}

		public bool TryProposeNext(int state, long fromTick, long horizonTick, out KingdomBreakpoint breakpoint, out KingdomCityFault fault)
		{
			breakpoint = new KingdomBreakpoint(KingdomBreakpointKind.CropStage, fromTick + 10L, 0);
			fault = KingdomCityFault.None;
			return true;
		}

		public bool TryApply(int state, KingdomBreakpoint breakpoint, out int next, out KingdomCityFault fault)
		{
			next = state + 1;
			fault = KingdomCityFault.None;
			return true;
		}

		public bool TryJumpToFixedPoint(int state, long throughTick, out int next, out KingdomCityFault fault)
		{
			next = -1;
			fault = KingdomCityFault.None;
			return true;
		}
	}

	internal sealed class FaultingModel : IKingdomAdvanceable<int>
	{
		public int RowCount(int state)
		{
			return 1;
		}

		public bool TryProposeNext(int state, long fromTick, long horizonTick, out KingdomBreakpoint breakpoint, out KingdomCityFault fault)
		{
			breakpoint = KingdomBreakpoint.None;
			fault = KingdomCityFault.InvalidRate;
			return false;
		}

		public bool TryApply(int state, KingdomBreakpoint breakpoint, out int next, out KingdomCityFault fault)
		{
			next = state;
			fault = KingdomCityFault.None;
			return true;
		}

		public bool TryJumpToFixedPoint(int state, long throughTick, out int next, out KingdomCityFault fault)
		{
			next = state;
			fault = KingdomCityFault.None;
			return true;
		}
	}

	/// <summary>
	/// Breakpoint integration: O(model), not O(days). LIVING-CITY-ARCHITECTURE §2.3, and the
	/// identity §0.0(a) turns on — not one term in the elapsed.
	/// </summary>
	public class KingdomAdvanceRulesTests
	{
		private const long Day = ToyCityModel.TicksPerDay;

		private static ToyCityState ClockOnly()
		{
			return new ToyCityState(0L, 100L, 100L, 0L, 600L, Day, 0L, 0, false);
		}

		/// <summary>
		/// The assertion Pass 32 step 90a makes: a one-day and a ninety-day reckoning of the same
		/// model do the same row-visits and the same draws. Only the folded occurrence count
		/// differs. If they scaled with the absence, a lane would be drawing per day and it would
		/// be the lane that is wrong.
		/// </summary>
		[Test]
		public void ADayAwayAndASeasonAwayCostExactlyTheSame()
		{
			ToyCityModel model = new ToyCityModel(116);
			KingdomAdvanceOutcome<ToyCityState> day;
			KingdomAdvanceOutcome<ToyCityState> season;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomAdvanceRules.TryRun(model, ClockOnly(), 0L, Day, out day, out fault), fault.ToString());
			Assert.IsTrue(KingdomAdvanceRules.TryRun(model, ClockOnly(), 0L, 90L * Day, out season, out fault), fault.ToString());

			Assert.AreEqual(day.Steps, season.Steps, "steps scaled with the absence");
			Assert.AreEqual(day.RowVisits, season.RowVisits, "row-visits scaled with the absence");
			Assert.AreEqual(day.State.Draws, season.State.Draws, "draws scaled with the absence");
			Assert.AreEqual(1, day.State.Draws, "one dated line per fold, not one per day");
			Assert.AreEqual(1L, day.State.ClockOccurrences);
			Assert.AreEqual(90L, season.State.ClockOccurrences, "the fold lost the occurrences it was supposed to count");
		}

		[Test]
		public void RowVisitsAreStepsTimesTwoRAndStayUnderTheLiveCeiling()
		{
			ToyCityModel model = new ToyCityModel(116);
			KingdomAdvanceOutcome<ToyCityState> outcome;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomAdvanceRules.TryRun(model, ClockOnly(), 0L, 90L * Day, out outcome, out fault));
			Assert.AreEqual((long)outcome.Steps * 2L * 116L, outcome.RowVisits);
			long ceiling;
			Assert.IsTrue(KingdomBudgetRules.TryMaxRowVisits(116, out ceiling));
			Assert.AreEqual(14848L, ceiling, "the constitution's own worst case for today's caps");
			Assert.LessOrEqual(outcome.RowVisits, ceiling);
		}

		/// <summary>The ceiling is a formula over the live R, so it survives the zone cap moving.
		/// LIVING-CITY-ARCHITECTURE §0.0(f).</summary>
		[TestCase(116, 14848L)]
		[TestCase(246, 31488L)]
		[TestCase(0, 0L)]
		public void TheRowVisitCeilingIsComputedFromTheLiveR(int rows, long expected)
		{
			long ceiling;
			Assert.IsTrue(KingdomBudgetRules.TryMaxRowVisits(rows, out ceiling));
			Assert.AreEqual(expected, ceiling);
		}

		[Test]
		public void AStockCrossingIsOneBreakpointAndTheRateGoesFlatAfterIt()
		{
			ToyCityModel model = new ToyCityModel(10);
			ToyCityState start = new ToyCityState(0L, 30L, 100L, -10L, 0L, 0L, 0L, 0, false);
			KingdomAdvanceOutcome<ToyCityState> outcome;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomAdvanceRules.TryRun(model, start, 0L, 90L * Day, out outcome, out fault), fault.ToString());
			Assert.AreEqual(0L, outcome.State.Level, "the stock did not reach empty");
			Assert.AreEqual(2, outcome.Steps, "one crossing and one closing pass");
			Assert.IsFalse(outcome.Overflowed);
		}

		/// <summary>
		/// The honest overflow of §2.3: on hitting the cap the model jumps to the fixed point and
		/// dates the remainder as settled, rather than truncating in silence. Row-visits still come
		/// in at exactly the constitution's <c>B x 2R</c>.
		/// </summary>
		[Test]
		public void AnEndlessModelStopsAtTheCapAndJumpsToItsFixedPoint()
		{
			EndlessModel model = new EndlessModel();
			KingdomAdvanceOutcome<int> outcome;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomAdvanceRules.TryRun(model, 0, 0L, 1000000L, out outcome, out fault), fault.ToString());
			Assert.IsTrue(outcome.Overflowed, "the cap was reached without saying so");
			Assert.AreEqual(KingdomAdvanceRules.MaxPasses, outcome.Steps);
			Assert.AreEqual(KingdomBudgetRules.MaxBreakpoints, KingdomAdvanceRules.MaxPasses);
			Assert.AreEqual((long)KingdomAdvanceRules.MaxPasses * 2L * 10L, outcome.RowVisits);
			Assert.AreEqual(-1, outcome.State, "the fixed-point jump did not run");
			Assert.AreEqual(1000000L, outcome.ProcessedThroughTick, "the remainder was not dated as settled");
		}

		[Test]
		public void AnEmptySpanIsOneClosingPassAndChangesNothing()
		{
			ToyCityModel model = new ToyCityModel(116);
			ToyCityState start = new ToyCityState(5000L, 100L, 100L, 0L, 9000L, Day, 0L, 0, false);
			KingdomAdvanceOutcome<ToyCityState> outcome;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomAdvanceRules.TryRun(model, start, 5000L, 5000L, out outcome, out fault));
			Assert.AreEqual(1, outcome.Steps);
			Assert.AreEqual(0, outcome.State.Draws, "an empty span drew");
			Assert.AreEqual(5000L, outcome.ProcessedThroughTick);
		}

		[Test]
		public void ABackwardSpanIsRefusedAndPublishesNothing()
		{
			ToyCityModel model = new ToyCityModel(1);
			KingdomAdvanceOutcome<ToyCityState> outcome;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomAdvanceRules.TryRun(model, ClockOnly(), 5000L, 4999L, out outcome, out fault));
			Assert.AreEqual(KingdomCityFault.ClockRegression, fault);
			Assert.AreEqual(0, outcome.Steps);
			Assert.IsNull(outcome.State);
		}

		[Test]
		public void ANullModelAndANegativeTickAreBothRefused()
		{
			KingdomAdvanceOutcome<ToyCityState> outcome;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomAdvanceRules.TryRun<ToyCityState>(null, ClockOnly(), 0L, 10L, out outcome, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
			Assert.IsFalse(KingdomAdvanceRules.TryRun(new ToyCityModel(1), ClockOnly(), -1L, 10L, out outcome, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidTick, fault);
		}

		[Test]
		public void AProposeFaultAbortsTheWholeAdvancement()
		{
			KingdomAdvanceOutcome<int> outcome;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomAdvanceRules.TryRun(new FaultingModel(), 7, 0L, 100L, out outcome, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidRate, fault);
			Assert.AreEqual(0, outcome.Steps);
		}

		// ---- The crossing solver ---------------------------------------------------------

		/// <summary>A crossing is computed, never searched. Ceiling division, because a stock that
		/// runs out part-way through a day runs out that day.</summary>
		[TestCase(30L, 100L, -10L, 3L, (int)KingdomBreakpointKind.StockEmpty)]
		[TestCase(35L, 100L, -10L, 4L, (int)KingdomBreakpointKind.StockEmpty)]
		[TestCase(0L, 100L, -10L, 0L, (int)KingdomBreakpointKind.StockEmpty)]
		[TestCase(90L, 100L, 10L, 1L, (int)KingdomBreakpointKind.StockFull)]
		[TestCase(0L, 100L, 3L, 34L, (int)KingdomBreakpointKind.StockFull)]
		[TestCase(100L, 100L, 10L, 0L, (int)KingdomBreakpointKind.StockFull)]
		public void ACrossingIsSolvedRatherThanStepped(long level, long capacity, long ratePerDay, long expectedDays, int expectedKind)
		{
			long ticks;
			KingdomBreakpointKind kind;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomAdvanceRules.TryCrossingTicks(level, capacity, ratePerDay, Day, out ticks, out kind, out fault));
			Assert.AreEqual(expectedDays * Day, ticks);
			Assert.AreEqual((KingdomBreakpointKind)expectedKind, kind);
		}

		[Test]
		public void AFlatRateHasNoCrossingAndThatIsNotAFault()
		{
			long ticks;
			KingdomBreakpointKind kind;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomAdvanceRules.TryCrossingTicks(50L, 100L, 0L, Day, out ticks, out kind, out fault));
			Assert.AreEqual(KingdomCityFault.None, fault, "a stock that is not moving will not arrive, and that is not a fault");
			Assert.AreEqual(KingdomBreakpointKind.None, kind);
		}

		[TestCase(50L, 100L, -10L, 0L, (int)KingdomCityFault.InvalidInterval)]
		[TestCase(150L, 100L, -10L, 1200L, (int)KingdomCityFault.InvalidCapacity)]
		[TestCase(-1L, 100L, -10L, 1200L, (int)KingdomCityFault.InvalidCapacity)]
		public void ACorruptCrossingIsRefusedByName(long level, long capacity, long ratePerDay, long ticksPerDay, int expected)
		{
			long ticks;
			KingdomBreakpointKind kind;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomAdvanceRules.TryCrossingTicks(level, capacity, ratePerDay, ticksPerDay, out ticks, out kind, out fault));
			Assert.AreEqual((KingdomCityFault)expected, fault);
		}

		// ---- Linear integration between breakpoints --------------------------------------

		[TestCase(50L, 100L, -10L, 2L, 30L)]
		[TestCase(50L, 100L, -10L, 10L, 0L)]
		[TestCase(50L, 100L, 20L, 10L, 100L)]
		[TestCase(50L, 100L, 0L, 90L, 50L)]
		[TestCase(50L, 100L, -10L, 0L, 50L)]
		public void ASegmentIntegratesLinearlyAndClampsAtTheEdges(long level, long capacity, long ratePerDay, long days, long expected)
		{
			long next;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomAdvanceRules.TryIntegrateSegment(level, capacity, ratePerDay, days * Day, Day, out next, out fault));
			Assert.AreEqual(expected, next);
		}

		/// <summary>A part-day remainder buys nothing and is not forgiven either: it stays in the
		/// span the caller keeps, exactly as AdvanceCheckpoint keeps its own.</summary>
		[Test]
		public void APartDayMovesNothing()
		{
			long next;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomAdvanceRules.TryIntegrateSegment(50L, 100L, -10L, Day - 1L, Day, out next, out fault));
			Assert.AreEqual(50L, next);
		}

		// ---- Earliest selection, and its frozen tie-break ---------------------------------

		[Test]
		public void TheEarliestBreakpointWinsAndTiesBreakOnKindThenRow()
		{
			KingdomBreakpoint[] candidates = new KingdomBreakpoint[4]
			{
				new KingdomBreakpoint(KingdomBreakpointKind.ClockDue, 500L, 7),
				new KingdomBreakpoint(KingdomBreakpointKind.CropStage, 300L, 4),
				new KingdomBreakpoint(KingdomBreakpointKind.StockEmpty, 300L, 9),
				new KingdomBreakpoint(KingdomBreakpointKind.StockEmpty, 300L, 2)
			};
			KingdomBreakpoint earliest;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomAdvanceRules.TryEarliest(candidates, 4, 0L, 1000L, out earliest, out fault));
			Assert.AreEqual(KingdomBreakpointKind.StockEmpty, earliest.Kind, "the tie broke on the wrong key");
			Assert.AreEqual(2, earliest.RowIndex);
			Assert.AreEqual(300L, earliest.Tick);
		}

		[Test]
		public void CandidatesOutsideTheSpanAreNotBreakpoints()
		{
			KingdomBreakpoint[] candidates = new KingdomBreakpoint[3]
			{
				new KingdomBreakpoint(KingdomBreakpointKind.ClockDue, 50L, 0),
				new KingdomBreakpoint(KingdomBreakpointKind.ClockDue, 5000L, 1),
				KingdomBreakpoint.None
			};
			KingdomBreakpoint earliest;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomAdvanceRules.TryEarliest(candidates, 3, 100L, 1000L, out earliest, out fault));
			Assert.AreEqual(KingdomCityFault.None, fault);
			Assert.AreEqual(KingdomBreakpointKind.None, earliest.Kind);
		}

		[Test]
		public void ANullOrOverlongCandidateSetIsRefused()
		{
			KingdomBreakpoint earliest;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomAdvanceRules.TryEarliest(null, 0, 0L, 10L, out earliest, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
			Assert.IsFalse(KingdomAdvanceRules.TryEarliest(new KingdomBreakpoint[1], 2, 0L, 10L, out earliest, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidIndex, fault);
		}
	}
}
#endif
