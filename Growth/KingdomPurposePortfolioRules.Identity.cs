using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Presence census for every field owned by legacy, reciprocal, or landing cargo.
	/// Values and property-table types are deliberately absent: any presence protects first, then
	/// the exact consumer applies the stricter shape and receipt proof.</summary>
	internal struct KingdomPurposeCargoEvidence
	{
		internal bool LegacySchema, LegacyKey, LegacyManifest, LegacyConsignment;
		internal bool LegacyOrigin, LegacyDestination;
		internal bool PortfolioSchema, PortfolioReceipt, PortfolioKey, PortfolioFood;
		internal bool LandedFood, LandedReceipt, LandedCount, LandedAttempt, LandedFault;
		internal bool EffectAttempt, EffectReady, EffectOffer, EffectCount, EffectFault;
		internal bool EffectMark, EffectIndex;
	}

	public static partial class KingdomPurposePortfolioRules
	{
		/// <summary>Canonical authentication of the physical two-city route and all four store
		/// identities. Used both at pair freeze and at the sole second-root endpoint adoption.</summary>
		public static bool TryRouteDigest(string RealmId, string FirstSettlementId,
			string SecondSettlementId, string FirstGateKey, string SecondGateKey,
			string FirstZoneId, string SecondZoneId, string FirstInputStoreId,
			string FirstOutputStoreId, string SecondInputStoreId, string SecondOutputStoreId,
			out string RouteDigest)
		{
			RouteDigest = null;
			if (!Id(RealmId) || !Id(FirstSettlementId) || !Id(SecondSettlementId)
				|| FirstSettlementId == SecondSettlementId || !Id(FirstGateKey)
				|| !Id(SecondGateKey) || FirstGateKey == SecondGateKey
				|| !Id(FirstZoneId) || !Id(SecondZoneId) || FirstZoneId == SecondZoneId
				|| !Id(FirstInputStoreId) || !Id(FirstOutputStoreId)
				|| !Id(SecondInputStoreId) || !Id(SecondOutputStoreId)
				|| !Distinct(FirstInputStoreId, FirstOutputStoreId,
					SecondInputStoreId, SecondOutputStoreId)) return false;
			string value = string.Join("\n", new string[]
			{
				RealmId, FirstSettlementId, SecondSettlementId, FirstGateKey, SecondGateKey,
				FirstZoneId, SecondZoneId, FirstInputStoreId, FirstOutputStoreId,
				SecondInputStoreId, SecondOutputStoreId
			});
			using (SHA256 sha = SHA256.Create())
			{
				byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
				StringBuilder encoded = new StringBuilder(64);
				for (int i = 0; i < bytes.Length; i++) encoded.Append(bytes[i].ToString("x2",
					CultureInfo.InvariantCulture));
				RouteDigest = encoded.ToString();
			}
			return true;
		}

		internal static bool RouteDigestMatches(KingdomPurposePairReceipt Pair)
		{
			return Pair != null && TryRouteDigest(Pair.RealmId, Pair.FirstSettlementId,
				Pair.SecondSettlementId, Pair.FirstGateKey, Pair.SecondGateKey,
				Pair.FirstZoneId, Pair.SecondZoneId, Pair.FirstInputStoreId,
				Pair.FirstOutputStoreId, Pair.SecondInputStoreId, Pair.SecondOutputStoreId,
				out string expected) && Pair.RouteDigest == expected;
		}

		/// <summary>Presence, not the decoded value, protects purpose cargo from ordinary civic
		/// material use. Torn evidence is ownership nobody may silently normalise into stock.</summary>
		internal static bool PurposeCargoIsProtected(KingdomPurposeCargoEvidence Evidence)
		{
			if (Evidence.EffectAttempt || Evidence.EffectReady || Evidence.EffectOffer
				|| Evidence.EffectCount || Evidence.EffectFault
				|| Evidence.EffectMark || Evidence.EffectIndex) return true;
			return Evidence.LegacySchema || Evidence.LegacyKey || Evidence.LegacyManifest
				|| Evidence.LegacyConsignment || Evidence.LegacyOrigin
				|| Evidence.LegacyDestination || Evidence.PortfolioSchema
				|| Evidence.PortfolioReceipt || Evidence.PortfolioKey || Evidence.PortfolioFood
				|| Evidence.LandedFood || Evidence.LandedReceipt || Evidence.LandedCount
				|| Evidence.LandedAttempt || Evidence.LandedFault;
		}

		/// <summary>An owned field is exact only in its declared property table. Dual-typed fields
		/// are torn evidence even when the preferred table happens to carry the expected value.</summary>
		internal static bool PurposeCargoFieldTypeIsExact(bool HasInt, bool HasString,
			bool IntegerField)
		{
			return IntegerField ? HasInt && !HasString : HasString && !HasInt;
		}

		private static bool SameOptional(string A, string B)
		{
			return string.IsNullOrEmpty(A) ? string.IsNullOrEmpty(B) : A == B;
		}

		private static bool Distinct(params string[] Values)
		{
			for (int i = 0; i < Values.Length; i++)
				for (int j = i + 1; j < Values.Length; j++)
					if (Values[i] == Values[j]) return false;
			return true;
		}

		private static bool InputCoherent(KingdomPurposeOperationReceipt Operation)
		{
			bool exempt = Operation.BootstrapExemption || Operation.ReturnExemption;
			if (exempt) return string.IsNullOrEmpty(Operation.InputCargoId)
				&& string.IsNullOrEmpty(Operation.InputCargoReceipt);
			return Id(Operation.InputCargoId)
				&& TryDecodeCargo(Operation.InputCargoReceipt, out var input)
				&& input.ObjectId == Operation.InputCargoId
				&& input.PairId == Operation.PairId && input.PairEpoch == Operation.PairEpoch
				&& input.SourceKind == Operation.DestinationKind
				&& input.DestinationKind == Operation.SourceKind
				&& input.SourceSettlementId == Operation.DestinationSettlementId
				&& input.DestinationSettlementId == Operation.SourceSettlementId
				&& input.SourceWorkId == Operation.DestinationWorkId
				&& input.DestinationWorkId == Operation.SourceWorkId
				&& input.RouteDigest == Operation.RouteDigest;
		}

		private static bool PairTerminalMetadataCoherent(KingdomPurposePairReceipt Pair)
		{
			if (Pair.Phase == KingdomPurposePairPhase.Orphaned)
				return Pair.ResumePhase > KingdomPurposePairPhase.Invalid
					&& Pair.ResumePhase < KingdomPurposePairPhase.Orphaned
					&& string.IsNullOrEmpty(Pair.Fault);
			if (Pair.Phase == KingdomPurposePairPhase.Quarantined)
				return Pair.ResumePhase == KingdomPurposePairPhase.Invalid
					&& !string.IsNullOrEmpty(Pair.Fault);
			return Pair.ResumePhase == KingdomPurposePairPhase.Invalid
				&& string.IsNullOrEmpty(Pair.Fault);
		}

		private static bool PairOperationCoherent(KingdomPurposePairReceipt Pair,
			out KingdomPurposePairFault Fault)
		{
			Fault = KingdomPurposePairFault.Identity;
			if (Pair.Operation == null)
			{
				Fault = KingdomPurposePairFault.None;
				return true;
			}
			KingdomPurposeOperationReceipt operation = Pair.Operation;
			if (!ValidOperation(operation, out Fault)) return false;
			Fault = KingdomPurposePairFault.Identity;
			if (operation.PairId != Pair.PairId || operation.PairEpoch != Pair.Epoch
				|| operation.Ordinal != Pair.NextOperationOrdinal - 1
				|| !OperationEndpointMatches(Pair, operation)) return false;
			if (!string.IsNullOrEmpty(operation.InputCargoReceipt)
				&& (!TryDecodeCargo(operation.InputCargoReceipt, out var input)
					|| !CargoMatchesPair(Pair, input)
					|| input.ObjectId != operation.InputCargoId
					|| input.DestinationSettlementId != operation.SourceSettlementId
					|| input.DestinationWorkId != operation.SourceWorkId)) return false;
			if (!string.IsNullOrEmpty(operation.OutputCargoReceipt)
				&& (!TryDecodeCargo(operation.OutputCargoReceipt, out var output)
					|| !CargoMatchesPair(Pair, output)
					|| output.ObjectId != operation.OutputCargoId
					|| output.SourceSettlementId != operation.SourceSettlementId
					|| output.DestinationSettlementId != operation.DestinationSettlementId
					|| output.SourceWorkId != operation.SourceWorkId
					|| !SameOptional(output.DestinationWorkId,
						operation.DestinationWorkId))) return false;
			Fault = KingdomPurposePairFault.None;
			return true;
		}

		private static bool OperationEndpointMatches(KingdomPurposePairReceipt Pair,
			KingdomPurposeOperationReceipt Operation)
		{
			bool first = Operation.SourceKind == Pair.FirstKind
				&& Operation.DestinationKind == Pair.SecondKind;
			bool second = Operation.SourceKind == Pair.SecondKind
				&& Operation.DestinationKind == Pair.FirstKind;
			if (!first && !second) return false;
			string sourceSettlement = first ? Pair.FirstSettlementId : Pair.SecondSettlementId;
			string destinationSettlement = first ? Pair.SecondSettlementId : Pair.FirstSettlementId;
			string sourceWork = first ? Pair.FirstWorkId : Pair.SecondWorkId;
			string destinationWork = first ? Pair.SecondWorkId : Pair.FirstWorkId;
			string sourceZone = first ? Pair.FirstZoneId : Pair.SecondZoneId;
			string destinationZone = first ? Pair.SecondZoneId : Pair.FirstZoneId;
			string sourceInput = first ? Pair.FirstInputStoreId : Pair.SecondInputStoreId;
			string sourceOutput = first ? Pair.FirstOutputStoreId : Pair.SecondOutputStoreId;
			string destinationInput = first ? Pair.SecondInputStoreId : Pair.FirstInputStoreId;
			string sourceGate = first ? Pair.FirstGateKey : Pair.SecondGateKey;
			string destinationGate = first ? Pair.SecondGateKey : Pair.FirstGateKey;
			return Operation.SourceSettlementId == sourceSettlement
				&& Operation.DestinationSettlementId == destinationSettlement
				&& Operation.SourceWorkId == sourceWork
				&& SameOptional(Operation.DestinationWorkId, destinationWork)
				&& Operation.SourceZoneId == sourceZone
				&& Operation.DestinationZoneId == destinationZone
				&& Operation.SourceInputStoreId == sourceInput
				&& Operation.SourceOutputStoreId == sourceOutput
				&& Operation.DestinationInputStoreId == destinationInput
				&& Operation.SourceGateKey == sourceGate
				&& Operation.DestinationGateKey == destinationGate
				&& Operation.RouteDigest == Pair.RouteDigest;
		}

		private static bool CargoMatchesPair(KingdomPurposePairReceipt Pair,
			KingdomPurposeCargoReceipt Cargo)
		{
			if (Cargo.PairId != Pair.PairId || Cargo.PairEpoch != Pair.Epoch
				|| Cargo.RouteDigest != Pair.RouteDigest) return false;
			bool first = Cargo.SourceKind == Pair.FirstKind
				&& Cargo.DestinationKind == Pair.SecondKind;
			bool second = Cargo.SourceKind == Pair.SecondKind
				&& Cargo.DestinationKind == Pair.FirstKind;
			if (!first && !second) return false;
			return Cargo.SourceSettlementId == (first
				? Pair.FirstSettlementId : Pair.SecondSettlementId)
				&& Cargo.DestinationSettlementId == (first
					? Pair.SecondSettlementId : Pair.FirstSettlementId)
				&& Cargo.SourceWorkId == (first ? Pair.FirstWorkId : Pair.SecondWorkId)
				&& SameOptional(Cargo.DestinationWorkId,
					first ? Pair.SecondWorkId : Pair.FirstWorkId);
		}
	}
}
