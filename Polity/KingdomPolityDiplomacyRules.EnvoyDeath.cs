using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityDiplomacyRules
	{
		private const string NeutralDeathConclusionPrefix =
			"taf:conclusion:envoy-death-neutral:v1:";
		private const string NeutralDeathFactPrefix =
			"taf:fact:polity-envoy-death-neutral:v1:";
		private const string NeutralDeathReceiptPrefix =
			"taf:receipt:polity-envoy-death-neutral:v1:";

		internal static bool TryConcludeNeutralEnvoyDeath(KingdomPolityLedger Ledger,
			long ExpectedRevision, string TermsPlanId, string CohortId, string ProjectionId,
			string BodyId, string TargetPolityId, long Tick,
			KingdomPolityConsignmentAbsenceProof ConsignmentAbsence,
			out KingdomPolityEnvoyDeathOutcome Outcome,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Outcome = KingdomPolityEnvoyDeathOutcome.Refused;
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || Tick < 0L)
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "neutral envoy death input is invalid", out Failure);
			if (!TryEnvoyDeathContext(Ledger, TermsPlanId, CohortId, ProjectionId,
				BodyId, TargetPolityId, Tick, out KingdomPolityIncidentRecord plan,
				out KingdomPolityCohortPlan cohort, out KingdomPolityGrievanceRecord original,
				out Failure)) return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityIncidentConclusion expected = NeutralDeathConclusion(TermsPlanId,
				CohortId, ProjectionId, BodyId, TargetPolityId, Tick, plan.Hospitality,
				out string receipt);
			if (plan.Conclusion != null)
			{
				if (!OwnsNeutralDeathConclusion(plan) || !SameEnvoyDeathConclusion(
					plan.Conclusion, expected) || cohort.RewardEventId != receipt ||
					original.Phase != KingdomPolityGrievancePhase.Withdrawn ||
					original.ResolutionRef != expected.ConclusionId ||
					(cohort.Phase != KingdomPolityCohortPhase.Materialized && cohort.Phase !=
						KingdomPolityCohortPhase.Concluded))
					return KingdomPolityAuthority.Refuse(Result,
						"neutral envoy death retry changed its exact terminal receipt", out Failure);
				if (cohort.Phase == KingdomPolityCohortPhase.Concluded)
				{
					Outcome = KingdomPolityEnvoyDeathOutcome.Committed;
					Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
					Result.CommittedRevision = Ledger.Revision; return true;
				}
			}
			else if (cohort.Phase != KingdomPolityCohortPhase.Materialized ||
				original.Phase != KingdomPolityGrievancePhase.Consumed ||
				original.ConsumedByIncidentId != plan.IncidentId)
				return KingdomPolityAuthority.Refuse(Result,
					"neutral envoy death source has left its open audience", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			if (plan.Conclusion == null)
				ApplyEnvoyTerminal(candidate, TermsPlanId, CohortId, original.GrievanceId,
					expected, receipt, KingdomPolityGrievancePhase.Withdrawn, false);
			if (!TryCloseDeathCorrespondence(candidate, TermsPlanId, CohortId,
				ConsignmentAbsence, Tick, out bool held, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			if (held && plan.Conclusion != null)
			{
				Outcome = KingdomPolityEnvoyDeathOutcome.PendingRecovery;
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (!held) KingdomPolityAuthority.Cohort(candidate, CohortId).Phase =
				KingdomPolityCohortPhase.Concluded;
			if (!KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure)) return false;
			Outcome = held ? KingdomPolityEnvoyDeathOutcome.PendingRecovery :
				KingdomPolityEnvoyDeathOutcome.Committed;
			return true;
		}

		internal static bool IsPendingEnvoyDeathClosure(KingdomPolityLedger Ledger,
			string TermsPlanId, string CohortId)
		{
			KingdomPolityIncidentRecord plan = FindPlan(Ledger, TermsPlanId);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(Ledger, CohortId);
			return cohort?.Phase == KingdomPolityCohortPhase.Materialized &&
				(OwnsHarmConclusion(plan) || OwnsNeutralDeathConclusion(plan));
		}

		private static bool TryEnvoyDeathContext(KingdomPolityLedger L, string PlanId,
			string CohortId, string ProjectionId, string BodyId, string TargetPolityId, long Tick,
			out KingdomPolityIncidentRecord Plan, out KingdomPolityCohortPlan Cohort,
			out KingdomPolityGrievanceRecord Original, out string Failure)
		{
			Plan = FindPlan(L, PlanId); Cohort = KingdomPolityAuthority.Cohort(L, CohortId);
			Original = Plan == null || Plan.GrievanceRefs.Count != 1 ? null :
				FindGrievance(L, Plan.GrievanceRefs[0]);
			KingdomPolityProjectionReceipt projection = KingdomPolityAuthority.Projection(
				L, ProjectionId); int bodies = 0;
			for (int i = 0; projection != null && i < projection.ObjectIds.Count; i++)
				if (projection.ObjectIds[i] == BodyId) bodies++;
			if (Plan == null || Plan.Purpose != KingdomPolityCohortPurpose.Envoy ||
				Plan.ParticipantCohortRefs.Count != 1 || Plan.ParticipantCohortRefs[0] != CohortId ||
				Cohort == null || Cohort.Purpose != KingdomPolityCohortPurpose.Envoy ||
				Cohort.PolityId == TargetPolityId || Cohort.ManifestationReceiptId != ProjectionId ||
				projection == null || projection.Kind !=
					KingdomPolityProjectionKind.CohortManifestation ||
				projection.SourceRef != CohortId || projection.Phase !=
					KingdomPolityProjectionPhase.Committed || bodies != 1 ||
				Tick < projection.CommittedTick || Original == null ||
				Original.IssuerPolityId != Cohort.PolityId ||
				Original.TargetPolityId != TargetPolityId ||
				!IsUniqueCurrentPolity(L, TargetPolityId) ||
				!DeathHospitalityAllowed(Plan))
				return HarmFail("envoy death lacks one exact committed body and audience",
					out Failure);
			Failure = null; return true;
		}

		private static void ApplyEnvoyTerminal(KingdomPolityLedger L, string PlanId,
			string CohortId, string OriginalId, KingdomPolityIncidentConclusion Conclusion,
			string Receipt, KingdomPolityGrievancePhase OriginalPhase, bool ConcludeCohort)
		{
			KingdomPolityIncidentRecord plan = FindPlan(L, PlanId);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(L, CohortId);
			KingdomPolityGrievanceRecord original = FindGrievance(L, OriginalId);
			plan.Conclusion = Conclusion; cohort.RewardEventId = Receipt;
			if (ConcludeCohort) cohort.Phase = KingdomPolityCohortPhase.Concluded;
			original.Phase = OriginalPhase; original.ResolutionRef = Conclusion.ConclusionId;
			if (plan.Hospitality?.Phase == KingdomPolityHospitalityPhase.Debited)
				plan.Hospitality.Phase = KingdomPolityHospitalityPhase.Applied;
		}

		private static bool TryCloseDeathCorrespondence(KingdomPolityLedger L,
			string TermsPlanId, string CohortId, KingdomPolityConsignmentAbsenceProof Absence,
			long Tick, out bool Held, out string Failure)
		{
			return KingdomPolityCorrespondenceRules.TryApplyRecipientUnavailable(L,
				TermsPlanId, CohortId, Absence, Tick, out Held, out Failure);
		}

		private static bool DeathHospitalityAllowed(KingdomPolityIncidentRecord Plan)
		{
			KingdomPolityHospitalityPhase? phase = Plan?.Hospitality?.Phase;
			return phase == null || phase == KingdomPolityHospitalityPhase.Debited ||
				phase == KingdomPolityHospitalityPhase.Abandoned ||
				phase == KingdomPolityHospitalityPhase.Quarantined ||
				phase == KingdomPolityHospitalityPhase.Applied && Plan.Conclusion != null;
		}

		private static bool TerminalHospitality(KingdomPolityHospitalityTransaction T)
		{
			return T == null || T.Phase == KingdomPolityHospitalityPhase.Applied ||
				T.Phase == KingdomPolityHospitalityPhase.Abandoned ||
				T.Phase == KingdomPolityHospitalityPhase.Quarantined;
		}

		private static void AppendHospitalityEvidence(KingdomPolityHospitalityTransaction T,
			List<string> Facts, List<string> Receipts)
		{
			if (T?.Phase != KingdomPolityHospitalityPhase.Debited &&
				T?.Phase != KingdomPolityHospitalityPhase.Applied) return;
			KingdomPolityAuthority.AddSortedUnique(Facts, T.Proof.ObservedFactId);
			KingdomPolityAuthority.AddSortedUnique(Receipts, T.Proof.ReceiptId);
		}

		private static bool SameEnvoyDeathConclusion(KingdomPolityIncidentConclusion A,
			KingdomPolityIncidentConclusion B)
		{
			return A != null && B != null && A.ConclusionId == B.ConclusionId &&
				A.ResolutionKind == B.ResolutionKind && A.CommitTick == B.CommitTick &&
				ExactDeathRefs(A.ObservedFactIds, B.ObservedFactIds) &&
				ExactDeathRefs(A.ReceiptRefs, B.ReceiptRefs) && A.SystemicDeltas.Count == 0 &&
				A.RelationDeltas.Count == 0 && string.IsNullOrEmpty(A.ConsentReceiptId) &&
				string.IsNullOrEmpty(A.EscrowReceiptId) &&
				string.IsNullOrEmpty(A.SnapshotReceiptId);
		}

		private static bool ExactDeathRefs(IList<string> A, IList<string> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++) if (A[i] != B[i]) return false;
			return true;
		}

		private static bool IsUniqueCurrentPolity(KingdomPolityLedger L, string Id)
		{
			int matches = 0;
			for (int i = 0; L != null && i < L.Polities.Count; i++)
				if (L.Polities[i].Source == KingdomPolitySource.CurrentRealm &&
					L.Polities[i].Lifecycle == KingdomPolityLifecycle.Active &&
					L.Polities[i].PolityId == Id) matches++;
			return matches == 1;
		}

		private static KingdomPolityIncidentConclusion NeutralDeathConclusion(string PlanId,
			string CohortId, string ProjectionId, string BodyId, string TargetPolityId, long Tick,
			KingdomPolityHospitalityTransaction Hospitality, out string Receipt)
		{
			string fact = KingdomPolityRules.ActivationId(NeutralDeathFactPrefix,
				"polity-neutral-envoy-death-fact-v1", PlanId, CohortId, BodyId,
				TargetPolityId, Tick.ToString(CultureInfo.InvariantCulture));
			Receipt = KingdomPolityRules.ActivationId(NeutralDeathReceiptPrefix,
				"polity-neutral-envoy-death-receipt-v1", fact, ProjectionId);
			List<string> facts = new List<string> { fact };
			List<string> receipts = new List<string> { Receipt };
			AppendHospitalityEvidence(Hospitality, facts, receipts);
			return new KingdomPolityIncidentConclusion
			{
				ConclusionId = KingdomPolityRules.ActivationId(NeutralDeathConclusionPrefix,
					"polity-neutral-envoy-death-conclusion-v1", PlanId, fact, Receipt),
				ResolutionKind = KingdomPolityResolutionKind.LiveScene, CommitTick = Tick,
				ObservedFactIds = facts, ReceiptRefs = receipts
			};
		}

		private static bool OwnsNeutralDeathConclusion(KingdomPolityIncidentRecord Plan)
		{
			return Plan?.Conclusion?.ConclusionId != null && Plan.Conclusion.ConclusionId.
				StartsWith(NeutralDeathConclusionPrefix, StringComparison.Ordinal);
		}
	}
}
