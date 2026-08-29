using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		private const int CargoFields = 20;
		private const int OperationFields = 48;
		private const int PairFields = 31;

		public static string EncodeCargo(KingdomPurposeCargoReceipt Cargo)
		{
			if (!ValidCargo(Cargo, out _)) return null;
			return EncodeFields(new string[]
			{
				"1", Cargo.PairId, N(Cargo.PairEpoch), Cargo.OperationId,
				N((int)Cargo.SourceKind), N((int)Cargo.DestinationKind),
				B(Cargo.BootstrapExemption), B(Cargo.ReturnExemption),
				Cargo.SourceSettlementId, Cargo.DestinationSettlementId,
				Cargo.SourceWorkId, Cargo.DestinationWorkId, Cargo.CargoKey,
				N((int)Cargo.EmbodiedMaterial), N(Cargo.EmbodiedUnits),
				N(Cargo.CarriedFood), Cargo.ObjectId, Cargo.TransportJobId,
				Cargo.RouteDigest, "purpose-cargo"
			});
		}

		public static bool TryDecodeCargo(string Receipt, out KingdomPurposeCargoReceipt Cargo)
		{
			Cargo = null;
			if (!TryDecodeFields(Receipt, CargoFields, out string[] f)
				|| f[0] != "1" || f[19] != "purpose-cargo"
				|| !Long(f[2], out long epoch) || !Int(f[4], out int source)
				|| !Int(f[5], out int destination) || !Bool(f[6], out bool bootstrap)
				|| !Bool(f[7], out bool returned) || !Int(f[13], out int material)
				|| !Int(f[14], out int units) || !Int(f[15], out int food)) return false;
			Cargo = new KingdomPurposeCargoReceipt
			{
				PairId = f[1], PairEpoch = epoch, OperationId = f[3],
				SourceKind = (KingdomPurposeKind)source,
				DestinationKind = (KingdomPurposeKind)destination,
				BootstrapExemption = bootstrap, ReturnExemption = returned,
				SourceSettlementId = f[8], DestinationSettlementId = f[9],
				SourceWorkId = f[10], DestinationWorkId = f[11], CargoKey = f[12],
				EmbodiedMaterial = (KingdomMaterial)material, EmbodiedUnits = units,
				CarriedFood = food, ObjectId = f[16], TransportJobId = f[17],
				RouteDigest = f[18]
			};
			return ValidCargo(Cargo, out _) && EncodeCargo(Cargo) == Receipt;
		}

		public static string EncodeOperation(KingdomPurposeOperationReceipt Operation)
		{
			if (!ValidOperation(Operation, out _)) return null;
			return EncodeFields(new string[]
			{
				"2", Operation.OperationId, N(Operation.Ordinal),
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
				Operation.DestinationWorkId, N(Operation.EffectStep), "purpose-operation"
			});
		}

		public static bool TryDecodeOperation(string Receipt,
			out KingdomPurposeOperationReceipt Operation)
		{
			Operation = null;
			if (!TryDecodeFields(Receipt, OperationFields, out string[] f)
				|| f[0] != "2" || f[47] != "purpose-operation"
				|| !Int(f[2], out int ordinal) || !Int(f[3], out int source)
				|| !Int(f[4], out int destination) || !Int(f[5], out int phase)
				|| !Bool(f[6], out bool bootstrap) || !Bool(f[7], out bool returned)
				|| !Int(f[21], out int waterRequested)
				|| !Int(f[22], out int waterSpent) || !Int(f[23], out int waterLost)
				|| !Int(f[24], out int foodRequested) || !Int(f[25], out int foodSpent)
				|| !Int(f[26], out int foodLost) || !Int(f[39], out int revision)
				|| !Long(f[41], out long pairEpoch)
				|| !Int(f[46], out int effectStep)) return false;
			Operation = new KingdomPurposeOperationReceipt
			{
				PairId = f[40], PairEpoch = pairEpoch,
				OperationId = f[1], Ordinal = ordinal,
				SourceKind = (KingdomPurposeKind)source,
				DestinationKind = (KingdomPurposeKind)destination,
				Phase = (KingdomPurposeOperationPhase)phase,
				BootstrapExemption = bootstrap, ReturnExemption = returned,
				InputCargoId = f[8], InputCargoReceipt = f[9],
				OutputCargoId = f[10], OutputCargoReceipt = f[11],
				SourceZoneId = f[12], DestinationZoneId = f[13],
				SourceInputStoreId = f[14], SourceOutputStoreId = f[15],
				DestinationInputStoreId = f[16], SourceGateKey = f[17],
				DestinationGateKey = f[18], RouteDigest = f[19], TransportJobId = f[20],
				WaterRequested = waterRequested, WaterSpent = waterSpent,
				WaterLost = waterLost, FoodRequested = foodRequested,
				FoodSpent = foodSpent, FoodLost = foodLost,
				MaterialRequested = f[27], MaterialSpent = f[28], MaterialLost = f[29],
				LocalDebitReceipt = f[30], ProcedureKey = f[31], ProcedureReceipt = f[32],
				EffectBeforeDigest = f[33], EffectAfterDigest = f[34],
				InputBeforeDigest = f[35], InputAfterDigest = f[36],
				OutputBeforeDigest = f[37], OutputAfterDigest = f[38], Revision = revision,
				SourceSettlementId = f[42], DestinationSettlementId = f[43],
				SourceWorkId = f[44], DestinationWorkId = f[45], EffectStep = effectStep
			};
			return ValidOperation(Operation, out _) && EncodeOperation(Operation) == Receipt;
		}

		public static string EncodePair(KingdomPurposePairReceipt Pair)
		{
			if (!ValidPair(Pair, out _)) return null;
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
				Pair.Operation == null ? "" : EncodeOperation(Pair.Operation),
				N(Pair.Revision), Pair.Fault, N(Pair.NextOperationOrdinal), "purpose-pair"
			});
		}

		public static bool TryDecodePair(string Receipt, out KingdomPurposePairReceipt Pair)
		{
			Pair = null;
			if (!TryDecodeFields(Receipt, PairFields, out string[] f)
				|| f[0] != "1" || f[30] != "purpose-pair" || !Long(f[3], out long epoch)
				|| !Int(f[4], out int first) || !Int(f[5], out int second)
				|| !Bool(f[19], out bool bootstrap) || !Bool(f[20], out bool returned)
				|| !Int(f[21], out int next) || !Int(f[24], out int phase)
				|| !Int(f[25], out int resume) || !Int(f[27], out int revision)
				|| !Int(f[29], out int nextOrdinal)) return false;
			KingdomPurposeOperationReceipt operation = null;
			if (!string.IsNullOrEmpty(f[26]) && !TryDecodeOperation(f[26], out operation)) return false;
			Pair = new KingdomPurposePairReceipt
			{
				PairId = f[1], RealmId = f[2], Epoch = epoch,
				FirstKind = (KingdomPurposeKind)first, SecondKind = (KingdomPurposeKind)second,
				FirstSettlementId = f[6], SecondSettlementId = f[7],
				FirstWorkId = f[8], SecondWorkId = f[9], FirstZoneId = f[10],
				SecondZoneId = f[11], FirstInputStoreId = f[12], FirstOutputStoreId = f[13],
				SecondInputStoreId = f[14], SecondOutputStoreId = f[15],
				FirstGateKey = f[16], SecondGateKey = f[17], RouteDigest = f[18],
				BootstrapUsed = bootstrap, ReturnUsed = returned,
				NextKind = (KingdomPurposeKind)next, CreditCargoId = f[22],
				CreditCargoReceipt = f[23], Phase = (KingdomPurposePairPhase)phase,
				ResumePhase = (KingdomPurposePairPhase)resume, Operation = operation,
				Revision = revision, Fault = f[28], NextOperationOrdinal = nextOrdinal
			};
			return ValidPair(Pair, out _) && EncodePair(Pair) == Receipt;
		}

		private static string EncodeFields(IList<string> Fields)
		{
			StringBuilder text = new StringBuilder("pv1");
			for (int i = 0; i < Fields.Count; i++)
			{
				string value = Fields[i] ?? "";
				text.Append(';').Append(value.Length.ToString(CultureInfo.InvariantCulture))
					.Append(':').Append(value);
				if (text.Length > MaxReceiptChars) return null;
			}
			return text.ToString();
		}

		private static bool TryDecodeFields(string Text, int Count, out string[] Fields)
		{
			Fields = null;
			if (string.IsNullOrEmpty(Text) || Text.Length > MaxReceiptChars
				|| !Text.StartsWith("pv1", StringComparison.Ordinal)) return false;
			string[] values = new string[Count];
			int at = 3;
			for (int i = 0; i < Count; i++)
			{
				if (at >= Text.Length || Text[at++] != ';') return false;
				int colon = Text.IndexOf(':', at);
				if (colon < at || colon - at > 8 || !Int(Text.Substring(at, colon - at), out int length)
					|| length < 0 || colon + 1 + length > Text.Length) return false;
				values[i] = Text.Substring(colon + 1, length);
				at = colon + 1 + length;
			}
			if (at != Text.Length) return false;
			Fields = values;
			return true;
		}

		private static string N(long Value) => Value.ToString(CultureInfo.InvariantCulture);
		private static string B(bool Value) => Value ? "1" : "0";
		private static bool Int(string Value, out int Parsed) => int.TryParse(Value,
			NumberStyles.None, CultureInfo.InvariantCulture, out Parsed);
		private static bool Long(string Value, out long Parsed) => long.TryParse(Value,
			NumberStyles.None, CultureInfo.InvariantCulture, out Parsed);
		private static bool Bool(string Value, out bool Parsed)
		{
			Parsed = Value == "1";
			return Parsed || Value == "0";
		}
	}
}
