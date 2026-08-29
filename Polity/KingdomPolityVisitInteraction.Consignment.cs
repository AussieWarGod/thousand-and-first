using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityVisitInteraction
	{
		private static bool OfferConsignment(KingdomSystem System,
			KingdomPolityIncidentRecord Terms, string CohortId, GameObject Body,
			out string Status)
		{
			Status = "";
			KingdomPolityIncidentRecord plan = ConsignmentFor(System?.PolityLedger,
				Terms?.IncidentPlanId, CohortId);
			if (plan == null) return true;
			if (!KingdomPolityCorrespondenceRules.TryDescribeConsignment(System.PolityLedger,
				plan.IncidentPlanId, out KingdomPolityConsignmentRequest request,
				out KingdomPolityCorrespondenceReplyKind reply, out string failure))
			{
				Popup.Show("The delegation's consignment request is unreadable: " + failure);
				return false;
			}
			if (reply == KingdomPolityCorrespondenceReplyKind.Fulfilled)
			{
				int delivered = plan.Conclusion.SystemicDeltas[0].Amount;
				Status = " The consignment landed " + delivered +
					" drams and recorded the same bounded relationship standing.";
				return true;
			}
			if (reply == KingdomPolityCorrespondenceReplyKind.Declined)
			{
				if (!TryIngestResourceRefusal(System, request, plan.IncidentPlanId,
					out failure))
				{
					Popup.Show("The decline remains exact, but its caused grievance awaits " +
						"recovery: " + failure); return false;
				}
				Status = " The water consignment was declined; that reply carries no standing change.";
				return true;
			}
			if (reply == KingdomPolityCorrespondenceReplyKind.Unfulfilled)
			{
				Status = " The one-shot consignment closed unfulfilled; no standing was credited.";
				return true;
			}
			if (reply == KingdomPolityCorrespondenceReplyKind.RecipientUnavailable)
			{
				Status = " The request closed because its exact recipient became unavailable; " +
					"no cargo, blame, or standing change was inferred.";
				return true;
			}
			int picked = Popup.PickOption(Title: "A bounded request",
				Intro: "The envoy asks for eight drams of fresh water from this settlement's " +
					"dedicated stores. Trade records one exact debit. Any conserved partial delivery " +
					"earns one relationship-standing point per dram, once; never experience or loot.",
				Options: new[] { "Deliver up to eight drams", "Decline the consignment",
					"Answer later" }, AllowEscape: true);
			if (picked == 0)
			{
				if (!KingdomTrade.TryDeliverPolityConsignment(System, Body?.CurrentZone, Body,
					request, out KingdomTradePolityConsignmentReceipt receipt, out failure))
				{
					Popup.Show(failure ?? "The exact water debit cannot be proved. Nothing is credited.");
					return false;
				}
				if (!KingdomPolityCorrespondenceRules.TryConsumeTradeReceipt(System.PolityLedger,
					System.PolityLedger.Revision, receipt, out KingdomPolityPublicationResult _,
					out failure))
				{
					Popup.Show("Trade kept the terminal receipt. The reply awaits recovery: " + failure);
					return false;
				}
				if (!KingdomTradeRules.TryAcknowledgePolityConsignment(System.TradeBook,
					System.PolityLedger, request, receipt, out bool _, out string ackFailure))
					Popup.Show("The reply is recorded. Trade retained its proof for recovery: " +
						(ackFailure ?? "unknown acknowledgement fault"));
				if (receipt.Kind == KingdomTradePolityConsignmentReceiptKind.Landed)
				{
					Status = " The consignment landed " + receipt.DeliveredDrams +
						" drams and recorded " + receipt.DeliveredDrams +
						" bounded relationship standing.";
					Popup.Show(receipt.DeliveredDrams + " drams pass under one exact Trade receipt. " +
						"The relationship records the same standing once; no experience or loot is granted.");
				}
				else
				{
					Status = " The one-shot consignment closed unfulfilled; no standing was credited.";
					Popup.Show("Trade closed the request unfulfilled. Any proved debit remains in " +
						"retained custody; no relationship standing was granted.");
				}
				return true;
			}
			if (picked == 1)
			{
				long tick = Now(); string witnessed = Witnessed("consignment-declined",
					plan.IncidentPlanId, N(tick));
				if (!KingdomPolityCorrespondenceRules.TryDeclineConsignmentWithExactGrievance(
					System.PolityLedger,
					System.PolityLedger.Revision, plan.IncidentPlanId, witnessed, tick,
					out string _, out KingdomPolityPublicationResult _, out failure))
				{
					Popup.Show("The decline and its caused grievance were not recorded: " +
						failure); return false;
				}
				Status = " The water consignment was declined; that reply carries no standing change.";
				return true;
			}
			return false;
		}

		private static KingdomPolityIncidentRecord ConsignmentFor(KingdomPolityLedger L,
			string TermsPlanId, string CohortId)
		{
			for (int i = 0; L != null && i < L.Incidents.Count; i++)
			{
				KingdomPolityIncidentRecord plan = L.Incidents[i];
				if (KingdomPolityCorrespondenceRules.IsConsignmentPlan(plan) &&
					KingdomPolityAuthority.Contains(plan.ParticipantCohortRefs, CohortId) &&
					KingdomPolityAuthority.Contains(plan.DisclosedStakeRefs, TermsPlanId)) return plan;
			}
			return null;
		}

		private static bool TryIngestResourceRefusal(KingdomSystem System,
			KingdomPolityConsignmentRequest Request, string PlanId, out string Failure)
		{
			Failure = null;
			KingdomPolityIncidentRecord plan = null;
			for (int i = 0; System?.PolityLedger != null &&
				i < System.PolityLedger.Incidents.Count; i++)
				if (System.PolityLedger.Incidents[i].IncidentPlanId == PlanId)
					plan = System.PolityLedger.Incidents[i];
			if (plan?.Conclusion == null || plan.Conclusion.ReceiptRefs.Count != 1)
			{
				Failure = "decline conclusion is absent"; return false;
			}
			KingdomPolityGrievanceIngressRequest ingress =
				new KingdomPolityGrievanceIngressRequest
				{
					SourceKind = KingdomPolityGrievanceSourceKind.ResourceRefusal,
					SourceRef = PlanId,
					SourceReceiptId = plan.Conclusion.ReceiptRefs[0],
					IssuerPolityId = Request.CounterpartyPolityId,
					TargetPolityId = Request.CurrentPolityId
				};
			return KingdomPolityDiplomacyRules.TryIngestExactGrievance(
				System.PolityLedger, System.PolityLedger.Revision, ingress, out string _,
				out KingdomPolityPublicationResult _, out Failure);
		}
	}
}
