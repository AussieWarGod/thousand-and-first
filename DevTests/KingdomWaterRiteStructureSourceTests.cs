#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomWaterRiteStructureSourceTests
	{
		private static readonly string[] ProductionFiles =
		{
			"Experience/KingdomWaterRite.cs",
			"Experience/KingdomWaterRite.z01.SharedLiving.cs",
			"Experience/KingdomWaterRite.z02.RiteTransaction.cs",
			"Experience/KingdomWaterRite.z03.OfferAndGates.cs",
			"Experience/KingdomWaterRite.z04.StampsAndCandidates.cs"
		};

		[Test]
		public void LogicalAuthorityRetainsEveryDeclarationInOriginalOrder()
		{
			Assert.AreEqual(5, KingdomWaterRiteLogicalSource.FileCount);
			string source = KingdomWaterRiteLogicalSource.Read();
			Assert.AreEqual(5, Count(source, "public static partial class KingdomWaterRite"));
			AssertOrdered(source, "public static bool Enabled", "private sealed class RepeatedAsking",
				"public static void Register(", "public static void OpenRite(",
				"public static void OnSettlementPass(", "private static void AdvanceSharedDays(",
				"public static int SharedDaysOf(", "public static string DumpLine(",
				"private static void Hold(", "private static bool Accept(",
				"private static void Chronicle(", "private sealed class RiteOffer",
				"private static RiteOffer OfferFor(", "private static WaterRiteBar BarFor(",
				"private static bool CouldWalkAway(", "private static void WriteStamp(",
				"private static List<GameObject> CandidatesIn(", "private static string NameOf(");
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
