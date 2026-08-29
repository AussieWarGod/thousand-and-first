using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	public static partial class KingdomTradeCodec
	{

		private static KingdomTradeBook DecodePayload(byte[] Payload)
		{
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader reader = new BinaryReader(stream))
			{
				KingdomTradeBook book = new KingdomTradeBook
				{
					FormatVersion = reader.ReadInt32(),
					SchemaState = (KingdomTradeSchemaState)reader.ReadByte(),
					SchemaFault = ReadString(reader),
					LegacyMigrated = ReadExactBoolean(reader),
					LegacyRejected = reader.ReadInt32(),
					RealmId = ReadString(reader),
					IdentityBound = ReadExactBoolean(reader),
					SettlementIds = ReadStringList(reader, KingdomTradeRules.MaxSettlementIds),
					OptionState = (KingdomTradeOptionState)reader.ReadByte(),
					OptionObservedTick = reader.ReadInt64(),
					OptionEpoch = reader.ReadInt64(),
					RestampPending = ReadExactBoolean(reader),
					NextCharterSequence = reader.ReadInt64(),
					NextOperationSequence = reader.ReadInt64(),
					RetiredThrough = reader.ReadInt64(),
					Charters = ReadList(reader, KingdomTradeRules.MaxCharters, ReadCharter),
					Manifest = ReadNullable(reader, ReadManifest),
					OpenOperation = ReadNullable(reader, ReadOperation),
					PendingRetirement = ReadNullable(reader, ReadProof),
					RecentProofs = ReadList(reader, KingdomTradeRules.MaxRecentProofs, ReadProof),
					CompactedProofs = ReadList(reader, KingdomTradeRules.MaxCompactedProofs,
						ReadProofCompaction),
					ActiveProjectionId = ReadString(reader),
					ActiveProjectionObjectId = ReadString(reader),
					Projections = ReadList(reader, KingdomTradeRules.MaxProjectionRows, ReadProjection),
					RetainedEscrowDrams = reader.ReadInt64(),
					UnattributedArchivedEscrowDrams = reader.ReadInt64(),
					Archives = ReadList(reader, KingdomTradeRules.MaxArchives, ReadArchive),
					Incidents = ReadList(reader, KingdomTradeRules.MaxIncidents, ReadIncident)
				};
				if (stream.Position != stream.Length) throw new InvalidDataException("Trailing Trade payload bytes.");
				return book;
			}
		}

		private static KingdomTradeBook DecodePayloadV3(byte[] Payload)
		{
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader reader = new BinaryReader(stream))
			{
				KingdomTradeBook book = new KingdomTradeBook
				{
					FormatVersion = reader.ReadInt32(),
					SchemaState = (KingdomTradeSchemaState)reader.ReadByte(),
					SchemaFault = ReadString(reader),
					LegacyMigrated = ReadExactBoolean(reader),
					LegacyRejected = reader.ReadInt32(),
					RealmId = ReadString(reader),
					IdentityBound = ReadExactBoolean(reader),
					SettlementIds = ReadStringList(reader, KingdomTradeRules.MaxSettlementIds),
					OptionState = (KingdomTradeOptionState)reader.ReadByte(),
					OptionObservedTick = reader.ReadInt64(),
					OptionEpoch = reader.ReadInt64(),
					RestampPending = ReadExactBoolean(reader),
					NextCharterSequence = reader.ReadInt64(),
					NextOperationSequence = reader.ReadInt64(),
					RetiredThrough = reader.ReadInt64(),
					Charters = ReadList(reader, KingdomTradeRules.MaxCharters, ReadCharter),
					Manifest = ReadNullable(reader, ReadManifest),
					OpenOperation = ReadNullable(reader, ReadOperationV3),
					PendingRetirement = ReadNullable(reader, ReadProofV4),
					RecentProofs = ReadList(reader, KingdomTradeRules.MaxRecentProofs, ReadProofV4),
					CompactedProofs = ReadList(reader, KingdomTradeRules.MaxCompactedProofs,
						ReadProofCompaction),
					ActiveProjectionId = ReadString(reader),
					ActiveProjectionObjectId = ReadString(reader),
					Projections = ReadList(reader, KingdomTradeRules.MaxProjectionRows, ReadProjection),
					RetainedEscrowDrams = reader.ReadInt64(),
					UnattributedArchivedEscrowDrams = reader.ReadInt64(),
					Archives = ReadList(reader, KingdomTradeRules.MaxArchives, ReadArchive),
					Incidents = ReadList(reader, KingdomTradeRules.MaxIncidents, ReadIncident)
				};
				if (stream.Position != stream.Length)
					throw new InvalidDataException("Trailing Trade wire-v3 payload bytes.");
				KingdomTradeRules.MigrateWireV3(book);
				return book;
			}
		}

		private delegate void RowWriter<T>(BinaryWriter Writer, T Row);
		private delegate T RowReader<T>(BinaryReader Reader);

		private static void WriteNullable<T>(BinaryWriter Writer, T Row, RowWriter<T> WriteRow)
			where T : class
		{
			Writer.Write(Row != null);
			if (Row != null) WriteRow(Writer, Row);
		}

		private static T ReadNullable<T>(BinaryReader Reader, RowReader<T> ReadRow)
			where T : class
		{
			return ReadExactBoolean(Reader) ? ReadRow(Reader) : null;
		}

		private static bool ReadExactBoolean(BinaryReader Reader)
		{
			byte value = Reader.ReadByte();
			if (value > 1) throw new InvalidDataException("Trade boolean is not canonical 0/1.");
			return value == 1;
		}

		private static void WriteList<T>(BinaryWriter Writer, List<T> Rows, int Maximum,
			RowWriter<T> WriteRow) where T : class
		{
			if (Rows == null) throw new InvalidDataException("Missing Trade evidence list.");
			int count = Rows.Count;
			if (count < 0 || count > Maximum) throw new InvalidDataException("Trade list exceeds hard bound.");
			Writer.Write(count);
			for (int i = 0; i < count; i++) WriteNullable(Writer, Rows[i], WriteRow);
		}

		private static List<T> ReadList<T>(BinaryReader Reader, int Maximum, RowReader<T> ReadRow)
			where T : class
		{
			int count = ReadCount(Reader, Maximum, "list rows");
			List<T> rows = new List<T>(count);
			for (int i = 0; i < count; i++) rows.Add(ReadNullable(Reader, ReadRow));
			return rows;
		}

		private static void WriteStringList(BinaryWriter Writer, List<string> Rows, int Maximum)
		{
			if (Rows == null) throw new InvalidDataException("Missing Trade string evidence list.");
			int count = Rows.Count;
			if (count < 0 || count > Maximum) throw new InvalidDataException("Trade string list exceeds hard bound.");
			Writer.Write(count);
			for (int i = 0; i < count; i++) WriteString(Writer, Rows[i]);
		}

		private static List<string> ReadStringList(BinaryReader Reader, int Maximum)
		{
			int count = ReadCount(Reader, Maximum, "string list rows");
			List<string> rows = new List<string>(count);
			for (int i = 0; i < count; i++) rows.Add(ReadString(Reader));
			return rows;
		}

		private static void WriteString(BinaryWriter Writer, string Value)
		{
			if (Value == null) { Writer.Write(-1); return; }
			if (Value.Length > MaxStringBytes
				|| StrictUtf8.GetByteCount(Value) > MaxStringBytes)
				throw new InvalidDataException("Trade string exceeds hard byte bound.");
			byte[] bytes = StrictUtf8.GetBytes(Value);
			Writer.Write(bytes.Length);
			Writer.Write(bytes, 0, bytes.Length);
		}

		private static string ReadString(BinaryReader Reader)
		{
			int length = Reader.ReadInt32();
			if (length == -1) return null;
			if (length < 0 || length > MaxStringBytes) throw new InvalidDataException("Trade string exceeds hard byte bound.");
			byte[] bytes = Reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException("Truncated Trade string.");
			return StrictUtf8.GetString(bytes);
		}

		private static int ReadCount(BinaryReader Reader, int Maximum, string Name)
		{
			int count = Reader.ReadInt32();
			if (count < 0 || count > Maximum) throw new InvalidDataException("Trade " + Name + " exceeds hard bound.");
			return count;
		}
	}
}
