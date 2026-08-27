#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomRaidIncidentRulesSourceTests
	{
		[Test]
		public void LogicalFamilyKeepsIdentityPublicationApplyValidationAndResolutionOrder()
		{
			string source = KingdomRaidIncidentRulesLogicalSource.Read();
			Ordered(source,
				"public const int MaxSeverity",
				"public static bool TryEncodeDefenceReservations(",
				"public static string GrievanceId(",
				"public static bool CanPublish(",
				"public static bool TryApply(",
				"public static bool ValidLedger(",
				"public static KingdomRaidLedger Copy(",
				"private static bool Resolve(",
				"private static bool IncidentFieldShape(",
				"private static bool CurrentLedger(",
				"private static bool RecoveryMatches(");
		}

		[Test]
		public void ConstantsAndAuthorityDeclarationHaveOneOwner()
		{
			string source = KingdomRaidIncidentRulesLogicalSource.Read();
			Assert.AreEqual(7, Count(source, "public static partial class KingdomRaidIncidentRules"));
			Assert.AreEqual(1, Count(source, "public const int MaxSeverity = 4"));
			Assert.AreEqual(1, Count(source, "public static bool TryApply("));
			Assert.AreEqual(1, Count(source, "public static bool ValidLedger("));
			StringAssert.DoesNotContain("public static class KingdomRaidIncidentRules", source);
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
