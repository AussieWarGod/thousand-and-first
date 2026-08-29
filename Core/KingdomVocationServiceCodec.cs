using System.IO;

namespace ThousandAndFirst
{
	/// <summary>Versioned service-book wire. V1/V2 migrate to current V3.</summary>
	public static partial class KingdomCivicPracticeCodec
	{
		private static byte[] EncodeServices(KingdomVocationServiceBook book)
		{
			if (!KingdomVocationServiceRules.TryValidate(book, out string failure))
				throw new InvalidDataException(failure);
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = Writer(stream))
			{
				WriteServiceHeader(writer, KingdomVocationServiceReceipt.CurrentVersion,
					book.Revision, book.Rows.Count);
				for (int i = 0; i < book.Rows.Count; i++)
					WriteRow(writer, book.Rows[i], WriteServiceV3);
				writer.Flush();
				return Cap(stream.ToArray(), MaxServiceBookBytes);
			}
		}

		private static KingdomVocationServiceBook DecodeServices(byte[] bytes, int wireVersion)
		{
			using (MemoryStream stream = new MemoryStream(bytes, false))
			using (BinaryReader reader = Reader(stream))
			{
				ReadServiceHeader(reader, wireVersion, out long revision, out int count);
				KingdomVocationServiceBook book = new KingdomVocationServiceBook { Revision = revision };
				for (int i = 0; i < count; i++) book.Rows.Add(ReadRow(reader,
					wireVersion >= KingdomVocationServiceReceipt.CurrentVersion
						? (Get<KingdomVocationServiceReceipt>)ReadServiceV3 : ReadServiceV1V2));
				string failure = null;
				bool valid = wireVersion == KingdomVocationServiceReceipt.LegacyVersion
					? KingdomVocationServiceRules.TryMigrateLegacy(book, out failure) :
					wireVersion == KingdomVocationServiceReceipt.PriorVersion
						? KingdomVocationServiceRules.TryMigratePrior(book, out failure) :
						KingdomVocationServiceRules.TryValidate(book, out failure);
				if (stream.Position != stream.Length || !valid)
					throw new InvalidDataException(failure ?? "trailing service bytes");
				return book;
			}
		}

#if TAF_TESTS
		private static byte[] EncodeServicesLegacyV1(KingdomVocationServiceBook book) =>
			EncodeOlderServices(book, KingdomVocationServiceReceipt.LegacyVersion);

		private static byte[] EncodeServicesPriorV2(KingdomVocationServiceBook book) =>
			EncodeOlderServices(book, KingdomVocationServiceReceipt.PriorVersion);

		private static byte[] EncodeOlderServices(KingdomVocationServiceBook book, int version)
		{
			KingdomVocationServiceBook older;
			string failure;
			bool valid = version == KingdomVocationServiceReceipt.LegacyVersion
				? KingdomVocationServiceRules.TryDowngradeLegacy(book, out older, out failure)
				: KingdomVocationServiceRules.TryDowngradePrior(book, out older, out failure);
			if (!valid) throw new InvalidDataException(failure);
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = Writer(stream))
			{
				WriteServiceHeader(writer, version, older.Revision, older.Rows.Count);
				for (int i = 0; i < older.Rows.Count; i++)
					WriteRow(writer, older.Rows[i], WriteServiceV1V2);
				writer.Flush(); return Cap(stream.ToArray(), MaxServiceBookBytes);
			}
		}
#endif

		private static void WriteServiceHeader(BinaryWriter writer, int version,
			long revision, int count)
		{
			writer.Write(0x50534654); writer.Write(version); writer.Write(revision); writer.Write(count);
		}

		private static void ReadServiceHeader(BinaryReader reader, int expectedVersion,
			out long revision, out int count)
		{
			if (reader.ReadInt32() != 0x50534654 || reader.ReadInt32() != expectedVersion)
				throw new InvalidDataException("invalid vocation service header");
			revision = reader.ReadInt64(); count = reader.ReadInt32();
			int cap = expectedVersion >= KingdomVocationServiceReceipt.CurrentVersion
				? KingdomVocationServiceRules.MaxRows : KingdomVocationServiceRules.PriorMaxRows;
			if (count < 0 || count > cap)
				throw new InvalidDataException("vocation service row cap exceeded");
		}

		private static void WriteServiceV3(BinaryWriter writer, KingdomVocationServiceReceipt row)
		{
			WriteServicePrefix(writer, row);
			WriteString(writer, row.Request.ResultText);
			WriteServiceSuffix(writer, row);
		}

		private static void WriteServiceV1V2(BinaryWriter writer, KingdomVocationServiceReceipt row)
		{
			WriteServicePrefix(writer, row); WriteServiceSuffix(writer, row);
		}

		private static void WriteServicePrefix(BinaryWriter writer, KingdomVocationServiceReceipt row)
		{
			writer.Write(row.Version); WriteString(writer, row.ServiceId);
			KingdomVocationServiceRequest request = row.Request;
			WriteString(writer, request.SettlementId); WriteString(writer, request.Vocation);
			writer.Write((byte)request.Kind); WriteString(writer, request.SourceReceiptId);
			WriteString(writer, request.SourceDescription);
		}

		private static void WriteServiceSuffix(BinaryWriter writer, KingdomVocationServiceReceipt row)
		{
			KingdomVocationServiceRequest request = row.Request;
			WriteString(writer, request.SinkReceiptId); writer.Write(request.InputUnits);
			writer.Write(request.CadenceOrdinal); writer.Write(request.RequestedTick);
			WriteString(writer, request.Digest); WriteString(writer, row.Verb);
			WriteString(writer, row.OutputText); writer.Write(row.OutputUnits); writer.Write(row.CompletedTick);
		}

		private static KingdomVocationServiceReceipt ReadServiceV3(BinaryReader reader)
		{
			KingdomVocationServiceReceipt row = ReadServicePrefix(reader);
			row.Request.ResultText = ReadString(reader); ReadServiceSuffix(reader, row); return row;
		}

		private static KingdomVocationServiceReceipt ReadServiceV1V2(BinaryReader reader)
		{
			KingdomVocationServiceReceipt row = ReadServicePrefix(reader);
			ReadServiceSuffix(reader, row); return row;
		}

		private static KingdomVocationServiceReceipt ReadServicePrefix(BinaryReader reader)
		{
			KingdomVocationServiceReceipt row = new KingdomVocationServiceReceipt
				{ Version = reader.ReadInt32(), ServiceId = ReadString(reader),
				Request = new KingdomVocationServiceRequest() };
			KingdomVocationServiceRequest request = row.Request;
			request.SettlementId = ReadString(reader); request.Vocation = ReadString(reader);
			request.Kind = (KingdomVocationServiceKind)reader.ReadByte();
			request.SourceReceiptId = ReadString(reader); request.SourceDescription = ReadString(reader);
			return row;
		}

		private static void ReadServiceSuffix(BinaryReader reader, KingdomVocationServiceReceipt row)
		{
			KingdomVocationServiceRequest request = row.Request;
			request.SinkReceiptId = ReadString(reader); request.InputUnits = reader.ReadInt32();
			request.CadenceOrdinal = reader.ReadInt64(); request.RequestedTick = reader.ReadInt64();
			request.Digest = ReadString(reader); row.Verb = ReadString(reader);
			row.OutputText = ReadString(reader); row.OutputUnits = reader.ReadInt32();
			row.CompletedTick = reader.ReadInt64();
		}
	}
}
