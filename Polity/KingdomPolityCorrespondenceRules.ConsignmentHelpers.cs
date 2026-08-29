using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCorrespondenceRules
	{
		private static KingdomPolityIncidentConclusion FulfilledConclusion(
			KingdomPolityConsignmentRequest R, KingdomTradePolityConsignmentReceipt P,
			KingdomPolityRelation Relation)
		{
			string fact = Id("taf:fact:witnessed:polity-consignment:v1:", "fulfilled-fact",
				R.CorrespondencePlanId, P.ReceiptDigest);
			string reply = Id("taf:receipt:polity-correspondence-reply:v1:", "fulfilled-reply",
				R.CorrespondencePlanId, P.ReceiptId);
			string standing = StandingReceiptId(R, P.ReceiptId, P.DeliveredDrams);
			List<string> receipts = new List<string>
				{ P.ReceiptId, TradeOperationProofRef(P), reply, standing };
			KingdomPolityRelationDelta relationDelta = null;
			if (Relation.Band == KingdomPolityRelationBand.Contact)
			{
				string relationReceipt = RelationReceiptId(Relation.RelationId,
					P.ReceiptId, fact);
				receipts.Add(relationReceipt);
				relationDelta = new KingdomPolityRelationDelta
				{
					RelationId = Relation.RelationId, Before = Relation.Band,
					After = KingdomPolityRelationBand.Neutral, ReceiptId = relationReceipt
				};
			}
			receipts.Sort(StringComparer.Ordinal);
			KingdomPolityIncidentConclusion result = Conclusion(R.CorrespondencePlanId,
				"fulfilled", P.CommitTick, fact, receipts);
			result.SystemicDeltas.Add(new KingdomPolitySystemicDelta
			{
				Kind = KingdomPolitySystemicDeltaKind.Standing,
				TargetId = R.CounterpartyPolityId,
				Amount = ConsignmentStanding(P.DeliveredDrams), ReceiptId = standing
			});
			if (relationDelta != null) result.RelationDeltas.Add(relationDelta);
			return result;
		}

		private static KingdomPolityIncidentConclusion UnfulfilledConclusion(
			KingdomPolityConsignmentRequest R, KingdomTradePolityConsignmentReceipt P)
		{
			string fact = Id("taf:fact:witnessed:polity-consignment-failed:v1:",
				"unfulfilled-fact", R.CorrespondencePlanId, P.ReceiptDigest);
			string reply = Id("taf:receipt:polity-correspondence-reply:v1:",
				"unfulfilled-reply", R.CorrespondencePlanId, P.ReceiptId);
			List<string> receipts = new List<string>
				{ P.ReceiptId, TradeOperationProofRef(P), reply };
			receipts.Sort(StringComparer.Ordinal);
			return Conclusion(R.CorrespondencePlanId, "unfulfilled", P.CommitTick,
				fact, receipts);
		}

		private static string TradeOperationProofRef(
			KingdomTradePolityConsignmentReceipt Receipt)
		{
			return Id("taf:receipt:trade-operation-proof:v1:", "operation-proof",
				Receipt.TradeOperationId, Receipt.TradeEvidenceHash);
		}

		private static KingdomPolityIncidentConclusion DeclinedConclusion(
			KingdomPolityConsignmentRequest R, string WitnessedFactId, long Tick)
		{
			string reply = Id("taf:receipt:polity-correspondence-reply:v1:", "declined-reply",
				R.CorrespondencePlanId, WitnessedFactId);
			return Conclusion(R.CorrespondencePlanId, "declined", Tick,
				WitnessedFactId, new List<string> { reply });
		}

		private static KingdomPolityIncidentConclusion Conclusion(string PlanId,
			string Kind, long Tick, string Fact, List<string> Receipts)
		{
			string[] identity = new string[Receipts.Count + 3];
			identity[0] = PlanId; identity[1] = Fact; identity[2] = N(Tick);
			for (int i = 0; i < Receipts.Count; i++) identity[i + 3] = Receipts[i];
			return new KingdomPolityIncidentConclusion
			{
				ConclusionId = Id("taf:conclusion:correspondence:v1:", Kind, identity),
				ResolutionKind = KingdomPolityResolutionKind.LiveScene, CommitTick = Tick,
				ObservedFactIds = new List<string> { Fact }, ReceiptRefs = Receipts
			};
		}

		private static bool TryReplyKind(KingdomPolityLedger Ledger,
			KingdomPolityIncidentRecord Plan, KingdomPolityConsignmentRequest Request,
			out KingdomPolityCorrespondenceReplyKind Reply, out string Failure)
		{
			Reply = KingdomPolityCorrespondenceReplyKind.None; Failure = null;
			if (Plan?.Conclusion == null) return true;
			KingdomPolityIncidentConclusion c = Plan.Conclusion;
			if (c.ObservedFactIds.Count != 1) return Fail(
				"correspondence reply has no exact witnessed fact", out Failure);
			string kind;
			if ((c.ReceiptRefs.Count == 4 || c.ReceiptRefs.Count == 5) &&
				c.ObservedFactIds[0].StartsWith(
				"taf:fact:witnessed:polity-consignment:v1:", StringComparison.Ordinal) &&
				ContainsPrefix(c.ReceiptRefs, "taf:receipt:trade-polity-consignment:v1:") &&
				ContainsPrefix(c.ReceiptRefs, "taf:receipt:polity-correspondence-reply:v1:") &&
				ValidFulfilledDeltas(Ledger, Request, c, out Failure))
			{
				Reply = KingdomPolityCorrespondenceReplyKind.Fulfilled; kind = "fulfilled";
			}
			else if (c.ReceiptRefs.Count == 3 && c.SystemicDeltas.Count == 0 &&
				c.RelationDeltas.Count == 0 && c.ObservedFactIds[0].StartsWith(
				"taf:fact:witnessed:polity-consignment-failed:v1:",
				StringComparison.Ordinal) && ContainsPrefix(c.ReceiptRefs,
					"taf:receipt:trade-polity-consignment:v1:") && ContainsPrefix(c.ReceiptRefs,
					"taf:receipt:polity-correspondence-reply:v1:"))
			{
				Reply = KingdomPolityCorrespondenceReplyKind.Unfulfilled; kind = "unfulfilled";
			}
			else if (IsRecipientUnavailableConclusion(c))
			{
				Reply = KingdomPolityCorrespondenceReplyKind.RecipientUnavailable;
				kind = "recipient-unavailable";
			}
			else if (c.ReceiptRefs.Count == 1 && c.ObservedFactIds[0].StartsWith(
				"taf:fact:witnessed:", StringComparison.Ordinal) && c.ReceiptRefs[0].StartsWith(
				"taf:receipt:polity-correspondence-reply:v1:", StringComparison.Ordinal) &&
				c.SystemicDeltas.Count == 0 && c.RelationDeltas.Count == 0)
			{
				Reply = KingdomPolityCorrespondenceReplyKind.Declined; kind = "declined";
			}
			else return Fail("correspondence reply kind is not exact", out Failure);
			KingdomPolityIncidentConclusion expected = Conclusion(Plan.IncidentPlanId, kind,
				c.CommitTick, c.ObservedFactIds[0], new List<string>(c.ReceiptRefs));
			for (int i = 0; i < c.SystemicDeltas.Count; i++) expected.SystemicDeltas.Add(
				CopySystemic(c.SystemicDeltas[i]));
			for (int i = 0; i < c.RelationDeltas.Count; i++) expected.RelationDeltas.Add(
				CopyRelation(c.RelationDeltas[i]));
			if (!ExactConclusion(c, expected))
				return Fail("correspondence reply commitment changed", out Failure);
			return true;
		}

		private static bool ExactOpenPlan(KingdomPolityIncidentRecord A,
			KingdomPolityIncidentRecord E)
		{
			return A != null && E != null && A.IncidentPlanId == E.IncidentPlanId &&
				A.IncidentId == E.IncidentId && Exact(A.GrievanceRefs, E.GrievanceRefs) &&
				Exact(A.ParticipantCohortRefs, E.ParticipantCohortRefs) &&
				Exact(A.DisclosedStakeRefs, E.DisclosedStakeRefs) &&
				A.MaxSystemicWound == E.MaxSystemicWound && A.Purpose == E.Purpose &&
				A.EventStreamId == E.EventStreamId && A.RulesVersion == E.RulesVersion &&
				A.EventOrdinal == E.EventOrdinal && Exact(A.EligibleSurfaceRefs,
					E.EligibleSurfaceRefs) && Exact(A.InterventionOptionKeys,
					E.InterventionOptionKeys) && A.Hospitality == null && A.Intervention == null &&
				A.Aftermath == null;
		}

		private static bool ExactConclusion(KingdomPolityIncidentConclusion A,
			KingdomPolityIncidentConclusion E)
		{
			return A != null && E != null && A.ConclusionId == E.ConclusionId &&
				A.ResolutionKind == E.ResolutionKind && A.CommitTick == E.CommitTick &&
				Exact(A.ObservedFactIds, E.ObservedFactIds) &&
				ExactSystemic(A.SystemicDeltas, E.SystemicDeltas) &&
				ExactRelations(A.RelationDeltas, E.RelationDeltas) &&
				Exact(A.ReceiptRefs, E.ReceiptRefs) &&
				string.IsNullOrEmpty(A.ConsentReceiptId) &&
				string.IsNullOrEmpty(A.EscrowReceiptId) &&
				string.IsNullOrEmpty(A.SnapshotReceiptId);
		}

		private static bool Exact(IList<string> A, IList<string> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++) if (A[i] != B[i]) return false;
			return true;
		}

		private static KingdomPolityIncidentRecord FindPlan(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; L != null && i < L.Incidents.Count; i++)
				if (L.Incidents[i].IncidentPlanId == Id) return L.Incidents[i];
			return null;
		}

		private static KingdomPolityRecord Current(KingdomPolityLedger L)
		{
			for (int i = 0; L != null && i < L.Polities.Count; i++)
				if (L.Polities[i].Source == KingdomPolitySource.CurrentRealm &&
					L.Polities[i].Lifecycle == KingdomPolityLifecycle.Active) return L.Polities[i];
			return null;
		}

		private static KingdomPolityRelation Relation(KingdomPolityLedger L,
			string From, string To)
		{
			for (int i = 0; L != null && i < L.Relations.Count; i++)
				if (L.Relations[i].FromPolityId == From && L.Relations[i].ToPolityId == To)
					return L.Relations[i];
			return null;
		}

		private static bool EligibleRelation(KingdomPolityRelationBand Band)
		{
			return Band == KingdomPolityRelationBand.Contact ||
				Band == KingdomPolityRelationBand.Neutral || Band == KingdomPolityRelationBand.Pact ||
				Band == KingdomPolityRelationBand.Truce;
		}

		private static bool EligibleRequestRelation(KingdomPolityLedger L,
			KingdomPolityConsignmentRequest R)
		{
			KingdomPolityRelation relation = R == null ? null : Relation(L,
				R.CounterpartyPolityId, R.CurrentPolityId);
			return relation != null && EligibleRelation(relation.Band);
		}

		private static bool ContainsPrefix(IList<string> Values, string Prefix)
		{
			for (int i = 0; Values != null && i < Values.Count; i++)
				if (Values[i].StartsWith(Prefix, StringComparison.Ordinal)) return true;
			return false;
		}
	}
}
