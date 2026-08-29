using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityConflictRules
	{
		internal static bool TryRecordWitnessedIntervention(KingdomPolityLedger Ledger,
			long ExpectedRevision, string IncidentPlanId, KingdomPolityInterventionChoice Choice,
			string SurfaceRef, string ZoneId, long Tick, string ObservedFactId,
			IList<string> ParticipantProjectionIds, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger);
			Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!TryCreateIntervention(IncidentPlanId, Choice, SurfaceRef, ZoneId, Tick,
					ObservedFactId, ParticipantProjectionIds,
					out KingdomPolityInterventionRecord intervention, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityIncidentRecord plan = FindPlan(Ledger, IncidentPlanId);
			if (plan == null || !KingdomPolityAuthority.Contains(plan.InterventionOptionKeys,
				OptionKey(Choice)))
				return KingdomPolityAuthority.Refuse(Result,
					"intervention is not offered by this clash", out Failure);
			if (plan.Intervention != null)
			{
				if (plan.Intervention.ProofDigest != intervention.ProofDigest)
					return KingdomPolityAuthority.Refuse(Result,
						"clash already carries another intervention", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision;
				return true;
			}
			if (!ExactLiveParticipants(Ledger, plan, intervention, out Failure))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "intervention is not witnessed in this loaded clash", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			FindPlan(candidate, IncidentPlanId).Intervention = intervention;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		internal static bool TryCreateAftermath(KingdomPolityIncidentRecord Plan,
			KingdomPolityWitnessedClashProof Proof, KingdomPolityAftermathKind Kind,
			out KingdomPolityAftermathRecord Aftermath, out string Failure)
		{
			Aftermath = null;
			Failure = null;
			if (Plan?.Conclusion == null || Proof == null ||
				Kind < KingdomPolityAftermathKind.Ceasefire ||
				Kind > KingdomPolityAftermathKind.WitnessedWithdrawal ||
				Proof.ObservedFactIds.Count < 1)
				return Fail("aftermath lacks a witnessed clash conclusion", out Failure);
			string id = KingdomPolityRules.ActivationId("taf:aftermath:witnessed:v1:",
				"polity-witnessed-aftermath-id-v1", Plan.IncidentPlanId,
				Plan.Conclusion.ConclusionId, ((byte)Kind).ToString(CultureInfo.InvariantCulture));
			string receipt = KingdomPolityRules.ActivationId("taf:receipt:aftermath:v1:",
				"polity-witnessed-aftermath-receipt-v1", id, Proof.ProofDigest);
			Aftermath = new KingdomPolityAftermathRecord
			{
				AftermathId = id, IncidentPlanId = Plan.IncidentPlanId,
				ConclusionId = Plan.Conclusion.ConclusionId, Kind = Kind,
				SurfaceRef = Proof.SurfaceRef, ZoneId = Proof.ZoneId,
				CommitTick = Proof.CommitTick, ObservedFactId = Proof.ObservedFactIds[0],
				InterventionId = Plan.Intervention?.InterventionId, ReceiptId = receipt
			};
			Aftermath.ProofDigest = AftermathDigest(Aftermath);
			return ValidAftermath(Aftermath) || Fail("aftermath proof is invalid", out Failure);
		}

		private static bool TryCreateIntervention(string IncidentPlanId,
			KingdomPolityInterventionChoice Choice, string SurfaceRef, string ZoneId, long Tick,
			string ObservedFactId, IList<string> ParticipantProjectionIds,
			out KingdomPolityInterventionRecord Intervention, out string Failure)
		{
			Intervention = null;
			Failure = null;
			List<string> projections = ParticipantProjectionIds == null
				? new List<string>() : new List<string>(ParticipantProjectionIds);
			projections.Sort(System.StringComparer.Ordinal);
			string projectionDigest = KingdomPolityRules.ActivationDigest(
				"polity-intervention-participants-v1", projections);
			string id = KingdomPolityRules.ActivationId("taf:intervention:witnessed:v1:",
				"polity-witnessed-intervention-id-v1", IncidentPlanId ?? "",
				((byte)Choice).ToString(CultureInfo.InvariantCulture), ObservedFactId ?? "",
				projectionDigest);
			string receipt = KingdomPolityRules.ActivationId("taf:receipt:intervention:v1:",
				"polity-witnessed-intervention-receipt-v1", id, SurfaceRef ?? "", ZoneId ?? "");
			Intervention = new KingdomPolityInterventionRecord
			{
				InterventionId = id, IncidentPlanId = IncidentPlanId, Choice = Choice,
				SurfaceRef = SurfaceRef, ZoneId = ZoneId, CommitTick = Tick,
				ObservedFactId = ObservedFactId, ParticipantProjectionIds = projections,
				ReceiptId = receipt
			};
			Intervention.ProofDigest = InterventionDigest(Intervention);
			if (ValidIntervention(Intervention)) return true;
			Intervention = null;
			return Fail("witnessed intervention input is invalid", out Failure);
		}

		private static bool ExactLiveParticipants(KingdomPolityLedger L,
			KingdomPolityIncidentRecord Plan, KingdomPolityInterventionRecord Intervention,
			out string Failure)
		{
			Failure = null;
			if (Plan == null || Plan.Conclusion != null ||
				Plan.Purpose != KingdomPolityCohortPurpose.Warband ||
				!KingdomPolityAuthority.Contains(Plan.EligibleSurfaceRefs,
					Intervention.SurfaceRef) || Plan.ParticipantCohortRefs.Count !=
					Intervention.ParticipantProjectionIds.Count)
				return Fail("intervention has no exact open clash", out Failure);
			for (int i = 0; i < Plan.ParticipantCohortRefs.Count; i++)
			{
				KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(L,
					Plan.ParticipantCohortRefs[i]);
				KingdomPolityProjectionReceipt projection = cohort == null ? null :
					KingdomPolityAuthority.Projection(L, cohort.ManifestationReceiptId);
				if (cohort == null || cohort.Phase != KingdomPolityCohortPhase.Materialized ||
					cohort.SurfaceRef != Intervention.SurfaceRef || projection == null ||
					projection.Phase != KingdomPolityProjectionPhase.Committed ||
					projection.ZoneId != Intervention.ZoneId ||
					!KingdomPolityAuthority.Contains(Intervention.ParticipantProjectionIds,
						projection.ProjectionId))
					return Fail("intervention participant is not live at this endpoint", out Failure);
			}
			return true;
		}

		private static KingdomPolityIncidentRecord FindPlan(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; L != null && i < L.Incidents.Count; i++)
				if (L.Incidents[i].IncidentPlanId == Id) return L.Incidents[i];
			return null;
		}
	}
}
