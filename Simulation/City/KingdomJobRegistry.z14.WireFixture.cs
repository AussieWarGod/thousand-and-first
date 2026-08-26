using System;
using System.Collections.Generic;
#if TAF_TESTS
using System.IO;
using System.Text;
#endif

using ThousandAndFirst.Simulation.Kernel;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
#if TAF_TESTS
	/// <summary>Engine-free fixture for the exact realm-archive job segment. Production owns the
	/// same field order in <c>KingdomRealmArchive.WriteJobs/ReadJobs</c>; this seam freezes v2 bytes
	/// and executes v2 padding, v3 rewrite, and repeated cold reads without mocking Qud's serializer.</summary>
	internal static class KingdomRealmJobWireFixture
	{
		internal const int LegacyVersion = 2;
		internal const int MissionVersion = 3;
		internal const int CurrentVersion = 4;
		private const int MaxJobs = 16;
		private const int MaxLegs = 96;
		private const int MaxChars = 512;
		private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

		internal static bool TryEncode(KingdomJobRegistry value, int version, out byte[] payload)
		{
			payload = null;
			if (value == null || (version != LegacyVersion && version != MissionVersion
				&& version != CurrentVersion)) return false;
			try
			{
				KingdomJobTable table;
				KingdomCityFault fault;
				if (!value.TryRead(out table, out fault)) return false;
				KingdomJobRegistry canonical = new KingdomJobRegistry { JobCounter = value.JobCounter };
				if (!canonical.TryPublish(table, out fault)) return false;
				if (version == LegacyVersion)
				{
					for (int i = 0; i < canonical.Count; i++)
						if (canonical.Kinds[i] != (int)KingdomJobKind.Delivery) return false;
				}
				if (version < CurrentVersion)
				{
					for (int i = 0; i < canonical.Count; i++)
						if (canonical.DeliveryPhases[i] != (int)KingdomDeliveryPhase.Legacy)
							return false;
				}
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(canonical.JobCounter);
					writer.Write(canonical.Count);
					for (int i = 0; i < canonical.Count; i++)
					{
						writer.Write(canonical.JobIds[i]); writer.Write(canonical.Kinds[i]);
						writer.Write(canonical.Cargos[i]); writer.Write(canonical.CargoAmounts[i]);
						WriteText(writer, canonical.SourceZoneIds[i]);
						WriteText(writer, canonical.DestZoneIds[i]);
						writer.Write(canonical.StartTicks[i]);
						writer.Write(canonical.WalkTicksPerCell[i]);
						writer.Write(canonical.Statuses[i]); writer.Write(canonical.OriginCodes[i]);
						writer.Write(canonical.DepositLegIndexes[i]);
						if (version >= MissionVersion)
						{
							writer.Write(canonical.SubjectIds[i]);
							WriteText(writer, canonical.SubjectNames[i]);
							WriteText(writer, canonical.TargetNames[i]);
							writer.Write(canonical.DueTicks[i]); writer.Write(canonical.WaterCosts[i]);
							writer.Write(canonical.ProvisionCosts[i]); writer.Write(canonical.OutcomeCodes[i]);
						}
						if (version >= CurrentVersion)
						{
							writer.Write(canonical.DeliverySourceEndpointIds[i]);
							WriteText(writer, canonical.DeliverySourceObjectIds[i]);
							writer.Write(canonical.DeliverySourceXs[i]);
							writer.Write(canonical.DeliverySourceYs[i]);
							writer.Write(canonical.DeliveryTargetEndpointIds[i]);
							WriteText(writer, canonical.DeliveryTargetObjectIds[i]);
							writer.Write(canonical.DeliveryTargetXs[i]);
							writer.Write(canonical.DeliveryTargetYs[i]);
							writer.Write(canonical.DeliverySourceBeforeAmounts[i]);
							writer.Write(canonical.DeliveryTripIds[i]);
							writer.Write(canonical.DeliveryStopOrdinals[i]);
							writer.Write(canonical.DeliveryPhases[i]);
							writer.Write(canonical.DeliveryCargoAuthorityKinds[i]);
							WriteText(writer, canonical.DeliveryOwnerOperationIds[i]);
							writer.Write(canonical.DeliveryOwnerManifestVersions[i]);
							WriteText(writer, canonical.DeliveryOwnerManifestDigests[i]);
							writer.Write(canonical.DeliveryOwnerManifestRevisions[i]);
							writer.Write(canonical.DeliveryManifestSourceStarts[i]);
							writer.Write(canonical.DeliveryManifestSourceCounts[i]);
							writer.Write(canonical.DeliveryTargetBeforeAmounts[i]);
							writer.Write(canonical.DeliveryTargetReceiptStates[i]);
						}
						writer.Write(canonical.LegCounts[i]);
					}
					writer.Write(canonical.LegZoneIds.Count);
					for (int i = 0; i < canonical.LegZoneIds.Count; i++)
					{
						WriteText(writer, canonical.LegZoneIds[i]);
						writer.Write(canonical.LegEnterX[i]); writer.Write(canonical.LegEnterY[i]);
						writer.Write(canonical.LegExitX[i]); writer.Write(canonical.LegExitY[i]);
						writer.Write(canonical.LegLengths[i]);
						writer.Write(canonical.LegDepartTicks[i]); writer.Write(canonical.LegArriveTicks[i]);
					}
					writer.Flush();
					payload = stream.ToArray();
					return true;
				}
			}
			catch { payload = null; return false; }
		}

		internal static bool TryDecode(byte[] payload, int version, out KingdomJobRegistry value)
		{
			value = null;
			if (payload == null || (version != LegacyVersion && version != MissionVersion
				&& version != CurrentVersion)) return false;
			try
			{
				KingdomJobRegistry read = new KingdomJobRegistry();
				using (MemoryStream stream = new MemoryStream(payload, false))
				using (BinaryReader reader = new BinaryReader(stream, StrictUtf8, true))
				{
					read.JobCounter = reader.ReadInt32();
					int jobs = reader.ReadInt32();
					if (read.JobCounter < 0 || jobs < 0 || jobs > MaxJobs) return false;
					for (int i = 0; i < jobs; i++)
					{
						read.JobIds.Add(reader.ReadInt32()); read.Kinds.Add(reader.ReadInt32());
						read.Cargos.Add(reader.ReadInt32()); read.CargoAmounts.Add(reader.ReadInt32());
						read.SourceZoneIds.Add(ReadText(reader)); read.DestZoneIds.Add(ReadText(reader));
						read.StartTicks.Add(reader.ReadInt64()); read.WalkTicksPerCell.Add(reader.ReadInt32());
						read.Statuses.Add(reader.ReadInt32()); read.OriginCodes.Add(reader.ReadInt32());
						read.DepositLegIndexes.Add(reader.ReadInt32());
						if (version >= MissionVersion)
						{
							read.SubjectIds.Add(reader.ReadInt32()); read.SubjectNames.Add(ReadText(reader));
							read.TargetNames.Add(ReadText(reader)); read.DueTicks.Add(reader.ReadInt64());
							read.WaterCosts.Add(reader.ReadInt32()); read.ProvisionCosts.Add(reader.ReadInt32());
							read.OutcomeCodes.Add(reader.ReadInt32());
						}
						if (version >= CurrentVersion)
						{
							read.DeliverySourceEndpointIds.Add(reader.ReadInt32());
							read.DeliverySourceObjectIds.Add(ReadText(reader));
							read.DeliverySourceXs.Add(reader.ReadInt32());
							read.DeliverySourceYs.Add(reader.ReadInt32());
							read.DeliveryTargetEndpointIds.Add(reader.ReadInt32());
							read.DeliveryTargetObjectIds.Add(ReadText(reader));
							read.DeliveryTargetXs.Add(reader.ReadInt32());
							read.DeliveryTargetYs.Add(reader.ReadInt32());
							read.DeliverySourceBeforeAmounts.Add(reader.ReadInt64());
							read.DeliveryTripIds.Add(reader.ReadInt32());
							read.DeliveryStopOrdinals.Add(reader.ReadInt32());
							read.DeliveryPhases.Add(reader.ReadInt32());
							read.DeliveryCargoAuthorityKinds.Add(reader.ReadInt32());
							read.DeliveryOwnerOperationIds.Add(ReadText(reader));
							read.DeliveryOwnerManifestVersions.Add(reader.ReadInt32());
							read.DeliveryOwnerManifestDigests.Add(ReadText(reader));
							read.DeliveryOwnerManifestRevisions.Add(reader.ReadInt64());
							read.DeliveryManifestSourceStarts.Add(reader.ReadInt32());
							read.DeliveryManifestSourceCounts.Add(reader.ReadInt32());
							read.DeliveryTargetBeforeAmounts.Add(reader.ReadInt64());
							read.DeliveryTargetReceiptStates.Add(reader.ReadInt32());
						}
						read.LegCounts.Add(reader.ReadInt32());
					}
					int legs = reader.ReadInt32();
					if (legs < 0 || legs > MaxLegs) return false;
					for (int i = 0; i < legs; i++)
					{
						read.LegZoneIds.Add(ReadText(reader)); read.LegEnterX.Add(reader.ReadInt32());
						read.LegEnterY.Add(reader.ReadInt32()); read.LegExitX.Add(reader.ReadInt32());
						read.LegExitY.Add(reader.ReadInt32()); read.LegLengths.Add(reader.ReadInt32());
						read.LegDepartTicks.Add(reader.ReadInt64()); read.LegArriveTicks.Add(reader.ReadInt64());
					}
					if (stream.Position != stream.Length) return false;
				}
				read.Normalize();
				KingdomJobTable table;
				KingdomCityFault fault;
				if (!read.TryRead(out table, out fault)) return false;
				value = read;
				return true;
			}
			catch { value = null; return false; }
		}

		private static void WriteText(BinaryWriter writer, string value)
		{
			if (value == null) { writer.Write(-1); return; }
			if (value.Length > MaxChars) throw new InvalidDataException();
			byte[] bytes = StrictUtf8.GetBytes(value);
			writer.Write(bytes.Length);
			writer.Write(bytes);
		}

		private static string ReadText(BinaryReader reader)
		{
			int length = reader.ReadInt32();
			if (length == -1) return null;
			if (length < 0 || length > MaxChars * 4) throw new InvalidDataException();
			byte[] bytes = reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			string value = StrictUtf8.GetString(bytes);
			if (value.Length > MaxChars) throw new InvalidDataException();
			return value;
		}
	}
#endif
}
