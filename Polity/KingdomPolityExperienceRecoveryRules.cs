namespace ThousandAndFirst
{
	internal enum KingdomPolityLeaseRecoveryAction : byte
	{
		Invalid = 0,
		ReleaseTerminal = 1,
		CancelUnpresented = 2,
		EnsureCurrentPlan = 3,
		EnsureProjected = 4,
		EnsureThenWithdrawLoaded = 5,
		EnsureThenCleanupLoaded = 6,
		EnsureThenRetainFrozen = 7,
		CleanupAbandonedLoaded = 8
	}

	/// <summary>Pure disposition and epoch proof for polity-owned shared-capacity recovery.</summary>
	internal static class KingdomPolityExperienceRecoveryRules
	{
		internal static KingdomPolityLeaseRecoveryAction Decide(
			KingdomPolityCohortPlan Cohort, string LoadedSettlementId, bool CurrentCauseAllowed)
		{
			if (Cohort == null) return KingdomPolityLeaseRecoveryAction.Invalid;
			bool projected = !string.IsNullOrEmpty(Cohort.ManifestationReceiptId);
			bool loaded = !string.IsNullOrEmpty(LoadedSettlementId) &&
				Cohort.SurfaceRef == LoadedSettlementId;
			if (Cohort.Phase == KingdomPolityCohortPhase.Abandoned)
				return projected && loaded
					? KingdomPolityLeaseRecoveryAction.CleanupAbandonedLoaded
					: KingdomPolityLeaseRecoveryAction.ReleaseTerminal;
			if (Cohort.Phase == KingdomPolityCohortPhase.Cancelled ||
				Cohort.Phase == KingdomPolityCohortPhase.Cleaned ||
				Cohort.Phase == KingdomPolityCohortPhase.Archived)
				return projected && Cohort.PresentationOptionKind ==
					KingdomExperienceOptionKind.None
					? KingdomPolityLeaseRecoveryAction.Invalid
					: KingdomPolityLeaseRecoveryAction.ReleaseTerminal;
			if (!projected)
				return Cohort.Phase != KingdomPolityCohortPhase.Planned
					? KingdomPolityLeaseRecoveryAction.Invalid
					: Cohort.PresentationOptionKind == KingdomExperienceOptionKind.None
						? KingdomPolityLeaseRecoveryAction.CancelUnpresented
					: CurrentCauseAllowed
						? KingdomPolityLeaseRecoveryAction.EnsureCurrentPlan
						: KingdomPolityLeaseRecoveryAction.CancelUnpresented;
			if (!loaded) return KingdomPolityLeaseRecoveryAction.EnsureThenRetainFrozen;
			if (Cohort.Phase == KingdomPolityCohortPhase.Concluded)
				return KingdomPolityLeaseRecoveryAction.EnsureThenCleanupLoaded;
			return CurrentCauseAllowed
				? KingdomPolityLeaseRecoveryAction.EnsureProjected
				: KingdomPolityLeaseRecoveryAction.EnsureThenWithdrawLoaded;
		}
	}
}
