using System;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst.Simulation.Kernel
{
	/// <summary>
	/// Stable event identity: the same key always names the same event, in any process, forever.
	/// </summary>
	internal static class SemanticEventIdentity
	{
		private const string IdPrefix = "taf:event:v1:";

		internal static bool TryCreateId(
			KernelSeed128 seed,
			SemanticEventKey key,
			out string eventId,
			out KernelFaultCode fault)
		{
			eventId = null;
			byte[] preimage;
			if (!KernelCanonicalEncoding.TryEncodeEventIdentityPreimage(seed, key, out preimage, out fault))
			{
				return false;
			}
			byte[] digest;
			if (!KernelDigest.TryComputeSha256(preimage, out digest, out fault))
			{
				return false;
			}
			eventId = IdPrefix + KernelDigest.ToLowercaseHex(digest);
			fault = KernelFaultCode.None;
			return true;
		}
	}

	/// <summary>
	/// Counter-based randomness. There is no stream object and no mutable cursor: a draw is a
	/// pure function of its coordinates.
	/// <para>
	/// The caller assigns each purpose a stable semantic <c>drawIndex</c>. Retrying, reporting,
	/// logging, batching, and unrelated calls therefore advance nothing, which is what makes a
	/// reload reproduce a settlement exactly rather than rerolling it.
	/// </para>
	/// </summary>
	internal static class CounterRandom
	{
		internal static bool TryDrawUInt64(
			KernelSeed128 seed,
			SemanticEventKey key,
			uint drawIndex,
			out ulong value,
			out KernelFaultCode fault)
		{
			return TryDrawBlock(seed, key, drawIndex, 0u, out value, out fault);
		}

		/// <summary>
		/// Uniform over <c>[0, exclusiveUpperBound)</c> by rejection, never by <c>% bound</c>
		/// alone.
		/// <para>
		/// Rejecting samples below <c>2^64 mod bound</c> leaves exactly <c>q * bound</c>
		/// candidates, so every residue has an equal number of preimages. Taking the modulus
		/// without the rejection would over-represent the low residues.
		/// </para>
		/// <para>
		/// Rejection advances only the block index. The semantic draw index, the event ordinal,
		/// and every other draw are untouched, so a rejection cannot perturb an unrelated result.
		/// </para>
		/// </summary>
		internal static bool TryDrawBelow(
			KernelSeed128 seed,
			SemanticEventKey key,
			uint drawIndex,
			ulong exclusiveUpperBound,
			out ulong value,
			out KernelFaultCode fault)
		{
			value = 0uL;

			// Frozen precedence: invalid key, then zero bound, then any provider failure.
			//
			// Both earlier checks must therefore complete before anything hashes. Validating the
			// key by attempting the first draw would satisfy the first rule and break the second,
			// because drawing hashes: a caller passing a zero bound on a machine whose provider is
			// refusing would be told the provider failed, and would go looking at the platform
			// instead of at the bound it can see is wrong.
			//
			// The key is settled first and without hashing by re-running it through the same
			// factory that admits one. That is the identical set of conditions rather than a
			// second copy of them, and it adds no surface: the encoder's own predicate is private
			// and exposing it would put two definitions of a usable key in reach of callers.
			SemanticEventKey validated;
			if (!SemanticEventKey.TryCreate(
				key.RulesVersionAtCreation,
				key.SettlementId,
				key.EventStreamId,
				key.EventKindCode,
				key.EventOrdinal,
				out validated,
				out fault))
			{
				return false;
			}

			if (exclusiveUpperBound == 0uL)
			{
				fault = KernelFaultCode.InvalidRandomBound;
				return false;
			}

			uint blockIndex = 0u;
			while (true)
			{
				ulong sample;
				if (!TryDrawBlock(seed, key, drawIndex, blockIndex, out sample, out fault))
				{
					value = 0uL;
					return false;
				}
				ulong accepted;
				if (TryAcceptBoundedSample(sample, exclusiveUpperBound, out accepted))
				{
					value = accepted;
					fault = KernelFaultCode.None;
					return true;
				}
				uint nextBlock;
				if (!TryNextRejectionBlockIndex(blockIndex, out nextBlock, out fault))
				{
					// Exhausted every block rather than wrapping to zero: wrapping would reuse
					// already-rejected samples forever and silently spin.
					value = 0uL;
					return false;
				}
				blockIndex = nextBlock;
			}
		}

		/// <summary>
		/// Maps one raw sample onto a bound, or reports that it must be rejected.
		/// <para>
		/// A zero bound returns false with value zero, which is indistinguishable from a
		/// rejection — <see cref="TryDrawBelow"/> validates the bound before consulting this
		/// result, so that ambiguity never reaches production control flow.
		/// </para>
		/// </summary>
		internal static bool TryAcceptBoundedSample(ulong sample, ulong exclusiveUpperBound, out ulong value)
		{
			value = 0uL;
			if (exclusiveUpperBound == 0uL)
			{
				return false;
			}
			// (0 - bound) in unchecked unsigned arithmetic is 2^64 - bound, so this is 2^64 mod
			// bound without needing an integer wider than the machine has.
			ulong threshold = unchecked(0uL - exclusiveUpperBound) % exclusiveUpperBound;
			if (sample < threshold)
			{
				return false;
			}
			value = sample % exclusiveUpperBound;
			return true;
		}

		/// <summary>
		/// Advances the rejection block, failing closed at the maximum instead of wrapping.
		/// </summary>
		internal static bool TryNextRejectionBlockIndex(uint currentBlockIndex, out uint nextBlockIndex, out KernelFaultCode fault)
		{
			if (currentBlockIndex == uint.MaxValue)
			{
				nextBlockIndex = 0u;
				fault = KernelFaultCode.CounterExhausted;
				return false;
			}
			nextBlockIndex = currentBlockIndex + 1u;
			fault = KernelFaultCode.None;
			return true;
		}

		private static bool TryDrawBlock(
			KernelSeed128 seed,
			SemanticEventKey key,
			uint drawIndex,
			uint blockIndex,
			out ulong value,
			out KernelFaultCode fault)
		{
			value = 0uL;
			byte[] preimage;
			if (!KernelCanonicalEncoding.TryEncodeRandomBlockPreimage(seed, key, drawIndex, blockIndex, out preimage, out fault))
			{
				return false;
			}
			byte[] digest;
			if (!KernelDigest.TryComputeSha256(preimage, out digest, out fault))
			{
				return false;
			}
			// Digest bytes 0..7, big-endian.
			ulong result = 0uL;
			for (int i = 0; i < 8; i++)
			{
				result = (result << 8) | digest[i];
			}
			value = result;
			fault = KernelFaultCode.None;
			return true;
		}
	}

	/// <summary>
	/// SHA-256 and hex, kept in one place so no caller can quietly substitute another algorithm
	/// or a culture-shaped formatter.
	/// <para>
	/// The card does not name this type, so its internal visibility is a deliberate exception
	/// rather than an oversight. The card's own goldens require it: the random-block digests, the
	/// identity preimages, and the 183-byte fixture are all specified as hex strings, and no
	/// production entry point returns either a raw digest or a rendered one — <c>TryDrawBlock</c>
	/// is private and yields a <c>ulong</c>. A test cannot assert those goldens without computing
	/// a digest and rendering it. The alternative is a second SHA-256 and a second hex formatter
	/// living in the test assembly, which would make the goldens agree with the tests rather than
	/// with the protocol.
	/// </para>
	/// </summary>
	internal static class KernelDigest
	{
		private const string HexDigits = "0123456789abcdef";

#if TAF_TESTS
		/// <summary>
		/// What an injected provider failure should throw. Test-only in the strictest sense: the
		/// game build compiles every <c>.cs</c> outside <c>DevTests</c> without defining
		/// <c>TAF_TESTS</c>, so neither this field nor the switch below exists in the shipped
		/// assembly — there is no flag to leave set and no branch to take in play.
		/// <para>
		/// It earns its place because the frozen fault precedence puts provider failure last, and
		/// without a way to make the provider fail on demand the two orderings that end there are
		/// unfalsifiable. A test that cannot fail is not evidence.
		/// </para>
		/// </summary>
		internal enum InjectedDigestFailure : byte
		{
			None = 0,
			Cryptographic = 1,
			PlatformUnsupported = 2,
			ProcessFatal = 3
		}

		internal static InjectedDigestFailure InjectedFailure = InjectedDigestFailure.None;
#endif

		internal static bool TryComputeSha256(byte[] input, out byte[] digest, out KernelFaultCode fault)
		{
			digest = null;
			if (input == null)
			{
				fault = KernelFaultCode.InvalidEventKey;
				return false;
			}
			try
			{
#if TAF_TESTS
				// Inside the try on purpose: the first two must be caught and mapped, and the third
				// must travel straight through, which is the distinction under test.
				switch (InjectedFailure)
				{
				case InjectedDigestFailure.Cryptographic:
					throw new CryptographicException("injected provider failure");
				case InjectedDigestFailure.PlatformUnsupported:
					throw new PlatformNotSupportedException("injected missing provider");
				case InjectedDigestFailure.ProcessFatal:
					throw new OutOfMemoryException("injected process-fatal failure");
				}
#endif
				using (SHA256 provider = SHA256.Create())
				{
					if (provider == null)
					{
						fault = KernelFaultCode.CryptographicFailure;
						return false;
					}
					digest = provider.ComputeHash(input);
				}
			}
			catch (CryptographicException)
			{
				// A refusing provider is reported, never worked around: substituting a different
				// algorithm would silently change every identity in the save.
				digest = null;
				fault = KernelFaultCode.CryptographicFailure;
				return false;
			}
			catch (PlatformNotSupportedException)
			{
				// Same disposition, different cause: the platform has no provider to refuse with.
				digest = null;
				fault = KernelFaultCode.CryptographicFailure;
				return false;
			}
			// Deliberately no general catch. An OutOfMemoryException, a thread abort, or any other
			// process-fatal condition is not a cryptographic failure, and reporting it as one would
			// convert a dying process into a plausible-looking fault code that the caller handles,
			// carries on from, and then writes to a save. Those propagate.
			if (digest == null || digest.Length != 32)
			{
				digest = null;
				fault = KernelFaultCode.CryptographicFailure;
				return false;
			}
			fault = KernelFaultCode.None;
			return true;
		}

		/// <summary>
		/// Written by hand from the digest bytes rather than through a formatter, because
		/// <c>ToString("x2")</c> is culture-shaped and has produced uppercase or non-ASCII digits
		/// under some cultures.
		/// </summary>
		internal static string ToLowercaseHex(byte[] bytes)
		{
			if (bytes == null)
			{
				throw new ArgumentNullException("bytes");
			}
			StringBuilder builder = new StringBuilder(bytes.Length * 2);
			for (int i = 0; i < bytes.Length; i++)
			{
				byte b = bytes[i];
				builder.Append(HexDigits[b >> 4]);
				builder.Append(HexDigits[b & 0x0F]);
			}
			return builder.ToString();
		}
	}
}
