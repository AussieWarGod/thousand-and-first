#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomCreedKindSourceTests
	{
		[Test]
		public void RegistryLoadsAsItsOwnMergedRuntimeDataAndReloadsAtomically()
		{
			string data = KingdomDataLogicalSource.Read();
			StringAssert.Contains("YieldXMLStreamsWithRoot(\"KingdomCreeds\")", data);
			StringAssert.Contains("{ \"creed\", HandleCreed }", data);
			StringAssert.Contains("KingdomCreedKindRules.TryMerge", data);
			StringAssert.Contains("_creedDrafts = null", data);
			StringAssert.Contains("_creedDefinitions = null", data);
		}

		[Test]
		public void EveryTheologicalEntryPointResolvesKindAndFailsClosed()
		{
			string conversion = TestMain.ReadRepositoryText(
				"Core/KingdomConversion.MealConversionAndCohabitation.cs")
				+ TestMain.ReadRepositoryText("Core/KingdomConversion.OsmosisAndBrink.cs")
				+ TestMain.ReadRepositoryText("Core/KingdomConversion.PressureAndHelpers.cs");
			Assert.GreaterOrEqual(conversion.Split(new string[] { "CreedUsesTheology" },
				System.StringSplitOptions.None).Length - 1, 7);
			string faith = TestMain.ReadRepositoryText("Experience/KingdomFaith.z01.ShrinePass.cs")
				+ TestMain.ReadRepositoryText("Experience/KingdomFaith.z02.ShrinePressureAndEducation.cs")
				+ TestMain.ReadRepositoryText("Experience/KingdomFaith.z03.EducationAndConsecration.cs");
			Assert.GreaterOrEqual(faith.Split(new string[] { "CreedUsesTheology" },
				System.StringSplitOptions.None).Length - 1, 4);
			StringAssert.Contains("candidates.RemoveAll", faith);
		}

		[Test]
		public void StoredCreedAbiStaysUntypedAndUnknownSavesAreNotRewritten()
		{
			string creed = TestMain.ReadRepositoryText("Core/KingdomCreed.00.ContentAndDraw.cs");
			StringAssert.Contains("public const string CreedProperty = \"KingdomCreed\"", creed);
			string definition = TestMain.ReadRepositoryText("Core/KingdomCreedDefinition.cs");
			StringAssert.DoesNotContain("Serialize", definition);
			string transition = TestMain.ReadRepositoryText("Core/KingdomConversion.Transitions.cs");
			StringAssert.Contains("AdoptionTelling", transition);
			StringAssert.Contains("ConversionTelling", transition);
		}

		[Test]
		public void ExplicitWaterRiteDispatchesAdoptionWithoutTheologicalObstacles()
		{
			string transaction = TestMain.ReadRepositoryText(
				"Experience/KingdomWaterRite.z02.RiteTransaction.cs");
			StringAssert.Contains("KingdomConversion.AdoptAffiliation", transaction);
			StringAssert.Contains("KingdomData.CreedUsesTheology(RealmCreed)", transaction);
			StringAssert.Contains("bool takesTheRoad = KingdomData.CreedUsesTheology", transaction);
			string facts = TestMain.ReadRepositoryText(
				"Experience/KingdomWaterRite.z03.OfferAndGates.cs");
			StringAssert.Contains("bool theological = KingdomData.CreedUsesTheology", facts);
			StringAssert.Contains("theological && KingdomQolRules.Has", facts);
			StringAssert.Contains("!KingdomData.CreedUsesTheology(consecrated)", facts);
			StringAssert.Contains("!KingdomData.CreedUsesTheology(closed)",
				TestMain.ReadRepositoryText("Experience/KingdomWaterRite.cs"));
		}

		[Test]
		public void CuratedMappingLedgerPinsPrimarySourceRulings()
		{
			string evidence = TestMain.ReadRepositoryText("_notes/CREED-KIND-EVIDENCE.md");
			foreach (string citation in new string[]
			{
				"Books.xml:768,1391", "Creatures.xml:3584,3587",
					"Creatures.xml:950-951", "Books.xml:452-469",
					"Factions.xml:1328-1351",
				"Conversations.xml:4303-4306,12077-12080",
				"Books.xml:564-572", "Factions.xml:1688-1719"
			}) StringAssert.Contains(citation, evidence);
			StringAssert.Contains("4 + 16 + 2 + 7 + 2 + 2 = 33", evidence);
		}
	}
}
#endif
