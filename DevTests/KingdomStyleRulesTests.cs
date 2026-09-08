#if TAF_TESTS
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomStyleRulesTests
	{
		private static void AssertPublicDto(System.Type Type, string[] Names,
			System.Type[] Types)
		{
			ClassicAssert.IsTrue(Type.IsPublic);
			ClassicAssert.IsTrue(Type.IsSealed);
			ClassicAssert.IsNotNull(Type.GetConstructor(System.Type.EmptyTypes));
			System.Reflection.FieldInfo[] fields = Type.GetFields();
			ClassicAssert.AreEqual(Names.Length, fields.Length);
			for (int i = 0; i < Names.Length; i++)
			{
				System.Reflection.FieldInfo field = Type.GetField(Names[i]);
				ClassicAssert.IsNotNull(field, Names[i]);
				ClassicAssert.AreEqual(Types[i], field.FieldType, Names[i]);
				ClassicAssert.IsTrue(field.IsPublic, Names[i]);
				ClassicAssert.IsFalse(field.IsInitOnly, Names[i]);
			}
		}

		[Test]
		public void StyleDeclarationsKeepExactPublicAbi()
		{
			ClassicAssert.AreEqual(typeof(byte), System.Enum.GetUnderlyingType(typeof(KingdomStyleStratum)));
			ClassicAssert.AreEqual(0, (byte)KingdomStyleStratum.Any);
			ClassicAssert.AreEqual(1, (byte)KingdomStyleStratum.Surface);
			ClassicAssert.AreEqual(2, (byte)KingdomStyleStratum.Deep);
			AssertPublicDto(typeof(KingdomStyleDraft), new string[] { "Name", "Aliases", "Terrain", "Region",
				"Strata", "Priority", "GroundClause", "Crop", "Seed", "CropRow",
				"WallMaterial", "TimberWall" }, new System.Type[] { typeof(string), typeof(string),
				typeof(string), typeof(string), typeof(string), typeof(string), typeof(string),
				typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) });
			AssertPublicDto(typeof(KingdomStyleDefinition), new string[] { "Name", "Aliases", "TerrainTokens",
				"RegionTokens", "Stratum", "Priority", "GroundClause", "CropBlueprint",
				"SeedBlueprint", "CropRowBlueprint", "HasWallMaterial", "WallMaterial",
				"TimberWallBlueprint" }, new System.Type[] { typeof(string), typeof(string[]), typeof(string[]),
				typeof(string[]), typeof(KingdomStyleStratum), typeof(int), typeof(string),
				typeof(string), typeof(string), typeof(string), typeof(bool),
				typeof(KingdomMaterial), typeof(string) });
		}

		private static KingdomStyleDefinition Style(string name, string terrain = null,
			string region = null, string strata = null, string priority = null,
			string clause = null, string crop = null, string seed = null, string row = null,
			string wallMaterial = null, string timberWall = null, string aliases = null)
		{
			KingdomStyleDraft draft = new KingdomStyleDraft
			{
				Name = name, Aliases = aliases, Terrain = terrain, Region = region, Strata = strata,
				Priority = priority, GroundClause = clause, Crop = crop, Seed = seed,
				CropRow = row, WallMaterial = wallMaterial, TimberWall = timberWall
			};
			ClassicAssert.IsTrue(KingdomStyleRules.TryParse(draft, out KingdomStyleDefinition result,
				out string error), error);
			return result;
		}

		[Test]
		public void AliasesCanonicalizeMergeTagsAndSaveMigrationWithoutCreatingASecondStyle()
		{
			List<KingdomStyleDefinition> definitions = new List<KingdomStyleDefinition>
			{
				Style("common"), Style("moonstair", aliases: "gyre,stair")
			};
			ClassicAssert.IsTrue(KingdomStyleRules.TryCanonical(definitions, "GYRE", out string canonical));
			ClassicAssert.AreEqual("moonstair", canonical);
			CollectionAssert.AreEqual(new string[] { "moonstair", "gyre", "stair" },
				KingdomStyleRules.KeysFor(definitions, "gyre"));
			ClassicAssert.IsTrue(KingdomStyleRules.TagAccepts(definitions, "gyre", "moonstair"));
			ClassicAssert.IsTrue(KingdomStyleRules.TagAccepts(definitions, "moonstair", "gyre"));
			ClassicAssert.IsFalse(KingdomStyleRules.TagAccepts(definitions, "all,!gyre", "moonstair"));
			ClassicAssert.AreEqual("moonstair", KingdomStyleRules.MigrateLegacyKey("GYRE"));
			ClassicAssert.AreEqual("third-party", KingdomStyleRules.MigrateLegacyKey("third-party"));

			KingdomStyleDefinition collision = Style("other", aliases: "moonstair");
			ClassicAssert.IsFalse(KingdomStyleRules.TryValidateBehavior(definitions, collision, -1,
				out string collisionError));
			StringAssert.Contains("canonical name or alias", collisionError);
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

			ClassicAssert.AreEqual("Congealed love", KingdomStyleRules.CropForStyle(definitions, "GLASS"));
			ClassicAssert.AreEqual("GlassSeed", KingdomStyleRules.SeedForStyle(definitions, "glass"));
			ClassicAssert.AreEqual("GlassRow", KingdomStyleRules.CropRowForStyle(definitions, "glass"));
			ClassicAssert.AreEqual("Congealed love", KingdomStyleRules.CropForSeed(definitions, "GlassSeed"));
			ClassicAssert.AreEqual("GlassSeed", KingdomStyleRules.SeedForCrop(definitions, "Congealed love"));
			ClassicAssert.AreEqual("GlassRow", KingdomStyleRules.RowForCrop(definitions, "Congealed love"));
			ClassicAssert.IsTrue(KingdomStyleRules.TryWallMaterial(definitions, "glass",
				out KingdomMaterial material));
			ClassicAssert.AreEqual(KingdomMaterial.ShapedStone, material);
			ClassicAssert.AreEqual("GlassWall", KingdomStyleRules.TimberWallForStyle(definitions, "glass"));
			ClassicAssert.AreEqual("Starapple", KingdomStyleRules.CropForStyle(definitions, "old-style"));
			ClassicAssert.AreEqual("CommonWall", KingdomStyleRules.TimberWallForStyle(definitions, "old-style"));
		}

		[Test]
		public void CropBehaviourIsAtomicAndReverseMappingsCannotConflict()
		{
			ClassicAssert.IsTrue(KingdomStyleRules.TryParse(new KingdomStyleDraft
			{
				Name = "partial", Crop = "CropOnly"
			}, out KingdomStyleDefinition partial, out string parseError), parseError);
			ClassicAssert.IsFalse(KingdomStyleRules.TryValidateBehavior(null, partial, -1,
				out string partialError));
			StringAssert.Contains("Crop, Seed, and CropRow together", partialError);

			List<KingdomStyleDefinition> definitions = new List<KingdomStyleDefinition>
			{
				Style("first", crop: "FirstCrop", seed: "SharedSeed", row: "FirstRow")
			};
			KingdomStyleDefinition conflict = Style("second", crop: "SecondCrop",
				seed: "SharedSeed", row: "SecondRow");
			ClassicAssert.IsFalse(KingdomStyleRules.TryValidateBehavior(definitions, conflict, -1,
				out string conflictError));
			StringAssert.Contains("different crop", conflictError);

			KingdomStyleDefinition shared = Style("third", crop: "FirstCrop",
				seed: "SharedSeed", row: "FirstRow");
			ClassicAssert.IsTrue(KingdomStyleRules.TryValidateBehavior(definitions, shared, -1,
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
			ClassicAssert.AreEqual("glass", KingdomStyleRules.Resolve(definitions,
				"TerrainGlassDunes", "Desert", 10, 10));
			ClassicAssert.IsTrue(KingdomStyleRules.TryCanonical(definitions, " GLASS ",
				out string canonical));
			ClassicAssert.AreEqual("glass", canonical);
			ClassicAssert.AreEqual("ground bright enough to found a glass city",
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
			ClassicAssert.AreEqual("ruin", KingdomStyleRules.Resolve(definitions,
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
			ClassicAssert.AreEqual("high", KingdomStyleRules.Resolve(definitions,
				"TerrainRuins", null, 10, 10));
			definitions.RemoveAt(3);
			ClassicAssert.AreEqual("first", KingdomStyleRules.Resolve(definitions,
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
			ClassicAssert.AreEqual("canopy", KingdomStyleRules.Resolve(definitions,
				"TerrainJungle", null, 10, 10));
			ClassicAssert.AreEqual("root", KingdomStyleRules.Resolve(definitions,
				"TerrainJungle", null, 11, 10));
		}

		[Test]
		public void NoSelectorStyleIsStillForceableButNeverHijacksFounding()
		{
			List<KingdomStyleDefinition> definitions = new List<KingdomStyleDefinition>
			{
				Style("common"), Style("glass")
			};
			ClassicAssert.AreEqual("common", KingdomStyleRules.Resolve(definitions,
				"TerrainUnknown", "Unknown", 10, 10));
			ClassicAssert.IsTrue(KingdomStyleRules.TryCanonical(definitions, "glass", out _));
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
			ClassicAssert.AreEqual("Glass", merged.Terrain);
			ClassicAssert.AreEqual("Dunes", merged.Region);
			ClassicAssert.AreEqual("surface", merged.Strata);
			ClassicAssert.AreEqual("900", merged.Priority);
			ClassicAssert.AreEqual("", merged.GroundClause, "blank explicitly clears inherited prose");
			ClassicAssert.AreEqual("NewCrop", merged.Crop);
			ClassicAssert.AreEqual("OldSeed", merged.Seed);
			ClassicAssert.AreEqual("OldRow", merged.CropRow);
			ClassicAssert.AreEqual("timber", merged.WallMaterial);
			ClassicAssert.AreEqual("", merged.TimberWall,
				"blank explicitly clears inherited behaviour");
			ClassicAssert.IsTrue(KingdomStyleRules.TryParse(merged, out KingdomStyleDefinition parsed,
				out string error), error);
			ClassicAssert.IsNull(parsed.GroundClause);
			ClassicAssert.IsNull(parsed.TimberWallBlueprint);
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
			ClassicAssert.IsFalse(KingdomStyleRules.TryParse(new KingdomStyleDraft
			{
				Name = name, Crop = crop, Seed = seed, CropRow = row,
				WallMaterial = material, TimberWall = timberWall
			}, out _, out string error));
			ClassicAssert.IsFalse(string.IsNullOrEmpty(error));
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
			ClassicAssert.IsFalse(KingdomStyleRules.TryParse(new KingdomStyleDraft
			{
				Name = name, Terrain = terrain, Region = region, Strata = strata,
				Priority = priority
			}, out _, out string error));
			ClassicAssert.IsFalse(string.IsNullOrEmpty(error));
		}

		[Test]
		public void RuntimeFoundingAndDebugWishUseMergedRegistryNotClosedBaseArray()
		{
			string data = KingdomDataLogicalSource.Read();
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

			string founding = KingdomFoundingLogicalSource.Read();
			StringAssert.Contains("style = KingdomData.StyleForSite", founding);
			StringAssert.Contains("KingdomData.TryGetStyle(style", founding);

			string wishes = TestMain.ReadRepositoryText(Path.Combine("Debug",
				"KingdomWishes.StyleRegardAndExile.cs"));
			StringAssert.Contains("KingdomData.TryGetStyle(style", wishes);
			StringAssert.Contains("string.Join(\", \", KingdomData.Styles)", wishes);
		}

		[Test]
		public void ShippedStyleDefinitionsCarryFoundingSelectorsAndProse()
		{
			string catalogue = TestMain.ReadRepositoryText(Path.Combine("RuntimeData",
				"KingdomBuildings.xml"));
			StringAssert.Contains("<style Name=\"verdant\" Terrain=", catalogue);
			StringAssert.Contains("<style Name=\"fungal\" Terrain=", catalogue);
			StringAssert.Contains("<style Name=\"moonstair\" Aliases=\"gyre\" Terrain=", catalogue);
			StringAssert.Contains("<style Name=\"eater\" Terrain=", catalogue);
			StringAssert.Contains("GroundClause=", catalogue);
			StringAssert.Contains("Crop=\"Vinewafer\"", catalogue);
			StringAssert.Contains("Crop=\"Bundle of Noisegrass\"", catalogue);
			StringAssert.Contains("WallMaterial=\"marble\"", catalogue);
			StringAssert.Contains("TimberWall=\"PlantWall\"", catalogue);
		}

		[Test]
		public void MoonStairGroundNeverInventsGyreOrGirshAllegiance()
		{
			XDocument catalogue = XDocument.Parse(TestMain.ReadRepositoryText(
				Path.Combine("RuntimeData", "KingdomBuildings.xml")));
			XElement[] styles = catalogue.Root.Elements("style").ToArray();
			CollectionAssert.AreEqual(new string[]
			{
				"common", "verdant", "fungal", "moonstair", "eater"
			}, styles.Select(e => (string)e.Attribute("Name")).ToArray());
			XElement stair = styles.Single(e => (string)e.Attribute("Name") == "moonstair");
			ClassicAssert.AreEqual("gyre", (string)stair.Attribute("Aliases"));
			ClassicAssert.AreEqual("Bundle of Noisegrass", (string)stair.Attribute("Crop"));
			ClassicAssert.AreEqual("r_KingdomSeedNoisegrass", (string)stair.Attribute("Seed"));
			ClassicAssert.AreEqual("r_KingdomRowNoisegrass", (string)stair.Attribute("CropRow"));
			ClassicAssert.IsFalse(styles.Any(e => (string)e.Attribute("Crop") == "Godshroom Cap"));

			XElement[] environmental = catalogue.Descendants("building")
				.Where(e => (string)e.Attribute("Styles") == "moonstair").ToArray();
			CollectionAssert.AreEquivalent(new string[] { "bonefold", "sacramentcourt" },
				environmental.Select(e => (string)e.Attribute("Key")).ToArray());
			XElement crystalCourt = environmental.Single(e =>
				(string)e.Attribute("Key") == "sacramentcourt");
			ClassicAssert.AreEqual("civic", (string)crystalCourt.Attribute("Category"));
			ClassicAssert.AreEqual("market,none", (string)crystalCourt.Attribute("Districts"));
			foreach (XElement building in environmental)
			{
				string display = ((string)building.Attribute("DisplayName") ?? "").ToLowerInvariant();
				StringAssert.DoesNotContain("gyre", display);
				StringAssert.DoesNotContain("girsh", display);
				StringAssert.DoesNotContain("sacrament", display);
				ClassicAssert.IsNull(building.Attribute("Creed"));
				ClassicAssert.IsNull(building.Attribute("Builders"));
			}
			foreach (string key in new string[] { "girshrotchapel", "gyrewightashcourt" })
			{
				XElement creedWork = catalogue.Descendants("building").Single(e =>
					(string)e.Attribute("Key") == key);
				ClassicAssert.AreEqual("all", (string)creedWork.Attribute("Styles"));
				StringAssert.StartsWith("creed:", (string)creedWork.Attribute("Builders"));
				ClassicAssert.IsNotEmpty((string)creedWork.Attribute("Creed"));
			}
			ClassicAssert.AreEqual("all", (string)catalogue.Descendants("building").Single(e =>
				(string)e.Attribute("Key") == "bazaar").Attribute("Styles"));
			ClassicAssert.AreEqual("all", (string)catalogue.Descendants("building").Single(e =>
				(string)e.Attribute("Key") == "bathhouse").Attribute("Styles"));

			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText(
				Path.Combine("RuntimeData", "ObjectBlueprints.xml")));
			XElement mill = objects.Descendants("object").Single(e =>
				(string)e.Attribute("Name") == "r_KingdomGrindMill");
			ClassicAssert.IsNotNull(objects.Descendants("object").SingleOrDefault(e =>
				(string)e.Attribute("Name") == "r_KingdomMoonStairCrystalRoot"));
			ClassicAssert.IsNotNull(objects.Descendants("object").SingleOrDefault(e =>
				(string)e.Attribute("Name") == "r_KingdomStructureMoonStairCrystalRib"));
			string architecture = TestMain.ReadRepositoryText(Path.Combine(
				"Architecture", "KingdomArchitectures-HousingWater.xml"));
			StringAssert.Contains("structure:crystal-rib", architecture);
			StringAssert.Contains("surface:marble-sill", architecture);
			string[] transformations = ((string)mill.Elements("part").Single(e =>
				(string)e.Attribute("Name") == "Mill").Attribute("Transformations")).Split(',');
			CollectionAssert.Contains(transformations, "Bundle of Noisegrass");
			CollectionAssert.Contains(transformations,
				"Godshroom Cap:r_KingdomGodshroomPickle");
			CollectionAssert.Contains(transformations,
				"Dreadroot Tuber:r_KingdomDreadrootMash");
		}

		[Test]
		public void RuntimeCropAndMaterialConsumersUseOpenStyleRegistry()
		{
			string crops = KingdomCropsLogicalSource.Read();
			string growth = KingdomGrowthLogicalSource.Read();
			string materials = KingdomMaterialsLogicalSource.Read();
			StringAssert.Contains("KingdomData.CropForStyle", crops);
			StringAssert.Contains("KingdomData.CropForSeed", crops);
			StringAssert.Contains("KingdomData.CropForStyle", growth);
			StringAssert.Contains("KingdomData.TryStyleWallMaterial", materials);
			StringAssert.Contains("KingdomData.TimberWallForStyle", materials);
		}
	}
}
#endif
