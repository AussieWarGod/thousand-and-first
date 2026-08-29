using System;

namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		public const int MaxReceiptChars = 16384;
		public const int MaxFaultChars = 720;

		public static bool ValidCargo(KingdomPurposeCargoReceipt Cargo,
			out KingdomPurposePairFault Fault)
		{
			Fault = KingdomPurposePairFault.Malformed;
			if (Cargo == null || !Id(Cargo.PairId) || Cargo.PairEpoch < 1L
				|| !Id(Cargo.OperationId) || !Kind(Cargo.SourceKind)
				|| !Kind(Cargo.DestinationKind)
				|| !Compatible(Cargo.SourceKind, Cargo.DestinationKind)
				|| (Cargo.BootstrapExemption && Cargo.ReturnExemption)
				|| !Id(Cargo.SourceSettlementId) || !Id(Cargo.DestinationSettlementId)
				|| Cargo.SourceSettlementId == Cargo.DestinationSettlementId
				|| !Id(Cargo.SourceWorkId)
				|| (Cargo.BootstrapExemption ? !OptionalId(Cargo.DestinationWorkId)
					: !Id(Cargo.DestinationWorkId))
				|| (!string.IsNullOrEmpty(Cargo.DestinationWorkId)
					&& Cargo.SourceWorkId == Cargo.DestinationWorkId)
				|| !Id(Cargo.ObjectId) || !Id(Cargo.TransportJobId)
				|| !Digest(Cargo.RouteDigest)) return false;
			if (!TryRecipe(Cargo.SourceKind, Cargo.DestinationKind, out var recipe)
				|| Cargo.CargoKey != recipe.CargoKey
				|| Cargo.EmbodiedMaterial != recipe.EmbodiedMaterial
				|| Cargo.EmbodiedUnits != recipe.EmbodiedUnits
				|| Cargo.CarriedFood != recipe.CarriedFood)
			{
				Fault = KingdomPurposePairFault.WrongRecipe;
				return false;
			}
			Fault = KingdomPurposePairFault.None;
			return true;
		}

		public static bool ValidOperation(KingdomPurposeOperationReceipt Operation,
			out KingdomPurposePairFault Fault)
		{
			Fault = KingdomPurposePairFault.Malformed;
			if (Operation == null || !Id(Operation.PairId) || Operation.PairEpoch < 1L
				|| !Id(Operation.OperationId) || Operation.Ordinal < 1
				|| !Kind(Operation.SourceKind) || !Kind(Operation.DestinationKind)
				|| Operation.Phase <= KingdomPurposeOperationPhase.Invalid
				|| (Operation.Phase > KingdomPurposeOperationPhase.Delivered
					&& Operation.Phase != KingdomPurposeOperationPhase.Quarantined
					&& Operation.Phase != KingdomPurposeOperationPhase.PickupComplete
					&& Operation.Phase != KingdomPurposeOperationPhase.LandingPending)
				|| Operation.Revision < 0 || (Operation.BootstrapExemption
					&& Operation.ReturnExemption)
				|| !Id(Operation.SourceSettlementId)
				|| !Id(Operation.DestinationSettlementId)
				|| Operation.SourceSettlementId == Operation.DestinationSettlementId
				|| !Id(Operation.SourceWorkId)
				|| (Operation.BootstrapExemption
					? !OptionalId(Operation.DestinationWorkId)
					: !Id(Operation.DestinationWorkId))
				|| (!string.IsNullOrEmpty(Operation.DestinationWorkId)
					&& Operation.SourceWorkId == Operation.DestinationWorkId)
				|| !Id(Operation.SourceZoneId) || !Id(Operation.DestinationZoneId)
				|| Operation.SourceZoneId == Operation.DestinationZoneId
				|| !Id(Operation.SourceInputStoreId) || !Id(Operation.SourceOutputStoreId)
				|| !Id(Operation.DestinationInputStoreId)
				|| !Distinct(Operation.SourceInputStoreId, Operation.SourceOutputStoreId,
					Operation.DestinationInputStoreId)
				|| !Id(Operation.SourceGateKey) || !Id(Operation.DestinationGateKey)
				|| Operation.SourceGateKey == Operation.DestinationGateKey
				|| !Digest(Operation.RouteDigest)) return false;
			if (!TryRecipe(Operation.SourceKind, Operation.DestinationKind, out var recipe)
				|| Operation.WaterRequested != recipe.WaterDrams
				|| Operation.FoodRequested != recipe.FoodServings
				|| Operation.MaterialRequested != recipe.MaterialClaim
				|| !CanonicalClaim(Operation.MaterialSpent)
				|| !CanonicalClaim(Operation.MaterialLost))
			{
				Fault = KingdomPurposePairFault.WrongRecipe;
				return false;
			}
			if (!ScalarAccounting(Operation.WaterRequested, Operation.WaterSpent,
				Operation.WaterLost) || !ScalarAccounting(Operation.FoodRequested,
				Operation.FoodSpent, Operation.FoodLost)
				|| !MaterialAccounting(Operation.MaterialRequested, Operation.MaterialSpent,
					Operation.MaterialLost, out _))
			{
				Fault = KingdomPurposePairFault.Accounting;
				return false;
			}
			if (!InputCoherent(Operation))
			{
				Fault = KingdomPurposePairFault.Identity;
				return false;
			}
			if (!OptionalId(Operation.OutputCargoId)
				|| !OptionalId(Operation.TransportJobId)
				|| !OptionalText(Operation.LocalDebitReceipt, MaxReceiptChars)
				|| !OptionalText(Operation.ProcedureKey, 128)
				|| !OptionalText(Operation.ProcedureReceipt, 4096)
				|| !OptionalDigest(Operation.EffectBeforeDigest)
				|| !OptionalDigest(Operation.EffectAfterDigest)
				|| !OptionalDigest(Operation.InputBeforeDigest)
				|| !OptionalDigest(Operation.InputAfterDigest)
				|| !OptionalDigest(Operation.OutputBeforeDigest)
				|| !OptionalDigest(Operation.OutputAfterDigest)) return false;
			bool body = Operation.SourceKind == KingdomPurposeKind.Flesh
				|| Operation.SourceKind == KingdomPurposeKind.Chrome;
			bool procedure = !string.IsNullOrEmpty(Operation.ProcedureKey)
				&& !string.IsNullOrEmpty(Operation.ProcedureReceipt);
			if (body != procedure
				|| string.IsNullOrEmpty(Operation.ProcedureKey)
					!= string.IsNullOrEmpty(Operation.ProcedureReceipt)) return false;
			if (body && (!KingdomPurposeBodyAuthorityRules.TryDecode(
				Operation.ProcedureReceipt, out KingdomPurposeBodyAuthority authority)
				|| authority.Kind != Operation.SourceKind
				|| authority.PairId != Operation.PairId
				|| authority.PairEpoch != Operation.PairEpoch
				|| authority.OperationId != Operation.OperationId
				|| authority.ProcedureKey != Operation.ProcedureKey)) return false;
			if (!OutputCoherent(Operation, out Fault)) return false;
			if (!EffectStepIsLegalFor(Operation.SourceKind, Operation.EffectStep)) return false;
			if (!OperationPhaseCoherent(Operation, out Fault)) return false;
			Fault = KingdomPurposePairFault.None;
			return true;
		}

		public static bool ValidPair(KingdomPurposePairReceipt Pair,
			out KingdomPurposePairFault Fault)
		{
			Fault = KingdomPurposePairFault.Malformed;
			if (Pair == null || !Id(Pair.PairId) || !Id(Pair.RealmId) || Pair.Epoch < 1L
				|| !Kind(Pair.FirstKind) || !Kind(Pair.SecondKind)
				|| !Compatible(Pair.FirstKind, Pair.SecondKind)
				|| !Id(Pair.FirstSettlementId) || !Id(Pair.SecondSettlementId)
				|| Pair.FirstSettlementId == Pair.SecondSettlementId
				|| !Id(Pair.FirstWorkId) || !OptionalId(Pair.SecondWorkId)
				|| (!string.IsNullOrEmpty(Pair.SecondWorkId)
					&& Pair.FirstWorkId == Pair.SecondWorkId)
				|| !Id(Pair.FirstZoneId) || !Id(Pair.SecondZoneId)
				|| Pair.FirstZoneId == Pair.SecondZoneId
				|| !Id(Pair.FirstInputStoreId) || !Id(Pair.FirstOutputStoreId)
				|| !Id(Pair.SecondInputStoreId) || !Id(Pair.SecondOutputStoreId)
				|| !Distinct(Pair.FirstInputStoreId, Pair.FirstOutputStoreId,
					Pair.SecondInputStoreId, Pair.SecondOutputStoreId)
				|| !Id(Pair.FirstGateKey) || !Id(Pair.SecondGateKey)
				|| Pair.FirstGateKey == Pair.SecondGateKey || !Digest(Pair.RouteDigest)
				|| Pair.Phase <= KingdomPurposePairPhase.Invalid
				|| Pair.Phase > KingdomPurposePairPhase.Quarantined
				|| Pair.NextOperationOrdinal < 1 || Pair.Revision < 0
				|| !OptionalText(Pair.Fault, MaxFaultChars)) return false;
			if (!PhaseCoherent(Pair, out Fault)) return false;
			if (!PairTerminalMetadataCoherent(Pair))
			{
				Fault = KingdomPurposePairFault.Phase;
				return false;
			}
			if (!PairOperationCoherent(Pair, out Fault)) return false;
			Fault = KingdomPurposePairFault.None;
			return true;
		}

		private static bool PhaseCoherent(KingdomPurposePairReceipt Pair,
			out KingdomPurposePairFault Fault)
		{
			Fault = KingdomPurposePairFault.Phase;
			bool hasSecond = !string.IsNullOrEmpty(Pair.SecondWorkId);
			bool hasOperation = Pair.Operation != null;
			bool hasCredit = !string.IsNullOrEmpty(Pair.CreditCargoId)
				|| !string.IsNullOrEmpty(Pair.CreditCargoReceipt);
			if (hasCredit && (!Id(Pair.CreditCargoId)
				|| !ValidEncodedCargo(Pair.CreditCargoReceipt, Pair.CreditCargoId,
					Pair.NextKind, out KingdomPurposeCargoReceipt credit)
				|| !CargoMatchesPair(Pair, credit))) return false;
			if (Pair.Phase == KingdomPurposePairPhase.Frozen)
				return !Pair.BootstrapUsed && !Pair.ReturnUsed
					&& !hasOperation && !hasCredit && Pair.NextKind == KingdomPurposeKind.None
					&& Pair.NextOperationOrdinal == 1;
			if (Pair.Phase == KingdomPurposePairPhase.BootstrapOutstanding)
				return Pair.BootstrapUsed && !Pair.ReturnUsed && hasOperation
					&& Pair.Operation.BootstrapExemption && !hasCredit
					&& Pair.NextOperationOrdinal == 2;
			if (Pair.Phase == KingdomPurposePairPhase.SecondPending)
				return Pair.BootstrapUsed && !Pair.ReturnUsed && hasOperation
					&& Pair.Operation.BootstrapExemption
					&& Pair.Operation.Phase == KingdomPurposeOperationPhase.Delivered && !hasCredit
					&& Pair.NextOperationOrdinal == 2;
			if (Pair.Phase == KingdomPurposePairPhase.ReturnOutstanding)
				return Pair.BootstrapUsed && Pair.ReturnUsed && hasSecond && hasOperation
					&& Pair.Operation.ReturnExemption && !hasCredit
					&& Pair.NextOperationOrdinal == 3;
			if (Pair.Phase == KingdomPurposePairPhase.CargoAwaitingActivation)
				return Pair.BootstrapUsed && Pair.ReturnUsed && hasSecond && hasOperation
					&& Pair.Operation.ReturnExemption
					&& Pair.Operation.Phase == KingdomPurposeOperationPhase.Delivered && !hasCredit
					&& Pair.NextOperationOrdinal == 3;
			if (Pair.Phase == KingdomPurposePairPhase.Active)
				return Pair.BootstrapUsed && Pair.ReturnUsed && hasSecond && !hasOperation
					&& Kind(Pair.NextKind) && hasCredit && Pair.NextOperationOrdinal >= 3;
			if (Pair.Phase == KingdomPurposePairPhase.OperationOutstanding)
				return Pair.BootstrapUsed && Pair.ReturnUsed && hasSecond && hasOperation
					&& !Pair.Operation.BootstrapExemption && !Pair.Operation.ReturnExemption
					&& Pair.NextKind == Pair.Operation.SourceKind && !hasCredit
					&& Pair.NextOperationOrdinal >= 4;
			if (Pair.Phase == KingdomPurposePairPhase.CargoAwaitingConsumption)
				return Pair.BootstrapUsed && Pair.ReturnUsed && hasSecond && hasOperation
					&& Pair.Operation.Phase == KingdomPurposeOperationPhase.Delivered
					&& Pair.NextKind == Pair.Operation.DestinationKind && !hasCredit
					&& Pair.NextOperationOrdinal >= 4;
			if (Pair.Phase == KingdomPurposePairPhase.Orphaned)
				return Pair.ResumePhase > KingdomPurposePairPhase.Invalid
					&& Pair.ResumePhase < KingdomPurposePairPhase.Orphaned;
			if (Pair.Phase == KingdomPurposePairPhase.Dormant)
				return !hasOperation && !hasCredit && Pair.NextKind == KingdomPurposeKind.None;
			if (Pair.Phase == KingdomPurposePairPhase.Quarantined)
				return !string.IsNullOrEmpty(Pair.Fault);
			return false;
		}

		private static bool ValidEncodedCargo(string Receipt, string ObjectId,
			KingdomPurposeKind ExpectedDestination, out KingdomPurposeCargoReceipt Cargo)
		{
			return TryDecodeCargo(Receipt, out Cargo) && Cargo.ObjectId == ObjectId
				&& Cargo.DestinationKind == ExpectedDestination;
		}

		private static bool Kind(KingdomPurposeKind Value)
		{
			return Value >= KingdomPurposeKind.Flesh && Value <= KingdomPurposeKind.Harvest;
		}

		private static bool Id(string Value)
		{
			return Value != null && Value.Length >= 1 && Value.Length <= 256
				&& Value.Trim() == Value && Value.IndexOf('\0') < 0;
		}

		private static bool OptionalId(string Value)
		{
			return string.IsNullOrEmpty(Value) || Id(Value);
		}

		private static bool OptionalText(string Value, int Max)
		{
			return string.IsNullOrEmpty(Value) || (Value.Length <= Max && Value.IndexOf('\0') < 0);
		}

		private static bool Digest(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9')
					|| (Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}

		private static bool OptionalDigest(string Value)
		{
			return string.IsNullOrEmpty(Value) || Digest(Value);
		}

		private static bool CanonicalClaim(string Value)
		{
			return KingdomMaterialDebitCost.TryParseClaim(Value, out var parsed)
				&& parsed.ToClaimString() == Value;
		}
	}
}
