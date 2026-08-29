using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeRules
	{
		/// <summary>
		/// Moves one exact consumed D2 proof into authenticated retirement compaction.
		/// Polity's durable conclusion is the sole deletion authority.
		/// </summary>
		public static bool TryAcknowledgePolityConsignment(KingdomTradeBook Book,
			KingdomPolityLedger Ledger, KingdomPolityConsignmentRequest Request,
			KingdomTradePolityConsignmentReceipt Receipt, out bool Changed,
			out string Failure)
		{
			Changed = false; Failure = null;
			if (!KingdomPolityCorrespondenceRules.TryValidateConsumedTradeReceipt(
				Ledger, Request, Receipt, out Failure) || !BookUsable(Book) ||
				Book.RealmId != Request.CurrentPolityId)
			{
				Failure = Failure ?? "Trade acknowledgement authority is invalid or foreign";
				return false;
			}
			int index = -1, matches = 0;
			for (int i = 0; i < Book.RecentProofs.Count; i++)
			{
				KingdomTradeProof row = Book.RecentProofs[i];
				if (row == null || row.Id != Receipt.TradeOperationId &&
					row.ManifestId != Request.ConsignmentId) continue;
				matches++; index = i;
			}
			// A valid conclusion with no readable proof is the crash-after-compaction retry.
			if (matches == 0) return true;
			if (matches != 1) return AckFail(
				"Trade acknowledgement proof is duplicated or colliding", out Failure);
			if (!TryInspectPolityConsignmentReceipt(Book, Request,
				out KingdomTradePolityConsignmentReceipt exact,
				out KingdomTradePolityConsignmentReceiptKind kind, out Failure) ||
				(kind != KingdomTradePolityConsignmentReceiptKind.Landed && kind !=
					KingdomTradePolityConsignmentReceiptKind.TerminalFailed) ||
				!ExactPolityConsignmentReceipt(exact, Receipt))
				return AckFail(Failure ??
					"Trade acknowledgement proof differs from the consumed reply", out Failure);
			List<KingdomTradeProof> proofs = new List<KingdomTradeProof>
				{ Book.RecentProofs[index] };
			List<int> indexes = new List<int> { index };
			if (!TryCompactProofRows(Book, proofs, indexes, out Failure)) return false;
			Changed = true; return true;
		}

		private static bool ExactPolityConsignmentReceipt(
			KingdomTradePolityConsignmentReceipt A,
			KingdomTradePolityConsignmentReceipt B)
		{
			return A != null && B != null && A.Kind == B.Kind && A.ReceiptId == B.ReceiptId &&
				A.TradeOperationId == B.TradeOperationId && A.TradeEvidenceHash ==
				B.TradeEvidenceHash && A.ConsignmentId == B.ConsignmentId &&
				A.CorrespondencePlanId == B.CorrespondencePlanId &&
				A.CounterpartyPolityId == B.CounterpartyPolityId && A.SurfaceRef ==
				B.SurfaceRef && A.RecipientBodyId == B.RecipientBodyId &&
				A.RecipientCohortId == B.RecipientCohortId && A.RecipientProjectionId ==
				B.RecipientProjectionId && A.RecipientWitnessDigest ==
				B.RecipientWitnessDigest && A.RequestedDrams == B.RequestedDrams &&
				A.DebitedDrams == B.DebitedDrams && A.DeliveredDrams == B.DeliveredDrams &&
				A.RetainedDrams == B.RetainedDrams && A.FailureText == B.FailureText &&
				A.CommitTick == B.CommitTick && A.ReceiptDigest == B.ReceiptDigest;
		}

		private static bool AckFail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
