using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityHospitalityRules
	{
		public const int RequiredDebitLines = 2;

		public static bool TryCreateProof(string ProofId, string SourceAuthorityId,
			string ItemOrServingId, long BeforeQuantity, long AfterQuantity, string ReceiptId,
			string ObservedFactId, long CommitTick, out KingdomPolityHospitalityProof Proof,
			out string Failure)
		{
			Proof = new KingdomPolityHospitalityProof
			{
				ProofId = ProofId, SourceAuthorityId = SourceAuthorityId,
				ItemOrServingId = ItemOrServingId, BeforeQuantity = BeforeQuantity,
				AfterQuantity = AfterQuantity, ConsumedQuantity = 1L, ReceiptId = ReceiptId,
				ObservedFactId = ObservedFactId, CommitTick = CommitTick
			};
			Proof.ProofDigest = Digest(Proof);
			if (TryValidate(Proof, out Failure)) return true;
			Proof = null; return false;
		}

		public static bool TryValidate(KingdomPolityHospitalityProof Proof, out string Failure)
		{
			Failure = null;
			if (Proof == null || !KingdomPolityRules.TypedId(Proof.ProofId,
				"taf:hospitality-proof:") ||
				!KingdomPolityRules.SemanticId(Proof.SourceAuthorityId) ||
				!KingdomPolityRules.SemanticId(Proof.ItemOrServingId) ||
				Proof.BeforeQuantity < 1L ||
				Proof.BeforeQuantity > KingdomPolityRules.MaxValueBudget ||
				Proof.AfterQuantity < 0L || Proof.ConsumedQuantity != 1L ||
				Proof.BeforeQuantity != Proof.AfterQuantity + Proof.ConsumedQuantity ||
				!KingdomPolityRules.SemanticId(Proof.ReceiptId) ||
				!KingdomPolityRules.TypedId(Proof.ObservedFactId, "taf:fact:witnessed:") ||
				Proof.CommitTick < 0L || !KingdomPolityRules.Digest(Proof.ProofDigest) ||
				Proof.ProofDigest != Digest(Proof))
			{
				Failure = "hospitality proof is not one exact witnessed consumption"; return false;
			}
			return true;
		}

		public static bool TryCreateTransaction(string TermsPlanId,
			KingdomPolityHospitalityPlanRequest Request,
			out KingdomPolityHospitalityTransaction Transaction, out string Failure)
		{
			Transaction = null;
			Failure = null;
			if (!KingdomPolityRules.TypedId(TermsPlanId, "taf:incident-plan:") ||
				Request == null || !KingdomPolityRules.SemanticId(Request.SurfaceRef) ||
				!KingdomPolityRules.Text(Request.ZoneId, true) || Request.PlannedTick < 0L ||
				!ValidLines(Request.Lines))
				return Fail("hospitality plan lacks exact food and water custody", out Failure);
			string id = KingdomPolityRules.ActivationId("taf:hospitality:v1:",
				"polity-hospitality-transaction-v1", TermsPlanId, Request.SurfaceRef,
				Request.ZoneId);
			Transaction = new KingdomPolityHospitalityTransaction
			{
				TransactionId = id, TermsPlanId = TermsPlanId,
				SurfaceRef = Request.SurfaceRef, ZoneId = Request.ZoneId,
				Phase = KingdomPolityHospitalityPhase.Planned,
				PlannedTick = Request.PlannedTick,
				Lines = CopyLines(Request.Lines)
			};
			Transaction.PlanDigest = PlanDigest(Transaction);
			if (TryValidateTransaction(Transaction, TermsPlanId, null, out Failure)) return true;
			Transaction = null;
			return false;
		}

		public static bool TryCreateCommittedProof(KingdomPolityHospitalityTransaction Transaction,
			string ObservedFactId, long CommitTick, out KingdomPolityHospitalityProof Proof,
			out string Failure)
		{
			Proof = null;
			Failure = null;
			if (Transaction == null || Transaction.Phase != KingdomPolityHospitalityPhase.Planned ||
				!TryValidateTransaction(Transaction, Transaction.TermsPlanId, null, out Failure) ||
				CommitTick < Transaction.PlannedTick)
				return Fail(Failure ?? "hospitality debit is not a frozen plan", out Failure);
			string serving = KingdomPolityRules.ActivationId("taf:serving:hospitality:v1:",
				"polity-hospitality-serving-v1", Transaction.TransactionId,
				Transaction.PlanDigest);
			string proof = KingdomPolityRules.ActivationId("taf:hospitality-proof:",
				"polity-hospitality-proof-id-v1", Transaction.TransactionId,
				ObservedFactId, CommitTick.ToString(CultureInfo.InvariantCulture));
			string receipt = KingdomPolityRules.ActivationId("taf:receipt:hospitality:v1:",
				"polity-hospitality-debit-receipt-v1", proof, Transaction.PlanDigest);
			return TryCreateProof(proof, Transaction.TransactionId, serving, 1L, 0L,
				receipt, ObservedFactId, CommitTick, out Proof, out Failure);
		}

		internal static bool TryValidateTransaction(KingdomPolityHospitalityTransaction T,
			string TermsPlanId, KingdomPolityIncidentConclusion Conclusion, out string Failure)
		{
			Failure = null;
			if (T == null || !KingdomPolityRules.TypedId(T.TransactionId, "taf:hospitality:v1:") ||
				T.TermsPlanId != TermsPlanId || !KingdomPolityRules.SemanticId(T.SurfaceRef) ||
				!KingdomPolityRules.Text(T.ZoneId, true) || T.PlannedTick < 0L ||
				T.DebitedTick < 0L || !ValidLines(T.Lines) ||
				T.TransactionId != KingdomPolityRules.ActivationId("taf:hospitality:v1:",
					"polity-hospitality-transaction-v1", TermsPlanId, T.SurfaceRef, T.ZoneId) ||
				!KingdomPolityRules.Digest(T.PlanDigest) || T.PlanDigest != PlanDigest(T) ||
				(byte)T.Phase > (byte)KingdomPolityHospitalityPhase.Quarantined ||
				!KingdomPolityRules.Text(T.Fault, false))
				return Fail("hospitality transaction is invalid", out Failure);
			bool paid = T.Phase == KingdomPolityHospitalityPhase.Debited ||
				T.Phase == KingdomPolityHospitalityPhase.Applied;
			if (paid != (T.Proof != null) || (paid && T.DebitedTick < T.PlannedTick) ||
				(!paid && T.DebitedTick != 0L) ||
				(paid && (!TryValidate(T.Proof, out Failure) ||
				 !ProofMatches(T, T.Proof, T.DebitedTick))))
				return Fail(Failure ?? "hospitality payment proof is incoherent", out Failure);
			if ((T.Phase == KingdomPolityHospitalityPhase.Quarantined) !=
				!string.IsNullOrEmpty(T.Fault))
				return Fail("hospitality quarantine evidence is incoherent", out Failure);
			if (T.Phase == KingdomPolityHospitalityPhase.Applied)
				return Conclusion != null && KingdomPolityAuthority.Contains(
					Conclusion.ObservedFactIds, T.Proof.ObservedFactId) &&
					KingdomPolityAuthority.Contains(Conclusion.ReceiptRefs, T.Proof.ReceiptId) ||
					Fail("applied hospitality is absent from the terms conclusion", out Failure);
			return Conclusion == null || T.Phase == KingdomPolityHospitalityPhase.Abandoned ||
				T.Phase == KingdomPolityHospitalityPhase.Quarantined ||
				Fail("open hospitality survived a terms conclusion", out Failure);
		}

		internal static bool ProofMatches(KingdomPolityHospitalityTransaction T,
			KingdomPolityHospitalityProof P, long Tick)
		{
			if (T == null || P == null || P.SourceAuthorityId != T.TransactionId ||
				P.CommitTick != Tick) return false;
			return TryCreateCommittedProof(CloneAsPlanned(T), P.ObservedFactId, Tick,
				out KingdomPolityHospitalityProof expected, out string _) &&
				expected.ProofDigest == P.ProofDigest && expected.ProofId == P.ProofId &&
				expected.ReceiptId == P.ReceiptId;
		}

		private static string Digest(KingdomPolityHospitalityProof P)
		{
			return KingdomPolityRules.ActivationDigest("polity-hospitality-proof-v1",
				P.ProofId ?? "", P.SourceAuthorityId ?? "", P.ItemOrServingId ?? "",
				P.BeforeQuantity.ToString(CultureInfo.InvariantCulture),
				P.AfterQuantity.ToString(CultureInfo.InvariantCulture),
				P.ConsumedQuantity.ToString(CultureInfo.InvariantCulture), P.ReceiptId ?? "",
				P.ObservedFactId ?? "", P.CommitTick.ToString(CultureInfo.InvariantCulture));
		}
	}
}
