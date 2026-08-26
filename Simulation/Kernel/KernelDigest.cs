using System;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst.Simulation.Kernel
{
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
