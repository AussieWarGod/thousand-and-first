using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityEndpointRuntime
	{
		/// <summary>
		/// Proves the exact loaded physical aftermath before a generic lifecycle conclusion.
		/// Death replay must run first so an owned death cannot be relabelled withdrawal.
		/// </summary>
		internal static bool TryProveMaterializedLifecycleAfterDeathReplay(KingdomSystem System,
			string CohortId, out string Failure)
		{
			Failure = null;
			if (!TryAdmit(System, CohortId, out Zone zone, out KingdomPolityLedger ledger,
				out KingdomPolityCohortPlan cohort, out Failure)) return false;
			if (cohort.Phase != KingdomPolityCohortPhase.Materialized)
				return FailPhysical(
					"generic polity lifecycle conclusion lacks a materialized cohort", out Failure);
			KingdomPolityProjectionReceipt receipt = KingdomPolityAuthority.Projection(ledger,
				cohort.ManifestationReceiptId);
			if (!ExactReceipt(cohort, receipt, zone, out Failure)) return false;
			if (receipt.Phase != KingdomPolityProjectionPhase.Committed)
				return FailPhysical(
					"generic polity lifecycle conclusion lacks committed physical authority",
					out Failure);
			return TryObserveForCleanup(zone, ledger, cohort, receipt,
				out GameObject[] observed, out Failure) &&
				TryProveFrozenCohort(ledger, zone, ledger.RealmId, cohort, receipt, observed,
					AllowRemovedGear: true, out FrozenCustodyPlan[] _, out Failure);
		}

		private static bool TryObserveForCleanup(Zone Zone, KingdomPolityLedger Ledger,
			KingdomPolityCohortPlan Cohort, KingdomPolityProjectionReceipt Receipt,
			out GameObject[] Observed, out string Failure)
		{
			if (!TryObserve(Zone, Ledger.RealmId, Cohort, Receipt, out Observed, out Failure))
				return false;
			for (int i = 0; i < Observed.Length; i++)
			{
				string bodyId = KingdomPolityCohortRules.PreparedObjectId(Cohort, i);
				bool present = GameObject.Validate(Observed[i]);
				bool witnessed = HasBodyRemovalWitness(Zone, Ledger.RealmId, Cohort.CohortId,
					Receipt.ProjectionId, bodyId, i);
				if (!TryFindResidentObject(bodyId, out GameObject resident, out Failure)) return false;
				bool exactResident = GameObject.Validate(resident) &&
					ReferenceEquals(resident, Observed[i]);
				if (!KingdomPolityPhysicalCustodyRules.RemovalCanContinue(present, witnessed,
					exactResident))
				{
					return FailPhysical(present ?
						"removed cohort body is still physically present or globally ambiguous" :
						"cohort body is absent without an exact death or cleanup witness", out Failure);
				}
			}
			return true;
		}

		private static bool TryClearCohortRemovalWitnesses(Zone Zone, KingdomPolityLedger Ledger,
			KingdomPolityCohortPlan Cohort, KingdomPolityProjectionReceipt Receipt,
			out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Cohort.ResolvedMembers.Count; i++)
			{
				if (!TryResolveFrozenSpec(Ledger, Cohort, i, out KingdomPolityNpcSpec spec,
					out string _, out Failure)) return false;
				for (int gear = 0; gear < spec.GearBlueprints.Count; gear++)
					if (!TryClearRemovalWitness(Zone, Ledger.RealmId, Cohort.CohortId,
						Receipt.ProjectionId, GearObjectId(Ledger.RealmId, Cohort, Receipt,
							spec, gear), gear, Gear: true, out Failure)) return false;
				if (!TryClearRemovalWitness(Zone, Ledger.RealmId, Cohort.CohortId,
					Receipt.ProjectionId, KingdomPolityCohortRules.PreparedObjectId(Cohort, i),
					i, Gear: false, out Failure)) return false;
			}
			return true;
		}
	}
}
