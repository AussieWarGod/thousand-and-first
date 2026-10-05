namespace ThousandAndFirst
{
	public static partial class KingdomUpgradeRules
	{
		/// <summary>
		/// Everything the retired-defect readmission reads off the engine for one quarantined
		/// improvement job, gathered by <c>KingdomUpgrade.TryReadmitRetiredHandoverDefect</c>
		/// before any effect. Field names follow the gate in the #283 design: the common gate,
		/// the once-only marker, signature A's structure and signature B's structure.
		/// </summary>
		public struct RetiredHandoverObservation
		{
			public bool Owned;
			public bool Current;
			public bool PredecessorReceipt;
			public bool SuccessorReceipt;
			public bool SuccessorPending;
			public bool SuccessorExact;
			public bool Working;
			public bool EffectsDone;
			public bool PredecessorExact;
			public bool ScaffoldLanded;
			public bool UpgradeQuarantined;
			public bool LayoutFault;
			public bool ContentCustody;
			public bool AlreadyReadmitted;
			public FounderMarks PredecessorMarks;
			public FounderMarks SuccessorMarks;
			public bool HasInventory;
			public bool HasLiquid;
			public bool SameWear;
			public int UpgradePhase;
			public bool SuccessorLayoutComplete;
			public bool ScaffoldReferenceLive;
		}

		/// <summary>
		/// Which retired #283 defect, if any, a quarantined improvement job carries. Structural:
		/// the job's failure text only narrows the candidates, and every conjunct of the common
		/// gate plus the named signature's own structure must hold. A job readmitted once never
		/// qualifies again.
		/// <para>
		/// Signature A (the dropped yielding mark): the predecessor yields and the successor does
		/// not, every other founder mark and the wear state settled, the predecessor's authored
		/// upgrade reached phase 5 and the successor's layout verifies complete. Signature B (the
		/// nulled scaffold reference): the scaffold reference is not live. Every HandOver endpoint
		/// predicate other than the phase test is already in the common gate, evaluated with the
		/// durable landed-scaffold identity.
		/// </para>
		/// </summary>
		public static KingdomRetiredHandoverDefect ClassifyRetiredHandoverDefect(
			KingdomConstructionJob Job, string PredecessorId, int PredecessorX, int PredecessorY,
			string SuccessorId, string SuccessorKey, RetiredHandoverObservation Observed)
		{
			if (Job == null || Observed.AlreadyReadmitted
				|| Job.Route != KingdomConstructionRoute.Improvement
				|| Job.Phase != KingdomConstructionPhase.InspectionRequired
				|| Job.PhysicalPhase != KingdomPhysicalPhase.None
				|| string.IsNullOrEmpty(PredecessorId) || Job.SubjectId != PredecessorId
				|| Job.SourceId != PredecessorId || string.IsNullOrEmpty(SuccessorId)
				|| Job.OutputId != SuccessorId || string.IsNullOrEmpty(SuccessorKey)
				|| Job.TargetKey != SuccessorKey || Job.X != PredecessorX || Job.Y != PredecessorY)
				return KingdomRetiredHandoverDefect.None;
			if (!Observed.Owned || !Observed.Current || !Observed.PredecessorReceipt
				|| !Observed.SuccessorReceipt || !Observed.SuccessorPending
				|| !Observed.SuccessorExact || !Observed.Working || Observed.EffectsDone
				|| !Observed.PredecessorExact || !Observed.ScaffoldLanded
				|| Observed.UpgradeQuarantined || Observed.LayoutFault || !Observed.ContentCustody)
				return KingdomRetiredHandoverDefect.None;
			switch (KingdomConstructionRules.RetiredHandoverDefectFor(Job.Failure))
			{
				case KingdomRetiredHandoverDefect.FounderMarks:
					return Observed.PredecessorMarks.Yielding && !Observed.SuccessorMarks.Yielding
						&& FounderMarksSettledExceptYielding(Observed.PredecessorMarks,
							Observed.SuccessorMarks, Observed.HasInventory, Observed.HasLiquid)
						&& Observed.SameWear && Observed.UpgradePhase == 5
						&& Observed.SuccessorLayoutComplete
						? KingdomRetiredHandoverDefect.FounderMarks
						: KingdomRetiredHandoverDefect.None;
				case KingdomRetiredHandoverDefect.LandedScaffold:
					return !Observed.ScaffoldReferenceLive
						? KingdomRetiredHandoverDefect.LandedScaffold
						: KingdomRetiredHandoverDefect.None;
				default:
					return KingdomRetiredHandoverDefect.None;
			}
		}
	}
}
