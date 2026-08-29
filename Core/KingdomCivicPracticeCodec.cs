using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Bounded, integrity-checked wire v4 for the independent D1/D12 authority.</summary>
	public static partial class KingdomCivicPracticeCodec
	{
		private const int Magic = 0x50534654;
		private const int LegacyWireVersion = 1;
		private const int IdentityWireVersion = 2;
		private const int PriorWireVersion = 3;
		public const int CurrentWireVersion = 4;
		public const int MaxRealmIdBytes = 77;
		public const int IdentityFramingBytes = 82;
		public const int MaxRowBytes = 4096;
		public const int HeaderBytes = 20;
		public const int MaxSiteBookBytes = HeaderBytes +
			KingdomSitePracticeRules.MaxRows * (4 + MaxRowBytes);
		public const int MaxServiceBookBytes = HeaderBytes +
			KingdomVocationServiceRules.MaxRows * (4 + MaxRowBytes);
		public const int NestedFramingBytes = 8;
		public const int EnvelopeOverheadBytes = 44;
		public const int MaxPayloadBytes = IdentityFramingBytes + NestedFramingBytes + MaxSiteBookBytes +
			MaxServiceBookBytes;
		public const int MaxEnvelopeBytes = MaxPayloadBytes + EnvelopeOverheadBytes;

		public static byte[] Encode(KingdomCivicPracticeEnvelope value)
		{
			if (value == null || value.Quarantined)
				throw new InvalidDataException("civic practice authority is absent or quarantined");
			int version;
			byte[] payload;
			if (value.IsOpaqueFuture)
			{
				version = value.OpaqueFutureVersion;
				payload = Clone(value.OpaqueFuturePayload);
				if (value.IdentityBound || value.RealmId != null ||
					!KingdomCivicPracticeStore.IsAuthorityEmpty(value))
					throw new InvalidDataException("future civic practice authority mixes current rows");
			}
			else
			{
				string failure;
				if (!KingdomCivicPracticeStore.TryValidateIdentity(value, out failure) ||
					!value.IdentityBound) throw new InvalidDataException(failure ??
						"civic practice must be bound before saving");
				version = CurrentWireVersion;
				byte[] sites = EncodeSites(value.SitePractices);
				byte[] services = EncodeServices(value.VocationServices);
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = Writer(stream))
				{
					WriteRealm(writer, value.RealmId); writer.Write(value.IdentityBound);
					writer.Write(sites.Length); writer.Write(sites);
					writer.Write(services.Length); writer.Write(services);
					writer.Flush(); payload = stream.ToArray();
				}
			}
			if (version < 1 || payload == null || payload.Length > MaxPayloadBytes)
				throw new InvalidDataException("civic practice payload exceeds its cap");
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = Writer(stream))
			{
				writer.Write(Magic); writer.Write(version); writer.Write(payload.Length);
				writer.Write(payload); writer.Write(Hash(version, payload)); writer.Flush();
				return stream.ToArray();
			}
		}

		public static KingdomCivicPracticeEnvelope Decode(byte[] bytes)
		{
			if (bytes == null || bytes.Length > MaxEnvelopeBytes)
				throw new InvalidDataException("civic practice envelope exceeds its cap");
			byte[] snapshot = (byte[])bytes.Clone();
			try
			{
				using (MemoryStream stream = new MemoryStream(snapshot, false))
				using (BinaryReader reader = Reader(stream))
				{
					if (reader.ReadInt32() != Magic)
						throw new InvalidDataException("unknown civic practice magic");
					int version = reader.ReadInt32();
					int length = reader.ReadInt32();
					if (version < 1 || length < 0 || length > MaxPayloadBytes)
						throw new InvalidDataException("invalid civic practice header");
					byte[] payload = reader.ReadBytes(length);
					byte[] digest = reader.ReadBytes(32);
					if (payload.Length != length || digest.Length != 32 ||
						stream.Position != stream.Length || !Equal(digest, Hash(version, payload)))
						throw new InvalidDataException("civic practice integrity check failed");
					if (version > CurrentWireVersion) return new KingdomCivicPracticeEnvelope
					{
						SitePractices = new KingdomSitePracticeBook(),
						VocationServices = new KingdomVocationServiceBook(),
						OpaqueFutureVersion = version,
						OpaqueFuturePayload = Clone(payload)
					};
					if (version != LegacyWireVersion && version != IdentityWireVersion &&
						version != PriorWireVersion && version != CurrentWireVersion)
						throw new InvalidDataException("unsupported civic practice version");
					return DecodeCurrent(payload, version);
				}
			}
			catch (EndOfStreamException error)
			{
				throw new InvalidDataException("truncated civic practice authority", error);
			}
			catch (DecoderFallbackException error)
			{
				throw new InvalidDataException("civic practice authority is not strict UTF-8", error);
			}
		}

		private static KingdomCivicPracticeEnvelope DecodeCurrent(byte[] payload, int version)
		{
			using (MemoryStream stream = new MemoryStream(payload, false))
			using (BinaryReader reader = Reader(stream))
			{
				bool identity = version >= IdentityWireVersion;
				string realm = null; bool bound = false;
				if (identity) { realm = ReadRealm(reader); bound = ReadBool(reader); }
				byte[] sites = Bounded(reader, MaxSiteBookBytes);
				byte[] services = Bounded(reader, MaxServiceBookBytes);
				if (stream.Position != stream.Length)
					throw new InvalidDataException("trailing civic practice bytes");
				KingdomCivicPracticeEnvelope value = new KingdomCivicPracticeEnvelope
				{
					RealmId = realm, IdentityBound = bound,
					SitePractices = DecodeSites(sites),
						VocationServices = DecodeServices(services,
							version >= CurrentWireVersion ? KingdomVocationServiceReceipt.CurrentVersion :
							version >= PriorWireVersion ? KingdomVocationServiceReceipt.PriorVersion :
							KingdomVocationServiceReceipt.LegacyVersion)
				};
				string failure;
				if (identity && !KingdomCivicPracticeStore.TryValidateIdentity(value, out failure))
					throw new InvalidDataException(failure);
				return value;
			}
		}

		// Shared framing -----------------------------------------------------------------
		private delegate void Put<T>(BinaryWriter writer, T value);
		private delegate T Get<T>(BinaryReader reader);
		private static void WriteRow<T>(BinaryWriter writer, T value, Put<T> put)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter nested = Writer(stream))
			{
				put(nested, value); nested.Flush(); byte[] bytes = stream.ToArray();
				if (bytes.Length > MaxRowBytes)
					throw new InvalidDataException("civic practice row exceeds its cap");
				writer.Write(bytes.Length); writer.Write(bytes);
			}
		}

		private static T ReadRow<T>(BinaryReader reader, Get<T> get)
		{
			int length = reader.ReadInt32();
			if (length < 1 || length > MaxRowBytes)
				throw new InvalidDataException("civic practice row exceeds its cap");
			byte[] bytes = reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			using (MemoryStream stream = new MemoryStream(bytes, false))
			using (BinaryReader nested = Reader(stream))
			{
				T value = get(nested);
				if (stream.Position != stream.Length)
					throw new InvalidDataException("trailing civic practice row bytes");
				return value;
			}
		}

		private static void WriteHeader(BinaryWriter writer, long revision, int count)
		{
			writer.Write(Magic); writer.Write(1); writer.Write(revision); writer.Write(count);
		}

		private static void ReadHeader(BinaryReader reader, out long revision,
			out int count, int max)
		{
			if (reader.ReadInt32() != Magic || reader.ReadInt32() != 1)
				throw new InvalidDataException("invalid nested civic practice header");
			revision = reader.ReadInt64(); count = reader.ReadInt32();
			if (count < 0 || count > max)
				throw new InvalidDataException("civic practice row cap exceeded");
		}

		private static BinaryWriter Writer(Stream stream) { return new BinaryWriter(
			stream, new UTF8Encoding(false, true), true); }
		private static BinaryReader Reader(Stream stream) { return new BinaryReader(
			stream, new UTF8Encoding(false, true), true); }
		private static void WriteString(BinaryWriter writer, string value)
		{
			writer.Write(value != null); if (value != null) writer.Write(value);
		}
		private static string ReadString(BinaryReader reader)
		{
			return reader.ReadBoolean() ? reader.ReadString() : null;
		}
		private static void WriteRealm(BinaryWriter writer, string value)
		{
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(value);
			if (bytes.Length > MaxRealmIdBytes)
				throw new InvalidDataException("civic practice realm exceeds its cap");
			writer.Write(bytes.Length); writer.Write(bytes);
		}
		private static string ReadRealm(BinaryReader reader)
		{
			int length = reader.ReadInt32();
			if (length < 0 || length > MaxRealmIdBytes)
				throw new InvalidDataException("civic practice realm exceeds its cap");
			byte[] bytes = reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			return new UTF8Encoding(false, true).GetString(bytes);
		}
		private static bool ReadBool(BinaryReader reader)
		{
			byte value = reader.ReadByte();
			if (value > 1) throw new InvalidDataException("invalid civic practice identity flag");
			return value == 1;
		}
		private static byte[] Bounded(BinaryReader reader, int max)
		{
			int length = reader.ReadInt32();
			if (length < 0 || length > max)
				throw new InvalidDataException("nested civic practice book exceeds its cap");
			byte[] bytes = reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			return bytes;
		}
		private static byte[] Cap(byte[] bytes, int max)
		{
			if (bytes.Length > max) throw new InvalidDataException("civic practice book exceeds its cap");
			return bytes;
		}
		private static byte[] Clone(byte[] bytes) { return bytes == null ? null : (byte[])bytes.Clone(); }
		private static byte[] Hash(int version, byte[] payload)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(Magic); writer.Write(version); writer.Write(payload.Length);
				writer.Write(payload); writer.Flush();
				using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(stream.ToArray());
			}
		}
		private static bool Equal(byte[] left, byte[] right)
		{
			if (left.Length != right.Length) return false;
			int difference = 0;
			for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
			return difference == 0;
		}
		internal static KingdomSitePracticeBook CloneSites(KingdomSitePracticeBook value) =>
			value == null ? null : DecodeSites(EncodeSites(value));
		internal static KingdomVocationServiceBook CloneServices(KingdomVocationServiceBook value) =>
			value == null ? null : DecodeServices(EncodeServices(value),
				KingdomVocationServiceReceipt.CurrentVersion);
	}
}
