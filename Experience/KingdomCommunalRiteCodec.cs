using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomCommunalRiteCodec
	{
		private const int Magic = 0x31524354;
		private const string HashDomain = "TAF-COMMUNAL-RITE-ENVELOPE-V1";
		public const int CurrentWireVersion = 1;
		public const int PayloadHeaderBytes = 102;
		public const int RowBytes = 356;
		public const int MaxPayloadBytes = PayloadHeaderBytes
			+ KingdomCommunalRiteRules.MaxRows * RowBytes;
		public const int EnvelopeOverheadBytes = 44;
		public const int MaxEnvelopeBytes = MaxPayloadBytes + EnvelopeOverheadBytes;

		public static byte[] EncodeEnvelope(KingdomCommunalRiteBook book)
		{
			if (book == null) throw new ArgumentNullException(nameof(book));
			if (book.SchemaState == KingdomExperienceSchemaState.Quarantined)
				return ExactOpaque(book, false);
			if (book.SchemaState == KingdomExperienceSchemaState.Unknown)
				return ExactOpaque(book, true);
			if (!KingdomCommunalRiteRules.TryValidate(book, out string failure))
				throw new InvalidDataException(failure);
			return Frame(CurrentWireVersion, EncodePayload(book));
		}

		public static KingdomCommunalRiteBook DecodeEnvelope(byte[] envelope)
		{
			if (envelope == null || envelope.Length == 0) return new KingdomCommunalRiteBook();
			if (envelope.Length > MaxEnvelopeBytes)
				throw new InvalidDataException("communal-rite envelope exceeds hard bound");
			byte[] exact = (byte[])envelope.Clone();
			try
			{
				using (MemoryStream stream = new MemoryStream(exact, false))
				using (BinaryReader reader = Reader(stream))
				{
					if (reader.ReadInt32() != Magic)
						return Quarantine(exact, "communal-rite magic is invalid");
					int wire = reader.ReadInt32();
					int length = reader.ReadInt32();
					if (wire < 1 || length < 0 || length > MaxPayloadBytes)
						return Quarantine(exact, "communal-rite frame is invalid");
					byte[] payload = reader.ReadBytes(length);
					byte[] digest = reader.ReadBytes(32);
					if (payload.Length != length || digest.Length != 32
						|| stream.Position != stream.Length
						|| !FixedEquals(digest, Hash(wire, payload)))
						return Quarantine(exact, "communal-rite integrity check failed");
					if (wire > CurrentWireVersion)
						return new KingdomCommunalRiteBook
						{
							SchemaState = KingdomExperienceSchemaState.Unknown,
							SchemaFault = "future communal-rite wire " + wire,
							OpaqueWireVersion = wire,
							OpaqueFuturePayload = (byte[])payload.Clone(),
							OpaqueEnvelope = exact
						};
					try { return DecodePayload(payload); }
					catch (Exception e) when (Malformed(e))
					{
						return Quarantine(exact, "communal-rite payload: " + e.Message);
					}
				}
			}
			catch (Exception e) when (Malformed(e))
			{
				return Quarantine(exact, "communal-rite frame: " + e.Message);
			}
		}

		public static string DigestHex(byte[] envelope)
		{
			byte[] bytes = envelope ?? new byte[0];
			using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(bytes));
		}

		public static bool TryPrepareCas(byte[] currentEnvelope, string expectedDigest,
			KingdomCommunalRiteBook nextBook, out byte[] nextEnvelope, out string nextDigest,
			out string failure)
		{
			nextEnvelope = null; nextDigest = null; failure = null;
			if (!string.Equals(DigestHex(currentEnvelope), expectedDigest,
				StringComparison.Ordinal)) return Fail("communal-rite byte CAS conflict", out failure);
			KingdomCommunalRiteBook current;
			try { current = DecodeEnvelope(currentEnvelope); }
			catch (Exception e) when (Malformed(e)) { return Fail(e.Message, out failure); }
			if (!KingdomCommunalRiteRules.TryValidate(current, out failure)
				|| !KingdomCommunalRiteRules.TryValidate(nextBook, out failure)
				|| current.Revision == long.MaxValue
				|| nextBook.Revision != current.Revision + 1L
				|| current.IdentityBound && (!nextBook.IdentityBound
					|| nextBook.RealmId != current.RealmId))
				return Fail(failure ?? "communal-rite staged CAS is invalid", out failure);
			try { nextEnvelope = EncodeEnvelope(nextBook); }
			catch (Exception e) when (Malformed(e)) { return Fail(e.Message, out failure); }
			nextDigest = DigestHex(nextEnvelope); return true;
		}

		private static byte[] ExactOpaque(KingdomCommunalRiteBook book, bool future)
		{
			if (book.OpaqueEnvelope == null || book.OpaqueEnvelope.Length == 0
				|| book.OpaqueEnvelope.Length > MaxEnvelopeBytes
				|| future && (book.OpaqueWireVersion <= CurrentWireVersion
					|| book.OpaqueFuturePayload == null))
				throw new InvalidDataException("communal-rite opaque envelope is invalid");
			KingdomCommunalRiteBook decoded = DecodeEnvelope(book.OpaqueEnvelope);
			if (future && (decoded.SchemaState != KingdomExperienceSchemaState.Unknown
				|| decoded.OpaqueWireVersion != book.OpaqueWireVersion
				|| !FixedEquals(decoded.OpaqueFuturePayload, book.OpaqueFuturePayload))
				|| !future && decoded.SchemaState != KingdomExperienceSchemaState.Quarantined)
				throw new InvalidDataException("communal-rite opaque disposition is not supported by its bytes");
			return (byte[])book.OpaqueEnvelope.Clone();
		}

		private static byte[] Frame(int wire, byte[] payload)
		{
			if (wire < 1 || payload == null || payload.Length > MaxPayloadBytes)
				throw new InvalidDataException("communal-rite payload exceeds hard bound");
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = Writer(stream))
			{
				writer.Write(Magic); writer.Write(wire); writer.Write(payload.Length);
				writer.Write(payload); writer.Write(Hash(wire, payload)); writer.Flush();
				return stream.ToArray();
			}
		}

		private static byte[] Hash(int wire, byte[] payload)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = Writer(stream))
			{
				WriteString(writer, HashDomain, 64); writer.Write(Magic); writer.Write(wire);
				writer.Write(payload.Length); writer.Write(payload); writer.Flush();
				using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(stream.ToArray());
			}
		}

		private static bool FixedEquals(byte[] left, byte[] right)
		{
			if (left == null || right == null || left.Length != right.Length) return false;
			int difference = 0;
			for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
			return difference == 0;
		}

		private static string Hex(byte[] bytes)
		{
			StringBuilder text = new StringBuilder(bytes.Length * 2);
			for (int i = 0; i < bytes.Length; i++) text.Append(bytes[i].ToString("x2"));
			return text.ToString();
		}

		private static KingdomCommunalRiteBook Quarantine(byte[] exact, string fault)
		{
			return new KingdomCommunalRiteBook
			{
				SchemaState = KingdomExperienceSchemaState.Quarantined,
				SchemaFault = fault, OpaqueEnvelope = exact
			};
		}

		private static bool Malformed(Exception e)
		{
			return e is InvalidDataException || e is EndOfStreamException
				|| e is DecoderFallbackException || e is EncoderFallbackException
				|| e is ArgumentException || e is OverflowException;
		}

		private static bool Fail(string message, out string failure)
		{
			failure = message; return false;
		}
	}
}
