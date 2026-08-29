using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityDiplomacyRules
	{
		private const string HarmConclusionPrefix = "taf:conclusion:envoy-harm:v1:";
		private const string HarmFactPrefix = "taf:fact:witnessed:envoy-harm:v1:";
		private const string HarmReceiptPrefix = "taf:receipt:polity-envoy-harm:v1:";

		internal static bool TryRecordWitnessedEnvoyHarm(KingdomPolityLedger Ledger,
			long ExpectedRevision, string TermsPlanId, string CohortId, string ProjectionId,
			string BodyId, string TargetPolityId, long Tick,
			KingdomPolityConsignmentAbsenceProof ConsignmentAbsence,
			out KingdomPolityEnvoyDeathOutcome Outcome, out string GrievanceId,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Outcome = KingdomPolityEnvoyDeathOutcome.Refused; GrievanceId = null;
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || Tick < 0L)
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "witnessed envoy harm input is invalid", out Failure);
			if (!TryHarmContext(Ledger, TermsPlanId, CohortId, ProjectionId, BodyId,
				TargetPolityId, Tick, out KingdomPolityIncidentRecord plan,
				out KingdomPolityCohortPlan cohort, out KingdomPolityGrievanceRecord original,
				out KingdomPolityIncidentConclusion conclusion, out string receipt, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityGrievanceIngressRequest ingress = HarmIngress(TermsPlanId, receipt,
				cohort.PolityId, TargetPolityId);
			if (plan.Conclusion != null)
				return TryPublishStoredHarm(Ledger, ExpectedRevision, ingress, plan, cohort,
					ConsignmentAbsence, ref Outcome, out GrievanceId, Result, out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			ApplyEnvoyTerminal(candidate, TermsPlanId, CohortId, original.GrievanceId,
				conclusion, receipt, KingdomPolityGrievancePhase.Resolved, false);
			if (!TryCloseDeathCorrespondence(candidate, TermsPlanId, CohortId,
				ConsignmentAbsence, Tick, out bool held, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			if (!held) KingdomPolityAuthority.Cohort(candidate, CohortId).Phase =
				KingdomPolityCohortPhase.Concluded;
			KingdomPolityGrievanceRecord grievance = null;
			if (!held && !TryDeriveExactGrievance(candidate, ingress, out grievance, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			if (grievance != null)
			{
				GrievanceId = grievance.GrievanceId;
				if (HasGrievanceSourceCollision(candidate, grievance))
					return KingdomPolityAuthority.Refuse(Result,
						"envoy harm source already emitted another grievance", out Failure);
				if (candidate.Grievances.Count < KingdomPolityRules.MaxGrievances)
					InsertGrievance(candidate, grievance);
			}
			if (!KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure)) return false;
			Outcome = held || grievance == null || FindGrievance(Ledger, grievance.GrievanceId) == null
				? KingdomPolityEnvoyDeathOutcome.PendingRecovery
				: KingdomPolityEnvoyDeathOutcome.Committed;
			return true;
		}

		private static bool TryPublishStoredHarm(KingdomPolityLedger Ledger,
			long ExpectedRevision, KingdomPolityGrievanceIngressRequest Ingress,
			KingdomPolityIncidentRecord Plan, KingdomPolityCohortPlan Cohort,
			KingdomPolityConsignmentAbsenceProof Absence,
			ref KingdomPolityEnvoyDeathOutcome Outcome, out string GrievanceId,
			KingdomPolityPublicationResult Result, out string Failure)
		{
			GrievanceId = null; Failure = null;
			if (Cohort.Phase == KingdomPolityCohortPhase.Materialized)
			{
				if (Ledger.Revision != ExpectedRevision)
					return KingdomPolityAuthority.Conflict(Result, out Failure);
				KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
				if (!TryCloseDeathCorrespondence(candidate, Plan.IncidentPlanId, Cohort.CohortId,
					Absence, Plan.Conclusion.CommitTick, out bool held, out Failure))
					return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
				if (held)
				{
					Outcome = KingdomPolityEnvoyDeathOutcome.PendingRecovery;
					Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
					Result.CommittedRevision = Ledger.Revision; return true;
				}
				KingdomPolityAuthority.Cohort(candidate, Cohort.CohortId).Phase =
					KingdomPolityCohortPhase.Concluded;
				if (!KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure)) return false;
				Outcome = KingdomPolityEnvoyDeathOutcome.PendingRecovery;
				return true;
			}
			if (!TryDeriveExactGrievance(Ledger, Ingress,
				out KingdomPolityGrievanceRecord expected, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			GrievanceId = expected.GrievanceId;
			KingdomPolityGrievanceRecord existing = FindGrievance(Ledger, GrievanceId);
			if (existing != null)
			{
				if (!ExactOpenGrievance(existing, expected)) return KingdomPolityAuthority.Refuse(
					Result, "envoy harm retry changed its exact grievance", out Failure);
				Outcome = KingdomPolityEnvoyDeathOutcome.Committed;
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (HasGrievanceSourceCollision(Ledger, expected)) return KingdomPolityAuthority.Refuse(
				Result, "envoy harm source already emitted another grievance", out Failure);
			if (Ledger.Grievances.Count >= KingdomPolityRules.MaxGrievances)
			{
				Outcome = KingdomPolityEnvoyDeathOutcome.PendingRecovery;
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger insertion = KingdomPolityRules.Clone(Ledger);
			InsertGrievance(insertion, expected);
			if (!KingdomPolityAuthority.Commit(Ledger, insertion, Result, out Failure)) return false;
			Outcome = KingdomPolityEnvoyDeathOutcome.Committed; return true;
		}

		private static KingdomPolityGrievanceIngressRequest HarmIngress(string PlanId,
			string Receipt, string Issuer, string Target)
		{
			return new KingdomPolityGrievanceIngressRequest
			{
				SourceKind = KingdomPolityGrievanceSourceKind.WitnessedEnvoyHarm,
				SourceRef = PlanId, SourceReceiptId = Receipt,
				IssuerPolityId = Issuer, TargetPolityId = Target
			};
		}

		private static bool DeriveWitnessedEnvoyHarm(KingdomPolityLedger L,
			KingdomPolityGrievanceIngressRequest R, out KingdomPolityGrievanceRecord G,
			out string Failure)
		{
			G = null; Failure = null;
			KingdomPolityIncidentRecord plan = FindPlan(L, R.SourceRef);
			KingdomPolityCohortPlan cohort = plan == null || plan.ParticipantCohortRefs.Count != 1
				? null : KingdomPolityAuthority.Cohort(L, plan.ParticipantCohortRefs[0]);
			KingdomPolityProjectionReceipt projection = cohort == null ? null :
				KingdomPolityAuthority.Projection(L, cohort.ManifestationReceiptId);
			KingdomPolityGrievanceRecord original = plan == null || plan.GrievanceRefs.Count != 1
				? null : FindGrievance(L, plan.GrievanceRefs[0]);
			string fact = HarmFact(plan?.Conclusion);
			if (plan?.Conclusion == null || fact == null || !TerminalHospitality(plan.Hospitality) ||
				cohort == null || cohort.Purpose != KingdomPolityCohortPurpose.Envoy ||
				cohort.PolityId != R.IssuerPolityId || cohort.Phase !=
					KingdomPolityCohortPhase.Concluded || cohort.RewardEventId != R.SourceReceiptId ||
				projection == null || projection.ProjectionId != cohort.ManifestationReceiptId ||
				projection.SourceRef != cohort.CohortId || projection.Phase !=
					KingdomPolityProjectionPhase.Committed || original == null ||
				original.IssuerPolityId != R.IssuerPolityId || original.TargetPolityId !=
					R.TargetPolityId || original.Phase != KingdomPolityGrievancePhase.Resolved ||
				original.ResolutionRef != plan.Conclusion.ConclusionId ||
				!IsUniqueCurrentPolity(L, R.TargetPolityId))
				return IngressFail("envoy harm lacks its exact loaded audience receipt", out Failure);
			string body = null;
			for (int i = 0; i < projection.ObjectIds.Count; i++)
			{
				string candidate = projection.ObjectIds[i];
				KingdomPolityIncidentConclusion expected = HarmConclusion(plan.IncidentPlanId,
					cohort.CohortId, projection.ProjectionId, candidate, R.TargetPolityId,
					plan.Conclusion.CommitTick, plan.Hospitality, out string candidateReceipt);
				if (candidateReceipt != R.SourceReceiptId ||
					!SameEnvoyDeathConclusion(plan.Conclusion, expected)) continue;
				if (body != null) return IngressFail("envoy harm body receipt is ambiguous", out Failure);
				body = candidate;
			}
			if (body == null) return IngressFail(
				"envoy harm receipt does not name one projected body", out Failure);
			string id = KingdomPolityRules.ActivationId(
				"taf:grievance:witnessed-harm:v1:", "polity-witnessed-envoy-harm-v1",
				plan.IncidentPlanId, fact);
			return BuildIngress(id, R, KingdomPolityGrievanceCause.WitnessedHarm, fact, 4,
				new List<string> { plan.IncidentPlanId, cohort.CohortId, projection.ProjectionId,
					body, original.GrievanceId, plan.Conclusion.ConclusionId, fact,
					R.SourceReceiptId }, out G, out Failure);
		}

		private static bool TryHarmContext(KingdomPolityLedger L, string PlanId,
			string CohortId, string ProjectionId, string BodyId, string TargetPolityId, long Tick,
			out KingdomPolityIncidentRecord Plan, out KingdomPolityCohortPlan Cohort,
			out KingdomPolityGrievanceRecord Original,
			out KingdomPolityIncidentConclusion Conclusion, out string Receipt, out string Failure)
		{
			Conclusion = null; Receipt = null;
			if (!TryEnvoyDeathContext(L, PlanId, CohortId, ProjectionId, BodyId, TargetPolityId,
				Tick, out Plan, out Cohort, out Original, out Failure)) return false;
			Conclusion = HarmConclusion(PlanId, CohortId, ProjectionId, BodyId, TargetPolityId,
				Tick, Plan.Hospitality, out Receipt);
			if (Plan.Conclusion == null)
				return (Cohort.Phase == KingdomPolityCohortPhase.Materialized && Original.Phase ==
					KingdomPolityGrievancePhase.Consumed && Original.ConsumedByIncidentId == Plan.IncidentId) ||
					HarmFail("envoy harm source has already left its open audience", out Failure);
			bool phase = Cohort.Phase == KingdomPolityCohortPhase.Materialized || Cohort.Phase ==
				KingdomPolityCohortPhase.Concluded;
			return (phase && Cohort.RewardEventId == Receipt && Original.Phase ==
				KingdomPolityGrievancePhase.Resolved && Original.ResolutionRef == Conclusion.ConclusionId &&
				SameEnvoyDeathConclusion(Plan.Conclusion, Conclusion)) || HarmFail(
					"envoy harm retry changed its exact receipt", out Failure);
		}

		private static KingdomPolityIncidentConclusion HarmConclusion(string PlanId,
			string CohortId, string ProjectionId, string BodyId, string TargetPolityId, long Tick,
			KingdomPolityHospitalityTransaction Hospitality, out string Receipt)
		{
			string fact = KingdomPolityRules.ActivationId(HarmFactPrefix,
				"polity-witnessed-envoy-harm-fact-v1", PlanId, CohortId, BodyId, TargetPolityId,
				Tick.ToString(CultureInfo.InvariantCulture));
			Receipt = KingdomPolityRules.ActivationId(HarmReceiptPrefix,
				"polity-witnessed-envoy-harm-receipt-v1", fact, ProjectionId);
			List<string> facts = new List<string> { fact };
			List<string> receipts = new List<string> { Receipt };
			AppendHospitalityEvidence(Hospitality, facts, receipts);
			return new KingdomPolityIncidentConclusion
			{
				ConclusionId = KingdomPolityRules.ActivationId(HarmConclusionPrefix,
					"polity-witnessed-envoy-harm-conclusion-v1", PlanId, fact, Receipt),
				ResolutionKind = KingdomPolityResolutionKind.LiveScene, CommitTick = Tick,
				ObservedFactIds = facts, ReceiptRefs = receipts
			};
		}

		private static string HarmFact(KingdomPolityIncidentConclusion Conclusion)
		{
			string result = null;
			for (int i = 0; Conclusion != null && i < Conclusion.ObservedFactIds.Count; i++)
				if (Conclusion.ObservedFactIds[i].StartsWith(HarmFactPrefix,
					StringComparison.Ordinal)) { if (result != null) return null; result = Conclusion.ObservedFactIds[i]; }
			return result;
		}

		private static bool OwnsHarmConclusion(KingdomPolityIncidentRecord Plan)
		{
			return Plan?.Conclusion?.ConclusionId != null && Plan.Conclusion.ConclusionId.StartsWith(
				HarmConclusionPrefix, StringComparison.Ordinal);
		}

		private static bool HasGrievanceSourceCollision(KingdomPolityLedger L,
			KingdomPolityGrievanceRecord Expected)
		{
			for (int i = 0; L != null && i < L.Grievances.Count; i++)
				if (L.Grievances[i].SourceEventId == Expected.SourceEventId &&
					L.Grievances[i].GrievanceId != Expected.GrievanceId) return true;
			return false;
		}

		private static bool HarmFail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
