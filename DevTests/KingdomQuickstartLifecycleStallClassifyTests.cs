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

		// The plot lane's own reading in run 23 also carries PhysicalPhase.None (the design never
		// got as far as its output-intent callback), so every plot-lane case below states it
		// explicitly rather than leaving it implicit.
		private const KingdomPhysicalPhase Unbuilt = KingdomPhysicalPhase.None;

		[Test]
		public void AnOccupantOnTheFootprintClassifiesAsApplyBlockedOccupant()
		{
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleStall.ApplyBlockedOccupant,
				KingdomQuickstartLifecycleStall.Classify(LastSemanticTick, StartedTick,
					LastWorkedTick, Remaining, Authored, StageApplied: 0, StageTarget: 4,
					OccupantCount: 1, PhysicalPhase: Unbuilt));
		}

		[Test]
		public void TheSameStalledStageWithNoOccupantFoundIsTheUnnamedCase()
		{
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleStall.StageNotApplied,
				KingdomQuickstartLifecycleStall.Classify(LastSemanticTick, StartedTick,
					LastWorkedTick, Remaining, Authored, StageApplied: 0, StageTarget: 4,
					OccupantCount: 0, PhysicalPhase: Unbuilt));
		}

		[Test]
		public void AStageAlreadyAtItsTargetFallsThroughToInsufficientTurnsUnchanged()
		{
			// Mutation evidence: the ONLY thing distinguishing this from the two cases above is
			// StageApplied == StageTarget: the exact same tick/occupant facts must therefore still
			// read as the ordinary case, proving the new branch does not fire on an already-caught-
			// up stage regardless of occupancy -- PhysicalPhase alone must not override that a
			// completed stage is genuinely done, so it is passed as a built value here too.
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleStall.InsufficientTurns,
				KingdomQuickstartLifecycleStall.Classify(LastSemanticTick, StartedTick,
					LastWorkedTick, RemainingTicks: 50L, Authored, StageApplied: 4, StageTarget: 4,
					OccupantCount: 1, PhysicalPhase: KingdomPhysicalPhase.Settled));
		}

		[Test]
		public void TheNewBranchNeverShadowsAnEarlierClassification()
		{
			// pass-never-ran still wins even with remaining<=0 and an unapplied stage.
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleStall.PassNeverRan,
				KingdomQuickstartLifecycleStall.Classify(LastSemanticTick: 100L, StartedTick,
					LastWorkedTick, Remaining, Authored, StageApplied: 0, StageTarget: 4,
					OccupantCount: 1, PhysicalPhase: Unbuilt));
			// no-labour-ever still wins even with remaining<=0 and an unapplied stage.
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleStall.NoLabourEver,
				KingdomQuickstartLifecycleStall.Classify(LastSemanticTick, StartedTick,
					LastWorkedTick: 0L, Remaining, Authored, StageApplied: 0, StageTarget: 4,
					OccupantCount: 1, PhysicalPhase: Unbuilt));
			// labour-stalled (remaining negative) still wins over the new branch.
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleStall.LabourStalled,
				KingdomQuickstartLifecycleStall.Classify(LastSemanticTick, StartedTick,
					LastWorkedTick, RemainingTicks: -1L, Authored, StageApplied: 0, StageTarget: 4,
					OccupantCount: 1, PhysicalPhase: Unbuilt));
		}

		[Test]
		public void AScaffoldJobWithStageFieldsBothZeroButABuiltPhysicalPhaseIsInsufficientTurns()
		{
			// Scaffold-backed jobs always pass StageApplied == StageTarget == 0 (Read() never
			// populates them for that lane); when the physical callback chain genuinely progressed
			// past None, that half of the OR is false too, so the ordinary case still applies.
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleStall.InsufficientTurns,
				KingdomQuickstartLifecycleStall.Classify(LastSemanticTick, StartedTick,
					LastWorkedTick, Remaining, Authored, StageApplied: 0, StageTarget: 0,
					OccupantCount: 0, PhysicalPhase: KingdomPhysicalPhase.Settled));
		}

		[Test]
		public void AScaffoldJobStuckAtPhysicalPhaseNoneReachesStageNotApplied()
		{
			// The review's own advisory on 090a188: PhysicalPhase is read and printed already but
			// was never fed to Classify, so a scaffold job with labour spent and its physical
			// callback chain stuck at None fell through to insufficient-turns -- the exact
			// "none of the other three" defect run 23 named, one lane over. OccupantCount is 0
			// here on purpose: the scaffold lane has no occupant guard in production
			// (Growth/KingdomArchitectureStamper.Verification.cs's CanInsert is reached only from
			// the plot stage walk), so apply-blocked-occupant cannot arise for it.
			ClassicAssert.AreEqual(KingdomQuickstartLifecycleStall.StageNotApplied,
				KingdomQuickstartLifecycleStall.Classify(LastSemanticTick, StartedTick,
					LastWorkedTick, Remaining, Authored, StageApplied: 0, StageTarget: 0,
					OccupantCount: 0, PhysicalPhase: Unbuilt));
		}
	}
}
#endif
