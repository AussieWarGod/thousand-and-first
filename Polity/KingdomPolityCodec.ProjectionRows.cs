using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCodec
	{
		private static void WriteProjection(BinaryWriter W, KingdomPolityProjectionReceipt V)
		{
			WriteString(W, V.ProjectionId); W.Write((byte)V.Kind); WriteString(W, V.SourceRef);
			W.Write((byte)V.Phase); WriteString(W, V.ZoneId);
			WriteStrings(W, V.ObjectIds, KingdomPolityRules.MaxRefs); WriteString(W, V.PriorDigest);
			WriteString(W, V.AppliedDigest); W.Write(V.PreparedTick); W.Write(V.CommittedTick);
		}

		private static KingdomPolityProjectionReceipt ReadProjection(BinaryReader R)
		{
			return new KingdomPolityProjectionReceipt
			{
				ProjectionId = ReadString(R), Kind = (KingdomPolityProjectionKind)R.ReadByte(),
				SourceRef = ReadString(R), Phase = (KingdomPolityProjectionPhase)R.ReadByte(),
				ZoneId = ReadString(R), ObjectIds = ReadStrings(R, KingdomPolityRules.MaxRefs),
				PriorDigest = ReadString(R), AppliedDigest = ReadString(R),
				PreparedTick = R.ReadInt64(), CommittedTick = R.ReadInt64()
			};
		}

		private static void WriteCompaction(BinaryWriter W, KingdomPolityCompactionReceipt V)
		{
			WriteString(W, V.ReceiptId); W.Write(V.SourceRevision); W.Write(V.CommittedRevision);
			W.Write(V.CommitTick);
			WriteList(W, V.RemovedProfiles, KingdomPolityRules.MaxProfiles, WriteProfileRef);
			WriteString(W, V.RemovedDigest);
		}

		private static KingdomPolityCompactionReceipt ReadCompaction(BinaryReader R)
		{
			return new KingdomPolityCompactionReceipt
			{
				ReceiptId = ReadString(R), SourceRevision = R.ReadInt64(),
				CommittedRevision = R.ReadInt64(), CommitTick = R.ReadInt64(),
				RemovedProfiles = ReadList(R, KingdomPolityRules.MaxProfiles, ReadProfileRef),
				RemovedDigest = ReadString(R)
			};
		}
	}
}
