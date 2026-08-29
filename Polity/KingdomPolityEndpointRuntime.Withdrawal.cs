using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityEndpointRuntime
	{
		/// <summary>Removes only exact loaded bodies when their optional presentation authority ends.</summary>
		internal static bool TryWithdrawCurrentEndpoint(KingdomSystem System, string CohortId,
			long Tick, out string Failure)
		{
			Failure = null; Zone zone; KingdomPolityLedger ledger; KingdomPolityCohortPlan cohort;
			if (!TryAdmit(System, CohortId, out zone, out ledger, out cohort, out Failure)) return false;
			if (!string.IsNullOrEmpty(cohort.ManifestationReceiptId) &&
				(cohort.Phase == KingdomPolityCohortPhase.Materialized ||
				 cohort.Phase == KingdomPolityCohortPhase.Concluded ||
				 cohort.Phase == KingdomPolityCohortPhase.Abandoned))
			{
				if (!TryReplayDeathIntents(System, CohortId, out Failure)) return false;
				cohort = KingdomPolityAuthority.Cohort(ledger, CohortId);
			}
			if (cohort.Phase == KingdomPolityCohortPhase.Abandoned)
				return TryCleanupCurrentEndpoint(System, CohortId, out Failure);
			if (cohort.Phase == KingdomPolityCohortPhase.Cleaned ||
				cohort.Phase == KingdomPolityCohortPhase.Cancelled)
				return KingdomPolityExperienceRuntime.TryReleaseForCohort(System, cohort, out Failure);
			if (string.IsNullOrEmpty(cohort.ManifestationReceiptId))
			{
				if (cohort.Phase != KingdomPolityCohortPhase.Planned) return false;
				if (!TryClearRolledBackPreparedEvidence(zone, ledger, cohort, out Failure)) return false;
				return KingdomPolityExperienceRuntime.TryReleaseForCohort(System, cohort, out Failure);
			}
			KingdomPolityProjectionReceipt receipt = KingdomPolityAuthority.Projection(ledger,
				cohort.ManifestationReceiptId);
			if (!ExactReceipt(cohort, receipt, zone, out Failure)) return false;
			if (cohort.Phase == KingdomPolityCohortPhase.Planned)
			{
				if (receipt.Phase != KingdomPolityProjectionPhase.Prepared)
				{
					Failure = "uncommitted polity withdrawal has a foreign phase"; return false;
				}
				long rollbackRevision = ledger.Revision;
				if (!TryRemovePreparedBodies(ledger, zone, ledger.RealmId, cohort, receipt,
					out Failure) ||
					!KingdomPolityCohortRules.TryRollbackPreparedEndpointManifestation(ledger,
						rollbackRevision, cohort.CohortId, receipt.ProjectionId, zone.ZoneID,
						receipt.ObjectIds, out KingdomPolityPublicationResult _, out Failure)) return false;
				if (!TryClearPreparedRollbackEvidence(zone, ledger, cohort, receipt, out Failure))
					return false;
				return KingdomPolityExperienceRuntime.TryReleaseForCohort(System, cohort, out Failure);
			}
			if (cohort.Phase == KingdomPolityCohortPhase.Materialized)
			{
				if (!TryProveMaterializedLifecycleAfterDeathReplay(System, CohortId,
					out Failure)) return false;
				string witnessed = KingdomPolityRules.ActivationId(
					"taf:event:polity-presentation-withdrawal:v1:",
					"polity-loaded-presentation-withdrawal-v1", cohort.CohortId,
					receipt.ProjectionId,
					Tick.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
				if (!KingdomPolityCohortRules.TryConcludeEndpointCohort(ledger, ledger.Revision,
					cohort.CohortId, witnessed, out KingdomPolityPublicationResult _,
					out Failure)) return false;
			}
			cohort = KingdomPolityAuthority.Cohort(ledger, CohortId);
			if (cohort.Phase != KingdomPolityCohortPhase.Concluded)
			{
				Failure = "only a loaded finite polity presentation can withdraw"; return false;
			}
			return TryCleanupCurrentEndpoint(System, CohortId, out Failure);
		}

		private static bool TryRemovePreparedBodies(KingdomPolityLedger Ledger, Zone Zone,
			string RealmId,
			KingdomPolityCohortPlan Cohort,
			KingdomPolityProjectionReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (!TryPromotePreparedCleanupIntents(Zone, RealmId, Cohort, Receipt,
				out Failure)) return false;
			if (!TryObserve(Zone, RealmId, Cohort, Receipt,
				out GameObject[] observed, out Failure)) return false;
			for (int i = 0; i < observed.Length; i++)
			{
				GameObject body = observed[i];
				if (!GameObject.Validate(body)) continue;
				if (!TryBuildCustodyPlan(Ledger, Zone, RealmId, Cohort, Receipt, body, i,
					AllowRemovedGear: true, out FrozenCustodyPlan plan, out Failure) ||
					!TryReleaseFrozenCustody(Ledger, RealmId, Cohort, Receipt, plan,
						out Failure) || !TryRemoveExactBody(body, plan.Cell, RealmId, Cohort,
						Receipt, i, out Failure)) return false;
			}
			if (!TryObserve(Zone, RealmId, Cohort, Receipt,
				out observed, out Failure)) return false;
			for (int i = 0; i < observed.Length; i++) if (GameObject.Validate(observed[i]))
			{
				Failure = "prepared polity withdrawal still observes an owned body"; return false;
			}
			return TryProvePreparedRollbackEvidence(Ledger, Zone, RealmId, Cohort, Receipt,
				out Failure);
		}

		private static bool TryProvePreparedRollbackEvidence(KingdomPolityLedger Ledger, Zone Zone,
			string RealmId, KingdomPolityCohortPlan Cohort,
			KingdomPolityProjectionReceipt Receipt, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Cohort.ResolvedMembers.Count; i++)
			{
				string bodyId = KingdomPolityCohortRules.PreparedObjectId(Cohort, i);
				if (!TryFindResidentObject(bodyId, out GameObject body, out Failure) ||
					GameObject.Validate(body) || !TryProveLocalObjectAbsence(Zone, bodyId, out Failure))
					return FailPhysical(Failure ??
						"prepared rollback still has resident body custody", out Failure);
				KingdomPolityCleanupEvidenceProof intent = TryProveCleanupIntent(Zone, RealmId,
					Cohort.CohortId, Receipt.ProjectionId, bodyId, i, (byte)Cohort.Phase,
					(byte)Receipt.Phase, out Cell _, out string _, out string _, out Failure);
				if (intent != KingdomPolityCleanupEvidenceProof.Absent ||
					TryProveRemovalWitness(Zone,
						KingdomPolityPhysicalCustodyRules.CleanupRemovalKind, RealmId,
						Cohort.CohortId, Receipt.ProjectionId, bodyId, i, out string _,
						out Failure) != KingdomPolityCleanupEvidenceProof.Exact)
					return FailPhysical(Failure ??
						"prepared rollback lacks exact final body evidence", out Failure);
				if (!TryResolveFrozenSpec(Ledger, Cohort, i, out KingdomPolityNpcSpec spec,
					out string _, out Failure)) return false;
				for (int gear = 0; gear < spec.GearBlueprints.Count; gear++)
				{
					string gearId = GearObjectId(RealmId, Cohort, Receipt, spec, gear);
					if (!TryFindResidentObject(gearId, out GameObject item, out Failure) ||
						GameObject.Validate(item) || !TryProveLocalObjectAbsence(Zone, gearId,
							out Failure) || TryProveRemovalWitness(Zone,
								KingdomPolityPhysicalCustodyRules.GearRemovalKind, RealmId,
								Cohort.CohortId, Receipt.ProjectionId, gearId, gear,
								out string _, out Failure) != KingdomPolityCleanupEvidenceProof.Exact)
						return FailPhysical(Failure ??
							"prepared rollback lacks exact final gear evidence", out Failure);
				}
			}
			return true;
		}

		private static bool TryClearRolledBackPreparedEvidence(Zone Zone,
			KingdomPolityLedger Ledger, KingdomPolityCohortPlan Cohort, out string Failure)
		{
			Failure = null;
			string projectionId = KingdomPolityRules.ActivationId(
				"taf:projection:cohort:v1:", "polity-cohort-projection-v1", Cohort.CohortId,
				Zone.ZoneID, Cohort.ProfileId, Cohort.ProfileRevision.ToString(
					global::System.Globalization.CultureInfo.InvariantCulture));
			KingdomPolityProjectionReceipt receipt = new KingdomPolityProjectionReceipt
				{ ProjectionId = projectionId, ZoneId = Zone.ZoneID };
			return TryClearPreparedRollbackEvidence(Zone, Ledger, Cohort, receipt, out Failure);
		}

		private static bool TryClearPreparedRollbackEvidence(Zone Zone,
			KingdomPolityLedger Ledger, KingdomPolityCohortPlan Cohort,
			KingdomPolityProjectionReceipt Receipt, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Cohort.ResolvedMembers.Count; i++)
			{
				string bodyId = KingdomPolityCohortRules.PreparedObjectId(Cohort, i);
				KingdomPolityCleanupEvidenceProof intent = TryProveCleanupIntent(Zone,
					Ledger.RealmId, Cohort.CohortId, Receipt.ProjectionId, bodyId, i,
					(byte)KingdomPolityCohortPhase.Planned,
					(byte)KingdomPolityProjectionPhase.Prepared, out Cell _, out string _,
					out string _, out Failure);
				if (intent != KingdomPolityCleanupEvidenceProof.Absent)
					return FailPhysical(Failure ??
						"rolled-back cleanup retained ambiguous intent authority", out Failure);
				if (!TryClearRolledBackWitness(Zone, Ledger.RealmId, Cohort.CohortId,
					Receipt.ProjectionId, bodyId, i, Gear: false, out Failure)) return false;
				if (!TryResolveFrozenSpec(Ledger, Cohort, i, out KingdomPolityNpcSpec spec,
					out string _, out Failure)) return false;
				for (int gear = 0; gear < spec.GearBlueprints.Count; gear++)
					if (!TryClearRolledBackWitness(Zone, Ledger.RealmId, Cohort.CohortId,
						Receipt.ProjectionId, GearObjectId(Ledger.RealmId, Cohort, Receipt, spec, gear),
						gear, Gear: true, out Failure)) return false;
			}
			return true;
		}

		private static bool TryClearRolledBackWitness(Zone Zone, string RealmId,
			string CohortId, string ProjectionId, string ObjectId, int Ordinal, bool Gear,
			out string Failure)
		{
			Failure = null;
			if (!TryFindResidentObject(ObjectId, out GameObject resident, out Failure) ||
				GameObject.Validate(resident) || !TryProveLocalObjectAbsence(Zone, ObjectId,
					out Failure)) return FailPhysical(Failure ??
						"rolled-back object lacks exact physical absence", out Failure);
			KingdomPolityCleanupEvidenceProof proof = TryProveRemovalWitness(Zone, Gear ?
				KingdomPolityPhysicalCustodyRules.GearRemovalKind :
				KingdomPolityPhysicalCustodyRules.CleanupRemovalKind, RealmId, CohortId,
				ProjectionId, ObjectId, Ordinal, out string _, out Failure);
			if (proof == KingdomPolityCleanupEvidenceProof.Absent) return true;
			if (proof != KingdomPolityCleanupEvidenceProof.Exact)
				return FailPhysical(Failure ??
						"rolled-back final witness lacks exact absence", out Failure);
			if (!TryClearRemovalWitness(Zone, RealmId, CohortId, ProjectionId, ObjectId,
				Ordinal, Gear, out Failure)) return false;
			if (!TryFindResidentObject(ObjectId, out resident, out Failure) ||
				GameObject.Validate(resident) || !TryProveLocalObjectAbsence(Zone, ObjectId,
					out Failure)) return FailPhysical(Failure ??
						"rolled-back absence changed during evidence clear", out Failure);
			return true;
		}

	}
}
