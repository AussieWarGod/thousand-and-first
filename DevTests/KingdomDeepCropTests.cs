#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomDeepCropTests
	{
		[Test]
		public void DeepAgriculturalDesignsDeclareOneExistingFungalCrop()
		{
			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			Dictionary<string, XElement> index = objects.Descendants("object").ToDictionary(
				e => (string)e.Attribute("Name"), StringComparer.Ordinal);
			XDocument buildings = XDocument.Parse(TestMain.ReadRepositoryText("KingdomBuildings.xml"));
			foreach (string key in new string[] { "fungalvault", "vaultgalleries" })
			{
				XElement building = buildings.Descendants("building").Single(e =>
					(string)e.Attribute("Key") == key);
				Assert.AreEqual("deep", (string)building.Attribute("Strata"), key);
				Assert.IsNull(building.Attribute("Crop"),
					"crop identity belongs to the physical field, not new catalogue/stratum grammar");
				Assert.AreEqual("Plump Mushroom", InheritedTag(index,
					(string)building.Attribute("Blueprint"), "r_KingdomCropBlueprint"), key);
			}
			XElement declaration = objects.Descendants("tag").Single(e =>
				(string)e.Attribute("Name") == "r_KingdomCropBlueprint");
			Assert.AreEqual("r_KingdomFungalVault",
				(string)declaration.Parent.Attribute("Name"));
		}

		[Test]
		public void DeclaredFungusUsesTheExistingFlatSeedRowAndYieldContract()
		{
			const string crop = "Plump Mushroom";
			XElement row = XDocument.Parse(TestMain.ReadRepositoryText(
				"KingdomBuildings.xml")).Descendants("style").Single(e =>
				(string)e.Attribute("Name") == "fungal");
			Assert.IsTrue(KingdomStyleRules.TryParse(new KingdomStyleDraft
			{
				Name = (string)row.Attribute("Name"),
				Terrain = (string)row.Attribute("Terrain"),
				Region = (string)row.Attribute("Region"),
				Strata = (string)row.Attribute("Strata"),
				Priority = (string)row.Attribute("Priority"),
				GroundClause = (string)row.Attribute("GroundClause"),
				Crop = (string)row.Attribute("Crop"),
				Seed = (string)row.Attribute("Seed"),
				CropRow = (string)row.Attribute("CropRow"),
				WallMaterial = (string)row.Attribute("WallMaterial"),
				TimberWall = (string)row.Attribute("TimberWall")
			}, out KingdomStyleDefinition fungal, out string error), error);
			List<KingdomStyleDefinition> registry = new List<KingdomStyleDefinition> { fungal };
			Assert.IsTrue(KingdomCropRules.DeclaredCropAllows(null, "Starapple"));
			Assert.IsTrue(KingdomCropRules.DeclaredCropAllows(crop, crop));
			Assert.IsFalse(KingdomCropRules.DeclaredCropAllows(crop, "Starapple"));
			Assert.AreEqual("r_KingdomSeedMushroom",
				KingdomStyleRules.SeedForCrop(registry, crop));
			Assert.AreEqual(crop,
				KingdomStyleRules.CropForSeed(registry,
					KingdomStyleRules.SeedForCrop(registry, crop)));
			Assert.AreEqual("r_KingdomRowMushroom",
				KingdomStyleRules.RowForCrop(registry, crop));
			Assert.AreEqual(6, KingdomCropRules.FoodPerDayForRows(12));
			Assert.AreEqual(18, KingdomCropRules.FoodPerDayForRows(36));
			Assert.AreEqual(KingdomCropRules.CropDays,
				KingdomCropRules.CropDaysForStyle("fungal"));
		}

		[Test]
		public void WrongCropRefusesBeforeConsentDebitMutationOrSeedDestruction()
		{
			string source = KingdomCropsLogicalSource.Read();
			StringAssert.Contains("KingdomData.CropForSeed", source,
				"sowing must resolve the merged style registry");
			StringAssert.Contains("KingdomData.RowForCrop", source,
				"row identity must come from the same merged registry");
			int declaration = At(source, "string declaredCrop = DeclaredCrop(work);");
			int refusal = At(source, "DeclaredCropRefusal(");
			Assert.Less(declaration, refusal);
			Assert.Less(refusal, At(source, "Popup.ShowYesNo("));
			Assert.Less(refusal, At(source, "TryReserveExactWater("));
			Assert.Less(refusal, At(source, "debit.Commit()"));
			Assert.Less(refusal, At(source, "Seed.Destroy("));
			StringAssert.Contains("KingdomOrdinaryFoodAuthority.TryObjectNow(Seed", source);
			StringAssert.Contains("SeedAtSnapshot(Seed", source);
			StringAssert.Contains("TryObjectNow(rowsAfter[i]", source);
		}

		[Test]
		public void RefusalNamesBothFieldAndRequiredCrop()
		{
			string refusal = KingdomCropRules.DeclaredCropRefusal(
				"Plump Mushroom", "fungal vault");
			StringAssert.Contains("fungal vault", refusal);
			StringAssert.Contains("Plump Mushroom", refusal);
			StringAssert.Contains("seed", refusal);
		}

		private static string InheritedTag(Dictionary<string, XElement> index,
			string blueprint, string tag)
		{
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			while (!string.IsNullOrEmpty(blueprint) && seen.Add(blueprint))
			{
				XElement current;
				if (!index.TryGetValue(blueprint, out current)) return null;
				XElement found = current.Elements("tag").FirstOrDefault(e =>
					(string)e.Attribute("Name") == tag);
				if (found != null) return (string)found.Attribute("Value");
				blueprint = (string)current.Attribute("Inherits");
			}
			return null;
		}

		private static int At(string source, string token)
		{
			int at = source.IndexOf(token, StringComparison.Ordinal);
			Assert.GreaterOrEqual(at, 0, token);
			return at;
		}
	}
}
#endif
