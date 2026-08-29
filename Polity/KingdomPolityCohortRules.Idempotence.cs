namespace ThousandAndFirst
{
	public static partial class KingdomPolityCohortRules
	{
		/// <summary>Compares caller-owned plan inputs without re-resolving a newer profile.</summary>
		private static bool ExactRequest(KingdomPolityCohortPlan Existing,
			KingdomPolityCohortPlanRequest Request)
		{
			if (Existing == null || Request == null || Existing.CohortId != Request.CohortId ||
				Existing.Purpose != Request.Purpose || Existing.SourceRef != Request.SourceRef ||
				Existing.PolityId != Request.PolityId || Existing.SurfaceRef != Request.SurfaceRef ||
				Existing.ScaleBudget != Request.MemberCount ||
				Existing.EventStreamId != Request.EventStreamId ||
				Existing.RulesVersion != Request.RulesVersion ||
				Existing.EventOrdinal != Request.EventOrdinal ||
				Request.PresentationAuthority == null ||
				Existing.PresentationOptionKind != Request.PresentationAuthority.OptionKind ||
				Existing.PresentationEnableEpoch != Request.PresentationAuthority.EnableEpoch ||
				Existing.PresentationReservedTick != Request.PresentationAuthority.ReservedTick ||
				Existing.ResolvedMembers.Count != Request.MemberCount) return false;
			bool named = !string.IsNullOrEmpty(Request.NamedFigureId);
			if (Existing.NamedRepresentativeAllowance != (named ? 1 : 0)) return false;
			for (int i = 0; i < Existing.ResolvedMembers.Count; i++)
			{
				if (!TryParseSignature(Existing.ResolvedMembers[i].SignatureKey,
					out string _, out string _, out string figureId)) return false;
				if (i == 0 && named)
				{
					if (figureId != Request.NamedFigureId) return false;
				}
				else if (!string.IsNullOrEmpty(figureId)) return false;
			}
			return true;
		}
	}
}
