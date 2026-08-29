#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if !TAF_CONSTRUCTION_INPUT_PORTABLE
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
#endif
using System.Text.RegularExpressions;
using System.Xml.Linq;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomLiquidRuntimeSourceTests
	{
		private static XElement Blueprint(XDocument Document, string Name)
		{
			return Document.Root.Elements("object").Single(row =>
				(string)row.Attribute("Name") == Name);
		}

		private static XElement Part(XElement Blueprint, string Name)
		{
			return Blueprint.Elements("part").Single(part =>
				(string)part.Attribute("Name") == Name);
		}

		[Test]
		public void PublicBlueprintIdsRemainStableWithUsefulFrozenDefaults()
		{
			XDocument document = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement water = Blueprint(document, "r_KingdomWaterMain");
			XElement brine = Blueprint(document, "r_KingdomBrineMain");
			XElement crossing = Blueprint(document, "r_KingdomLiquidCrossing");
			XElement waterTap = Blueprint(document, "r_KingdomWaterTap");
			XElement brineTap = Blueprint(document, "r_KingdomBrineTap");
			Assert.AreEqual("Furniture", (string)water.Attribute("Inherits"));
			Assert.AreEqual("r_KingdomWaterMain", (string)brine.Attribute("Inherits"));
			Assert.AreEqual("Furniture", (string)crossing.Attribute("Inherits"));
			Assert.AreEqual("Furniture", (string)waterTap.Attribute("Inherits"));
			Assert.AreEqual("r_KingdomWaterTap", (string)brineTap.Attribute("Inherits"));
			Assert.AreEqual("EW", (string)Part(water, "r_KingdomLiquidConduit").Attribute("Joins"));
			Assert.AreEqual("EW", (string)Part(brine, "r_KingdomLiquidConduit").Attribute("Joins"));
			Assert.AreEqual("EW", (string)Part(waterTap, "r_KingdomLiquidTap").Attribute("Joins"));
			Assert.AreEqual("EW", (string)Part(brineTap, "r_KingdomLiquidTap").Attribute("Joins"));
			Assert.AreEqual("NSEW", (string)Part(crossing,
				"r_KingdomLiquidCrossover").Attribute("Pairs"));
			Assert.AreEqual("196", (string)Part(water, "Render").Attribute("RenderString"));
			Assert.AreEqual("205", (string)Part(brine, "Render").Attribute("RenderString"));
			Assert.AreEqual("216", (string)Part(crossing, "Render").Attribute("RenderString"));
			Assert.IsNull(Part(water, "Render").Attribute("Tile"));
			Assert.IsNull(Part(brine, "Render").Attribute("Tile"));
			Assert.IsNull(Part(crossing, "Render").Attribute("Tile"));
		}

		[Test]
		public void ExistingPartFieldLayoutAndPositionalSaveReadabilityStayIntact()
		{
			string declarations = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomConduitPart.cs");
			string visual = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomConduitPart.Visual.cs");
			Assert.AreEqual(new string[] { "Liquid", "Joins", "RefusalAnnounced" },
				DeclaredFields(ClassBlock(declarations, "r_KingdomLiquidConduit")));
			Assert.AreEqual(new string[] { "Pairs" },
				DeclaredFields(ClassBlock(declarations, "r_KingdomLiquidCrossover")));
			Assert.AreEqual(new string[] { "Liquid", "Joins", "RefusalAnnounced" },
				DeclaredFields(ClassBlock(declarations, "r_KingdomLiquidTap")));
			StringAssert.DoesNotContain("public string ", visual);
			StringAssert.DoesNotContain("public bool ", visual);
			StringAssert.DoesNotContain("override void Write", declarations + visual);
			StringAssert.DoesNotContain("override void Read", declarations + visual);
			StringAssert.Contains("public string Joins = \"EW\";", declarations);
			StringAssert.Contains("public string Pairs = \"NSEW\";", declarations);
		}

		[Test]
		public void RuntimeActionsArePlayerAuthorizedRenderedAndTruthfullyExamined()
		{
			string part = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomConduitPart.Visual.cs");
			string interaction = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomLiquidConfiguration.cs");
			foreach (string command in new string[] { "r_ConfigureLiquidMain",
				"r_ConfigureLiquidTap", "r_ConfigureLiquidCrossing" })
				StringAssert.Contains(command, part);
			Assert.GreaterOrEqual(Occurrences(part, "E.Actor != null && E.Actor.IsPlayer()"), 5);
			Assert.AreEqual(3, Occurrences(part, "E.RenderString = ((char)"));
			Assert.AreEqual(3, Occurrences(part, "E.Tile = null;"));
			Assert.AreEqual(3, Occurrences(part, "GetShortDescriptionEvent E"));
			StringAssert.Contains("Popup.PickOption", interaction);
			StringAssert.Contains("DeclarationReadsBack", interaction);
			StringAssert.Contains("CrossingReadsBack", interaction);
			Assert.AreEqual(3, Occurrences(interaction,
				"KingdomNetworks.MarkTopologyChanged();"));
			StringAssert.DoesNotContain("MarkTopologyChanged", part);
			string visualRules = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomLiquidVisualRules.cs");
			string configurationRules = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomLiquidConfigurationRules.cs");
			StringAssert.DoesNotContain("MarkTopologyChanged", visualRules + configurationRules);
		}

		[Test]
		public void GalleryStagesAllMasksAndBothCrossingOrientationsInBounds()
		{
			string cases = TestMain.ReadRepositoryText(
				"Debug/KingdomArchitectureGalleryWishes.VisualCases.cs");
			StringAssert.Contains("for (int mask = 0; mask < 16; mask++)", cases);
			Assert.AreEqual(2, Occurrences(cases, "for (int mask = 0; mask < 16; mask++)"));
			Assert.AreEqual(2, Occurrences(cases, "AddLineCase(result"));
			Assert.AreEqual(2, Occurrences(cases, "AddTapCase(result"));
			Assert.AreEqual(2, Occurrences(cases, "Kind = VisualCaseKind.Objects, Width = 7, Height = 7"));
			StringAssert.Contains("(mask % 4) * 2, (mask / 4) * 2, joins", cases);
			HashSet<string> matrixRoles = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> matrixCells = new HashSet<string>(StringComparer.Ordinal);
			for (int mask = 0; mask < 16; mask++)
			{
				int x = (mask % 4) * 2;
				int y = (mask / 4) * 2;
				Assert.IsTrue(matrixRoles.Add("mask-" + mask.ToString("D2")));
				Assert.IsTrue(matrixCells.Add(x + "," + y));
				Assert.That(x, Is.InRange(0, 6));
				Assert.That(y, Is.InRange(0, 6));
			}

			string crossing = MethodBlock(cases, "private static void AddLiquidCrossingCase",
				"private static VisualPlacement At");
			Assert.AreEqual(10, Occurrences(crossing, "item.Placements.Add"));
			StringAssert.Contains("Width = 9, Height = 3", crossing);
			StringAssert.Contains("1, 1, \"NSEW\"", crossing);
			StringAssert.Contains("7, 1, \"EWNS\"", crossing);
			MatchCollection placed = Regex.Matches(crossing,
				"At\\(\"([^\"]+)\", \"[^\"]+\",(?:\\s*)?([0-9]+), ([0-9]+), \"[^\"]+\"\\)");
			Assert.AreEqual(10, placed.Count);
			HashSet<string> roles = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> cells = new HashSet<string>(StringComparer.Ordinal);
			foreach (Match placement in placed)
			{
				int x = int.Parse(placement.Groups[2].Value);
				int y = int.Parse(placement.Groups[3].Value);
				Assert.IsTrue(roles.Add(placement.Groups[1].Value));
				Assert.IsTrue(cells.Add(x + "," + y));
				Assert.That(x, Is.InRange(0, 8));
				Assert.That(y, Is.InRange(0, 2));
			}
		}

		[Test]
		public void GalleryAppliesAndReceiptsFrozenDeclarationsBeforePlacement()
		{
			string staging = TestMain.ReadRepositoryText(
				"Debug/KingdomArchitectureGalleryWishes.VisualStaging.cs");
			AssertOrdered(staging, "TryApplyVisualDeclaration(item, placement",
				"Cell cell = Zone.GetCell", "cell.AddObject(item");
			StringAssert.Contains("conduit.Joins = Placement.Declaration;", staging);
			StringAssert.Contains("tap.Joins = Placement.Declaration;", staging);
			StringAssert.Contains("crossing.Pairs = Placement.Declaration;", staging);
			string state = TestMain.ReadRepositoryText(
				"Debug/KingdomArchitectureGalleryWishes.VisualState.cs");
			StringAssert.Contains("item.Placements[p].Declaration ?? \"<default>\"", state);
			string geometry = TestMain.ReadRepositoryText(
				"Debug/KingdomArchitectureGalleryWishes.VisualGeometry.cs");
			StringAssert.Contains("LiquidDeclaration(item)", geometry);
			StringAssert.Contains("main:", geometry);
			StringAssert.Contains("tap:", geometry);
			StringAssert.Contains("crossing:", geometry);
		}

		#if !TAF_CONSTRUCTION_INPUT_PORTABLE
		[Test]
		public void InstalledEngineStillProvidesChosenRenderAndInteractionSeams()
		{
			using (FileStream stream = File.OpenRead(LocateAssembly()))
			using (PEReader pe = new PEReader(stream))
			{
				MetadataReader metadata = pe.GetMetadataReader();
				AssertTypeMembers(metadata, "XRL.World", "RenderEvent",
					new string[] { "RenderString", "Tile" });
				AssertTypeMembers(metadata, "XRL.UI", "Popup", new string[] { "PickOption" });
				AssertTypeMembers(metadata, "XRL.World", "GetInventoryActionsEvent",
					new string[] { "ID" });
				AssertTypeMembers(metadata, "XRL.World", "InventoryActionEvent",
					new string[] { "ID", "Command" });
				AssertTypeMembers(metadata, "XRL.World", "GetShortDescriptionEvent",
					new string[] { "ID" });
				AssertTypeMembers(metadata, "XRL.World", "IShortDescriptionEvent",
					new string[] { "Postfix" });
			}
		}
		#endif

		[Test]
		public void OwnedRuntimeSourcesStayBelowStructuralCap()
		{
			foreach (string path in new string[]
			{
				"Simulation/City/KingdomConduitPart.cs",
				"Simulation/City/KingdomConduitPart.Visual.cs",
				"Simulation/City/KingdomLiquidVisualRules.cs",
				"Simulation/City/KingdomLiquidConfigurationRules.cs",
				"Simulation/City/KingdomLiquidConfiguration.cs"
			})
			{
				int lines = TestMain.ReadRepositoryText(path).Split('\n').Length;
				Assert.Less(lines, 300, path);
			}
		}

		private static string[] DeclaredFields(string Source)
		{
			return Regex.Matches(Source, "public (?:string|bool) ([A-Za-z][A-Za-z0-9]*)")
				.Cast<Match>().Select(match => match.Groups[1].Value).ToArray();
		}

		private static string ClassBlock(string Source, string Name)
		{
			int start = Source.IndexOf("class " + Name, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, Name);
			int open = Source.IndexOf('{', start);
			Assert.Greater(open, start, Name);
			int depth = 0;
			for (int i = open; i < Source.Length; i++)
			{
				if (Source[i] == '{') depth++;
				else if (Source[i] == '}' && --depth == 0)
					return Source.Substring(start, i - start + 1);
			}
			Assert.Fail("unterminated class " + Name);
			return null;
		}

		private static string MethodBlock(string Source, string Start, string End)
		{
			int start = Source.IndexOf(Start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, Start);
			int end = Source.IndexOf(End, start + Start.Length, StringComparison.Ordinal);
			Assert.Greater(end, start, End);
			return Source.Substring(start, end - start);
		}

		private static int Occurrences(string Source, string Value)
		{
			int count = 0;
			for (int at = 0; (at = Source.IndexOf(Value, at, StringComparison.Ordinal)) >= 0;
				at += Value.Length) count++;
			return count;
		}

		private static void AssertOrdered(string Source, params string[] Terms)
		{
			int at = -1;
			foreach (string term in Terms)
			{
				int next = Source.IndexOf(term, at + 1, StringComparison.Ordinal);
				Assert.Greater(next, at, term);
				at = next;
			}
		}

		#if !TAF_CONSTRUCTION_INPUT_PORTABLE
		private static void AssertTypeMembers(MetadataReader Metadata, string Namespace,
			string Name, string[] Members)
		{
			TypeDefinition type = Metadata.TypeDefinitions.Select(handle =>
				Metadata.GetTypeDefinition(handle)).Single(row =>
				Metadata.GetString(row.Namespace) == Namespace && Metadata.GetString(row.Name) == Name);
			HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
			foreach (FieldDefinitionHandle handle in type.GetFields())
				names.Add(Metadata.GetString(Metadata.GetFieldDefinition(handle).Name));
			foreach (MethodDefinitionHandle handle in type.GetMethods())
				names.Add(Metadata.GetString(Metadata.GetMethodDefinition(handle).Name));
			foreach (PropertyDefinitionHandle handle in type.GetProperties())
				names.Add(Metadata.GetString(Metadata.GetPropertyDefinition(handle).Name));
			foreach (string member in Members) Assert.IsTrue(names.Contains(member),
				Namespace + "." + Name + "." + member);
		}

		private static string LocateAssembly()
		{
			string supplied = Environment.GetEnvironmentVariable("TAF_QUD_BASE");
			if (!string.IsNullOrWhiteSpace(supplied))
			{
				string path = Path.GetFullPath(Path.Combine(supplied, "..", "..", "Managed",
					"Assembly-CSharp.dll"));
				if (File.Exists(path)) return path;
				throw new InvalidOperationException("TAF_QUD_BASE does not resolve Assembly-CSharp.dll: "
					+ supplied);
			}
			foreach (string path in new string[]
			{
				@"F:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed\Assembly-CSharp.dll",
				"/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ_Data/Managed/Assembly-CSharp.dll"
			}) if (File.Exists(path)) return path;
			throw new InvalidOperationException("Set TAF_QUD_BASE to installed Caves of Qud Base.");
		}
		#endif
	}
}
#endif
