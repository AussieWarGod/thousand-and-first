using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCodec
	{
		private static void WriteRelationV9(BinaryWriter W, KingdomPolityRelation V)
		{
			WriteRelation(W, V); W.Write((byte)V.FoundationState); W.Write((byte)V.InitialBand);
			WriteString(W, V.FoundationOriginalCauseRef);
			WriteString(W, V.FoundationCorrectionReceiptId);
		}

		private static KingdomPolityRelation ReadRelationV9(BinaryReader R)
		{
			KingdomPolityRelation v = ReadRelation(R);
			v.FoundationState = (KingdomPolityFoundationRelationState)R.ReadByte();
			v.InitialBand = (KingdomPolityRelationBand)R.ReadByte();
			v.FoundationOriginalCauseRef = ReadString(R);
			v.FoundationCorrectionReceiptId = ReadString(R); return v;
		}

		private static void WriteCohortV9(BinaryWriter W, KingdomPolityCohortPlan V)
		{
			WriteCohort(W, V); WriteNullable(W, V.AmbientTransaction, WriteAmbientV9);
		}

		private static KingdomPolityCohortPlan ReadCohortV9(BinaryReader R)
		{
			KingdomPolityCohortPlan v = ReadCohortV6(R);
			v.AmbientTransaction = ReadNullable(R, ReadAmbientV9); return v;
		}

		private static void WriteAmbientV9(BinaryWriter W, KingdomPolityAmbientTransaction V)
		{
			WriteAmbient(W, V);
			// V8's nested writer ends at the handoff. V9 appends the consumer receipt outside
			// the frozen transaction digest so old terminal ids remain byte-identical.
			WriteNullable(W, V.AdmissionHandoff?.AdmissionReceipt, WriteAdmissionReceipt);
		}

		private static KingdomPolityAmbientTransaction ReadAmbientV9(BinaryReader R)
		{
			KingdomPolityAmbientTransaction v = ReadAmbient(R);
			KingdomPolityAdmissionReceipt receipt = ReadNullable(R, ReadAdmissionReceipt);
			if (receipt != null && v.AdmissionHandoff == null)
				throw new InvalidDataException("Admission receipt has no handoff owner.");
			if (v.AdmissionHandoff != null) v.AdmissionHandoff.AdmissionReceipt = receipt;
			return v;
		}

		private static void WriteAdmissionReceipt(BinaryWriter W,
			KingdomPolityAdmissionReceipt V)
		{
			W.Write(V.Version); WriteString(W, V.ReceiptId); WriteString(W, V.OperationId);
			WriteString(W, V.HandoffId); WriteString(W, V.RealmId);
			WriteString(W, V.SourcePolityId); WriteString(W, V.CohortId);
			WriteString(W, V.MemberId); WriteString(W, V.TargetSettlementId);
			WriteString(W, V.SourceObjectId); WriteString(W, V.SourceZoneId);
			W.Write((byte)V.Phase); W.Write(V.PreparedTick); W.Write(V.DecidedTick);
			W.Write(V.ResidentId); WriteString(W, V.BodyReceiptId);
			WriteString(W, V.Fault); WriteString(W, V.Digest);
		}

		private static KingdomPolityAdmissionReceipt ReadAdmissionReceipt(BinaryReader R)
		{
			return new KingdomPolityAdmissionReceipt
			{
				Version = R.ReadInt32(), ReceiptId = ReadString(R), OperationId = ReadString(R),
				HandoffId = ReadString(R), RealmId = ReadString(R),
				SourcePolityId = ReadString(R), CohortId = ReadString(R), MemberId = ReadString(R),
				TargetSettlementId = ReadString(R), SourceObjectId = ReadString(R),
				SourceZoneId = ReadString(R), Phase = (KingdomPolityAdmissionReceiptPhase)R.ReadByte(),
				PreparedTick = R.ReadInt64(), DecidedTick = R.ReadInt64(), ResidentId = R.ReadInt32(),
				BodyReceiptId = ReadString(R), Fault = ReadString(R), Digest = ReadString(R)
			};
		}
	}
}
