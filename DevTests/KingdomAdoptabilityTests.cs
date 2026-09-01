using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomAdoptabilityTests
	{
		private static readonly string[] Expected = {
			"barracks", "bathhouse", "bazaar", "bench", "blockhut", "blockyard", "bonefold", "bookshelf",
			"caproof", "caravanserai", "carvedcell", "carvedgallery", "chargingpost",
			"cairn", "court", "deepcut", "factorhouse", "finehouse", "fire", "forge", "forgehall", "hall", "house",
			"housecourt", "hut", "hutyard", "hindrenweavehall", "larder", "manor",
			"masonyard", "mudhut", "mudhutcourt", "oven", "reservoir", "sawyeryard",
			"scriptorium", "shrine", "shrinegarth", "smelter", "smithy", "stiltrow", "strangersguestscreen", "temple", "tent", "tentrow",
			"terrace", "toolshed", "underbench", "watchhouse", "workshop", "ydroofline"
		};

		[Test]
		public void ShippedAdoptionSetIsCuratedAndEveryDeclarationPassesPolicy()
		{
			XDocument catalogue = ReadXml("RuntimeData/KingdomBuildings.xml");
			List<XElement> adopted = catalogue.Descendants("building")
				.Where(x => Fold((string)x.Attribute("Adoptable")) == "yes").ToList();
			CollectionAssert.AreEquivalent(Expected,
				adopted.Select(x => (string)x.Attribute("Key")).ToArray());
			foreach (XElement row in catalogue.Descendants("building"))
			{
				string key = (string)row.Attribute("Key");
				Assert.That(KingdomPlotRules.TryParseSize((string)row.Attribute("Plot"),
					out KingdomPlotRules.PlotSize size), Is.True, key);
				bool eligible = KingdomAdoptabilityRules.TryClassify(key,
					(string)row.Attribute("Category"), size,
					Fold((string)row.Attribute("Open")) == "yes", out _, out string failure);
				Assert.That(Fold((string)row.Attribute("Adoptable")) == "yes",
					Is.EqualTo(eligible), key + ": " + failure);
			}
		}

		[Test]
		public void OrdinaryStaffedRoomsHaveStockedExactDesignFixtures()
		{
			XDocument catalogue = ReadXml("RuntimeData/KingdomBuildings.xml");
			XDocument objects = ReadXml("RuntimeData/ObjectBlueprints.xml");
			XDocument populations = ReadXml("RuntimeData/PopulationTables.xml");
			Dictionary<string, XElement> blueprints = objects.Descendants("object")
				.ToDictionary(x => (string)x.Attribute("Name"), StringComparer.Ordinal);
			HashSet<string> stocked = new HashSet<string>(populations.Descendants("object")
				.Select(x => (string)x.Attribute("Blueprint")).Where(x => x != null),
				StringComparer.Ordinal);
			HashSet<string> portableDesigns = new HashSet<string>(StringComparer.Ordinal);
			foreach (string name in stocked)
			{
				if (!blueprints.TryGetValue(name, out XElement item)
					|| Tag(item, blueprints, "r_KingdomPortableProvider") != "yes") continue;
				string design = Tag(item, blueprints, "r_KingdomProviderBuildKey");
				if (!string.IsNullOrEmpty(design)) portableDesigns.Add(design);
			}
			foreach (XElement row in catalogue.Descendants("building")
				.Where(x => Fold((string)x.Attribute("Adoptable")) == "yes"))
			{
				string key = (string)row.Attribute("Key");
				string category = Fold((string)row.Attribute("Category"));
				int staff = (int?)row.Attribute("Staff") ?? 0;
				if (staff > 0 && category != "housing" && category != "storage")
					Assert.That(KingdomAdoptionOperationRules.RequiresContract(category, staff),
						Is.True, key + " lacks staffed-room authority");
				if (category == "housing")
				{
					Assert.That(stocked, Does.Contain("r_KingdomPortableBedroll"),
						key + " cannot physically embody housing capacity");
					string carries = Fold((string)row.Attribute("Carries"));
					string provides = Fold((string)row.Attribute("Provides"));
					if (carries.Split(',').Any(x => !x.StartsWith("roof:",
						StringComparison.Ordinal)) || provides.Length > 0)
						Assert.That(portableDesigns, Does.Contain(key),
							key + " has no stocked exact-design amenity fixture");
					continue;
				}
				if (key == "larder") continue;
				if (key == "bookshelf")
					Assert.That(stocked, Does.Contain("r_KingdomPortableChronicleShelf"));
				else if (key == "chargingpost")
				{
					Assert.That(portableDesigns, Does.Contain(key),
						key + " has no stocked exact-design benefit fixture");
					Assert.That(stocked, Does.Contain("r_KingdomPortableChargingCradle"),
						key + " has no stocked physical charging fixture");
				}
				else if (key == "shrine" || key == "shrinegarth")
					Assert.That(stocked, Does.Contain("r_KingdomPortableShrine"));
				else Assert.That(portableDesigns, Does.Contain(key),
					key + " has no stocked exact-design benefit fixture");
			}
		}

		[TestCase("field", "food", KingdomPlotRules.PlotSize.Medium, true)]
		[TestCase("grindmill", "craft", KingdomPlotRules.PlotSize.Medium, false)]
		[TestCase("vathouse", "craft", KingdomPlotRules.PlotSize.Medium, false)]
		[TestCase("ezrawheelshade", "craft", KingdomPlotRules.PlotSize.Medium, false)]
		[TestCase("heartmoot", "civic", KingdomPlotRules.PlotSize.Large, false)]
		[TestCase("arcology", "civic", KingdomPlotRules.PlotSize.Huge, false)]
		public void AuthoredOperationBoundariesFailClosed(string key, string category,
			KingdomPlotRules.PlotSize size, bool open)
		{
			Assert.That(KingdomAdoptabilityRules.TryClassify(key, category, size, open,
				out KingdomAdoptionTargetKind kind, out _), Is.False);
			Assert.That(kind, Is.EqualTo(KingdomAdoptionTargetKind.None));
		}

		[Test]
		public void LarderIsTypedDryContainerAuthorityOnly()
		{
			Assert.That(KingdomAdoptabilityRules.TryClassify("larder", "storage",
				KingdomPlotRules.PlotSize.Small, false, out KingdomAdoptionTargetKind kind,
				out string failure), Is.True, failure);
			Assert.That(kind, Is.EqualTo(KingdomAdoptionTargetKind.Larder));
			Assert.That(KingdomAdoptabilityRules.CandidateMatches(kind, false, true), Is.True);
			Assert.That(KingdomAdoptabilityRules.CandidateMatches(kind, true, true), Is.False);
			Assert.That(KingdomAdoptabilityRules.CandidateMatches(kind, false, false), Is.False);
		}

		[TestCase("fire", "civic", KingdomPlotRules.PlotSize.Small)]
		[TestCase("bazaar", "civic", KingdomPlotRules.PlotSize.Medium)]
		[TestCase("sawyeryard", "craft", KingdomPlotRules.PlotSize.Medium)]
		[TestCase("reservoir", "storage", KingdomPlotRules.PlotSize.Large)]
		public void OrdinaryOpenRolesUseExactPlotAuthority(string key, string category,
			KingdomPlotRules.PlotSize size)
		{
			Assert.That(KingdomAdoptabilityRules.TryClassify(key, category, size, true,
				out KingdomAdoptionTargetKind kind, out string failure), Is.True, failure);
			Assert.That(kind, Is.EqualTo(KingdomAdoptionTargetKind.OpenPlot));
		}

		[Test]
		public void LoaderMenuAndTransactionsShareThePolicy()
		{
			string loader = Text("Core/KingdomData.Buildings.cs");
			string menu = Text("Core/KingdomCharterPart.MealAndAdoption.cs");
			string work = Text("Growth/KingdomAdopt.Work.cs");
			string storage = Text("Growth/KingdomAdopt.cs");
			StringAssert.Contains("KingdomAdoptabilityRules.TryClassify", loader);
			StringAssert.Contains("unsafe Adoptable declaration", loader);
			StringAssert.Contains("KingdomAdoptabilityRules.TryClassify", menu);
			StringAssert.Contains("KingdomAdoptabilityRules.TryClassify", work);
			StringAssert.Contains("KingdomAdoptabilityRules.TryClassify", storage);
			StringAssert.Contains("KingdomAdoptabilityRules.CandidateMatches", storage);
		}

		private static string Tag(XElement Item, IDictionary<string, XElement> Blueprints,
			string Name)
		{
			for (int depth = 0; Item != null && depth < 32; depth++)
			{
				XElement tag = Item.Elements("tag").FirstOrDefault(x =>
					(string)x.Attribute("Name") == Name);
				if (tag != null) return (string)tag.Attribute("Value") ?? "";
				string parent = (string)Item.Attribute("Inherits");
				Item = parent != null && Blueprints.TryGetValue(parent, out XElement inherited)
					? inherited : null;
			}
			return "";
		}

		private static XDocument ReadXml(string path)
		{
			return XDocument.Parse(Text(path));
		}

		private static string Text(string path) => TestMain.ReadRepositoryText(path);
		private static string Fold(string value) => (value ?? "").Trim().ToLowerInvariant();
	}
}
