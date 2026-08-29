#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomCeremonyStructureSourceTests
	{
		private static readonly string[] ProductionFiles =
		{
			"Experience/KingdomCeremony.cs",
			"Experience/KingdomCeremony.z01.Raising.cs",
			"Experience/KingdomCeremony.z02.ConstructionOutbox.cs",
			"Experience/KingdomCeremony.z03.NotableAndPatternBook.cs"
		};

		[Test]
		public void LogicalAuthorityRetainsEveryDeclarationInOriginalOrder()
		{
			Assert.AreEqual(4, KingdomCeremonyLogicalSource.FileCount);
			string source = KingdomCeremonyLogicalSource.Read();
			Assert.AreEqual(4, Count(source, "public static partial class KingdomCeremony"));
			AssertOrdered(source, "public static bool Enabled", "public static void StakePlan(",
				"public static void OnBuildingRaised(", "public static bool EnsureBuildingRaised(",
				"public static bool DispatchPending(", "public static bool EnsureRoadPaved(",
				"private static bool PublishRouteOutbox(", "private static bool Dispatch(",
				"public static void OnOfficeHolderNamed(", "private static string[] TasteOfferIn(",
				"public static KingdomTradePatternReceipt FreezePatternBook(",
				"private static long CurrentTicks()");
		}

		[Test]
		public void EveryProductionShardIsStrictlyUnderThreeHundredPhysicalLines()
		{
			for (int i = 0; i < ProductionFiles.Length; i++)
			{
				string source = TestMain.ReadRepositoryText(ProductionFiles[i]);
				int lines = source.Replace("\r\n", "\n").Split('\n').Length;
				Assert.Less(lines, 300, ProductionFiles[i]);
			}
		}

		private static int Count(string source, string token)
		{
			int count = 0;
			int cursor = 0;
			while ((cursor = source.IndexOf(token, cursor, StringComparison.Ordinal)) >= 0)
			{
				count++;
				cursor += token.Length;
			}
			return count;
		}

		private static void AssertOrdered(string source, params string[] tokens)
		{
			int cursor = -1;
			for (int i = 0; i < tokens.Length; i++)
			{
				int next = source.IndexOf(tokens[i], cursor + 1, StringComparison.Ordinal);
				Assert.Greater(next, cursor, tokens[i]);
				cursor = next;
			}
		}
	}
}
#endif
