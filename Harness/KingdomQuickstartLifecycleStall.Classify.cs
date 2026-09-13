namespace ThousandAndFirst.Harness
{
	/// <summary>The pure classification half of KingdomQuickstartLifecycleStall, engine-free on
	/// purpose so it compiles (and is proved by value, not only by source pin) in both DevTests
	/// portable projects -- see DevTests/KingdomQuickstartLifecycleContractTests.cs and
	/// DevTests/KingdomQuickstartLifecycleStallClassifyTests.cs.</summary>
	internal static partial class KingdomQuickstartLifecycleStall
	{
		/// <summary>The settlement pass never reached this job at all.</summary>
		internal const string PassNeverRan = "pass-never-ran";

		/// <summary>Labour never touched it: the last worked tick is still absent or its start.</summary>
		internal const string NoLabourEver = "no-labour-ever";

		/// <summary>Labour was recorded, but the remaining work never came down.</summary>
		internal const string LabourStalled = "labour-stalled";

		/// <summary>Work is coming down; there simply were not enough turns.</summary>
		internal const string InsufficientTurns = "insufficient-turns";

		/// <summary>Labour finished (remaining ticks spent) but the plot's own stage never
		/// advanced to match -- native run 23 (3e3ff75), #163: a living occupant on the
		/// footprint refuses the apply every pass forever, silently, once labour is done.
		/// Unnamed blocker; see <see cref="ApplyBlockedOccupant"/> when one is identified.</summary>
		internal const string StageNotApplied = "stage-not-applied";

		/// <summary>Same as <see cref="StageNotApplied"/>, with a living occupant identified
		/// standing on the plot's own footprint (Growth/KingdomArchitectureStamper.
		/// Verification.cs:149-151 CanInsert refuses on IsCreature/IsPlayer).</summary>
		internal const string ApplyBlockedOccupant = "apply-blocked-occupant";

		/// <summary>
		/// The six cases, in the order that makes each answer the previous one's absence: a pass
		/// that never reached this job explains everything downstream of it; then labour that
		/// never happened; then labour that happened without the work coming down; then labour
		/// that finished without the job's own physical stage advancing to match -- native run 23
		/// (3e3ff75), #163: a living occupant on a plot's footprint refuses the apply forever,
		/// silently, once labour is spent -- named by occupant when one is found; and only then
		/// the ordinary case of a job that needed more turns.
		///
		/// <para>StageApplied/StageTarget carry the plot lane's own KingdomPlotRules.PlotStage
		/// reading (0 for both on a scaffold-backed job, where Read() never populates them, so
		/// that half of the check is structurally inert there); PhysicalPhase is
		/// Job.PhysicalPhase, read for both lanes, and catches the scaffold-backed case the same
		/// review that named this fix pointed out: a scaffold job whose labour is spent but whose
		/// physical callback chain never left KingdomPhysicalPhase.None is the same "finished
		/// labour, no result" shape as a stuck plot stage, just on the other lane.</para>
		/// </summary>
		internal static string Classify(long LastSemanticTick, long StartedTick, long LastWorkedTick,
			long RemainingTicks, long AuthoredTicks, int StageApplied, int StageTarget,
			int OccupantCount, KingdomPhysicalPhase PhysicalPhase)
		{
			if (LastSemanticTick < StartedTick) return PassNeverRan;
			if (LastWorkedTick <= 0L || LastWorkedTick == StartedTick) return NoLabourEver;
			if (AuthoredTicks > 0L && RemainingTicks >= AuthoredTicks) return LabourStalled;
			if (RemainingTicks < 0L) return LabourStalled;
			if (RemainingTicks <= 0L
				&& (StageApplied < StageTarget || PhysicalPhase == KingdomPhysicalPhase.None))
				return OccupantCount > 0 ? ApplyBlockedOccupant : StageNotApplied;
			return InsufficientTurns;
		}
	}
}
