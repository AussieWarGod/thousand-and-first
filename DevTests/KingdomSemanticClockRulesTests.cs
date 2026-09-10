#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	public class KingdomSemanticClockRulesTests
	{
		[Test]
		public void CadenceIsAnAbsoluteGameDay()
		{
			ClassicAssert.AreEqual(KingdomRules.TicksPerDay, KingdomSemanticClockRules.CadenceTicks);
			ClassicAssert.AreEqual(0L, KingdomSemanticClockRules.AbsoluteBoundary(1199L));
			ClassicAssert.AreEqual(1200L, KingdomSemanticClockRules.AbsoluteBoundary(1200L));
			ClassicAssert.AreEqual(1200L, KingdomSemanticClockRules.AbsoluteBoundary(2399L));
		}

		[Test]
		public void ExistingSemanticStampCarriesBothLogicalClocks()
		{
			var state = KingdomSemanticClockRules.FromLastDispatchTick(2399L);

			ClassicAssert.AreEqual(1200L, state.LastBoundaryTick);
			ClassicAssert.AreEqual(2399L, state.LastDispatchTick);
		}

		[Test]
		public void EndTurnIsFreeUntilTheNextAbsoluteBoundary()
		{
			var state = new KingdomSemanticClockState(1200L, 1250L);
			var before = KingdomSemanticClockRules.Decide(state, 2399L, ForceActivation: false);
			var due = KingdomSemanticClockRules.Decide(state, 2400L, ForceActivation: false);

			ClassicAssert.IsFalse(before.ShouldDispatch);
			ClassicAssert.AreEqual(KingdomSemanticDispatchKind.Cadence, due.Kind);
			ClassicAssert.AreEqual(2400L, due.DueBoundaryTick);
			ClassicAssert.AreEqual(2400L, due.Next.LastBoundaryTick);
			ClassicAssert.AreEqual(2400L, due.Next.LastDispatchTick);
		}

		[Test]
		public void ObservationPartitionDoesNotMoveTheTerminalCheckpoint()
		{
			var initial = new KingdomSemanticClockState(0L, 0L);
			var direct = KingdomSemanticClockRules.Decide(initial, 4801L, ForceActivation: false);

			var split = initial;
			long[] observations = { 1L, 1199L, 1200L, 1250L, 2399L, 2400L, 3601L, 4801L };
			foreach (long tick in observations)
			{
				var decision = KingdomSemanticClockRules.Decide(split, tick, ForceActivation: false);
				if (decision.ShouldDispatch)
				{
					split = decision.Next;
				}
			}

			ClassicAssert.IsTrue(direct.ShouldDispatch);
			ClassicAssert.AreEqual(direct.Next.LastBoundaryTick, split.LastBoundaryTick);
			ClassicAssert.AreEqual(direct.Next.LastDispatchTick, split.LastDispatchTick);
		}

		[Test]
		public void ActivationCannotReplayAnyPartOfAnAlreadySettledDay()
		{
			var state = new KingdomSemanticClockState(2400L, 2400L);
			var duplicate = KingdomSemanticClockRules.Decide(state, 2400L, ForceActivation: true);
			var fresh = KingdomSemanticClockRules.Decide(state, 2401L, ForceActivation: true);

			ClassicAssert.IsFalse(duplicate.ShouldDispatch);
			ClassicAssert.IsFalse(fresh.ShouldDispatch);
		}

		[Test]
		public void FirstActivationSeedsExactlyOnePreBoundaryPass()
		{
			var empty = new KingdomSemanticClockState(0L, 0L);
			var first = KingdomSemanticClockRules.Decide(empty, 1L, ForceActivation: true);
			var settled = KingdomSemanticClockRules.Decide(first.Next, 1199L, ForceActivation: true);

			ClassicAssert.AreEqual(KingdomSemanticDispatchKind.Activation, first.Kind);
			ClassicAssert.AreEqual(1L, first.Next.LastDispatchTick);
			ClassicAssert.IsFalse(settled.ShouldDispatch);
		}

		[Test]
		public void FailedPassCanRetryBecauseDecisionDoesNotMutateInput()
		{
			var state = new KingdomSemanticClockState(1200L, 1300L);
			var first = KingdomSemanticClockRules.Decide(state, 2400L, ForceActivation: false);
			var retry = KingdomSemanticClockRules.Decide(state, 2400L, ForceActivation: false);

			ClassicAssert.AreEqual(KingdomSemanticDispatchKind.Cadence, first.Kind);
			ClassicAssert.AreEqual(first.Kind, retry.Kind);
			ClassicAssert.AreEqual(state.LastBoundaryTick, 1200L);
			ClassicAssert.AreEqual(state.LastDispatchTick, 1300L);
		}

		[Test]
		public void SubsystemReceiptResumesOnlyItsOwnGroundUntilPublished()
		{
			const long required = 15L;
			ClassicAssert.AreEqual(KingdomSemanticPassReceiptVerdict.Start,
				KingdomSemanticClockRules.ReceiptVerdict(false, 0L, null, 0L, required,
					0L, "A"));
			ClassicAssert.AreEqual(KingdomSemanticPassReceiptVerdict.Resume,
				KingdomSemanticClockRules.ReceiptVerdict(true, 2400L, "A", 3L, required,
					1200L, "A"));
			ClassicAssert.AreEqual(KingdomSemanticPassReceiptVerdict.RefuseDifferentGround,
				KingdomSemanticClockRules.ReceiptVerdict(true, 2400L, "A", 3L, required,
					1200L, "B"));
		}

		[Test]
		public void CompletedButUnpublishedReceiptIsReplayedAsNoOpsThenReplacedAfterPublish()
		{
			const long required = 31L;
			ClassicAssert.AreEqual(KingdomSemanticPassReceiptVerdict.Resume,
				KingdomSemanticClockRules.ReceiptVerdict(true, 2400L, "A", required,
					required, 1200L, "A"));
			ClassicAssert.AreEqual(KingdomSemanticPassReceiptVerdict.Start,
				KingdomSemanticClockRules.ReceiptVerdict(true, 2400L, "A", required,
					required, 2400L, "A"));
		}

		[TestCase(false, 2400L, "A", 0L, 7L, 1200L, 5100L)]
		[TestCase(true, 2400L, "A", 7L, 7L, 2400L, 5100L)]
		[TestCase(true, 2400L, "A", 7L, 7L, 2401L, 5100L)]
		[TestCase(true, 2400L, "A", 3L, 7L, 1200L, 1200L)]
		[TestCase(true, 2400L, "A", 7L, 7L, 2399L, 2399L)]
		[TestCase(true, 2400L, "A", 3L, 7L, 2400L, 2400L)]
		[TestCase(true, 0L, "A", 7L, 7L, 1200L, 1200L)]
		[TestCase(true, 2400L, null, 7L, 7L, 2400L, 2400L)]
		[TestCase(true, 2400L, "A", -1L, 7L, 2400L, 2400L)]
		[TestCase(true, 2400L, "A", 7L, 0L, 2400L, 2400L)]
		public void MasterResumePreservesOnlyUnpublishedOrMalformedActiveReceipts(bool active,
			long started, string zone, long completed, long required, long prior, long expected)
			=> ClassicAssert.AreEqual(expected, KingdomSemanticClockRules.MasterResumeDispatchTick(
				Active: active, StartedTick: started, BoundZoneId: zone, StartedMask: required,
				CompletedMask: completed, RequiredMask: required, LastSemanticTick: prior, NowTick: 5100L));

		[TestCase(-1L, 7L, 2400L)]
		[TestCase(3L, 7L, 2400L)]
		[TestCase(0L, 7L, 2400L)]
		[TestCase(7L, 7L, 5100L)]
		public void MasterResumeNeverPublishesUnstartedOrNegativeStepMasks(long started,
			long completed, long expected)
			=> ClassicAssert.AreEqual(expected, KingdomSemanticClockRules.MasterResumeDispatchTick(
				true, 2400L, "A", started, completed, 7L, 2400L, 5100L));

		[Test]
		public void PublishedMasterResumeCannotSpendPausedSemanticTimeOnNextWake()
		{
			long resumed = KingdomSemanticClockRules.MasterResumeDispatchTick(true, 2400, "A", 7, 7, 7, 2400, 5100);
			var state = KingdomSemanticClockRules.FromLastDispatchTick(resumed);
			ClassicAssert.IsFalse(KingdomSemanticClockRules.Decide(state, 5101, false).ShouldDispatch);
			ClassicAssert.IsTrue(KingdomSemanticClockRules.Decide(state, 6000, false).ShouldDispatch);
		}

		[Test]
		public void InvalidOrPreDayTicksDoNotCreateCadenceWork()
		{
			var empty = new KingdomSemanticClockState(-1L, -1L);
			ClassicAssert.IsFalse(KingdomSemanticClockRules.Decide(empty, -1L, false).ShouldDispatch);
			ClassicAssert.IsFalse(KingdomSemanticClockRules.Decide(empty, 1L, false).ShouldDispatch);
			ClassicAssert.AreEqual(0L, empty.LastBoundaryTick);
			ClassicAssert.AreEqual(0L, empty.LastDispatchTick);
		}
	}
}
#endif
