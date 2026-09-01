using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Pure bounded rules and canonical wire codec for per-zone observations.</summary>
	public static class KingdomZoneObservationRules
	{
		public const int CurrentVersion = 1;
		public const int MaxIdentityChars = 512;
		public const int MaxPurposeChars = 64;
		public const int MaxRevisionChars = 128;
		public const int MaxPayloadChars = 8192;
		internal const int MaxWireBytes = 16384;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		public static bool TryCreate(string Purpose, string RealmId, string SettlementId,
			string ZoneId, string OwnerId, string SourceRevision, long ObservedTick,
			string Payload, out KingdomZoneObservationReceipt Receipt)
		{
			Receipt = null;
			if (ObservedTick < 0L || !Text(Purpose, MaxPurposeChars)
				|| !Text(RealmId, MaxIdentityChars) || !Text(SettlementId, MaxIdentityChars)
				|| !Text(ZoneId, MaxIdentityChars) || !Text(OwnerId, MaxIdentityChars)
				|| !Text(SourceRevision, MaxRevisionChars) || !Text(Payload, MaxPayloadChars))
				return false;
			Receipt = new KingdomZoneObservationReceipt {
				Version = CurrentVersion, Purpose = Purpose, RealmId = RealmId,
				SettlementId = SettlementId, ZoneId = ZoneId, OwnerId = OwnerId,
				SourceRevision = SourceRevision, ObservedTick = ObservedTick, Payload = Payload };
			Receipt.SourceDigest = ExpectedSourceDigest(Receipt);
			if (Valid(Receipt)) return true;
			Receipt = null; return false;
		}

		public static bool Valid(KingdomZoneObservationReceipt Receipt)
		{
			if (Receipt == null || Receipt.Version != CurrentVersion || Receipt.ObservedTick < 0L
				|| !Text(Receipt.Purpose, MaxPurposeChars)
				|| !Text(Receipt.RealmId, MaxIdentityChars)
				|| !Text(Receipt.SettlementId, MaxIdentityChars)
				|| !Text(Receipt.ZoneId, MaxIdentityChars)
				|| !Text(Receipt.OwnerId, MaxIdentityChars)
				|| !Text(Receipt.SourceRevision, MaxRevisionChars)
				|| !Text(Receipt.Payload, MaxPayloadChars)
				|| !LowerHexDigest(Receipt.SourceDigest)) return false;
			string expected = ExpectedSourceDigest(Receipt);
			return expected != null && string.Equals(expected, Receipt.SourceDigest,
				StringComparison.Ordinal);
		}

		/// <summary>Exact raw-type, purpose, identity, revision, and non-future-tick read.</summary>
		public static bool TryReadExact(object Raw, string Purpose, string RealmId,
			string SettlementId, string ZoneId, string OwnerId, string SourceRevision,
			long CurrentTick, out KingdomZoneObservationReceipt Receipt)
		{
			Receipt = null;
			if (Raw == null || Raw.GetType() != typeof(string) || CurrentTick < 0L
				|| !KingdomZoneObservationCodec.TryDecode((string)Raw, out Receipt)
				|| Receipt.ObservedTick > CurrentTick
				|| !string.Equals(Receipt.Purpose, Purpose, StringComparison.Ordinal)
				|| !string.Equals(Receipt.RealmId, RealmId, StringComparison.Ordinal)
				|| !string.Equals(Receipt.SettlementId, SettlementId, StringComparison.Ordinal)
				|| !string.Equals(Receipt.ZoneId, ZoneId, StringComparison.Ordinal)
				|| !string.Equals(Receipt.OwnerId, OwnerId, StringComparison.Ordinal)
				|| !string.Equals(Receipt.SourceRevision, SourceRevision,
					StringComparison.Ordinal))
			{
				Receipt = null; return false;
			}
			return true;
		}

		internal static bool Text(string Value, int Maximum)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > Maximum
				|| Value.Trim().Length != Value.Length) return false;
			try { return Utf8.GetByteCount(Value) <= Maximum * 4; }
			catch (EncoderFallbackException) { return false; }
		}

		public static bool LowerHexDigest(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9')
					|| (Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}

		private static string ExpectedSourceDigest(KingdomZoneObservationReceipt Receipt)
		{
			if (Receipt == null) return null;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Utf8))
				{
					Write(writer, "taf.zone-observation.source"); writer.Write(Receipt.Version);
					Write(writer, Receipt.Purpose); Write(writer, Receipt.RealmId);
					Write(writer, Receipt.SettlementId); Write(writer, Receipt.ZoneId);
					Write(writer, Receipt.OwnerId); Write(writer, Receipt.SourceRevision);
					writer.Write(Receipt.ObservedTick); Write(writer, Receipt.Payload);
					writer.Flush(); return Digest(stream.ToArray(), stream.Length);
				}
			}
			catch (Exception exception)
			{
				if (!(exception is IOException) && !(exception is EncoderFallbackException)
					&& !(exception is CryptographicException)) throw;
				return null;
			}
		}

		internal static string Digest(byte[] Bytes, long Count)
		{
			if (Bytes == null || Count < 0L || Count > Bytes.Length || Count > int.MaxValue)
				return null;
			byte[] digest;
			using (SHA256 sha = SHA256.Create())
				digest = sha.ComputeHash(Bytes, 0, (int)Count);
			StringBuilder value = new StringBuilder(64);
			for (int i = 0; i < digest.Length; i++) value.Append(digest[i].ToString("x2"));
			return value.ToString();
		}

		internal static void Write(BinaryWriter Writer, string Value)
		{
			byte[] bytes = Utf8.GetBytes(Value ?? ""); Writer.Write(bytes.Length); Writer.Write(bytes);
		}
	}

	public static class KingdomZoneObservationCodec
	{
		private const string Prefix = "TAFZO1:";
		private const int Magic = 0x314f5a54;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		public static bool TryEncode(KingdomZoneObservationReceipt Receipt, out string Wire)
		{
			Wire = null;
			if (!KingdomZoneObservationRules.Valid(Receipt)) return false;
			try
			{
				byte[] payload;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Utf8))
				{
					writer.Write(Magic); writer.Write(Receipt.Version);
					WriteFields(writer, Receipt); writer.Flush(); payload = stream.ToArray();
				}
				if (payload.Length + 32 > KingdomZoneObservationRules.MaxWireBytes) return false;
				byte[] wire = new byte[payload.Length + 32];
				Buffer.BlockCopy(payload, 0, wire, 0, payload.Length);
				using (SHA256 sha = SHA256.Create())
					Buffer.BlockCopy(sha.ComputeHash(payload), 0, wire, payload.Length, 32);
				Wire = Prefix + Convert.ToBase64String(wire); return true;
			}
			catch { Wire = null; return false; }
		}

		public static bool TryDecode(string Wire, out KingdomZoneObservationReceipt Receipt)
		{
			Receipt = null;
			int maximum = Prefix.Length + ((KingdomZoneObservationRules.MaxWireBytes + 2) / 3) * 4;
			if (string.IsNullOrEmpty(Wire) || Wire.Length > maximum
				|| !Wire.StartsWith(Prefix, StringComparison.Ordinal)) return false;
			try
			{
				string encoded = Wire.Substring(Prefix.Length);
				byte[] wire = Convert.FromBase64String(encoded);
				if (wire.Length < 40 || wire.Length > KingdomZoneObservationRules.MaxWireBytes
					|| Convert.ToBase64String(wire) != encoded) return false;
				int payloadLength = wire.Length - 32;
				using (SHA256 sha = SHA256.Create())
				{
					byte[] digest = sha.ComputeHash(wire, 0, payloadLength);
					for (int i = 0; i < 32; i++) if (digest[i] != wire[payloadLength + i]) return false;
				}
				using (MemoryStream stream = new MemoryStream(wire, 0, payloadLength, false))
				using (BinaryReader reader = new BinaryReader(stream, Utf8))
				{
					if (reader.ReadInt32() != Magic) return false;
					KingdomZoneObservationReceipt parsed = new KingdomZoneObservationReceipt {
						Version = reader.ReadInt32(), Purpose = Read(reader, 64),
						RealmId = Read(reader, 512), SettlementId = Read(reader, 512),
						ZoneId = Read(reader, 512), OwnerId = Read(reader, 512),
						SourceRevision = Read(reader, 128), SourceDigest = Read(reader, 64),
						ObservedTick = reader.ReadInt64(), Payload = Read(reader, 8192) };
					if (stream.Position != stream.Length || !KingdomZoneObservationRules.Valid(parsed))
						return false;
					Receipt = parsed; return true;
				}
			}
			catch { Receipt = null; return false; }
		}

		private static void WriteFields(BinaryWriter Writer, KingdomZoneObservationReceipt R)
		{
			KingdomZoneObservationRules.Write(Writer, R.Purpose);
			KingdomZoneObservationRules.Write(Writer, R.RealmId);
			KingdomZoneObservationRules.Write(Writer, R.SettlementId);
			KingdomZoneObservationRules.Write(Writer, R.ZoneId);
			KingdomZoneObservationRules.Write(Writer, R.OwnerId);
			KingdomZoneObservationRules.Write(Writer, R.SourceRevision);
			KingdomZoneObservationRules.Write(Writer, R.SourceDigest);
			Writer.Write(R.ObservedTick); KingdomZoneObservationRules.Write(Writer, R.Payload);
		}

		private static string Read(BinaryReader Reader, int MaximumChars)
		{
			int count = Reader.ReadInt32();
			if (count <= 0 || count > MaximumChars * 4
				|| Reader.BaseStream.Length - Reader.BaseStream.Position < count)
				throw new InvalidDataException();
			string value = Utf8.GetString(Reader.ReadBytes(count));
			if (value.Length > MaximumChars) throw new InvalidDataException(); return value;
		}
	}
}
