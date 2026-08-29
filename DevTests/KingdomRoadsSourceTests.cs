#if TAF_TESTS
using System;
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
			Assert.AreEqual(10, KingdomRoadsLogicalSource.FileCount);
			Assert.AreEqual(10, Count(source, "public static partial class KingdomRoads"));
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
			StringAssert.Contains("KingdomArchitectureRules.IsCurrentSnapshotEncoding", source);
			StringAssert.Contains("KingdomRoadRules.TryExactTrace", source);
			StringAssert.Contains("errand.ExactRoute == null", source);
			StringAssert.Contains("KingdomPlotRules.TryDoor(rect, HeartX, HeartY", source);
			StringAssert.DoesNotContain("TryWorldAnchor(snapshot, rect, anchor", source);
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
