using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCodec
	{
		private static byte[] EncodePayloadV9(KingdomPolityLedger L)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(stream))
			{
				WriteHeader(w, L, KingdomPolityRules.CurrentFormatVersion);
				WriteOptions(w, L.Options); WriteAuthorityRowsV9(w, L);
				WriteList(w, L.Projections, KingdomPolityRules.MaxProjections, WriteProjection);
				w.Write(L.FoldedCompactionCount); WriteString(w, L.FoldedCompactionDigest);
				WriteList(w, L.Compactions, KingdomPolityRules.MaxCompactions, WriteCompaction);
				w.Flush(); return BoundedPayload(stream);
			}
		}

		private static KingdomPolityLedger DecodePayloadV9(byte[] Payload)
		{
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader r = new BinaryReader(stream))
			{
				KingdomPolityLedger l = ReadHeader(r, KingdomPolityRules.CurrentFormatVersion);
				l.Options = ReadOptions(r); ReadAuthorityRowsV9(r, l);
				l.Projections = ReadList(r, KingdomPolityRules.MaxProjections, ReadProjection);
				l.FoldedCompactionCount = r.ReadInt64(); l.FoldedCompactionDigest = ReadString(r);
				l.Compactions = ReadList(r, KingdomPolityRules.MaxCompactions, ReadCompaction);
				RequireEnd(stream); return l;
			}
		}

		private static void WriteAuthorityRowsV9(BinaryWriter W, KingdomPolityLedger L)
		{
			WriteList(W, L.Polities, KingdomPolityRules.MaxPolities, WritePolity);
			WriteList(W, L.Relations, KingdomPolityRules.MaxRelations, WriteRelationV9);
			WriteList(W, L.Profiles, KingdomPolityRules.MaxProfiles, WriteProfileV7);
			WriteList(W, L.Routes, KingdomPolityRules.MaxRoutes, WriteRoute);
			WriteList(W, L.Grievances, KingdomPolityRules.MaxGrievances, WriteGrievance);
			WriteList(W, L.Fronts, KingdomPolityRules.MaxFronts, WriteFront);
			WriteList(W, L.Cohorts, KingdomPolityRules.MaxCohorts, WriteCohortV9);
			WriteList(W, L.NamedFigures, KingdomPolityRules.MaxNamedFigures, WriteFigureV8);
			WriteList(W, L.Incidents, KingdomPolityRules.MaxIncidents, WriteIncident);
		}

		private static void ReadAuthorityRowsV9(BinaryReader R, KingdomPolityLedger L)
		{
			L.Polities = ReadList(R, KingdomPolityRules.MaxPolities, ReadPolity);
			L.Relations = ReadList(R, KingdomPolityRules.MaxRelations, ReadRelationV9);
			L.Profiles = ReadList(R, KingdomPolityRules.MaxProfiles, ReadProfileV7);
			L.Routes = ReadList(R, KingdomPolityRules.MaxRoutes, ReadRoute);
			L.Grievances = ReadList(R, KingdomPolityRules.MaxGrievances, ReadGrievance);
			L.Fronts = ReadList(R, KingdomPolityRules.MaxFronts, ReadFront);
			L.Cohorts = ReadList(R, KingdomPolityRules.MaxCohorts, ReadCohortV9);
			L.NamedFigures = ReadList(R, KingdomPolityRules.MaxNamedFigures, ReadFigureV8);
			L.Incidents = ReadList(R, KingdomPolityRules.MaxIncidents, ReadIncident);
		}
	}
}
