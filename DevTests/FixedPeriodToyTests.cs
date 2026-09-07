#if TAF_TESTS
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Tests
{
	public class FixedPeriodToyTests
	{
		[Test]
		public void ExtractedToyDeclarationsKeepExactTopLevelAbiAndDefaults()
		{
			ClassicAssert.AreEqual("ThousandAndFirst.Simulation.Kernel.ToyPulseRange", typeof(ToyPulseRange).FullName);
			ClassicAssert.AreEqual("ThousandAndFirst.Simulation.Kernel.FixedPeriodToyState", typeof(FixedPeriodToyState).FullName);
			ClassicAssert.AreEqual("ThousandAndFirst.Simulation.Kernel.ToyAdvanceResult", typeof(ToyAdvanceResult).FullName);
			ClassicAssert.AreEqual("ThousandAndFirst.Simulation.Kernel.FixedPeriodToyRules", typeof(FixedPeriodToyRules).FullName);
			ClassicAssert.IsTrue(typeof(ToyPulseRange).IsValueType);
			ClassicAssert.IsTrue(typeof(ToyAdvanceResult).IsValueType);
			ClassicAssert.IsTrue(typeof(FixedPeriodToyState).IsClass && typeof(FixedPeriodToyState).IsSealed);
			ClassicAssert.IsTrue(typeof(FixedPeriodToyRules).IsAbstract && typeof(FixedPeriodToyRules).IsSealed);
			ClassicAssert.IsTrue(typeof(ToyPulseRange).IsNotPublic);
			ClassicAssert.IsTrue(typeof(FixedPeriodToyState).IsNotPublic);
			ClassicAssert.IsTrue(typeof(ToyAdvanceResult).IsNotPublic);
			ClassicAssert.IsTrue(typeof(FixedPeriodToyRules).IsNotPublic);

			AssertFields(typeof(ToyPulseRange),
				new[] { "RulesVersionAtCreation", "EventStreamId", "EventKindCode", "FirstOrdinal", "Count" },
				new[] { typeof(int), typeof(string), typeof(uint), typeof(ulong), typeof(ulong) });
			AssertFields(typeof(FixedPeriodToyState),
				new[] { "SchemaVersion", "RulesVersion", "SimulationSeed", "SettlementId", "ProcessedThroughTick",
					"ClockScheduled", "NextDueTick", "NextOrdinal", "IntervalTicks", "OptionLatch",
					"HasEmittedRange", "EmittedRange" },
				new[] { typeof(int), typeof(int), typeof(KernelSeed128), typeof(string), typeof(long),
					typeof(bool), typeof(long), typeof(ulong), typeof(long), typeof(OptionLatchState),
					typeof(bool), typeof(ToyPulseRange) });
			AssertFields(typeof(ToyAdvanceResult),
				new[] { "State", "OptionTransition", "Fault" },
				new[] { typeof(FixedPeriodToyState), typeof(OptionTransitionKind), typeof(KernelFaultCode) });

			ToyPulseRange emptyRange = default(ToyPulseRange);
			ClassicAssert.AreEqual(0, emptyRange.RulesVersionAtCreation);
			ClassicAssert.IsNull(emptyRange.EventStreamId);
			ClassicAssert.AreEqual(0u, emptyRange.EventKindCode);
			ClassicAssert.AreEqual(0uL, emptyRange.FirstOrdinal);
			ClassicAssert.AreEqual(0uL, emptyRange.Count);
			ToyAdvanceResult emptyResult = default(ToyAdvanceResult);
			ClassicAssert.IsNull(emptyResult.State);
			ClassicAssert.AreEqual(default(OptionTransitionKind), emptyResult.OptionTransition);
			ClassicAssert.AreEqual(default(KernelFaultCode), emptyResult.Fault);
			ClassicAssert.IsFalse(emptyResult.Succeeded);
		}

		[Test]
		public void LogicalSourceKeepsOneOrderedPartialAuthority()
		{
			string source = LogicalSource();
			ClassicAssert.AreEqual(4, Count(source, "internal static partial class FixedPeriodToyRules"));
			ClassicAssert.AreEqual(1, Count(source, "internal readonly struct ToyPulseRange"));
			ClassicAssert.AreEqual(1, Count(source, "internal sealed class FixedPeriodToyState"));
			ClassicAssert.AreEqual(1, Count(source, "internal readonly struct ToyAdvanceResult"));
			ClassicAssert.Less(source.IndexOf("internal static ToyAdvanceResult Create", StringComparison.Ordinal),
				source.IndexOf("internal static ToyAdvanceResult ObserveOptionOnLoad", StringComparison.Ordinal));
			ClassicAssert.Less(source.IndexOf("internal static ToyAdvanceResult ObserveOptionOnLoad", StringComparison.Ordinal),
				source.IndexOf("internal static ToyAdvanceResult AdvanceThrough", StringComparison.Ordinal));
			ClassicAssert.Less(source.IndexOf("internal static ToyAdvanceResult AdvanceThrough", StringComparison.Ordinal),
				source.IndexOf("internal static bool TryGetEventKey", StringComparison.Ordinal));
			ClassicAssert.Less(source.IndexOf("internal static bool TryGetEventKey", StringComparison.Ordinal),
				source.IndexOf("internal static bool TryEncodeCanonical", StringComparison.Ordinal));
			ClassicAssert.Less(source.IndexOf("internal static bool TryEncodeCanonical", StringComparison.Ordinal),
				source.IndexOf("private static bool TryFold", StringComparison.Ordinal));
			ClassicAssert.Less(source.IndexOf("private static bool TryFold", StringComparison.Ordinal),
				source.IndexOf("private static bool IsCanonical", StringComparison.Ordinal));
		}

		/// <summary>
		/// The 183-byte fixture, hard-coded from the card. Created enabled at tick 0 with interval
		/// 10, then advanced unchanged through tick 25.
		/// </summary>
		private const string FixtureHex =
			"5441464b535430310000000100000003000102030405060708090a0b0c0d0e0f"
			+ "000000137461663a736574746c656d656e743a74657374000000187461663a73"
			+ "747265616d3a6b65726e656c2d746f793a7631ffff0001000000000000001901"
			+ "000000000000001e0000000000000002000000000000000a0200000000000000"
			+ "000100000003000000187461663a73747265616d3a6b65726e656c2d746f793a"
			+ "7631ffff0001000000000000000000000000000000027e";

		private const string ToyOrdinalZeroEventId =
			"taf:event:v1:c32737a586f1d42448355441fdaace7abe4bfb32b27f40b1e0537f860eba5f54";

		private const string Settlement = "taf:settlement:test";

		private static FixedPeriodToyState Fixture()
		{
			ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, 10L, true);
			ClassicAssert.IsTrue(created.Succeeded, "create");
			ToyAdvanceResult advanced = FixedPeriodToyRules.AdvanceThrough(created.State, 25L, true);
			ClassicAssert.IsTrue(advanced.Succeeded, "advance");
			return advanced.State;
		}

		/// <summary>
		/// The state-validity verdict as a caller can observe it. <c>IsCanonical</c> is private, so
		/// every invariant is asserted through a named entry point instead; advance validates the
		/// state before it looks at the tick, so the state fault is what surfaces.
		/// </summary>
		private static bool IsAccepted(FixedPeriodToyState state, out KernelFaultCode fault)
		{
			ToyAdvanceResult probe = FixedPeriodToyRules.AdvanceThrough(state, long.MaxValue / 2L, true);
			fault = probe.Fault;
			if (probe.Succeeded)
			{
				return true;
			}
			// Only a state verdict counts here; a clock fault means the state itself was accepted.
			return fault != KernelFaultCode.InvalidToyState && fault != KernelFaultCode.InvalidOptionLatch;
		}

		/// <summary>
		/// A distinct object with identical field values — what a load produces, as opposed to the
		/// same reference handed back. Every reload assertion is worthless without this.
		/// </summary>
		private static FixedPeriodToyState Clone(FixedPeriodToyState source)
		{
			return new FixedPeriodToyState(
				source.SchemaVersion,
				source.RulesVersion,
				source.SimulationSeed,
				source.SettlementId,
				source.ProcessedThroughTick,
				source.ClockScheduled,
				source.NextDueTick,
				source.NextOrdinal,
				source.IntervalTicks,
				new OptionLatchState(source.OptionLatch.Value, source.OptionLatch.ChangedAtTick),
				source.HasEmittedRange,
				source.HasEmittedRange
					? new ToyPulseRange(
						source.EmittedRange.RulesVersionAtCreation,
						source.EmittedRange.EventStreamId,
						source.EmittedRange.EventKindCode,
						source.EmittedRange.FirstOrdinal,
						source.EmittedRange.Count)
					: default(ToyPulseRange));
		}

		private static string Encode(FixedPeriodToyState state)
		{
			byte[] bytes;
			KernelFaultCode fault;
			ClassicAssert.IsTrue(FixedPeriodToyRules.TryEncodeCanonical(state, out bytes, out fault), "encode fault " + fault);
			return KernelDigest.ToLowercaseHex(bytes);
		}

		[Test]
		public void TheFixtureStateMatchesTheCardExactly()
		{
			FixedPeriodToyState state = Fixture();
			ClassicAssert.AreEqual(25L, state.ProcessedThroughTick);
			ClassicAssert.AreEqual(30L, state.NextDueTick);
			ClassicAssert.AreEqual(2uL, state.NextOrdinal);
			ClassicAssert.IsTrue(state.ClockScheduled);
			ClassicAssert.AreEqual(OptionLatchValue.Enabled, state.OptionLatch.Value);
			ClassicAssert.AreEqual(0L, state.OptionLatch.ChangedAtTick);
			ClassicAssert.IsTrue(state.HasEmittedRange);
			ClassicAssert.AreEqual(3, state.EmittedRange.RulesVersionAtCreation);
			ClassicAssert.AreEqual(FixedPeriodToyRules.ToyPulseEventStreamId, state.EmittedRange.EventStreamId);
			ClassicAssert.AreEqual(FixedPeriodToyRules.ToyPulseEventKind, state.EmittedRange.EventKindCode);
			ClassicAssert.AreEqual(0uL, state.EmittedRange.FirstOrdinal);
			ClassicAssert.AreEqual(2uL, state.EmittedRange.Count);
		}

		[Test]
		public void TheFixtureEncodesToTheHardCoded183Bytes()
		{
			byte[] bytes;
			KernelFaultCode fault;
			ClassicAssert.IsTrue(FixedPeriodToyRules.TryEncodeCanonical(Fixture(), out bytes, out fault));
			ClassicAssert.AreEqual(183, bytes.Length);
			ClassicAssert.AreEqual(FixtureHex, KernelDigest.ToLowercaseHex(bytes));
			ClassicAssert.AreEqual(0x7E, bytes[bytes.Length - 1], "terminal marker");
		}

		[Test]
		public void OrdinalZeroExpandsToTheHardCodedEventId()
		{
			SemanticEventKey key;
			KernelFaultCode fault;
			ClassicAssert.IsTrue(FixedPeriodToyRules.TryGetEventKey(Fixture(), 0uL, out key, out fault));
			string id;
			ClassicAssert.IsTrue(SemanticEventIdentity.TryCreateId(KernelCanonicalTests.GoldenSeed(), key, out id, out fault));
			ClassicAssert.AreEqual(ToyOrdinalZeroEventId, id);
		}

		[Test]
		public void OnlyOrdinalsInsideTheEmittedRangeHaveIdentity()
		{
			FixedPeriodToyState state = Fixture();
			SemanticEventKey key;
			KernelFaultCode fault;
			ClassicAssert.IsTrue(FixedPeriodToyRules.TryGetEventKey(state, 0uL, out key, out fault));
			ClassicAssert.IsTrue(FixedPeriodToyRules.TryGetEventKey(state, 1uL, out key, out fault));
			// Ordinal 2 is NextOrdinal: nothing has emitted it yet, so it has no identity.
			ClassicAssert.IsFalse(FixedPeriodToyRules.TryGetEventKey(state, 2uL, out key, out fault));
			ClassicAssert.AreEqual(KernelFaultCode.InvalidEventKey, fault);
			ClassicAssert.IsFalse(FixedPeriodToyRules.TryGetEventKey(state, ulong.MaxValue, out key, out fault));
		}

		[Test]
		public void CreateDisabledSchedulesNothing()
		{
			ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 5L, 10L, false);
			ClassicAssert.IsTrue(created.Succeeded);
			ClassicAssert.IsFalse(created.State.ClockScheduled);
			ClassicAssert.AreEqual(0L, created.State.NextDueTick);
			ClassicAssert.AreEqual(OptionTransitionKind.InitializedDisabled, created.OptionTransition);
			ClassicAssert.IsFalse(created.State.HasEmittedRange);
			ClassicAssert.AreEqual(0uL, created.State.NextOrdinal);
		}

		[TestCase(-1L, 10L, 1)]
		[TestCase(0L, 0L, 2)]
		[TestCase(0L, -5L, 2)]
		public void CreateFailsClosed(long now, long interval, int expectedCode)
		{
			KernelFaultCode expected = (KernelFaultCode)expectedCode;
			ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, now, interval, true);
			ClassicAssert.IsFalse(created.Succeeded);
			ClassicAssert.AreEqual(expected, created.Fault);
			ClassicAssert.IsNull(created.State);
		}

		[Test]
		public void CreateRejectsABadSettlementOrRulesVersion()
		{
			ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState,
				FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 0, Settlement, 0L, 10L, true).Fault);
			ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState,
				FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, "nope", 0L, 10L, true).Fault);
		}

		[Test]
		public void LoadObservationNeverEmitsAndNeverMovesProcessedThrough()
		{
			FixedPeriodToyState state = Fixture();
			ToyAdvanceResult unchanged = FixedPeriodToyRules.ObserveOptionOnLoad(state, 500L, true);
			ClassicAssert.IsTrue(unchanged.Succeeded);
			ClassicAssert.AreEqual(OptionTransitionKind.None, unchanged.OptionTransition);
			ClassicAssert.AreSame(state, unchanged.State, "an unchanged load is a no-op");

			ToyAdvanceResult disabled = FixedPeriodToyRules.ObserveOptionOnLoad(state, 500L, false);
			ClassicAssert.IsTrue(disabled.Succeeded);
			ClassicAssert.AreEqual(OptionTransitionKind.Disabled, disabled.OptionTransition);
			ClassicAssert.AreEqual(25L, disabled.State.ProcessedThroughTick, "load is an observation, not a simulation step");
			ClassicAssert.AreEqual(2uL, disabled.State.NextOrdinal, "no pulse is emitted on load");
			ClassicAssert.IsFalse(disabled.State.ClockScheduled);
			ClassicAssert.AreEqual(0L, disabled.State.NextDueTick);
		}

		[Test]
		public void LoadReanchorsRatherThanInferringAnOfflineBacklog()
		{
			// The old overdue schedule is discarded deliberately: a change seen across a stopped
			// process is not a backlog of activity that happened while nobody was playing.
			ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, 10L, false);
			ToyAdvanceResult resumed = FixedPeriodToyRules.ObserveOptionOnLoad(created.State, 1000L, true);
			ClassicAssert.IsTrue(resumed.Succeeded);
			ClassicAssert.AreEqual(OptionTransitionKind.Enabled, resumed.OptionTransition);
			ClassicAssert.AreEqual(1010L, resumed.State.NextDueTick, "one full interval from load, not replayed history");
			ClassicAssert.AreEqual(0uL, resumed.State.NextOrdinal);
			ClassicAssert.AreEqual(0L, resumed.State.ProcessedThroughTick);
		}

		[Test]
		public void RepeatedLoadObservationChangesNothing()
		{
			FixedPeriodToyState state = Fixture();
			string before = Encode(state);
			for (int i = 0; i < 100; i++)
			{
				ToyAdvanceResult step = FixedPeriodToyRules.ObserveOptionOnLoad(state, 100L + i, true);
				ClassicAssert.IsTrue(step.Succeeded);
				state = step.State;
			}
			ClassicAssert.AreEqual(before, Encode(state), "loading repeatedly must not advance, materialize, reroll, or notify");
		}

		[Test]
		public void UnchangedDisabledWakeEmitsNothingButStillProcessesThrough()
		{
			ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, 10L, false);
			ToyAdvanceResult advanced = FixedPeriodToyRules.AdvanceThrough(created.State, 999L, false);
			ClassicAssert.IsTrue(advanced.Succeeded);
			ClassicAssert.AreEqual(999L, advanced.State.ProcessedThroughTick);
			ClassicAssert.AreEqual(0uL, advanced.State.NextOrdinal);
			ClassicAssert.IsFalse(advanced.State.HasEmittedRange);
			ClassicAssert.AreEqual(OptionTransitionKind.None, advanced.OptionTransition);
		}

		[Test]
		public void ResumingSchedulesAFullIntervalAndNeverReplaysDisabledTime()
		{
			ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, 10L, false);
			ToyAdvanceResult resumed = FixedPeriodToyRules.AdvanceThrough(created.State, 100L, true);
			ClassicAssert.IsTrue(resumed.Succeeded);
			ClassicAssert.AreEqual(110L, resumed.State.NextDueTick);
			ClassicAssert.AreEqual(0uL, resumed.State.NextOrdinal, "the disabled century produced nothing");
			ClassicAssert.AreEqual(100L, resumed.State.ProcessedThroughTick);
		}

		[Test]
		public void DisablingFreezesTheAccumulatedRange()
		{
			FixedPeriodToyState state = Fixture();
			ToyAdvanceResult disabled = FixedPeriodToyRules.AdvanceThrough(state, 26L, false);
			ClassicAssert.IsTrue(disabled.Succeeded);
			ClassicAssert.AreEqual(2uL, disabled.State.NextOrdinal);
			ClassicAssert.IsTrue(disabled.State.HasEmittedRange, "history survives being switched off");
			ClassicAssert.AreEqual(2uL, disabled.State.EmittedRange.Count);
			ClassicAssert.IsFalse(disabled.State.ClockScheduled);
		}

		[Test]
		public void AdvanceFailsClosedOnRegressionAndLeavesTheSourceUntouched()
		{
			FixedPeriodToyState state = Fixture();
			string before = Encode(state);
			ToyAdvanceResult regressed = FixedPeriodToyRules.AdvanceThrough(state, 24L, true);
			ClassicAssert.IsFalse(regressed.Succeeded);
			ClassicAssert.AreEqual(KernelFaultCode.ClockRegression, regressed.Fault);
			ClassicAssert.AreSame(state, regressed.State, "the original reference comes back");
			ClassicAssert.AreEqual(before, Encode(state), "caller state byte-identical after a fault");

			ToyAdvanceResult negative = FixedPeriodToyRules.AdvanceThrough(state, -1L, true);
			ClassicAssert.IsFalse(negative.Succeeded);
			ClassicAssert.AreEqual(KernelFaultCode.InvalidTick, negative.Fault);
		}

		[Test]
		public void ANullOrMalformedStateIsRefused()
		{
			KernelFaultCode fault;
			ClassicAssert.IsFalse(IsAccepted(null, out fault));
			ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, fault);

			ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, FixedPeriodToyRules.AdvanceThrough(null, 0L, true).Fault);
			ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, FixedPeriodToyRules.ObserveOptionOnLoad(null, 0L, true).Fault);

			byte[] bytes;
			ClassicAssert.IsFalse(FixedPeriodToyRules.TryEncodeCanonical(null, out bytes, out fault));
			ClassicAssert.IsNull(bytes);
		}

		[Test]
		public void CanonicalInvariantsRejectContradictoryStates()
		{
			KernelFaultCode fault;
			KernelSeed128 seed = KernelCanonicalTests.GoldenSeed();
			OptionLatchState enabled = new OptionLatchState(OptionLatchValue.Enabled, 0L);
			OptionLatchState disabled = new OptionLatchState(OptionLatchValue.Disabled, 0L);

			// Enabled but unscheduled.
			ClassicAssert.IsFalse(IsAccepted(new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L, enabled, false, default(ToyPulseRange)), out fault));

			// Enabled with a deadline that is not strictly after processed-through.
			ClassicAssert.IsFalse(IsAccepted(new FixedPeriodToyState(
				1, 3, seed, Settlement, 10L, true, 10L, 0uL, 10L, enabled, false, default(ToyPulseRange)), out fault));

			// Disabled but carrying a schedule.
			ClassicAssert.IsFalse(IsAccepted(new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, true, 5L, 0uL, 10L, disabled, false, default(ToyPulseRange)), out fault));

			// An unobserved latch is never valid on a live toy.
			ClassicAssert.IsFalse(IsAccepted(new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L, new OptionLatchState(OptionLatchValue.Unobserved, 0L), false, default(ToyPulseRange)), out fault));
			ClassicAssert.AreEqual(KernelFaultCode.InvalidOptionLatch, fault);

			// Range present but its span disagrees with NextOrdinal.
			ClassicAssert.IsFalse(IsAccepted(new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, false, 0L, 5uL, 10L, disabled, true,
				new ToyPulseRange(3, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, 2uL)), out fault));

			// Absent range but a nonzero ordinal.
			ClassicAssert.IsFalse(IsAccepted(new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, false, 0L, 3uL, 10L, disabled, false, default(ToyPulseRange)), out fault));

			// Range whose stream or kind is not the reserved toy constant.
			ClassicAssert.IsFalse(IsAccepted(new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, false, 0L, 2uL, 10L, disabled, true,
				new ToyPulseRange(3, "taf:stream:other", FixedPeriodToyRules.ToyPulseEventKind, 0uL, 2uL)), out fault));
		}

		/// <summary>
		/// The folded advance against the naive one it replaces, over a domain small enough to
		/// enumerate: same first ordinal, same count, same next ordinal, same following deadline,
		/// and — the part that actually matters downstream — the same expanded event ID for every
		/// single pulse in the range.
		/// </summary>
		[Test]
		public void FoldedAdvanceEqualsOnePulseAtATimeReplayIncludingEveryExpandedEventId()
		{
			KernelFaultCode fault;
			int compared = 0;

			for (long interval = 1L; interval <= 6L; interval++)
			{
				for (long end = 0L; end <= 40L; end++)
				{
					ToyAdvanceResult created = FixedPeriodToyRules.Create(
						KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, interval, true);
					ClassicAssert.IsTrue(created.Succeeded);

					ToyAdvanceResult folded = FixedPeriodToyRules.AdvanceThrough(created.State, end, true);
					ClassicAssert.IsTrue(folded.Succeeded, "folded advance to " + end);

					// The replay: wake at literally every tick, so each pulse is processed alone.
					FixedPeriodToyState replayed = created.State;
					for (long t = 1L; t <= end; t++)
					{
						ToyAdvanceResult step = FixedPeriodToyRules.AdvanceThrough(replayed, t, true);
						ClassicAssert.IsTrue(step.Succeeded, "replay step " + t);
						replayed = step.State;
					}

					ClassicAssert.AreEqual(folded.State.NextOrdinal, replayed.NextOrdinal, "next ordinal, interval " + interval + ", end " + end);
					ClassicAssert.AreEqual(folded.State.NextDueTick, replayed.NextDueTick, "following due, interval " + interval + ", end " + end);
					ClassicAssert.AreEqual(folded.State.HasEmittedRange, replayed.HasEmittedRange);
					if (folded.State.HasEmittedRange)
					{
						ClassicAssert.AreEqual(folded.State.EmittedRange.FirstOrdinal, replayed.EmittedRange.FirstOrdinal, "first ordinal");
						ClassicAssert.AreEqual(folded.State.EmittedRange.Count, replayed.EmittedRange.Count, "count");
					}
					ClassicAssert.AreEqual(Encode(folded.State), Encode(replayed), "complete canonical bytes, interval " + interval + ", end " + end);

					// Every pulse expands to the same identity either way.
					for (ulong ordinal = 0uL; ordinal < folded.State.NextOrdinal; ordinal++)
					{
						SemanticEventKey foldedKey;
						SemanticEventKey replayedKey;
						ClassicAssert.IsTrue(FixedPeriodToyRules.TryGetEventKey(folded.State, ordinal, out foldedKey, out fault));
						ClassicAssert.IsTrue(FixedPeriodToyRules.TryGetEventKey(replayed, ordinal, out replayedKey, out fault));
						string foldedId;
						string replayedId;
						ClassicAssert.IsTrue(SemanticEventIdentity.TryCreateId(KernelCanonicalTests.GoldenSeed(), foldedKey, out foldedId, out fault));
						ClassicAssert.IsTrue(SemanticEventIdentity.TryCreateId(KernelCanonicalTests.GoldenSeed(), replayedKey, out replayedId, out fault));
						ClassicAssert.AreEqual(foldedId, replayedId, "event id for ordinal " + ordinal + ", interval " + interval + ", end " + end);
					}
					compared++;
				}
			}
			ClassicAssert.AreEqual(6 * 41, compared);
		}

		/// <summary>
		/// The save/reload shape, exercised where it is most dangerous: right on either side of a
		/// deadline. Cloning the canonical state and continuing must not duplicate an ordinal,
		/// redraw anything, or split the range.
		/// </summary>
		[Test]
		public void CloningAcrossADueBoundaryNeverDuplicatesRerollsOrSplits()
		{
			const long Interval = 10L;
			foreach (long boundary in new long[] { 9L, 10L, 11L, 19L, 20L, 21L })
			{
				ToyAdvanceResult created = FixedPeriodToyRules.Create(
					KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, Interval, true);
				ToyAdvanceResult upTo = FixedPeriodToyRules.AdvanceThrough(created.State, boundary, true);
				ClassicAssert.IsTrue(upTo.Succeeded, "advance to " + boundary);

				// The reload: a genuinely distinct object carrying the same field values, which is
				// what a load actually produces. Reusing the same reference here would have tested
				// nothing at all — every assertion below would pass on an object that was never
				// reconstructed.
				string saved = Encode(upTo.State);
				FixedPeriodToyState reloaded = Clone(upTo.State);
				ClassicAssert.IsFalse(ReferenceEquals(upTo.State, reloaded), "the reload fixture must be a distinct object");
				ClassicAssert.AreEqual(saved, Encode(reloaded), "a reconstructed state must be byte-identical at " + boundary);

				ToyAdvanceResult continued = FixedPeriodToyRules.AdvanceThrough(reloaded, boundary + 25L, true);
				ClassicAssert.IsTrue(continued.Succeeded);

				// The control: never saved at all.
				ToyAdvanceResult straight = FixedPeriodToyRules.AdvanceThrough(created.State, boundary + 25L, true);
				ClassicAssert.IsTrue(straight.Succeeded);

				ClassicAssert.AreEqual(Encode(straight.State), Encode(continued.State),
					"a reload at tick " + boundary + " changed the outcome");
				ClassicAssert.AreEqual(0uL, continued.State.EmittedRange.FirstOrdinal, "the range must stay one span, not split at " + boundary);
				ClassicAssert.AreEqual(continued.State.NextOrdinal, continued.State.EmittedRange.Count, "no ordinal duplicated or skipped");
			}
		}

		/// <summary>
		/// Two algorithms that look reasonable and are banned, with the exact damage each does.
		/// These exist so that a future rewrite that reaches for either one fails here and reads
		/// why, rather than shipping a settlement that quietly drifts.
		/// </summary>
		[Test]
		public void TheTwoBannedSchedulingAlgorithmsAreDemonstrablyWrong()
		{
			const long Interval = 10L;
			const long Now = 25L;

			ToyAdvanceResult created = FixedPeriodToyRules.Create(
				KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, Interval, true);
			ToyAdvanceResult actual = FixedPeriodToyRules.AdvanceThrough(created.State, Now, true);
			ClassicAssert.IsTrue(actual.Succeeded);
			ClassicAssert.AreEqual(2uL, actual.State.NextOrdinal, "deadlines at 10 and 20 have passed");
			ClassicAssert.AreEqual(30L, actual.State.NextDueTick);

			// Banned oracle one: reanchor from now. Loses the five ticks already served toward the
			// next deadline, so every wake quietly pushes the schedule further out and a settlement
			// observed often runs slower than one observed rarely.
			long reanchored = Now + Interval;
			ClassicAssert.AreEqual(35L, reanchored);
			ClassicAssert.AreNotEqual(actual.State.NextDueTick, reanchored,
				"reanchoring from now discards the partial period and makes the rate depend on observation");

			// Banned oracle two: loop with a cap, then reset. Discards whatever debt exceeded the
			// cap, so a long absence silently loses events rather than folding them.
			const ulong Cap = 1uL;
			ulong cappedCount = 0uL;
			long deadline = 0L;
			while (deadline <= Now && cappedCount < Cap)
			{
				cappedCount++;
				deadline += Interval;
			}
			ClassicAssert.AreEqual(1uL, cappedCount);
			ClassicAssert.AreNotEqual(actual.State.NextOrdinal, cappedCount,
				"a capped loop drops real semantic debt instead of folding it");
		}

		/// <summary>
		/// The jumps a real absence produces. Whatever the span, the canonical result must equal
		/// what fine-grained observation would have produced.
		/// </summary>
		[Test]
		public void LongAbsencesProduceTheSameRangeAsContinuousObservation()
		{
			const long Day = 1200L;
			foreach (long days in new long[] { 1L, 30L, 100L, 365L, 10000L })
			{
				long end = days * Day;
				ToyAdvanceResult created = FixedPeriodToyRules.Create(
					KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, Day, true);

				ToyAdvanceResult jumped = FixedPeriodToyRules.AdvanceThrough(created.State, end, true);
				ClassicAssert.IsTrue(jumped.Succeeded, days + "-day jump");
				ClassicAssert.AreEqual((ulong)days, jumped.State.NextOrdinal, days + "-day jump ordinal");

				// Fine-grained: one wake per day. Same answer, or absence means something different
				// from presence and the whole model is broken.
				FixedPeriodToyState stepped = created.State;
				for (long d = 1L; d <= days; d++)
				{
					ToyAdvanceResult step = FixedPeriodToyRules.AdvanceThrough(stepped, d * Day, true);
					ClassicAssert.IsTrue(step.Succeeded);
					stepped = step.State;
				}
				ClassicAssert.AreEqual(Encode(jumped.State), Encode(stepped), days + "-day jump diverged from daily observation");
			}
		}

		/// <summary>
		/// The rules version is part of an event's identity and is owned by the range that emitted
		/// it. A later epoch continues the same lane rather than restarting it, so a version bump
		/// can never license reusing an ordinal that has already been spent.
		/// </summary>
		[Test]
		public void RulesVersionOwnershipIsFixedAtEmissionAndNeverLicensesOrdinalReuse()
		{
			KernelFaultCode fault;

			SemanticEventKey underN;
			SemanticEventKey underNext;
			ClassicAssert.IsTrue(SemanticEventKey.TryCreate(3, Settlement, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, out underN, out fault));
			ClassicAssert.IsTrue(SemanticEventKey.TryCreate(4, Settlement, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, out underNext, out fault));

			string idN;
			string idNext;
			ClassicAssert.IsTrue(SemanticEventIdentity.TryCreateId(KernelCanonicalTests.GoldenSeed(), underN, out idN, out fault));
			ClassicAssert.IsTrue(SemanticEventIdentity.TryCreateId(KernelCanonicalTests.GoldenSeed(), underNext, out idNext, out fault));
			ClassicAssert.AreNotEqual(idN, idNext, "otherwise identical keys under versions N and N+1 must differ");

			// An already-emitted range keeps the version it was emitted under.
			ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, 10L, true);
			ToyAdvanceResult advanced = FixedPeriodToyRules.AdvanceThrough(created.State, 25L, true);
			ClassicAssert.IsTrue(advanced.Succeeded);
			ClassicAssert.AreEqual(3, advanced.State.EmittedRange.RulesVersionAtCreation, "the range owns its version");

			SemanticEventKey emitted;
			ClassicAssert.IsTrue(FixedPeriodToyRules.TryGetEventKey(advanced.State, 0uL, out emitted, out fault));
			ClassicAssert.AreEqual(3, emitted.RulesVersionAtCreation, "expanding an emitted ordinal must not adopt a newer version");

			// And the lane continues: the next ordinal is 2, not a reset to 0.
			ClassicAssert.AreEqual(2uL, advanced.State.NextOrdinal);
			ClassicAssert.AreEqual(FixedPeriodToyRules.ToyPulseEventStreamId, advanced.State.EmittedRange.EventStreamId);
			ClassicAssert.AreEqual(FixedPeriodToyRules.ToyPulseEventKind, advanced.State.EmittedRange.EventKindCode);
		}

		/// <summary>
		/// The two invariants the earlier set does not reach, plus the containment guarantee the
		/// card requires: because the encoder must refuse an invalid source, the proof that nothing
		/// was mutated cannot itself be an encoding. Capture the raw fields, assert the caller gets
		/// back the very same object, and compare field by field.
		/// </summary>
		[Test]
		public void MoreInvariantsAndAnInvalidSourceIsHandedBackUntouched()
		{
			KernelFaultCode fault;
			KernelSeed128 seed = KernelCanonicalTests.GoldenSeed();

			// A deadline that is not strictly after the tick the latch last changed. If this were
			// allowed, a settings change and a pulse could occupy the same instant with no rule for
			// which happened first, and the partition property would stop holding.
			FixedPeriodToyState deadlineAtLatchChange = new FixedPeriodToyState(
				1, 3, seed, Settlement, 5L, true, 20L, 0uL, 10L,
				new OptionLatchState(OptionLatchValue.Enabled, 20L), false, default(ToyPulseRange));
			ClassicAssert.IsFalse(IsAccepted(deadlineAtLatchChange, out fault),
				"a deadline may not coincide with a later latch change");

			// A present range claiming zero pulses. An emitted range with nothing in it is not a
			// smaller range, it is a contradiction: the flag says something happened.
			FixedPeriodToyState zeroCountRange = new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L,
				new OptionLatchState(OptionLatchValue.Disabled, 0L), true,
				new ToyPulseRange(3, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, 0uL));
			ClassicAssert.IsFalse(IsAccepted(zeroCountRange, out fault), "an emitted range of zero pulses is a contradiction");

			// A range whose span wraps past the top of the ordinal space.
			FixedPeriodToyState wrappingRange = new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L,
				new OptionLatchState(OptionLatchValue.Disabled, 0L), true,
				new ToyPulseRange(3, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, ulong.MaxValue, 2uL));
			ClassicAssert.IsFalse(IsAccepted(wrappingRange, out fault), "a range may not wrap the ordinal space");

			// Containment: every raw field of an invalid source survives an attempted advance, and
			// the caller is handed back the identical object rather than a repaired copy.
			FixedPeriodToyState source = zeroCountRange;
			int schemaVersion = source.SchemaVersion;
			int rulesVersion = source.RulesVersion;
			long processed = source.ProcessedThroughTick;
			bool scheduled = source.ClockScheduled;
			long nextDue = source.NextDueTick;
			ulong nextOrdinal = source.NextOrdinal;
			long interval = source.IntervalTicks;
			OptionLatchValue latchValue = source.OptionLatch.Value;
			long latchTick = source.OptionLatch.ChangedAtTick;
			bool hasRange = source.HasEmittedRange;
			ulong firstOrdinal = source.EmittedRange.FirstOrdinal;
			ulong count = source.EmittedRange.Count;

			ToyAdvanceResult refused = FixedPeriodToyRules.AdvanceThrough(source, 500L, true);
			ClassicAssert.IsFalse(refused.Succeeded, "an invalid source must not advance");
			ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, refused.Fault);
			ClassicAssert.IsTrue(ReferenceEquals(source, refused.State), "the caller must get its own object back, not a repaired one");

			ClassicAssert.AreEqual(schemaVersion, source.SchemaVersion);
			ClassicAssert.AreEqual(rulesVersion, source.RulesVersion);
			ClassicAssert.AreEqual(processed, source.ProcessedThroughTick);
			ClassicAssert.AreEqual(scheduled, source.ClockScheduled);
			ClassicAssert.AreEqual(nextDue, source.NextDueTick);
			ClassicAssert.AreEqual(nextOrdinal, source.NextOrdinal);
			ClassicAssert.AreEqual(interval, source.IntervalTicks);
			ClassicAssert.AreEqual(latchValue, source.OptionLatch.Value);
			ClassicAssert.AreEqual(latchTick, source.OptionLatch.ChangedAtTick);
			ClassicAssert.AreEqual(hasRange, source.HasEmittedRange);
			ClassicAssert.AreEqual(firstOrdinal, source.EmittedRange.FirstOrdinal);
			ClassicAssert.AreEqual(count, source.EmittedRange.Count);
		}

		/// <summary>
		/// When more than one thing is wrong at once, which fault comes back is part of the API,
		/// not an implementation detail: a caller that branches on the code needs the answer to be
		/// the same next release. Every case here is invalid in at least two ways.
		/// </summary>
		[Test]
		public void CombinedInvalidInputsResolveToTheFrozenFaultPrecedence()
		{
			KernelFaultCode fault;
			KernelSeed128 seed = KernelCanonicalTests.GoldenSeed();

			// Create: a bad tick and a bad interval together. The tick is checked first.
			ToyAdvanceResult bothBad = FixedPeriodToyRules.Create(seed, 3, Settlement, -1L, -1L, true);
			ClassicAssert.IsFalse(bothBad.Succeeded);
			ClassicAssert.AreEqual(KernelFaultCode.InvalidTick, bothBad.Fault, "tick before interval");
			ClassicAssert.IsNull(bothBad.State, "nothing partial is published");

			// Create: a bad interval and a bad settlement identifier together. Create resolves this
			// as interval-before-identity.
			//
			// Note the asymmetry with AdvanceThrough below, which resolves state before arithmetic.
			// It is defensible — Create has no prior state to sanity-check, only arguments that are
			// about to become state — but it is a real difference in two neighbouring APIs, and a
			// caller that branches on the code will meet it. Pinned here as observed behaviour and
			// flagged for review rather than quietly matched.
			ToyAdvanceResult badIdAndInterval = FixedPeriodToyRules.Create(seed, 3, "NOPE", 0L, 0L, true);
			ClassicAssert.IsFalse(badIdAndInterval.Succeeded);
			ClassicAssert.AreEqual(KernelFaultCode.InvalidInterval, badIdAndInterval.Fault, "Create resolves interval before identity");

			// With a valid interval, the identity fault does surface.
			ToyAdvanceResult badIdOnly = FixedPeriodToyRules.Create(seed, 3, "NOPE", 0L, 10L, true);
			ClassicAssert.IsFalse(badIdOnly.Succeeded);
			ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, badIdOnly.Fault);
			ClassicAssert.IsNull(badIdOnly.State);

			// Advance: an invalid source and a regressed clock. The source is checked first,
			// because a regression judged against nonsense is not a meaningful answer.
			FixedPeriodToyState invalid = new FixedPeriodToyState(
				1, 3, seed, Settlement, 100L, true, 5L, 0uL, 10L,
				new OptionLatchState(OptionLatchValue.Enabled, 0L), false, default(ToyPulseRange));
			ClassicAssert.IsFalse(IsAccepted(invalid, out fault));
			ToyAdvanceResult invalidAndRegressed = FixedPeriodToyRules.AdvanceThrough(invalid, 1L, true);
			ClassicAssert.IsFalse(invalidAndRegressed.Succeeded);
			ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, invalidAndRegressed.Fault, "source state before regression");
			ClassicAssert.IsTrue(ReferenceEquals(invalid, invalidAndRegressed.State));

			// Advance: a valid source with both a negative tick and a regression. Negative wins.
			FixedPeriodToyState valid = Fixture();
			ToyAdvanceResult negativeAndRegressed = FixedPeriodToyRules.AdvanceThrough(valid, -5L, true);
			ClassicAssert.IsFalse(negativeAndRegressed.Succeeded);
			ClassicAssert.AreEqual(KernelFaultCode.InvalidTick, negativeAndRegressed.Fault, "negative tick before regression");
			ClassicAssert.IsTrue(ReferenceEquals(valid, negativeAndRegressed.State));

			// Load observation: an invalid latch on the source plus a regressed tick.
			FixedPeriodToyState unobservedLatch = new FixedPeriodToyState(
				1, 3, seed, Settlement, 50L, false, 0L, 0uL, 10L,
				new OptionLatchState(OptionLatchValue.Unobserved, 0L), false, default(ToyPulseRange));
			ToyAdvanceResult loadRefused = FixedPeriodToyRules.ObserveOptionOnLoad(unobservedLatch, 1L, true);
			ClassicAssert.IsFalse(loadRefused.Succeeded);
			ClassicAssert.AreEqual(KernelFaultCode.InvalidOptionLatch, loadRefused.Fault, "latch validity before regression");
			ClassicAssert.IsTrue(ReferenceEquals(unobservedLatch, loadRefused.State));

			// Event key expansion: an invalid source and an out-of-range ordinal.
			SemanticEventKey key;
			ClassicAssert.IsFalse(FixedPeriodToyRules.TryGetEventKey(invalid, ulong.MaxValue, out key, out fault));
			ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, fault, "source state before ordinal range");

			// Encoding: an invalid source publishes no bytes at all.
			byte[] bytes;
			ClassicAssert.IsFalse(FixedPeriodToyRules.TryEncodeCanonical(invalid, out bytes, out fault));
			ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, fault);
			ClassicAssert.IsNull(bytes, "a refused encode must not hand back a partial buffer");
		}

		/// <summary>
		/// Every place the toy can overflow, each reached independently, each asserting the same
		/// two things: the caller gets its own object back by reference, and that object's
		/// canonical bytes are unchanged. Bytes rather than fields, because a partial write that
		/// happened to restore the fields I chose to check would pass a field comparison.
		/// </summary>
		[Test]
		public void EveryArithmeticOverflowLeavesTheSourceIdenticalByReferenceAndByBytes()
		{
			KernelSeed128 seed = KernelCanonicalTests.GoldenSeed();
			OptionLatchState enabled = new OptionLatchState(OptionLatchValue.Enabled, 0L);

			// Schedule: resuming from a load would have to add an interval past the end of time.
			FixedPeriodToyState scheduleEdge = new FixedPeriodToyState(
				1, 3, seed, Settlement, long.MaxValue, false, 0L, 0uL, long.MaxValue,
				new OptionLatchState(OptionLatchValue.Disabled, 0L), false, default(ToyPulseRange));
			CheckOverflow(scheduleEdge, "schedule", delegate
			{
				return FixedPeriodToyRules.ObserveOptionOnLoad(scheduleEdge, long.MaxValue, true);
			});

			// Following deadline: the pulse at long.MaxValue fires, but the next one cannot exist.
			FixedPeriodToyState deadlineEdge = new FixedPeriodToyState(
				1, 3, seed, Settlement, long.MaxValue - 1L, true, long.MaxValue, 0uL, long.MaxValue,
				enabled, false, default(ToyPulseRange));
			CheckOverflow(deadlineEdge, "following deadline", delegate
			{
				return FixedPeriodToyRules.AdvanceThrough(deadlineEdge, long.MaxValue, true);
			});

			// Ordinal and range span, which are one site and not two.
			//
			// I first wrote these as separate fixtures and claimed four independent overflow
			// sites. They are not independent, and cannot be made so. A canonical state with a
			// range satisfies NextOrdinal == FirstOrdinal + Count, and FirstOrdinal is unsigned,
			// so NextOrdinal >= Count always. Advancing adds the same due count to both, so
			// NextOrdinal + due >= Count + due: the ordinal reaches the top first, or they reach it
			// together. The count can never overflow while the ordinal still fits.
			//
			// So this is one invariant with one reachable guard, tested once and labelled honestly.
			// The count guard in production is not therefore dead: it protects the same arithmetic
			// against a non-canonical state that reached it another way, which is exactly the
			// defence-in-depth a fail-closed kernel wants. It simply cannot be the *first* thing to
			// fire from a valid source, and a test that claimed otherwise was claiming a
			// distinction the type system already forbids.
			FixedPeriodToyState ordinalEdge = new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, true, 1L, ulong.MaxValue, 1L, enabled, true,
				new ToyPulseRange(3, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, ulong.MaxValue));
			CheckOverflow(ordinalEdge, "ordinal and range span", delegate
			{
				return FixedPeriodToyRules.AdvanceThrough(ordinalEdge, 5L, true);
			});

			// The inequality the paragraph above rests on, asserted rather than asserted-by-prose,
			// across the whole reachable shape of a range.
			foreach (ulong first in new ulong[] { 0uL, 1uL, 2uL, 1000uL, ulong.MaxValue / 2uL })
			{
				foreach (ulong count in new ulong[] { 1uL, 2uL, 1000uL, ulong.MaxValue / 2uL })
				{
					if (first > ulong.MaxValue - count)
					{
						continue;
					}
					ulong nextOrdinal = first + count;
					ClassicAssert.IsTrue(nextOrdinal >= count,
						"NextOrdinal must dominate Count, else the two guards could fire independently: first "
							+ first + ", count " + count);
				}
			}
		}

		private static void CheckOverflow(FixedPeriodToyState source, string label, Func<ToyAdvanceResult> act)
		{
			KernelFaultCode canonicalFault;
			bool sourceWasValid = IsAccepted(source, out canonicalFault);
			string before = sourceWasValid ? Encode(source) : null;

			ToyAdvanceResult result = act();
			ClassicAssert.IsFalse(result.Succeeded, label + " must fail closed");
			ClassicAssert.IsTrue(ReferenceEquals(source, result.State), label + ": the caller keeps its own object");

			if (sourceWasValid)
			{
				// The exact code, not merely a failure: an overflow reported as a bad state would
				// send a reader looking for corruption that is not there.
				ClassicAssert.AreEqual(KernelFaultCode.ArithmeticOverflow, result.Fault, label + ": must report overflow exactly");
				ClassicAssert.AreEqual(OptionTransitionKind.None, result.OptionTransition, label + ": a refusal transitions nothing");
				ClassicAssert.AreEqual(before, Encode(source), label + ": the source bytes must be untouched");
				ClassicAssert.AreEqual(before, Encode(result.State), label + ": the returned state is the untouched source");
			}
			else
			{
				// A source the encoder itself refuses cannot be compared by bytes, so the
				// reference identity above is the whole guarantee, and the fault must say so.
				ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, result.Fault, label + ": invalid source reports as such");
			}
		}

		/// <summary>
		/// The toggle matrix at a real deadline: for every combination of wake tick relative to the
		/// deadline and option value on either side of it, the outcome must match what a wake at
		/// every single tick would have produced. This is the boundary where a transition and a
		/// pulse compete for the same instant.
		/// </summary>
		[Test]
		public void TheFullToggleMatrixAroundADeadlineAgreesWithTickByTickObservation()
		{
			const long Interval = 10L;
			int combinations = 0;

			foreach (bool startEnabled in new bool[] { false, true })
			{
				foreach (long wake in new long[] { Interval - 1L, Interval, Interval + 1L })
				{
					foreach (bool thenEnabled in new bool[] { false, true })
					{
						ToyAdvanceResult created = FixedPeriodToyRules.Create(
							KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, Interval, startEnabled);
						ClassicAssert.IsTrue(created.Succeeded);

						ToyAdvanceResult direct = FixedPeriodToyRules.AdvanceThrough(created.State, wake, thenEnabled);
						ClassicAssert.IsTrue(direct.Succeeded, "direct wake at " + wake);

						// The same input history, observed at every tick instead of once.
						FixedPeriodToyState walked = created.State;
						for (long t = 1L; t <= wake; t++)
						{
							// The option takes its new value at the wake tick and not before,
							// which is the whole point of the boundary.
							bool valueNow = t < wake ? startEnabled : thenEnabled;
							ToyAdvanceResult step = FixedPeriodToyRules.AdvanceThrough(walked, t, valueNow);
							ClassicAssert.IsTrue(step.Succeeded, "tick " + t);
							walked = step.State;
						}

						ClassicAssert.AreEqual(Encode(direct.State), Encode(walked),
							"start " + startEnabled + ", wake " + wake + ", then " + thenEnabled);
						combinations++;
					}
				}
			}
			ClassicAssert.AreEqual(2 * 3 * 2, combinations);
		}

		/// <summary>
		/// A range that claims a different lane or a different rules version than the state it sits
		/// in is not a smaller truth, it is two truths. Both must be refused, and refusing must not
		/// disturb the object that carried them.
		/// </summary>
		[Test]
		public void ARangeDisagreeingWithItsStateIsRefusedWithoutTouchingIt()
		{
			KernelSeed128 seed = KernelCanonicalTests.GoldenSeed();
			OptionLatchState disabled = new OptionLatchState(OptionLatchValue.Disabled, 0L);
			KernelFaultCode fault;

			FixedPeriodToyState wrongKind = new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, false, 0L, 2uL, 10L, disabled, true,
				new ToyPulseRange(3, FixedPeriodToyRules.ToyPulseEventStreamId, 0x1234u, 0uL, 2uL));
			ClassicAssert.IsFalse(IsAccepted(wrongKind, out fault), "a foreign event kind must be refused");
			ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, fault);

			FixedPeriodToyState wrongRules = new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, false, 0L, 2uL, 10L, disabled, true,
				new ToyPulseRange(4, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, 2uL));
			ClassicAssert.IsFalse(IsAccepted(wrongRules, out fault), "a range under another rules version must be refused");
			ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, fault);

			// Malformed states survive being refused, field for field, with the same reference back.
			foreach (FixedPeriodToyState bad in new FixedPeriodToyState[] { wrongKind, wrongRules })
			{
				int rules = bad.RulesVersion;
				ulong ordinal = bad.NextOrdinal;
				uint kind = bad.EmittedRange.EventKindCode;
				int rangeRules = bad.EmittedRange.RulesVersionAtCreation;
				ulong count = bad.EmittedRange.Count;

				ToyAdvanceResult refused = FixedPeriodToyRules.AdvanceThrough(bad, 500L, true);
				ClassicAssert.IsFalse(refused.Succeeded);
				ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, refused.Fault);
				ClassicAssert.IsTrue(ReferenceEquals(bad, refused.State));

				ClassicAssert.AreEqual(rules, bad.RulesVersion);
				ClassicAssert.AreEqual(ordinal, bad.NextOrdinal);
				ClassicAssert.AreEqual(kind, bad.EmittedRange.EventKindCode);
				ClassicAssert.AreEqual(rangeRules, bad.EmittedRange.RulesVersionAtCreation);
				ClassicAssert.AreEqual(count, bad.EmittedRange.Count);

				byte[] bytes;
				ClassicAssert.IsFalse(FixedPeriodToyRules.TryEncodeCanonical(bad, out bytes, out fault));
				ClassicAssert.IsNull(bytes, "a refused encode publishes no buffer");
			}
		}

		/// <summary>
		/// Every structural invariant violated on its own, so each guard is shown to fire for its
		/// own reason rather than being masked by a neighbouring one. For every refusal: the exact
		/// fault, the caller's own object back by reference, every raw field unchanged, and — where
		/// the fixture is encodable at all — identical canonical bytes.
		/// </summary>
		[Test]
		public void EveryInvariantIsViolatedIndependentlyAndRefusedWithoutMutation()
		{
			KernelSeed128 seed = KernelCanonicalTests.GoldenSeed();
			OptionLatchState enabled = new OptionLatchState(OptionLatchValue.Enabled, 0L);
			OptionLatchState disabled = new OptionLatchState(OptionLatchValue.Disabled, 0L);
			ToyPulseRange goodRange = new ToyPulseRange(
				3, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, 2uL);

			// name, fixture, expected fault
			object[][] cases =
			{
				new object[] { "schema version zero", new FixedPeriodToyState(
					0, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L, disabled, false, default(ToyPulseRange)),
					KernelFaultCode.InvalidToyState },

				new object[] { "state rules version zero", new FixedPeriodToyState(
					1, 0, seed, Settlement, 0L, false, 0L, 0uL, 10L, disabled, false, default(ToyPulseRange)),
					KernelFaultCode.InvalidToyState },

				new object[] { "settlement id off grammar", new FixedPeriodToyState(
					1, 3, seed, "NOPE", 0L, false, 0L, 0uL, 10L, disabled, false, default(ToyPulseRange)),
					KernelFaultCode.InvalidToyState },

				new object[] { "null settlement id", new FixedPeriodToyState(
					1, 3, seed, null, 0L, false, 0L, 0uL, 10L, disabled, false, default(ToyPulseRange)),
					KernelFaultCode.InvalidToyState },

				new object[] { "negative processed tick", new FixedPeriodToyState(
					1, 3, seed, Settlement, -1L, false, 0L, 0uL, 10L, disabled, false, default(ToyPulseRange)),
					KernelFaultCode.InvalidToyState },

				new object[] { "negative deadline while scheduled", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, true, -1L, 0uL, 10L, enabled, false, default(ToyPulseRange)),
					KernelFaultCode.InvalidToyState },

				new object[] { "zero interval", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 0L, 0uL, 0L, disabled, false, default(ToyPulseRange)),
					KernelFaultCode.InvalidToyState },

				new object[] { "negative interval", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 0L, 0uL, -5L, disabled, false, default(ToyPulseRange)),
					KernelFaultCode.InvalidToyState },

				new object[] { "latch tick negative", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L,
					new OptionLatchState(OptionLatchValue.Disabled, -1L), false, default(ToyPulseRange)),
					KernelFaultCode.InvalidOptionLatch },

				new object[] { "latch byte unknown", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L,
					new OptionLatchState((OptionLatchValue)200, 0L), false, default(ToyPulseRange)),
					KernelFaultCode.InvalidOptionLatch },

				new object[] { "latch unobserved on a live toy", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L,
					new OptionLatchState(OptionLatchValue.Unobserved, 0L), false, default(ToyPulseRange)),
					KernelFaultCode.InvalidOptionLatch },

				new object[] { "disabled and unscheduled but carrying a deadline", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 7L, 0uL, 10L, disabled, false, default(ToyPulseRange)),
					KernelFaultCode.InvalidToyState },

				new object[] { "enabled but unscheduled", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L, enabled, false, default(ToyPulseRange)),
					KernelFaultCode.InvalidToyState },

				new object[] { "deadline not after processed", new FixedPeriodToyState(
					1, 3, seed, Settlement, 10L, true, 10L, 0uL, 10L, enabled, false, default(ToyPulseRange)),
					KernelFaultCode.InvalidToyState },

				new object[] { "nonzero first ordinal", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 0L, 7uL, 10L, disabled, true,
					new ToyPulseRange(3, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 5uL, 2uL)),
					KernelFaultCode.InvalidToyState },

				new object[] { "range kind foreign", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 0L, 2uL, 10L, disabled, true,
					new ToyPulseRange(3, FixedPeriodToyRules.ToyPulseEventStreamId, 0x1234u, 0uL, 2uL)),
					KernelFaultCode.InvalidToyState },

				new object[] { "range rules version differs from state", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 0L, 2uL, 10L, disabled, true,
					new ToyPulseRange(4, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, 2uL)),
					KernelFaultCode.InvalidToyState },

				new object[] { "range stream foreign", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 0L, 2uL, 10L, disabled, true,
					new ToyPulseRange(3, "taf:stream:somewhere-else", FixedPeriodToyRules.ToyPulseEventKind, 0uL, 2uL)),
					KernelFaultCode.InvalidToyState },

				new object[] { "range count zero", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L, disabled, true,
					new ToyPulseRange(3, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, 0uL)),
					KernelFaultCode.InvalidToyState },

				new object[] { "range span disagrees with next ordinal", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 0L, 5uL, 10L, disabled, true, goodRange),
					KernelFaultCode.InvalidToyState },

				new object[] { "absent range with a nonzero ordinal", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 0L, 3uL, 10L, disabled, false, default(ToyPulseRange)),
					KernelFaultCode.InvalidToyState }
			};

			foreach (object[] entry in cases)
			{
				string label = (string)entry[0];
				FixedPeriodToyState bad = (FixedPeriodToyState)entry[1];
				KernelFaultCode expected = (KernelFaultCode)entry[2];

				KernelFaultCode fault;
				ClassicAssert.IsFalse(IsAccepted(bad, out fault), label + " must be refused");
				ClassicAssert.AreEqual(expected, fault, label + ": exact fault");

				AssertRefusedWithoutMutation(bad, label);
			}

			// A fixture with an absent range must reject each raw range field being set on its own,
			// so "absent" cannot mean "absent except for the parts nobody looked at".
			ToyPulseRange[] nonDefaultFields =
			{
				new ToyPulseRange(3, null, 0u, 0uL, 0uL),
				new ToyPulseRange(0, FixedPeriodToyRules.ToyPulseEventStreamId, 0u, 0uL, 0uL),
				new ToyPulseRange(0, null, FixedPeriodToyRules.ToyPulseEventKind, 0uL, 0uL),
				new ToyPulseRange(0, null, 0u, 1uL, 0uL),
				new ToyPulseRange(0, null, 0u, 0uL, 1uL)
			};
			for (int i = 0; i < nonDefaultFields.Length; i++)
			{
				FixedPeriodToyState absentButDirty = new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L, disabled, false, nonDefaultFields[i]);
				KernelFaultCode fault;
				ClassicAssert.IsFalse(IsAccepted(absentButDirty, out fault),
					"an absent range carrying raw field " + i + " must be refused");
				ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, fault, "raw field " + i);
				AssertRefusedWithoutMutation(absentButDirty, "absent range, raw field " + i);
			}
		}

		/// <summary>
		/// A bad latch combined with each non-latch invariant that outranks it. The frozen order
		/// puts any bad non-latch state ahead of a bad embedded latch, so every one of these must
		/// report <c>InvalidToyState</c> — the latch is also wrong, and that is not the answer.
		/// <para>
		/// Every fixture here passed before the reorder by reporting the latch instead, which is
		/// why the earlier matrix missed it: each of its cases was wrong in exactly one way.
		/// </para>
		/// </summary>
		[Test]
		public void ABadLatchNeverOutranksABadNonLatchInvariant()
		{
			KernelSeed128 seed = KernelCanonicalTests.GoldenSeed();

			// Three ways for the latch to be wrong, crossed with every non-latch rule that is
			// evaluable without it.
			OptionLatchState[] badLatches =
			{
				new OptionLatchState((OptionLatchValue)200, 0L),
				new OptionLatchState(OptionLatchValue.Unobserved, 0L),
				new OptionLatchState(OptionLatchValue.Disabled, -1L)
			};

			for (int i = 0; i < badLatches.Length; i++)
			{
				OptionLatchState latch = badLatches[i];
				string who = "bad latch " + i;

				object[][] combined =
				{
					new object[] { who + " + schema version", new FixedPeriodToyState(
						0, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L, latch, false, default(ToyPulseRange)) },

					new object[] { who + " + state rules version", new FixedPeriodToyState(
						1, 0, seed, Settlement, 0L, false, 0L, 0uL, 10L, latch, false, default(ToyPulseRange)) },

					new object[] { who + " + settlement id", new FixedPeriodToyState(
						1, 3, seed, "NOPE", 0L, false, 0L, 0uL, 10L, latch, false, default(ToyPulseRange)) },

					new object[] { who + " + negative processed tick", new FixedPeriodToyState(
						1, 3, seed, Settlement, -1L, false, 0L, 0uL, 10L, latch, false, default(ToyPulseRange)) },

					new object[] { who + " + negative deadline", new FixedPeriodToyState(
						1, 3, seed, Settlement, 0L, false, -1L, 0uL, 10L, latch, false, default(ToyPulseRange)) },

					new object[] { who + " + nonpositive interval", new FixedPeriodToyState(
						1, 3, seed, Settlement, 0L, false, 0L, 0uL, 0L, latch, false, default(ToyPulseRange)) },

					new object[] { who + " + absent range with a nonzero ordinal", new FixedPeriodToyState(
						1, 3, seed, Settlement, 0L, false, 0L, 3uL, 10L, latch, false, default(ToyPulseRange)) },

					new object[] { who + " + range count zero", new FixedPeriodToyState(
						1, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L, latch, true,
						new ToyPulseRange(3, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, 0uL)) },

					new object[] { who + " + range kind foreign", new FixedPeriodToyState(
						1, 3, seed, Settlement, 0L, false, 0L, 2uL, 10L, latch, true,
						new ToyPulseRange(3, FixedPeriodToyRules.ToyPulseEventStreamId, 0x1234u, 0uL, 2uL)) },

					new object[] { who + " + range rules version", new FixedPeriodToyState(
						1, 3, seed, Settlement, 0L, false, 0L, 2uL, 10L, latch, true,
						new ToyPulseRange(4, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, 2uL)) },

					new object[] { who + " + range stream foreign", new FixedPeriodToyState(
						1, 3, seed, Settlement, 0L, false, 0L, 2uL, 10L, latch, true,
						new ToyPulseRange(3, "taf:stream:somewhere-else", FixedPeriodToyRules.ToyPulseEventKind, 0uL, 2uL)) },

					new object[] { who + " + nonzero first ordinal", new FixedPeriodToyState(
						1, 3, seed, Settlement, 0L, false, 0L, 7uL, 10L, latch, true,
						new ToyPulseRange(3, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 5uL, 2uL)) },

					new object[] { who + " + range span disagrees", new FixedPeriodToyState(
						1, 3, seed, Settlement, 0L, false, 0L, 5uL, 10L, latch, true,
						new ToyPulseRange(3, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, 2uL)) }
				};

				foreach (object[] entry in combined)
				{
					string label = (string)entry[0];
					FixedPeriodToyState bad = (FixedPeriodToyState)entry[1];

					// Every named entry point must agree on which fault wins.
					ToyAdvanceResult advanced = FixedPeriodToyRules.AdvanceThrough(bad, 1000L, true);
					ClassicAssert.IsFalse(advanced.Succeeded, label);
					ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, advanced.Fault, label + ": advance");

					ToyAdvanceResult observed = FixedPeriodToyRules.ObserveOptionOnLoad(bad, 1000L, false);
					ClassicAssert.IsFalse(observed.Succeeded, label);
					ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, observed.Fault, label + ": load");

					SemanticEventKey key = KernelCanonicalTests.GoldenKey();
					KernelFaultCode fault;
					ClassicAssert.IsFalse(FixedPeriodToyRules.TryGetEventKey(bad, 0uL, out key, out fault), label);
					ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, fault, label + ": key expansion");
					ClassicAssert.AreEqual(default(SemanticEventKey), key, label + ": default key published");

					byte[] bytes = new byte[] { 9 };
					ClassicAssert.IsFalse(FixedPeriodToyRules.TryEncodeCanonical(bad, out bytes, out fault), label);
					ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, fault, label + ": encode");
					ClassicAssert.IsNull(bytes, label + ": no buffer published");

					AssertRefusedWithoutMutation(bad, label);
				}
			}

			// The schedule rule is the one invariant the latch selects, and it splits.
			//
			// A known value with a malformed change tick still selects a rule, and most of that
			// rule reads only the value. So these must report the state, not the latch. I first
			// wrote this pair off as an unavoidable exception; it is not, and the distinction is
			// between a latch that cannot be read and a latch that can be read but is wrong.
			OptionLatchState enabledBadTick = new OptionLatchState(OptionLatchValue.Enabled, -1L);
			OptionLatchState disabledBadTick = new OptionLatchState(OptionLatchValue.Disabled, -1L);

			object[][] knownValueButBadTick =
			{
				// Enabled says there must be a schedule; there is none.
				new object[] { "enabled with a bad tick and no schedule", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L, enabledBadTick, false, default(ToyPulseRange)) },
				// Enabled says the deadline must be after the processed tick; it is not.
				new object[] { "enabled with a bad tick and a stale deadline", new FixedPeriodToyState(
					1, 3, seed, Settlement, 10L, true, 10L, 0uL, 10L, enabledBadTick, false, default(ToyPulseRange)) },
				// Disabled says there must be no schedule; there is one.
				new object[] { "disabled with a bad tick and a schedule", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, true, 5L, 0uL, 10L, disabledBadTick, false, default(ToyPulseRange)) },
				// Disabled says the deadline must be zero; it is not.
				new object[] { "disabled with a bad tick and a deadline", new FixedPeriodToyState(
					1, 3, seed, Settlement, 0L, false, 7L, 0uL, 10L, disabledBadTick, false, default(ToyPulseRange)) }
			};

			foreach (object[] entry in knownValueButBadTick)
			{
				string label = (string)entry[0];
				FixedPeriodToyState bad = (FixedPeriodToyState)entry[1];

				ToyAdvanceResult advanced = FixedPeriodToyRules.AdvanceThrough(bad, 1000L, true);
				ClassicAssert.IsFalse(advanced.Succeeded, label);
				ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, advanced.Fault,
					label + ": a readable latch value still selects a schedule rule, and the state loses first");

				ToyAdvanceResult observed = FixedPeriodToyRules.ObserveOptionOnLoad(bad, 1000L, false);
				ClassicAssert.IsFalse(observed.Succeeded, label);
				ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, observed.Fault, label + ": load");

				SemanticEventKey key = KernelCanonicalTests.GoldenKey();
				KernelFaultCode fault;
				ClassicAssert.IsFalse(FixedPeriodToyRules.TryGetEventKey(bad, 0uL, out key, out fault), label);
				ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, fault, label + ": key expansion");
				ClassicAssert.AreEqual(default(SemanticEventKey), key, label);

				byte[] bytes = new byte[] { 9 };
				ClassicAssert.IsFalse(FixedPeriodToyRules.TryEncodeCanonical(bad, out bytes, out fault), label);
				ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, fault, label + ": encode");
				ClassicAssert.IsNull(bytes, label);

				AssertRefusedWithoutMutation(bad, label);
			}

			// What genuinely does remain a latch fault: a value that selects no rule at all. There
			// is nothing to evaluate ahead of it, so this is not an exception to the order — it is
			// the order, applied to a state with no non-latch rule available.
			FixedPeriodToyState unreadable = new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, true, 5L, 0uL, 10L,
				new OptionLatchState((OptionLatchValue)200, 0L), false, default(ToyPulseRange));
			ToyAdvanceResult unreadableResult = FixedPeriodToyRules.AdvanceThrough(unreadable, 1000L, true);
			ClassicAssert.IsFalse(unreadableResult.Succeeded);
			ClassicAssert.AreEqual(KernelFaultCode.InvalidOptionLatch, unreadableResult.Fault,
				"an unknown value selects no schedule rule, so there is nothing that could outrank it");
			AssertRefusedWithoutMutation(unreadable, "unreadable latch + schedule");

			// Unobserved is the same case by a different route.
			FixedPeriodToyState unobserved = new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, true, 5L, 0uL, 10L,
				new OptionLatchState(OptionLatchValue.Unobserved, 0L), false, default(ToyPulseRange));
			ToyAdvanceResult unobservedResult = FixedPeriodToyRules.AdvanceThrough(unobserved, 1000L, true);
			ClassicAssert.IsFalse(unobservedResult.Succeeded);
			ClassicAssert.AreEqual(KernelFaultCode.InvalidOptionLatch, unobservedResult.Fault);
			AssertRefusedWithoutMutation(unobserved, "unobserved latch + schedule");

			// And the tick-reading half of the schedule rule still reports the state, once the
			// latch itself is sound.
			FixedPeriodToyState deadlineBeforeChange = new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, true, 5L, 0uL, 10L,
				new OptionLatchState(OptionLatchValue.Enabled, 20L), false, default(ToyPulseRange));
			ToyAdvanceResult deadlineResult = FixedPeriodToyRules.AdvanceThrough(deadlineBeforeChange, 1000L, true);
			ClassicAssert.IsFalse(deadlineResult.Succeeded);
			ClassicAssert.AreEqual(KernelFaultCode.InvalidToyState, deadlineResult.Fault,
				"a deadline at or before the latch change is a state fault, not a latch fault");
		}

		/// <summary>
		/// Snapshots every raw field of a malformed source, drives every entry point at it, and
		/// asserts nothing moved. Canonical bytes are compared only where the fixture is encodable,
		/// because the encoder is required to refuse an invalid source — so for the rest, the raw
		/// fields and the reference are the whole guarantee.
		/// </summary>
		private static void AssertRefusedWithoutMutation(FixedPeriodToyState bad, string label)
		{
			int schema = bad.SchemaVersion;
			int rules = bad.RulesVersion;
			ulong seedHigh = bad.SimulationSeed.High;
			ulong seedLow = bad.SimulationSeed.Low;
			string settlement = bad.SettlementId;
			long processed = bad.ProcessedThroughTick;
			bool scheduled = bad.ClockScheduled;
			long nextDue = bad.NextDueTick;
			ulong nextOrdinal = bad.NextOrdinal;
			long interval = bad.IntervalTicks;
			OptionLatchValue latchValue = bad.OptionLatch.Value;
			long latchTick = bad.OptionLatch.ChangedAtTick;
			bool hasRange = bad.HasEmittedRange;
			int rangeRules = bad.EmittedRange.RulesVersionAtCreation;
			string rangeStream = bad.EmittedRange.EventStreamId;
			uint rangeKind = bad.EmittedRange.EventKindCode;
			ulong rangeFirst = bad.EmittedRange.FirstOrdinal;
			ulong rangeCount = bad.EmittedRange.Count;

			KernelFaultCode fault;

			ToyAdvanceResult advanced = FixedPeriodToyRules.AdvanceThrough(bad, long.MaxValue / 2L, true);
			ClassicAssert.IsFalse(advanced.Succeeded, label + ": advance must refuse");
			ClassicAssert.IsTrue(ReferenceEquals(bad, advanced.State), label + ": advance returns the caller's object");
			ClassicAssert.AreEqual(OptionTransitionKind.None, advanced.OptionTransition, label + ": advance transitions nothing");

			ToyAdvanceResult observed = FixedPeriodToyRules.ObserveOptionOnLoad(bad, long.MaxValue / 2L, false);
			ClassicAssert.IsFalse(observed.Succeeded, label + ": load observation must refuse");
			ClassicAssert.IsTrue(ReferenceEquals(bad, observed.State), label + ": load returns the caller's object");
			ClassicAssert.AreEqual(OptionTransitionKind.None, observed.OptionTransition, label + ": load transitions nothing");

			SemanticEventKey key;
			ClassicAssert.IsFalse(FixedPeriodToyRules.TryGetEventKey(bad, 0uL, out key, out fault), label + ": key expansion must refuse");
			ClassicAssert.AreEqual(default(SemanticEventKey), key, label + ": a refused expansion publishes the default key");

			byte[] bytes;
			ClassicAssert.IsFalse(FixedPeriodToyRules.TryEncodeCanonical(bad, out bytes, out fault), label + ": encode must refuse");
			ClassicAssert.IsNull(bytes, label + ": a refused encode publishes no buffer");

			ClassicAssert.AreEqual(schema, bad.SchemaVersion, label);
			ClassicAssert.AreEqual(rules, bad.RulesVersion, label);
			ClassicAssert.AreEqual(seedHigh, bad.SimulationSeed.High, label);
			ClassicAssert.AreEqual(seedLow, bad.SimulationSeed.Low, label);
			ClassicAssert.AreEqual(settlement, bad.SettlementId, label);
			ClassicAssert.AreEqual(processed, bad.ProcessedThroughTick, label);
			ClassicAssert.AreEqual(scheduled, bad.ClockScheduled, label);
			ClassicAssert.AreEqual(nextDue, bad.NextDueTick, label);
			ClassicAssert.AreEqual(nextOrdinal, bad.NextOrdinal, label);
			ClassicAssert.AreEqual(interval, bad.IntervalTicks, label);
			ClassicAssert.AreEqual(latchValue, bad.OptionLatch.Value, label);
			ClassicAssert.AreEqual(latchTick, bad.OptionLatch.ChangedAtTick, label);
			ClassicAssert.AreEqual(hasRange, bad.HasEmittedRange, label);
			ClassicAssert.AreEqual(rangeRules, bad.EmittedRange.RulesVersionAtCreation, label);
			ClassicAssert.AreEqual(rangeStream, bad.EmittedRange.EventStreamId, label);
			ClassicAssert.AreEqual(rangeKind, bad.EmittedRange.EventKindCode, label);
			ClassicAssert.AreEqual(rangeFirst, bad.EmittedRange.FirstOrdinal, label);
			ClassicAssert.AreEqual(rangeCount, bad.EmittedRange.Count, label);
		}

		[Test]
		public void AnEnormousDueCountFoldsWithoutIterating()
		{
			// A single wake far in the future must fold the whole span in one step. If anything
			// looped per occurrence this would not return.
			ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, 1L, true);
			ToyAdvanceResult advanced = FixedPeriodToyRules.AdvanceThrough(created.State, 4000000000L, true);
			ClassicAssert.IsTrue(advanced.Succeeded);
			ClassicAssert.AreEqual(4000000000uL, advanced.State.NextOrdinal);
			ClassicAssert.AreEqual(4000000000uL, advanced.State.EmittedRange.Count);
			ClassicAssert.AreEqual(4000000001L, advanced.State.NextDueTick);
		}

		private static void AssertFields(Type type, string[] names, Type[] types)
		{
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic
				| BindingFlags.DeclaredOnly);
			Array.Sort(fields, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
			CollectionAssert.AreEqual(names, Array.ConvertAll(fields, field => field.Name), type.Name);
			CollectionAssert.AreEqual(types, Array.ConvertAll(fields, field => field.FieldType), type.Name);
			foreach (FieldInfo field in fields)
				ClassicAssert.IsTrue(field.IsAssembly && field.IsInitOnly, type.Name + "." + field.Name);
		}

		private static string LogicalSource()
		{
			return string.Join("\n", new[]
			{
				TestMain.ReadRepositoryText(Path.Combine("Simulation", "Kernel", "FixedPeriodToy.Declarations.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Simulation", "Kernel", "FixedPeriodToy.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Simulation", "Kernel", "FixedPeriodToy.OptionTransitions.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Simulation", "Kernel", "FixedPeriodToy.EventAndCodec.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Simulation", "Kernel", "FixedPeriodToy.ValidationAndFold.cs"))
			});
		}

		private static int Count(string source, string term)
		{
			int count = 0;
			int at = 0;
			while ((at = source.IndexOf(term, at, StringComparison.Ordinal)) >= 0)
			{
				count++;
				at += term.Length;
			}
			return count;
		}
	}
}
#endif
