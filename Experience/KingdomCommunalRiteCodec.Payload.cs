using System;
using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomCommunalRiteCodec
	{
		private static byte[] EncodePayload(KingdomCommunalRiteBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = Writer(stream))
			{
				writer.Write(Magic); writer.Write(CurrentWireVersion);
				WriteString(writer, book.RealmId, KingdomCommunalRiteRules.MaxRealmIdBytes);
				writer.Write(book.IdentityBound); writer.Write(book.Revision);
				writer.Write(book.Rows.Count);
				for (int i = 0; i < book.Rows.Count; i++) WriteRow(writer, book.Rows[i]);
				writer.Flush();
				byte[] payload = stream.ToArray();
				if (payload.Length > MaxPayloadBytes)
					throw new InvalidDataException("communal-rite payload exceeds hard bound");
				return payload;
			}
		}

		private static KingdomCommunalRiteBook DecodePayload(byte[] payload)
		{
			using (MemoryStream stream = new MemoryStream(payload, false))
			using (BinaryReader reader = Reader(stream))
			{
				if (reader.ReadInt32() != Magic || reader.ReadInt32() != CurrentWireVersion)
					throw new InvalidDataException("nested communal-rite header is invalid");
				KingdomCommunalRiteBook book = new KingdomCommunalRiteBook
				{
					RealmId = ReadString(reader,
						KingdomCommunalRiteRules.MaxRealmIdBytes),
					IdentityBound = ReadBool(reader), Revision = reader.ReadInt64()
				};
				int count = reader.ReadInt32();
				if (count < 0 || count > KingdomCommunalRiteRules.MaxRows)
					throw new InvalidDataException("communal-rite row count exceeds cap");
				for (int i = 0; i < count; i++) book.Rows.Add(ReadRow(reader));
				if (stream.Position != stream.Length)
					throw new InvalidDataException("communal-rite payload has trailing bytes");
				if (!KingdomCommunalRiteRules.TryValidate(book, out string failure))
					throw new InvalidDataException(failure);
				return book;
			}
		}

		private static void WriteRow(BinaryWriter writer, KingdomCommunalRiteReceipt row)
		{
			writer.Write(row.Version); writer.Write((byte)row.Phase);
			WriteString(writer, row.SettlementId,
				KingdomCommunalRiteRules.MaxSettlementIdBytes);
			WriteString(writer, row.PracticeId,
				KingdomCommunalRiteRules.MaxPracticeIdBytes);
			WriteString(writer, row.EventId, KingdomCommunalRiteRules.MaxEventIdBytes);
			writer.Write(row.EventTick); writer.Write(row.EnableEpoch);
			writer.Write(row.ProjectionTick);
		}

		private static KingdomCommunalRiteReceipt ReadRow(BinaryReader reader)
		{
			return new KingdomCommunalRiteReceipt
			{
				Version = reader.ReadInt32(),
				Phase = (KingdomCommunalRitePhase)reader.ReadByte(),
				SettlementId = ReadString(reader,
					KingdomCommunalRiteRules.MaxSettlementIdBytes),
				PracticeId = ReadString(reader,
					KingdomCommunalRiteRules.MaxPracticeIdBytes),
				EventId = ReadString(reader, KingdomCommunalRiteRules.MaxEventIdBytes),
				EventTick = reader.ReadInt64(), EnableEpoch = reader.ReadInt64(),
				ProjectionTick = reader.ReadInt64()
			};
		}
	}
}
