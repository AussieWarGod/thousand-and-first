namespace ThousandAndFirst
{
	public static partial class KingdomConstructionRules
	{
		/// <summary>Proves the immutable paid duration and route of one durable scaffold.</summary>
		public static bool TryScaffoldWorkBill(KingdomConstructionJob Job,
			out long RequiredTicks)
		{
			RequiredTicks = 0L;
			if (!ValidJob(Job) || !FullyFundedExact(Job)
				|| Job.BuildTruthSchema != BuildTruthSchema
				|| (Job.Phase != KingdomConstructionPhase.ProjectionPending
					&& Job.Phase != KingdomConstructionPhase.Working
					&& Job.Phase != KingdomConstructionPhase.Outstanding)) return false;
			bool scaffold = (Job.Route == KingdomConstructionRoute.CommissionScaffold
				|| Job.Route == KingdomConstructionRoute.PlanScaffold)
				&& Job.Projection == KingdomConstructionProjection.Scaffold;
			bool improvement = Job.Route == KingdomConstructionRoute.Improvement
				&& Job.Projection == KingdomConstructionProjection.Improvement;
			if (!scaffold && !improvement) return false;
			RequiredTicks = Job.DueTick - Job.StartedTick;
			return RequiredTicks > 0L;
		}

		/// <summary>
		/// Freezes the full paid labour window when a receipt-backed scaffold first becomes
		/// physical. A late projection starts with the full bill; time before the frame existed
		/// is never treated as work.
		/// </summary>
		public static bool TryInitialScaffoldWork(KingdomConstructionJob Job,
			long ProjectionTick, out long RemainingTicks, out long LastWorkedTick)
		{
			RemainingTicks = 0L;
			LastWorkedTick = 0L;
			if (ProjectionTick <= 0L || ProjectionTick < (Job == null ? 0L : Job.StartedTick)
				|| !TryScaffoldWorkBill(Job, out RemainingTicks)) return false;
			LastWorkedTick = ProjectionTick;
			return true;
		}

		public static bool MatchesInitialDurableWork(KingdomConstructionJob Job,
			long ProjectionTick, long CompleteTick, long RemainingTicks, long LastWorkedTick)
		{
			return TryInitialScaffoldWork(Job, ProjectionTick, out long required, out long observed)
				&& CompleteTick == Job.DueTick && RemainingTicks == required
				&& LastWorkedTick == observed;
		}

		public static bool IsFreshDurableWorkSentinel(KingdomConstructionJob Job,
			long CompleteTick, long RemainingTicks, long LastWorkedTick)
		{
			return Job != null && RemainingTicks == 0L && LastWorkedTick == 0L
				&& (CompleteTick == 0L || CompleteTick == Job.DueTick);
		}

		public static bool ValidDurableScaffoldWork(KingdomConstructionJob Job,
			long CompleteTick, long RemainingTicks, long LastWorkedTick)
		{
			if (!TryScaffoldWorkBill(Job, out long required)) return false;
			bool complete = RemainingTicks == 0L && LastWorkedTick >= Job.StartedTick
				&& CompleteTick > Job.StartedTick
				&& CompleteTick <= LastWorkedTick;
			bool working = RemainingTicks > 0L && RemainingTicks <= required
				&& LastWorkedTick >= Job.StartedTick && CompleteTick == Job.DueTick;
			return complete || working;
		}
	}
}
