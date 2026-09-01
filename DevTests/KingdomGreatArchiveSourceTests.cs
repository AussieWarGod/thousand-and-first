#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomGreatArchiveSourceTests
	{
		[Test]
		public void RegistrationIsReadOnlyAndRunsAfterCatalogueReload()
		{
			string runtime = Read("Growth/KingdomGreatArchive.cs");
			StringAssert.Contains("ReadOnly = true", runtime);
			StringAssert.Contains("KnowledgeView = ViewKey", runtime);
			StringAssert.DoesNotContain("MaterialKey =", runtime);
			StringAssert.DoesNotContain("BuildTicks =", runtime);
			StringAssert.DoesNotContain("Crew =", runtime);
			string loader = Read("Core/KingdomLoader.cs");
			AssertBefore(loader, "KingdomData.Reload();",
				"KingdomGreatArchive.EnsureRegistered();");
		}

		[Test]
		public void DisplayTierBoundTracksTheResearchTierBound()
		{
			StringAssert.Contains("public const int TierCount = 4;",
				Read("Growth/KingdomResearchRules.cs"));
			StringAssert.Contains("public const int MaxTier = 4;",
				Read("Core/KingdomGreatArchiveRules.cs"));
		}

		[Test]
		public void EligibilityReadsOneExactLoadedCapitalSurvey()
		{
			string runtime = Read("Growth/KingdomGreatArchive.cs");
			StringAssert.Contains("HostRoot.CurrentZone != HostZone", runtime);
			StringAssert.Contains("System.City.SettlementId !=", runtime);
			StringAssert.Contains("KingdomHostedArcology.IsOperationalPure(HostRoot)", runtime);
			StringAssert.DoesNotContain("KingdomHostedArcology.Operational(HostRoot)", runtime);
			StringAssert.Contains("KingdomSurvey.TakeCustodyOnly(HostZone)", runtime);
			Assert.AreEqual(1, Count(runtime, "KingdomSurvey.TakeCustodyOnly("));
			StringAssert.DoesNotContain("KingdomSurvey.Take(", runtime);
			StringAssert.Contains("== \"bookshelf\"", runtime);
			StringAssert.Contains("== \"vellumpress\"", runtime);
			StringAssert.DoesNotContain("GetZone(", runtime);
		}

		[Test]
		public void ViewCannotStartOrMutateResearch()
		{
			string source = Read("Growth/KingdomGreatArchive.cs")
				+ Read("Growth/KingdomGreatArchive.Facts.cs")
				+ Read("Growth/KingdomGreatArchive.Requirements.cs")
				+ Read("Growth/KingdomGreatArchive.View.cs");
			StringAssert.Contains("JournalAPI.HasNote", source);
			StringAssert.DoesNotContain("KingdomResearch.Discovered(", source);
			StringAssert.DoesNotContain("ResearchSubject", source);
			StringAssert.DoesNotContain("ResearchAccrued", source);
			StringAssert.DoesNotContain("SetStringGameState", source);
			StringAssert.DoesNotContain("KingdomZoning.Learn", source);
			StringAssert.DoesNotContain("Popup.PickOption", source);
		}

		[Test]
		public void EveryGreatArchiveShardStaysBelowThreeHundredLines()
		{
			foreach (string folder in new[] { "Core", "Growth" })
			{
				string root = Path.Combine(TestMain.RepositoryRoot, folder);
				foreach (string file in Directory.GetFiles(root,
					"KingdomGreatArchive*.cs", SearchOption.TopDirectoryOnly))
					Assert.Less(File.ReadAllLines(file).Length, 300,
						Path.GetFileName(file) + " must be split");
			}
		}

		private static string Read(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static int Count(string source, string token)
		{
			int count = 0; int at = 0;
			while ((at = source.IndexOf(token, at, StringComparison.Ordinal)) >= 0)
			{
				count++; at += token.Length;
			}
			return count;
		}

		private static void AssertBefore(string source, string first, string second)
		{
			int a = source.IndexOf(first, StringComparison.Ordinal);
			int b = source.IndexOf(second, StringComparison.Ordinal);
			Assert.GreaterOrEqual(a, 0, first); Assert.Greater(b, a, second);
		}
	}
}
#endif
