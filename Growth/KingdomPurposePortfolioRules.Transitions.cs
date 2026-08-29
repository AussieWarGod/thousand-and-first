using System;

namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		public static bool ValidTransition(KingdomPurposePairReceipt Before,
			KingdomPurposePairReceipt After, out KingdomPurposePairFault Fault)
		{
			Fault = KingdomPurposePairFault.Transition;
			if (!ValidPair(Before, out _) || !ValidPair(After, out _)
				|| Before.Revision == int.MaxValue
				|| (!SamePairIdentity(Before, After)
					&& !AdoptsSecondEndpoint(Before, After))
				|| !AdoptOnce(Before.SecondWorkId, After.SecondWorkId)
				|| !PairRevisionHeadroomIsValid(After)
				|| After.Revision != Before.Revision + 1)
				return false;
			if (Before.Phase == KingdomPurposePairPhase.Orphaned
				&& After.Phase == KingdomPurposePairPhase.Orphaned)
			{
				bool advanced = AdvanceCommittedWhileOrphaned(Before, After);
				if (advanced) Fault = KingdomPurposePairFault.None;
				return advanced;
			}
			if (After.Phase == KingdomPurposePairPhase.Quarantined)
			{
				bool quarantined = !string.IsNullOrEmpty(After.Fault)
					&& SameStateExceptTerminal(Before, After);
				if (quarantined) Fault = KingdomPurposePairFault.None;
				return quarantined;
			}
			if (After.Phase == KingdomPurposePairPhase.Orphaned)
			{
				bool orphaned = Before.Phase < KingdomPurposePairPhase.Orphaned
					&& After.ResumePhase == Before.Phase && SameStateExceptTerminal(Before, After);
				if (orphaned) Fault = KingdomPurposePairFault.None;
				return orphaned;
			}
			if (Before.Phase == KingdomPurposePairPhase.Orphaned)
			{
				bool resumed = After.Phase == Before.ResumePhase
					&& After.ResumePhase == KingdomPurposePairPhase.Invalid
					&& SameOperationalState(Before, After);
				if (resumed) Fault = KingdomPurposePairFault.None;
				return resumed;
			}
			if ((Before.Phase == KingdomPurposePairPhase.Frozen
				|| Before.Phase == KingdomPurposePairPhase.Active)
				&& After.Phase == KingdomPurposePairPhase.Dormant)
			{
				bool dormant = After.Operation == null && string.IsNullOrEmpty(After.CreditCargoId)
					&& After.NextKind == KingdomPurposeKind.None
					&& After.NextOperationOrdinal == Before.NextOperationOrdinal;
				if (dormant) Fault = KingdomPurposePairFault.None;
				return dormant;
			}

			bool allowed = false;
			switch (Before.Phase)
			{
			case KingdomPurposePairPhase.Frozen:
				allowed = After.Phase == KingdomPurposePairPhase.BootstrapOutstanding
					&& !Before.BootstrapUsed && After.BootstrapUsed && !After.ReturnUsed
					&& StartsNextOperation(Before, After)
					&& After.Operation?.BootstrapExemption == true
					&& After.Operation.SourceKind == Before.FirstKind;
				break;
			case KingdomPurposePairPhase.BootstrapOutstanding:
				allowed = AdvanceOrDelivered(Before, After,
					KingdomPurposePairPhase.SecondPending);
				break;
			case KingdomPurposePairPhase.SecondPending:
				allowed = After.Phase == KingdomPurposePairPhase.ReturnOutstanding
					&& (string.IsNullOrEmpty(Before.SecondWorkId)
						? Id(After.SecondWorkId) : After.SecondWorkId == Before.SecondWorkId)
					&& !Before.ReturnUsed && After.ReturnUsed
					&& StartsNextOperation(Before, After)
					&& After.Operation?.ReturnExemption == true
					&& After.Operation.SourceKind == Before.SecondKind;
				break;
			case KingdomPurposePairPhase.ReturnOutstanding:
				allowed = AdvanceOrDelivered(Before, After,
					KingdomPurposePairPhase.CargoAwaitingActivation);
				break;
			case KingdomPurposePairPhase.CargoAwaitingActivation:
				allowed = After.Phase == KingdomPurposePairPhase.OperationOutstanding
					&& StartsNextOperation(Before, After)
					&& After.Operation != null && !After.Operation.BootstrapExemption
					&& !After.Operation.ReturnExemption
					&& After.Operation.SourceKind == Before.FirstKind
					&& After.Operation.InputCargoId == Before.Operation?.OutputCargoId
					&& After.Operation.InputCargoReceipt == Before.Operation?.OutputCargoReceipt
					&& After.NextKind == Before.FirstKind;
				break;
			case KingdomPurposePairPhase.Active:
				allowed = After.Phase == KingdomPurposePairPhase.OperationOutstanding
					&& StartsNextOperation(Before, After)
					&& After.Operation != null && !After.Operation.BootstrapExemption
					&& !After.Operation.ReturnExemption
					&& After.Operation.SourceKind == Before.NextKind
					&& After.Operation.InputCargoId == Before.CreditCargoId
					&& After.Operation.InputCargoReceipt == Before.CreditCargoReceipt;
				break;
			case KingdomPurposePairPhase.OperationOutstanding:
				allowed = AdvanceOrDelivered(Before, After,
					KingdomPurposePairPhase.CargoAwaitingConsumption);
				break;
			case KingdomPurposePairPhase.CargoAwaitingConsumption:
				allowed = CreditDelivered(Before, After, Before.Operation.DestinationKind);
				break;
			}
			if (!allowed) return false;
			Fault = KingdomPurposePairFault.None;
			return true;
		}

		public static bool ValidOperationTransition(KingdomPurposeOperationReceipt Before,
			KingdomPurposeOperationReceipt After)
		{
			if (!ValidOperation(Before, out _) || !ValidOperation(After, out _)
				|| Before.Revision == int.MaxValue
				|| After.Revision != Before.Revision + 1 || !SameOperationIdentity(Before, After)
				|| After.WaterSpent < Before.WaterSpent || After.WaterLost < Before.WaterLost
				|| After.FoodSpent < Before.FoodSpent || After.FoodLost < Before.FoodLost
				|| !ClaimMonotone(Before.MaterialSpent, After.MaterialSpent)
				|| !ClaimMonotone(Before.MaterialLost, After.MaterialLost)
				|| !EffectStepMonotone(Before, After)
				|| !AdoptOnce(Before.OutputCargoId, After.OutputCargoId)
				|| !AdoptOnce(Before.OutputCargoReceipt, After.OutputCargoReceipt)
				|| !AdoptOnce(Before.TransportJobId, After.TransportJobId)
				|| !AdoptOnce(Before.LocalDebitReceipt, After.LocalDebitReceipt)
				|| !AdoptOnce(Before.EffectBeforeDigest, After.EffectBeforeDigest)
				|| !AdoptOnce(Before.EffectAfterDigest, After.EffectAfterDigest)
				|| !AdoptOnce(Before.InputBeforeDigest, After.InputBeforeDigest)
				|| !AdoptOnce(Before.InputAfterDigest, After.InputAfterDigest)
				|| !AdoptOnce(Before.OutputBeforeDigest, After.OutputBeforeDigest)
				|| !AdoptOnce(Before.OutputAfterDigest, After.OutputAfterDigest)) return false;
			if (After.Phase == Before.Phase)
				return EvidenceAdvanced(Before, After);
			if (After.Phase == KingdomPurposeOperationPhase.Quarantined) return true;
			if (Before.Phase == KingdomPurposeOperationPhase.Prepared)
				return After.Phase == ((Before.BootstrapExemption || Before.ReturnExemption)
					? KingdomPurposeOperationPhase.LocalDebitPending
					: KingdomPurposeOperationPhase.InputDebitPending);
			bool routeAdvance = (Before.Phase == KingdomPurposeOperationPhase.Dispatching
					&& After.Phase == KingdomPurposeOperationPhase.PickupComplete)
				|| (Before.Phase == KingdomPurposeOperationPhase.PickupComplete
					&& After.Phase == KingdomPurposeOperationPhase.LandingPending)
				|| (Before.Phase == KingdomPurposeOperationPhase.LandingPending
					&& After.Phase == KingdomPurposeOperationPhase.Delivered);
			if (!routeAdvance && (int)After.Phase != (int)Before.Phase + 1) return false;
			if (After.Phase == KingdomPurposeOperationPhase.LocalDebited && !FullyDebited(After))
				return false;
			return true;
		}

		private static bool AdvanceOrDelivered(KingdomPurposePairReceipt Before,
			KingdomPurposePairReceipt After, KingdomPurposePairPhase DeliveredPhase)
		{
			if (Before.Operation == null || After.Operation == null) return false;
			if (Before.SecondWorkId != After.SecondWorkId
				|| Before.NextOperationOrdinal != After.NextOperationOrdinal
				|| Before.BootstrapUsed != After.BootstrapUsed
				|| Before.ReturnUsed != After.ReturnUsed
				|| Before.CreditCargoId != After.CreditCargoId
				|| Before.CreditCargoReceipt != After.CreditCargoReceipt) return false;
			if (!ValidOperationTransition(Before.Operation, After.Operation)) return false;
			if (After.Operation.Phase != KingdomPurposeOperationPhase.Delivered)
				return After.Phase == Before.Phase && Before.NextKind == After.NextKind;
			return After.Phase == DeliveredPhase
				&& After.NextKind == (DeliveredPhase
					== KingdomPurposePairPhase.CargoAwaitingConsumption
						? After.Operation.DestinationKind : Before.NextKind);
		}

		private static bool AdvanceCommittedWhileOrphaned(
			KingdomPurposePairReceipt Before, KingdomPurposePairReceipt After)
		{
			if (Before.Operation == null || After.Operation == null
				|| !SameOperationIdentity(Before.Operation, After.Operation)
				|| (Before.ResumePhase != KingdomPurposePairPhase.BootstrapOutstanding
					&& Before.ResumePhase != KingdomPurposePairPhase.ReturnOutstanding
					&& Before.ResumePhase != KingdomPurposePairPhase.OperationOutstanding))
				return false;
			KingdomPurposePairReceipt liveBefore = Before.Copy();
			KingdomPurposePairReceipt liveAfter = After.Copy();
			liveBefore.Phase = Before.ResumePhase;
			liveBefore.ResumePhase = KingdomPurposePairPhase.Invalid;
			liveAfter.Phase = After.ResumePhase;
			liveAfter.ResumePhase = KingdomPurposePairPhase.Invalid;
			return ValidTransition(liveBefore, liveAfter, out _);
		}

		private static bool CreditDelivered(KingdomPurposePairReceipt Before,
			KingdomPurposePairReceipt After, KingdomPurposeKind Next)
		{
			return Before.Operation != null && Before.Operation.Phase == KingdomPurposeOperationPhase.Delivered
				&& Before.NextOperationOrdinal == After.NextOperationOrdinal
				&& After.Phase == KingdomPurposePairPhase.Active && After.Operation == null
				&& After.NextKind == Next && After.CreditCargoId == Before.Operation.OutputCargoId
				&& After.CreditCargoReceipt == Before.Operation.OutputCargoReceipt;
		}

		private static bool StartsNextOperation(KingdomPurposePairReceipt Before,
			KingdomPurposePairReceipt After)
		{
			return Before.NextOperationOrdinal != int.MaxValue && After.Operation != null
				&& After.Operation.Ordinal == Before.NextOperationOrdinal
				&& After.NextOperationOrdinal == Before.NextOperationOrdinal + 1;
		}

		private static bool SamePairIdentity(KingdomPurposePairReceipt A,
			KingdomPurposePairReceipt B)
		{
			return A.PairId == B.PairId && A.RealmId == B.RealmId && A.Epoch == B.Epoch
				&& A.FirstKind == B.FirstKind && A.SecondKind == B.SecondKind
				&& A.FirstSettlementId == B.FirstSettlementId
				&& A.SecondSettlementId == B.SecondSettlementId
				&& A.FirstWorkId == B.FirstWorkId && A.FirstZoneId == B.FirstZoneId
				&& A.SecondZoneId == B.SecondZoneId
				&& A.FirstInputStoreId == B.FirstInputStoreId
				&& A.FirstOutputStoreId == B.FirstOutputStoreId
				&& A.SecondInputStoreId == B.SecondInputStoreId
				&& A.SecondOutputStoreId == B.SecondOutputStoreId
				&& A.FirstGateKey == B.FirstGateKey && A.SecondGateKey == B.SecondGateKey
				&& A.RouteDigest == B.RouteDigest;
		}

		private static bool SameOperationIdentity(KingdomPurposeOperationReceipt A,
			KingdomPurposeOperationReceipt B)
		{
			return A.PairId == B.PairId && A.PairEpoch == B.PairEpoch
				&& A.OperationId == B.OperationId && A.Ordinal == B.Ordinal
				&& A.SourceKind == B.SourceKind && A.DestinationKind == B.DestinationKind
				&& A.SourceSettlementId == B.SourceSettlementId
				&& A.DestinationSettlementId == B.DestinationSettlementId
				&& A.SourceWorkId == B.SourceWorkId
				&& A.DestinationWorkId == B.DestinationWorkId
				&& A.BootstrapExemption == B.BootstrapExemption
				&& A.ReturnExemption == B.ReturnExemption
				&& A.InputCargoId == B.InputCargoId
				&& A.InputCargoReceipt == B.InputCargoReceipt
				&& A.SourceZoneId == B.SourceZoneId && A.DestinationZoneId == B.DestinationZoneId
				&& A.SourceInputStoreId == B.SourceInputStoreId
				&& A.SourceOutputStoreId == B.SourceOutputStoreId
				&& A.DestinationInputStoreId == B.DestinationInputStoreId
				&& A.SourceGateKey == B.SourceGateKey && A.DestinationGateKey == B.DestinationGateKey
				&& A.RouteDigest == B.RouteDigest && A.WaterRequested == B.WaterRequested
				&& A.FoodRequested == B.FoodRequested && A.MaterialRequested == B.MaterialRequested
				&& A.ProcedureKey == B.ProcedureKey && A.ProcedureReceipt == B.ProcedureReceipt;
		}
	}
}
