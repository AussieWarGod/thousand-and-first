using System;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeRules
	{
		public static bool TryReadPolityConsignmentReceipt(KingdomTradeBook Book,
			KingdomPolityConsignmentRequest Request,
			out KingdomTradePolityConsignmentReceipt Receipt, out string Failure)
		{
			if (!TryInspectPolityConsignmentReceipt(Book, Request, out Receipt,
				out KingdomTradePolityConsignmentReceiptKind kind, out Failure)) return false;
			return kind == KingdomTradePolityConsignmentReceiptKind.Landed ||
				kind == KingdomTradePolityConsignmentReceiptKind.TerminalFailed ||
				FailMissing(out Failure);
		}

		public static bool TryInspectPolityConsignmentReceipt(KingdomTradeBook Book,
			KingdomPolityConsignmentRequest Request,
			out KingdomTradePolityConsignmentReceipt Receipt,
			out KingdomTradePolityConsignmentReceiptKind Kind, out string Failure)
		{
			Receipt = null; Kind = KingdomTradePolityConsignmentReceiptKind.Invalid;
			Failure = null;
			if (!BookUsable(Book) || !KingdomPolityCorrespondenceRules.
				TryValidateConsignmentRequestShape(Request, out Failure) ||
				!string.Equals(Book.RealmId, Request.CurrentPolityId, StringComparison.Ordinal))
			{
				Failure = Failure ?? "Trade book does not own this polity request"; return false;
			}
			KingdomTradeProof exact = null; int matches = 0;
			for (int i = 0; i < Book.RecentProofs.Count; i++)
			{
				KingdomTradeProof proof = Book.RecentProofs[i];
				if (proof == null || proof.Kind != KingdomTradeOperationKind.PolityConsignmentDelivery ||
					!string.Equals(proof.ManifestId, Request.ConsignmentId,
						StringComparison.Ordinal)) continue;
				matches++; exact = proof;
			}
			if (matches == 0)
			{
				Kind = KingdomTradePolityConsignmentReceiptKind.Missing; return true;
			}
			if (matches != 1 || !TryClassifyPolityConsignmentProof(Book, exact, Request,
				out Kind, out Failure))
			{
				Kind = KingdomTradePolityConsignmentReceiptKind.Invalid;
				Failure = matches > 1 ? "Trade consignment proof is duplicated" :
					(Failure ?? "Trade consignment proof is invalid");
				return true;
			}
			Receipt = BuildPolityConsignmentReceipt(exact, Request, Kind);
			if (!KingdomPolityCorrespondenceRules.TryValidateTradeReceipt(Request,
				Receipt, out Failure))
			{
				Receipt = null; Kind = KingdomTradePolityConsignmentReceiptKind.Invalid;
				return true;
			}
			return true;
		}

		public static bool TryFindPolityConsignmentReceipt(KingdomTradeBook Book,
			KingdomPolityConsignmentRequest Request,
			out KingdomTradePolityConsignmentReceipt Receipt, out bool Found,
			out string Failure)
		{
			if (!TryInspectPolityConsignmentReceipt(Book, Request, out Receipt,
				out KingdomTradePolityConsignmentReceiptKind kind, out Failure))
			{
				Found = false; return false;
			}
			Found = kind == KingdomTradePolityConsignmentReceiptKind.Landed ||
				kind == KingdomTradePolityConsignmentReceiptKind.TerminalFailed;
			if (kind == KingdomTradePolityConsignmentReceiptKind.Invalid) return false;
			return true;
		}

		private static bool FailMissing(out string Failure)
		{
			Failure = "Trade has no exact terminal consignment proof for this request";
			return false;
		}

		internal static bool PolityConsignmentMatches(KingdomTradeOperation Operation,
			KingdomPolityConsignmentRequest Request, string SettlementName,
			KingdomTradePolityRecipientWitness Witness)
		{
			return Operation != null && Request != null &&
				Operation.Kind == KingdomTradeOperationKind.PolityConsignmentDelivery &&
				Operation.CharterId == Request.CorrespondencePlanId &&
				Operation.ManifestId == Request.ConsignmentId &&
				Operation.DealKey == Request.CounterpartyPolityId &&
				Operation.DealDisplayName == Request.NeedRef &&
				Operation.Faction == Request.RecipientCohortId &&
				Operation.MaterialClaim == Request.RequestDigest &&
				Operation.SettlementId == Request.SurfaceRef &&
				Operation.OriginId == Request.SurfaceRef &&
				Operation.DestinationId == Request.SurfaceRef &&
				Operation.SettlementName == SettlementName &&
				Operation.OriginName == SettlementName && Operation.DestinationName == SettlementName &&
				Operation.WaterDirection == KingdomTradeWaterDirection.Debit &&
				Operation.RequestedWater == Request.RequestedDrams &&
				TryValidatePolityRecipientWitnessShape(Operation.PolityRecipient,
					out string _) && ExactPolityRecipientWitness(Operation.PolityRecipient, Witness) &&
				Operation.PolityRecipient.CohortId == Request.RecipientCohortId &&
				Operation.PolityRecipient.SurfaceRef == Request.SurfaceRef &&
				Operation.PolityRecipient.RequestDigest == Request.RequestDigest;
		}

		private static bool ValidPolityConsignmentOperation(KingdomTradeOperation Operation)
		{
			if (Operation == null) return false;
			if (Operation.Kind != KingdomTradeOperationKind.PolityConsignmentDelivery)
				return Operation.PolityRecipient == null;
			return Operation.CharterId != null && Operation.CharterId.StartsWith(
				"taf:incident-plan:correspondence:v1:", StringComparison.Ordinal) &&
				Operation.ManifestId != null && Operation.ManifestId.StartsWith(
					"taf:manifest:polity-consignment:v1:", StringComparison.Ordinal) &&
				ValidName(Operation.DealKey) && ValidName(Operation.DealDisplayName) &&
				Operation.DealDisplayName.StartsWith("taf:need:polity-water:v1:",
					StringComparison.Ordinal) && ValidName(Operation.Faction) &&
				Operation.Faction.StartsWith("taf:cohort:", StringComparison.Ordinal) &&
				CanonicalSha256(Operation.MaterialClaim) && Operation.Cycles == 0 &&
				Operation.IncomePerCycle == 0 && Operation.IntervalTicks == 0L &&
				Operation.DueBefore == 0L && Operation.DueAfter == 0L &&
				string.IsNullOrEmpty(Operation.CaravanBlueprint) &&
				string.IsNullOrEmpty(Operation.ProjectionId) &&
				string.IsNullOrEmpty(Operation.PriorProjectionId) &&
				Operation.MaterialRequested == 0 && Operation.MaterialProved == 0 &&
				Operation.MaterialOutputs.Count == 0 && Operation.ManifestLoadedTick == 0L &&
				Operation.ManifestDeadlineTick == 0L && Operation.Standing == null &&
				Operation.SettlementId == Operation.OriginId &&
				Operation.SettlementId == Operation.DestinationId &&
				Operation.SettlementName == Operation.OriginName &&
				Operation.SettlementName == Operation.DestinationName &&
				Operation.RequestedWater == KingdomPolityCorrespondenceRules.FirstContactWaterDrams &&
				TryValidatePolityRecipientWitnessShape(Operation.PolityRecipient,
					out string _) && Operation.PolityRecipient.CohortId == Operation.Faction &&
				Operation.PolityRecipient.SurfaceRef == Operation.SettlementId &&
				Operation.PolityRecipient.RequestDigest == Operation.MaterialClaim;
		}

		private static bool InvalidWaterDirection(KingdomTradeOperation Operation)
		{
			if (Operation == null) return true;
			if (Operation.Kind == KingdomTradeOperationKind.ManifestLoad || Operation.Kind ==
				KingdomTradeOperationKind.PolityConsignmentDelivery)
				return Operation.WaterDirection != KingdomTradeWaterDirection.Debit;
			if (Operation.Kind == KingdomTradeOperationKind.CharterDelivery || Operation.Kind ==
				KingdomTradeOperationKind.ManifestDelivery)
				return Operation.WaterDirection != KingdomTradeWaterDirection.Credit;
			return (Operation.Kind == KingdomTradeOperationKind.ManifestTurnback || Operation.Kind ==
				KingdomTradeOperationKind.ManifestLapse) &&
				Operation.WaterDirection != KingdomTradeWaterDirection.None;
		}
	}
}
