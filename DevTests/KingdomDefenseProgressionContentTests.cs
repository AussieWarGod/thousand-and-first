#if TAF_TESTS
using System;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomDefenseProgressionContentTests
	{
		[Test]
		public void GarrisonAndFrontierFabricRemainSeparateExactLineages()
		{
			XDocument catalogue = XDocument.Parse(TestMain.ReadRepositoryText(
				"KingdomBuildings.xml"));
			XElement watchhouse = Building(catalogue, "watchhouse");
			XElement barracks = Building(catalogue, "barracks");
			ClassicAssert.AreEqual("barracks", (string)watchhouse.Attribute("UpgradesTo"));
			ClassicAssert.AreEqual("stone:14,shapedtimber:6",
				(string)watchhouse.Attribute("UpgradeMaterials"));
			ClassicAssert.AreEqual("M", (string)watchhouse.Attribute("Plot"));
			ClassicAssert.AreEqual("L", (string)barracks.Attribute("Plot"));
			ClassicAssert.IsNull(watchhouse.Attribute("Defence"));
			ClassicAssert.IsNull(barracks.Attribute("Defence"));

			foreach (string key in new[] { "palisade", "rubblewall" })
			{
				XElement wall = Building(catalogue, key);
				ClassicAssert.AreEqual("rampart", (string)wall.Attribute("UpgradesTo"), key);
				ClassicAssert.IsNull(wall.Attribute("Plot"), key);
				ClassicAssert.IsNotNull(wall.Attribute("Defence"), key);
			}

			XDocument architecture = XDocument.Parse(TestMain.ReadRepositoryText(
				"Architecture/KingdomArchitectures-CivicFaith.xml"));
			XElement plan = architecture.Descendants("plan").Single(e =>
				(string)e.Attribute("Key") == "defense-garrison");
			XElement medium = plan.Elements("binding").Single(e =>
				(string)e.Attribute("Size") == "M");
			XElement large = plan.Elements("binding").Single(e =>
				(string)e.Attribute("Size") == "L");
			XElement mediumWatch = medium.Elements("tier").Single();
			XElement largeWatch = large.Elements("tier").Single(e =>
				(string)e.Attribute("BuildKey") == "watchhouse");
			XElement largeBarracks = large.Elements("tier").Single(e =>
				(string)e.Attribute("BuildKey") == "barracks");
			ClassicAssert.AreEqual("0", (string)mediumWatch.Attribute("Level"));
			ClassicAssert.AreEqual("0", (string)largeWatch.Attribute("Level"));
			ClassicAssert.AreEqual("defense-watchhouse-l0",
				(string)largeWatch.Attribute("Map"));
			ClassicAssert.AreEqual("1", (string)largeBarracks.Attribute("Level"));
			ClassicAssert.AreEqual("renovate-expand",
				(string)largeBarracks.Attribute("Transition"));
		}

		[Test]
		public void CreedProgressionExistsOnlyWhereASecondPracticeWasAuthored()
		{
			XDocument catalogue = XDocument.Parse(TestMain.ReadRepositoryText(
				"KingdomBuildings.xml"));
			var byCreed = catalogue.Descendants("building")
				.Where(e => e.Attribute("Creed") != null)
				.GroupBy(e => (string)e.Attribute("Creed"))
				.ToDictionary(group => group.Key, group => group.ToArray(),
					StringComparer.Ordinal);
			ClassicAssert.AreEqual(33, byCreed.Count);
			CollectionAssert.AreEquivalent(new[] { "Robots" }, byCreed
				.Where(pair => pair.Value.Length > 1).Select(pair => pair.Key));
			XElement[] successors = byCreed.Values.SelectMany(value => value)
				.Where(e => e.Attribute("UpgradesTo") != null).ToArray();
			ClassicAssert.AreEqual(1, successors.Length);
			ClassicAssert.AreEqual("robotchargebay", (string)successors[0].Attribute("Key"));
			ClassicAssert.AreEqual("robotservicebay",
				(string)successors[0].Attribute("UpgradesTo"));
		}

		private static XElement Building(XDocument catalogue, string key)
		{
			return catalogue.Descendants("building").Single(e =>
				(string)e.Attribute("Key") == key);
		}
	}
}
#endif
