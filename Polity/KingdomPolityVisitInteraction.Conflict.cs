using System;
using System.Collections.Generic;
using XRL.UI;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityVisitInteraction
	{
		private static void AnswerConflict(KingdomSystem System, string CohortId)
		{
			KingdomPolityIncidentRecord clash = ClashFor(System.PolityLedger, CohortId);
			if (clash == null)
			{
				Popup.Show("No caused confrontation is recorded for this party."); return;
			}
			if (clash.Conclusion != null)
			{
				ShowAftermath(System, clash); return;
			}
			if (clash.Intervention != null)
			{
				if (clash.Intervention.Choice ==
					KingdomPolityInterventionChoice.ConsentAbstractResolution)
				{
					if (!KingdomPolityConsentedEscrowRuntime.TryRecover(System, Now(),
						out string escrowFailure))
						Popup.Show("The consented escrow remains safely leased: " + escrowFailure);
					else
					{
						clash = ClashFor(System.PolityLedger, CohortId);
						if (clash?.Conclusion != null) ShowAftermath(System, clash);
						else Popup.Show("The consented escrow remains prepared for exact recovery.");
					}
					return;
				}
				if (clash.Intervention.Choice ==
					KingdomPolityInterventionChoice.SupportSettlement &&
					!TryIngestTrespass(System, clash, out string causeFailure))
				{
					Popup.Show("Your stance is recorded, but its caused grievance awaits " +
						"recovery: " + causeFailure); return;
				}
				Popup.Show("Your witnessed stance is already recorded. The finite scene remains " +
					"open until a loaded outcome is witnessed; no unseen winner or loss is inferred.");
				return;
			}
			int picked = Popup.PickOption(Title: "A witnessed confrontation",
				Intro: "Both finite parties are present here. Choose one explicit stance; " +
					"nothing is resolved outside this loaded scene.",
				Options: new[] { "Mediate a ceasefire", "Stand with the settlement",
					"Stand with the visitors", "Observe without taking a side",
					"Consent to a reversible collateral settlement", "Leave" },
				AllowEscape: true);
			if (picked < 0 || picked > 4) return;
			KingdomPolityInterventionChoice choice =
				(KingdomPolityInterventionChoice)(picked + 1);
			long tick = Now(); string witnessed = Witnessed("intervention",
				clash.IncidentPlanId, choice.ToString(), N(tick));
			if (choice == KingdomPolityInterventionChoice.ConsentAbstractResolution)
			{
				OfferConsentedEscrow(System, clash, tick); return;
			}
			if (!KingdomPolityEndpointRuntime.TryRecordCurrentEndpointIntervention(System,
				clash.IncidentPlanId, choice, tick, witnessed, out string failure))
			{
				Popup.Show("The stance is not recorded: " + failure); return;
			}
			clash = ClashFor(System.PolityLedger, CohortId);
			if (choice == KingdomPolityInterventionChoice.MediateCeasefire)
			{
				MediateCeasefire(System, clash, tick); return;
			}
			Popup.Show("Your witnessed stance is recorded. The confrontation remains finite " +
				"and local until an outcome is witnessed here.");
		}

		private static void MediateCeasefire(KingdomSystem System,
			KingdomPolityIncidentRecord Clash, long Tick)
		{
			List<KingdomPolityRelationDelta> deltas = CeasefireDeltas(System.PolityLedger,
				Clash);
			List<string> receipts = new List<string>();
			for (int i = 0; i < deltas.Count; i++) receipts.Add(deltas[i].ReceiptId);
			string witness = Clash.Intervention.ObservedFactId;
			KingdomPolityAuthority.AddSortedUnique(receipts,
				KingdomPolityRules.ActivationId("taf:receipt:polity-clash:v1:",
					"polity-mediated-clash-receipt-v1", Clash.IncidentPlanId, witness));
			if (!KingdomPolityEndpointRuntime.TryConcludeCurrentEndpointClash(System,
				Clash.IncidentPlanId, Tick, new List<string> { witness },
				new List<KingdomPolitySystemicDelta>(), deltas, receipts, out string failure))
			{
				Popup.Show("The mediation is recorded, but the ceasefire cannot close: " + failure);
				return;
			}
			Clash = ClashFor(System.PolityLedger, Clash.ParticipantCohortRefs[0]);
			ConcludeParticipants(System, Clash);
			Popup.Show("A witnessed ceasefire is recorded. The parties prepare to withdraw. " +
				"No winner, casualty, death, or conquest is inferred.");
		}

		private static List<KingdomPolityRelationDelta> CeasefireDeltas(
			KingdomPolityLedger Ledger, KingdomPolityIncidentRecord Clash)
		{
			List<KingdomPolityRelation> relations = new List<KingdomPolityRelation>();
			for (int i = 0; i < Clash.GrievanceRefs.Count; i++)
			{
				KingdomPolityGrievanceRecord grievance = FindGrievance(Ledger,
					Clash.GrievanceRefs[i]);
				for (int j = 0; grievance != null && j < Ledger.Relations.Count; j++)
				{
					KingdomPolityRelation relation = Ledger.Relations[j];
					bool endpoints = relation.FromPolityId == grievance.IssuerPolityId &&
						relation.ToPolityId == grievance.TargetPolityId ||
						relation.FromPolityId == grievance.TargetPolityId &&
						relation.ToPolityId == grievance.IssuerPolityId;
					if (endpoints && !HasRelation(relations, relation.RelationId))
						relations.Add(relation);
				}
			}
			relations.Sort((a, b) => string.CompareOrdinal(a.RelationId, b.RelationId));
			List<KingdomPolityRelationDelta> result = new List<KingdomPolityRelationDelta>();
			for (int i = 0; i < relations.Count; i++)
			{
				KingdomPolityRelation relation = relations[i];
				if (relation.Band == KingdomPolityRelationBand.Truce ||
					relation.Band == KingdomPolityRelationBand.Pact) continue;
				result.Add(new KingdomPolityRelationDelta
				{
					RelationId = relation.RelationId, Before = relation.Band,
					After = KingdomPolityRelationBand.Truce,
					ReceiptId = KingdomPolityRules.ActivationId(
						"taf:receipt:polity-ceasefire:v1:", "polity-mediated-relation-v1",
						Clash.Intervention.InterventionId, relation.RelationId)
				});
			}
			return result;
		}

		private static void ConcludeParticipants(KingdomSystem System,
			KingdomPolityIncidentRecord Clash)
		{
			if (!TryConcludeParticipants(System, Clash, out string failure))
				KingdomLog.Log("polity: clash cohort conclusion awaits recovery (" +
					failure + ")");
		}

		private static bool TryConcludeParticipants(KingdomSystem System,
			KingdomPolityIncidentRecord Clash, out string Failure)
		{
			Failure = null;
			if (System?.PolityLedger == null || Clash?.Conclusion == null)
				return ReplayFail("clash lacks its exact witnessed conclusion", out Failure);
			string conclusionId = Clash.Conclusion.ConclusionId;
			for (int i = 0; i < Clash.ParticipantCohortRefs.Count; i++)
			{
				string cohortId = Clash.ParticipantCohortRefs[i]; bool applied = false;
				for (int attempt = 0; attempt < 2; attempt++)
				{
					if (KingdomPolityCohortRules.TryConcludeEndpointCohort(System.PolityLedger,
						System.PolityLedger.Revision, cohortId, conclusionId,
						out KingdomPolityPublicationResult result, out Failure))
					{
						applied = true; break;
					}
					if (result.Outcome != KingdomPolityCasOutcome.Conflict) return false;
				}
				KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(
					System.PolityLedger, cohortId);
				if (!applied || cohort?.Phase != KingdomPolityCohortPhase.Concluded ||
					cohort.RewardEventId != conclusionId)
					return ReplayFail("clash participant lacks its exact incident conclusion",
						out Failure);
			}
			return true;
		}

		private static void ShowAftermath(KingdomSystem System,
			KingdomPolityIncidentRecord Clash)
		{
			ConcludeParticipants(System, Clash);
			string text = Clash.Aftermath?.Kind == KingdomPolityAftermathKind.Ceasefire
				? "A witnessed ceasefire ended this confrontation."
				: Clash.Aftermath?.Kind == KingdomPolityAftermathKind.ConsentedResolution
					? "A consented collateral settlement ended this confrontation; its exact object was released unchanged."
					: "A witnessed withdrawal ended this confrontation.";
			Popup.Show(text + " No unseen winner, casualty, death, or conquest is recorded.");
		}

		private static KingdomPolityGrievanceRecord FindGrievance(KingdomPolityLedger Ledger,
			string Id)
		{
			for (int i = 0; Ledger != null && i < Ledger.Grievances.Count; i++)
				if (Ledger.Grievances[i].GrievanceId == Id) return Ledger.Grievances[i];
			return null;
		}

		private static bool HasRelation(IList<KingdomPolityRelation> Values, string Id)
		{
			for (int i = 0; i < Values.Count; i++) if (Values[i].RelationId == Id) return true;
			return false;
		}

		private static bool TryIngestTrespass(KingdomSystem System,
			KingdomPolityIncidentRecord Clash, out string Failure)
		{
			Failure = null; string issuer = null, target = null;
			for (int i = 0; System?.PolityLedger != null &&
				i < System.PolityLedger.Polities.Count; i++)
				if (System.PolityLedger.Polities[i].Source ==
					KingdomPolitySource.CurrentRealm)
				{
					if (issuer != null) { Failure = "current polity identity is ambiguous"; return false; }
					issuer = System.PolityLedger.Polities[i].PolityId;
				}
			for (int i = 0; Clash != null && i < Clash.ParticipantCohortRefs.Count; i++)
			{
				KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(
					System.PolityLedger, Clash.ParticipantCohortRefs[i]);
				if (cohort == null || cohort.PolityId == issuer) continue;
				if (target != null && target != cohort.PolityId)
				{
					Failure = "visitor polity identity is ambiguous"; return false;
				}
				target = cohort.PolityId;
			}
			if (issuer == null || target == null || Clash?.Intervention == null)
			{
				Failure = "exact trespass endpoints are absent"; return false;
			}
			KingdomPolityGrievanceIngressRequest ingress =
				new KingdomPolityGrievanceIngressRequest
				{
					SourceKind = KingdomPolityGrievanceSourceKind.WitnessedTrespass,
					SourceRef = Clash.IncidentPlanId,
					SourceReceiptId = Clash.Intervention.ReceiptId,
					IssuerPolityId = issuer, TargetPolityId = target
				};
			return KingdomPolityDiplomacyRules.TryIngestExactGrievance(
				System.PolityLedger, System.PolityLedger.Revision, ingress, out string _,
				out KingdomPolityPublicationResult _, out Failure);
		}
	}
}
