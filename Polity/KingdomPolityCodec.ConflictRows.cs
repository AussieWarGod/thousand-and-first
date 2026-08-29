using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCodec
	{
		private static void WriteIntervention(BinaryWriter W,
			KingdomPolityInterventionRecord V)
		{
			WriteString(W, V.InterventionId); WriteString(W, V.IncidentPlanId);
			W.Write((byte)V.Choice); WriteString(W, V.SurfaceRef); WriteString(W, V.ZoneId);
			W.Write(V.CommitTick); WriteString(W, V.ObservedFactId);
			WriteStrings(W, V.ParticipantProjectionIds, KingdomPolityRules.MaxRefs);
			WriteString(W, V.ReceiptId); WriteString(W, V.ProofDigest);
		}

		private static KingdomPolityInterventionRecord ReadIntervention(BinaryReader R)
		{
			return new KingdomPolityInterventionRecord
			{
				InterventionId = ReadString(R), IncidentPlanId = ReadString(R),
				Choice = (KingdomPolityInterventionChoice)R.ReadByte(),
				SurfaceRef = ReadString(R), ZoneId = ReadString(R), CommitTick = R.ReadInt64(),
				ObservedFactId = ReadString(R),
				ParticipantProjectionIds = ReadStrings(R, KingdomPolityRules.MaxRefs),
				ReceiptId = ReadString(R), ProofDigest = ReadString(R)
			};
		}

		private static void WriteAftermath(BinaryWriter W, KingdomPolityAftermathRecord V)
		{
			WriteString(W, V.AftermathId); WriteString(W, V.IncidentPlanId);
			WriteString(W, V.ConclusionId); W.Write((byte)V.Kind);
			WriteString(W, V.SurfaceRef); WriteString(W, V.ZoneId); W.Write(V.CommitTick);
			WriteString(W, V.ObservedFactId); WriteString(W, V.InterventionId);
			WriteString(W, V.ReceiptId); WriteString(W, V.ProofDigest);
		}

		private static KingdomPolityAftermathRecord ReadAftermath(BinaryReader R)
		{
			return new KingdomPolityAftermathRecord
			{
				AftermathId = ReadString(R), IncidentPlanId = ReadString(R),
				ConclusionId = ReadString(R), Kind = (KingdomPolityAftermathKind)R.ReadByte(),
				SurfaceRef = ReadString(R), ZoneId = ReadString(R), CommitTick = R.ReadInt64(),
				ObservedFactId = ReadString(R), InterventionId = ReadString(R),
				ReceiptId = ReadString(R), ProofDigest = ReadString(R)
			};
		}
	}
}
