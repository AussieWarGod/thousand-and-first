using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.Kernel
{
	/// <summary>
	/// Writes the fixed kernel protocols as bytes, deterministically.
	/// <para>
	/// Everything multibyte is big-endian and fixed-width; signed values use their two's-complement
	/// bit pattern; bool is exactly <c>0x00</c> or <c>0x01</c>; required strings are strict UTF-8
	/// with no BOM behind an unsigned 32-bit byte length. No culture, normalization, terminator,
	/// CLR type or enum name, reflection order, platform endianness, or runtime hash code ever
	/// reaches the bytes.
	/// </para>
	/// <para>
	/// This is <b>not</b> the private v3 save codec and must never grow into one: no reflection, no
	/// tagged realm sections, no engine writers, no general object API. It exists for fixed kernel
	/// protocols and deterministic diagnostic comparison, and nothing else.
	/// </para>
	/// </summary>
	internal sealed class CanonicalByteWriter : IDisposable
	{
		// Throw-on-invalid, and never emit a BOM: a preamble would silently prefix every payload.
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		private MemoryStream _stream;

		internal CanonicalByteWriter()
		{
			_stream = new MemoryStream();
		}

		internal void WriteByte(byte value)
		{
			Require();
			_stream.WriteByte(value);
		}

		internal void WriteBool(bool value)
		{
			WriteByte(value ? (byte)1 : (byte)0);
		}

		internal void WriteInt32(int value)
		{
			WriteUInt32(unchecked((uint)value));
		}

		internal void WriteUInt32(uint value)
		{
			Require();
			_stream.WriteByte((byte)(value >> 24));
			_stream.WriteByte((byte)(value >> 16));
			_stream.WriteByte((byte)(value >> 8));
			_stream.WriteByte((byte)value);
		}

		internal void WriteInt64(long value)
		{
			WriteUInt64(unchecked((ulong)value));
		}

		internal void WriteUInt64(ulong value)
		{
			Require();
			_stream.WriteByte((byte)(value >> 56));
			_stream.WriteByte((byte)(value >> 48));
			_stream.WriteByte((byte)(value >> 40));
			_stream.WriteByte((byte)(value >> 32));
			_stream.WriteByte((byte)(value >> 24));
			_stream.WriteByte((byte)(value >> 16));
			_stream.WriteByte((byte)(value >> 8));
			_stream.WriteByte((byte)value);
		}

		/// <summary>
		/// Length-prefixed strict UTF-8. Empty is legal at this layer; the higher
		/// <c>Try*</c> encoders validate identifiers before calling in.
		/// </summary>
		internal void WriteRequiredUtf8(string value)
		{
			Require();
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			// Strict encoding throws EncoderFallbackException on malformed UTF-16 (a lone
			// surrogate, say) rather than substituting U+FFFD, because a replacement character
			// would make two different inputs encode identically.
			byte[] payload = StrictUtf8.GetBytes(value);
			WriteUInt32((uint)payload.Length);
			_stream.Write(payload, 0, payload.Length);
		}

		/// <summary>
		/// Exact bytes with no length prefix. Used only for the fixed eight-byte protocol tags,
		/// whose length is part of the format rather than data.
		/// </summary>
		internal void WriteRawBytes(byte[] value)
		{
			Require();
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			_stream.Write(value, 0, value.Length);
		}

		internal byte[] ToArray()
		{
			Require();
			return _stream.ToArray();
		}

		public void Dispose()
		{
			if (_stream != null)
			{
				_stream.Dispose();
				_stream = null;
			}
		}

		private void Require()
		{
			if (_stream == null)
			{
				throw new ObjectDisposedException("CanonicalByteWriter");
			}
		}
	}

	/// <summary>
	/// The two fixed identity preimages. Field order here is the wire format and is frozen:
	/// changing it changes every event ID and every random draw ever derived.
	/// </summary>
	internal static class KernelCanonicalEncoding
	{
		private static readonly byte[] EventTag = { 0x54, 0x41, 0x46, 0x5F, 0x45, 0x56, 0x54, 0x31 };   // "TAF_EVT1"

		private static readonly byte[] RandomTag = { 0x54, 0x41, 0x46, 0x5F, 0x52, 0x4E, 0x47, 0x31 };  // "TAF_RNG1"

		internal static bool TryEncodeEventIdentityPreimage(
			KernelSeed128 seed,
			SemanticEventKey key,
			out byte[] bytes,
			out KernelFaultCode fault)
		{
			bytes = null;
			if (!IsUsableKey(key))
			{
				fault = KernelFaultCode.InvalidEventKey;
				return false;
			}
			using (CanonicalByteWriter writer = new CanonicalByteWriter())
			{
				writer.WriteRawBytes(EventTag);
				WriteCommon(writer, seed, key);
				bytes = writer.ToArray();
			}
			fault = KernelFaultCode.None;
			return true;
		}

		internal static bool TryEncodeRandomBlockPreimage(
			KernelSeed128 seed,
			SemanticEventKey key,
			uint drawIndex,
			uint blockIndex,
			out byte[] bytes,
			out KernelFaultCode fault)
		{
			bytes = null;
			if (!IsUsableKey(key))
			{
				fault = KernelFaultCode.InvalidEventKey;
				return false;
			}
			using (CanonicalByteWriter writer = new CanonicalByteWriter())
			{
				writer.WriteRawBytes(RandomTag);
				WriteCommon(writer, seed, key);
				writer.WriteUInt32(drawIndex);
				writer.WriteUInt32(blockIndex);
				bytes = writer.ToArray();
			}
			fault = KernelFaultCode.None;
			return true;
		}

		/// <summary>
		/// The shared body of both preimages. The random preimage is byte-identical to the event
		/// preimage through this point and differs only in its leading tag, which is what keeps
		/// the two domains separated rather than merely different.
		/// </summary>
		private static void WriteCommon(CanonicalByteWriter writer, KernelSeed128 seed, SemanticEventKey key)
		{
			writer.WriteUInt64(seed.High);
			writer.WriteUInt64(seed.Low);
			writer.WriteInt32(key.RulesVersionAtCreation);
			writer.WriteRequiredUtf8(key.SettlementId);
			writer.WriteRequiredUtf8(key.EventStreamId);
			writer.WriteUInt32(key.EventKindCode);
			writer.WriteUInt64(key.EventOrdinal);
		}

		/// <summary>
		/// A default-constructed key carries null IDs and a zero kind, which
		/// <see cref="SemanticEventKey.TryCreate"/> would have refused. Encoders are reachable
		/// with one, so they re-check rather than trusting that every caller went through
		/// the factory.
		/// </summary>
		private static bool IsUsableKey(SemanticEventKey key)
		{
			return key.RulesVersionAtCreation >= 1
				&& key.EventKindCode != 0u
				&& KernelSemanticId.IsValid(key.SettlementId)
				&& KernelSemanticId.IsValid(key.EventStreamId);
		}
	}
}
