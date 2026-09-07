#if TAF_TESTS
using System;

using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	public class KingdomTradeStateSourceTests
	{
		[Test]
		public void LogicalFamilyReassemblesDeclarationBookAndCodecOrder()
		{
			string source = KingdomTradeStateLogicalSource.Read();
			Ordered(source,
				"public enum KingdomTradeSchemaState",
				"public enum KingdomTradeManifestStatus",
				"public sealed class KingdomTradeProjectionRow",
				"public sealed class KingdomTradeOperation",
				"public sealed class KingdomTradeReferenceSeal",
				"public sealed class KingdomTradeBook",
				"public const int CurrentWireVersion",
				"public static KingdomTradeBook DecodeEnvelopeRaw(",
				"public static byte[] EncodePayload(",
				"private static void WriteCharter(",
				"private static void WriteOperation(",
				"private static KingdomTradeArchive ReadArchive(");
		}

		[Test]
		public void WireAuthorityAndKeyDeclarationsHaveOneOwner()
		{
			string source = KingdomTradeStateLogicalSource.Read();
			ClassicAssert.AreEqual(8, Count(source, "public static partial class KingdomTradeCodec"));
			ClassicAssert.AreEqual(1, Count(source, "public sealed class KingdomTradeBook"));
			ClassicAssert.AreEqual(1, Count(source, "public sealed class KingdomTradeManifestState"));
			ClassicAssert.AreEqual(1, Count(source, "public sealed class KingdomTradeOperation"));
			ClassicAssert.AreEqual(1, Count(source, "public sealed class KingdomTradeAuthoritySeal"));
			ClassicAssert.AreEqual(1, Count(source, "private static readonly UTF8Encoding StrictUtf8"));
			StringAssert.DoesNotContain("public static class KingdomTradeCodec", source);
		}

		private static void Ordered(string source, params string[] markers)
		{
			int position = -1;
			for (int i = 0; i < markers.Length; i++)
			{
				int next = source.IndexOf(markers[i], position + 1, StringComparison.Ordinal);
				ClassicAssert.Greater(next, position, markers[i]);
				position = next;
			}
		}

		private static int Count(string source, string token)
		{
			int count = 0;
			for (int at = 0; (at = source.IndexOf(token, at, StringComparison.Ordinal)) >= 0;
				at += token.Length) count++;
			return count;
		}
	}
}
#endif
