#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomCreedSourceTests
	{
		[Test]
		public void LogicalFamilyKeepsContentHistoryBrinkRiteSecessionAndReportOrder()
		{
			string source = KingdomCreedLogicalSource.Read();
			Ordered(source,
				"public const string CreedProperty",
				"public static bool CanBeCreed(",
				"public static void Record(",
				"public static void OnZoneActivated(",
				"private static void RunSecessionWindow(",
				"public static bool HoldRite(",
				"public static bool Declare(",
				"public static void EaseForMeal(",
				"public static bool Secede(",
				"public static string Report(",
				"private static void Reconcile(");
		}

		[Test]
		public void ConstantsAndAuthorityDeclarationHaveOneOwner()
		{
			string source = KingdomCreedLogicalSource.Read();
			Assert.AreEqual(7, Count(source, "public static partial class KingdomCreed"));
			Assert.AreEqual(1, Count(source, "public const string CreedProperty"));
			Assert.AreEqual(1, Count(source, "public const int CreedSignificance = 3"));
			Assert.AreEqual(1, Count(source, "public static bool Declare("));
			StringAssert.DoesNotContain("public static class KingdomCreed", source);
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
