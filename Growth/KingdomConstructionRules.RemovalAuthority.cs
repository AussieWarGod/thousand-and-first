namespace ThousandAndFirst
{
	/// <summary>Pure improvement-removal identity and callback laws.</summary>
	public static partial class KingdomConstructionRules
	{
		/// <summary>A renamed predecessor remains authoritative through either durable receipt.</summary>
		public static bool ImprovementPredecessorAuthority(string CandidateId,
			string ConstructionReceipt, string HandoverSourceId, string HandoverReceipt,
			string JobId, string SubjectId, string SourceId)
		{
			if (string.IsNullOrEmpty(JobId) || string.IsNullOrEmpty(SubjectId)) return false;
			return CandidateId == SubjectId
				|| !string.IsNullOrEmpty(SourceId) && CandidateId == SourceId
				|| ConstructionReceipt == JobId
				|| HandoverSourceId == SubjectId
				|| !string.IsNullOrEmpty(SourceId) && HandoverSourceId == SourceId
				|| HandoverReceipt == JobId;
		}

		/// <summary>Direct liveness defeats old-ID absence. Only an unchanged exact survivor retries.</summary>
		public static KingdomExactRemovalAction ImprovementRemovalAftermath(
			KingdomPhysicalLookupState State, bool DirectReferenceLive,
			bool AuthorityReferenceExact, bool IdentityMatches, bool GroundMatches)
		{
			bool exactShape = DirectReferenceLive && AuthorityReferenceExact
				&& IdentityMatches && GroundMatches;
			return GlobalRemovalAftermath(State, DirectReferenceLive, exactShape);
		}
	}
}
