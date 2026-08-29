using System;

namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		private static bool OutputCoherent(KingdomPurposeOperationReceipt Operation,
			out KingdomPurposePairFault Fault)
		{
			Fault = KingdomPurposePairFault.Identity;
			bool hasId = !string.IsNullOrEmpty(Operation.OutputCargoId);
			bool hasReceipt = !string.IsNullOrEmpty(Operation.OutputCargoReceipt);
			bool outputPhase = Operation.Phase >= KingdomPurposeOperationPhase.OutputPending;
			KingdomPurposeCargoReceipt cargo = null;
			if (hasId != hasReceipt || (hasReceipt && !ValidEncodedCargo(
				Operation.OutputCargoReceipt, Operation.OutputCargoId,
				Operation.DestinationKind, out cargo))) return false;
			if (hasId && Operation.OutputCargoId == Operation.InputCargoId) return false;
			if (hasReceipt && (cargo.OperationId != Operation.OperationId
				|| cargo.PairId != Operation.PairId || cargo.PairEpoch != Operation.PairEpoch
				|| cargo.SourceKind != Operation.SourceKind
				|| cargo.DestinationKind != Operation.DestinationKind
				|| cargo.SourceSettlementId != Operation.SourceSettlementId
				|| cargo.DestinationSettlementId != Operation.DestinationSettlementId
				|| cargo.SourceWorkId != Operation.SourceWorkId
				|| !SameOptional(cargo.DestinationWorkId, Operation.DestinationWorkId)
				|| cargo.BootstrapExemption != Operation.BootstrapExemption
				|| cargo.ReturnExemption != Operation.ReturnExemption
				|| cargo.TransportJobId != Operation.TransportJobId
				|| cargo.RouteDigest != Operation.RouteDigest)) return false;
			if (Operation.Phase >= KingdomPurposeOperationPhase.Dispatching && !hasReceipt)
				return false;
			if (Operation.Phase >= KingdomPurposeOperationPhase.Dispatching
				&& !Id(Operation.TransportJobId)) return false;
			if (!hasReceipt && !string.IsNullOrEmpty(Operation.TransportJobId)) return false;
			if (!outputPhase && hasReceipt) return false;
			Fault = KingdomPurposePairFault.None;
			return true;
		}

		private static bool OperationPhaseCoherent(KingdomPurposeOperationReceipt Operation,
			out KingdomPurposePairFault Fault)
		{
			Fault = KingdomPurposePairFault.Phase;
			bool exempt = Operation.BootstrapExemption || Operation.ReturnExemption;
			if (Operation.Phase == KingdomPurposeOperationPhase.Quarantined) return false;
			bool hasLocal = !string.IsNullOrEmpty(Operation.LocalDebitReceipt);
			if (Operation.Phase < KingdomPurposeOperationPhase.LocalDebitPending)
			{
				if (hasLocal) return false;
			}
			else if (!hasLocal || !TryDecodeLocalDebit(Operation.LocalDebitReceipt,
				out KingdomPurposeLocalDebitReceipt local)
				|| !LocalDebitMatchesOperation(local, Operation)) return false;
			if (exempt && (Operation.Phase == KingdomPurposeOperationPhase.InputDebitPending
				|| Operation.Phase == KingdomPurposeOperationPhase.InputDebited)) return false;
			if (Operation.Phase < KingdomPurposeOperationPhase.LocalDebitPending
				&& (Operation.WaterSpent != 0 || Operation.WaterLost != 0
					|| Operation.FoodSpent != 0 || Operation.FoodLost != 0
					|| Operation.MaterialSpent != EmptyClaim()
					|| Operation.MaterialLost != EmptyClaim())) return false;
			if (exempt && (!string.IsNullOrEmpty(Operation.InputBeforeDigest)
				|| !string.IsNullOrEmpty(Operation.InputAfterDigest))) return false;
			if (!exempt && (Operation.Phase < KingdomPurposeOperationPhase.InputDebitPending
					? !string.IsNullOrEmpty(Operation.InputBeforeDigest)
						|| !string.IsNullOrEmpty(Operation.InputAfterDigest)
					: !Digest(Operation.InputBeforeDigest)
						|| (Operation.Phase < KingdomPurposeOperationPhase.InputDebited
							? !string.IsNullOrEmpty(Operation.InputAfterDigest)
							: !Digest(Operation.InputAfterDigest)))) return false;
			if (Operation.Phase >= KingdomPurposeOperationPhase.LocalDebited
				&& (!FullyDebited(Operation) || Operation.WaterLost != 0
					|| Operation.FoodLost != 0 || Operation.MaterialLost != EmptyClaim())) return false;
			if (Operation.Phase < KingdomPurposeOperationPhase.EffectPending
				? !string.IsNullOrEmpty(Operation.EffectBeforeDigest)
					|| !string.IsNullOrEmpty(Operation.EffectAfterDigest)
				: !Digest(Operation.EffectBeforeDigest)
					|| (Operation.Phase < KingdomPurposeOperationPhase.EffectApplied
						? !string.IsNullOrEmpty(Operation.EffectAfterDigest)
						: !Digest(Operation.EffectAfterDigest))) return false;
			if (!EffectPhaseCoherent(Operation)) return false;
			if (Operation.Phase < KingdomPurposeOperationPhase.OutputPending
				? !string.IsNullOrEmpty(Operation.OutputBeforeDigest)
					|| !string.IsNullOrEmpty(Operation.OutputAfterDigest)
				: !Digest(Operation.OutputBeforeDigest)
					|| (Operation.Phase < KingdomPurposeOperationPhase.Dispatching
						? !string.IsNullOrEmpty(Operation.OutputAfterDigest)
						: !Digest(Operation.OutputAfterDigest))) return false;
			Fault = KingdomPurposePairFault.None;
			return true;
		}

		private static bool LocalDebitMatchesOperation(KingdomPurposeLocalDebitReceipt Local,
			KingdomPurposeOperationReceipt Operation)
		{
			return Local.PairId == Operation.PairId && Local.PairEpoch == Operation.PairEpoch
				&& Local.OperationId == Operation.OperationId
				&& Local.SourceSettlementId == Operation.SourceSettlementId
				&& Local.SourceZoneId == Operation.SourceZoneId
				&& Local.SourceWorkId == Operation.SourceWorkId
				&& Local.SourceInputStoreId == Operation.SourceInputStoreId
				&& Local.WaterRequested == Operation.WaterRequested
				&& Local.FoodRequested == Operation.FoodRequested
				&& Local.MaterialRequested == Operation.MaterialRequested;
		}
	}
}
