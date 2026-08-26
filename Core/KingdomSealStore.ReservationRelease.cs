using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealStore
	{
		internal bool TryWriteReceipt(KingdomSealReceipt Receipt, out string Failure)
		{
			Failure = "";
			if (!ValidReceipt(Receipt))
			{
				Failure = "the receipt is malformed";
				return false;
			}
			if (Receipt.State == KingdomSealReceiptState.Committed)
			{
				Failure = "a committed receipt requires its exact live reservation claim";
				return false;
			}
			FileStream gate;
			if (!TryLockReceipts(out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				KingdomSealReceipt existing;
				if (!TryFindReceipt(Receipt.LegacyId, out existing, out Failure))
				{
					return false;
				}
				if (existing == null)
				{
					Failure = "a reservation must be claimed atomically before it can change";
					return false;
				}
				if (existing.LineageId != Receipt.LineageId || existing.TargetGameId != Receipt.TargetGameId)
				{
					Failure = "the receipt tuple does not match the existing claim";
					return false;
				}
				if (existing.State == Receipt.State)
				{
					return true;
				}
				if (existing.State != KingdomSealReceiptState.Reserved
					|| (Receipt.State != KingdomSealReceiptState.Committed
						&& Receipt.State != KingdomSealReceiptState.Declined))
				{
					Failure = "a receipt cannot move backwards or leave a final state";
					return false;
				}
				if (Receipt.WrittenTick < existing.WrittenTick)
				{
					Failure = "a receipt's written tick cannot go backwards";
					return false;
				}
				return TryWriteReceiptFile(Receipt, true, out Failure);
			}
		}

		internal bool TryReleaseReservation(KingdomSealReceipt Receipt, out string Failure)
		{
			Failure = "";
			if (!ValidReceipt(Receipt) || Receipt.State != KingdomSealReceiptState.Reserved)
			{
				Failure = "only an exact reserved receipt can be released";
				return false;
			}
			FileStream gate;
			if (!TryLockReceipts(out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				KingdomSealReceipt existing;
				if (!TryFindReceipt(Receipt.LegacyId, out existing, out Failure))
				{
					return false;
				}
				if (existing == null)
				{
					return true;
				}
				if (existing.State != KingdomSealReceiptState.Reserved || !SameReceipt(existing, Receipt))
				{
					Failure = "the reservation changed and cannot be released by this receipt";
					return false;
				}
				KingdomSealReservationLease lease;
				if (!TryAcquireLiveClaim(existing, out lease, out Failure))
				{
					return false;
				}
				using (lease)
				{
					return TryRemoveReservationLocked(existing, out Failure);
				}
			}
		}

		internal bool TryReleaseReservation(KingdomSealReceipt Receipt,
			KingdomSealReservationLease Lease, out string Failure)
		{
			Failure = "";
			if (!ValidReceipt(Receipt) || Receipt.State != KingdomSealReceiptState.Reserved
				|| Lease == null || !Lease.IsHeld || !Lease.Matches(Receipt))
			{
				Failure = "only the live holder of an exact reserved receipt can release it";
				return false;
			}
			FileStream gate;
			if (!TryLockReceipts(out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				KingdomSealReceipt existing;
				if (!TryFindReceipt(Receipt.LegacyId, out existing, out Failure))
				{
					return false;
				}
				if (existing == null)
				{
					Lease.Dispose();
					return true;
				}
				if (existing.State != KingdomSealReceiptState.Reserved || !SameReceipt(existing, Receipt))
				{
					Failure = "the reservation changed and cannot be released by this live claim";
					return false;
				}
				if (!TryRemoveReservationLocked(existing, out Failure))
				{
					return false;
				}
				Lease.Dispose();
				return true;
			}
		}

		internal bool TryReleaseAbandonedReservation(KingdomSealReceipt Receipt,
			out bool Released, out string Failure)
		{
			Released = false;
			Failure = "";
			if (!ValidReceipt(Receipt) || Receipt.State != KingdomSealReceiptState.Reserved)
			{
				Failure = "only an exact reserved receipt can be reconciled";
				return false;
			}
			FileStream gate;
			if (!TryLockReceipts(out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				KingdomSealReceipt existing;
				if (!TryFindReceipt(Receipt.LegacyId, out existing, out Failure))
				{
					return false;
				}
				if (existing == null)
				{
					Released = true;
					return true;
				}
				if (existing.State != KingdomSealReceiptState.Reserved || !SameReceipt(existing, Receipt))
				{
					Failure = "the reservation changed while reconciliation was examining it";
					return false;
				}
				KingdomSealReservationLease lease;
				bool contended;
				if (!TryAcquireLiveClaim(existing, out lease, out contended, out Failure))
				{
					if (contended)
					{
						Failure = "";
						return true;
					}
					return false;
				}
				using (lease)
				{
					if (!TryRemoveReservationLocked(existing, out Failure))
					{
						return false;
					}
					Released = true;
					return true;
				}
			}
		}

	}
}
