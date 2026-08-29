using System;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeRules
	{
		private static bool TryClassifyPolityConsignmentProof(KingdomTradeBook Book,
			KingdomTradeProof Proof, KingdomPolityConsignmentRequest Request,
			out KingdomTradePolityConsignmentReceiptKind Kind, out string Failure)
		{
			Kind = KingdomTradePolityConsignmentReceiptKind.Invalid; Failure = null;
			string witnessFailure;
			bool witness = TryValidatePolityRecipientWitnessShape(
				Proof?.PolityRecipient, out witnessFailure);
			if (!ValidProof(Book, Proof, true) || Proof.RequestedWater !=
				Request.RequestedDrams || Proof.AmbiguousWater != 0 || !string.Equals(
					Proof.SettlementId, Request.SurfaceRef, StringComparison.Ordinal) ||
				Proof.ManifestEscrowBefore != 0 || Proof.ManifestEscrowDebit != 0 ||
				Proof.ManifestEscrowAfter != 0 || !witness || Proof.PolityRecipient.CohortId !=
				Request.RecipientCohortId || Proof.PolityRecipient.SurfaceRef !=
				Request.SurfaceRef || Proof.PolityRecipient.RequestDigest != Request.RequestDigest)
			{
				Failure = witnessFailure ?? "Trade consignment proof changed or is foreign";
				return false;
			}
			if (Proof.Disposition == KingdomTradePhase.Terminal && Proof.ProvedWater >= 1 &&
				Proof.ProvedWater <= Proof.RequestedWater && Proof.RetainedBefore == 0L &&
				Proof.RetainedDelta == 0L && Proof.RetainedAfter == 0L && Proof.RetainedState ==
				KingdomTradePhysicalState.None && string.IsNullOrEmpty(Proof.Fault))
			{
				Kind = KingdomTradePolityConsignmentReceiptKind.Landed; return true;
			}
			bool zero = Proof.ProvedWater == 0 && Proof.RetainedBefore == 0L &&
				Proof.RetainedDelta == 0L && Proof.RetainedAfter == 0L &&
				Proof.RetainedState == KingdomTradePhysicalState.None;
			bool retained = Proof.ProvedWater > 0 && Proof.RetainedDelta == Proof.ProvedWater &&
				Proof.RetainedState == KingdomTradePhysicalState.Proved &&
				Proof.RetainedAfter == Proof.RetainedBefore + Proof.RetainedDelta;
			if (Proof.Disposition == KingdomTradePhase.Quarantined && (zero || retained) &&
				!string.IsNullOrEmpty(Proof.Fault))
			{
				Kind = KingdomTradePolityConsignmentReceiptKind.TerminalFailed; return true;
			}
			Failure = "Trade consignment proof has no exact landed or terminal-failed shape";
			return false;
		}

		private static KingdomTradePolityConsignmentReceipt BuildPolityConsignmentReceipt(
			KingdomTradeProof Proof, KingdomPolityConsignmentRequest Request,
			KingdomTradePolityConsignmentReceiptKind Kind)
		{
			bool landed = Kind == KingdomTradePolityConsignmentReceiptKind.Landed;
			KingdomTradePolityConsignmentReceipt receipt =
				new KingdomTradePolityConsignmentReceipt
				{
					Kind = Kind, TradeOperationId = Proof.Id,
					TradeEvidenceHash = Proof.OperationEvidenceHash,
					ConsignmentId = Request.ConsignmentId,
					CorrespondencePlanId = Request.CorrespondencePlanId,
					CounterpartyPolityId = Request.CounterpartyPolityId,
					SurfaceRef = Request.SurfaceRef,
					RecipientBodyId = Proof.PolityRecipient.BodyId,
					RecipientCohortId = Proof.PolityRecipient.CohortId,
					RecipientProjectionId = Proof.PolityRecipient.ProjectionId,
					RecipientWitnessDigest = Proof.PolityRecipient.WitnessDigest,
					RequestedDrams = Proof.RequestedWater,
					DebitedDrams = Proof.ProvedWater,
					DeliveredDrams = landed ? Proof.ProvedWater : 0,
					RetainedDrams = landed ? 0 : Proof.ProvedWater,
					FailureText = landed ? null : Proof.Fault,
					CommitTick = Proof.Tick
				};
			receipt.ReceiptDigest = KingdomPolityCorrespondenceRules.TradeReceiptDigest(receipt);
			receipt.ReceiptId = KingdomPolityCorrespondenceRules.TradeReceiptId(receipt);
			return receipt;
		}
	}
}
