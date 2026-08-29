using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomExperienceCodec
	{
		private static void WriteFirstFeast(BinaryWriter W, KingdomFirstFeastReceipt R)
		{
			W.Write(R.Version); W.Write((byte)R.Phase); W.Write((byte)R.Choice);
			W.Write(R.Generation); WriteString(W, R.SettlementId);
			WriteString(W, R.SettlementName); WriteString(W, R.DeedId);
			WriteString(W, R.DeedText); W.Write(R.DeedTick);
			WriteString(W, R.GuestTerminalReceiptId);
			WriteString(W, R.GuestTerminalDigest); W.Write(R.GuestTerminalTick);
			WriteString(W, R.AdventureEventId); WriteString(W, R.AdventureFingerprint);
			W.Write(R.ProposerResidentId); WriteString(W, R.ProposerName);
			W.Write(R.WitnessResidentId); WriteString(W, R.WitnessName);
			WriteString(W, R.DishName); WriteString(W, R.Ingredients);
			WriteString(W, R.OfferedDedication); WriteString(W, R.AdaptedDedication);
			WriteString(W, R.PracticeId); W.Write(R.OfferedTick); W.Write(R.DecidedTick);
			W.Write(R.EnableEpoch); WriteString(W, R.Fault);
		}

		private static KingdomFirstFeastReceipt ReadFirstFeast(BinaryReader R)
		{
			return new KingdomFirstFeastReceipt
			{
				Version = R.ReadInt32(), Phase = (KingdomFirstFeastPhase)R.ReadByte(),
				Choice = (KingdomFirstFeastChoice)R.ReadByte(), Generation = R.ReadInt32(),
				SettlementId = ReadString(R), SettlementName = ReadString(R),
				DeedId = ReadString(R), DeedText = ReadString(R), DeedTick = R.ReadInt64(),
				GuestTerminalReceiptId = ReadString(R),
				GuestTerminalDigest = ReadString(R), GuestTerminalTick = R.ReadInt64(),
				AdventureEventId = ReadString(R), AdventureFingerprint = ReadString(R),
				ProposerResidentId = R.ReadInt32(), ProposerName = ReadString(R),
				WitnessResidentId = R.ReadInt32(), WitnessName = ReadString(R),
				DishName = ReadString(R), Ingredients = ReadString(R),
				OfferedDedication = ReadString(R), AdaptedDedication = ReadString(R),
				PracticeId = ReadString(R), OfferedTick = R.ReadInt64(),
				DecidedTick = R.ReadInt64(), EnableEpoch = R.ReadInt64(), Fault = ReadString(R)
			};
		}
	}
}
