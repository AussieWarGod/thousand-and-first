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
	public class KingdomCreedContentTests
	{
		private static readonly string[] ShippedCreeds = new string[]
		{
			"Baetyls", "Barathrumites", "Chavvah", "Consortium", "Cragmensch",
			"Daughters", "Dromad", "Entropic", "Ezra", "Farmers", "Girsh", "Goatfolk",
			"Gyre Wights", "Hindren", "Issachari", "Joppa", "Kyakukya", "Mamon",
			"Mechanimists", "Merchants", "Mopango", "Naphtaali", "Resheph", "Robots",
			"Seekers", "Snapjaws", "Strangers", "Svardym", "Templar", "Trolls", "Wardens",
			"Water", "YdFreehold"
		};

		[TestCase("Joppa", true, false, false, 1, true, true)]
		[TestCase("Robots", true, false, true, 5, false, true)]
		[TestCase("birds", true, false, true, 0, false, false)]
		[TestCase("SultanCult7", true, false, false, 6, true, false)]
		[TestCase("Playerhater", true, true, false, 6, true, false)]
		[TestCase("hidden", false, false, false, 6, true, false)]
		public void AdmissionIsDerivedFromOpenFactionFacts(string name, bool visible,
			bool hates, bool old, int significance, bool article, bool expected)
		{
			Assert.AreEqual(expected, KingdomCreedContentRules.CanBeCreed(
				new CreedFactionFacts(name, visible, hates, old, significance, article), 3));
		}

		[Test]
		public void Installed21151CensusIsAnExactThirtyThreeAndChiliadAddsNone()
		{
			string root = LocateBase();
			string[] admitted = ReadAdmitted(Path.Combine(root, "Factions.xml"));
			CollectionAssert.AreEqual(ShippedCreeds, admitted);
			Assert.AreEqual(33, admitted.Length);
			Assert.AreEqual(0, ReadAdmitted(Path.Combine(root, "ChiliadFactions.xml")).Length);
		}

		[Test]
		public void EveryShippedCreedOwnsBehaviorBearingCatalogueContent()
		{
			XDocument catalogue = XDocument.Parse(TestMain.ReadRepositoryText("KingdomBuildings.xml"));
			XElement[] creedWorks = catalogue.Descendants("building")
				.Where(e => e.Attribute("Creed") != null).ToArray();
			string[] covered = creedWorks.Select(e => (string)e.Attribute("Creed"))
				.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
			CollectionAssert.AreEqual(ShippedCreeds, covered);
			Assert.AreEqual(34, creedWorks.Length,
				"33 admitted creeds plus the reviewed robot service-bay successor");
			CollectionAssert.AreEquivalent(new string[] { "robotchargebay", "robotservicebay" },
				creedWorks.Where(e => (string)e.Attribute("Creed") == "Robots")
					.Select(e => (string)e.Attribute("Key")).ToArray(),
				"Robots alone carry the second built-in creed tier");
			foreach (XElement work in creedWorks)
			{
				string creed = (string)work.Attribute("Creed");
				Assert.IsFalse(string.IsNullOrWhiteSpace((string)work.Attribute("Builders")),
					creed + " must name its ordinary provenance gate");
				Assert.IsTrue(work.Attribute("Carries") != null
					|| Positive(work, "Defence"), creed + " content must change simulation");
				Assert.IsNotNull(work.Attribute("Materials"), creed + " must pay a material-truth bill");
				Assert.IsNotNull(work.Attribute("Plot"), creed + " must own spatial architecture");
			}
		}

		[Test]
		public void WaterBaronGaugeHouseStoresMeasuredWaterButDoesNotMintIt()
		{
			XDocument catalogue = XDocument.Parse(TestMain.ReadRepositoryText("KingdomBuildings.xml"));
			XElement work = catalogue.Descendants("building").Single(e =>
				(string)e.Attribute("Key") == "waterbaronsgaugehouse");
			string carries = (string)work.Attribute("Carries") ?? "";
			StringAssert.DoesNotContain("water:", carries);
			StringAssert.Contains("order:2", carries);

			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement blueprint = objects.Descendants("object").Single(e =>
				(string)e.Attribute("Name") == "r_KingdomWaterBaronsGaugeHouse");
			Assert.AreEqual("r_KingdomCaskRack", (string)blueprint.Attribute("Inherits"));
		}

		[Test]
		public void CreedFoodWorksExposeThePhysicalMechanismTheyClaim()
		{
			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			AssertFoodMechanism(objects, "r_KingdomKyakukyaSpiceHearth",
				"r_KingdomLarder", "r_KingdomLarderCapacity", 96);
			AssertFoodMechanism(objects, "r_KingdomSnapjawTrailDen",
				"r_KingdomOpenCreedFurnitureProfile", "r_KingdomLarderCapacity", 64);
			XElement trailDen = objects.Descendants("object").Single(e =>
				(string)e.Attribute("Name") == "r_KingdomSnapjawTrailDen");
			CollectionAssert.IsSupersetOf(trailDen.Elements("part")
				.Select(e => (string)e.Attribute("Name")).ToArray(),
				new string[] { "Container", "Inventory" });
			AssertFoodMechanism(objects, "r_KingdomSvardymBrineNursery",
				"r_KingdomField", "r_KingdomCropRows", 8);
			AssertFoodMechanism(objects, "r_KingdomMopangoRefugeKitchen",
				"r_KingdomLarder", "r_KingdomLarderCapacity", 128);
			AssertFoodMechanism(objects, "r_KingdomYdVineBower",
				"r_KingdomField", "r_KingdomCropRows", 6);

			string architecture = TestMain.ReadRepositoryText(Path.Combine("Architecture",
				"KingdomArchitectures-Creeds.xml"));
			StringAssert.Contains("Anchors=\"storage:trail-meat\"", architecture);
			StringAssert.Contains("<require Role=\"storage:trail-meat\" Min=\"1\"/>", architecture);
			StringAssert.Contains("Key=\"hearth\" Blueprint=\"r_KingdomCivicCampfire\"", architecture);
			Assert.AreEqual(2, architecture.Split(new string[] { "Object=\"$hearth\"" },
				StringSplitOptions.None).Length - 1,
				"spice and refuge kitchens must use real cookable hearth fixtures");
		}

		[Test]
		public void EveryCreedDesignHasEveryApplicableExactLotAndNoSharedProxyTopology()
		{
			XDocument catalogue = XDocument.Parse(TestMain.ReadRepositoryText("KingdomBuildings.xml"));
			Dictionary<string, string> plot = catalogue.Descendants("building")
				.Where(e => e.Attribute("Creed") != null)
				.ToDictionary(e => (string)e.Attribute("Key"), e => (string)e.Attribute("Plot"));
			Dictionary<string, HashSet<string>> sizes = plot.Keys.ToDictionary(k => k,
				k => new HashSet<string>(StringComparer.Ordinal));
			foreach (string file in Directory.GetFiles(Path.Combine(TestMain.RepositoryRoot,
				"Architecture"), "KingdomArchitectures*.xml"))
			{
				XDocument architecture = XDocument.Load(file);
				foreach (XElement binding in architecture.Descendants("binding"))
				{
					string size = (string)binding.Attribute("Size");
					foreach (XElement tier in binding.Elements("tier"))
					{
						string key = (string)tier.Attribute("BuildKey");
						if (key != null && sizes.TryGetValue(key, out var found)) found.Add(size);
					}
				}
			}
			string[] ladder = new string[] { "S", "M", "L", "XL" };
			foreach (KeyValuePair<string, string> work in plot)
			{
				string[] expected = ladder.Skip(Array.IndexOf(ladder, work.Value)).ToArray();
				CollectionAssert.AreEquivalent(expected, sizes[work.Key], work.Key);
			}

			XDocument authored = XDocument.Parse(TestMain.ReadRepositoryText(
				Path.Combine("Architecture", "KingdomArchitectures-Creeds.xml")));
			XElement[] maps = authored.Descendants("map").ToArray();
			string[] topology = maps
				.Select(m => string.Join("/", m.Elements("row").Select(r => (string)r.Attribute("Cells"))))
				.ToArray();
			Assert.AreEqual(31, topology.Length,
				"30 base creed maps plus the reviewed robot renovation map");
			Assert.AreEqual(31, topology.Distinct(StringComparer.Ordinal).Count(),
				"a renamed or recoloured proxy map is not creed architecture");
			Assert.IsTrue(maps.Any(m =>
				(string)m.Attribute("Key") == "creed-robot-chargebay-s0"));
			Assert.IsTrue(maps.Any(m =>
				(string)m.Attribute("Key") == "creed-robot-servicebay-s1"));
		}

		[Test]
		public void RuntimeProofDerivesTheCensusAndWalksFrozenMappings()
		{
			string source = TestMain.ReadRepositoryText(Path.Combine("Debug",
				"KingdomCreedContentWish.cs"));
			StringAssert.Contains("foreach (Faction faction in Factions.Loop())", source);
			StringAssert.Contains("KingdomCreed.CanBeCreed(faction)", source);
			StringAssert.Contains("KingdomArchitecture.InspectMappings()", source);
			StringAssert.Contains("KingdomZoning.GateFor(buildings[i].Key).Creed", source);
			StringAssert.Contains("behavior-bearing mapped creed-work", source);
			Assert.IsFalse(source.Contains("Joppa"));
			Assert.IsFalse(source.Contains("Barathrumites"));
		}

		[Test]
		public void CreedAndTempleVisualFixturesKeepTheirDeclaredFunction()
		{
			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			var fixtures = new Dictionary<string, string>
			{
				{ "r_KingdomCreedSpindleWheel", "Items/sw_waterwheel_1.bmp" },
				{ "r_KingdomCreedDryContact", "Items/sw_copper_wire.bmp" },
				{ "r_KingdomCreedHornPost", "Terrain/sw_monument1.bmp" },
				{ "r_KingdomCreedScrapAltar", "Terrain/sw_monument7.bmp" },
				{ "r_KingdomCreedColdBrazier", "Items/sw_firepan.bmp" },
				{ "r_KingdomCreedVineTrellis", "Tiles/sw_watervine2.bmp" },
				{ "r_KingdomCreedLivingTrunk", "Terrain/sw_bigtree1.bmp" }
			};
			foreach (var fixture in fixtures)
			{
				XElement blueprint = objects.Descendants("object").Single(e =>
					(string)e.Attribute("Name") == fixture.Key);
				Assert.AreEqual("Furniture", (string)blueprint.Attribute("Inherits"), fixture.Key);
				Assert.AreEqual(fixture.Value, (string)blueprint.Elements("part").Single(e =>
					(string)e.Attribute("Name") == "Render").Attribute("Tile"), fixture.Key);
				CollectionAssert.IsSubsetOf(blueprint.Elements("part").Select(e =>
					(string)e.Attribute("Name")).ToArray(),
					new string[] { "Render", "Description", "Physics", "Metal" }, fixture.Key);
			}
			XElement armsRack = objects.Descendants("object").Single(e =>
				(string)e.Attribute("Name") == "r_KingdomCreedWeaponRack");
			Assert.AreEqual("Furniture", (string)armsRack.Attribute("Inherits"));
			Assert.AreEqual("Items/sw_weapons_rack.bmp", (string)armsRack.Elements("part")
				.Single(e => (string)e.Attribute("Name") == "Render").Attribute("Tile"));
			CollectionAssert.AreEquivalent(
				new string[] { "Render", "Description", "Physics", "Container", "Inventory" },
				armsRack.Elements("part").Select(e => (string)e.Attribute("Name")).ToArray(),
				"the ordered rack is paid empty storage, not an inert practice silhouette");
			Assert.AreEqual("true", (string)armsRack.Elements("property").Single(e =>
				(string)e.Attribute("Name") == "DontWarnOnOpen").Attribute("Value"));
			Assert.IsFalse(armsRack.Elements().Any(e => e.Name.LocalName == "inventoryobject"),
				"construction must not mint rack contents");

			string creed = TestMain.ReadRepositoryText(Path.Combine("Architecture",
				"KingdomArchitectures-Creeds.xml"));
			foreach (string slot in new string[] { "$spindle", "$contact", "$hornpost", "$altar",
				"$drain", "$orderedarmsrack", "$brazier", "$trellis", "$trunk" })
				StringAssert.Contains(slot, creed);
			XElement creedPalette = XDocument.Parse(creed).Descendants("palette").Single(e =>
				(string)e.Attribute("Key") == "creed-practice-hands");
			XElement trunkSlot = creedPalette.Elements("slot").Single(e =>
				(string)e.Attribute("Key") == "trunk");
			Assert.AreEqual("timber", (string)trunkSlot.Attribute("Material"));
			Assert.AreEqual("yes", (string)trunkSlot.Attribute("Natural"));
			XElement path = objects.Descendants("object").Single(e =>
				(string)e.Attribute("Name") == "r_KingdomGroundTroddenPath");
			Assert.AreEqual("ArenaFloor", (string)path.Attribute("Inherits"));
			Assert.IsNull(path.Elements("part").Single(e =>
				(string)e.Attribute("Name") == "Render").Attribute("Tile"));

			XDocument faith = XDocument.Parse(TestMain.ReadRepositoryText(Path.Combine(
				"Architecture", "KingdomArchitectures-CivicFaith.xml")));
			Assert.AreEqual(2, faith.Descendants("slot").Count(e =>
				(string)e.Attribute("Blueprint") == "r_KingdomFixtureChairStone"
				&& (string)e.Attribute("Role") == "functional-stone-nave-seat"));
			XElement[] seats = faith.Descendants("glyph").Where(e =>
				(string)e.Attribute("Anchors") == "seat:nave").ToArray();
			Assert.AreEqual(2, seats.Length);
			Assert.IsTrue(seats.All(e => (string)e.Attribute("Object") == "$seat"
				&& e.Attribute("Structure") == null && (string)e.Attribute("Pass") == "adjacent"));
		}

		[Test]
		public void LifecycleMarkersAndArcologyHaveDistinctLocalRenderLanguage()
		{
			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			string[] names = { "r_KingdomHeartStake", "r_KingdomPlanMarker",
				"r_KingdomRelocationStake", "r_KingdomClearanceStake", "r_KingdomSocket",
				"r_KingdomNotice", "r_KingdomCarrySign" };
			string[] glyphs = names.Select(name => (string)objects.Descendants("object").Single(e =>
				(string)e.Attribute("Name") == name).Elements("part").Single(e =>
				(string)e.Attribute("Name") == "Render").Attribute("RenderString")).ToArray();
			Assert.AreEqual(names.Length, glyphs.Distinct(StringComparer.Ordinal).Count());
			XElement arcology = objects.Descendants("object").Single(e =>
				(string)e.Attribute("Name") == "r_KingdomArcology");
			Assert.AreEqual("Tiles/sw_arch.png", (string)arcology.Elements("part").Single(e =>
				(string)e.Attribute("Name") == "Render").Attribute("Tile"));
		}

		private static bool Positive(XElement element, string name)
		{
			return int.TryParse((string)element.Attribute(name), out int value) && value > 0;
		}

		private static void AssertFoodMechanism(XDocument objects, string name,
			string parent, string tag, int value)
		{
			XElement blueprint = objects.Descendants("object").Single(e =>
				(string)e.Attribute("Name") == name);
			Assert.AreEqual(parent, (string)blueprint.Attribute("Inherits"), name);
			XElement mechanism = blueprint.Elements("tag").Single(e =>
				(string)e.Attribute("Name") == tag);
			Assert.AreEqual(value.ToString(), (string)mechanism.Attribute("Value"), name);
		}

		private static string[] ReadAdmitted(string path)
		{
			XDocument document = XDocument.Load(path);
			return document.Root.Elements("faction").Select(e => new CreedFactionFacts(
				(string)e.Attribute("Name"), Bool(e, "Visible", true), Bool(e, "HatesPlayer", false),
				Bool(e, "Old", true), Number(e, "HistoricalSignificance"),
				Bool(e, "FormatWithArticle", false)))
				.Where(f => KingdomCreedContentRules.CanBeCreed(f, 3)).Select(f => f.Name)
				.OrderBy(x => x, StringComparer.Ordinal).ToArray();
		}

		private static bool Bool(XElement element, string name, bool fallback)
		{
			return bool.TryParse((string)element.Attribute(name), out bool value) ? value : fallback;
		}

		private static int Number(XElement element, string name)
		{
			return int.TryParse((string)element.Attribute(name), out int value) ? value : 0;
		}

		private static string LocateBase()
		{
			string supplied = Environment.GetEnvironmentVariable("TAF_QUD_BASE");
			if (supplied != null)
			{
				if (!string.IsNullOrWhiteSpace(supplied)
					&& File.Exists(Path.Combine(supplied, "Factions.xml"))) return supplied;
				throw new InvalidOperationException(
					"TAF_QUD_BASE is set but does not contain Factions.xml: " + supplied);
			}
			string[] candidates = new string[]
			{
				@"F:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\StreamingAssets\Base",
				"/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/Base"
			};
			for (int i = 0; i < candidates.Length; i++)
				if (!string.IsNullOrEmpty(candidates[i])
					&& File.Exists(Path.Combine(candidates[i], "Factions.xml"))) return candidates[i];
			Assert.Ignore("Creed census requires installed Qud 2.0.211.51 base data.");
			return null;
		}
	}
}
#endif
