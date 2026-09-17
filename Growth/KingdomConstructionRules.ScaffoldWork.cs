namespace ThousandAndFirst
{
	public static partial class KingdomConstructionRules
	{
		internal static bool ScaffoldRemovalPhaseAdmitted(KingdomConstructionPhase Phase,
			KingdomPhysicalPhase Physical, bool Improvement, bool RemovalProved)
		{
			return Phase == KingdomConstructionPhase.ProjectionPending
				|| Phase == KingdomConstructionPhase.Working
				|| Phase == KingdomConstructionPhase.Outstanding && Improvement && RemovalProved
					&& Physical == KingdomPhysicalPhase.None;
		}

		/// <summary>Issue #212: the paid moot handover at f93450c5 quarantined with this
		/// sentence alone, and the log could not say which of eleven identity predicates had
		/// failed. The refusal now names the first failed predicate, in proof order.</summary>
		public const string ScaffoldRemovalIdentitySentence =
			"Scaffold-removal intent or successor identity changed";
		public const string ScaffoldRemovalCellPredicate = "recorded cell absent";
		public const string ScaffoldRemovalBlueprintPredicate = "successor blueprint absent";
		public const string ScaffoldRemovalRoutePredicate =
			"job route is neither this scaffold nor a pending improvement";
		public const string ScaffoldRemovalPhasePredicate =
			"job phase not admitted for removal proof";
		public const string ScaffoldRemovalOwnerPredicate = "job not owned by this settlement";
		public const string ScaffoldRemovalCurrentPredicate = "job not current in the registry";
		public const string ScaffoldRemovalIntentPredicate = "removal intent missing or foreign";
		public const string ScaffoldRemovalSuccessorPredicate =
			"successor is not the exact recorded output";
		public const string ScaffoldRemovalGatehousePredicate = "gatehouse projection incomplete";
		public const string ScaffoldRemovalOutputPredicate = "recorded output is not exactly live";
		public const string ScaffoldRemovalSameOutputPredicate =
			"live output is a different object";

		/// <summary>The identity predicates in the exact short-circuit order the proof walks.
		/// The backing array is private; callers see a read-only view, so no caller can add a
		/// predicate the proof never evaluated.</summary>
		private static readonly string[] ScaffoldRemovalIdentityPredicateNames =
		{
			ScaffoldRemovalCellPredicate, ScaffoldRemovalBlueprintPredicate,
			ScaffoldRemovalRoutePredicate, ScaffoldRemovalPhasePredicate,
			ScaffoldRemovalOwnerPredicate, ScaffoldRemovalCurrentPredicate,
			ScaffoldRemovalIntentPredicate, ScaffoldRemovalSuccessorPredicate,
			ScaffoldRemovalGatehousePredicate, ScaffoldRemovalOutputPredicate,
			ScaffoldRemovalSameOutputPredicate
		};

		public static System.Collections.Generic.IReadOnlyList<string>
			ScaffoldRemovalIdentityPredicates
		{
			get { return System.Array.AsReadOnly(ScaffoldRemovalIdentityPredicateNames); }
		}

		/// <summary>True only for a name the proof actually walks.</summary>
		public static bool IsScaffoldRemovalIdentityPredicate(string Predicate)
		{
			return !string.IsNullOrEmpty(Predicate)
				&& System.Array.IndexOf(ScaffoldRemovalIdentityPredicateNames, Predicate) >= 0;
		}

		/// <summary>Composes the refusal; an unnamed or invented predicate keeps the bare
		/// sentence so no caller can display a predicate the proof never evaluated.</summary>
		public static string ScaffoldRemovalIdentityRefusal(string Predicate)
		{
			if (!IsScaffoldRemovalIdentityPredicate(Predicate))
				return ScaffoldRemovalIdentitySentence + ".";
			return ScaffoldRemovalIdentitySentence + ": " + Predicate + ".";
		}

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

		/// <summary>Scaffold-specific destructive aftermath also retains direct-reference
		/// evidence, so changing the offered object's ID cannot masquerade as old-ID absence.</summary>
		public static KingdomExactRemovalAction ScaffoldRemovalAftermath(
			KingdomPhysicalLookupState State, bool ExactReference, bool ExactShape,
			bool OriginalReferenceValid)
		{
			if (State == KingdomPhysicalLookupState.Absent && OriginalReferenceValid)
				return KingdomExactRemovalAction.Quarantine;
			return GlobalRemovalAftermath(State, ExactReference, ExactShape);
		}
	}
}
