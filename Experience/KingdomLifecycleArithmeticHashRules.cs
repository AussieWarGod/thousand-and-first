using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		public static bool CheckedAdd(long A, long B, out long Result)
		{
			if ((B > 0L && A > long.MaxValue - B) || (B < 0L && A < long.MinValue - B))
			{
				Result = A;
				return false;
			}
			Result = A + B;
			return true;
		}

		public static bool CheckedAdd(int A, int B, out int Result)
		{
			long value = (long)A + B;
			if (value < int.MinValue || value > int.MaxValue)
			{
				Result = A;
				return false;
			}
			Result = (int)value;
			return true;
		}

		public static bool ExactCountTransition(int Before, int After, int Removed,
			bool SameObject, bool SameContext)
		{
			return Before > 0 && Removed > 0 && Removed <= Before
				&& After == Before - Removed && SameObject && SameContext;
		}

		private static bool CheckedAccumulate(long[] Values, int Index, long Delta)
		{
			if (Values == null || Index < 0 || Index >= Values.Length || Delta < 0L) return false;
			long value;
			if (!CheckedAdd(Values[Index], Delta, out value)) return false;
			Values[Index] = value;
			return true;
		}

		private static long SumSix(int a, int b, int c, int d, int e, int f)
		{
			if (a < 0 || b < 0 || c < 0 || d < 0 || e < 0 || f < 0) return -1L;
			return (long)a + b + c + d + e + f;
		}

		private static string HashId(string Namespace, Action<BinaryWriter> WritePayload)
		{
			if (string.IsNullOrEmpty(Namespace) || WritePayload == null) return null;
			try
			{
				byte[] bytes;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					CanonicalString(writer, "taf:kingdom-lifecycle:v3");
					CanonicalString(writer, Namespace);
					WritePayload(writer);
					writer.Flush();
					bytes = stream.ToArray();
				}
				byte[] digest;
				using (SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(bytes);
				StringBuilder hex = new StringBuilder(64);
				for (int i = 0; i < digest.Length; i++)
					hex.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
				return "taf:" + Namespace + ":" + hex;
			}
			catch (Exception)
			{
				return null;
			}
		}

		private static void CanonicalString(BinaryWriter Writer, string Value)
		{
			if (Value == null)
			{
				Writer.Write(-1);
				return;
			}
			int byteCount = StrictUtf8.GetByteCount(Value);
			if (byteCount > MaxTextBytes)
				throw new InvalidDataException("bounded canonical string exceeded");
			byte[] bytes = StrictUtf8.GetBytes(Value);
			Writer.Write(byteCount);
			Writer.Write(bytes);
		}

		private static bool ValidGeneratedId(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > MaxIdChars || !Value.StartsWith("taf:",
				StringComparison.Ordinal)) return false;
			int colon = Value.LastIndexOf(':');
			if (colon <= 4 || Value.Length - colon - 1 != 64) return false;
			for (int i = colon + 1; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9') || (Value[i] >= 'a' && Value[i] <= 'f')))
					return false;
			return true;
		}

		private static bool ValidHashNamespace(string Value, string Namespace)
		{
			return ValidGeneratedId(Value) && Value.StartsWith("taf:" + Namespace + ":",
				StringComparison.Ordinal);
		}
	}
}
