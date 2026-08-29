using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomRealmRetirementCodec
	{
		private const int Magic = 1413565778;
		private const int End = 1413565779;
		private const int MaxEnvelopeBytes = 1024 * 1024;
		internal const int MaxPayloadBytes = MaxEnvelopeBytes - 64;
		private const string Prefix = "taf-retirement-v1:";

		public static string Encode(KingdomRealmRetirementState State)
		{
			if (!KingdomRealmRetirementRules.Valid(State, out string failure))
				throw new InvalidDataException(failure);
			return Prefix + Convert.ToBase64String(Envelope(1, WriteState(State)));
		}

		public static bool TryDecode(string Encoded, out KingdomRealmRetirementState State,
			out string Failure)
		{
			State = null;
			if (!TryEnvelope(Encoded, 1, out byte[] payload, out Failure)) return false;
			try
			{
				State = ReadState(payload);
				if (!KingdomRealmRetirementRules.Valid(State, out Failure))
				{
					State = null;
					return false;
				}
				return true;
			}
			catch (Exception error)
			{
				if (!WireException(error)) throw;
				Failure = "retirement payload is malformed";
				State = null;
				return false;
			}
		}

		public static string EncodeFence(KingdomIdentityFence Fence)
		{
			if (!KingdomIdentityFenceRules.Valid(Fence, out string failure))
				throw new InvalidDataException(failure);
			return Prefix + Convert.ToBase64String(Envelope(2, WriteFence(Fence)));
		}

		public static bool TryDecodeFence(string Encoded, out KingdomIdentityFence Fence,
			out string Failure)
		{
			Fence = null;
			if (!TryEnvelope(Encoded, 2, out byte[] payload, out Failure)) return false;
			try
			{
				Fence = ReadFence(payload);
				if (!KingdomIdentityFenceRules.Valid(Fence, out Failure))
				{
					Fence = null;
					return false;
				}
				return true;
			}
			catch (Exception error)
			{
				if (!WireException(error)) throw;
				Failure = "identity fence payload is malformed";
				Fence = null;
				return false;
			}
		}

		private static byte[] Envelope(byte Kind, byte[] Payload)
		{
			if (Payload == null || Payload.Length > MaxPayloadBytes)
				throw new InvalidDataException("retirement payload exceeds its byte cap");
			byte[] digest;
			using (SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(Payload);
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(Magic);
				writer.Write(Kind);
				writer.Write(Payload.Length);
				writer.Write(Payload);
				writer.Write(digest);
				writer.Write(End);
				writer.Flush();
				return stream.ToArray();
			}
		}

		internal static bool FitsPayload(KingdomRealmRetirementState State)
		{
			try { return State != null && WriteState(State).Length <= MaxPayloadBytes; }
			catch (Exception error)
			{
				if (!WireException(error)) throw;
				return false;
			}
		}

		private static bool TryEnvelope(string Encoded, byte ExpectedKind,
			out byte[] Payload, out string Failure)
		{
			Payload = null;
			Failure = null;
			if (string.IsNullOrEmpty(Encoded) || !Encoded.StartsWith(Prefix,
				StringComparison.Ordinal) || Encoded.Length > MaxEnvelopeBytes * 2)
				return Fail("retirement envelope is absent or outside its bounds", out Failure);
			try
			{
				byte[] envelope = Convert.FromBase64String(Encoded.Substring(Prefix.Length));
				if (envelope.Length > MaxEnvelopeBytes || envelope.Length < 45)
					return Fail("retirement envelope has an invalid length", out Failure);
				using (MemoryStream stream = new MemoryStream(envelope, false))
				using (BinaryReader reader = new BinaryReader(stream))
				{
					if (reader.ReadInt32() != Magic || reader.ReadByte() != ExpectedKind)
						return Fail("retirement envelope kind or marker differs", out Failure);
					int count = reader.ReadInt32();
					if (count < 0 || count > MaxEnvelopeBytes - 64
						|| count > stream.Length - stream.Position - 36)
						return Fail("retirement payload length is invalid", out Failure);
					Payload = reader.ReadBytes(count);
					byte[] expected = reader.ReadBytes(32);
					if (Payload.Length != count || expected.Length != 32
						|| reader.ReadInt32() != End || stream.Position != stream.Length)
						return Fail("retirement envelope is truncated or has trailing bytes", out Failure);
					byte[] actual;
					using (SHA256 sha = SHA256.Create()) actual = sha.ComputeHash(Payload);
					if (!ConstantTime(expected, actual))
						return Fail("retirement envelope checksum differs", out Failure);
				}
				return true;
			}
			catch (Exception error)
			{
				if (!WireException(error)) throw;
				Payload = null;
				return Fail("retirement envelope is malformed", out Failure);
			}
		}

		private static bool ConstantTime(byte[] Left, byte[] Right)
		{
			if (Left == null || Right == null || Left.Length != Right.Length) return false;
			int diff = 0;
			for (int i = 0; i < Left.Length; i++) diff |= Left[i] ^ Right[i];
			return diff == 0;
		}

		private static bool WireException(Exception Error)
		{
			return Error is FormatException || Error is IOException
				|| Error is EndOfStreamException || Error is InvalidDataException
				|| Error is ArgumentException || Error is DecoderFallbackException;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
