using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCodec
	{
		private static void WriteFigureV8(BinaryWriter W, KingdomPolityNamedFigureRecord V)
		{
			WriteFigure(W, V); WriteString(W, V.DeedSummary);
		}

		private static KingdomPolityNamedFigureRecord ReadFigureV8(BinaryReader R)
		{
			KingdomPolityNamedFigureRecord value = ReadFigure(R);
			value.DeedSummary = ReadString(R); return value;
		}

		private static void WriteCohortV8(BinaryWriter W, KingdomPolityCohortPlan V)
		{
			WriteCohort(W, V); WriteNullable(W, V.AmbientTransaction, WriteAmbient);
		}

		private static KingdomPolityCohortPlan ReadCohortV8(BinaryReader R)
		{
			KingdomPolityCohortPlan value = ReadCohortV6(R);
			value.AmbientTransaction = ReadNullable(R, ReadAmbient); return value;
		}

		private static void WriteAmbient(BinaryWriter W, KingdomPolityAmbientTransaction V)
		{
			W.Write(V.Version); WriteString(W, V.TransactionId); W.Write((byte)V.Purpose);
			WriteString(W, V.SourcePolityId); WriteString(W, V.SourceSettlementId);
			WriteString(W, V.SourceSettlementName); WriteString(W, V.SourceZoneId);
			WriteString(W, V.DestinationSettlementId); WriteString(W, V.DestinationSettlementName);
			WriteString(W, V.DestinationZoneId); WriteString(W, V.LocalLocusRef);
			WriteStrings(W, V.FactRefs, KingdomPolityAmbientTransactionRules.MaximumFacts);
			WriteString(W, V.SafeDetail);
			WriteStrings(W, V.ManifestRefs, KingdomPolityAmbientTransactionRules.MaximumManifestRows);
			WriteStrings(W, V.PhysicalStockObjectIds,
				KingdomPolityAmbientTransactionRules.MaximumManifestRows);
			WriteString(W, V.NewsRef); W.Write(V.PreparedTick); WriteString(W, V.FrozenDigest);
			W.Write((byte)V.TerminalChoice); W.Write(V.TerminalTick);
			WriteString(W, V.TerminalReceiptId);
			WriteNullable(W, V.AdmissionHandoff, WriteAdmissionHandoff); WriteString(W, V.Fault);
		}

		private static KingdomPolityAmbientTransaction ReadAmbient(BinaryReader R)
		{
			return new KingdomPolityAmbientTransaction
			{
				Version = R.ReadInt32(), TransactionId = ReadString(R),
				Purpose = (KingdomPolityCohortPurpose)R.ReadByte(), SourcePolityId = ReadString(R),
				SourceSettlementId = ReadString(R), SourceSettlementName = ReadString(R),
				SourceZoneId = ReadString(R), DestinationSettlementId = ReadString(R),
				DestinationSettlementName = ReadString(R), DestinationZoneId = ReadString(R),
				LocalLocusRef = ReadString(R), FactRefs = ReadStrings(R,
					KingdomPolityAmbientTransactionRules.MaximumFacts), SafeDetail = ReadString(R),
				ManifestRefs = ReadStrings(R, KingdomPolityAmbientTransactionRules.MaximumManifestRows),
				PhysicalStockObjectIds = ReadStrings(R,
					KingdomPolityAmbientTransactionRules.MaximumManifestRows), NewsRef = ReadString(R),
				PreparedTick = R.ReadInt64(), FrozenDigest = ReadString(R),
				TerminalChoice = (KingdomPolityAmbientTerminalChoice)R.ReadByte(),
				TerminalTick = R.ReadInt64(), TerminalReceiptId = ReadString(R),
				AdmissionHandoff = ReadNullable(R, ReadAdmissionHandoff), Fault = ReadString(R)
			};
		}

		private static void WriteAdmissionHandoff(BinaryWriter W,
			KingdomPolityAdmissionHandoff V)
		{
			W.Write(V.Version); WriteString(W, V.HandoffId); WriteString(W, V.RealmId);
			WriteString(W, V.PolityId); WriteString(W, V.CohortId); WriteString(W, V.MemberId);
			WriteString(W, V.TargetSettlementId); WriteString(W, V.SourceObjectId);
			WriteString(W, V.SourceZoneId); WriteString(W, V.ProposedResidentName);
			W.Write((byte)V.Decision); W.Write(V.PreparedTick); W.Write(V.DecidedTick);
			WriteString(W, V.CauseDigest); WriteString(W, V.Fault);
		}

		private static KingdomPolityAdmissionHandoff ReadAdmissionHandoff(BinaryReader R)
		{
			return new KingdomPolityAdmissionHandoff
			{
				Version = R.ReadInt32(), HandoffId = ReadString(R), RealmId = ReadString(R),
				PolityId = ReadString(R), CohortId = ReadString(R), MemberId = ReadString(R),
				TargetSettlementId = ReadString(R), SourceObjectId = ReadString(R),
				SourceZoneId = ReadString(R), ProposedResidentName = ReadString(R),
				Decision = (KingdomPolityAdmissionDecision)R.ReadByte(),
				PreparedTick = R.ReadInt64(), DecidedTick = R.ReadInt64(),
				CauseDigest = ReadString(R), Fault = ReadString(R)
			};
		}
	}
}
