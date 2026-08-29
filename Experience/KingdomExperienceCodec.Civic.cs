using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomExperienceCodec
	{
		private static void WriteOffice(BinaryWriter W, KingdomCivicOfficeReceipt R)
		{
			W.Write(R.Version); W.Write((byte)R.Phase); W.Write((byte)R.VacancyCause);
			W.Write(R.Generation); WriteString(W, R.SettlementId);
			WriteString(W, R.SettlementName); W.Write(R.WorkId);
			W.Write(R.HolderResidentId); WriteString(W, R.HolderName);
			WriteString(W, R.HolderObjectId); WriteBool(W, R.OwnsRole);
			W.Write(R.PredecessorResidentId); WriteString(W, R.PredecessorName);
			W.Write(R.ChangedTick); WriteString(W, R.Fault);
		}

		private static KingdomCivicOfficeReceipt ReadOffice(BinaryReader R)
		{
			return new KingdomCivicOfficeReceipt
			{
				Version = R.ReadInt32(), Phase = (KingdomCivicOfficePhase)R.ReadByte(),
				VacancyCause = (KingdomCivicOfficeVacancyCause)R.ReadByte(),
				Generation = R.ReadInt32(), SettlementId = ReadString(R),
				SettlementName = ReadString(R), WorkId = R.ReadInt32(),
				HolderResidentId = R.ReadInt32(), HolderName = ReadString(R),
				HolderObjectId = ReadString(R), OwnsRole = ReadBool(R),
				PredecessorResidentId = R.ReadInt32(), PredecessorName = ReadString(R),
				ChangedTick = R.ReadInt64(), Fault = ReadString(R)
			};
		}

		private static void WriteRemembrance(BinaryWriter W, KingdomRemembranceReceipt R)
		{
			W.Write(R.Version); W.Write((byte)R.Phase); W.Write(R.Generation);
			WriteString(W, R.SettlementId); WriteString(W, R.SettlementName);
			W.Write(R.SubjectResidentId); WriteString(W, R.SubjectName);
			W.Write(R.MournerResidentId); WriteString(W, R.MournerName);
			WriteString(W, R.CarrierObjectId); WriteString(W, R.CarrierZoneId);
			W.Write(R.DecidedTick); WriteString(W, R.Fault);
		}

		private static KingdomRemembranceReceipt ReadRemembrance(BinaryReader R)
		{
			return new KingdomRemembranceReceipt
			{
				Version = R.ReadInt32(), Phase = (KingdomRemembrancePhase)R.ReadByte(),
				Generation = R.ReadInt32(), SettlementId = ReadString(R),
				SettlementName = ReadString(R), SubjectResidentId = R.ReadInt32(),
				SubjectName = ReadString(R), MournerResidentId = R.ReadInt32(),
				MournerName = ReadString(R), CarrierObjectId = ReadString(R),
				CarrierZoneId = ReadString(R), DecidedTick = R.ReadInt64(),
				Fault = ReadString(R)
			};
		}

		private static void WriteVoice(BinaryWriter W, KingdomCivicVoiceReceipt R)
		{
			W.Write(R.Version); W.Write((byte)R.Fixture); W.Write(R.SourceVersion);
			WriteString(W, R.SourceId); WriteString(W, R.SettlementId); WriteVoiceText(W, R.Facts);
			W.Write(R.CauseTick); W.Write(R.EnableEpoch); W.Write(R.FirstResidentId);
			WriteString(W, R.FirstName); W.Write(R.SecondResidentId); WriteString(W, R.SecondName);
			WriteBool(W, R.CallbackConsumed); W.Write(R.CallbackTick);
		}

		private static KingdomCivicVoiceReceipt ReadVoice(BinaryReader R)
		{
			return new KingdomCivicVoiceReceipt
			{
				Version = R.ReadInt32(), Fixture = (KingdomCivicVoiceFixture)R.ReadByte(),
				SourceVersion = R.ReadInt32(), SourceId = ReadString(R),
				SettlementId = ReadString(R), Facts = ReadVoiceText(R), CauseTick = R.ReadInt64(),
				EnableEpoch = R.ReadInt64(), FirstResidentId = R.ReadInt32(),
				FirstName = ReadString(R), SecondResidentId = R.ReadInt32(),
				SecondName = ReadString(R), CallbackConsumed = ReadBool(R),
				CallbackTick = R.ReadInt64()
			};
		}
	}
}
