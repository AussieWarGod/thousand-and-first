#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.AreEqual("BrinestalkWall", (string)brinestalk.Attribute("Inherits"));
			ClassicAssert.AreEqual("BaseWallRock", (string)rock.Attribute("Inherits"));
			foreach (XElement wrapper in new XElement[] { brinestalk, rock })
			{
				ClassicAssert.IsTrue(Child(wrapper, "removebuilder", "Animated"));
				ClassicAssert.IsTrue(Child(wrapper, "removebuilder", "RandomTile"));
				ClassicAssert.IsTrue(Child(wrapper, "removepart", "Graffitied"));
				ClassicAssert.IsTrue(Child(wrapper, "removetag", "NamingTag"));
				ClassicAssert.IsTrue(Child(wrapper, "removetag", "Animatable"));
				ClassicAssert.IsTrue(Child(wrapper, "removetag",
					"DynamicObjectsTable:AnimatableFurniture"));
				ClassicAssert.IsFalse(Child(wrapper, "removetag", "PaintedWall"));
			}

			ClassicAssert.AreEqual("r_KingdomStructureBrinestalkWall",
				(string)Blueprint(document, "r_KingdomPalisade").Attribute("Inherits"));
			ClassicAssert.AreEqual("r_KingdomStructureRockWall",
				(string)Blueprint(document, "r_KingdomRampart").Attribute("Inherits"));
			ClassicAssert.AreEqual("Rubble",
				(string)Blueprint(document, "r_KingdomRubbleWall").Attribute("Inherits"));
			ClassicAssert.IsTrue(Child(Blueprint(document, "r_KingdomRubbleWall"),
				"removebuilder", "RandomTile"));
		}

		[Test]
		public void FrontierWallLoreMaterialStatsAndDefaultVanillaTilesRemainStable()
		{
			XDocument document = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement palisade = Blueprint(document, "r_KingdomPalisade");
			XElement rampart = Blueprint(document, "r_KingdomRampart");
			XElement rubble = Blueprint(document, "r_KingdomRubbleWall");
			ClassicAssert.AreEqual("Walls/wall_brinestalk-00000000.png",
				Attribute(palisade, "part", "Render", "Tile"));
			ClassicAssert.AreEqual("900", Attribute(palisade, "part", "Physics", "Weight"));
			ClassicAssert.AreEqual("120", Attribute(palisade, "stat", "Hitpoints", "Value"));
			ClassicAssert.AreEqual("4", Attribute(palisade, "stat", "AV", "Value"));
			StringAssert.Contains("Cut thornbrush", Attribute(palisade, "part", "Description", "Short"));

			ClassicAssert.AreEqual("Tiles/wall_rock-00000000.bmp",
				Attribute(rampart, "part", "Render", "Tile"));
			ClassicAssert.AreEqual("4000", Attribute(rampart, "part", "Physics", "Weight"));
			ClassicAssert.AreEqual("400", Attribute(rampart, "stat", "Hitpoints", "Value"));
			ClassicAssert.AreEqual("10", Attribute(rampart, "stat", "AV", "Value"));
			StringAssert.Contains("Fieldstone", Attribute(rampart, "part", "Description", "Short"));

			ClassicAssert.AreEqual("3000", Attribute(rubble, "part", "Physics", "Weight"));
			ClassicAssert.AreEqual("220", Attribute(rubble, "stat", "Hitpoints", "Value"));
			ClassicAssert.AreEqual("5", Attribute(rubble, "stat", "AV", "Value"));
			ClassicAssert.AreEqual("Tiles2/sw_rubble_2.bmp",
				Attribute(rubble, "part", "Render", "Tile"));
			ClassicAssert.AreNotEqual(Attribute(rampart, "part", "Render", "Tile"),
				Attribute(rubble, "part", "Render", "Tile"));
			StringAssert.Contains("already here", Attribute(rubble, "part", "Description", "Short"));
		}

		[Test]
		public void NonWallWorksDoNotAccidentallyAcquirePaintedWallRendering()
		{
			XDocument document = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement profile = Blueprint(document, "r_KingdomOpenCreedFurnitureProfile");
			ClassicAssert.AreEqual("Furniture", (string)profile.Attribute("Inherits"));
			ClassicAssert.IsTrue(Child(profile, "removepart", "Graffitied"));
			ClassicAssert.AreEqual("false", Attribute(profile, "part", "Physics", "Solid"));
			ClassicAssert.IsFalse(profile.Elements("part").Any(part =>
				(string)part.Attribute("Name") == "Render"));
			foreach (string name in new string[] { "r_KingdomSnapjawTrailDen",
				"r_KingdomIssachariRiflePorch", "r_KingdomTemplarPurityArsenal",
				"r_KingdomWardensWatchLodge" })
			{
				XElement work = Blueprint(document, name);
				ClassicAssert.AreEqual("r_KingdomOpenCreedFurnitureProfile",
					(string)work.Attribute("Inherits"), name);
				ClassicAssert.AreEqual("false", Attribute(work, "part", "Physics", "Solid"), name);
				XElement render = work.Elements("part").Single(part =>
					(string)part.Attribute("Name") == "Render");
				ClassicAssert.IsFalse(string.IsNullOrWhiteSpace((string)render.Attribute("RenderString")),
					name);
				ClassicAssert.IsNull(render.Attribute("Tile"), name + " inherited a wall tile");
			}
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
			ClassicAssert.AreEqual(3, Occurrences(source, "AddWallTopologyCase(result"));
			foreach (string key in new string[] { "palisade", "rampart", "rubblewall" })
				StringAssert.Contains("AddWallTopologyCase(result, \"" + key + "\"", source);
			StringAssert.DoesNotContain("AddObjectCase(result, \"palisade\"", source);
			StringAssert.DoesNotContain("AddObjectCase(result, \"rampart\"", source);
			StringAssert.DoesNotContain("AddObjectCase(result, \"rubblewall\"", source);

			int start = source.IndexOf("private static void AddWallTopologyCase",
				StringComparison.Ordinal);
			int end = source.IndexOf("private static void AddLineCase", start,
				StringComparison.Ordinal);
			ClassicAssert.Greater(start, 0);
			ClassicAssert.Greater(end, start);
			string method = source.Substring(start, end - start);
			ClassicAssert.AreEqual(24, Occurrences(method, "item.Placements.Add"));
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
			ClassicAssert.AreEqual(24, placements.Count);
			HashSet<string> occupied = new HashSet<string>(StringComparer.Ordinal);
			foreach (Match placement in placements)
			{
				int x = int.Parse(placement.Groups[1].Value);
				int y = int.Parse(placement.Groups[2].Value);
				ClassicAssert.GreaterOrEqual(x, 0);
				ClassicAssert.Less(x, 13);
				ClassicAssert.GreaterOrEqual(y, 0);
				ClassicAssert.Less(y, 9);
				ClassicAssert.IsTrue(occupied.Add(x + "," + y), "duplicate visual cell " + x + "," + y);
			}
		}

		private static string ObjectBlock(string Source, string Name)
		{
			int start = Source.IndexOf("<object Name=\"" + Name + "\"", StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, Name);
			int end = Source.IndexOf("</object>", start, StringComparison.Ordinal);
			ClassicAssert.Greater(end, start, Name);
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
			if (supplied != null)
			{
				if (!string.IsNullOrWhiteSpace(supplied)
					&& File.Exists(Path.Combine(supplied, "ObjectBlueprints", "Walls.xml"))) return supplied;
				throw new InvalidOperationException("TAF_QUD_BASE lacks ObjectBlueprints/Walls.xml: "
					+ supplied);
			}
			foreach (string candidate in new string[]
			{
				@"F:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\StreamingAssets\Base",
				"/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/Base"
			}) if (File.Exists(Path.Combine(candidate, "ObjectBlueprints", "Walls.xml"))) return candidate;
			Assert.Ignore("Frontier wall native test requires TAF_QUD_BASE or installed Qud.");
			return null;
		}
	}
}
#endif
