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
}
