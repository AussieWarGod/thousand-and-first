#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomZoningSourceTests
	{
		[Test]
		public void LogicalFamilyKeepsGatesRosterOffersGroundAndTransferOrder()
		{
			string source = KingdomZoningLogicalSource.Read();
			Ordered(source,
				"public const string RosterState",
				"private static readonly Dictionary<string, ZoneGate> Gates",
				"public static List<string> Roster(",
				"public static bool Offered(",
				"public static ZoningJudgement Judge(",
				"public static void ShowKeepers(",
				"private static ZoningJudgement JudgeAt(",
				"public static string KeptMegastructure(",
				"private static string Refusal(",
				"private static void SetDownWhatWasLearned(",
				"private static string Stored(");
		}

		[Test]
		public void StaticGateStateAndAuthorityDeclarationHaveOneOwner()
		{
			string source = KingdomZoningLogicalSource.Read();
			Assert.AreEqual(9, Count(source, "public static partial class KingdomZoning"));
			Assert.AreEqual(1, Count(source, "private static readonly Dictionary<string, ZoneGate> Gates"));
			Assert.AreEqual(1, Count(source, "private static string KeptCacheZone"));
			Assert.AreEqual(1, Count(source, "public static bool Offered("));
			StringAssert.DoesNotContain("public static class KingdomZoning", source);
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
