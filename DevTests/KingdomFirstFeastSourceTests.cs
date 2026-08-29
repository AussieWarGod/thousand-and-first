#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomFirstFeastSourceTests
	{
		private static string Read(string Name)
		{
			return TestMain.ReadRepositoryText(Path.Combine("Experience", Name));
		}

		[Test]
		public void OfferIsDurableBeforeDisplayAndDeferHasNoWritePath()
		{
			string open = Read("KingdomFirstFeastRuntime.Open.cs");
			Assert.Less(open.IndexOf("TryPublishOffer(System, context", StringComparison.Ordinal),
				open.IndexOf("RenderOffer(receipt", StringComparison.Ordinal));
			string transitions = Read("KingdomFirstFeastRules.Transitions.cs");
			int defer = transitions.IndexOf(
				"if (Choice == KingdomFirstFeastChoice.Defer)", StringComparison.Ordinal);
			int decision = transitions.IndexOf("Next = Current.Copy(); Next.Choice = Choice",
				StringComparison.Ordinal);
			Assert.Greater(defer, 0); Assert.Greater(decision, defer);
			string slice = transitions.Substring(defer, decision - defer);
			StringAssert.Contains("Next = Current.Copy(); return true;", slice);
			StringAssert.DoesNotContain("Changed = true", slice);
			StringAssert.DoesNotContain("DecidedTick =", slice);
			string store = Read("KingdomExperienceRules.FirstFeast.cs");
			StringAssert.Contains("if (!changed)", store);
			StringAssert.Contains("Receipt = decided; return true;", store);
		}

		[Test]
		public void PracticeHasOneAffirmativeActionAndNoMechanicalDescendants()
		{
			string[] files = new string[] {
				"KingdomFirstFeastModels.cs", "KingdomFirstFeastRules.cs",
				"KingdomFirstFeastRules.Transitions.cs", "KingdomFirstFeastRules.Prose.cs",
				"KingdomFirstFeastRuntime.Context.cs", "KingdomFirstFeastRuntime.Open.cs",
				"KingdomFirstFeastRuntime.Telling.cs",
				"KingdomExperienceRules.FirstFeast.cs" };
			string combined = "";
			for (int i = 0; i < files.Length; i++) combined += Read(files[i]);
			string[] forbidden = new string[] { "CookingGameState", "CookingRecipe",
				"FromIngredients", "LearnRecipe", "AddRecipeNote", "JournalAPI",
				"specificProcgenMeals", "presetMeals", "GameObjectFactory",
				"AddXP", "Award", "Reputation", "KingdomLarder", "HoldSharedMeal",
				"Calendar.Get", "Faction.WaterRitualRecipeText" };
			for (int i = 0; i < forbidden.Length; i++)
				StringAssert.DoesNotContain(forbidden[i], combined, forbidden[i]);
			string open = Read("KingdomFirstFeastRuntime.Open.cs");
			Assert.AreEqual(1, Count(open, "KingdomGovernanceScope.Commit("));
			StringAssert.Contains("decision == KingdomFirstFeastChoice.Adopt", open);
			StringAssert.Contains("decision == KingdomFirstFeastChoice.Adapt", open);
			StringAssert.Contains("if (committed", open);
		}

		[Test]
		public void O9UsesNamedCookServiceAndDefinesNoSecondRecipeAuthority()
		{
			string models = Read("KingdomFirstFeastModels.cs");
			string rules = Read("KingdomFirstFeastRules.cs");
			string telling = Read("KingdomFirstFeastRuntime.Telling.cs");
			StringAssert.Contains("NamedCookServiceSupersedes", models + rules);
			StringAssert.Contains("KingdomNamedCookRules.ServiceState", telling);
			StringAssert.Contains("RecipePolicyText", telling);
			StringAssert.DoesNotContain("LearnRecipe", models + rules + telling);
			StringAssert.DoesNotContain("knownRecipies", models + rules + telling);
			StringAssert.DoesNotContain("TeachesDish", models + rules + telling);
		}

		[Test]
		public void CharterSurfacesOneExactFirstFeastCommand()
		{
			string menu = TestMain.ReadRepositoryText(Path.Combine("Core",
				"KingdomCharterMenuRules.cs"));
			string charter = TestMain.ReadRepositoryText(Path.Combine("Core",
				"KingdomCharterPart.cs"));
			Assert.AreEqual(3, Count(menu, "FirstFeastPractice"),
				"enum, route, and chapter placement are expected");
			Assert.AreEqual(1, Count(charter,
				"case KingdomCharterAction.FirstFeastPractice:"));
			StringAssert.Contains("KingdomFirstFeastRuntime.Open(System, ParentObject)", charter);
		}

		[Test]
		public void FoundingAnchorsEligibilityAndLoadRecoversOnlyCommittedTelling()
		{
			string founding = TestMain.ReadRepositoryText(Path.Combine("Core",
				"KingdomFounding.01.FirstPublication.cs"));
			string loader = TestMain.ReadRepositoryText(Path.Combine("Core",
				"KingdomLoader.cs"));
			StringAssert.Contains("Math.Max(system.SettlementIdentityFoundedTick", founding);
			StringAssert.Contains("TryObserveConfiguredOptions(system, experienceTick", founding);
			StringAssert.Contains("KingdomFirstFeastRuntime.ReconcileBestEffort(kingdomSystem)",
				loader);
			StringAssert.DoesNotContain("TryPublishOffer", loader);
		}

		[Test]
		public void DeedRequiresExactJoinedGuestThenLaterTerminalChronicleReceipt()
		{
			string context = Read("KingdomFirstFeastRuntime.Context.cs");
			AssertOrdered(context, "TryJoinedAwaitingPractice", "TerminalDigest(guest)",
				"TryAdventureAfter(guest.GuestTerminalTick", "TryBuildDeedId(candidate");
			StringAssert.Contains("row.Updated <= guestTick", context);
			StringAssert.Contains("KingdomChronicleReceiptRules.IsTerminal(row)", context);
			StringAssert.Contains("row.LegacyBlocked", context);
			StringAssert.DoesNotContain("FoundingTransactionId, Context.FoundedTick", context);
			string rules = Read("KingdomFirstFeastRules.cs");
			StringAssert.Contains("GuestTerminalReceiptId", rules);
			StringAssert.Contains("GuestTerminalDigest", rules);
			StringAssert.Contains("AdventureEventId", rules);
			StringAssert.Contains("AdventureFingerprint", rules);
			StringAssert.Contains("Row.DeedTick <= Row.GuestTerminalTick", rules);
		}

		[Test]
		public void FirstFeastProductionFilesStayBelowThreeHundredLines()
		{
			string[] files = new string[] { "KingdomFirstFeastModels.cs",
				"KingdomFirstFeastRules.cs", "KingdomFirstFeastRules.Transitions.cs",
				"KingdomFirstFeastRules.Prose.cs", "KingdomFirstFeastRuntime.Context.cs",
				"KingdomFirstFeastRuntime.Open.cs", "KingdomFirstFeastRuntime.Telling.cs",
				"KingdomExperienceState.FirstFeast.cs",
				"KingdomExperienceRules.FirstFeast.cs",
				"KingdomExperienceRules.FirstFeastRetirement.cs",
				"KingdomExperienceCodec.FirstFeast.cs" };
			for (int i = 0; i < files.Length; i++)
			{
				int lines = Read(files[i]).Split(new char[] { '\n' }).Length;
				Assert.Less(lines, 300, files[i] + " has " + lines + " lines");
			}
		}

		private static int Count(string Text, string Needle)
		{
			int count = 0, at = 0;
			while ((at = Text.IndexOf(Needle, at, StringComparison.Ordinal)) >= 0)
			{
				count++; at += Needle.Length;
			}
			return count;
		}

		private static void AssertOrdered(string source, params string[] needles)
		{
			int prior = -1;
			for (int i = 0; i < needles.Length; i++)
			{
				int at = source.IndexOf(needles[i], prior + 1, StringComparison.Ordinal);
				Assert.Greater(at, prior, needles[i]); prior = at;
			}
		}
	}
}
#endif
