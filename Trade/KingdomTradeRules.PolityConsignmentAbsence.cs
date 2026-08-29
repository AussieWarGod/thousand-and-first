using System;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeRules
	{
		/// <summary>
		/// Proves only that this exact request has never entered Trade custody. It creates no
		/// Trade receipt and cannot cancel, consume, or destroy an operation or cargo.
		/// </summary>
		internal static bool TryProveNoPolityConsignmentCustody(KingdomTradeBook Book,
			KingdomPolityConsignmentRequest Request,
			out KingdomPolityConsignmentAbsenceProof Proof, out bool CustodyOrProofExists,
			out string Failure)
		{
			Proof = null; CustodyOrProofExists = false; Failure = null;
			if (!BookUsable(Book) || !KingdomPolityCorrespondenceRules.
				TryValidateConsignmentRequestShape(Request, out Failure) ||
				!string.Equals(Book.RealmId, Request.CurrentPolityId, StringComparison.Ordinal))
			{
				Failure = Failure ?? "Trade book does not own this polity request"; return false;
			}
			if (!TryInspectPolityConsignmentReceipt(Book, Request,
				out KingdomTradePolityConsignmentReceipt _,
				out KingdomTradePolityConsignmentReceiptKind kind, out Failure)) return false;
			if (kind == KingdomTradePolityConsignmentReceiptKind.Invalid)
				return AbsenceFail(Failure ??
					"Trade consignment proof is ambiguous or invalid", out Failure);
			if (kind != KingdomTradePolityConsignmentReceiptKind.Missing)
			{
				CustodyOrProofExists = true; return true;
			}
			if (TouchesRequest(Book.OpenOperation, Request) ||
				TouchesRequest(Book.PendingRetirement, Request))
			{
				CustodyOrProofExists = true; return true;
			}
			for (int i = 0; i < Book.RecentProofs.Count; i++)
				if (TouchesRequest(Book.RecentProofs[i], Request))
				{
					CustodyOrProofExists = true; return true;
				}
			Proof = new KingdomPolityConsignmentAbsenceProof
			{
				CorrespondencePlanId = Request.CorrespondencePlanId,
				TermsPlanId = Request.TermsPlanId,
				RecipientCohortId = Request.RecipientCohortId,
				ConsignmentId = Request.ConsignmentId,
				RequestDigest = Request.RequestDigest
			};
			Proof.ProofDigest = KingdomPolityCorrespondenceRules.
				ConsignmentAbsenceDigest(Proof);
			return true;
		}

		private static bool TouchesRequest(KingdomTradeOperation Operation,
			KingdomPolityConsignmentRequest Request)
		{
			if (Operation == null || Request == null) return false;
			return Operation.CharterId == Request.CorrespondencePlanId ||
				Operation.ManifestId == Request.ConsignmentId ||
				Operation.MaterialClaim == Request.RequestDigest ||
				Operation.PolityRecipient?.RequestDigest == Request.RequestDigest;
		}

		private static bool TouchesRequest(KingdomTradeProof Proof,
			KingdomPolityConsignmentRequest Request)
		{
			if (Proof == null || Request == null) return false;
			return Proof.ManifestId == Request.ConsignmentId ||
				Proof.PolityRecipient?.RequestDigest == Request.RequestDigest;
		}

		private static bool AbsenceFail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
