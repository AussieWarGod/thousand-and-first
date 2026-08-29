using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Independent save authority; never appended to the Experience v4 payload.</summary>
	public static class KingdomCivicArtifactsCodec
	{
		private const int Magic = 0x41434654;
		private const int LegacyWireVersion = 1;
		public const int CurrentWireVersion = 2;
		public const int MaxRealmIdBytes = 77;
		public const int IdentityFramingBytes = 82;
		public const int NestedFramingBytes = 8;
		public const int MaxPayloadBytes = IdentityFramingBytes + NestedFramingBytes +
			KingdomWitnessWorkCodec.MaxBookEncodedBytes +
			KingdomArtifactRecognitionCodec.MaxBookEncodedBytes;
		public const int EnvelopeOverheadBytes = 44;
		public const int MaxEnvelopeBytes = MaxPayloadBytes + EnvelopeOverheadBytes;

		public static byte[] Encode(KingdomCivicArtifactsEnvelope Value)
		{
			if (Value == null || Value.Quarantined)
				throw new InvalidDataException("civic artifacts are absent or quarantined");
			int version; byte[] payload;
			if (Value.IsOpaqueFuture)
			{
				version = Value.OpaqueFutureVersion; payload = Clone(Value.OpaqueFuturePayload);
				if (Value.IdentityBound || Value.RealmId != null ||
					!KingdomCivicArtifactsStore.IsAuthorityEmpty(Value))
					throw new InvalidDataException("future civic artifacts cannot mix current authority");
			}
			else
			{
				string failure;
				if (!KingdomCivicArtifactsStore.TryValidateIdentity(Value, out failure) ||
					!Value.IdentityBound) throw new InvalidDataException(failure ??
						"civic artifacts must be bound before saving");
				version = CurrentWireVersion;
				byte[] witness = KingdomWitnessWorkCodec.Encode(Value.WitnessWorks);
				byte[] recognition = KingdomArtifactRecognitionCodec.Encode(Value.Recognitions);
				using (MemoryStream p = new MemoryStream()) using (BinaryWriter w =
					new BinaryWriter(p, new UTF8Encoding(false, true), true))
				{
					WriteRealm(w, Value.RealmId); w.Write(Value.IdentityBound);
					w.Write(witness.Length); w.Write(witness); w.Write(recognition.Length);
					w.Write(recognition); w.Flush(); payload = p.ToArray();
				}
			}
			if (version < 1 || payload == null || payload.Length > MaxPayloadBytes)
				throw new InvalidDataException("civic artifacts exceed their bounded payload");
			using (MemoryStream m = new MemoryStream()) using (BinaryWriter output =
				new BinaryWriter(m, new UTF8Encoding(false, true), true))
			{
				output.Write(Magic); output.Write(version); output.Write(payload.Length);
				output.Write(payload); output.Write(Hash(version, payload)); output.Flush();
				byte[] bytes = m.ToArray(); if (bytes.Length > MaxEnvelopeBytes)
					throw new InvalidDataException("civic artifacts envelope exceeds its cap");
				return bytes;
			}
		}

		public static KingdomCivicArtifactsEnvelope Decode(byte[] Bytes)
		{
			if (Bytes == null || Bytes.Length > MaxEnvelopeBytes)
				throw new InvalidDataException("civic artifacts envelope exceeds its cap");
			byte[] snapshot = (byte[])Bytes.Clone();
			try
			{
				using (MemoryStream m = new MemoryStream(snapshot, false)) using (BinaryReader r =
					new BinaryReader(m, new UTF8Encoding(false, true), true))
				{
					if (r.ReadInt32() != Magic) throw new InvalidDataException("unknown civic artifacts magic");
					int version = r.ReadInt32(); int size = r.ReadInt32();
					if (version < 1 || size < 0 || size > MaxPayloadBytes)
						throw new InvalidDataException("invalid civic artifacts header");
					byte[] payload = r.ReadBytes(size); byte[] digest = r.ReadBytes(32);
					if (payload.Length != size || digest.Length != 32 || m.Position != m.Length ||
						!Equal(digest, Hash(version, payload)))
						throw new InvalidDataException("civic artifacts integrity check failed");
					if (version > CurrentWireVersion) return new KingdomCivicArtifactsEnvelope
						{ WitnessWorks = new KingdomWitnessWorkBook(), Recognitions =
						new KingdomArtifactRecognitionBook(), OpaqueFutureVersion = version,
						OpaqueFuturePayload = Clone(payload) };
					if (version != LegacyWireVersion && version != CurrentWireVersion)
						throw new InvalidDataException("unsupported civic artifacts version");
					using (MemoryStream p = new MemoryStream(payload, false)) using (BinaryReader pr =
						new BinaryReader(p, new UTF8Encoding(false, true), true))
					{
						string realm = null; bool bound = false;
						if (version == CurrentWireVersion)
						{
							realm = ReadRealm(pr); bound = ReadBoolean(pr);
						}
						byte[] witness = Bounded(pr, KingdomWitnessWorkCodec.MaxBookEncodedBytes);
						byte[] recognition = Bounded(pr,
							KingdomArtifactRecognitionCodec.MaxBookEncodedBytes);
						if (p.Position != p.Length) throw new InvalidDataException(
							"trailing civic artifacts payload bytes");
						KingdomCivicArtifactsEnvelope value = new KingdomCivicArtifactsEnvelope {
							RealmId = realm, IdentityBound = bound, WitnessWorks =
							KingdomWitnessWorkCodec.Decode(witness), Recognitions =
							KingdomArtifactRecognitionCodec.Decode(recognition) };
						string failure;
						if (version == CurrentWireVersion &&
							!KingdomCivicArtifactsStore.TryValidateIdentity(value, out failure))
							throw new InvalidDataException(failure);
						return value;
					}
				}
			}
			catch (EndOfStreamException e) { throw new InvalidDataException("truncated civic artifacts envelope", e); }
			catch (DecoderFallbackException e) { throw new InvalidDataException("civic artifacts are not strict UTF-8", e); }
		}

		private static byte[] Bounded(BinaryReader R, int Max)
		{
			int size = R.ReadInt32(); if (size < 0 || size > Max)
				throw new InvalidDataException("nested civic artifact book exceeds its cap");
			byte[] value = R.ReadBytes(size); if (value.Length != size) throw new EndOfStreamException();
			return value;
		}

		private static void WriteRealm(BinaryWriter Writer, string Value)
		{
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(Value);
			if (bytes.Length > MaxRealmIdBytes) throw new InvalidDataException(
				"civic artifacts realm exceeds its cap");
			Writer.Write(bytes.Length); Writer.Write(bytes);
		}

		private static string ReadRealm(BinaryReader Reader)
		{
			int length = Reader.ReadInt32();
			if (length < 0 || length > MaxRealmIdBytes) throw new InvalidDataException(
				"civic artifacts realm exceeds its cap");
			byte[] bytes = Reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			return new UTF8Encoding(false, true).GetString(bytes);
		}

		private static bool ReadBoolean(BinaryReader Reader)
		{
			byte value = Reader.ReadByte();
			if (value > 1) throw new InvalidDataException("invalid civic artifacts identity flag");
			return value == 1;
		}

		private static byte[] Hash(int Version, byte[] Payload)
		{
			using (MemoryStream m = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(m))
			{
				w.Write(Magic); w.Write(Version); w.Write(Payload.Length); w.Write(Payload); w.Flush();
				using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(m.ToArray());
			}
		}
		private static byte[] Clone(byte[] V) { return V == null ? null : (byte[])V.Clone(); }
		private static bool Equal(byte[] A, byte[] B) { if (A.Length != B.Length) return false; int d = 0; for (int i = 0; i < A.Length; i++) d |= A[i] ^ B[i]; return d == 0; }
	}
}
