#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomFoundingSourceTests
	{
		[Test]
		public void LogicalFamilyKeepsRegistrationPublicationClaimsAndCharterOrder()
		{
			string source = KingdomFoundingLogicalSource.Read();
			Ordered(source,
				"private const string PendingFactionProperty",
				"public static Faction Found(",
				"private static Faction CompleteFirstPublication(",
				"private static bool TryReadOrFreezeFoundingStandings(",
				"public static KingdomSettlement.SecondFoundingVerdict JudgeSite(",
				"public static bool ClaimZone(",
				"public static bool EnrollCitizen(",
				"internal static bool TryRestoreRuinStructures(",
				"public static void CharterVillage(");
		}

		[Test]
		public void ConstantsAndAuthorityDeclarationHaveOneOwner()
		{
			string source = KingdomFoundingLogicalSource.Read();
			Assert.AreEqual(9, Count(source, "public static partial class KingdomFounding"));
			Assert.AreEqual(1, Count(source, "private const string PendingFactionProperty"));
			Assert.AreEqual(1, Count(source, "internal static bool ClaimZone("));
			Assert.AreEqual(1, Count(source, "internal static bool TryRestoreRuinStructures("));
			StringAssert.DoesNotContain("public static class KingdomFounding", source);
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
