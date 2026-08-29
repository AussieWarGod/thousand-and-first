namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputRules
	{
		private static bool ParentCoherent(KingdomConstructionInputReceipt receipt)
		{
			switch (receipt.TxPhase)
			{
			case KingdomConstructionInputTxPhase.ReservationPrepared:
			case KingdomConstructionInputTxPhase.Reserved:
				return SourcesAre(receipt, InitialSource) && CargoAre(receipt, InitialCargo)
					&& InitialMutableEmpty(receipt);
			case KingdomConstructionInputTxPhase.SourcePending:
				return SourcesAre(receipt, SourcePreparing) && CargoAre(receipt, CargoPreparing);
			case KingdomConstructionInputTxPhase.Routing:
				return SourcesAre(receipt, SourceDebited)
					&& CargoAre(receipt, CargoRouting);
			case KingdomConstructionInputTxPhase.LandedAwaitingOwner:
				return SourcesAre(receipt, SourceDebited)
					&& CargoAre(receipt, CargoLanded);
			case KingdomConstructionInputTxPhase.DebitPending:
				return SourcesAre(receipt, SourceDebitClosing)
					&& CargoAre(receipt, CargoDebitClosing);
			case KingdomConstructionInputTxPhase.Closing:
			case KingdomConstructionInputTxPhase.Committed:
				return SourcesAre(receipt, SourceSpent) && CargoAre(receipt, CargoSpent)
					&& NoUnreplacedLoss(receipt);
			case KingdomConstructionInputTxPhase.RollbackPending:
				return SourcesAre(receipt, SourceRollingBack)
					&& CargoAre(receipt, CargoRollingBack);
			case KingdomConstructionInputTxPhase.RolledBack:
				return SourcesAre(receipt, SourceRolledBack)
					&& CargoAre(receipt, CargoRolledBack);
			case KingdomConstructionInputTxPhase.CompensationPending:
				return SourcesAre(receipt, SourceCompensating)
					&& CargoAre(receipt, CargoCompensating);
			case KingdomConstructionInputTxPhase.Compensated:
				return SourcesAre(receipt, SourceCompensated)
					&& CargoAre(receipt, CargoCompensated);
			case KingdomConstructionInputTxPhase.Quarantined:
				return true;
			case KingdomConstructionInputTxPhase.CancellationPending:
				return SourcesAre(receipt, SourceCancelling)
					&& CargoAre(receipt, CargoCancelling) && NoUnreplacedLoss(receipt);
			case KingdomConstructionInputTxPhase.Cancelled:
				return SourcesAre(receipt, SourceCancelled)
					&& CargoAre(receipt, CargoCancelled) && NoUnreplacedLoss(receipt);
			default:
				return false;
			}
		}

		private static bool ParentReady(KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputTxPhase next)
		{
			switch (next)
			{
			case KingdomConstructionInputTxPhase.Reserved:
				return receipt.TxPhase == KingdomConstructionInputTxPhase.ReservationPrepared;
			case KingdomConstructionInputTxPhase.SourcePending:
				return receipt.TxPhase == KingdomConstructionInputTxPhase.Reserved;
			case KingdomConstructionInputTxPhase.Routing:
				return receipt.TxPhase == KingdomConstructionInputTxPhase.SourcePending
					&& SourcesAre(receipt, SourceDebited) && CargoAre(receipt, CargoInFlight)
					&& NoUnreplacedLoss(receipt);
			case KingdomConstructionInputTxPhase.LandedAwaitingOwner:
				return receipt.TxPhase == KingdomConstructionInputTxPhase.Routing
					&& CargoAre(receipt, CargoLanded) && NoUnreplacedLoss(receipt);
			case KingdomConstructionInputTxPhase.DebitPending:
				return receipt.TxPhase == KingdomConstructionInputTxPhase.LandedAwaitingOwner
					&& NoUnreplacedLoss(receipt);
			case KingdomConstructionInputTxPhase.Closing:
				return receipt.TxPhase == KingdomConstructionInputTxPhase.DebitPending
					&& SourcesAre(receipt, SourceSpent) && CargoAre(receipt, CargoSpent)
					&& NoUnreplacedLoss(receipt);
			case KingdomConstructionInputTxPhase.Committed:
				return receipt.TxPhase == KingdomConstructionInputTxPhase.Closing;
			case KingdomConstructionInputTxPhase.RollbackPending:
				return (receipt.TxPhase == KingdomConstructionInputTxPhase.ReservationPrepared
					|| receipt.TxPhase == KingdomConstructionInputTxPhase.Reserved
					|| receipt.TxPhase == KingdomConstructionInputTxPhase.SourcePending)
					&& SourcesAre(receipt, SourceRollbackEligible)
					&& CargoAre(receipt, CargoRollbackEligible);
			case KingdomConstructionInputTxPhase.RolledBack:
				return receipt.TxPhase == KingdomConstructionInputTxPhase.RollbackPending
					&& SourcesAre(receipt, SourceRolledBack) && CargoAre(receipt, CargoRolledBack);
			case KingdomConstructionInputTxPhase.CompensationPending:
				return ((receipt.TxPhase == KingdomConstructionInputTxPhase.SourcePending
						&& CargoAny(receipt, KingdomConstructionInputCargoPhase.InFlight))
					|| receipt.TxPhase == KingdomConstructionInputTxPhase.Routing
					|| receipt.TxPhase == KingdomConstructionInputTxPhase.LandedAwaitingOwner
					|| receipt.TxPhase == KingdomConstructionInputTxPhase.DebitPending)
					&& SourcesAre(receipt, SourceDebited) && CargoAre(receipt, CargoCompensationEligible);
			case KingdomConstructionInputTxPhase.Compensated:
				return receipt.TxPhase == KingdomConstructionInputTxPhase.CompensationPending
					&& SourcesAre(receipt, SourceCompensated) && CargoAre(receipt, CargoCompensated);
			case KingdomConstructionInputTxPhase.Quarantined:
				return !Terminal(receipt.TxPhase);
			case KingdomConstructionInputTxPhase.CancellationPending:
				return receipt.TxPhase >= KingdomConstructionInputTxPhase.ReservationPrepared
					&& receipt.TxPhase <= KingdomConstructionInputTxPhase.DebitPending
					&& !SourcesAny(receipt, KingdomConstructionInputSourcePhase.Spent)
					&& !CargoAny(receipt, KingdomConstructionInputCargoPhase.DebitIntent)
					&& !CargoAny(receipt, KingdomConstructionInputCargoPhase.Spent)
					&& NoUnreplacedLoss(receipt);
			case KingdomConstructionInputTxPhase.Cancelled:
				return receipt.TxPhase == KingdomConstructionInputTxPhase.CancellationPending
					&& SourcesAre(receipt, SourceCancelled)
					&& CargoAre(receipt, CargoCancelled) && NoUnreplacedLoss(receipt);
			default:
				return false;
			}
		}

		private delegate bool SourceLaw(KingdomConstructionInputSourcePhase phase);
		private delegate bool CargoLaw(KingdomConstructionInputCargoPhase phase);
		private static bool SourcesAre(KingdomConstructionInputReceipt r, SourceLaw law)
		{ for (int i = 0; i < r.SourceCount; i++) if (!law(r.SourceAt(i).Phase)) return false; return true; }
		private static bool CargoAre(KingdomConstructionInputReceipt r, CargoLaw law)
		{ for (int i = 0; i < r.CargoCount; i++) if (!law(r.CargoAt(i).Phase)) return false; return true; }
		private static bool CargoAny(KingdomConstructionInputReceipt r, KingdomConstructionInputCargoPhase phase)
		{ for (int i = 0; i < r.CargoCount; i++) if (r.CargoAt(i).Phase == phase) return true; return false; }
		private static bool SourcesAny(KingdomConstructionInputReceipt r,
			KingdomConstructionInputSourcePhase phase)
		{ for (int i = 0; i < r.SourceCount; i++) if (r.SourceAt(i).Phase == phase) return true; return false; }
		private static bool NoUnreplacedLoss(KingdomConstructionInputReceipt r)
		{
			for (int i = 0; i < r.SourceCount; i++) if (r.SourceAt(i).ProvedLost != 0) return false;
			for (int i = 0; i < r.CargoCount; i++)
				if (r.CargoAt(i).Lost != 0) return false;
			return true;
		}
		private static bool InitialMutableEmpty(KingdomConstructionInputReceipt r)
		{
			for (int i = 0; i < r.SourceCount; i++)
			{
				KingdomConstructionInputSourceLine x = r.SourceAt(i);
				if (!string.IsNullOrEmpty(x.RemainderObjectId)
					|| !string.IsNullOrEmpty(x.BeforeWitnessHash)
					|| !string.IsNullOrEmpty(x.AfterWitnessHash) || x.ProvedLost != 0) return false;
			}
			for (int i = 0; i < r.CargoCount; i++)
			{
				KingdomConstructionInputCargoLine x = r.CargoAt(i);
				if (!string.IsNullOrEmpty(x.ObjectId)
					|| x.CustodyTopology != KingdomConstructionInputTopology.Invalid
					|| !string.IsNullOrEmpty(x.CustodyOwnerId)
					|| !string.IsNullOrEmpty(x.CustodyZoneId) || x.CustodyX != -1 || x.CustodyY != -1
					|| !string.IsNullOrEmpty(x.BeforeWitnessHash)
					|| !string.IsNullOrEmpty(x.AfterWitnessHash) || x.Spent != 0 || x.Lost != 0) return false;
			}
			return true;
		}

		private static bool InitialSource(KingdomConstructionInputSourcePhase p) { return p == KingdomConstructionInputSourcePhase.Reserved; }
		private static bool InitialCargo(KingdomConstructionInputCargoPhase p) { return p == KingdomConstructionInputCargoPhase.Planned; }
		private static bool SourcePreparing(KingdomConstructionInputSourcePhase p) { return p >= KingdomConstructionInputSourcePhase.Reserved && p <= KingdomConstructionInputSourcePhase.Debited; }
		private static bool CargoPreparing(KingdomConstructionInputCargoPhase p) { return p >= KingdomConstructionInputCargoPhase.Planned && p <= KingdomConstructionInputCargoPhase.InFlight; }
		private static bool SourceDebited(KingdomConstructionInputSourcePhase p) { return p == KingdomConstructionInputSourcePhase.Debited; }
		private static bool CargoAtSource(KingdomConstructionInputCargoPhase p) { return p == KingdomConstructionInputCargoPhase.AtSource; }
		private static bool CargoInFlight(KingdomConstructionInputCargoPhase p) { return p == KingdomConstructionInputCargoPhase.InFlight; }
		private static bool CargoRouting(KingdomConstructionInputCargoPhase p) { return p >= KingdomConstructionInputCargoPhase.InFlight && p <= KingdomConstructionInputCargoPhase.Landed; }
		private static bool CargoLanded(KingdomConstructionInputCargoPhase p) { return p == KingdomConstructionInputCargoPhase.Landed; }
		private static bool SourceDebitClosing(KingdomConstructionInputSourcePhase p) { return p == KingdomConstructionInputSourcePhase.Debited || p == KingdomConstructionInputSourcePhase.Spent; }
		private static bool CargoDebitClosing(KingdomConstructionInputCargoPhase p) { return p >= KingdomConstructionInputCargoPhase.Landed && p <= KingdomConstructionInputCargoPhase.Spent; }
		private static bool SourceSpent(KingdomConstructionInputSourcePhase p) { return p == KingdomConstructionInputSourcePhase.Spent; }
		private static bool CargoSpent(KingdomConstructionInputCargoPhase p) { return p == KingdomConstructionInputCargoPhase.Spent; }
		private static bool SourceRollbackEligible(KingdomConstructionInputSourcePhase p) { return p >= KingdomConstructionInputSourcePhase.Reserved && p <= KingdomConstructionInputSourcePhase.TransferIntent; }
		private static bool CargoRollbackEligible(KingdomConstructionInputCargoPhase p) { return p >= KingdomConstructionInputCargoPhase.Planned && p <= KingdomConstructionInputCargoPhase.PickupIntent; }
		private static bool SourceRollingBack(KingdomConstructionInputSourcePhase p) { return SourceRollbackEligible(p) || p == KingdomConstructionInputSourcePhase.RestoreIntent || p == KingdomConstructionInputSourcePhase.Restored; }
		private static bool CargoRollingBack(KingdomConstructionInputCargoPhase p) { return CargoRollbackEligible(p) || p == KingdomConstructionInputCargoPhase.ReleaseIntent || p == KingdomConstructionInputCargoPhase.Released; }
		private static bool SourceRolledBack(KingdomConstructionInputSourcePhase p) { return p == KingdomConstructionInputSourcePhase.Reserved || p == KingdomConstructionInputSourcePhase.Restored; }
		private static bool CargoRolledBack(KingdomConstructionInputCargoPhase p) { return p == KingdomConstructionInputCargoPhase.Planned || p == KingdomConstructionInputCargoPhase.Released; }
		private static bool SourceCompensating(KingdomConstructionInputSourcePhase p) { return p == KingdomConstructionInputSourcePhase.Debited || p == KingdomConstructionInputSourcePhase.CompensationIntent || p == KingdomConstructionInputSourcePhase.Compensated; }
		private static bool SourceCompensated(KingdomConstructionInputSourcePhase p) { return p == KingdomConstructionInputSourcePhase.Compensated; }
		private static bool SourceCancelling(KingdomConstructionInputSourcePhase p)
		{ return p >= KingdomConstructionInputSourcePhase.Reserved
			&& p <= KingdomConstructionInputSourcePhase.Restored
			|| p == KingdomConstructionInputSourcePhase.CompensationIntent
			|| p == KingdomConstructionInputSourcePhase.Compensated; }
		private static bool SourceCancelled(KingdomConstructionInputSourcePhase p)
		{ return p == KingdomConstructionInputSourcePhase.Reserved
			|| p == KingdomConstructionInputSourcePhase.Restored
			|| p == KingdomConstructionInputSourcePhase.Compensated; }
		private static bool CargoCompensationEligible(KingdomConstructionInputCargoPhase p) { return p >= KingdomConstructionInputCargoPhase.AtSource && p <= KingdomConstructionInputCargoPhase.DebitIntent; }
		private static bool CargoCompensating(KingdomConstructionInputCargoPhase p) { return CargoCompensationEligible(p) || p == KingdomConstructionInputCargoPhase.ReleaseIntent || p == KingdomConstructionInputCargoPhase.Released || p == KingdomConstructionInputCargoPhase.CompensationIntent || p == KingdomConstructionInputCargoPhase.Compensated; }
		private static bool CargoCompensated(KingdomConstructionInputCargoPhase p) { return p == KingdomConstructionInputCargoPhase.Released || p == KingdomConstructionInputCargoPhase.Compensated; }
		private static bool CargoCancelling(KingdomConstructionInputCargoPhase p)
		{ return p >= KingdomConstructionInputCargoPhase.Planned
			&& p <= KingdomConstructionInputCargoPhase.Landed
			|| p == KingdomConstructionInputCargoPhase.ReleaseIntent
			|| p == KingdomConstructionInputCargoPhase.Released
			|| p == KingdomConstructionInputCargoPhase.CompensationIntent
			|| p == KingdomConstructionInputCargoPhase.Compensated; }
		private static bool CargoCancelled(KingdomConstructionInputCargoPhase p)
		{ return p == KingdomConstructionInputCargoPhase.Planned
			|| p == KingdomConstructionInputCargoPhase.Released
			|| p == KingdomConstructionInputCargoPhase.Compensated; }
		private static bool Terminal(KingdomConstructionInputTxPhase p) { return p == KingdomConstructionInputTxPhase.Committed || p == KingdomConstructionInputTxPhase.RolledBack || p == KingdomConstructionInputTxPhase.Compensated || p == KingdomConstructionInputTxPhase.Quarantined || p == KingdomConstructionInputTxPhase.Cancelled; }
	}
}
