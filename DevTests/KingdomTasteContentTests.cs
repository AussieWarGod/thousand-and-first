#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomTasteContentTests
	{
		[TestCase("r_KingdomFounderStatue", "223")]
		[TestCase("r_KingdomWatchtower", "024")]
		[TestCase("r_KingdomHideRack", "209")]
		[TestCase("r_KingdomMud", ",")]
		public void SemanticAliasesUseHonestGlyphsInsteadOfUnrelatedVanillaTiles(
			string name, string glyph)
		{
			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement render = Part(Blueprint(objects, name), "Render");
			Assert.AreEqual(glyph, (string)render.Attribute("RenderString"));
			Assert.IsNull(render.Attribute("Tile"));
		}

		[Test]
		public void InertSpectrumRailBorrowsTheNativeTechlightSilhouetteOnly()
		{
			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement lamp = Blueprint(objects, "r_KingdomArcologySpectrumLamp");
			Assert.AreEqual("items/sw_hitech_lightsource1.bmp",
				(string)Part(lamp, "Render").Attribute("Tile"));
			Assert.IsNull(lamp.Elements("part").SingleOrDefault(e =>
				(string)e.Attribute("Name") == "LightSource"));
		}

		[Test]
		public void CreedRackStoresOnlyRealDepositsAndLivingTrunksBlockMovement()
		{
			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement rack = Blueprint(objects, "r_KingdomCreedWeaponRack");
			Assert.IsNotNull(Part(rack, "Container"));
			Assert.IsNotNull(Part(rack, "Inventory"));
			Assert.IsFalse(rack.Elements("inventoryobject").Any());
			Assert.IsFalse(rack.Elements("tag").Any(e =>
				(string)e.Attribute("Name") == "InventoryPopulationTable"));
			Assert.IsFalse(rack.Elements("part").Any(e =>
				(string)e.Attribute("Name") == "Commerce"));
			XElement practice = Blueprint(objects, "r_KingdomCreedPracticeArmsRack");
			Assert.IsFalse(practice.Elements("part").Any(e =>
				(string)e.Attribute("Name") == "Container"
					|| (string)e.Attribute("Name") == "Inventory"));

			XElement trunk = Blueprint(objects, "r_KingdomCreedLivingTrunk");
			Assert.AreEqual("true", (string)Part(trunk, "Physics").Attribute("Solid"));
			XDocument architecture = XDocument.Parse(TestMain.ReadRepositoryText(
				"Architecture/KingdomArchitectures-Creeds.xml"));
			XElement school = architecture.Descendants("map").Single(e =>
				(string)e.Attribute("Key") == "creed-chavvah-school-s0");
			XElement living = school.Elements("glyph").Single(e =>
				(string)e.Attribute("Char") == "t");
			Assert.AreEqual("adjacent", (string)living.Attribute("Pass"));
		}

		[TestCase("r_KingdomSnapjawTrailDen", "127")]
		[TestCase("r_KingdomIssachariRiflePorch", "239")]
		[TestCase("r_KingdomTemplarPurityArsenal", "239")]
		[TestCase("r_KingdomWardensWatchLodge", "127")]
		public void PassableCreedRootsNeverInheritTheRockWallTile(string name, string glyph)
		{
			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement profile = Blueprint(objects, "r_KingdomOpenCreedFurnitureProfile");
			Assert.IsFalse(profile.Elements("part").Any(e =>
				(string)e.Attribute("Name") == "Render" && e.Attribute("Tile") != null));
			XElement root = Blueprint(objects, name);
			Assert.AreEqual("r_KingdomOpenCreedFurnitureProfile",
				(string)root.Attribute("Inherits"));
			XElement render = Part(root, "Render");
			Assert.AreEqual(glyph, (string)render.Attribute("RenderString"));
			Assert.IsNull(render.Attribute("Tile"));
		}

		[Test]
		public void EveryRoadWearStageOwnsADifferentNativeSilhouette()
		{
			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			string[] names =
			{
				"r_KingdomGroundWornTrack",
				"r_KingdomGroundTroddenTrack",
				"r_KingdomGroundTroddenPath"
			};
			string[] tiles = names.Select(name =>
			{
				XElement blueprint = Blueprint(objects, name);
				string tile = (string)Part(blueprint, "Render").Attribute("Tile");
				return tile ?? "terrain/sw_arena_floor.bmp";
			}).ToArray();
			Assert.AreEqual(names.Length,
				tiles.Distinct(StringComparer.OrdinalIgnoreCase).Count());
		}

		[Test]
		public void DirtRenderedGroundNeverClaimsToBeStone()
		{
			string[] files =
			{
				"Architecture/KingdomArchitectures-Creeds.xml",
				"Architecture/KingdomArchitectures-DeepEndgame.xml",
				"Architecture/KingdomArchitectures-PurposePortfolio.xml",
				"Architecture/KingdomArchitectures-ReopenedExotics.xml"
			};
			foreach (string file in files)
			{
				XDocument architecture = XDocument.Parse(TestMain.ReadRepositoryText(file));
				XElement[] falseStone = architecture.Descendants("slot").Where(slot =>
					new[] { "DirtFloor", "DirtPath", "DirtRoad" }.Contains(
						(string)slot.Attribute("Blueprint"))
					&& (string)slot.Attribute("Material") == "stone").ToArray();
				Assert.IsEmpty(falseStone, file);
			}

			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement dust = Blueprint(objects, "r_KingdomGroundStoneDust");
			Assert.AreEqual("cut-stone dust", (string)Part(dust, "Render").Attribute("DisplayName"));
			Assert.AreEqual("Terrain/sw_ground_dots3.png",
				(string)Part(dust, "Render").Attribute("Tile"));
		}

		[Test]
		public void CreedFoodStoresAreUsableEmptyAndCategoryHonest()
		{
			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			var expected = new Dictionary<string, string>
			{
				{ "r_KingdomCreedJoppaSeedBin", "Items/sw_basket.bmp" },
				{ "r_KingdomCreedKyakukyaSpiceJar", "Items/sw_vase.bmp" },
				{ "r_KingdomCreedSnapjawMeatCache", "Items/sw_basket.bmp" },
				{ "r_KingdomCreedFarmersLabelledBin", "Assets_Content_Textures_Tiles_sw_chest.bmp" }
			};
			foreach (var item in expected)
			{
				XElement store = Blueprint(objects, item.Key);
				Assert.AreEqual("r_KingdomFixtureBasketEmpty",
					(string)store.Attribute("Inherits"), item.Key);
				Assert.AreEqual(item.Value, (string)Part(store, "Render").Attribute("Tile"),
					item.Key);
				Assert.AreEqual(item.Value, (string)store.Elements("tag").Single(e =>
					(string)e.Attribute("Name") == "EmptyTile").Attribute("Value"), item.Key);
				Assert.AreEqual("*delete", (string)store.Elements("tag").Single(e =>
					(string)e.Attribute("Name") == "InventoryPopulationTable").Attribute("Value"),
					item.Key);
				Assert.IsFalse(store.Elements("inventoryobject").Any(), item.Key);
				Assert.IsFalse(store.Elements("part").Any(e =>
					(string)e.Attribute("Name") == "Commerce"
					|| (string)e.Attribute("Name") == "LiquidVolume"), item.Key);
			}
			Assert.AreEqual(expected.Count - 1, expected.Values.Distinct(
				StringComparer.OrdinalIgnoreCase).Count());
			Assert.AreNotEqual(
				(string)Part(Blueprint(objects, "r_KingdomCreedJoppaSeedBin"), "Render")
					.Attribute("TileColor"),
				(string)Part(Blueprint(objects, "r_KingdomCreedSnapjawMeatCache"), "Render")
					.Attribute("TileColor"));

			XDocument architecture = XDocument.Parse(TestMain.ReadRepositoryText(
				"Architecture/KingdomArchitectures-Creeds.xml"));
			string[] references = { "$seedbin", "$spicejar", "$meatcache", "$labelledbin" };
			foreach (string reference in references)
				Assert.AreEqual(1, architecture.Descendants("glyph").Count(e =>
					(string)e.Attribute("Object") == reference), reference);
		}

		[Test]
		public void MemorialsCarryInspectableLoreWithoutBorrowingAStaticVillageSecret()
		{
			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			string[] names = { "r_KingdomCairn", "r_KingdomGraveGrove", "r_KingdomNicheTomb",
				"r_KingdomCragmenschStoneGarden" };
			string[] lore = names.Select(name =>
			{
				XElement memorial = Blueprint(objects, name);
				Assert.IsNotNull(Part(memorial, "SmartuseLooks"), name);
				Assert.IsNotNull(Part(memorial, "Interesting"), name);
				Assert.IsFalse(memorial.Elements("part").Any(e =>
					(string)e.Attribute("Name") == "RevealVillageHistoryOnLook"), name);
				Assert.IsFalse(memorial.Elements("part").Any(e =>
					(string)e.Attribute("Name") == "Container"
					|| (string)e.Attribute("Name") == "Inventory"), name);
				return (string)Part(memorial, "RulesDescription").Attribute("Text");
			}).ToArray();
			Assert.IsTrue(lore.All(text => !string.IsNullOrWhiteSpace(text)));
			Assert.AreEqual(names.Length, lore.Distinct(StringComparer.Ordinal).Count());
		}

		[Test]
		public void ReliquaryCaseAndRelicAreDifferentInertThings()
		{
			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement relicCase = Blueprint(objects, "r_KingdomFixtureRelicCaseScrap");
			XElement relic = Blueprint(objects, "r_KingdomFixtureMachineRelic");
			Assert.AreNotEqual((string)Part(relicCase, "Render").Attribute("Tile"),
				(string)Part(relic, "Render").Attribute("Tile"));
			string[] active = { "Container", "Inventory", "Commerce", "ElectricalPowerTransmission",
				"ElectricalPowerGenerator", "GreatMachine", "QuestManager" };
			foreach (XElement item in new XElement[] { relicCase, relic })
				Assert.IsFalse(item.Elements("part").Any(e => active.Contains(
					(string)e.Attribute("Name"))), (string)item.Attribute("Name"));

			XDocument architecture = XDocument.Parse(TestMain.ReadRepositoryText(
				"Architecture/KingdomArchitectures-DeepEndgame.xml"));
			XElement palette = architecture.Descendants("palette").Single(e =>
				(string)e.Attribute("Key") == "deepend-reliquary-salvage");
			string caseBlueprint = (string)palette.Elements("slot").Single(e =>
				(string)e.Attribute("Role") == "recovered-relic-case").Attribute("Blueprint");
			string relicBlueprint = (string)palette.Elements("slot").Single(e =>
				(string)e.Attribute("Role") == "retained-machine-relic").Attribute("Blueprint");
			Assert.AreEqual("r_KingdomFixtureRelicCaseScrap", caseBlueprint);
			Assert.AreEqual("r_KingdomFixtureMachineRelic", relicBlueprint);
		}

		[Test]
		public void CreedFramesScreensAndGrownCellArePhysicalContracts()
		{
			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XDocument architecture = XDocument.Parse(TestMain.ReadRepositoryText(
				"Architecture/KingdomArchitectures-Creeds.xml"));

			AssertAnchorCount(architecture, "creed-baetyl-frame-s0",
				"frame:measured-gantry", 4);
			AssertAnchorCount(architecture, "creed-dromad-shade-s0",
				"frame:travelling-awning", 4);
			AssertAnchorCount(architecture, "creed-gyre-ashcourt-s0",
				"screen:bone-chitin-votive", 4);
			AssertGlyphCount(architecture, "creed-chavvah-school-s0", "B", 14);

			XElement ossuary = Blueprint(objects, "r_KingdomStructureGyreOssuaryScreen");
			Assert.AreEqual("BaseWallBone", (string)ossuary.Attribute("Inherits"));
			Assert.IsNull(Part(ossuary, "Render").Attribute("Tile"));
			XElement bough = Blueprint(objects, "r_KingdomStructureChavvahTrunk");
			Assert.AreEqual("ChavvahTrunk", (string)bough.Attribute("Inherits"));
			Assert.IsNull(Part(bough, "Render").Attribute("Tile"));

			XElement pennon = Blueprint(objects, "r_KingdomCreedGoatfolkChallengePennon");
			Assert.AreEqual("Items/sw_banner.bmp", (string)Part(pennon, "Render").Attribute("Tile"));
			Assert.IsFalse(pennon.Elements("part").Any(e =>
				(string)e.Attribute("Name") == "Container"
				|| (string)e.Attribute("Name") == "Inventory"));
			string generator = TestMain.ReadRepositoryText(
				Path.Combine("Tools", "generate-lot-realizations.py"));
			StringAssert.Contains("\"$hornpost\": \"$goatpennon\"", generator);
			StringAssert.DoesNotContain("\"$hornpost\": \"$hornpost\"", generator);
		}

		[Test]
		public void MechanimistsUseOneProcessionalReliquaryNotASmallProxy()
		{
			XDocument catalogue = XDocument.Parse(TestMain.ReadRepositoryText("KingdomBuildings.xml"));
			XElement[] works = catalogue.Descendants("building").Where(e =>
				(string)e.Attribute("Creed") == "Mechanimists").ToArray();
			Assert.AreEqual(1, works.Length);
			Assert.AreEqual("reliquary", (string)works[0].Attribute("Key"));
			Assert.AreEqual("L", (string)works[0].Attribute("Plot"));
			Assert.AreEqual("Town", (string)works[0].Attribute("MinStage"));
			Assert.AreEqual("workshop", (string)works[0].Attribute("MinTech"));
		}

		[Test]
		public void RuntimeFactionsUseAStableGlyphInsteadOfJoppaTerrainAuthority()
		{
			string founding = TestMain.ReadRepositoryText(
				"Core/KingdomFounding.00.FirstFoundingRegistration.cs");
			string polity = TestMain.ReadRepositoryText(
				"Polity/KingdomPolityFactionRuntime.cs");
			string source = founding + polity;
			StringAssert.DoesNotContain("SetVillageFactionEmblem", source);
			StringAssert.DoesNotContain("sw_joppa.bmp", source);
			StringAssert.Contains("KingdomFactionEmblemPresentation.TryApply", source);
			StringAssert.Contains("Stat.GetSeededRandomGenerator(Seed)", founding);
			StringAssert.Contains("Crayons.GetRandomColorExcept", founding);
			StringAssert.Contains("emblem.Tile = null", founding);
			StringAssert.Contains("emblem.RenderString = Glyph", founding);
			StringAssert.Contains("candidate[0] == foreground[0]", founding);
			StringAssert.Contains(
				"KingdomFactionEmblemPresentation.TryApply(F, F.Name)", polity);
		}

		[Test]
		public void HindrenStitcherUsesOrdinaryTextileMachineNotNachamsWireExtruder()
		{
			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement fixture = Blueprint(objects, "r_KingdomHindrenLoomSemantic");
			XElement render = Part(fixture, "Render");
			Assert.AreEqual("Hindren treadle stitcher",
				(string)render.Attribute("DisplayName"));
			Assert.AreEqual("Items/sw_sewing_machine.bmp", (string)render.Attribute("Tile"));
			StringAssert.DoesNotContain("Nacham", fixture.ToString());
			StringAssert.Contains("needle and bobbin", fixture.ToString());
		}

		private static void AssertAnchorCount(XDocument architecture, string mapKey,
			string anchor, int expected)
		{
			XElement map = architecture.Descendants("map").Single(e =>
				(string)e.Attribute("Key") == mapKey);
			XElement glyph = map.Elements("glyph").Single(e =>
				((string)e.Attribute("Anchors") ?? "").Split(',').Contains(anchor));
			char character = ((string)glyph.Attribute("Char"))[0];
			int count = map.Elements("row").Sum(row =>
				((string)row.Attribute("Cells")).Count(value => value == character));
			Assert.AreEqual(expected, count, mapKey + " " + anchor);
		}

		private static void AssertGlyphCount(XDocument architecture, string mapKey,
			string glyph, int expected)
		{
			XElement map = architecture.Descendants("map").Single(e =>
				(string)e.Attribute("Key") == mapKey);
			char character = glyph[0];
			int count = map.Elements("row").Sum(row =>
				((string)row.Attribute("Cells")).Count(value => value == character));
			Assert.AreEqual(expected, count, mapKey + " " + glyph);
		}

		private static XElement Blueprint(XDocument document, string name)
		{
			return document.Descendants("object").Single(e =>
				(string)e.Attribute("Name") == name);
		}

		private static XElement Part(XElement blueprint, string name)
		{
			return blueprint.Elements("part").Single(e =>
				(string)e.Attribute("Name") == name);
		}
	}
}
#endif
