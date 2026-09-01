using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCodec
	{
		private static byte[] EncodePayloadV7(KingdomPolityLedger L)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(stream))
			{
				WriteHeader(w, L, KingdomPolityRules.AmbientPriorFormatVersion);
				WriteOptions(w, L.Options);
				WriteAuthorityRowsV7(w, L, WriteFigure, WriteCohort, WriteIncident);
				WriteList(w, L.Projections, KingdomPolityRules.MaxProjections, WriteProjection);
				w.Write(L.FoldedCompactionCount); WriteString(w, L.FoldedCompactionDigest);
				WriteList(w, L.Compactions, KingdomPolityRules.MaxCompactions, WriteCompaction);
				w.Flush(); return BoundedPayload(stream);
			}
		}

		private static KingdomPolityLedger DecodePayloadV7(byte[] Payload)
		{
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader r = new BinaryReader(stream))
			{
				KingdomPolityLedger l = ReadHeader(r, KingdomPolityRules.AmbientPriorFormatVersion);
				l.Options = ReadOptions(r); ReadAuthorityRowsV7(r, l, ReadFigure, ReadCohortV6,
					ReadIncident);
				l.Projections = ReadList(r, KingdomPolityRules.MaxProjections, ReadProjection);
				l.FoldedCompactionCount = r.ReadInt64(); l.FoldedCompactionDigest = ReadString(r);
				l.Compactions = ReadList(r, KingdomPolityRules.MaxCompactions, ReadCompaction);
				RequireEnd(stream); return l;
			}
		}

		private static void WriteAuthorityRowsV7(BinaryWriter W, KingdomPolityLedger L,
			RowWriter<KingdomPolityNamedFigureRecord> WriteNamedFigure,
			RowWriter<KingdomPolityCohortPlan> WriteCohortRow,
			RowWriter<KingdomPolityIncidentRecord> WriteIncidentRow)
		{
			WriteList(W, L.Polities, KingdomPolityRules.MaxPolities, WritePolity);
			WriteList(W, L.Relations, KingdomPolityRules.MaxRelations, WriteRelation);
			WriteList(W, L.Profiles, KingdomPolityRules.MaxProfiles, WriteProfileV7);
			WriteList(W, L.Routes, KingdomPolityRules.MaxRoutes, WriteRoute);
			WriteList(W, L.Grievances, KingdomPolityRules.MaxGrievances, WriteGrievance);
			WriteList(W, L.Fronts, KingdomPolityRules.MaxFronts, WriteFront);
			WriteList(W, L.Cohorts, KingdomPolityRules.MaxCohorts, WriteCohortRow);
			WriteList(W, L.NamedFigures, KingdomPolityRules.MaxNamedFigures, WriteNamedFigure);
			WriteList(W, L.Incidents, KingdomPolityRules.MaxIncidents, WriteIncidentRow);
		}

		private static void ReadAuthorityRowsV7(BinaryReader R, KingdomPolityLedger L,
			RowReader<KingdomPolityNamedFigureRecord> ReadNamedFigure,
			RowReader<KingdomPolityCohortPlan> ReadCohortRow,
			RowReader<KingdomPolityIncidentRecord> ReadIncidentRow)
		{
			L.Polities = ReadList(R, KingdomPolityRules.MaxPolities, ReadPolity);
			L.Relations = ReadList(R, KingdomPolityRules.MaxRelations, ReadRelation);
			L.Profiles = ReadList(R, KingdomPolityRules.MaxProfiles, ReadProfileV7);
			L.Routes = ReadList(R, KingdomPolityRules.MaxRoutes, ReadRoute);
			L.Grievances = ReadList(R, KingdomPolityRules.MaxGrievances, ReadGrievance);
			L.Fronts = ReadList(R, KingdomPolityRules.MaxFronts, ReadFront);
			L.Cohorts = ReadList(R, KingdomPolityRules.MaxCohorts, ReadCohortRow);
			L.NamedFigures = ReadList(R, KingdomPolityRules.MaxNamedFigures, ReadNamedFigure);
			L.Incidents = ReadList(R, KingdomPolityRules.MaxIncidents, ReadIncidentRow);
		}
	}
}
