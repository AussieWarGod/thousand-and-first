using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityConflictRules
	{
		internal static bool TryValidateIncident(KingdomPolityIncidentRecord Plan,
			out string Failure)
		{
			Failure = null;
			if (Plan == null) return Fail("conflict plan is absent", out Failure);
			if (Plan.Intervention != null && (!ValidIntervention(Plan.Intervention) ||
				Plan.Purpose != KingdomPolityCohortPurpose.Warband ||
				Plan.Intervention.IncidentPlanId != Plan.IncidentPlanId ||
				!KingdomPolityAuthority.Contains(Plan.EligibleSurfaceRefs,
					Plan.Intervention.SurfaceRef) ||
				!KingdomPolityAuthority.Contains(Plan.InterventionOptionKeys,
					OptionKey(Plan.Intervention.Choice))))
				return Fail("witnessed intervention is not bound to this clash", out Failure);
			bool consented = Plan.Conclusion?.ResolutionKind ==
				KingdomPolityResolutionKind.ConsentedEscrow;
			if (Plan.Aftermath != null && (!ValidAftermath(Plan.Aftermath) ||
				Plan.Purpose != KingdomPolityCohortPurpose.Warband || Plan.Conclusion == null ||
				Plan.Aftermath.IncidentPlanId != Plan.IncidentPlanId ||
				Plan.Aftermath.ConclusionId != Plan.Conclusion.ConclusionId ||
				!KingdomPolityAuthority.Contains(Plan.EligibleSurfaceRefs,
					Plan.Aftermath.SurfaceRef) ||
				(!consented && !KingdomPolityAuthority.Contains(
					Plan.Conclusion.ObservedFactIds, Plan.Aftermath.ObservedFactId) ||
				 consented && (Plan.Intervention == null || Plan.Aftermath.ObservedFactId !=
					Plan.Intervention.ObservedFactId)) ||
				!KingdomPolityAuthority.Contains(Plan.Conclusion.ReceiptRefs,
					Plan.Aftermath.ReceiptId)))
				return Fail("aftermath is not bound to its witnessed conclusion", out Failure);
			if (Plan.Aftermath != null && ((Plan.Intervention == null) !=
				string.IsNullOrEmpty(Plan.Aftermath.InterventionId) || Plan.Intervention != null &&
				Plan.Aftermath.InterventionId != Plan.Intervention.InterventionId))
				return Fail("aftermath intervention reference is incoherent", out Failure);
			if (Plan.Conclusion != null && Plan.Intervention != null &&
				(!KingdomPolityAuthority.Contains(Plan.Conclusion.ReceiptRefs,
					Plan.Intervention.ReceiptId) || !consented &&
				 !KingdomPolityAuthority.Contains(Plan.Conclusion.ObservedFactIds,
					Plan.Intervention.ObservedFactId) || consented &&
				 Plan.Conclusion.ConsentReceiptId != Plan.Intervention.ReceiptId))
				return Fail("conclusion omitted its witnessed intervention", out Failure);
			return true;
		}

		internal static bool TryValidateGraph(KingdomPolityLedger Ledger,
			KingdomPolityIncidentRecord Plan, out string Failure)
		{
			Failure = null;
			KingdomPolityInterventionRecord intervention = Plan?.Intervention;
			if (intervention != null)
			{
				if (Plan.ParticipantCohortRefs.Count !=
					intervention.ParticipantProjectionIds.Count)
					return Fail("intervention does not bind every finite clash participant",
						out Failure);
				for (int i = 0; i < Plan.ParticipantCohortRefs.Count; i++)
				{
					KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(Ledger,
						Plan.ParticipantCohortRefs[i]);
					KingdomPolityProjectionReceipt receipt = cohort == null ? null :
						KingdomPolityAuthority.Projection(Ledger, cohort.ManifestationReceiptId);
					if (cohort == null || cohort.SurfaceRef != intervention.SurfaceRef ||
						receipt == null || receipt.Kind !=
							KingdomPolityProjectionKind.CohortManifestation ||
						receipt.SourceRef != cohort.CohortId ||
						receipt.ZoneId != intervention.ZoneId ||
						receipt.Phase == KingdomPolityProjectionPhase.Prepared ||
						receipt.Phase == KingdomPolityProjectionPhase.Cancelled ||
						receipt.CommittedTick > intervention.CommitTick ||
						!KingdomPolityAuthority.Contains(intervention.ParticipantProjectionIds,
							receipt.ProjectionId))
						return Fail("intervention projection proof is missing or foreign",
							out Failure);
				}
				if (Plan.Conclusion != null && intervention.CommitTick > Plan.Conclusion.CommitTick)
					return Fail("intervention occurs after its conclusion", out Failure);
			}
			KingdomPolityAftermathRecord aftermath = Plan?.Aftermath;
			if (aftermath == null) return true;
			if (Plan.Conclusion == null || aftermath.CommitTick != Plan.Conclusion.CommitTick ||
				intervention != null && (aftermath.SurfaceRef != intervention.SurfaceRef ||
				aftermath.ZoneId != intervention.ZoneId) ||
				(aftermath.Kind == KingdomPolityAftermathKind.Ceasefire) !=
					HasPeaceResolution(Plan) ||
				(aftermath.Kind == KingdomPolityAftermathKind.ConsentedResolution) !=
					(Plan.Conclusion.ResolutionKind ==
					 KingdomPolityResolutionKind.ConsentedEscrow))
				return Fail("aftermath does not match the exact witnessed outcome", out Failure);
			return true;
		}

		internal static bool ValidIntervention(KingdomPolityInterventionRecord V)
		{
			return V != null && KingdomPolityRules.TypedId(V.InterventionId,
				"taf:intervention:witnessed:v1:") &&
				KingdomPolityRules.TypedId(V.IncidentPlanId, "taf:incident-plan:") &&
				V.Choice >= KingdomPolityInterventionChoice.MediateCeasefire &&
				V.Choice <= KingdomPolityInterventionChoice.ConsentAbstractResolution &&
				KingdomPolityRules.SemanticId(V.SurfaceRef) &&
				KingdomPolityRules.Text(V.ZoneId, true) && V.CommitTick >= 0L &&
				KingdomPolityRules.TypedId(V.ObservedFactId, "taf:fact:witnessed:") &&
				Canonical(V.ParticipantProjectionIds, 1, KingdomPolityRules.MaxRefs) &&
				KingdomPolityRules.SemanticId(V.ReceiptId) &&
				KingdomPolityRules.Digest(V.ProofDigest) && V.ProofDigest == InterventionDigest(V);
		}

		internal static bool ValidAftermath(KingdomPolityAftermathRecord V)
		{
			return V != null && KingdomPolityRules.TypedId(V.AftermathId,
				"taf:aftermath:witnessed:v1:") &&
				KingdomPolityRules.TypedId(V.IncidentPlanId, "taf:incident-plan:") &&
				KingdomPolityRules.TypedId(V.ConclusionId, "taf:conclusion:") &&
				V.Kind >= KingdomPolityAftermathKind.Ceasefire &&
				V.Kind <= KingdomPolityAftermathKind.ConsentedResolution &&
				KingdomPolityRules.SemanticId(V.SurfaceRef) &&
				KingdomPolityRules.Text(V.ZoneId, true) && V.CommitTick >= 0L &&
				KingdomPolityRules.TypedId(V.ObservedFactId, "taf:fact:witnessed:") &&
				KingdomPolityRules.OptionalId(V.InterventionId) &&
				KingdomPolityRules.SemanticId(V.ReceiptId) &&
				KingdomPolityRules.Digest(V.ProofDigest) && V.ProofDigest == AftermathDigest(V);
		}

		internal static string OptionKey(KingdomPolityInterventionChoice Choice)
		{
			switch (Choice)
			{
			case KingdomPolityInterventionChoice.MediateCeasefire: return "mediate-ceasefire";
			case KingdomPolityInterventionChoice.SupportSettlement: return "assist-defender";
			case KingdomPolityInterventionChoice.SupportVisitor: return "assist-attacker";
			case KingdomPolityInterventionChoice.Observe: return "observe";
			case KingdomPolityInterventionChoice.ConsentAbstractResolution:
				return "consent-abstract-resolution";
			default: return null;
			}
		}

		private static bool Canonical(IList<string> Values, int Minimum, int Maximum)
		{
			if (Values == null || Values.Count < Minimum || Values.Count > Maximum) return false;
			string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				if (!KingdomPolityRules.SemanticId(Values[i]) || previous != null &&
					string.CompareOrdinal(previous, Values[i]) >= 0) return false;
				previous = Values[i];
			}
			return true;
		}

		private static string InterventionDigest(KingdomPolityInterventionRecord V)
		{
			List<string> values = new List<string> { V.InterventionId ?? "",
				V.IncidentPlanId ?? "", ((byte)V.Choice).ToString(CultureInfo.InvariantCulture),
				V.SurfaceRef ?? "", V.ZoneId ?? "",
				V.CommitTick.ToString(CultureInfo.InvariantCulture), V.ObservedFactId ?? "" };
			values.AddRange(V.ParticipantProjectionIds ?? new List<string>());
			values.Add(V.ReceiptId ?? "");
			return KingdomPolityRules.ActivationDigest("polity-witnessed-intervention-v1", values);
		}

		private static string AftermathDigest(KingdomPolityAftermathRecord V)
		{
			return KingdomPolityRules.ActivationDigest("polity-witnessed-aftermath-v1",
				V.AftermathId ?? "", V.IncidentPlanId ?? "", V.ConclusionId ?? "",
				((byte)V.Kind).ToString(CultureInfo.InvariantCulture), V.SurfaceRef ?? "",
				V.ZoneId ?? "", V.CommitTick.ToString(CultureInfo.InvariantCulture),
				V.ObservedFactId ?? "", V.InterventionId ?? "", V.ReceiptId ?? "");
		}

		private static bool HasPeaceResolution(KingdomPolityIncidentRecord Plan)
		{
			if (Plan.Intervention?.Choice ==
				KingdomPolityInterventionChoice.MediateCeasefire) return true;
			for (int i = 0; Plan.Conclusion != null &&
				i < Plan.Conclusion.RelationDeltas.Count; i++)
				if (Plan.Conclusion.RelationDeltas[i].After == KingdomPolityRelationBand.Truce ||
					Plan.Conclusion.RelationDeltas[i].After == KingdomPolityRelationBand.Pact)
					return true;
			return false;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
