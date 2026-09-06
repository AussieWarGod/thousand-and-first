using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Harness
{
	/// <summary>Frozen native test witness after one departure. StepWire is opaque here;
	/// the native owner separately proves its actual subsidence protocol and save provenance.</summary>
	internal sealed class KingdomScenarioSaveSnapshot
	{
		internal readonly string GameId, StepWire, ZoneId, MissingObjectId;
		internal readonly long Now;
		internal readonly int LedgerDepartures, MissingResidentId;
		internal readonly IReadOnlyList<int> ResidentIds;
		internal readonly IReadOnlyList<string> ObjectIds;

		internal KingdomScenarioSaveSnapshot(string gameId, string stepWire, string zoneId,
			long now, int ledgerDepartures, int missingResidentId, string missingObjectId,
			int[] residentIds, string[] objectIds)
		{
			GameId = gameId; StepWire = stepWire; ZoneId = zoneId; Now = now;
			LedgerDepartures = ledgerDepartures; MissingResidentId = missingResidentId;
			MissingObjectId = missingObjectId;
			ResidentIds = residentIds == null ? null : Array.AsReadOnly((int[])residentIds.Clone());
			ObjectIds = objectIds == null ? null : Array.AsReadOnly((string[])objectIds.Clone());
		}
	}

	internal static class KingdomScenarioSaveSnapshotCodec
	{
		internal const int ResidentCount = 49;
		internal const int MaxStepWireChars = 131072;
		internal const int MaxWireChars = 262144;
		internal const string Prefix = "ssv1:";
		internal const int Magic = 0x31565354;
		internal const int Version = 1;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static bool Valid(KingdomScenarioSaveSnapshot value)
		{
			if (value == null || !Text(value.GameId, 36) || value.GameId.Length != 36
				|| !Guid.TryParseExact(value.GameId, "D", out Guid gameId)
				|| gameId.ToString("D") != value.GameId || !Text(value.StepWire, MaxStepWireChars)
				|| !Text(value.ZoneId, 128) || value.Now < 0 || value.LedgerDepartures < 1
				|| value.MissingResidentId <= 0 || !Text(value.MissingObjectId, 512)
				|| value.ResidentIds == null || value.ObjectIds == null
				|| value.ResidentIds.Count != ResidentCount || value.ObjectIds.Count != ResidentCount) return false;
			HashSet<int> residents = new HashSet<int>();
			HashSet<string> objects = new HashSet<string>(StringComparer.Ordinal);
			long bytes = 8 + 8 + 4 + 4 + 4;
			bytes += FrameBytes(value.GameId) + FrameBytes(value.StepWire)
				+ FrameBytes(value.ZoneId) + FrameBytes(value.MissingObjectId);
			for (int i = 0; i < ResidentCount; i++)
			{
				int id = value.ResidentIds[i]; string body = value.ObjectIds[i];
				if (id <= 0 || id == value.MissingResidentId || !residents.Add(id)
					|| !Text(body, 512) || body == value.MissingObjectId || !objects.Add(body)) return false;
				bytes += 4 + FrameBytes(body);
			}
			return Prefix.Length + ((bytes + 2) / 3) * 4 <= MaxWireChars;
		}

		internal static bool TryEncode(KingdomScenarioSaveSnapshot value, out string wire)
		{
			wire = null;
			if (!Valid(value)) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Utf8, true))
				{
					writer.Write(Magic); writer.Write(Version);
					WriteText(writer, value.GameId); WriteText(writer, value.StepWire); WriteText(writer, value.ZoneId);
					writer.Write(value.Now); writer.Write(value.LedgerDepartures); writer.Write(value.MissingResidentId);
					WriteText(writer, value.MissingObjectId); writer.Write(ResidentCount);
					for (int i = 0; i < ResidentCount; i++)
					{ writer.Write(value.ResidentIds[i]); WriteText(writer, value.ObjectIds[i]); }
					writer.Flush();
					string encoded = Prefix + Convert.ToBase64String(stream.ToArray());
					if (encoded.Length > MaxWireChars) return false;
					wire = encoded; return true;
				}
			}
			catch (Exception) { return false; }
		}

		internal static bool TryDecode(string wire, out KingdomScenarioSaveSnapshot value)
		{
			value = null;
			if (string.IsNullOrEmpty(wire) || wire.Length > MaxWireChars
				|| !wire.StartsWith(Prefix, StringComparison.Ordinal)) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream(Convert.FromBase64String(wire.Substring(Prefix.Length)), false))
				using (BinaryReader reader = new BinaryReader(stream, Utf8, true))
				{
					if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version) return false;
					string gameId = ReadText(reader, 36), step = ReadText(reader, MaxStepWireChars), zone = ReadText(reader, 128);
					long now = reader.ReadInt64(); int departures = reader.ReadInt32(), missingId = reader.ReadInt32();
					string missingObject = ReadText(reader, 512);
					if (reader.ReadInt32() != ResidentCount) return false;
					int[] ids = new int[ResidentCount]; string[] objects = new string[ResidentCount];
					for (int i = 0; i < ResidentCount; i++)
					{ ids[i] = reader.ReadInt32(); objects[i] = ReadText(reader, 512); }
					KingdomScenarioSaveSnapshot decoded = new KingdomScenarioSaveSnapshot(gameId, step, zone,
						now, departures, missingId, missingObject, ids, objects);
					if (stream.Position != stream.Length || !TryEncode(decoded, out string canonical) || canonical != wire)
						return false;
					value = decoded; return true;
				}
			}
			catch (Exception) { return false; }
		}

		private static bool Text(string value, int maxChars)
		{
			if (string.IsNullOrEmpty(value) || value.Length > maxChars) return false;
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if (char.IsControl(c)) return false;
				if (char.IsHighSurrogate(c))
				{
					if (i + 1 == value.Length || !char.IsLowSurrogate(value[++i])) return false;
				}
				else if (char.IsLowSurrogate(c)) return false;
			}
			return true;
		}

		private static long FrameBytes(string value) { return 4L + Utf8.GetByteCount(value); }
		private static void WriteText(BinaryWriter writer, string value)
		{
			byte[] bytes = Utf8.GetBytes(value); writer.Write(bytes.Length); writer.Write(bytes);
		}
		private static string ReadText(BinaryReader reader, int maxChars)
		{
			int length = reader.ReadInt32();
			if (length <= 0 || length > maxChars * 4
				|| length > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException();
			string value = Utf8.GetString(reader.ReadBytes(length));
			if (!Text(value, maxChars)) throw new InvalidDataException();
			return value;
		}
	}
}
