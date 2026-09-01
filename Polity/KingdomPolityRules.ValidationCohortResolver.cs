namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		private static bool ValidCohortResolver(KingdomPolityLedger L,
			KingdomPolityCohortPlan C)
		{
			if (C == null || (C.RulesVersion != 1 && C.RulesVersion !=
				KingdomPolityLoadoutCatalogue.PriorResolverVersion &&
				C.RulesVersion != KingdomPolityNpcRules.RulesVersion)) return false;
			KingdomPolityProfileRevision profile = KingdomPolityAuthority.Profile(L,
				C.ProfileId, C.ProfileRevision);
			if (profile == null || profile.PolityId != C.PolityId) return false;
			// Legacy plans (resolver schemas 1 and 2) predate pinned regenerable member
			// signatures; only current-resolver plans must regenerate byte-exactly.
			if (C.RulesVersion != KingdomPolityNpcRules.RulesVersion) return true;
			for (int i = 0; i < C.ResolvedMembers.Count; i++)
			{
				KingdomPolityCohortMember member = C.ResolvedMembers[i];
				if (!KingdomPolityCohortRules.TryParseSignature(member.SignatureKey,
					out string role, out string resolver, out string figureId) ||
					!KingdomPolityNpcRules.TryResolvePinned(profile, role, i, C.RulesVersion,
						C.MinimumLevel, C.MaximumLevel, out KingdomPolityNpcSpec spec,
						out string _) || spec.ResolverDigest != resolver ||
					member.LoadoutKey != resolver || member.BlueprintKey != spec.BodyBlueprint ||
					!Contains(C.RoleSlots, role)) return false;
				if (!string.IsNullOrEmpty(figureId))
				{
					KingdomPolityNamedFigureRecord figure = KingdomPolityAuthority.Figure(L, figureId);
					if (i != 0 || figure == null || figure.PolityId != C.PolityId ||
						figure.Phase != KingdomPolityFigurePhase.Active || figure.ResidentId != 0 ||
						figure.RoleKey != role) return false;
				}
			}
			return true;
		}
	}
}
