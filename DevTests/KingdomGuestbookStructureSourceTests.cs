#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomGuestbookStructureSourceTests
	{
		private static readonly string[] ProductionFiles =
		{
			"Experience/KingdomGuestbook.cs",
			"Experience/KingdomGuestbook.z01.LodgingAndHousing.cs",
			"Experience/KingdomGuestbook.z01b.MarketHandoff.cs",
			"Experience/KingdomGuestbook.z02.Lifecycle.cs",
			"Experience/KingdomGuestbook.z03.ReportingAndCarrySign.cs",
			"Experience/KingdomCarryHaul.cs",
			"Experience/r_KingdomNotableGuest.cs",
			"Experience/r_KingdomCarrySign.cs"
		};

		[Test]
		public void LogicalAuthorityRetainsEveryDeclarationInOriginalOrder()
		{
			Assert.AreEqual(8, KingdomGuestbookLogicalSource.FileCount);
			string source = KingdomGuestbookLogicalSource.Read();
			AssertOrdered(source, "public static void OnZoneActivated(",
				"public static void TryLodge(", "internal static void AppendGuestbookLine(",
				"public static string RollAppendix(", "public class KingdomCarryHaul",
				"public class r_KingdomNotableGuest", "public class r_KingdomCarrySign");
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
