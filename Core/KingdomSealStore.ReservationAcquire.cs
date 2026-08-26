using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealStore
	{
		internal bool TryClaimReservation(KingdomSealRecord Legacy, string TargetGameId, long WrittenTick,
			out KingdomSealReceipt Receipt, out KingdomSealReservationLease Lease, out string Failure)
		{
			Receipt = null;
			Lease = null;
			Failure = "";
			if (Legacy == null || Legacy.Status != KingdomSealStatus.Promoted || !Legacy.IsResolved
				|| !KingdomSealReceipt.ValidId(Legacy.LineageId) || !KingdomSealReceipt.ValidId(Legacy.LegacyId)
				|| !KingdomSealReceipt.ValidId(TargetGameId) || WrittenTick < 0L)
			{
				Failure = "the reservation does not name one valid promoted legacy and target";
				return false;
			}
			KingdomSealRecord stored = ReadSlot(LegacyPath(Legacy.LegacyId));
			if (stored == null || !SameRecord(stored, Legacy))
			{
				Failure = "the legacy is not the immutable record on disk";
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
				if (!TryFindReceipt(Legacy.LegacyId, out existing, out Failure))
				{
					return false;
				}
				if (existing != null)
				{
					if (existing.LineageId == Legacy.LineageId && existing.TargetGameId == TargetGameId
						&& (existing.State == KingdomSealReceiptState.Reserved
							|| existing.State == KingdomSealReceiptState.Committed))
					{
						if (existing.State == KingdomSealReceiptState.Reserved
							&& !TryAcquireLiveClaim(existing, out Lease, out Failure))
						{
							return false;
						}
						Receipt = existing;
						return true;
					}
					Failure = "that legacy already has a claim";
					return false;
				}
				KingdomSealReceipt created = new KingdomSealReceipt
				{
					LineageId = Legacy.LineageId,
					LegacyId = Legacy.LegacyId,
					TargetGameId = TargetGameId,
					State = KingdomSealReceiptState.Reserved,
					WrittenTick = WrittenTick
				};
				if (!TryAcquireLiveClaim(created, out Lease, out Failure))
				{
					return false;
				}
				if (!TryWriteReceiptFile(created, false, out Failure))
				{
					Lease.Dispose();
					Lease = null;
					return false;
				}
				Receipt = created;
				return true;
			}
		}

		internal bool TryAcquireReservationLease(KingdomSealReceipt Receipt,
			out KingdomSealReservationLease Lease, out string Failure)
		{
			Lease = null;
			Failure = "";
			if (!ValidReceipt(Receipt) || Receipt.State != KingdomSealReceiptState.Reserved)
			{
				Failure = "only an exact reserved receipt can hold a live claim";
				return false;
			}
			FileStream receiptsGate;
			if (!TryLockReceipts(out receiptsGate, out Failure))
			{
				return false;
			}
			using (receiptsGate)
			{
				KingdomSealReceipt existing;
				if (!TryFindReceipt(Receipt.LegacyId, out existing, out Failure))
				{
					return false;
				}
				if (!SameReceipt(existing, Receipt))
				{
					Failure = "the reservation changed before its live claim was acquired";
					return false;
				}
				return TryAcquireLiveClaim(existing, out Lease, out Failure);
			}
		}

		internal bool TryInspectReceipt(KingdomSealReceipt Expected,
			out KingdomSealReceipt Current, out string Failure)
		{
			Current = null;
			Failure = "";
			if (!ValidReceipt(Expected))
			{
				Failure = "the expected receipt is malformed";
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
				if (!TryFindReceipt(Expected.LegacyId, out existing, out Failure))
				{
					return false;
				}
				if (existing == null || existing.LineageId != Expected.LineageId
					|| existing.LegacyId != Expected.LegacyId
					|| existing.TargetGameId != Expected.TargetGameId
					|| existing.WrittenTick < Expected.WrittenTick
					|| (existing.State != Expected.State
						&& Expected.State != KingdomSealReceiptState.Reserved))
				{
					Failure = "the current receipt is not a monotone state of the expected tuple";
					return false;
				}
				Current = existing;
				return true;
			}
		}

		internal bool TryCommitReservation(KingdomSealReceipt Reserved,
			KingdomSealReservationLease Lease, long WrittenTick,
			out KingdomSealReceipt Committed, out string Failure)
		{
			Committed = null;
			Failure = "";
			if (!ValidReceipt(Reserved) || Reserved.State != KingdomSealReceiptState.Reserved
				|| Lease == null || !Lease.IsHeld || !Lease.Matches(Reserved)
				|| WrittenTick < Reserved.WrittenTick)
			{
				Failure = "only the live holder of an exact reservation can commit it monotonically";
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
				if (!TryFindReceipt(Reserved.LegacyId, out existing, out Failure))
				{
					return false;
				}
				if (existing == null || existing.LineageId != Reserved.LineageId
					|| existing.LegacyId != Reserved.LegacyId
					|| existing.TargetGameId != Reserved.TargetGameId)
				{
					Failure = "the reservation tuple changed before it could be committed";
					return false;
				}
				if (existing.State == KingdomSealReceiptState.Committed
					&& existing.WrittenTick >= Reserved.WrittenTick)
				{
					Committed = existing;
					Lease.Dispose();
					return true;
				}
				if (!SameReceipt(existing, Reserved))
				{
					Failure = "the exact reservation changed before it could be committed";
					return false;
				}
				KingdomSealReceipt committed = new KingdomSealReceipt
				{
					LineageId = Reserved.LineageId,
					LegacyId = Reserved.LegacyId,
					TargetGameId = Reserved.TargetGameId,
					State = KingdomSealReceiptState.Committed,
					WrittenTick = WrittenTick
				};
				if (!TryWriteReceiptFile(committed, true, out Failure))
				{
					return false;
				}
				Committed = committed;
				Lease.Dispose();
				return true;
			}
		}

	}
}
