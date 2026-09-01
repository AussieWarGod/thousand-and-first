using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCodec
	{
		private static byte[] EncodePayloadV8(KingdomPolityLedger L)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(stream))
			{
				WriteHeader(w, L, KingdomPolityRules.AdmissionPriorFormatVersion);
				WriteOptions(w, L.Options);
				WriteAuthorityRowsV7(w, L, WriteFigureV8, WriteCohortV8, WriteIncident);
				WriteList(w, L.Projections, KingdomPolityRules.MaxProjections, WriteProjection);
				w.Write(L.FoldedCompactionCount); WriteString(w, L.FoldedCompactionDigest);
				WriteList(w, L.Compactions, KingdomPolityRules.MaxCompactions, WriteCompaction);
				w.Flush(); return BoundedPayload(stream);
			}
		}

		private static KingdomPolityLedger DecodePayloadV8(byte[] Payload)
		{
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader r = new BinaryReader(stream))
			{
				KingdomPolityLedger l = ReadHeader(r,
					KingdomPolityRules.AdmissionPriorFormatVersion);
				l.Options = ReadOptions(r); ReadAuthorityRowsV7(r, l, ReadFigureV8,
					ReadCohortV8, ReadIncident);
				l.Projections = ReadList(r, KingdomPolityRules.MaxProjections, ReadProjection);
				l.FoldedCompactionCount = r.ReadInt64(); l.FoldedCompactionDigest = ReadString(r);
				l.Compactions = ReadList(r, KingdomPolityRules.MaxCompactions, ReadCompaction);
				RequireEnd(stream); return l;
			}
		}
	}
}
