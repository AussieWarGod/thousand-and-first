using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Integrity-checked, bounded wire envelope for D5 history only.</summary>
	public static class KingdomBodyHistoryCodec
	{
		private const int Magic = 0x35424654;
		private const int MaxStringBytes = 1024;
		private const int LegacyWireVersion = 1;
		private const int BookWireVersion = 1;
		public const int CurrentWireVersion = 2;
		public const int MaxRealmIdBytes = 77;
		public const int IdentityFramingBytes = 82;
		public const int MaxRowBytes = 4096;
		public const int HeaderBytes = 20;
		public const int MaxPayloadBytes = IdentityFramingBytes + HeaderBytes
			+ KingdomBodyHistoryRules.MaxRows * (4 + MaxRowBytes);
		public const int EnvelopeOverheadBytes = 44;
		public const int MaxEnvelopeBytes = MaxPayloadBytes + EnvelopeOverheadBytes;

		public static byte[] Encode(KingdomBodyHistoryEnvelope Value)
		{
			if (Value == null || Value.Quarantined)
				throw new InvalidDataException("body history absent or quarantined");
			int version;
			byte[] payload;
			if (Value.IsOpaqueFuture)
			{
				version = Value.OpaqueFutureVersion;
				payload = Value.OpaqueFuturePayload == null
					? null : (byte[])Value.OpaqueFuturePayload.Clone();
				if (Value.IdentityBound || Value.RealmId != null ||
					!KingdomBodyHistoryStore.IsAuthorityEmpty(Value))
					throw new InvalidDataException("future body history mixes current rows");
			}
			else
			{
				string failure;
				if (!KingdomBodyHistoryStore.TryValidateIdentity(Value, out failure) ||
					!Value.IdentityBound) throw new InvalidDataException(failure ??
						"body history must be bound before saving");
				version = CurrentWireVersion;
				byte[] book = EncodeBook(Value.Book);
				using (MemoryStream body = new MemoryStream())
				using (BinaryWriter identity = Writer(body))
				{
					KingdomBodyHistoryStore.WriteIdentity(identity, Value);
					identity.Write(book); identity.Flush(); payload = body.ToArray();
				}
			}
			if (version < 1 || payload == null || payload.Length > MaxPayloadBytes)
				throw new InvalidDataException("body history payload exceeds cap");

			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = Writer(stream))
			{
				writer.Write(Magic);
				writer.Write(version);
				writer.Write(payload.Length);
				writer.Write(payload);
				writer.Write(IntegrityHash(version, payload));
				writer.Flush();
				byte[] result = stream.ToArray();
				if (result.Length > MaxEnvelopeBytes)
					throw new InvalidDataException("body history envelope exceeds cap");
				return result;
			}
		}

		public static KingdomBodyHistoryEnvelope Decode(byte[] Bytes)
		{
			if (Bytes == null || Bytes.Length > MaxEnvelopeBytes)
				throw new InvalidDataException("body history envelope exceeds cap");
			byte[] snapshot = (byte[])Bytes.Clone();
			try
			{
				using (MemoryStream stream = new MemoryStream(snapshot, false))
				using (BinaryReader reader = Reader(stream))
				{
					if (reader.ReadInt32() != Magic)
						throw new InvalidDataException("body history magic");
					int version = reader.ReadInt32();
					int length = reader.ReadInt32();
					if (version < 1 || length < 0 || length > MaxPayloadBytes)
						throw new InvalidDataException("body history header");
					byte[] payload = reader.ReadBytes(length);
					byte[] hash = reader.ReadBytes(32);
					if (payload.Length != length || hash.Length != 32
						|| stream.Position != stream.Length
						|| !ConstantTimeEquals(hash, IntegrityHash(version, payload)))
						throw new InvalidDataException("body history integrity check failed");
					if (version > CurrentWireVersion)
					{
						return new KingdomBodyHistoryEnvelope
						{
							Book = new KingdomBodyHistoryBook(),
							OpaqueFutureVersion = version,
							OpaqueFuturePayload = (byte[])payload.Clone()
						};
					}
					if (version == LegacyWireVersion)
						return new KingdomBodyHistoryEnvelope { Book = DecodeBook(payload) };
					if (version != CurrentWireVersion)
						throw new InvalidDataException("unsupported body history version");
					using (MemoryStream body = new MemoryStream(payload, false))
					using (BinaryReader identity = Reader(body))
					{
						KingdomBodyHistoryEnvelope value = new KingdomBodyHistoryEnvelope();
						KingdomBodyHistoryStore.ReadIdentity(identity, value);
						value.Book = DecodeBook(identity.ReadBytes((int)(body.Length - body.Position)));
						string failure;
						if (!KingdomBodyHistoryStore.TryValidateIdentity(value, out failure))
							throw new InvalidDataException(failure);
						return value;
					}
				}
			}
			catch (EndOfStreamException error)
			{
				throw new InvalidDataException("truncated body history", error);
			}
			catch (DecoderFallbackException error)
			{
				throw new InvalidDataException("body history is not strict UTF-8", error);
			}
		}

		private static byte[] EncodeBook(KingdomBodyHistoryBook Book)
		{
			string failure;
			if (!KingdomBodyHistoryRules.TryValidate(Book, out failure))
				throw new InvalidDataException(failure);
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = Writer(stream))
			{
				writer.Write(Magic);
				writer.Write(BookWireVersion);
				writer.Write(Book.Revision);
				writer.Write(Book.Rows.Count);
				for (int i = 0; i < Book.Rows.Count; i++)
				{
					byte[] row = EncodeRow(Book.Rows[i]);
					if (row.Length > MaxRowBytes)
						throw new InvalidDataException("body history row cap");
					writer.Write(row.Length);
					writer.Write(row);
				}
				writer.Flush();
				return stream.ToArray();
			}
		}

		private static KingdomBodyHistoryBook DecodeBook(byte[] Bytes)
		{
			using (MemoryStream stream = new MemoryStream(Bytes, false))
			using (BinaryReader reader = Reader(stream))
			{
				if (reader.ReadInt32() != Magic
					|| reader.ReadInt32() != BookWireVersion)
					throw new InvalidDataException("nested body history header");
				KingdomBodyHistoryBook book = new KingdomBodyHistoryBook
				{
					Revision = reader.ReadInt64()
				};
				int count = reader.ReadInt32();
				if (count < 0 || count > KingdomBodyHistoryRules.MaxRows)
					throw new InvalidDataException("body history row count");
				for (int i = 0; i < count; i++)
				{
					int length = reader.ReadInt32();
					if (length < 1 || length > MaxRowBytes)
						throw new InvalidDataException("body history row cap");
					byte[] row = reader.ReadBytes(length);
					if (row.Length != length) throw new EndOfStreamException();
					book.Rows.Add(DecodeRow(row));
				}
				if (stream.Position != stream.Length)
					throw new InvalidDataException("trailing body history payload");
				string failure;
				if (!KingdomBodyHistoryRules.TryValidate(book, out failure))
					throw new InvalidDataException(failure);
				return book;
			}
		}

		private static byte[] EncodeRow(KingdomBodyHistoryReceipt Row)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = Writer(stream))
			{
				writer.Write(Row.Version);
				WriteString(writer, Row.ReceiptId);
				WriteString(writer, Row.ResidentIdentity);
				WriteString(writer, Row.BodyObjectId);
				WriteString(writer, Row.ProcedureKey);
				WriteString(writer, Row.ProcedureReceiptId);
				WriteString(writer, Row.BodyPartFact);
				WriteString(writer, Row.Description);
				WriteString(writer, Row.Digest);
				writer.Write(Row.WitnessedTick);
				writer.Flush();
				return stream.ToArray();
			}
		}

		private static KingdomBodyHistoryReceipt DecodeRow(byte[] Bytes)
		{
			using (MemoryStream stream = new MemoryStream(Bytes, false))
			using (BinaryReader reader = Reader(stream))
			{
				KingdomBodyHistoryReceipt row = new KingdomBodyHistoryReceipt
				{
					Version = reader.ReadInt32(),
					ReceiptId = ReadString(reader),
					ResidentIdentity = ReadString(reader),
					BodyObjectId = ReadString(reader),
					ProcedureKey = ReadString(reader),
					ProcedureReceiptId = ReadString(reader),
					BodyPartFact = ReadString(reader),
					Description = ReadString(reader),
					Digest = ReadString(reader),
					WitnessedTick = reader.ReadInt64()
				};
				if (stream.Position != stream.Length)
					throw new InvalidDataException("trailing body history row");
				return row;
			}
		}

		private static void WriteString(BinaryWriter Writer, string Value)
		{
			if (Value == null)
			{
				Writer.Write(-1);
				return;
			}
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(Value);
			if (bytes.Length > MaxStringBytes)
				throw new InvalidDataException("body history string cap");
			Writer.Write(bytes.Length);
			Writer.Write(bytes);
		}

		private static string ReadString(BinaryReader Reader)
		{
			int length = Reader.ReadInt32();
			if (length == -1) return null;
			if (length < 0 || length > MaxStringBytes)
				throw new InvalidDataException("body history string cap");
			byte[] bytes = Reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			return new UTF8Encoding(false, true).GetString(bytes);
		}

		private static byte[] IntegrityHash(int Version, byte[] Payload)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(Magic);
				writer.Write(Version);
				writer.Write(Payload.Length);
				writer.Write(Payload);
				writer.Flush();
				using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(stream.ToArray());
			}
		}

		private static bool ConstantTimeEquals(byte[] Left, byte[] Right)
		{
			if (Left.Length != Right.Length) return false;
			int difference = 0;
			for (int i = 0; i < Left.Length; i++) difference |= Left[i] ^ Right[i];
			return difference == 0;
		}

		private static BinaryWriter Writer(Stream Stream)
		{
			return new BinaryWriter(Stream, new UTF8Encoding(false, true), true);
		}

		private static BinaryReader Reader(Stream Stream)
		{
			return new BinaryReader(Stream, new UTF8Encoding(false, true), true);
		}

		internal static KingdomBodyHistoryBook CloneBook(KingdomBodyHistoryBook Value) =>
			Value == null ? null : DecodeBook(EncodeBook(Value));
	}
}
