#if TAF_TESTS
using System;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

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
			Assert.AreEqual("barracks", (string)watchhouse.Attribute("UpgradesTo"));
			Assert.AreEqual("stone:14,shapedtimber:6",
				(string)watchhouse.Attribute("UpgradeMaterials"));
			Assert.AreEqual("M", (string)watchhouse.Attribute("Plot"));
			Assert.AreEqual("L", (string)barracks.Attribute("Plot"));
			Assert.IsNull(watchhouse.Attribute("Defence"));
			Assert.IsNull(barracks.Attribute("Defence"));

			foreach (string key in new[] { "palisade", "rubblewall" })
			{
				XElement wall = Building(catalogue, key);
				Assert.AreEqual("rampart", (string)wall.Attribute("UpgradesTo"), key);
				Assert.IsNull(wall.Attribute("Plot"), key);
				Assert.IsNotNull(wall.Attribute("Defence"), key);
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
			Assert.AreEqual("0", (string)mediumWatch.Attribute("Level"));
			Assert.AreEqual("0", (string)largeWatch.Attribute("Level"));
			Assert.AreEqual("defense-watchhouse-l0",
				(string)largeWatch.Attribute("Map"));
			Assert.AreEqual("1", (string)largeBarracks.Attribute("Level"));
			Assert.AreEqual("renovate-expand",
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
			Assert.AreEqual(33, byCreed.Count);
			CollectionAssert.AreEquivalent(new[] { "Robots" }, byCreed
				.Where(pair => pair.Value.Length > 1).Select(pair => pair.Key));
			XElement[] successors = byCreed.Values.SelectMany(value => value)
				.Where(e => e.Attribute("UpgradesTo") != null).ToArray();
			Assert.AreEqual(1, successors.Length);
			Assert.AreEqual("robotchargebay", (string)successors[0].Attribute("Key"));
			Assert.AreEqual("robotservicebay",
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
