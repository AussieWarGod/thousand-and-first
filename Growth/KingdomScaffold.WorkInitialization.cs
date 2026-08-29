namespace XRL.World.Parts
{
	public partial class r_KingdomScaffold
	{
		/// <summary>
		/// Initializes a new or interrupted receipt-backed frame exactly once. A partly written
		/// labour window is damage, never permission to infer progress from the calendar.
		/// </summary>
		public bool TryInitializeDurableWork(ThousandAndFirst.KingdomConstructionJob Job,
			long ProjectionTick, out string Failure)
		{
			Failure = null;
			if (!ThousandAndFirst.KingdomConstructionRules.TryScaffoldWorkBill(Job,
				out long required))
			{
				Failure = "The scaffold receipt has no exact positive paid labour window.";
				return false;
			}
			if (RemainingTicks == 0L && LastWorkedTick == 0L)
			{
				if (!ThousandAndFirst.KingdomConstructionRules.IsFreshDurableWorkSentinel(
					Job, CompleteTick, RemainingTicks, LastWorkedTick))
				{
					Failure = "The scaffold labour window has a torn completion sentinel.";
					return false;
				}
				if (!ThousandAndFirst.KingdomConstructionRules.TryInitialScaffoldWork(Job,
					ProjectionTick, out required, out long observed))
				{
					Failure = "The scaffold could not anchor its paid work to physical projection.";
					return false;
				}
				CompleteTick = Job.DueTick;
				RemainingTicks = required;
				LastWorkedTick = observed;
			}
			return TryValidateDurableWork(Job, out Failure);
		}

		public bool TryValidateDurableWork(ThousandAndFirst.KingdomConstructionJob Job,
			out string Failure)
		{
			Failure = null;
			if (!ThousandAndFirst.KingdomConstructionRules.TryScaffoldWorkBill(Job, out _))
			{
				Failure = "The scaffold receipt has no exact positive paid labour window.";
				return false;
			}
			if (ThousandAndFirst.KingdomConstructionRules.ValidDurableScaffoldWork(Job,
				CompleteTick, RemainingTicks, LastWorkedTick)) return true;
			Failure = "The scaffold labour window is partial, contradictory, or over its paid bill.";
			return false;
		}

		public bool MatchesInitialDurableWork(ThousandAndFirst.KingdomConstructionJob Job,
			long ProjectionTick)
		{
			return ThousandAndFirst.KingdomConstructionRules.MatchesInitialDurableWork(Job,
				ProjectionTick, CompleteTick, RemainingTicks, LastWorkedTick);
		}

		/// <summary>Reproves an uncommitted physical frame before any retry may publish Working.</summary>
		public bool TryValidateInitialDurableWork(ThousandAndFirst.KingdomConstructionJob Job,
			long ProjectionTick, out string Failure)
		{
			Failure = null;
			if (ParentObject != null && ParentObject.GetIntProperty(FinalPendingProperty) == 0
				&& MatchesInitialDurableWork(Job, ProjectionTick)) return true;
			Failure = "The interrupted scaffold does not retain its exact initial paid labour anchor.";
			return false;
		}
	}
}
