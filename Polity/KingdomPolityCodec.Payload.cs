using System;
using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCodec
	{
		private static byte[] EncodePayloadV6(KingdomPolityLedger L)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(stream))
			{
				WriteHeader(w, L, KingdomPolityRules.PreviousFormatVersion);
				WriteOptions(w, L.Options);
				WriteAuthorityRows(w, L, WriteFigure, WriteCohort, WriteIncident);
				WriteList(w, L.Projections, KingdomPolityRules.MaxProjections, WriteProjection);
				w.Write(L.FoldedCompactionCount); WriteString(w, L.FoldedCompactionDigest);
				WriteList(w, L.Compactions, KingdomPolityRules.MaxCompactions, WriteCompaction);
				w.Flush(); return BoundedPayload(stream);
			}
		}

		private static byte[] EncodePayloadV5(KingdomPolityLedger L)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(stream))
			{
				WriteHeader(w, L, KingdomPolityRules.ImmediatePriorFormatVersion);
				WriteOptions(w, L.Options);
				WriteAuthorityRows(w, L, WriteFigure, WriteCohort, WriteIncident);
				WriteList(w, L.Projections, KingdomPolityRules.MaxProjections, WriteProjection);
				w.Write(L.FoldedCompactionCount); WriteString(w, L.FoldedCompactionDigest);
				WriteList(w, L.Compactions, KingdomPolityRules.MaxCompactions, WriteCompaction);
				w.Flush(); return BoundedPayload(stream);
			}
		}

		private static byte[] EncodePayloadV4(KingdomPolityLedger L)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(stream))
			{
				WriteHeader(w, L, KingdomPolityRules.PriorFormatVersion);
				WriteOptions(w, L.Options);
				WriteAuthorityRows(w, L, WriteFigure, WriteCohort, WriteIncidentLegacy);
				WriteList(w, L.Projections, KingdomPolityRules.MaxProjections, WriteProjection);
				w.Write(L.FoldedCompactionCount); WriteString(w, L.FoldedCompactionDigest);
				WriteList(w, L.Compactions, KingdomPolityRules.MaxCompactions, WriteCompaction);
				w.Flush(); return BoundedPayload(stream);
			}
		}

		private static byte[] EncodePayloadV3(KingdomPolityLedger L)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(stream))
			{
				WriteHeader(w, L, KingdomPolityRules.OlderFormatVersion);
				WriteOptions(w, L.Options);
				WriteAuthorityRows(w, L, WriteFigure, WriteCohortLegacy, WriteIncidentLegacy);
				WriteList(w, L.Projections, KingdomPolityRules.MaxProjections, WriteProjection);
				w.Write(L.FoldedCompactionCount); WriteString(w, L.FoldedCompactionDigest);
				WriteList(w, L.Compactions, KingdomPolityRules.MaxCompactions, WriteCompaction);
				w.Flush(); return BoundedPayload(stream);
			}
		}

		private static byte[] EncodePayloadV2(KingdomPolityLedger L)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(stream))
			{
				WriteHeader(w, L, KingdomPolityRules.OldestFormatVersion);
				WriteOptions(w, L.Options);
				WriteAuthorityRows(w, L, WriteFigureLegacy, WriteCohortLegacy,
					WriteIncidentLegacy);
				WriteList(w, L.Projections, KingdomPolityRules.MaxProjections, WriteProjection);
				w.Write(L.FoldedCompactionCount); WriteString(w, L.FoldedCompactionDigest);
				WriteList(w, L.Compactions, KingdomPolityRules.MaxCompactions, WriteCompaction);
				w.Flush(); return BoundedPayload(stream);
			}
		}

		private static byte[] EncodePayloadV1(KingdomPolityLedger L)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(stream))
			{
				WriteHeader(w, L, KingdomPolityRules.LegacyFormatVersion);
				WriteAuthorityRows(w, L, WriteFigureLegacy, WriteCohortLegacy,
					WriteIncidentLegacy);
				w.Flush(); return BoundedPayload(stream);
			}
		}

		private static KingdomPolityLedger DecodePayloadV5(byte[] Payload)
		{
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader r = new BinaryReader(stream))
			{
				KingdomPolityLedger l = ReadHeader(r, KingdomPolityRules.ImmediatePriorFormatVersion);
				l.Options = ReadOptions(r); ReadAuthorityRows(r, l, ReadFigure, ReadCohort,
					ReadIncident);
				l.Projections = ReadList(r, KingdomPolityRules.MaxProjections, ReadProjection);
				l.FoldedCompactionCount = r.ReadInt64(); l.FoldedCompactionDigest = ReadString(r);
				l.Compactions = ReadList(r, KingdomPolityRules.MaxCompactions, ReadCompaction);
				RequireEnd(stream); return l;
			}
		}

		private static KingdomPolityLedger DecodePayloadV6(byte[] Payload)
		{
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader r = new BinaryReader(stream))
			{
				KingdomPolityLedger l = ReadHeader(r, KingdomPolityRules.PreviousFormatVersion);
				l.Options = ReadOptions(r); ReadAuthorityRows(r, l, ReadFigure, ReadCohortV6,
					ReadIncident);
				l.Projections = ReadList(r, KingdomPolityRules.MaxProjections, ReadProjection);
				l.FoldedCompactionCount = r.ReadInt64(); l.FoldedCompactionDigest = ReadString(r);
				l.Compactions = ReadList(r, KingdomPolityRules.MaxCompactions, ReadCompaction);
				RequireEnd(stream); return l;
			}
		}

		private static KingdomPolityLedger DecodePayloadV4(byte[] Payload)
		{
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader r = new BinaryReader(stream))
			{
				KingdomPolityLedger l = ReadHeader(r,
					KingdomPolityRules.PriorFormatVersion);
				l.Options = ReadOptions(r); ReadAuthorityRows(r, l, ReadFigure, ReadCohort,
					ReadIncidentLegacy);
				l.Projections = ReadList(r, KingdomPolityRules.MaxProjections, ReadProjection);
				l.FoldedCompactionCount = r.ReadInt64(); l.FoldedCompactionDigest = ReadString(r);
				l.Compactions = ReadList(r, KingdomPolityRules.MaxCompactions, ReadCompaction);
				RequireEnd(stream); return l;
			}
		}

		private static KingdomPolityLedger DecodePayloadV3(byte[] Payload)
		{
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader r = new BinaryReader(stream))
			{
				KingdomPolityLedger l = ReadHeader(r, KingdomPolityRules.OlderFormatVersion);
				l.Options = ReadOptions(r); ReadAuthorityRows(r, l, ReadFigure, ReadCohortLegacy,
					ReadIncidentLegacy);
				l.Projections = ReadList(r, KingdomPolityRules.MaxProjections, ReadProjection);
				l.FoldedCompactionCount = r.ReadInt64(); l.FoldedCompactionDigest = ReadString(r);
				l.Compactions = ReadList(r, KingdomPolityRules.MaxCompactions, ReadCompaction);
				RequireEnd(stream); return l;
			}
		}

		private static KingdomPolityLedger DecodePayloadV2(byte[] Payload)
		{
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader r = new BinaryReader(stream))
			{
				KingdomPolityLedger l = ReadHeader(r, KingdomPolityRules.OldestFormatVersion);
				l.Options = ReadOptions(r); ReadAuthorityRows(r, l, ReadFigureLegacy,
					ReadCohortLegacy, ReadIncidentLegacy);
				l.Projections = ReadList(r, KingdomPolityRules.MaxProjections, ReadProjection);
				l.FoldedCompactionCount = r.ReadInt64(); l.FoldedCompactionDigest = ReadString(r);
				l.Compactions = ReadList(r, KingdomPolityRules.MaxCompactions, ReadCompaction);
				RequireEnd(stream); return l;
			}
		}

		private static KingdomPolityLedger DecodePayloadV1(byte[] Payload)
		{
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader r = new BinaryReader(stream))
			{
				KingdomPolityLedger l = ReadHeader(r, KingdomPolityRules.LegacyFormatVersion);
				l.Options = DisabledDefaultOptions(); ReadAuthorityRows(r, l, ReadFigureLegacy,
					ReadCohortLegacy, ReadIncidentLegacy);
				l.Projections.Clear(); l.Compactions.Clear(); RequireEnd(stream); return l;
			}
		}

		private static void WriteHeader(BinaryWriter W, KingdomPolityLedger L, int Version)
		{
			W.Write(Version); W.Write((byte)L.SchemaState); WriteString(W, L.SchemaFault);
			W.Write(L.MigratedFromVersion); WriteString(W, L.RealmId); WriteBool(W, L.IdentityBound);
			W.Write(L.Revision);
		}

		private static KingdomPolityLedger ReadHeader(BinaryReader R, int Expected)
		{
			int format = R.ReadInt32();
			if (format != Expected) throw new InvalidDataException("Polity nested format mismatch.");
			return new KingdomPolityLedger
			{
				FormatVersion = format,
				SchemaState = (KingdomPolitySchemaState)R.ReadByte(),
				SchemaFault = ReadString(R),
				MigratedFromVersion = R.ReadInt32(),
				RealmId = ReadString(R),
				IdentityBound = ReadBool(R),
				Revision = R.ReadInt64()
			};
		}

		private static void WriteAuthorityRows(BinaryWriter W, KingdomPolityLedger L,
			RowWriter<KingdomPolityNamedFigureRecord> WriteNamedFigure,
			RowWriter<KingdomPolityCohortPlan> WriteCohortRow,
			RowWriter<KingdomPolityIncidentRecord> WriteIncidentRow)
		{
			WriteList(W, L.Polities, KingdomPolityRules.MaxPolities, WritePolity);
			WriteList(W, L.Relations, KingdomPolityRules.MaxRelations, WriteRelation);
			WriteList(W, L.Profiles, KingdomPolityRules.MaxProfiles, WriteProfile);
			WriteList(W, L.Routes, KingdomPolityRules.MaxRoutes, WriteRoute);
			WriteList(W, L.Grievances, KingdomPolityRules.MaxGrievances, WriteGrievance);
			WriteList(W, L.Fronts, KingdomPolityRules.MaxFronts, WriteFront);
			WriteList(W, L.Cohorts, KingdomPolityRules.MaxCohorts, WriteCohortRow);
			WriteList(W, L.NamedFigures, KingdomPolityRules.MaxNamedFigures, WriteNamedFigure);
			WriteList(W, L.Incidents, KingdomPolityRules.MaxIncidents, WriteIncidentRow);
		}

		private static void ReadAuthorityRows(BinaryReader R, KingdomPolityLedger L,
			RowReader<KingdomPolityNamedFigureRecord> ReadNamedFigure,
			RowReader<KingdomPolityCohortPlan> ReadCohortRow,
			RowReader<KingdomPolityIncidentRecord> ReadIncidentRow)
		{
			L.Polities = ReadList(R, KingdomPolityRules.MaxPolities, ReadPolity);
			L.Relations = ReadList(R, KingdomPolityRules.MaxRelations, ReadRelation);
			L.Profiles = ReadList(R, KingdomPolityRules.MaxProfiles, ReadProfile);
			L.Routes = ReadList(R, KingdomPolityRules.MaxRoutes, ReadRoute);
			L.Grievances = ReadList(R, KingdomPolityRules.MaxGrievances, ReadGrievance);
			L.Fronts = ReadList(R, KingdomPolityRules.MaxFronts, ReadFront);
			L.Cohorts = ReadList(R, KingdomPolityRules.MaxCohorts, ReadCohortRow);
			L.NamedFigures = ReadList(R, KingdomPolityRules.MaxNamedFigures, ReadNamedFigure);
			L.Incidents = ReadList(R, KingdomPolityRules.MaxIncidents, ReadIncidentRow);
		}

		private static void WriteOptions(BinaryWriter W, KingdomPolityOptions O)
		{
			W.Write((byte)O.ImportPolicy); WriteBool(W, O.ImportPolicyFrozen);
			W.Write((byte)O.Presentation); W.Write(O.ObservedTick); W.Write(O.EnableEpoch);
			W.Write(O.FutureCauseFloorTick);
		}

		private static KingdomPolityOptions ReadOptions(BinaryReader R)
		{
			return new KingdomPolityOptions
			{
				ImportPolicy = (KingdomPolityImportPolicy)R.ReadByte(),
				ImportPolicyFrozen = ReadBool(R),
				Presentation = (KingdomPolityPresentationState)R.ReadByte(),
				ObservedTick = R.ReadInt64(), EnableEpoch = R.ReadInt64(),
				FutureCauseFloorTick = R.ReadInt64()
			};
		}

		internal static KingdomPolityOptions DisabledDefaultOptions()
		{
			return new KingdomPolityOptions
			{
				ImportPolicy = KingdomPolityImportPolicy.Off,
				Presentation = KingdomPolityPresentationState.Unobserved,
				FutureCauseFloorTick = long.MaxValue
			};
		}

		private static byte[] BoundedPayload(MemoryStream Stream)
		{
			if (Stream.Length > MaxEnvelopeBytes - 12)
				throw new InvalidDataException("Polity payload exceeds hard bound.");
			return Stream.ToArray();
		}
	}
}
