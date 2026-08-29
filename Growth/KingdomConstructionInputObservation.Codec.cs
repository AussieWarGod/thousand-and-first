using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputObservationCodec
	{
		private const int Magic = 0x5441494F;
		private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

		public static bool TryEncode(KingdomConstructionInputObservationBook book,
			out string encoded)
		{
			encoded = null;
			if (!KingdomConstructionInputObservationRules.Valid(book)) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Utf8))
				{
					writer.Write(Magic); writer.Write(book.Schema);
					WriteText(writer, book.RealmId); writer.Write(book.RealmEpoch);
					writer.Write(book.ZoneCount);
					for (int i = 0; i < book.ZoneCount; i++) WriteZone(writer, book.ZoneAt(i));
					writer.Flush();
					if (stream.Length > KingdomConstructionInputObservationRules.MaxPayloadBytes)
						return false;
					encoded = Convert.ToBase64String(stream.ToArray());
					return true;
				}
			}
			catch { encoded = null; return false; }
		}

		public static bool TryDecode(string encoded,
			out KingdomConstructionInputObservationBook book)
		{
			book = null;
			int maxEncoded = ((KingdomConstructionInputObservationRules.MaxPayloadBytes + 2)
				/ 3) * 4;
			if (string.IsNullOrEmpty(encoded) || encoded.Length > maxEncoded) return false;
			try
			{
				byte[] payload = Convert.FromBase64String(encoded);
				if (payload.Length > KingdomConstructionInputObservationRules.MaxPayloadBytes)
					return false;
				if (Convert.ToBase64String(payload) != encoded) return false;
				using (MemoryStream stream = new MemoryStream(payload, false))
				using (BinaryReader reader = new BinaryReader(stream, Utf8))
				{
					if (reader.ReadInt32() != Magic) return false;
					int schema = reader.ReadInt32();
					string realm = ReadText(reader, 128); long epoch = reader.ReadInt64();
					int count = reader.ReadInt32();
					if (schema != KingdomConstructionInputObservationRules.Schema
						|| count < 0 || count > KingdomConstructionInputObservationRules.MaxZones)
						return false;
					KingdomConstructionInputZoneObservation[] zones =
						new KingdomConstructionInputZoneObservation[count];
					int totalLines = 0;
					for (int i = 0; i < count; i++)
						zones[i] = ReadZone(reader, ref totalLines);
					if (stream.Position != stream.Length) return false;
					book = new KingdomConstructionInputObservationBook(schema, realm, epoch, zones);
					return KingdomConstructionInputObservationRules.Valid(book);
				}
			}
			catch { book = null; return false; }
		}

		private static void WriteZone(BinaryWriter writer,
			KingdomConstructionInputZoneObservation zone)
		{
			WriteText(writer, zone.SettlementId); WriteText(writer, zone.ZoneId);
			writer.Write(zone.ObservedTick); writer.Write(zone.DailyWaterUpkeep);
			writer.Write(zone.Width); writer.Write(zone.Height);
			byte[] passable = zone.CopyPassable(), paved = zone.CopyPaved();
			writer.Write(passable.Length); writer.Write(passable);
			writer.Write(paved.Length); writer.Write(paved); writer.Write(zone.LineCount);
			for (int i = 0; i < zone.LineCount; i++)
			{
				KingdomConstructionInputObservationLine line = zone.LineAt(i);
				writer.Write((byte)line.Kind); WriteText(writer, line.Classification);
				WriteText(writer, line.HolderId); WriteText(writer, line.SourceObjectId);
				writer.Write((byte)line.Topology); writer.Write(line.X); writer.Write(line.Y);
				WriteText(writer, line.Blueprint); writer.Write(line.Count);
				writer.Write(line.DedicationOrdinal); writer.Write(line.AlwaysStack);
				writer.Write(line.ProtectedCargo);
			}
		}

		private static KingdomConstructionInputZoneObservation ReadZone(BinaryReader reader,
			ref int totalLines)
		{
			string settlement = ReadText(reader, 128), zoneId = ReadText(reader, 128);
			long tick = reader.ReadInt64(); int upkeep = reader.ReadInt32();
			int width = reader.ReadInt32(), height = reader.ReadInt32();
			int cells = checked(width * height);
			if (cells <= 0 || cells > KingdomConstructionInputObservationRules.MaxCells)
				throw new InvalidDataException();
			byte[] passable = ReadBytes(reader, cells), paved = ReadBytes(reader, cells);
			int count = reader.ReadInt32();
			if (count < 0 || count > KingdomConstructionInputObservationRules.MaxLines)
				throw new InvalidDataException();
			if (totalLines > KingdomConstructionInputObservationRules.MaxLines - count)
				throw new InvalidDataException();
			totalLines += count;
			KingdomConstructionInputObservationLine[] lines =
				new KingdomConstructionInputObservationLine[count];
			for (int i = 0; i < count; i++)
				lines[i] = new KingdomConstructionInputObservationLine(
					(KingdomConstructionInputKind)reader.ReadByte(), ReadText(reader, 512),
					ReadText(reader, 128), ReadText(reader, 128),
					(KingdomConstructionInputTopology)reader.ReadByte(), reader.ReadInt32(),
					reader.ReadInt32(), ReadText(reader, 160), reader.ReadInt32(),
					reader.ReadInt32(), reader.ReadBoolean(), reader.ReadBoolean());
			return new KingdomConstructionInputZoneObservation(settlement, zoneId, tick,
				upkeep, width, height, passable, paved, lines);
		}

		private static void WriteText(BinaryWriter writer, string value)
		{
			byte[] bytes = Utf8.GetBytes(value ?? ""); writer.Write(bytes.Length); writer.Write(bytes);
		}
		private static string ReadText(BinaryReader reader, int maxChars)
		{
			int count = reader.ReadInt32();
			if (count < 0 || count > maxChars * 4) throw new InvalidDataException();
			string value = Utf8.GetString(reader.ReadBytes(count));
			if (value.Length > maxChars) throw new InvalidDataException(); return value;
		}
		private static byte[] ReadBytes(BinaryReader reader, int expected)
		{
			if (reader.ReadInt32() != expected) throw new InvalidDataException();
			byte[] value = reader.ReadBytes(expected);
			if (value.Length != expected) throw new EndOfStreamException(); return value;
		}
	}
}
