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
			Assert.AreEqual(33, creedWorks.Length, "one bespoke built-in design per admitted creed");
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
				"r_KingdomRampart", "r_KingdomLarderCapacity", 64);
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
			string[] topology = authored.Descendants("map")
				.Select(m => string.Join("/", m.Elements("row").Select(r => (string)r.Attribute("Cells"))))
				.ToArray();
			Assert.AreEqual(30, topology.Length);
			Assert.AreEqual(30, topology.Distinct(StringComparer.Ordinal).Count(),
				"a renamed or recoloured proxy map is not creed architecture");
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
			string[] candidates = new string[]
			{
				supplied,
				@"F:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\StreamingAssets\Base",
				"/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/Base"
			};
			for (int i = 0; i < candidates.Length; i++)
				if (!string.IsNullOrEmpty(candidates[i])
					&& File.Exists(Path.Combine(candidates[i], "Factions.xml"))) return candidates[i];
			throw new InvalidOperationException("Creed census requires installed Qud 2.0.211.51 base data.");
		}
	}
}
#endif
