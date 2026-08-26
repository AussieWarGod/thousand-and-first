#if TAF_TESTS
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomStyleRulesTests
	{
		private static KingdomStyleDefinition Style(string name, string terrain = null,
			string region = null, string strata = null, string priority = null,
			string clause = null, string crop = null, string seed = null, string row = null,
			string wallMaterial = null, string timberWall = null)
		{
			KingdomStyleDraft draft = new KingdomStyleDraft
			{
				Name = name, Terrain = terrain, Region = region, Strata = strata,
				Priority = priority, GroundClause = clause, Crop = crop, Seed = seed,
				CropRow = row, WallMaterial = wallMaterial, TimberWall = timberWall
			};
			Assert.IsTrue(KingdomStyleRules.TryParse(draft, out KingdomStyleDefinition result,
				out string error), error);
			return result;
		}

		[Test]
		public void ExternalStyleOwnsCropSeedRowAndMaterialBehaviourWithoutCoreEdits()
		{
			List<KingdomStyleDefinition> definitions = new List<KingdomStyleDefinition>
			{
				Style("common", crop: "Starapple", seed: "CommonSeed", row: "CommonRow",
					timberWall: "CommonWall"),
				Style("glass", crop: "Congealed love", seed: "GlassSeed", row: "GlassRow",
					wallMaterial: "shaped stone", timberWall: "GlassWall")
			};

			Assert.AreEqual("Congealed love", KingdomStyleRules.CropForStyle(definitions, "GLASS"));
			Assert.AreEqual("GlassSeed", KingdomStyleRules.SeedForStyle(definitions, "glass"));
			Assert.AreEqual("GlassRow", KingdomStyleRules.CropRowForStyle(definitions, "glass"));
			Assert.AreEqual("Congealed love", KingdomStyleRules.CropForSeed(definitions, "GlassSeed"));
			Assert.AreEqual("GlassSeed", KingdomStyleRules.SeedForCrop(definitions, "Congealed love"));
			Assert.AreEqual("GlassRow", KingdomStyleRules.RowForCrop(definitions, "Congealed love"));
			Assert.IsTrue(KingdomStyleRules.TryWallMaterial(definitions, "glass",
				out KingdomMaterial material));
			Assert.AreEqual(KingdomMaterial.ShapedStone, material);
			Assert.AreEqual("GlassWall", KingdomStyleRules.TimberWallForStyle(definitions, "glass"));
			Assert.AreEqual("Starapple", KingdomStyleRules.CropForStyle(definitions, "old-style"));
			Assert.AreEqual("CommonWall", KingdomStyleRules.TimberWallForStyle(definitions, "old-style"));
		}

		[Test]
		public void CropBehaviourIsAtomicAndReverseMappingsCannotConflict()
		{
			Assert.IsTrue(KingdomStyleRules.TryParse(new KingdomStyleDraft
			{
				Name = "partial", Crop = "CropOnly"
			}, out KingdomStyleDefinition partial, out string parseError), parseError);
			Assert.IsFalse(KingdomStyleRules.TryValidateBehavior(null, partial, -1,
				out string partialError));
			StringAssert.Contains("Crop, Seed, and CropRow together", partialError);

			List<KingdomStyleDefinition> definitions = new List<KingdomStyleDefinition>
			{
				Style("first", crop: "FirstCrop", seed: "SharedSeed", row: "FirstRow")
			};
			KingdomStyleDefinition conflict = Style("second", crop: "SecondCrop",
				seed: "SharedSeed", row: "SecondRow");
			Assert.IsFalse(KingdomStyleRules.TryValidateBehavior(definitions, conflict, -1,
				out string conflictError));
			StringAssert.Contains("different crop", conflictError);

			KingdomStyleDefinition shared = Style("third", crop: "FirstCrop",
				seed: "SharedSeed", row: "FirstRow");
			Assert.IsTrue(KingdomStyleRules.TryValidateBehavior(definitions, shared, -1,
				out string sharedError), sharedError);
		}

		[Test]
		public void ExternalStyleIsValidatedCanonicalAndSelectableFromTerrain()
		{
			List<KingdomStyleDefinition> definitions = new List<KingdomStyleDefinition>
			{
				Style("common", clause: "common ground"),
				Style("glass", "TerrainGlass,CrystalDunes", "Glass", "surface", "700",
					"ground bright enough to found a glass city")
			};
			Assert.AreEqual("glass", KingdomStyleRules.Resolve(definitions,
				"TerrainGlassDunes", "Desert", 10, 10));
			Assert.IsTrue(KingdomStyleRules.TryCanonical(definitions, " GLASS ",
				out string canonical));
			Assert.AreEqual("glass", canonical);
			Assert.AreEqual("ground bright enough to found a glass city",
				KingdomStyleRules.DescribeGround(definitions, "glass"));
		}

		[Test]
		public void ExactTerrainLaneOutranksRegionEvenAtLowerPriority()
		{
			List<KingdomStyleDefinition> definitions = new List<KingdomStyleDefinition>
			{
				Style("common"),
				Style("ruin", "Ruins", null, "all", "10"),
				Style("marsh", null, "Saltmarsh", "all", "900")
			};
			Assert.AreEqual("ruin", KingdomStyleRules.Resolve(definitions,
				"TerrainJoppaRuins", "Saltmarsh", 10, 10));
		}

		[Test]
		public void PriorityThenDeclarationOrderBreakAmbiguousSelectorTies()
		{
			List<KingdomStyleDefinition> definitions = new List<KingdomStyleDefinition>
			{
				Style("common"),
				Style("first", "Ruins", null, "all", "10"),
				Style("second", "Ruins", null, "all", "10"),
				Style("high", "Ruins", null, "all", "11")
			};
			Assert.AreEqual("high", KingdomStyleRules.Resolve(definitions,
				"TerrainRuins", null, 10, 10));
			definitions.RemoveAt(3);
			Assert.AreEqual("first", KingdomStyleRules.Resolve(definitions,
				"TerrainRuins", null, 10, 10));
		}

		[Test]
		public void StratumSelectorMakesSurfaceAndDeepStylesDistinct()
		{
			List<KingdomStyleDefinition> definitions = new List<KingdomStyleDefinition>
			{
				Style("common"),
				Style("canopy", "Jungle", null, "surface", "20"),
				Style("root", "Jungle", null, "deep", "30")
			};
			Assert.AreEqual("canopy", KingdomStyleRules.Resolve(definitions,
				"TerrainJungle", null, 10, 10));
			Assert.AreEqual("root", KingdomStyleRules.Resolve(definitions,
				"TerrainJungle", null, 11, 10));
		}

		[Test]
		public void NoSelectorStyleIsStillForceableButNeverHijacksFounding()
		{
			List<KingdomStyleDefinition> definitions = new List<KingdomStyleDefinition>
			{
				Style("common"), Style("glass")
			};
			Assert.AreEqual("common", KingdomStyleRules.Resolve(definitions,
				"TerrainUnknown", "Unknown", 10, 10));
			Assert.IsTrue(KingdomStyleRules.TryCanonical(definitions, "glass", out _));
		}

		[Test]
		public void LaterStyleLayerOverridesOnlyAttributesItNames()
		{
			KingdomStyleDraft earlier = new KingdomStyleDraft
			{
				Name = "glass", Terrain = "Glass", Region = "Dunes", Strata = "surface",
				Priority = "100", GroundClause = "old clause", Crop = "OldCrop",
				Seed = "OldSeed", CropRow = "OldRow", WallMaterial = "timber",
				TimberWall = "OldWall"
			};
			KingdomStyleDraft later = new KingdomStyleDraft
			{
				Name = "glass", Priority = "900", GroundClause = "", Crop = "NewCrop",
				TimberWall = ""
			};
			KingdomStyleDraft merged = KingdomStyleRules.Merge(earlier, later);
			Assert.AreEqual("Glass", merged.Terrain);
			Assert.AreEqual("Dunes", merged.Region);
			Assert.AreEqual("surface", merged.Strata);
			Assert.AreEqual("900", merged.Priority);
			Assert.AreEqual("", merged.GroundClause, "blank explicitly clears inherited prose");
			Assert.AreEqual("NewCrop", merged.Crop);
			Assert.AreEqual("OldSeed", merged.Seed);
			Assert.AreEqual("OldRow", merged.CropRow);
			Assert.AreEqual("timber", merged.WallMaterial);
			Assert.AreEqual("", merged.TimberWall,
				"blank explicitly clears inherited behaviour");
			Assert.IsTrue(KingdomStyleRules.TryParse(merged, out KingdomStyleDefinition parsed,
				out string error), error);
			Assert.IsNull(parsed.GroundClause);
			Assert.IsNull(parsed.TimberWallBlueprint);
		}

		[TestCase("glass", "Crop", "Seed", "Row", "adamant", "Wall")]
		[TestCase("glass", "Crop\nBad", "Seed", "Row", "timber", "Wall")]
		[TestCase("glass", "Crop", "Seed\nBad", "Row", "timber", "Wall")]
		[TestCase("glass", "Crop", "Seed", "Row\nBad", "timber", "Wall")]
		[TestCase("glass", "Crop", "Seed", "Row", "timber", "Wall\nBad")]
		[TestCase("glass", "Crop", "Seed", "Row", "timber\n", "Wall")]
		public void MalformedBehaviourDeclarationsFailLoudly(string name, string crop,
			string seed, string row, string material, string timberWall = "Wall")
		{
			Assert.IsFalse(KingdomStyleRules.TryParse(new KingdomStyleDraft
			{
				Name = name, Crop = crop, Seed = seed, CropRow = row,
				WallMaterial = material, TimberWall = timberWall
			}, out _, out string error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[TestCase("bad style", "Terrain", null, null, null)]
		[TestCase("!bad", "Terrain", null, null, null)]
		[TestCase("bad,style", "Terrain", null, null, null)]
		[TestCase("glass", "one,,two", null, null, null)]
		[TestCase("glass", "one\ntwo", null, null, null)]
		[TestCase("glass", "one", null, "sky", null)]
		[TestCase("glass", "one", null, "surface", "10001")]
		public void MalformedDeclarationsFailLoudly(string name, string terrain, string region,
			string strata, string priority)
		{
			Assert.IsFalse(KingdomStyleRules.TryParse(new KingdomStyleDraft
			{
				Name = name, Terrain = terrain, Region = region, Strata = strata,
				Priority = priority
			}, out _, out string error));
			Assert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void RuntimeFoundingAndDebugWishUseMergedRegistryNotClosedBaseArray()
		{
			string data = TestMain.ReadRepositoryText(Path.Combine("Core", "KingdomData.cs"));
			StringAssert.Contains("xml.GetAttribute(\"Terrain\")", data);
			StringAssert.Contains("xml.GetAttribute(\"Region\")", data);
			StringAssert.Contains("KingdomStyleRules.Resolve(_styleDefinitions", data);
			StringAssert.Contains("xml.GetAttribute(\"Crop\")", data);
			StringAssert.Contains("xml.GetAttribute(\"WallMaterial\")", data);
			StringAssert.Contains("KingdomStyleRules.CropForStyle(_styleDefinitions", data);
			StringAssert.Contains("GetBlueprintIfExists(names[i])", data);
			StringAssert.Contains("HasPart(\"r_KingdomSeed\")", data);
			StringAssert.Contains("InheritsFrom(\"Plant\")", data);
			StringAssert.Contains("GetPartParameter(\"Physics\", \"Solid\", false)", data);

			string founding = TestMain.ReadRepositoryText(Path.Combine("Core", "KingdomFounding.cs"));
			StringAssert.Contains("style = KingdomData.StyleForSite", founding);
			StringAssert.Contains("KingdomData.TryGetStyle(style", founding);

			string wishes = TestMain.ReadRepositoryText(Path.Combine("Debug", "KingdomWishes.cs"));
			StringAssert.Contains("KingdomData.TryGetStyle(style", wishes);
			StringAssert.Contains("string.Join(\", \", KingdomData.Styles)", wishes);
		}

		[Test]
		public void ShippedStyleDefinitionsCarryFoundingSelectorsAndProse()
		{
			string catalogue = TestMain.ReadRepositoryText("KingdomBuildings.xml");
			StringAssert.Contains("<style Name=\"verdant\" Terrain=", catalogue);
			StringAssert.Contains("<style Name=\"fungal\" Terrain=", catalogue);
			StringAssert.Contains("<style Name=\"gyre\" Terrain=", catalogue);
			StringAssert.Contains("<style Name=\"eater\" Terrain=", catalogue);
			StringAssert.Contains("GroundClause=", catalogue);
			StringAssert.Contains("Crop=\"Vinewafer\"", catalogue);
			StringAssert.Contains("WallMaterial=\"marble\"", catalogue);
			StringAssert.Contains("TimberWall=\"PlantWall\"", catalogue);
		}

		[Test]
		public void RuntimeCropAndMaterialConsumersUseOpenStyleRegistry()
		{
			string crops = TestMain.ReadRepositoryText(Path.Combine("Growth", "KingdomCrops.cs"));
			string growth = TestMain.ReadRepositoryText(Path.Combine("Growth", "KingdomGrowth.cs"));
			string materials = TestMain.ReadRepositoryText(Path.Combine("Growth", "KingdomMaterials.cs"));
			StringAssert.Contains("KingdomData.CropForStyle", crops);
			StringAssert.Contains("KingdomData.CropForSeed", crops);
			StringAssert.Contains("KingdomData.CropForStyle", growth);
			StringAssert.Contains("KingdomData.TryStyleWallMaterial", materials);
			StringAssert.Contains("KingdomData.TimberWallForStyle", materials);
		}
	}
}
#endif
