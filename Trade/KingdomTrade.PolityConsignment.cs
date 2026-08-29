using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomTrade
	{
		/// <summary>Trade is sole physical custodian for one loaded, witnessed consignment.</summary>
		public static bool TryDeliverPolityConsignment(KingdomSystem System, Zone Ground,
			GameObject Recipient, KingdomPolityConsignmentRequest Request,
			out KingdomTradePolityConsignmentReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (!KingdomMaster.NewWorkAllowed(System))
			{
				Failure = "Settlement simulation is paused; no consignment was debited."; return false;
			}
			TradeLease lease;
			if (!TryEnter(System, out lease))
			{
				Failure = "Another Trade receipt is in flight; no consignment was debited."; return false;
			}
			using (lease)
			{
				return TryDeliverPolityConsignmentCore(System, Ground, Recipient,
					Request, out Receipt, out Failure);
			}
		}

		private static bool TryDeliverPolityConsignmentCore(KingdomSystem System, Zone Ground,
			GameObject Recipient, KingdomPolityConsignmentRequest Request,
			out KingdomTradePolityConsignmentReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (!Enabled)
			{
				Failure = "Trade is disabled; no new consignment was debited."; return false;
			}
			KingdomTradePolityRecipientWitness witness;
			if (!KingdomPolityVisitInteraction.TryCaptureConsignmentRecipientWitness(System,
				Recipient, Request, out witness, out Failure) || Ground == null ||
				Recipient.CurrentZone != Ground)
			{
				Failure = Failure ?? "Consignment delivery requires the exact loaded envoy."; return false;
			}
			if (!TryPreflightPolityConsignmentGround(System, Ground, Request, out Failure))
				return false;
			long now = Math.Max(0L, The.Game?.TimeTicks ?? 0L);
			KingdomTradeBook book = System.TradeBook;
			if (!KingdomTradeRules.BookUsable(book) || !string.Equals(book.RealmId,
				Request.CurrentPolityId, StringComparison.Ordinal))
			{
				Failure = "Trade authority does not belong to this polity request."; return false;
			}
			if (!KingdomTradeRules.TryInspectPolityConsignmentReceipt(book, Request,
				out Receipt, out KingdomTradePolityConsignmentReceiptKind receiptKind,
				out Failure)) return false;
			if (receiptKind == KingdomTradePolityConsignmentReceiptKind.Landed || receiptKind ==
				KingdomTradePolityConsignmentReceiptKind.TerminalFailed) return true;
			if (receiptKind == KingdomTradePolityConsignmentReceiptKind.Invalid) return false;
			if (book.OpenOperation == null)
			{
				if (!KingdomTradeRules.TryValidatePolityConsignmentPreparation(book, Request,
					Ground.ZoneID, System.City.SettlementId, System.SeatName, witness, out Failure))
					return false;
				if (!PreparePolityConsignment(System, book, Ground, Request, witness,
					now, out Failure))
					return false;
				ApplyOption(book, true, now);
			}
			else if (!KingdomTradeRules.PolityConsignmentMatches(book.OpenOperation,
				Request, System.SeatName, witness))
			{
				Failure = "Another Trade operation owns the durable receipt slot."; return false;
			}
			else ApplyOption(book, true, now);
			KingdomSurvey survey = KingdomSurvey.Take(Ground, System);
			ContinueOperation(System, book, Ground, survey, now);
			if (!KingdomTradeRules.TryInspectPolityConsignmentReceipt(book, Request,
				out Receipt, out receiptKind, out Failure)) return false;
			if (receiptKind == KingdomTradePolityConsignmentReceiptKind.Landed || receiptKind ==
				KingdomTradePolityConsignmentReceiptKind.TerminalFailed) return true;
			if (receiptKind == KingdomTradePolityConsignmentReceiptKind.Invalid) return false;
			Failure = "The exact consignment receipt is incomplete or quarantined; no reply was credited.";
			return false;
		}

		private static bool TryPreflightPolityConsignmentGround(KingdomSystem System,
			Zone Ground, KingdomPolityConsignmentRequest Request, out string Failure)
		{
			Failure = null;
			if (System == null || Ground == null || Request == null || !System.Founded ||
				System.Ledger == null || System.ClaimedZones == null ||
				!System.ClaimedZones.Contains(Ground.ZoneID) || System.City?.ZoneIds == null ||
				!System.City.ZoneIds.Contains(Ground.ZoneID) || System.City.SettlementId !=
				Request.SurfaceRef || !KingdomTradeRules.ValidName(System.SeatName))
			{
				Failure = "Consignment must start on its exact loaded owned settlement ground.";
				return false;
			}
			return true;
		}

		private static bool PreparePolityConsignment(KingdomSystem System, KingdomTradeBook Book,
			Zone Ground, KingdomPolityConsignmentRequest Request,
			KingdomTradePolityRecipientWitness Witness, long Tick, out string Failure)
		{
			Failure = null;
			KingdomTradeOperation operation = KingdomTradeRules.NewOperation(Book,
				KingdomTradeOperationKind.PolityConsignmentDelivery, Tick);
			if (operation == null)
			{
				Failure = "Trade cannot reserve another durable operation receipt."; return false;
			}
			operation.ZoneId = Ground.ZoneID;
			operation.SettlementName = System.SeatName;
			operation.SettlementId = System.City.SettlementId;
			operation.CharterId = Request.CorrespondencePlanId;
			operation.ManifestId = Request.ConsignmentId;
			operation.DealKey = Request.CounterpartyPolityId;
			operation.DealDisplayName = Request.NeedRef;
			operation.Faction = Request.RecipientCohortId;
			operation.MaterialClaim = Request.RequestDigest;
			operation.OriginId = operation.DestinationId = Request.SurfaceRef;
			operation.OriginName = operation.DestinationName = System.SeatName;
			operation.WaterDirection = KingdomTradeWaterDirection.Debit;
			operation.RequestedWater = Request.RequestedDrams;
			operation.PolityRecipient = KingdomTradeRules.ClonePolityRecipientWitness(Witness);
			return true;
		}
	}
}
