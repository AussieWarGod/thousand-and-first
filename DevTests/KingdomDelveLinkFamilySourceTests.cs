#if TAF_TESTS
using System;

using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	public class KingdomDelveLinkFamilySourceTests
	{
		[Test]
		public void LogicalFamilyKeepsPreflightSettlementStrikeReceiptAndCustodyOrder()
		{
			string source = KingdomDelveLinkLogicalSource.Read();
			Ordered(source,
				"public sealed class KingdomDelveLinkIntent",
				"public const string DownBlueprint",
				"private sealed class Derived",
				"public static bool TryPreflight(",
				"public static bool TrySettle(",
				"public static bool TryPreflightStrike(",
				"public static bool TryFinishStrike(",
				"public static bool TryReadPhysicalReceipt(",
				"private static bool TryDerive(",
				"private static bool TryInitializeRoot(",
				"private static bool TrySettleConnections(",
				"private static bool Quarantine(");
		}

		[Test]
		public void IntentConstantsAndNestedDerivationHaveOneOwner()
		{
			string source = KingdomDelveLinkLogicalSource.Read();
			ClassicAssert.AreEqual(8, Count(source, "public static partial class KingdomDelveLink"));
			ClassicAssert.AreEqual(1, Count(source, "public sealed class KingdomDelveLinkIntent"));
			ClassicAssert.AreEqual(1, Count(source, "private sealed class Derived"));
			ClassicAssert.AreEqual(1, Count(source, "public const int LinkSchema = 1"));
			StringAssert.DoesNotContain("public static class KingdomDelveLink", source);
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
