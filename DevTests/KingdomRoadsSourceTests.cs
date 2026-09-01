#if TAF_TESTS
using System;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomRoadsSourceTests
	{
		[Test]
		public void LogicalFamilyPreservesAuthorityNestedTypesAndMemberOrder()
		{
			string source = KingdomRoadsLogicalSource.Read();
			Assert.AreEqual(11, KingdomRoadsLogicalSource.FileCount);
			Assert.AreEqual(11, Count(source, "public static partial class KingdomRoads"));
			Assert.AreEqual(1, Count(source, "private struct Errand"));
			Assert.AreEqual(1, Count(source, "private sealed class RoadReceipt"));
			Assert.AreEqual(1, Count(source, "private sealed class RoadRow"));
			StringAssert.DoesNotContain("public static class KingdomRoads", source);

			AssertOrdered(source,
				"internal static void RetryConstruction(",
				"internal static void InspectConstruction(",
				"public const string TallyProperty",
				"private struct Errand",
				"public static List<KingdomRoadRules.WornCell> ReadTally(",
				"public static void OnSettlementPass(",
				"private static List<Errand> Errands(",
				"private static KingdomRoadRules.WearState Apply(",
				"public static bool Lay(",
				"private static void Announce(",
				"private static void RecordSemantic(",
				"public static List<Cell> PathCells(",
				"public static bool Pave(",
				"private static bool SettleRoadTerminal(",
				"private sealed class RoadReceipt",
				"private sealed class RoadRow",
				"private static bool RoadTerminalExact(",
				"private static bool ProjectPaving(",
				"private static bool FreezeRoadReceipt(",
				"private static string EncodeRoadReceipt(",
				"private static bool TryDecodeRoadReceipt(",
				"private static KingdomPhysicalLookupState FindRoadId(",
				"public static string WornLine(");
		}

		[Test]
		public void LogicalFamilyRetainsConstructionReceiptAndMutationProtocol()
		{
			string source = KingdomRoadsLogicalSource.Read();
			StringAssert.Contains("Job.Route != KingdomConstructionRoute.RoadPaving", source);
			StringAssert.Contains("ProjectPaving(Z, Job.TargetKey, cells, Job, out _, out _, out _);", source);
			StringAssert.Contains("KingdomConstruction.UpdatePhysical(ref Updated,", source);
			StringAssert.Contains("KingdomPhysicalPhase.RoadTallySettled", source);
			StringAssert.Contains("KingdomConstruction.Complete(ref Updated)", source);
			StringAssert.Contains("KingdomConstruction.Quarantine(ref Updated, Failure)", source);
			StringAssert.Contains("KingdomConstruction.FindExactId", source);
			StringAssert.Contains("Object.Obliterate(null, Silent: true)", source);
			StringAssert.Contains("KingdomSurvey.ObserveCurrentTopologyInActive", source);
		}

		[Test]
		public void AuthoredEntrancesUseFrozenExactRoutesWhileLegacyGeometryStillSearches()
		{
			string source = KingdomRoadsLogicalSource.Read();
			StringAssert.Contains("public List<ArchitecturePoint> ExactRoute;", source);
			StringAssert.Contains("KingdomRoadRules.TryAuthoredLane", source);
			StringAssert.Contains("KingdomArchitectureRules.IsManagedSnapshotEncoding", source);
			StringAssert.Contains("KingdomRoadRules.TryExactTrace", source);
			StringAssert.Contains("errand.ExactRoute == null", source);
			StringAssert.Contains("KingdomPlotRules.TryDoor(rect, HeartX, HeartY", source);
			StringAssert.DoesNotContain("TryWorldAnchor(snapshot, rect, anchor", source);
		}

		[Test]
		public void RoadsFreezeOpenRoleWidthAndResolvePavingFromThreeIndependentAxes()
		{
			string source = KingdomRoadsLogicalSource.Read();
			StringAssert.Contains("public const string PathRoleProperty", source);
			StringAssert.Contains("public const string PathWidthProperty", source);
			StringAssert.Contains("RoadEntranceKey(anchor.Key)", source);
			StringAssert.Contains("KingdomRoadClearanceRules.ForArchitecture", source);
			StringAssert.Contains("KingdomRoadClearanceRules.TryExpand", source);
			StringAssert.Contains("KingdomRoadPaletteRules.TerrainKey", source);
			StringAssert.Contains("KingdomZoning.Tech(System)", source);
			StringAssert.Contains("KingdomRoadPaletteRules.TryResolveCurrent", source);
			StringAssert.Contains("CopyRoadSemantic(old, floor);", source);
			StringAssert.DoesNotContain("WallBlueprintFor(System.Style", source);
		}

		[Test]
		public void EveryLivedInRoadRungHasADistinctNativeVisual()
		{
			string source = KingdomRoadsLogicalSource.Read();
			StringAssert.Contains("case KingdomRoadRules.WearState.Worn:", source);
			StringAssert.Contains("blueprint = WornBlueprint;", source);
			StringAssert.Contains("state >= (int)KingdomRoadRules.WearState.Worn", source);

			XDocument objects = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			string[] names =
			{
				"r_KingdomGroundWornTrack",
				"r_KingdomGroundTroddenTrack",
				"r_KingdomGroundTroddenPath"
			};
			XElement[] rungs = names.Select(name => objects.Descendants("object").Single(e =>
				(string)e.Attribute("Name") == name)).ToArray();
			CollectionAssert.AreEqual(new[] { "Floor", "Floor", "ArenaFloor" },
				rungs.Select(e => (string)e.Attribute("Inherits")).ToArray());
			string[] tiles = rungs.Select(e => (string)e.Elements("part").Single(p =>
				(string)p.Attribute("Name") == "Render").Attribute("Tile")
					?? "terrain/sw_arena_floor.bmp").ToArray();
			Assert.AreEqual(3, tiles.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
				"road wear must not collapse back into one inherited dirt render");
		}

		private static void AssertOrdered(string Source, params string[] Markers)
		{
			int position = -1;
			for (int i = 0; i < Markers.Length; i++)
			{
				int next = Source.IndexOf(Markers[i], position + 1, StringComparison.Ordinal);
				Assert.Greater(next, position, Markers[i]);
				position = next;
			}
		}

		private static int Count(string Source, string Needle)
		{
			int count = 0;
			int position = 0;
			while ((position = Source.IndexOf(Needle, position, StringComparison.Ordinal)) >= 0)
			{
				count++;
				position += Needle.Length;
			}
			return count;
		}
	}
}
#endif
