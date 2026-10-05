namespace ThousandAndFirst
{
	/// <summary>The two retired improvement-handover defects a stuck save can carry (#283).
	/// <see cref="FounderMarks"/> is signature A: the authored lane dropped the yielding mark.
	/// <see cref="LandedScaffold"/> is signature B: HandOver refused the nulled scaffold
	/// reference of a landed scaffold.</summary>
	public enum KingdomRetiredHandoverDefect : byte
	{
		None = 0,
		FounderMarks = 1,
		LandedScaffold = 2
	}

	public static partial class KingdomConstructionRules
	{
		/// <summary>The handover's founder-marks refusal
		/// (<c>Growth/KingdomUpgrade.24.HandoverContents.cs</c>). Signature A's text.</summary>
		public const string HandoverMarksFailure =
			"Founder marks did not settle exactly on the successor.";

		/// <summary>The handover's endpoint refusal (<c>Growth/KingdomUpgrade.20.HandOver.cs</c>).
		/// Signature B's text.</summary>
		public const string HandoverEndpointsFailure =
			"The paid improvement job no longer matches its exact physical endpoints.";

		/// <summary>The retired defect a quarantine failure text can name. Text only narrows the
		/// candidates; the readmission gate is structural and never admits on text alone.
		/// </summary>
		public static KingdomRetiredHandoverDefect RetiredHandoverDefectFor(string Failure)
		{
			return Failure == HandoverMarksFailure ? KingdomRetiredHandoverDefect.FounderMarks
				: Failure == HandoverEndpointsFailure ? KingdomRetiredHandoverDefect.LandedScaffold
				: KingdomRetiredHandoverDefect.None;
		}

		/// <summary>"A" or "B", the letters the design names the signatures by; null for None.
		/// </summary>
		public static string RetiredHandoverDefectLetter(KingdomRetiredHandoverDefect Defect)
		{
			return Defect == KingdomRetiredHandoverDefect.FounderMarks ? "A"
				: Defect == KingdomRetiredHandoverDefect.LandedScaffold ? "B" : null;
		}

		/// <summary>The exact prefix a readmitted job's failure carries ahead of its retired
		/// cause. Only <c>KingdomConstruction.Readmit</c> writes it.</summary>
		public static string ReadmissionPrefix(KingdomRetiredHandoverDefect Defect)
		{
			string letter = RetiredHandoverDefectLetter(Defect);
			return letter == null ? null
				: "readmitted after retired handover defect #283 (" + letter + "): ";
		}

		/// <summary>The failure a readmission of <paramref name="Job"/> must publish: the exact
		/// prefix plus the retired cause, which must be exactly the named signature's text.
		/// </summary>
		public static bool TryReadmissionFailure(KingdomConstructionJob Job,
			KingdomRetiredHandoverDefect Defect, out string Failure)
		{
			Failure = null;
			if (Job == null || Defect == KingdomRetiredHandoverDefect.None
				|| RetiredHandoverDefectFor(Job.Failure) != Defect) return false;
			string candidate = ReadmissionPrefix(Defect) + Job.Failure;
			if (candidate.Length > MaxFailureChars) return false;
			Failure = candidate;
			return true;
		}

		/// <summary>
		/// The single lawful exit from InspectionRequired other than cancellation: an improvement
		/// job quarantined by one of the two retired #283 defects, moved to Outstanding with every
		/// identity, physical, funding and timing field unchanged and its failure rewritten to the
		/// exact readmission prefix plus the retired cause. Pure. Everything else out of
		/// InspectionRequired stays refused.
		/// </summary>
		public static bool IsRetiredDefectReadmission(KingdomConstructionJob Current,
			KingdomConstructionJob Next)
		{
			if (Current == null || Next == null
				|| Current.Phase != KingdomConstructionPhase.InspectionRequired
				|| Next.Phase != KingdomConstructionPhase.Outstanding
				|| Current.Route != KingdomConstructionRoute.Improvement
				|| Next.Route != KingdomConstructionRoute.Improvement
				|| Current.PhysicalPhase != KingdomPhysicalPhase.None
				|| Next.PhysicalPhase != KingdomPhysicalPhase.None
				|| string.IsNullOrEmpty(Current.SubjectId) || string.IsNullOrEmpty(Current.OutputId)
				|| !SameReadmissionIdentity(Current, Next)) return false;
			string failure;
			return TryReadmissionFailure(Current, RetiredHandoverDefectFor(Current.Failure),
				out failure) && Next.Failure == failure;
		}

		private static bool SameReadmissionIdentity(KingdomConstructionJob A, KingdomConstructionJob B)
		{
			return A.Id == B.Id && A.OwnerKey == B.OwnerKey && A.ZoneId == B.ZoneId
				&& A.Projection == B.Projection && A.X == B.X && A.Y == B.Y
				&& A.SubjectId == B.SubjectId && A.SourceId == B.SourceId
				&& A.OutputId == B.OutputId && A.TargetKey == B.TargetKey && A.Payload == B.Payload
				&& A.PhysicalIndex == B.PhysicalIndex && A.PhysicalAmount == B.PhysicalAmount
				&& A.PhysicalSpilled == B.PhysicalSpilled && A.PhysicalItemId == B.PhysicalItemId
				&& A.PhysicalDestinationId == B.PhysicalDestinationId
				&& A.PhysicalReceipt == B.PhysicalReceipt && A.InputReceipt == B.InputReceipt
				&& A.InputReceiptHash == B.InputReceiptHash
				&& A.BuildTruthSchema == B.BuildTruthSchema && A.BuildHasPlot == B.BuildHasPlot
				&& A.BuildFrontier == B.BuildFrontier && A.BuildDefence == B.BuildDefence
				&& A.CreatedTick == B.CreatedTick && A.StartedTick == B.StartedTick
				&& A.DueTick == B.DueTick && A.Compacted == B.Compacted
				&& A.CompactHash == B.CompactHash && SameClaims(A.Claims, B.Claims)
				&& SameOutbox(A.Outbox, B.Outbox);
		}

		private static bool SameClaims(KingdomConstructionClaims A, KingdomConstructionClaims B)
		{
			if (A == null || B == null) return A == null && B == null;
			return A.WaterRequested == B.WaterRequested && A.WaterSpent == B.WaterSpent
				&& A.WaterOutstanding == B.WaterOutstanding && A.WaterLost == B.WaterLost
				&& A.Exact == B.Exact && A.MaterialRequested == B.MaterialRequested
				&& A.MaterialSpent == B.MaterialSpent
				&& A.MaterialOutstanding == B.MaterialOutstanding
				&& A.MaterialLost == B.MaterialLost;
		}

		private static bool SameOutbox(KingdomConstructionOutbox A, KingdomConstructionOutbox B)
		{
			if (A == null || B == null) return A == null && B == null;
			return A.EventId == B.EventId && A.Mode == B.Mode && A.Chronicle == B.Chronicle
				&& A.ChronicleState == B.ChronicleState && A.Ledger == B.Ledger
				&& A.LedgerState == B.LedgerState && A.LedgerBeforeCount == B.LedgerBeforeCount
				&& A.LedgerBeforeHash == B.LedgerBeforeHash
				&& A.LedgerAfterCount == B.LedgerAfterCount
				&& A.LedgerAfterHash == B.LedgerAfterHash && A.Message == B.Message
				&& A.MessageState == B.MessageState && A.Deed == B.Deed
				&& A.DeedState == B.DeedState;
		}
	}
}
