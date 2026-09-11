#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// VALUE tests for KingdomQuickstartLifecycleStall.Classify -- pure, engine-free, so the
	/// classification order (native run 23, 3e3ff75, #163) is proved by value rather than only by
	/// reading source text. See DevTests/KingdomQuickstartLifecycleContractTests.cs for the
	/// source-only pins over how KingdomQuickstartLifecycleStall.Describe/DetailMessage actually
	/// call this class.
	/// </summary>
	[TestFixture]
	public sealed class KingdomQuickstartLifecycleStallClassifyTests
	{
		// Run 23's own reading: lastSemanticTick=306000, startedTick=299125, lastWorkedTick=301200,
		// remaining=0, authored=700 -- labour fully spent, but the plot's stage (0=Staked) never
		// caught up to its target (4=Done) because a living occupant refused every apply.
		private const long LastSemanticTick = 306000L;
		private const long StartedTick = 299125L;
		private const long LastWorkedTick = 301200L;
		private const long Remaining = 0L;
		private const long Authored = 700L;

		[Test]
		public void AnOccupantOnTheFootprintClassifiesAsApplyBlockedOccupant()
		{
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleStall.ApplyBlockedOccupant,
				KingdomQuickstartLifecycleStall.Classify(LastSemanticTick, StartedTick,
					LastWorkedTick, Remaining, Authored, StageApplied: 0, StageTarget: 4,
					OccupantCount: 1));
		}

		[Test]
		public void TheSameStalledStageWithNoOccupantFoundIsTheUnnamedCase()
		{
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleStall.StageNotApplied,
				KingdomQuickstartLifecycleStall.Classify(LastSemanticTick, StartedTick,
					LastWorkedTick, Remaining, Authored, StageApplied: 0, StageTarget: 4,
					OccupantCount: 0));
		}

		[Test]
		public void AStageAlreadyAtItsTargetFallsThroughToInsufficientTurnsUnchanged()
		{
			// Mutation evidence: the ONLY thing distinguishing this from the two cases above is
			// StageApplied == StageTarget: the exact same tick/occupant facts must therefore still
			// read as the ordinary case, proving the new branch does not fire on an already-caught-
			// up stage regardless of occupancy.
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleStall.InsufficientTurns,
				KingdomQuickstartLifecycleStall.Classify(LastSemanticTick, StartedTick,
					LastWorkedTick, RemainingTicks: 50L, Authored, StageApplied: 4, StageTarget: 4,
					OccupantCount: 1));
		}

		[Test]
		public void TheNewBranchNeverShadowsAnEarlierClassification()
		{
			// pass-never-ran still wins even with remaining<=0 and an unapplied stage.
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleStall.PassNeverRan,
				KingdomQuickstartLifecycleStall.Classify(LastSemanticTick: 100L, StartedTick,
					LastWorkedTick, Remaining, Authored, StageApplied: 0, StageTarget: 4,
					OccupantCount: 1));
			// no-labour-ever still wins even with remaining<=0 and an unapplied stage.
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleStall.NoLabourEver,
				KingdomQuickstartLifecycleStall.Classify(LastSemanticTick, StartedTick,
					LastWorkedTick: 0L, Remaining, Authored, StageApplied: 0, StageTarget: 4,
					OccupantCount: 1));
			// labour-stalled (remaining negative) still wins over the new branch.
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleStall.LabourStalled,
				KingdomQuickstartLifecycleStall.Classify(LastSemanticTick, StartedTick,
					LastWorkedTick, RemainingTicks: -1L, Authored, StageApplied: 0, StageTarget: 4,
					OccupantCount: 1));
		}

		[Test]
		public void AScaffoldBackedJobsUnusedStageFieldsNeverTriggerTheNewBranch()
		{
			// Scaffold-backed jobs pass StageApplied == StageTarget == 0 (Read() never populates
			// them for that lane), so the branch is structurally inert there -- confirmed by
			// value, not just by reading that Read() only sets them in the PlotWorks arm.
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleStall.InsufficientTurns,
				KingdomQuickstartLifecycleStall.Classify(LastSemanticTick, StartedTick,
					LastWorkedTick, Remaining, Authored, StageApplied: 0, StageTarget: 0,
					OccupantCount: 0));
		}
	}
}
#endif
