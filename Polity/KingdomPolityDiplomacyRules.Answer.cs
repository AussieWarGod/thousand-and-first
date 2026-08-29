using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityDiplomacyRules
	{
		public static bool TryAnswerTerms(KingdomPolityLedger Ledger, long ExpectedRevision,
			string TermsPlanId, KingdomPolityTermsChoice Choice, string WitnessedFactId, long Tick,
			KingdomPolityHospitalityProof Hospitality,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				Choice == KingdomPolityTermsChoice.None || (byte)Choice > 4 ||
				!KingdomPolityRules.TypedId(WitnessedFactId, "taf:fact:witnessed:") || Tick < 0L ||
				(Hospitality != null && (!KingdomPolityHospitalityRules.TryValidate(Hospitality,
					out Failure) || Hospitality.CommitTick > Tick)))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "terms answer is not an exact witnessed choice", out Failure);
			KingdomPolityIncidentRecord plan = FindPlan(Ledger, TermsPlanId);
			string conclusionId = TermsConclusionId(TermsPlanId, Choice, WitnessedFactId);
			if (plan != null && plan.Conclusion != null)
			{
				if (plan.Conclusion.ConclusionId != conclusionId)
					return KingdomPolityAuthority.Refuse(Result,
						"terms were already answered under another witnessed choice", out Failure);
				bool concludedWithHospitality = plan.Hospitality?.Phase ==
					KingdomPolityHospitalityPhase.Applied;
				if (concludedWithHospitality != (Hospitality != null) ||
					Hospitality != null && plan.Hospitality.Proof?.ProofDigest !=
						Hospitality.ProofDigest)
					return KingdomPolityAuthority.Refuse(Result,
						"terms answer retry changed its hospitality evidence", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			KingdomPolityGrievanceRecord grievance = plan == null || plan.GrievanceRefs.Count != 1
				? null : FindGrievance(Ledger, plan.GrievanceRefs[0]);
			KingdomPolityCohortPlan envoy = plan == null || plan.ParticipantCohortRefs.Count != 1
				? null : KingdomPolityAuthority.Cohort(Ledger, plan.ParticipantCohortRefs[0]);
			if (plan == null || plan.Purpose != KingdomPolityCohortPurpose.Envoy || grievance == null ||
				grievance.ConsumedByIncidentId != plan.IncidentId ||
				grievance.Phase != KingdomPolityGrievancePhase.Consumed ||
				!WitnessedCohort(Ledger, envoy))
				return KingdomPolityAuthority.Refuse(Result,
					"terms lack their consumed cause or committed witnessed envoy", out Failure);
			KingdomPolityRelation relation = FindRelation(Ledger, grievance.IssuerPolityId,
				grievance.TargetPolityId);
			if (relation == null) return KingdomPolityAuthority.Refuse(Result,
				"terms have no exact directed relation authority", out Failure);
			KingdomPolityHospitalityTransaction hospitalityTransaction = plan.Hospitality;
			if (Hospitality != null && (hospitalityTransaction == null ||
				hospitalityTransaction.Phase != KingdomPolityHospitalityPhase.Debited ||
				!KingdomPolityHospitalityRules.ProofMatches(hospitalityTransaction,
					Hospitality, Hospitality.CommitTick)))
				return KingdomPolityAuthority.Refuse(Result,
					"terms hospitality is not an exact committed debit", out Failure);
			if (Hospitality == null && hospitalityTransaction != null &&
				(hospitalityTransaction.Phase == KingdomPolityHospitalityPhase.Planned ||
				 hospitalityTransaction.Phase == KingdomPolityHospitalityPhase.Debited))
				return KingdomPolityAuthority.Refuse(Result,
					"terms hospitality must finish or quarantine before answering", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityIncidentRecord changedPlan = FindPlan(candidate, TermsPlanId);
			KingdomPolityGrievanceRecord changedGrievance = FindGrievance(candidate,
				grievance.GrievanceId);
			KingdomPolityRelation changedRelation = FindRelation(candidate,
				grievance.IssuerPolityId, grievance.TargetPolityId);
			KingdomPolityRelationBand after = BandFor(Choice);
			changedPlan.Conclusion = TermsConclusion(changedPlan, changedRelation, after,
				Choice, WitnessedFactId, Tick, Hospitality);
			changedGrievance.Phase = KingdomPolityGrievancePhase.Resolved;
			changedGrievance.ResolutionRef = changedPlan.Conclusion.ConclusionId;
			changedRelation.Band = after; changedRelation.ChangedTick = Tick;
			KingdomPolityAuthority.AddSortedUnique(changedRelation.SourceRefs, WitnessedFactId);
			if (Hospitality != null)
			{
				changedPlan.Hospitality.Phase = KingdomPolityHospitalityPhase.Applied;
			}
			if (Choice == KingdomPolityTermsChoice.Refuse)
			{
				if (!ApplyRefusal(candidate, changedPlan, changedGrievance, envoy,
					WitnessedFactId, Tick, out Failure))
					return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			}
			else SettleFront(candidate, changedGrievance.GrievanceId,
				Choice == KingdomPolityTermsChoice.Truce);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		private static KingdomPolityIncidentConclusion TermsConclusion(
			KingdomPolityIncidentRecord Plan, KingdomPolityRelation Relation,
			KingdomPolityRelationBand After, KingdomPolityTermsChoice Choice,
			string WitnessedFactId, long Tick, KingdomPolityHospitalityProof Hospitality)
		{
			string id = TermsConclusionId(Plan.IncidentPlanId, Choice, WitnessedFactId);
			string receipt = KingdomPolityRules.ActivationId("taf:receipt:polity-terms:v1:",
				"polity-terms-receipt-v1", id);
			KingdomPolityIncidentConclusion result = new KingdomPolityIncidentConclusion
			{
				ConclusionId = id, ResolutionKind = KingdomPolityResolutionKind.LiveScene,
				CommitTick = Tick, ObservedFactIds = new List<string> { WitnessedFactId },
				ReceiptRefs = new List<string> { receipt }
			};
			if (Relation.Band != After)
			{
				string relationReceipt = KingdomPolityRules.ActivationId(
					"taf:receipt:polity-relation:v1:", "polity-terms-relation-v1", id,
					Relation.RelationId, Relation.Band.ToString(), After.ToString());
				result.RelationDeltas.Add(new KingdomPolityRelationDelta
				{
					RelationId = Relation.RelationId, Before = Relation.Band, After = After,
					ReceiptId = relationReceipt
				});
				KingdomPolityAuthority.AddSortedUnique(result.ReceiptRefs, relationReceipt);
			}
			if (Hospitality != null)
			{
				KingdomPolityAuthority.AddSortedUnique(result.ObservedFactIds,
					Hospitality.ObservedFactId);
				KingdomPolityAuthority.AddSortedUnique(result.ReceiptRefs, Hospitality.ReceiptId);
			}
			return result;
		}

		private static bool ApplyRefusal(KingdomPolityLedger L,
			KingdomPolityIncidentRecord Terms, KingdomPolityGrievanceRecord Original,
			KingdomPolityCohortPlan Envoy, string WitnessedFactId, long Tick,
			out string Failure)
		{
			Failure = null;
			string sourceReceipt = KingdomPolityRules.ActivationId(
				"taf:receipt:polity-terms:v1:", "polity-terms-receipt-v1",
				Terms.Conclusion.ConclusionId);
			KingdomPolityGrievanceIngressRequest ingress =
				new KingdomPolityGrievanceIngressRequest
				{
					SourceKind = KingdomPolityGrievanceSourceKind.RefusedTerms,
					SourceRef = Terms.IncidentPlanId, SourceReceiptId = sourceReceipt,
					IssuerPolityId = Original.IssuerPolityId,
					TargetPolityId = Original.TargetPolityId
				};
			if (!TryDeriveExactGrievance(L, ingress,
				out KingdomPolityGrievanceRecord expected, out Failure)) return false;
			KingdomPolityGrievanceRecord refusal = FindGrievance(L, expected.GrievanceId);
			if (refusal == null)
			{
				if (L.Grievances.Count >= KingdomPolityRules.MaxGrievances)
				{
					Failure = "refused terms cannot record their caused grievance"; return false;
				}
				refusal = expected; InsertGrievance(L, refusal);
			}
			else if (!ExactOpenGrievance(refusal, expected))
			{
				Failure = "refused terms grievance retry changed source"; return false;
			}
			if (!TryInsertBrokenPactGrievances(L, Terms, out Failure)) return false;
			KingdomPolityRouteRecord route = Envoy.SourceRef.StartsWith("taf:route:",
				StringComparison.Ordinal) ? KingdomPolityAuthority.Route(L, Envoy.SourceRef) : null;
			string target = route == null ? Envoy.CohortId : route.RouteId;
			KingdomPolityFrontTarget kind = route == null ? KingdomPolityFrontTarget.Cohort :
				KingdomPolityFrontTarget.Route;
			KingdomPolityFrontRecord front = route == null ? null : FindFront(L, route.FrontId);
			if (front == null)
			{
				if (L.Fronts.Count >= KingdomPolityRules.MaxFronts)
				{
					Failure = "refused terms cannot open a bounded front"; return false;
				}
				front = new KingdomPolityFrontRecord
				{
					FrontId = KingdomPolityRules.ActivationId("taf:front:refused-terms:v1:",
						"polity-refused-front-v1", Terms.IncidentPlanId, target),
					TargetKind = kind, TargetRef = target,
					PressureBand = refusal.Severity, NextDueEventTick = Tick,
					GrievanceRefs = new List<string> { refusal.GrievanceId },
					Phase = KingdomPolityFrontPhase.ConfrontationAvailable
				};
				L.Fronts.Add(front); if (route != null) route.FrontId = front.FrontId;
			}
			else
			{
				KingdomPolityAuthority.AddSortedUnique(front.GrievanceRefs, refusal.GrievanceId);
				front.PressureBand = Math.Min(5, Math.Max(front.PressureBand, refusal.Severity));
				front.NextDueEventTick = Tick;
				front.Phase = KingdomPolityFrontPhase.ConfrontationAvailable;
			}
			if (route != null)
			{
				if (route.Phase != KingdomPolityRoutePhase.AvailableToWitness &&
					route.Phase != KingdomPolityRoutePhase.Blocked &&
					route.Phase != KingdomPolityRoutePhase.ConfrontationAvailable)
				{
					Failure = "refused terms route is not at a witnessed endpoint"; return false;
				}
				route.Phase = KingdomPolityRoutePhase.ConfrontationAvailable;
			}
			return true;
		}
	}
}
