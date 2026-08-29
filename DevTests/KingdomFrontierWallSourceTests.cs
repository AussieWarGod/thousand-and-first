#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomFrontierWallSourceTests
	{
		private static XElement Blueprint(XDocument Document, string Name)
		{
			return Document.Root.Elements("object").Single(row =>
				(string)row.Attribute("Name") == Name);
		}

		private static bool Child(XElement Row, string Element, string Name)
		{
			return Row.Elements(Element).Any(child =>
				(string)child.Attribute("Name") == Name);
		}

		private static string Attribute(XElement Row, string Element, string Name,
			string AttributeName)
		{
			return (string)Row.Elements(Element).Single(child =>
				(string)child.Attribute("Name") == Name).Attribute(AttributeName);
		}

		[Test]
		public void PublicFrontierWallsUseDeterministicVanillaConnectedWallChains()
		{
			XDocument document = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement brinestalk = Blueprint(document, "r_KingdomStructureBrinestalkWall");
			XElement rock = Blueprint(document, "r_KingdomStructureRockWall");
			Assert.AreEqual("BrinestalkWall", (string)brinestalk.Attribute("Inherits"));
			Assert.AreEqual("BaseWallRock", (string)rock.Attribute("Inherits"));
			foreach (XElement wrapper in new XElement[] { brinestalk, rock })
			{
				Assert.IsTrue(Child(wrapper, "removebuilder", "Animated"));
				Assert.IsTrue(Child(wrapper, "removebuilder", "RandomTile"));
				Assert.IsTrue(Child(wrapper, "removepart", "Graffitied"));
				Assert.IsTrue(Child(wrapper, "removetag", "NamingTag"));
				Assert.IsTrue(Child(wrapper, "removetag", "Animatable"));
				Assert.IsTrue(Child(wrapper, "removetag",
					"DynamicObjectsTable:AnimatableFurniture"));
				Assert.IsFalse(Child(wrapper, "removetag", "PaintedWall"));
			}

			Assert.AreEqual("r_KingdomStructureBrinestalkWall",
				(string)Blueprint(document, "r_KingdomPalisade").Attribute("Inherits"));
			Assert.AreEqual("r_KingdomStructureRockWall",
				(string)Blueprint(document, "r_KingdomRampart").Attribute("Inherits"));
			Assert.AreEqual("r_KingdomRampart",
				(string)Blueprint(document, "r_KingdomRubbleWall").Attribute("Inherits"));
		}

		[Test]
		public void FrontierWallLoreMaterialStatsAndDefaultVanillaTilesRemainStable()
		{
			XDocument document = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement palisade = Blueprint(document, "r_KingdomPalisade");
			XElement rampart = Blueprint(document, "r_KingdomRampart");
			XElement rubble = Blueprint(document, "r_KingdomRubbleWall");
			Assert.AreEqual("Walls/wall_brinestalk-00000000.png",
				Attribute(palisade, "part", "Render", "Tile"));
			Assert.AreEqual("900", Attribute(palisade, "part", "Physics", "Weight"));
			Assert.AreEqual("120", Attribute(palisade, "stat", "Hitpoints", "Value"));
			Assert.AreEqual("4", Attribute(palisade, "stat", "AV", "Value"));
			StringAssert.Contains("Cut thornbrush", Attribute(palisade, "part", "Description", "Short"));

			Assert.AreEqual("Tiles/wall_rock-00000000.bmp",
				Attribute(rampart, "part", "Render", "Tile"));
			Assert.AreEqual("4000", Attribute(rampart, "part", "Physics", "Weight"));
			Assert.AreEqual("400", Attribute(rampart, "stat", "Hitpoints", "Value"));
			Assert.AreEqual("10", Attribute(rampart, "stat", "AV", "Value"));
			StringAssert.Contains("Fieldstone", Attribute(rampart, "part", "Description", "Short"));

			Assert.AreEqual("3000", Attribute(rubble, "part", "Physics", "Weight"));
			Assert.AreEqual("220", Attribute(rubble, "stat", "Hitpoints", "Value"));
			Assert.AreEqual("5", Attribute(rubble, "stat", "AV", "Value"));
			StringAssert.Contains("already here", Attribute(rubble, "part", "Description", "Short"));
		}

		[Test]
		public void NonWallWorksDoNotAccidentallyAcquirePaintedWallRendering()
		{
			XDocument document = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement profile = Blueprint(document, "r_KingdomRampartFurnitureProfile");
			Assert.AreEqual("Furniture", (string)profile.Attribute("Inherits"));
			Assert.IsTrue(Child(profile, "removepart", "Graffitied"));
			foreach (string name in new string[] { "r_KingdomSnapjawTrailDen",
				"r_KingdomIssachariRiflePorch", "r_KingdomTemplarPurityArsenal",
				"r_KingdomWardensWatchLodge" })
				Assert.AreEqual("r_KingdomRampartFurnitureProfile",
					(string)Blueprint(document, name).Attribute("Inherits"), name);
		}

		[Test]
		public void InstalledVanillaParentsProvideExactPaintedWallVocabulary()
		{
			string walls = File.ReadAllText(Path.Combine(LocateBase(),
				"ObjectBlueprints", "Walls.xml"));
			string wall = ObjectBlock(walls, "Wall");
			string rock = ObjectBlock(walls, "BaseWallRock");
			string brinestalk = ObjectBlock(walls, "BrinestalkWall");
			StringAssert.Contains("<builder Name=\"Animated\"", wall);
			StringAssert.Contains("<part Name=\"Graffitied\"", wall);
			StringAssert.Contains("<tag Name=\"PaintedWall\" Value=\"wall_rock\"", rock);
			StringAssert.Contains("<tag Name=\"SingleTile\" Value=\"Tiles/wall_rock-00000000.bmp\"", rock);
			StringAssert.Contains("Inherits=\"BaseWallWood\"", brinestalk);
			StringAssert.Contains("<tag Name=\"PaintedWall\" Value=\"wall_brinestalk\"", brinestalk);
			StringAssert.Contains("<tag Name=\"PaintedWallAtlas\" Value=\"Assets_Content_Textures_Walls_\"",
				brinestalk);
			StringAssert.Contains("<tag Name=\"PaintedWallExtension\" Value=\".png\"", brinestalk);
		}

		[Test]
		public void NativeGalleryExercisesEveryConnectedWallReviewTopology()
		{
			string source = TestMain.ReadRepositoryText(
				"Debug/KingdomArchitectureGalleryWishes.VisualCases.cs");
			Assert.AreEqual(3, Occurrences(source, "AddWallTopologyCase(result"));
			foreach (string key in new string[] { "palisade", "rampart", "rubblewall" })
				StringAssert.Contains("AddWallTopologyCase(result, \"" + key + "\"", source);
			StringAssert.DoesNotContain("AddObjectCase(result, \"palisade\"", source);
			StringAssert.DoesNotContain("AddObjectCase(result, \"rampart\"", source);
			StringAssert.DoesNotContain("AddObjectCase(result, \"rubblewall\"", source);

			int start = source.IndexOf("private static void AddWallTopologyCase",
				StringComparison.Ordinal);
			int end = source.IndexOf("private static void AddLineCase", start,
				StringComparison.Ordinal);
			Assert.Greater(start, 0);
			Assert.Greater(end, start);
			string method = source.Substring(start, end - start);
			Assert.AreEqual(24, Occurrences(method, "item.Placements.Add"));
			foreach (string role in new string[] { "single", "horizontal-centre",
				"vertical-centre", "corner-turn", "tee-centre", "cross-centre",
				"gate-adjacent-west", "gate", "gate-adjacent-east" })
				StringAssert.Contains("\"" + role + "\"", method);
			StringAssert.Contains("Width = 13, Height = 9", method);
			StringAssert.Contains("At(\"cross-centre\", Wall, 10, 4)", method);
			StringAssert.Contains("At(\"gate\", Gate, 10, 8)", method);
			StringAssert.Contains("At(\"gate-adjacent-west\", Wall, 9, 8)", method);
			StringAssert.Contains("At(\"gate-adjacent-east\", Wall, 11, 8)", method);

			MatchCollection placements = Regex.Matches(method,
				"At\\(\"[^\"]+\", (?:Wall|Gate), ([0-9]+), ([0-9]+)\\)");
			Assert.AreEqual(24, placements.Count);
			HashSet<string> occupied = new HashSet<string>(StringComparer.Ordinal);
			foreach (Match placement in placements)
			{
				int x = int.Parse(placement.Groups[1].Value);
				int y = int.Parse(placement.Groups[2].Value);
				Assert.GreaterOrEqual(x, 0);
				Assert.Less(x, 13);
				Assert.GreaterOrEqual(y, 0);
				Assert.Less(y, 9);
				Assert.IsTrue(occupied.Add(x + "," + y), "duplicate visual cell " + x + "," + y);
			}
		}

		private static string ObjectBlock(string Source, string Name)
		{
			int start = Source.IndexOf("<object Name=\"" + Name + "\"", StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, Name);
			int end = Source.IndexOf("</object>", start, StringComparison.Ordinal);
			Assert.Greater(end, start, Name);
			return Source.Substring(start, end - start);
		}

		private static int Occurrences(string Source, string Value)
		{
			int count = 0;
			for (int at = 0; (at = Source.IndexOf(Value, at, StringComparison.Ordinal)) >= 0;
				at += Value.Length) count++;
			return count;
		}

		private static string LocateBase()
		{
			string supplied = Environment.GetEnvironmentVariable("TAF_QUD_BASE");
			if (!string.IsNullOrWhiteSpace(supplied))
			{
				if (File.Exists(Path.Combine(supplied, "ObjectBlueprints", "Walls.xml"))) return supplied;
				throw new InvalidOperationException("TAF_QUD_BASE lacks ObjectBlueprints/Walls.xml: "
					+ supplied);
			}
			foreach (string candidate in new string[]
			{
				@"F:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\StreamingAssets\Base",
				"/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/Base"
			}) if (File.Exists(Path.Combine(candidate, "ObjectBlueprints", "Walls.xml"))) return candidate;
			throw new InvalidOperationException("Set TAF_QUD_BASE to the installed Caves of Qud base.");
		}
	}
}
#endif
