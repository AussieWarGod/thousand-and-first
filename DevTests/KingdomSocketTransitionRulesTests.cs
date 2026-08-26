#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomSocketTransitionRulesTests
	{
		[Test]
		public void LogicalAuthorityPreservesEnginePartAndNestedDeclarationOrder()
		{
			string source = KingdomSocketLogicalSource.Read();
			Assert.AreEqual(14, KingdomSocketLogicalSource.FileCount);
			AssertOrdered(source,
				"[Serializable]",
				"public class r_KingdomSocket : IPart",
				"public string LastDesignKey;",
				"public static partial class KingdomSocket",
				"internal static void RetryConstruction(",
				"internal static void InspectConstruction(",
				"private static KingdomPhysicalLookupState FindSocketResult(",
				"private static void ContinueSocketBuild(",
				"private static bool RemoveSocketPredecessor(",
				"private static bool HasBlockingReceipt(",
				"private struct ConvertContext",
				"private sealed class PreparedConvert",
				"private static bool Validate(",
				"public static bool AssessConvert(",
				"private static bool TryPrepareConvert(",
				"public static bool ExecuteConvert(",
				"private static bool ExecutePreparedConvert(",
				"private static bool ProjectConvertOrder(",
				"internal static bool ResumeStrikeSuccessor(",
				"private static bool HasStrikePlotParts(",
				"public static bool OnCleared(",
				"private static void LeaveSocket(",
				"private sealed class PreparedSocketBuild",
				"public static bool BuildOnSocket(",
				"private static bool ExecuteSocketBuild(",
				"public static bool Redress(",
				"public static void OpenConvert(",
				"public static void OpenRedress(");
			Assert.IsFalse(source.Contains("partial class r_KingdomSocket"));
		}

		[Test]
		public void ParseFreezesDirectionalTypedDelta()
		{
			Assert.IsTrue(KingdomSocketTransitionRules.TryParse("shed-to-post", "toolshed",
				"chargingpost", " CRAFT ", "M", "12", "scrap:2", "900",
				out KingdomSocketTransition transition, out string failure), failure);
			Assert.AreEqual("toolshed", transition.FromBuildKey);
			Assert.AreEqual("chargingpost", transition.ToBuildKey);
			Assert.AreEqual("craft", transition.LotType);
			Assert.AreEqual(ArchitectureLotSize.Medium, transition.LotSize);
			Assert.AreEqual(12, transition.WaterDrams);
			Assert.AreEqual(900L, transition.WorkTicks);
			Assert.AreEqual(2, transition.Materials.Get(KingdomMaterial.Scrap));
		}

		[Test]
		public void RouteIdentityIsDirectional()
		{
			string forward = KingdomSocketTransitionRules.IndexKey("a", "b", "craft",
				ArchitectureLotSize.Small);
			string reverse = KingdomSocketTransitionRules.IndexKey("b", "a", "craft",
				ArchitectureLotSize.Small);
			Assert.AreNotEqual(forward, reverse);
		}

		[TestCase("a", "a", "craft", "S", "1", "scrap:1", "1")]
		[TestCase("a", "b", "craft", "bogus", "1", "scrap:1", "1")]
		[TestCase("a", "b", "craft", "S", "-1", "scrap:1", "1")]
		[TestCase("a", "b", "craft", "S", "1", "scrap:1", "0")]
		[TestCase("a", "b", "craft", "S", "1", "unknown:1", "1")]
		public void MalformedOrSelfRouteRefuses(string from, string to, string type,
			string size, string water, string materials, string ticks)
		{
			Assert.IsFalse(KingdomSocketTransitionRules.TryParse("route", from, to, type,
				size, water, materials, ticks, out _, out string failure));
			Assert.IsFalse(string.IsNullOrEmpty(failure));
		}

		[Test]
		public void UndeclaredRefusalNamesBothEndpointsAndRemedy()
		{
			string refusal = KingdomSocketTransitionRules.RefuseUndeclared("tool shed",
				"charging post");
			StringAssert.Contains("tool shed", refusal);
			StringAssert.Contains("charging post", refusal);
			StringAssert.Contains("explicit transition", refusal);
		}

		[Test]
		public void PlanQuoteUsesOnlyDeclaredDeltaAndNoStrike()
		{
			KingdomMaterialTally materials = new KingdomMaterialTally();
			materials.Set(KingdomMaterial.Scrap, 3);
			KingdomSocketRules.ConversionQuote quote = KingdomSocketRules.AssessPlanChange(
				new KingdomSocketTransition
				{
					WaterDrams = 7, WorkTicks = 450L, Materials = materials
				});
			Assert.AreEqual(0, quote.StrikeEffort);
			Assert.AreEqual(0, quote.Salvage.Total());
			Assert.AreEqual(7, quote.NewDrams);
			Assert.AreEqual(3, quote.NetMaterials.Get(KingdomMaterial.Scrap));
			Assert.AreEqual(450L, quote.WorkTicks);
		}

		[Test]
		public void ShippedEarlyHousingRoutesCoverEveryExactSizeAndStayCheaper()
		{
			XmlDocument buildings = new XmlDocument();
			buildings.Load(Path.Combine(TestMain.RepositoryRoot, "KingdomBuildings.xml"));
			Dictionary<string, XmlElement> byKey = new Dictionary<string, XmlElement>(
				StringComparer.Ordinal);
			foreach (XmlElement building in buildings.SelectNodes("//building"))
				byKey[building.GetAttribute("Key")] = building;

			XmlDocument routes = new XmlDocument();
			routes.Load(Path.Combine(TestMain.RepositoryRoot, "Architecture",
				"KingdomArchitectureTransitions.xml"));
			XmlNodeList declared = routes.SelectNodes("/KingdomArchitectureTransitions/transition");
			Assert.AreEqual(24, declared.Count);
			HashSet<string> mappings = new HashSet<string>(StringComparer.Ordinal);
			foreach (string file in Directory.GetFiles(Path.Combine(TestMain.RepositoryRoot,
				"Architecture"), "KingdomArchitectures*.xml"))
			{
				XmlDocument architecture = new XmlDocument();
				architecture.Load(file);
				foreach (XmlElement binding in architecture.SelectNodes(
					"/KingdomArchitectures/plan/binding"))
					foreach (XmlElement tier in binding.SelectNodes("tier"))
						mappings.Add(tier.GetAttribute("BuildKey") + ":"
							+ binding.GetAttribute("Type") + ":" + binding.GetAttribute("Size"));
			}
			string[] sizes = { "S", "M", "L", "XL" };
			string[,] pairs =
			{
				{ "tent", "hut" }, { "tentrow", "hutyard" },
				{ "tent", "mudhut" }, { "tentrow", "mudhutcourt" },
				{ "tent", "blockhut" }, { "tentrow", "blockyard" }
			};
			HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
			foreach (XmlElement route in declared)
			{
				string from = route.GetAttribute("From");
				string to = route.GetAttribute("To");
				string identity = from + ">" + to + ":" + route.GetAttribute("Size");
				Assert.IsTrue(identities.Add(identity), identity);
				Assert.IsFalse(identities.Contains(to + ">" + from + ":"
					+ route.GetAttribute("Size")), "undeclared reverse became present: " + identity);
				XmlElement target = byKey[to];
				string mappingSuffix = ":" + route.GetAttribute("Type") + ":"
					+ route.GetAttribute("Size");
				Assert.IsTrue(mappings.Contains(from + mappingSuffix), identity + " source mapping");
				Assert.IsTrue(mappings.Contains(to + mappingSuffix), identity + " target mapping");
				Assert.Less(int.Parse(route.GetAttribute("Water")),
					int.Parse(target.GetAttribute("Cost")), identity + " water");
				Assert.Less(MaterialTotal(route.GetAttribute("Materials")),
					MaterialTotal(target.GetAttribute("Materials")), identity + " materials");
				Assert.Less(long.Parse(route.GetAttribute("Ticks")),
					long.Parse(target.GetAttribute("Ticks")), identity + " labour");
			}
			for (int pair = 0; pair < pairs.GetLength(0); pair++)
				for (int size = 0; size < sizes.Length; size++)
					Assert.IsTrue(identities.Contains(pairs[pair, 0] + ">" + pairs[pair, 1]
						+ ":" + sizes[size]), "missing exact route");
			Assert.AreEqual("all,!common,!eater", byKey["hut"].GetAttribute("Styles"));
			Assert.AreEqual("common", byKey["mudhut"].GetAttribute("Styles"));
			Assert.AreEqual("eater", byKey["blockhut"].GetAttribute("Styles"));
			AssertEveryDeclaredTargetVariantRetainsSourceStatefulFixtures(declared);
		}

		[Test]
		public void TransitionUiPreparesOneSnapshotBeforeConfirmationAndDebit()
		{
			string socket = KingdomSocketLogicalSource.Read();
			StringAssert.Contains("TryPrepareConvert(System, zone, target, chosen.Key, skinKey", socket);
			StringAssert.Contains("KingdomArchitecturePreview.TryRenderTransition(conversion.Architecture", socket);
			StringAssert.Contains("Popup.PickOption(Title: \"Preview exact change:", socket);
			StringAssert.Contains("ExecutePreparedConvert(System, zone, target, conversion", socket);
			StringAssert.Contains("[change: \" + transition.WaterDrams", socket);
			StringAssert.Contains("!KingdomSocketTransitions.TryGet(currentKey, entry.Key", socket);
			Assert.Less(socket.IndexOf("TryPrepareConvert(System, zone, target, chosen.Key, skinKey",
				StringComparison.Ordinal), socket.IndexOf("Popup.PickOption(Title: \"Preview exact change:",
				StringComparison.Ordinal));
			Assert.Less(socket.IndexOf("Popup.PickOption(Title: \"Preview exact change:",
				StringComparison.Ordinal), socket.IndexOf("ExecutePreparedConvert(System, zone, target, conversion",
				StringComparison.Ordinal));
		}

		[Test]
		public void ExistingDurableTransitionReceiptNamesAndSchemaLastPublicationStayFrozen()
		{
			string source = TestMain.ReadRepositoryText("Growth/KingdomSocketTransitions.cs");
			StringAssert.Contains("ReceiptSchemaProperty = \"r_TAF_SocketTransitionSchema\"", source);
			StringAssert.Contains("ReceiptKeyProperty = \"r_TAF_SocketTransitionKey\"", source);
			StringAssert.Contains("ReceiptBeforeHashProperty = \"r_TAF_SocketTransitionBefore\"", source);
			StringAssert.Contains("ReceiptAfterHashProperty = \"r_TAF_SocketTransitionAfter\"", source);
			StringAssert.Contains("ReceiptJobProperty = \"r_TAF_SocketTransitionJob\"", source);
			int bind = source.IndexOf("internal static bool BindReceipt", StringComparison.Ordinal);
			int key = source.IndexOf("Owner.SetStringProperty(ReceiptKeyProperty", bind,
				StringComparison.Ordinal);
			int before = source.IndexOf("Owner.SetStringProperty(ReceiptBeforeHashProperty", key,
				StringComparison.Ordinal);
			int after = source.IndexOf("Owner.SetStringProperty(ReceiptAfterHashProperty", before,
				StringComparison.Ordinal);
			int job = source.IndexOf("Owner.SetStringProperty(ReceiptJobProperty", after,
				StringComparison.Ordinal);
			int schema = source.IndexOf("Owner.SetIntProperty(ReceiptSchemaProperty, 1)", job,
				StringComparison.Ordinal);
			Assert.Greater(key, bind);
			Assert.Greater(before, key);
			Assert.Greater(after, before);
			Assert.Greater(job, after);
			Assert.Greater(schema, job);
		}

		private static int MaterialTotal(string Text)
		{
			int total = 0;
			foreach (string term in Text.Split(','))
				total += int.Parse(term.Substring(term.IndexOf(':') + 1));
			return total;
		}

		private sealed class LayoutFixture
		{
			public string Name;
			public string Map;
			public string Palette;
		}

		private static void AssertEveryDeclaredTargetVariantRetainsSourceStatefulFixtures(
			XmlNodeList Declared)
		{
			Dictionary<string, XmlElement> maps = new Dictionary<string, XmlElement>(
				StringComparer.Ordinal);
			Dictionary<string, XmlElement> palettes = new Dictionary<string, XmlElement>(
				StringComparer.Ordinal);
			Dictionary<string, List<LayoutFixture>> layouts =
				new Dictionary<string, List<LayoutFixture>>(StringComparer.Ordinal);
			foreach (string file in Directory.GetFiles(Path.Combine(TestMain.RepositoryRoot,
				"Architecture"), "KingdomArchitectures*.xml"))
			{
				XmlDocument architecture = new XmlDocument();
				architecture.Load(file);
				foreach (XmlElement map in architecture.SelectNodes(
					"/KingdomArchitectures/map")) maps[map.GetAttribute("Key")] = map;
				foreach (XmlElement palette in architecture.SelectNodes(
					"/KingdomArchitectures/palette"))
					palettes[palette.GetAttribute("Key")] = palette;
				foreach (XmlElement binding in architecture.SelectNodes(
					"/KingdomArchitectures/plan/binding"))
				{
					foreach (XmlElement tier in binding.SelectNodes("tier"))
					{
						string mapping = tier.GetAttribute("BuildKey") + ":"
							+ binding.GetAttribute("Type") + ":" + binding.GetAttribute("Size");
						if (!layouts.TryGetValue(mapping, out List<LayoutFixture> choices))
						{
							choices = new List<LayoutFixture>();
							layouts.Add(mapping, choices);
						}
						foreach (XmlElement variant in tier.SelectNodes("variant"))
						{
							choices.Add(new LayoutFixture
							{
								Name = variant.GetAttribute("Key"),
								Map = string.IsNullOrEmpty(variant.GetAttribute("Map"))
									? tier.GetAttribute("Map") : variant.GetAttribute("Map"),
								Palette = string.IsNullOrEmpty(variant.GetAttribute("Palette"))
									? tier.GetAttribute("Palette") : variant.GetAttribute("Palette")
							});
						}
					}
				}
			}
			foreach (XmlElement route in Declared)
			{
				string suffix = ":" + route.GetAttribute("Type") + ":"
					+ route.GetAttribute("Size");
				string sourceKey = route.GetAttribute("From") + suffix;
				string targetKey = route.GetAttribute("To") + suffix;
				Assert.IsTrue(layouts.TryGetValue(sourceKey, out List<LayoutFixture> sources),
					sourceKey);
				Assert.IsTrue(layouts.TryGetValue(targetKey, out List<LayoutFixture> targets),
					targetKey);
				foreach (LayoutFixture source in sources)
				{
					HashSet<string> retained = StatefulFixtureSignatures(source, maps, palettes);
					string sourceMain = MainCoordinate(source, maps);
					foreach (LayoutFixture target in targets)
					{
						string context = route.GetAttribute("Key") + " target variant " + target.Name;
						CollectionAssert.IsSubsetOf(retained,
							StatefulFixtureSignatures(target, maps, palettes), context);
						Assert.AreEqual(sourceMain, MainCoordinate(target, maps), context + " main");
					}
				}
			}
		}

		private static HashSet<string> StatefulFixtureSignatures(LayoutFixture Layout,
			Dictionary<string, XmlElement> Maps, Dictionary<string, XmlElement> Palettes)
		{
			XmlElement map = Maps[Layout.Map];
			XmlElement palette = Palettes[Layout.Palette];
			Dictionary<char, XmlElement> glyphs = new Dictionary<char, XmlElement>();
			foreach (XmlElement glyph in map.SelectNodes("glyph"))
				glyphs[glyph.GetAttribute("Char")[0]] = glyph;
			HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
			int y = 0;
			foreach (XmlElement row in map.SelectNodes("row"))
			{
				string cells = row.GetAttribute("Cells");
				for (int x = 0; x < cells.Length; x++)
				{
					if (!glyphs.TryGetValue(cells[x], out XmlElement glyph)
						|| glyph.GetAttribute("Stateful") != "yes"
						|| glyph.GetAttribute("Object") == "$building") continue;
					string slotKey = glyph.GetAttribute("Object").Substring(1);
					XmlElement slot = (XmlElement)palette.SelectSingleNode(
						"slot[@Key='" + slotKey + "']");
					string anchor = glyph.GetAttribute("Anchors");
					result.Add(anchor + "|" + x + "|" + y + "|"
						+ slot.GetAttribute("Blueprint") + "|" + slot.GetAttribute("Material")
						+ "|" + slot.GetAttribute("MinTech") + "|" + slot.GetAttribute("Knowledge")
						+ "|" + slot.GetAttribute("Power") + "|" + slot.GetAttribute("Natural"));
				}
				y++;
			}
			return result;
		}

		private static string MainCoordinate(LayoutFixture Layout,
			Dictionary<string, XmlElement> Maps)
		{
			XmlElement map = Maps[Layout.Map];
			char mainGlyph = '\0';
			foreach (XmlElement glyph in map.SelectNodes("glyph"))
				if (glyph.GetAttribute("Object") == "$building")
					mainGlyph = glyph.GetAttribute("Char")[0];
			int y = 0;
			foreach (XmlElement row in map.SelectNodes("row"))
			{
				int x = row.GetAttribute("Cells").IndexOf(mainGlyph);
				if (x >= 0) return x + "|" + y;
				y++;
			}
			return null;
		}

		private static void AssertOrdered(string Source, params string[] Needles)
		{
			int previous = -1;
			for (int i = 0; i < Needles.Length; i++)
			{
				int next = Source.IndexOf(Needles[i], previous + 1, StringComparison.Ordinal);
				Assert.Greater(next, previous, "missing or out of order: " + Needles[i]);
				previous = next;
			}
		}
	}
}
#endif
