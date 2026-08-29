using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCorrespondenceRules
	{
		public static int ConsignmentStanding(int DeliveredDrams)
		{
			return DeliveredDrams < 1 || DeliveredDrams > FirstContactWaterDrams ? 0 :
				DeliveredDrams * ConsignmentStandingPerDram;
		}

		private static string StandingReceiptId(KingdomPolityConsignmentRequest R,
			string TradeReceiptId, int Delivered)
		{
			return Id("taf:receipt:polity-consignment-standing:v1:", "standing",
				R.CorrespondencePlanId, R.CounterpartyPolityId, TradeReceiptId, N(Delivered));
		}

		private static string RelationReceiptId(string RelationId, string TradeReceiptId,
			string FactId)
		{
			return Id("taf:receipt:polity-consignment-relation:v1:", "relation",
				RelationId, TradeReceiptId, FactId);
		}

		private static bool ValidFulfilledDeltas(KingdomPolityLedger L,
			KingdomPolityConsignmentRequest R, KingdomPolityIncidentConclusion C,
			out string Failure)
		{
			Failure = null; string trade = SinglePrefix(C.ReceiptRefs,
				"taf:receipt:trade-polity-consignment:v1:");
			if (R == null || trade == null || C.SystemicDeltas.Count != 1 ||
				C.RelationDeltas.Count > 1) return Fail(
				"fulfilled correspondence has no exact bounded relationship delta", out Failure);
			KingdomPolitySystemicDelta standing = C.SystemicDeltas[0];
			if (standing.Kind != KingdomPolitySystemicDeltaKind.Standing ||
				standing.TargetId != R.CounterpartyPolityId || standing.Amount < 1 ||
				standing.Amount > FirstContactWaterDrams * ConsignmentStandingPerDram ||
				standing.ReceiptId != StandingReceiptId(R, trade,
					standing.Amount / ConsignmentStandingPerDram) ||
				!KingdomPolityAuthority.Contains(C.ReceiptRefs, standing.ReceiptId))
				return Fail("fulfilled correspondence standing is not quantity-derived", out Failure);
			if (C.RelationDeltas.Count == 0) return C.ReceiptRefs.Count == 4 ||
				Fail("fulfilled correspondence has an extra relation receipt", out Failure);
			KingdomPolityRelation relation = Relation(L, R.CounterpartyPolityId,
				R.CurrentPolityId);
			KingdomPolityRelationDelta delta = C.RelationDeltas[0];
			if (relation == null || delta.RelationId != relation.RelationId ||
				delta.Before != KingdomPolityRelationBand.Contact || delta.After !=
				KingdomPolityRelationBand.Neutral || delta.ReceiptId != RelationReceiptId(
					delta.RelationId, trade, C.ObservedFactIds[0]) ||
				!KingdomPolityAuthority.Contains(C.ReceiptRefs, delta.ReceiptId) ||
				C.ReceiptRefs.Count != 5)
				return Fail("fulfilled correspondence relation step is not exact", out Failure);
			return true;
		}

		private static bool ReceiptMatchesConclusion(KingdomPolityIncidentRecord Plan,
			KingdomPolityConsignmentRequest R, KingdomTradePolityConsignmentReceipt P,
			KingdomPolityCorrespondenceReplyKind Reply)
		{
			bool landed = P.Kind == KingdomTradePolityConsignmentReceiptKind.Landed;
			if (landed != (Reply == KingdomPolityCorrespondenceReplyKind.Fulfilled) ||
				!landed && Reply != KingdomPolityCorrespondenceReplyKind.Unfulfilled ||
				!KingdomPolityAuthority.Contains(Plan.Conclusion.ReceiptRefs, P.ReceiptId) ||
				!KingdomPolityAuthority.Contains(Plan.Conclusion.ReceiptRefs,
					TradeOperationProofRef(P)))
				return false;
			string fact = landed ? Id("taf:fact:witnessed:polity-consignment:v1:",
				"fulfilled-fact", R.CorrespondencePlanId, P.ReceiptDigest) : Id(
				"taf:fact:witnessed:polity-consignment-failed:v1:", "unfulfilled-fact",
				R.CorrespondencePlanId, P.ReceiptDigest);
			return Plan.Conclusion.ObservedFactIds.Count == 1 &&
				Plan.Conclusion.ObservedFactIds[0] == fact;
		}

		private static bool CanApplyConsignmentRelationship(KingdomPolityLedger L,
			KingdomPolityConsignmentRequest R, KingdomPolityIncidentConclusion C,
			out string Failure)
		{
			Failure = null; KingdomPolityRelation relation = Relation(L,
				R.CounterpartyPolityId, R.CurrentPolityId);
			if (relation == null) return Fail("consignment relation is absent", out Failure);
			string fact = C.ObservedFactIds[0];
			if (!KingdomPolityAuthority.Contains(relation.SourceRefs, fact) &&
				relation.SourceRefs.Count >= KingdomPolityRules.MaxRefs)
				return Fail("consignment relation evidence is full", out Failure);
			if (C.RelationDeltas.Count == 0) return true;
			if (relation.Band == C.RelationDeltas[0].Before &&
				C.RelationDeltas[0].RelationId == relation.RelationId) return true;
			return Fail("consignment relation changed before its exact credit CAS", out Failure);
		}

		private static void ApplyConsignmentRelationship(KingdomPolityLedger L,
			KingdomPolityConsignmentRequest R, KingdomPolityIncidentConclusion C, long Tick)
		{
			KingdomPolityRelation relation = Relation(L, R.CounterpartyPolityId,
				R.CurrentPolityId);
			if (C.RelationDeltas.Count == 1) relation.Band = C.RelationDeltas[0].After;
			relation.ChangedTick = Tick;
			KingdomPolityAuthority.AddSortedUnique(relation.SourceRefs, C.ObservedFactIds[0]);
		}

		private static string SinglePrefix(IList<string> Values, string Prefix)
		{
			string result = null;
			for (int i = 0; Values != null && i < Values.Count; i++)
				if (Values[i].StartsWith(Prefix, StringComparison.Ordinal))
				{
					if (result != null) return null; result = Values[i];
				}
			return result;
		}

		private static KingdomPolitySystemicDelta CopySystemic(KingdomPolitySystemicDelta V)
		{
			return new KingdomPolitySystemicDelta { Kind = V.Kind, TargetId = V.TargetId,
				Amount = V.Amount, ReceiptId = V.ReceiptId };
		}

		private static KingdomPolityRelationDelta CopyRelation(KingdomPolityRelationDelta V)
		{
			return new KingdomPolityRelationDelta { RelationId = V.RelationId, Before = V.Before,
				After = V.After, ReceiptId = V.ReceiptId };
		}

		private static bool ExactSystemic(IList<KingdomPolitySystemicDelta> A,
			IList<KingdomPolitySystemicDelta> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++) if (A[i].Kind != B[i].Kind ||
				A[i].TargetId != B[i].TargetId || A[i].Amount != B[i].Amount ||
				A[i].ReceiptId != B[i].ReceiptId) return false;
			return true;
		}

		private static bool ExactRelations(IList<KingdomPolityRelationDelta> A,
			IList<KingdomPolityRelationDelta> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++) if (A[i].RelationId != B[i].RelationId ||
				A[i].Before != B[i].Before || A[i].After != B[i].After ||
				A[i].ReceiptId != B[i].ReceiptId) return false;
			return true;
		}
	}
}
