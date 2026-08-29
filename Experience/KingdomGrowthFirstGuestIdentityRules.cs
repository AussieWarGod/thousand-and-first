using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Stable public identities shared by the growth owner and O11. Kept independent of the
	/// lifecycle book so a reference consumer need not compile or construct that authority.
	/// </summary>
	public static class KingdomGrowthFirstGuestIdentityRules
	{
		private const int MaxIdChars = 256;
		private const int MaxTextBytes = 16384;
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static string OpportunityId(string settlementId, long sequence)
		{
			if (!ValidRootId(settlementId) || sequence <= 0L) return null;
			return HashId("growth-first-guest-opportunity", delegate(BinaryWriter writer)
			{
				CanonicalString(writer, settlementId);
				writer.Write(sequence);
			});
		}

		public static string CauseId(string settlementId, long sequence,
			long causeTick, long cadenceTicks)
		{
			if (!ValidRootId(settlementId) || sequence <= 0L || causeTick < 0L
				|| cadenceTicks <= 0L) return null;
			return HashId("growth-first-guest-cause", delegate(BinaryWriter writer)
			{
				CanonicalString(writer, settlementId);
				writer.Write(sequence);
				writer.Write(causeTick);
				writer.Write(cadenceTicks);
			});
		}

		public static string TerminalReceiptId(string candidateId, string decisionReceiptId,
			string arrivalOperationId, KingdomGrowthArrivalDisposition result, long terminalTick)
		{
			if (!ValidRootId(candidateId) || !ValidRootId(decisionReceiptId)
				|| !ValidRootId(arrivalOperationId) || terminalTick < 0L) return null;
			return HashId("growth-first-guest-terminal", delegate(BinaryWriter writer)
			{
				CanonicalString(writer, candidateId);
				CanonicalString(writer, decisionReceiptId);
				CanonicalString(writer, arrivalOperationId);
				writer.Write((byte)result); writer.Write(terminalTick);
			});
		}

		private static bool ValidRootId(string value)
		{
			if (string.IsNullOrEmpty(value) || value.Length > MaxIdChars) return false;
			try { return StrictUtf8.GetByteCount(value) <= MaxIdChars * 4; }
			catch (EncoderFallbackException) { return false; }
		}

		private static string HashId(string name, Action<BinaryWriter> writePayload)
		{
			try
			{
				byte[] bytes;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					CanonicalString(writer, "taf:kingdom-lifecycle:v3");
					CanonicalString(writer, name);
					writePayload(writer);
					writer.Flush();
					bytes = stream.ToArray();
				}
				byte[] digest;
				using (SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(bytes);
				StringBuilder hex = new StringBuilder(64);
				for (int i = 0; i < digest.Length; i++)
					hex.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
				return "taf:" + name + ":" + hex;
			}
			catch (Exception) { return null; }
		}

		private static void CanonicalString(BinaryWriter writer, string value)
		{
			int byteCount = StrictUtf8.GetByteCount(value);
			if (byteCount > MaxTextBytes)
				throw new InvalidDataException("bounded canonical string exceeded");
			byte[] bytes = StrictUtf8.GetBytes(value);
			writer.Write(byteCount);
			writer.Write(bytes);
		}
	}
}
