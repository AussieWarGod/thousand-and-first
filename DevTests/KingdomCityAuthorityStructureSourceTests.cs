#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomCityAuthorityStructureSourceTests
	{
		private static readonly string[] ProductionFiles =
		{
			"Simulation/City/KingdomHappenings.cs",
			"Simulation/City/KingdomHappenings.z01.FestivalsAndPilgrims.cs",
			"Simulation/City/KingdomHappenings.z02.WeddingsAndBreakdowns.cs",
			"Simulation/City/KingdomHappenings.z03.Funerals.cs",
			"Simulation/City/KingdomHappenings.z04.ReportingAndPlumbing.cs",
			"Simulation/City/KingdomHeartbeat.cs",
			"Simulation/City/KingdomHeartbeat.z01.Slice.cs",
			"Simulation/City/KingdomHeartbeat.z02.Prefetch.cs"
		};

		[Test]
		public void LogicalAuthoritiesRetainEveryShardInDeclarationOrder()
		{
			Assert.AreEqual(5, KingdomHappeningsLogicalSource.FileCount);
			string happenings = KingdomHappeningsLogicalSource.Read();
			AssertOrdered(happenings, "internal static int Reckon(",
				"private static KingdomCityState Festivals(",
				"private static KingdomCityState Weddings(",
				"public static string FuneralClause(",
				"public static void Digest(", "private static string Named(");

			Assert.AreEqual(3, KingdomHeartbeatLogicalSource.FileCount);
			string heartbeat = KingdomHeartbeatLogicalSource.Read();
			AssertOrdered(heartbeat, "public static void OnEndTurn(KingdomSystem System)",
				"private static void Slice(", "private static int Advance(",
				"private static void Prefetch(", "private static void Refuse(");
		}

		[Test]
		public void EverySplitProductionShardIsStrictlyUnderThreeHundredPhysicalLines()
		{
			for (int i = 0; i < ProductionFiles.Length; i++)
			{
				string source = TestMain.ReadRepositoryText(ProductionFiles[i]);
				int lines = source.Replace("\r\n", "\n").Split('\n').Length;
				Assert.Less(lines, 300, ProductionFiles[i]);
			}
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
