namespace ThousandAndFirst
{
	/// <summary>Single active polity reconciliation entered by load and current-city cadence.</summary>
	public static class KingdomPolityActiveRuntime
	{
		public static bool TryReconcile(KingdomSystem System, long Tick, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded || System.PolityLedger == null || Tick < 0L)
				return true;
			if (!KingdomMaster.NewWorkAllowed(System))
				return TryReconcileCommittedCapacity(System, Tick, out Failure);
			// Capacity reconciliation is committed recovery. It must precede topology-dependent
			// profile/resident/office work so frozen projections remain accounted while paused.
			if (!KingdomPolityPresentationRuntime.TryObserve(System, Tick,
					out bool enabled, out Failure) ||
				!KingdomPolityExperienceRuntime.TryRecover(System, Tick, enabled, out Failure) ||
				!KingdomPolityCorrespondenceRuntime.TryRecoverTradeReceipts(System, out Failure) ||
				!KingdomPolityCorrespondenceRuntime.TryRecoverEnvoyDeaths(System, out Failure) ||
				!KingdomPolityConsentedEscrowRuntime.TryRecover(System, Tick, out Failure) ||
				!KingdomPolityProfileRuntime.TryReconcile(System, Tick, out Failure) ||
				!KingdomPolityResidentRuntime.TryReconcile(System, Tick, out Failure) ||
				!KingdomPolityPromotionRuntime.TryReconcile(System, out Failure) ||
				!KingdomPolityVisitRuntime.TryReconcile(System, Tick, out Failure) ||
				!KingdomPolitySchedulerRuntime.TryReconcile(System, Tick, enabled, out Failure))
				return false;
			return true;
		}

		/// <summary>Reconciles only already-committed shared capacity; never emits new work.</summary>
		public static bool TryReconcileCommittedCapacity(KingdomSystem System, long Tick,
			out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded || System.PolityLedger == null || Tick < 0L)
				return true;
			if (!KingdomPolityExperienceRuntime.TryRecover(System, Tick, false, out Failure))
				return false;
			return KingdomPolityCorrespondenceRuntime.TryRecoverTradeReceipts(System, out Failure) &&
				KingdomPolityCorrespondenceRuntime.TryRecoverEnvoyDeaths(System, out Failure) &&
				KingdomPolityConsentedEscrowRuntime.TryRecover(System, Tick, out Failure);
		}

		public static void WitnessCohortDeath(KingdomSystem System, string CohortId, long Tick)
		{
			KingdomPolitySchedulerRuntime.WitnessDeath(System, CohortId, Tick);
		}
	}
}
