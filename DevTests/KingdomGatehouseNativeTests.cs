#if TAF_TESTS
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Installed-base proof that the authored root inherits Qud's actual vanilla Door.</summary>
	[TestFixture]
	public class KingdomGatehouseNativeTests
	{
		[Test]
		public void GateRootRetainsVanillaDoorPartAndOwnsOnlyTopology()
		{
			string baseRoot = LocateBase();
			// Qud ships a few XML-1.0-forbidden control references later in this file. Read
			// only the literal native Door block instead of weakening XML parsing globally.
			string furniture = File.ReadAllText(Path.Combine(baseRoot,
				"ObjectBlueprints", "Furniture.xml"));
			int doorAt = furniture.IndexOf("<object Name=\"Door\"", StringComparison.Ordinal);
			Assert.GreaterOrEqual(doorAt, 0);
			int doorEnd = furniture.IndexOf("</object>", doorAt, StringComparison.Ordinal);
			Assert.Greater(doorEnd, doorAt);
			string door = furniture.Substring(doorAt, doorEnd - doorAt);
			StringAssert.Contains("Inherits=\"MountedFurniture\"", door);
			StringAssert.Contains("<part Name=\"Door\"", door);
			StringAssert.Contains("<tag Name=\"Door\"", door);
			int gateAt = furniture.IndexOf("<object Name=\"Gate\"", StringComparison.Ordinal);
			Assert.GreaterOrEqual(gateAt, 0);
			int gateEnd = furniture.IndexOf("</object>", gateAt, StringComparison.Ordinal);
			string nativeGate = furniture.Substring(gateAt, gateEnd - gateAt);
			StringAssert.Contains("ClosedTile=\"Items/sw_fence_gates_2_open.bmp\"", nativeGate);
			StringAssert.Contains("OpenTile=\"Items/sw_fence_gates_closed.bmp\"", nativeGate);
			StringAssert.Contains("<object Name=\"BaseChair\" Inherits=\"Furniture\">", furniture);
			StringAssert.Contains("<part Name=\"Chair\" />", furniture);
			StringAssert.Contains("<object Name=\"Floor Cushion\" Inherits=\"BaseChair\">",
				furniture);
			StringAssert.Contains("<object Name=\"Bench\" Inherits=\"BaseChair\">", furniture);

			XDocument authored = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement authoredGate = authored.Descendants("object")
				.Single(e => (string)e.Attribute("Name") == "r_KingdomGatehouse");
			Assert.AreEqual("Door", (string)authoredGate.Attribute("Inherits"));
			Assert.IsTrue(authoredGate.Elements("part")
				.Any(e => (string)e.Attribute("Name") == "r_KingdomGatehouse"));
			Assert.IsFalse(authoredGate.Elements("part").Any(e =>
				(string)e.Attribute("Name") == "r_KingdomGatehouseProjectionV2"),
				"the fixed-layout v2 custody part is attached only after exact v2 decode");
			Assert.IsFalse(authoredGate.Elements("part").Any(e =>
				(string)e.Attribute("Name") == "r_KingdomGatehouseProjectionV1Pending"),
				"the pending-v1 migration carrier is never authored onto completed v1 roots");
			Assert.IsFalse(authoredGate.Elements("removepart")
				.Any(e => (string)e.Attribute("Name") == "Door"));
			XElement physics = authoredGate.Elements("part")
				.Single(e => (string)e.Attribute("Name") == "Physics");
			Assert.IsNull(physics.Attribute("Solid"),
				"vanilla Door, not a frozen solid furniture root, owns open/closed passability");
		}

		[Test]
		public void V2FormsUseOnlyVerifiedWallAndFunctionalWatchWrappers()
		{
			XDocument authored = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XDocument catalogue = XDocument.Parse(TestMain.ReadRepositoryText(
				"KingdomBuildings.xml"));
			CollectionAssert.AreEqual(new string[]
			{
				"common", "verdant", "fungal", "gyre", "eater"
			}, catalogue.Root.Elements("style").Select(e => (string)e.Attribute("Name")).ToArray());
			XElement gateEntry = catalogue.Descendants("building").Single(e =>
				(string)e.Attribute("Key") == "gatehouse");
			Assert.AreEqual("all", (string)gateEntry.Attribute("Styles"));
			Assert.AreEqual("stone:34,timber:10,scrap:6",
				(string)gateEntry.Attribute("Materials"), "v1 fallback remains unchanged");
			string[] walls = new string[]
			{
				"r_KingdomStructureSandstone", "r_KingdomStructureBrinestalkWall",
				"r_KingdomStructureMushroomWall", "r_KingdomStructureLimestone",
				"r_KingdomRubbleWall"
			};
			string[] watches = new string[]
			{
				"r_KingdomFixtureChairStone", "r_KingdomFixtureBenchTimber",
				"r_KingdomFixtureCushionCanvas", "r_KingdomFixtureChairMarble"
			};
			for (int i = 0; i < walls.Length; i++)
				Assert.AreEqual(1, authored.Descendants("object").Count(e =>
					(string)e.Attribute("Name") == walls[i]), walls[i]);
			for (int i = 0; i < watches.Length; i++)
				Assert.AreEqual(1, authored.Descendants("object").Count(e =>
					(string)e.Attribute("Name") == watches[i]), watches[i]);
			string rules = TestMain.ReadRepositoryText("Growth/KingdomGatehouseRules.cs");
			StringAssert.DoesNotContain("r_KingdomStructureMetalWall", rules);
			StringAssert.DoesNotContain("r_KingdomStructureRustedMetalWall", rules);
			StringAssert.Contains("Items/sw_bench.bmp", rules);
			StringAssert.Contains("Items/sw_cushion1.bmp", rules);
		}

		private static string LocateBase()
		{
			string supplied = Environment.GetEnvironmentVariable("TAF_QUD_BASE");
			if (supplied != null)
			{
				if (!string.IsNullOrWhiteSpace(supplied) && File.Exists(Path.Combine(supplied,
					"ObjectBlueprints", "Furniture.xml"))) return supplied;
				throw new InvalidOperationException(
					"TAF_QUD_BASE is set but does not contain ObjectBlueprints/Furniture.xml: "
					+ supplied);
			}
			string[] candidates = new string[]
			{
				@"F:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\StreamingAssets\Base",
				"/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/Base"
			};
			for (int i = 0; i < candidates.Length; i++)
			{
				if (!string.IsNullOrEmpty(candidates[i])
					&& File.Exists(Path.Combine(candidates[i], "ObjectBlueprints", "Furniture.xml")))
					return candidates[i];
			}
			Assert.Ignore(
				"Gatehouse native test requires TAF_QUD_BASE or the configured Caves of Qud base.");
			return null;
		}
	}
}
#endif
