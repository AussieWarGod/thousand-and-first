#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomPortersSourceTests
	{
		[Test]
		public void LogicalFamilyKeepsOpeningRenderingCustodyAndRouteOrder()
		{
			string source = KingdomPortersLogicalSource.Read();
			Ordered(source,
				"public const string StockProperty",
				"public static int Embody(",
				"private static int Fold(",
				"public static void Render(",
				"internal static void Step(",
				"private static GameObject Mint(",
				"private static void Deposit(",
				"private static bool Close(",
				"private static void Handoff(",
				"private static bool TryPlan(",
				"private static bool TryPassage(",
				"private static GameObject NearestLarderWithRoom(",
				"private static void Refuse(");
		}

		[Test]
		public void ConstantsAndAuthorityDeclarationStayUnique()
		{
			string source = KingdomPortersLogicalSource.Read();
			Assert.AreEqual(7, Count(source, "public static partial class KingdomPorters"));
			Assert.AreEqual(1, Count(source, "public const int LoadPerTrip = 12"));
			Assert.AreEqual(1, Count(source, "public const string StockProperty"));
			Assert.AreEqual(1, Count(source, "private static void Handoff("));
			StringAssert.DoesNotContain("public static class KingdomPorters", source);
		}

		private static void Ordered(string source, params string[] markers)
		{
			int position = -1;
			for (int i = 0; i < markers.Length; i++)
			{
				int next = source.IndexOf(markers[i], position + 1, StringComparison.Ordinal);
				Assert.Greater(next, position, markers[i]);
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
