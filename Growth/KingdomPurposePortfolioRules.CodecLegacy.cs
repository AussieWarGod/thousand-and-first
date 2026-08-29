namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		private const int LegacyOperationFields = 47;

		internal static bool TryDecodePairAny(string Receipt,
			out KingdomPurposePairReceipt Pair, out bool Legacy)
		{
			Legacy = false;
			if (TryDecodePair(Receipt, out Pair)) return true;
			if (!TryDecodeLegacyPair(Receipt, out Pair)) return false;
			Legacy = true;
			Pair.LegacyWire = true;
			return true;
		}

		internal static string EncodeLegacyOperation(KingdomPurposeOperationReceipt Operation)
		{
			if (!ValidOperation(Operation, out _)
				|| Operation.EffectStep != PurposeEffectExempt) return null;
			return EncodeFields(new string[]
			{
				"1", Operation.OperationId, N(Operation.Ordinal),
				N((int)Operation.SourceKind), N((int)Operation.DestinationKind),
				N((int)Operation.Phase), B(Operation.BootstrapExemption),
				B(Operation.ReturnExemption), Operation.InputCargoId,
				Operation.InputCargoReceipt, Operation.OutputCargoId,
				Operation.OutputCargoReceipt, Operation.SourceZoneId,
				Operation.DestinationZoneId, Operation.SourceInputStoreId,
				Operation.SourceOutputStoreId, Operation.DestinationInputStoreId,
				Operation.SourceGateKey, Operation.DestinationGateKey,
				Operation.RouteDigest, Operation.TransportJobId,
				N(Operation.WaterRequested), N(Operation.WaterSpent),
				N(Operation.WaterLost), N(Operation.FoodRequested),
				N(Operation.FoodSpent), N(Operation.FoodLost),
				Operation.MaterialRequested, Operation.MaterialSpent,
				Operation.MaterialLost, Operation.LocalDebitReceipt, Operation.ProcedureKey,
				Operation.ProcedureReceipt, Operation.EffectBeforeDigest,
				Operation.EffectAfterDigest, Operation.InputBeforeDigest,
				Operation.InputAfterDigest, Operation.OutputBeforeDigest,
				Operation.OutputAfterDigest, N(Operation.Revision), Operation.PairId,
				N(Operation.PairEpoch), Operation.SourceSettlementId,
				Operation.DestinationSettlementId, Operation.SourceWorkId,
				Operation.DestinationWorkId, "purpose-operation"
			});
		}

		internal static bool TryDecodeLegacyOperation(string Receipt,
			out KingdomPurposeOperationReceipt Operation)
		{
			Operation = null;
			if (!TryDecodeFields(Receipt, LegacyOperationFields, out string[] f)
				|| f[0] != "1" || f[46] != "purpose-operation"
				|| !Int(f[2], out int ordinal) || !Int(f[3], out int source)
				|| !Int(f[4], out int destination) || !Int(f[5], out int phase)
				|| !Bool(f[6], out bool bootstrap) || !Bool(f[7], out bool returned)
				|| !Int(f[21], out int waterRequested)
				|| !Int(f[22], out int waterSpent) || !Int(f[23], out int waterLost)
				|| !Int(f[24], out int foodRequested) || !Int(f[25], out int foodSpent)
				|| !Int(f[26], out int foodLost) || !Int(f[39], out int revision)
				|| !Long(f[41], out long pairEpoch)) return false;
			Operation = new KingdomPurposeOperationReceipt
			{
				PairId = f[40], PairEpoch = pairEpoch, OperationId = f[1], Ordinal = ordinal,
				SourceKind = (KingdomPurposeKind)source,
				DestinationKind = (KingdomPurposeKind)destination,
				Phase = (KingdomPurposeOperationPhase)phase,
				BootstrapExemption = bootstrap, ReturnExemption = returned,
				InputCargoId = f[8], InputCargoReceipt = f[9], OutputCargoId = f[10],
				OutputCargoReceipt = f[11], SourceZoneId = f[12], DestinationZoneId = f[13],
				SourceInputStoreId = f[14], SourceOutputStoreId = f[15],
				DestinationInputStoreId = f[16], SourceGateKey = f[17],
				DestinationGateKey = f[18], RouteDigest = f[19], TransportJobId = f[20],
				WaterRequested = waterRequested, WaterSpent = waterSpent,
				WaterLost = waterLost, FoodRequested = foodRequested, FoodSpent = foodSpent,
				FoodLost = foodLost, MaterialRequested = f[27], MaterialSpent = f[28],
				MaterialLost = f[29], LocalDebitReceipt = f[30], ProcedureKey = f[31],
				ProcedureReceipt = f[32], EffectBeforeDigest = f[33],
				EffectAfterDigest = f[34], InputBeforeDigest = f[35], InputAfterDigest = f[36],
				OutputBeforeDigest = f[37], OutputAfterDigest = f[38], Revision = revision,
				SourceSettlementId = f[42], DestinationSettlementId = f[43],
				SourceWorkId = f[44], DestinationWorkId = f[45],
				EffectStep = PurposeEffectExempt
			};
			return ValidOperation(Operation, out _)
				&& EncodeLegacyOperation(Operation) == Receipt;
		}

		internal static string EncodeLegacyPair(KingdomPurposePairReceipt Pair)
		{
			if (!ValidPair(Pair, out _)
				|| Pair.Operation != null && Pair.Operation.EffectStep != PurposeEffectExempt)
				return null;
			return EncodeFields(new string[]
			{
				"1", Pair.PairId, Pair.RealmId, N(Pair.Epoch), N((int)Pair.FirstKind),
				N((int)Pair.SecondKind), Pair.FirstSettlementId, Pair.SecondSettlementId,
				Pair.FirstWorkId, Pair.SecondWorkId, Pair.FirstZoneId, Pair.SecondZoneId,
				Pair.FirstInputStoreId, Pair.FirstOutputStoreId, Pair.SecondInputStoreId,
				Pair.SecondOutputStoreId, Pair.FirstGateKey, Pair.SecondGateKey,
				Pair.RouteDigest, B(Pair.BootstrapUsed), B(Pair.ReturnUsed),
				N((int)Pair.NextKind), Pair.CreditCargoId, Pair.CreditCargoReceipt,
				N((int)Pair.Phase), N((int)Pair.ResumePhase),
				Pair.Operation == null ? "" : EncodeLegacyOperation(Pair.Operation),
				N(Pair.Revision), Pair.Fault, N(Pair.NextOperationOrdinal), "purpose-pair"
			});
		}

		internal static bool TryDecodeLegacyPair(string Receipt,
			out KingdomPurposePairReceipt Pair)
		{
			Pair = null;
			if (!TryDecodeFields(Receipt, PairFields, out string[] f)
				|| f[0] != "1" || f[30] != "purpose-pair" || string.IsNullOrEmpty(f[26])
				|| !Long(f[3], out long epoch) || !Int(f[4], out int first)
				|| !Int(f[5], out int second) || !Bool(f[19], out bool bootstrap)
				|| !Bool(f[20], out bool returned) || !Int(f[21], out int next)
				|| !Int(f[24], out int phase) || !Int(f[25], out int resume)
				|| !Int(f[27], out int revision) || !Int(f[29], out int nextOrdinal)
				|| !TryDecodeLegacyOperation(f[26], out KingdomPurposeOperationReceipt operation))
				return false;
			Pair = new KingdomPurposePairReceipt
			{
				PairId = f[1], RealmId = f[2], Epoch = epoch,
				FirstKind = (KingdomPurposeKind)first, SecondKind = (KingdomPurposeKind)second,
				FirstSettlementId = f[6], SecondSettlementId = f[7], FirstWorkId = f[8],
				SecondWorkId = f[9], FirstZoneId = f[10], SecondZoneId = f[11],
				FirstInputStoreId = f[12], FirstOutputStoreId = f[13],
				SecondInputStoreId = f[14], SecondOutputStoreId = f[15],
				FirstGateKey = f[16], SecondGateKey = f[17], RouteDigest = f[18],
				BootstrapUsed = bootstrap, ReturnUsed = returned,
				NextKind = (KingdomPurposeKind)next, CreditCargoId = f[22],
				CreditCargoReceipt = f[23], Phase = (KingdomPurposePairPhase)phase,
				ResumePhase = (KingdomPurposePairPhase)resume, Operation = operation,
				Revision = revision, Fault = f[28], NextOperationOrdinal = nextOrdinal
			};
			return ValidPair(Pair, out _) && EncodeLegacyPair(Pair) == Receipt;
		}
	}
}
