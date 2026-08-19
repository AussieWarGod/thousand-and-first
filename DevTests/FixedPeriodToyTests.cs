#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Tests
{
	public class FixedPeriodToyTests
	{
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
			Assert.IsTrue(created.Succeeded, "create");
			ToyAdvanceResult advanced = FixedPeriodToyRules.AdvanceThrough(created.State, 25L, true);
			Assert.IsTrue(advanced.Succeeded, "advance");
			return advanced.State;
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
			Assert.IsTrue(FixedPeriodToyRules.TryEncodeCanonical(state, out bytes, out fault), "encode fault " + fault);
			return KernelDigest.ToLowercaseHex(bytes);
		}

		[Test]
		public void TheFixtureStateMatchesTheCardExactly()
		{
			FixedPeriodToyState state = Fixture();
			Assert.AreEqual(25L, state.ProcessedThroughTick);
			Assert.AreEqual(30L, state.NextDueTick);
			Assert.AreEqual(2uL, state.NextOrdinal);
			Assert.IsTrue(state.ClockScheduled);
			Assert.AreEqual(OptionLatchValue.Enabled, state.OptionLatch.Value);
			Assert.AreEqual(0L, state.OptionLatch.ChangedAtTick);
			Assert.IsTrue(state.HasEmittedRange);
			Assert.AreEqual(3, state.EmittedRange.RulesVersionAtCreation);
			Assert.AreEqual(FixedPeriodToyRules.ToyPulseEventStreamId, state.EmittedRange.EventStreamId);
			Assert.AreEqual(FixedPeriodToyRules.ToyPulseEventKind, state.EmittedRange.EventKindCode);
			Assert.AreEqual(0uL, state.EmittedRange.FirstOrdinal);
			Assert.AreEqual(2uL, state.EmittedRange.Count);
		}

		[Test]
		public void TheFixtureEncodesToTheHardCoded183Bytes()
		{
			byte[] bytes;
			KernelFaultCode fault;
			Assert.IsTrue(FixedPeriodToyRules.TryEncodeCanonical(Fixture(), out bytes, out fault));
			Assert.AreEqual(183, bytes.Length);
			Assert.AreEqual(FixtureHex, KernelDigest.ToLowercaseHex(bytes));
			Assert.AreEqual(0x7E, bytes[bytes.Length - 1], "terminal marker");
		}

		[Test]
		public void OrdinalZeroExpandsToTheHardCodedEventId()
		{
			SemanticEventKey key;
			KernelFaultCode fault;
			Assert.IsTrue(FixedPeriodToyRules.TryGetEventKey(Fixture(), 0uL, out key, out fault));
			string id;
			Assert.IsTrue(SemanticEventIdentity.TryCreateId(KernelCanonicalTests.GoldenSeed(), key, out id, out fault));
			Assert.AreEqual(ToyOrdinalZeroEventId, id);
		}

		[Test]
		public void OnlyOrdinalsInsideTheEmittedRangeHaveIdentity()
		{
			FixedPeriodToyState state = Fixture();
			SemanticEventKey key;
			KernelFaultCode fault;
			Assert.IsTrue(FixedPeriodToyRules.TryGetEventKey(state, 0uL, out key, out fault));
			Assert.IsTrue(FixedPeriodToyRules.TryGetEventKey(state, 1uL, out key, out fault));
			// Ordinal 2 is NextOrdinal: nothing has emitted it yet, so it has no identity.
			Assert.IsFalse(FixedPeriodToyRules.TryGetEventKey(state, 2uL, out key, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidEventKey, fault);
			Assert.IsFalse(FixedPeriodToyRules.TryGetEventKey(state, ulong.MaxValue, out key, out fault));
		}

		[Test]
		public void CreateDisabledSchedulesNothing()
		{
			ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 5L, 10L, false);
			Assert.IsTrue(created.Succeeded);
			Assert.IsFalse(created.State.ClockScheduled);
			Assert.AreEqual(0L, created.State.NextDueTick);
			Assert.AreEqual(OptionTransitionKind.InitializedDisabled, created.OptionTransition);
			Assert.IsFalse(created.State.HasEmittedRange);
			Assert.AreEqual(0uL, created.State.NextOrdinal);
		}

		[TestCase(-1L, 10L, 1)]
		[TestCase(0L, 0L, 2)]
		[TestCase(0L, -5L, 2)]
		public void CreateFailsClosed(long now, long interval, int expectedCode)
		{
			KernelFaultCode expected = (KernelFaultCode)expectedCode;
			ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, now, interval, true);
			Assert.IsFalse(created.Succeeded);
			Assert.AreEqual(expected, created.Fault);
			Assert.IsNull(created.State);
		}

		[Test]
		public void CreateRejectsABadSettlementOrRulesVersion()
		{
			Assert.AreEqual(KernelFaultCode.InvalidToyState,
				FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 0, Settlement, 0L, 10L, true).Fault);
			Assert.AreEqual(KernelFaultCode.InvalidToyState,
				FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, "nope", 0L, 10L, true).Fault);
		}

		[Test]
		public void LoadObservationNeverEmitsAndNeverMovesProcessedThrough()
		{
			FixedPeriodToyState state = Fixture();
			ToyAdvanceResult unchanged = FixedPeriodToyRules.ObserveOptionOnLoad(state, 500L, true);
			Assert.IsTrue(unchanged.Succeeded);
			Assert.AreEqual(OptionTransitionKind.None, unchanged.OptionTransition);
			Assert.AreSame(state, unchanged.State, "an unchanged load is a no-op");

			ToyAdvanceResult disabled = FixedPeriodToyRules.ObserveOptionOnLoad(state, 500L, false);
			Assert.IsTrue(disabled.Succeeded);
			Assert.AreEqual(OptionTransitionKind.Disabled, disabled.OptionTransition);
			Assert.AreEqual(25L, disabled.State.ProcessedThroughTick, "load is an observation, not a simulation step");
			Assert.AreEqual(2uL, disabled.State.NextOrdinal, "no pulse is emitted on load");
			Assert.IsFalse(disabled.State.ClockScheduled);
			Assert.AreEqual(0L, disabled.State.NextDueTick);
		}

		[Test]
		public void LoadReanchorsRatherThanInferringAnOfflineBacklog()
		{
			// The old overdue schedule is discarded deliberately: a change seen across a stopped
			// process is not a backlog of activity that happened while nobody was playing.
			ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, 10L, false);
			ToyAdvanceResult resumed = FixedPeriodToyRules.ObserveOptionOnLoad(created.State, 1000L, true);
			Assert.IsTrue(resumed.Succeeded);
			Assert.AreEqual(OptionTransitionKind.Enabled, resumed.OptionTransition);
			Assert.AreEqual(1010L, resumed.State.NextDueTick, "one full interval from load, not replayed history");
			Assert.AreEqual(0uL, resumed.State.NextOrdinal);
			Assert.AreEqual(0L, resumed.State.ProcessedThroughTick);
		}

		[Test]
		public void RepeatedLoadObservationChangesNothing()
		{
			FixedPeriodToyState state = Fixture();
			string before = Encode(state);
			for (int i = 0; i < 100; i++)
			{
				ToyAdvanceResult step = FixedPeriodToyRules.ObserveOptionOnLoad(state, 100L + i, true);
				Assert.IsTrue(step.Succeeded);
				state = step.State;
			}
			Assert.AreEqual(before, Encode(state), "loading repeatedly must not advance, materialize, reroll, or notify");
		}

		[Test]
		public void UnchangedDisabledWakeEmitsNothingButStillProcessesThrough()
		{
			ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, 10L, false);
			ToyAdvanceResult advanced = FixedPeriodToyRules.AdvanceThrough(created.State, 999L, false);
			Assert.IsTrue(advanced.Succeeded);
			Assert.AreEqual(999L, advanced.State.ProcessedThroughTick);
			Assert.AreEqual(0uL, advanced.State.NextOrdinal);
			Assert.IsFalse(advanced.State.HasEmittedRange);
			Assert.AreEqual(OptionTransitionKind.None, advanced.OptionTransition);
		}

		[Test]
		public void ResumingSchedulesAFullIntervalAndNeverReplaysDisabledTime()
		{
			ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, 10L, false);
			ToyAdvanceResult resumed = FixedPeriodToyRules.AdvanceThrough(created.State, 100L, true);
			Assert.IsTrue(resumed.Succeeded);
			Assert.AreEqual(110L, resumed.State.NextDueTick);
			Assert.AreEqual(0uL, resumed.State.NextOrdinal, "the disabled century produced nothing");
			Assert.AreEqual(100L, resumed.State.ProcessedThroughTick);
		}

		[Test]
		public void DisablingFreezesTheAccumulatedRange()
		{
			FixedPeriodToyState state = Fixture();
			ToyAdvanceResult disabled = FixedPeriodToyRules.AdvanceThrough(state, 26L, false);
			Assert.IsTrue(disabled.Succeeded);
			Assert.AreEqual(2uL, disabled.State.NextOrdinal);
			Assert.IsTrue(disabled.State.HasEmittedRange, "history survives being switched off");
			Assert.AreEqual(2uL, disabled.State.EmittedRange.Count);
			Assert.IsFalse(disabled.State.ClockScheduled);
		}

		[Test]
		public void AdvanceFailsClosedOnRegressionAndLeavesTheSourceUntouched()
		{
			FixedPeriodToyState state = Fixture();
			string before = Encode(state);
			ToyAdvanceResult regressed = FixedPeriodToyRules.AdvanceThrough(state, 24L, true);
			Assert.IsFalse(regressed.Succeeded);
			Assert.AreEqual(KernelFaultCode.ClockRegression, regressed.Fault);
			Assert.AreSame(state, regressed.State, "the original reference comes back");
			Assert.AreEqual(before, Encode(state), "caller state byte-identical after a fault");

			ToyAdvanceResult negative = FixedPeriodToyRules.AdvanceThrough(state, -1L, true);
			Assert.IsFalse(negative.Succeeded);
			Assert.AreEqual(KernelFaultCode.InvalidTick, negative.Fault);
		}

		[Test]
		public void ANullOrMalformedStateIsRefused()
		{
			KernelFaultCode fault;
			Assert.IsFalse(FixedPeriodToyRules.IsCanonical(null, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidToyState, fault);

			Assert.AreEqual(KernelFaultCode.InvalidToyState, FixedPeriodToyRules.AdvanceThrough(null, 0L, true).Fault);
			Assert.AreEqual(KernelFaultCode.InvalidToyState, FixedPeriodToyRules.ObserveOptionOnLoad(null, 0L, true).Fault);

			byte[] bytes;
			Assert.IsFalse(FixedPeriodToyRules.TryEncodeCanonical(null, out bytes, out fault));
			Assert.IsNull(bytes);
		}

		[Test]
		public void CanonicalInvariantsRejectContradictoryStates()
		{
			KernelFaultCode fault;
			KernelSeed128 seed = KernelCanonicalTests.GoldenSeed();
			OptionLatchState enabled = new OptionLatchState(OptionLatchValue.Enabled, 0L);
			OptionLatchState disabled = new OptionLatchState(OptionLatchValue.Disabled, 0L);

			// Enabled but unscheduled.
			Assert.IsFalse(FixedPeriodToyRules.IsCanonical(new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L, enabled, false, default(ToyPulseRange)), out fault));

			// Enabled with a deadline that is not strictly after processed-through.
			Assert.IsFalse(FixedPeriodToyRules.IsCanonical(new FixedPeriodToyState(
				1, 3, seed, Settlement, 10L, true, 10L, 0uL, 10L, enabled, false, default(ToyPulseRange)), out fault));

			// Disabled but carrying a schedule.
			Assert.IsFalse(FixedPeriodToyRules.IsCanonical(new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, true, 5L, 0uL, 10L, disabled, false, default(ToyPulseRange)), out fault));

			// An unobserved latch is never valid on a live toy.
			Assert.IsFalse(FixedPeriodToyRules.IsCanonical(new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L, new OptionLatchState(OptionLatchValue.Unobserved, 0L), false, default(ToyPulseRange)), out fault));
			Assert.AreEqual(KernelFaultCode.InvalidOptionLatch, fault);

			// Range present but its span disagrees with NextOrdinal.
			Assert.IsFalse(FixedPeriodToyRules.IsCanonical(new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, false, 0L, 5uL, 10L, disabled, true,
				new ToyPulseRange(3, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, 2uL)), out fault));

			// Absent range but a nonzero ordinal.
			Assert.IsFalse(FixedPeriodToyRules.IsCanonical(new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, false, 0L, 3uL, 10L, disabled, false, default(ToyPulseRange)), out fault));

			// Range whose stream or kind is not the reserved toy constant.
			Assert.IsFalse(FixedPeriodToyRules.IsCanonical(new FixedPeriodToyState(
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
					Assert.IsTrue(created.Succeeded);

					ToyAdvanceResult folded = FixedPeriodToyRules.AdvanceThrough(created.State, end, true);
					Assert.IsTrue(folded.Succeeded, "folded advance to " + end);

					// The replay: wake at literally every tick, so each pulse is processed alone.
					FixedPeriodToyState replayed = created.State;
					for (long t = 1L; t <= end; t++)
					{
						ToyAdvanceResult step = FixedPeriodToyRules.AdvanceThrough(replayed, t, true);
						Assert.IsTrue(step.Succeeded, "replay step " + t);
						replayed = step.State;
					}

					Assert.AreEqual(folded.State.NextOrdinal, replayed.NextOrdinal, "next ordinal, interval " + interval + ", end " + end);
					Assert.AreEqual(folded.State.NextDueTick, replayed.NextDueTick, "following due, interval " + interval + ", end " + end);
					Assert.AreEqual(folded.State.HasEmittedRange, replayed.HasEmittedRange);
					if (folded.State.HasEmittedRange)
					{
						Assert.AreEqual(folded.State.EmittedRange.FirstOrdinal, replayed.EmittedRange.FirstOrdinal, "first ordinal");
						Assert.AreEqual(folded.State.EmittedRange.Count, replayed.EmittedRange.Count, "count");
					}
					Assert.AreEqual(Encode(folded.State), Encode(replayed), "complete canonical bytes, interval " + interval + ", end " + end);

					// Every pulse expands to the same identity either way.
					for (ulong ordinal = 0uL; ordinal < folded.State.NextOrdinal; ordinal++)
					{
						SemanticEventKey foldedKey;
						SemanticEventKey replayedKey;
						Assert.IsTrue(FixedPeriodToyRules.TryGetEventKey(folded.State, ordinal, out foldedKey, out fault));
						Assert.IsTrue(FixedPeriodToyRules.TryGetEventKey(replayed, ordinal, out replayedKey, out fault));
						string foldedId;
						string replayedId;
						Assert.IsTrue(SemanticEventIdentity.TryCreateId(KernelCanonicalTests.GoldenSeed(), foldedKey, out foldedId, out fault));
						Assert.IsTrue(SemanticEventIdentity.TryCreateId(KernelCanonicalTests.GoldenSeed(), replayedKey, out replayedId, out fault));
						Assert.AreEqual(foldedId, replayedId, "event id for ordinal " + ordinal + ", interval " + interval + ", end " + end);
					}
					compared++;
				}
			}
			Assert.AreEqual(6 * 41, compared);
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
				Assert.IsTrue(upTo.Succeeded, "advance to " + boundary);

				// The reload: a genuinely distinct object carrying the same field values, which is
				// what a load actually produces. Reusing the same reference here would have tested
				// nothing at all — every assertion below would pass on an object that was never
				// reconstructed.
				string saved = Encode(upTo.State);
				FixedPeriodToyState reloaded = Clone(upTo.State);
				Assert.IsFalse(ReferenceEquals(upTo.State, reloaded), "the reload fixture must be a distinct object");
				Assert.AreEqual(saved, Encode(reloaded), "a reconstructed state must be byte-identical at " + boundary);

				ToyAdvanceResult continued = FixedPeriodToyRules.AdvanceThrough(reloaded, boundary + 25L, true);
				Assert.IsTrue(continued.Succeeded);

				// The control: never saved at all.
				ToyAdvanceResult straight = FixedPeriodToyRules.AdvanceThrough(created.State, boundary + 25L, true);
				Assert.IsTrue(straight.Succeeded);

				Assert.AreEqual(Encode(straight.State), Encode(continued.State),
					"a reload at tick " + boundary + " changed the outcome");
				Assert.AreEqual(0uL, continued.State.EmittedRange.FirstOrdinal, "the range must stay one span, not split at " + boundary);
				Assert.AreEqual(continued.State.NextOrdinal, continued.State.EmittedRange.Count, "no ordinal duplicated or skipped");
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
			Assert.IsTrue(actual.Succeeded);
			Assert.AreEqual(2uL, actual.State.NextOrdinal, "deadlines at 10 and 20 have passed");
			Assert.AreEqual(30L, actual.State.NextDueTick);

			// Banned oracle one: reanchor from now. Loses the five ticks already served toward the
			// next deadline, so every wake quietly pushes the schedule further out and a settlement
			// observed often runs slower than one observed rarely.
			long reanchored = Now + Interval;
			Assert.AreEqual(35L, reanchored);
			Assert.AreNotEqual(actual.State.NextDueTick, reanchored,
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
			Assert.AreEqual(1uL, cappedCount);
			Assert.AreNotEqual(actual.State.NextOrdinal, cappedCount,
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
				Assert.IsTrue(jumped.Succeeded, days + "-day jump");
				Assert.AreEqual((ulong)days, jumped.State.NextOrdinal, days + "-day jump ordinal");

				// Fine-grained: one wake per day. Same answer, or absence means something different
				// from presence and the whole model is broken.
				FixedPeriodToyState stepped = created.State;
				for (long d = 1L; d <= days; d++)
				{
					ToyAdvanceResult step = FixedPeriodToyRules.AdvanceThrough(stepped, d * Day, true);
					Assert.IsTrue(step.Succeeded);
					stepped = step.State;
				}
				Assert.AreEqual(Encode(jumped.State), Encode(stepped), days + "-day jump diverged from daily observation");
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
			Assert.IsTrue(SemanticEventKey.TryCreate(3, Settlement, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, out underN, out fault));
			Assert.IsTrue(SemanticEventKey.TryCreate(4, Settlement, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, out underNext, out fault));

			string idN;
			string idNext;
			Assert.IsTrue(SemanticEventIdentity.TryCreateId(KernelCanonicalTests.GoldenSeed(), underN, out idN, out fault));
			Assert.IsTrue(SemanticEventIdentity.TryCreateId(KernelCanonicalTests.GoldenSeed(), underNext, out idNext, out fault));
			Assert.AreNotEqual(idN, idNext, "otherwise identical keys under versions N and N+1 must differ");

			// An already-emitted range keeps the version it was emitted under.
			ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, 10L, true);
			ToyAdvanceResult advanced = FixedPeriodToyRules.AdvanceThrough(created.State, 25L, true);
			Assert.IsTrue(advanced.Succeeded);
			Assert.AreEqual(3, advanced.State.EmittedRange.RulesVersionAtCreation, "the range owns its version");

			SemanticEventKey emitted;
			Assert.IsTrue(FixedPeriodToyRules.TryGetEventKey(advanced.State, 0uL, out emitted, out fault));
			Assert.AreEqual(3, emitted.RulesVersionAtCreation, "expanding an emitted ordinal must not adopt a newer version");

			// And the lane continues: the next ordinal is 2, not a reset to 0.
			Assert.AreEqual(2uL, advanced.State.NextOrdinal);
			Assert.AreEqual(FixedPeriodToyRules.ToyPulseEventStreamId, advanced.State.EmittedRange.EventStreamId);
			Assert.AreEqual(FixedPeriodToyRules.ToyPulseEventKind, advanced.State.EmittedRange.EventKindCode);
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
			Assert.IsFalse(FixedPeriodToyRules.IsCanonical(deadlineAtLatchChange, out fault),
				"a deadline may not coincide with a later latch change");

			// A present range claiming zero pulses. An emitted range with nothing in it is not a
			// smaller range, it is a contradiction: the flag says something happened.
			FixedPeriodToyState zeroCountRange = new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L,
				new OptionLatchState(OptionLatchValue.Disabled, 0L), true,
				new ToyPulseRange(3, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, 0uL));
			Assert.IsFalse(FixedPeriodToyRules.IsCanonical(zeroCountRange, out fault), "an emitted range of zero pulses is a contradiction");

			// A range whose span wraps past the top of the ordinal space.
			FixedPeriodToyState wrappingRange = new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, false, 0L, 0uL, 10L,
				new OptionLatchState(OptionLatchValue.Disabled, 0L), true,
				new ToyPulseRange(3, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, ulong.MaxValue, 2uL));
			Assert.IsFalse(FixedPeriodToyRules.IsCanonical(wrappingRange, out fault), "a range may not wrap the ordinal space");

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
			Assert.IsFalse(refused.Succeeded, "an invalid source must not advance");
			Assert.AreEqual(KernelFaultCode.InvalidToyState, refused.Fault);
			Assert.IsTrue(ReferenceEquals(source, refused.State), "the caller must get its own object back, not a repaired one");

			Assert.AreEqual(schemaVersion, source.SchemaVersion);
			Assert.AreEqual(rulesVersion, source.RulesVersion);
			Assert.AreEqual(processed, source.ProcessedThroughTick);
			Assert.AreEqual(scheduled, source.ClockScheduled);
			Assert.AreEqual(nextDue, source.NextDueTick);
			Assert.AreEqual(nextOrdinal, source.NextOrdinal);
			Assert.AreEqual(interval, source.IntervalTicks);
			Assert.AreEqual(latchValue, source.OptionLatch.Value);
			Assert.AreEqual(latchTick, source.OptionLatch.ChangedAtTick);
			Assert.AreEqual(hasRange, source.HasEmittedRange);
			Assert.AreEqual(firstOrdinal, source.EmittedRange.FirstOrdinal);
			Assert.AreEqual(count, source.EmittedRange.Count);
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
			Assert.IsFalse(bothBad.Succeeded);
			Assert.AreEqual(KernelFaultCode.InvalidTick, bothBad.Fault, "tick before interval");
			Assert.IsNull(bothBad.State, "nothing partial is published");

			// Create: a bad interval and a bad settlement identifier together. Create resolves this
			// as interval-before-identity.
			//
			// Note the asymmetry with AdvanceThrough below, which resolves state before arithmetic.
			// It is defensible — Create has no prior state to sanity-check, only arguments that are
			// about to become state — but it is a real difference in two neighbouring APIs, and a
			// caller that branches on the code will meet it. Pinned here as observed behaviour and
			// flagged for review rather than quietly matched.
			ToyAdvanceResult badIdAndInterval = FixedPeriodToyRules.Create(seed, 3, "NOPE", 0L, 0L, true);
			Assert.IsFalse(badIdAndInterval.Succeeded);
			Assert.AreEqual(KernelFaultCode.InvalidInterval, badIdAndInterval.Fault, "Create resolves interval before identity");

			// With a valid interval, the identity fault does surface.
			ToyAdvanceResult badIdOnly = FixedPeriodToyRules.Create(seed, 3, "NOPE", 0L, 10L, true);
			Assert.IsFalse(badIdOnly.Succeeded);
			Assert.AreEqual(KernelFaultCode.InvalidToyState, badIdOnly.Fault);
			Assert.IsNull(badIdOnly.State);

			// Advance: an invalid source and a regressed clock. The source is checked first,
			// because a regression judged against nonsense is not a meaningful answer.
			FixedPeriodToyState invalid = new FixedPeriodToyState(
				1, 3, seed, Settlement, 100L, true, 5L, 0uL, 10L,
				new OptionLatchState(OptionLatchValue.Enabled, 0L), false, default(ToyPulseRange));
			Assert.IsFalse(FixedPeriodToyRules.IsCanonical(invalid, out fault));
			ToyAdvanceResult invalidAndRegressed = FixedPeriodToyRules.AdvanceThrough(invalid, 1L, true);
			Assert.IsFalse(invalidAndRegressed.Succeeded);
			Assert.AreEqual(KernelFaultCode.InvalidToyState, invalidAndRegressed.Fault, "source state before regression");
			Assert.IsTrue(ReferenceEquals(invalid, invalidAndRegressed.State));

			// Advance: a valid source with both a negative tick and a regression. Negative wins.
			FixedPeriodToyState valid = Fixture();
			ToyAdvanceResult negativeAndRegressed = FixedPeriodToyRules.AdvanceThrough(valid, -5L, true);
			Assert.IsFalse(negativeAndRegressed.Succeeded);
			Assert.AreEqual(KernelFaultCode.InvalidTick, negativeAndRegressed.Fault, "negative tick before regression");
			Assert.IsTrue(ReferenceEquals(valid, negativeAndRegressed.State));

			// Load observation: an invalid latch on the source plus a regressed tick.
			FixedPeriodToyState unobservedLatch = new FixedPeriodToyState(
				1, 3, seed, Settlement, 50L, false, 0L, 0uL, 10L,
				new OptionLatchState(OptionLatchValue.Unobserved, 0L), false, default(ToyPulseRange));
			ToyAdvanceResult loadRefused = FixedPeriodToyRules.ObserveOptionOnLoad(unobservedLatch, 1L, true);
			Assert.IsFalse(loadRefused.Succeeded);
			Assert.AreEqual(KernelFaultCode.InvalidOptionLatch, loadRefused.Fault, "latch validity before regression");
			Assert.IsTrue(ReferenceEquals(unobservedLatch, loadRefused.State));

			// Event key expansion: an invalid source and an out-of-range ordinal.
			SemanticEventKey key;
			Assert.IsFalse(FixedPeriodToyRules.TryGetEventKey(invalid, ulong.MaxValue, out key, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidToyState, fault, "source state before ordinal range");

			// Encoding: an invalid source publishes no bytes at all.
			byte[] bytes;
			Assert.IsFalse(FixedPeriodToyRules.TryEncodeCanonical(invalid, out bytes, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidToyState, fault);
			Assert.IsNull(bytes, "a refused encode must not hand back a partial buffer");
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
					Assert.IsTrue(nextOrdinal >= count,
						"NextOrdinal must dominate Count, else the two guards could fire independently: first "
							+ first + ", count " + count);
				}
			}
		}

		private static void CheckOverflow(FixedPeriodToyState source, string label, Func<ToyAdvanceResult> act)
		{
			KernelFaultCode canonicalFault;
			bool sourceWasValid = FixedPeriodToyRules.IsCanonical(source, out canonicalFault);
			string before = sourceWasValid ? Encode(source) : null;

			ToyAdvanceResult result = act();
			Assert.IsFalse(result.Succeeded, label + " must fail closed");
			Assert.IsTrue(ReferenceEquals(source, result.State), label + ": the caller keeps its own object");

			if (sourceWasValid)
			{
				// The exact code, not merely a failure: an overflow reported as a bad state would
				// send a reader looking for corruption that is not there.
				Assert.AreEqual(KernelFaultCode.ArithmeticOverflow, result.Fault, label + ": must report overflow exactly");
				Assert.AreEqual(OptionTransitionKind.None, result.OptionTransition, label + ": a refusal transitions nothing");
				Assert.AreEqual(before, Encode(source), label + ": the source bytes must be untouched");
				Assert.AreEqual(before, Encode(result.State), label + ": the returned state is the untouched source");
			}
			else
			{
				// A source the encoder itself refuses cannot be compared by bytes, so the
				// reference identity above is the whole guarantee, and the fault must say so.
				Assert.AreEqual(KernelFaultCode.InvalidToyState, result.Fault, label + ": invalid source reports as such");
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
						Assert.IsTrue(created.Succeeded);

						ToyAdvanceResult direct = FixedPeriodToyRules.AdvanceThrough(created.State, wake, thenEnabled);
						Assert.IsTrue(direct.Succeeded, "direct wake at " + wake);

						// The same input history, observed at every tick instead of once.
						FixedPeriodToyState walked = created.State;
						for (long t = 1L; t <= wake; t++)
						{
							// The option takes its new value at the wake tick and not before,
							// which is the whole point of the boundary.
							bool valueNow = t < wake ? startEnabled : thenEnabled;
							ToyAdvanceResult step = FixedPeriodToyRules.AdvanceThrough(walked, t, valueNow);
							Assert.IsTrue(step.Succeeded, "tick " + t);
							walked = step.State;
						}

						Assert.AreEqual(Encode(direct.State), Encode(walked),
							"start " + startEnabled + ", wake " + wake + ", then " + thenEnabled);
						combinations++;
					}
				}
			}
			Assert.AreEqual(2 * 3 * 2, combinations);
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
			Assert.IsFalse(FixedPeriodToyRules.IsCanonical(wrongKind, out fault), "a foreign event kind must be refused");
			Assert.AreEqual(KernelFaultCode.InvalidToyState, fault);

			FixedPeriodToyState wrongRules = new FixedPeriodToyState(
				1, 3, seed, Settlement, 0L, false, 0L, 2uL, 10L, disabled, true,
				new ToyPulseRange(4, FixedPeriodToyRules.ToyPulseEventStreamId, FixedPeriodToyRules.ToyPulseEventKind, 0uL, 2uL));
			Assert.IsFalse(FixedPeriodToyRules.IsCanonical(wrongRules, out fault), "a range under another rules version must be refused");
			Assert.AreEqual(KernelFaultCode.InvalidToyState, fault);

			// Malformed states survive being refused, field for field, with the same reference back.
			foreach (FixedPeriodToyState bad in new FixedPeriodToyState[] { wrongKind, wrongRules })
			{
				int rules = bad.RulesVersion;
				ulong ordinal = bad.NextOrdinal;
				uint kind = bad.EmittedRange.EventKindCode;
				int rangeRules = bad.EmittedRange.RulesVersionAtCreation;
				ulong count = bad.EmittedRange.Count;

				ToyAdvanceResult refused = FixedPeriodToyRules.AdvanceThrough(bad, 500L, true);
				Assert.IsFalse(refused.Succeeded);
				Assert.AreEqual(KernelFaultCode.InvalidToyState, refused.Fault);
				Assert.IsTrue(ReferenceEquals(bad, refused.State));

				Assert.AreEqual(rules, bad.RulesVersion);
				Assert.AreEqual(ordinal, bad.NextOrdinal);
				Assert.AreEqual(kind, bad.EmittedRange.EventKindCode);
				Assert.AreEqual(rangeRules, bad.EmittedRange.RulesVersionAtCreation);
				Assert.AreEqual(count, bad.EmittedRange.Count);

				byte[] bytes;
				Assert.IsFalse(FixedPeriodToyRules.TryEncodeCanonical(bad, out bytes, out fault));
				Assert.IsNull(bytes, "a refused encode publishes no buffer");
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
				Assert.IsFalse(FixedPeriodToyRules.IsCanonical(bad, out fault), label + " must be refused");
				Assert.AreEqual(expected, fault, label + ": exact fault");

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
				Assert.IsFalse(FixedPeriodToyRules.IsCanonical(absentButDirty, out fault),
					"an absent range carrying raw field " + i + " must be refused");
				Assert.AreEqual(KernelFaultCode.InvalidToyState, fault, "raw field " + i);
				AssertRefusedWithoutMutation(absentButDirty, "absent range, raw field " + i);
			}
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
			Assert.IsFalse(advanced.Succeeded, label + ": advance must refuse");
			Assert.IsTrue(ReferenceEquals(bad, advanced.State), label + ": advance returns the caller's object");
			Assert.AreEqual(OptionTransitionKind.None, advanced.OptionTransition, label + ": advance transitions nothing");

			ToyAdvanceResult observed = FixedPeriodToyRules.ObserveOptionOnLoad(bad, long.MaxValue / 2L, false);
			Assert.IsFalse(observed.Succeeded, label + ": load observation must refuse");
			Assert.IsTrue(ReferenceEquals(bad, observed.State), label + ": load returns the caller's object");
			Assert.AreEqual(OptionTransitionKind.None, observed.OptionTransition, label + ": load transitions nothing");

			SemanticEventKey key;
			Assert.IsFalse(FixedPeriodToyRules.TryGetEventKey(bad, 0uL, out key, out fault), label + ": key expansion must refuse");
			Assert.AreEqual(default(SemanticEventKey), key, label + ": a refused expansion publishes the default key");

			byte[] bytes;
			Assert.IsFalse(FixedPeriodToyRules.TryEncodeCanonical(bad, out bytes, out fault), label + ": encode must refuse");
			Assert.IsNull(bytes, label + ": a refused encode publishes no buffer");

			Assert.AreEqual(schema, bad.SchemaVersion, label);
			Assert.AreEqual(rules, bad.RulesVersion, label);
			Assert.AreEqual(seedHigh, bad.SimulationSeed.High, label);
			Assert.AreEqual(seedLow, bad.SimulationSeed.Low, label);
			Assert.AreEqual(settlement, bad.SettlementId, label);
			Assert.AreEqual(processed, bad.ProcessedThroughTick, label);
			Assert.AreEqual(scheduled, bad.ClockScheduled, label);
			Assert.AreEqual(nextDue, bad.NextDueTick, label);
			Assert.AreEqual(nextOrdinal, bad.NextOrdinal, label);
			Assert.AreEqual(interval, bad.IntervalTicks, label);
			Assert.AreEqual(latchValue, bad.OptionLatch.Value, label);
			Assert.AreEqual(latchTick, bad.OptionLatch.ChangedAtTick, label);
			Assert.AreEqual(hasRange, bad.HasEmittedRange, label);
			Assert.AreEqual(rangeRules, bad.EmittedRange.RulesVersionAtCreation, label);
			Assert.AreEqual(rangeStream, bad.EmittedRange.EventStreamId, label);
			Assert.AreEqual(rangeKind, bad.EmittedRange.EventKindCode, label);
			Assert.AreEqual(rangeFirst, bad.EmittedRange.FirstOrdinal, label);
			Assert.AreEqual(rangeCount, bad.EmittedRange.Count, label);
		}

		[Test]
		public void AnEnormousDueCountFoldsWithoutIterating()
		{
			// A single wake far in the future must fold the whole span in one step. If anything
			// looped per occurrence this would not return.
			ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, 1L, true);
			ToyAdvanceResult advanced = FixedPeriodToyRules.AdvanceThrough(created.State, 4000000000L, true);
			Assert.IsTrue(advanced.Succeeded);
			Assert.AreEqual(4000000000uL, advanced.State.NextOrdinal);
			Assert.AreEqual(4000000000uL, advanced.State.EmittedRange.Count);
			Assert.AreEqual(4000000001L, advanced.State.NextDueTick);
		}
	}
}
#endif
