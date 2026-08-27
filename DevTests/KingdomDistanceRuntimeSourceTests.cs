#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Production-edge proof for LIVING-CITY-ARCHITECTURE §3.10. Pure matrix tests
	/// cannot prove the rendered ground is measured or that carry consumes the measured answer.</summary>
	[TestFixture]
	public class KingdomDistanceRuntimeSourceTests
	{
		[Test]
		public void CarryConsumesExactMeasuredPlanInsteadOfZoneRowProxy()
		{
			string city = KingdomCityLogicalSource.Read();
			string carry = Between(city, "private static KingdomCityState CarryKind(",
				"private static KingdomCityState Reconcile(");
			StringAssert.Contains("KingdomCentralLogistics.TryQueueScalar(System, state", carry);
			StringAssert.DoesNotContain("KingdomDistanceRuntime.Land", carry);
			StringAssert.DoesNotContain("KingdomDistanceRuntime.Commit", carry);
			StringAssert.DoesNotContain("TryApplyTransfer", carry);
			StringAssert.DoesNotContain("TryPlanTransfer", carry);

			string runtime = RuntimeSource();
			StringAssert.Contains("cache.TryCompose(source, sourceEndpoint.EndpointId", runtime);
			StringAssert.Contains("KingdomLogisticsRules.TryNearestHolder", runtime);
			StringAssert.Contains("KingdomLogisticsRules.TryNoNearerHolder", runtime);
			StringAssert.Contains("sourceEndpoint.Amount(kind)", runtime);
			StringAssert.Contains("targetEndpoint.Room(kind)", runtime);

			string central = KingdomCentralLogisticsLogicalSource.Read();
			StringAssert.Contains("KingdomLogisticsRules.TryPlanSnapshot", central);
			StringAssert.Contains("TryExactScalarAmount(survey, seed, source: true", central);
			StringAssert.Contains("TryDebitScalar(survey, seed, total", central);
		}

		[Test]
		public void RenderMeasuresRealWalkabilityRoadCellsAndShaftReceipts()
		{
			string runtime = RuntimeSource();
			StringAssert.Contains("KingdomRoads.Walkable(cell)", runtime);
			StringAssert.Contains("KingdomRoads.AppliedState(cell) == KingdomRoadRules.WearState.Paved",
				runtime);
			StringAssert.Contains("KingdomDelveLink.TryReadPhysicalReceipt(head.ZoneId, out receipt)",
				runtime);
			StringAssert.Contains("KingdomDistanceSliceRules.TryMeasureExact(passable, paved", runtime);
			StringAssert.Contains("cache.Matrix.TryWriteZone(zoneIndex, ids, edges, pairs", runtime);
		}

		[Test]
		public void ObservationRunsOnlyAtGroundHandoffsAndNeverAtReckon()
		{
			string city = KingdomCityLogicalSource.Read();
			Assert.AreEqual(2, Count(city, "KingdomDistanceRuntime.Observe("));
			string checkIn = Between(city, "public static void CheckIn(",
				"public static void CheckOut(");
			AssertOrdered(checkIn, "state = Reify(", "KingdomDistanceRuntime.Observe(",
				"KingdomCentralLogistics.RecoverPreparedSources(",
				"KingdomCentralLogistics.SettleScalarArrivals(", "state = Carry(",
				"KingdomCentralLogistics.StartPlanned(");
			string reckon = Between(city, "private static KingdomCityState Reckon(",
				"private static KingdomCityState Networks(");
			StringAssert.DoesNotContain("KingdomDistance", reckon);
		}

		[Test]
		public void SparseCacheIsBoundedAndAbsentFromSaveWire()
		{
			string rules = string.Join("\n",
				Source("KingdomDistanceRules.cs"),
				Source("KingdomDistanceMatrix.cs"),
				Source("KingdomDistanceMatrix.Operations.cs"));
			StringAssert.Contains("MaxWorkEdgeEntries", rules);
			StringAssert.Contains("MaxSamePairEntries", rules);
			StringAssert.Contains("MaxEndpointsForZone(int zoneIndex)", rules);
			StringAssert.Contains("int[][] endpointIds", rules);
			StringAssert.DoesNotContain("worksPerZone", rules);
			string runtime = RuntimeSource();
			StringAssert.Contains("cache.Matrix.MaxEndpointsForZone(zoneIndex)", runtime);
			StringAssert.Contains("if (candidates[i].Required)", runtime);

			string book = KingdomCityBookLogicalSource.Read();
			AssertOrdered(book, "[NonSerialized]", "internal KingdomDistanceCache DistanceCache");
			string slice = string.Join("\n",
				Source("KingdomDistancePoint.cs"),
				Source("KingdomDistanceSliceRules.cs"),
				Source("KingdomDistanceSliceRules.Pathfinding.cs"));
			StringAssert.DoesNotContain("XRL", slice);
			StringAssert.DoesNotContain("The.Game", slice);
		}

		[Test]
		public void RuntimeSplitKeepsOnePartialAuthorityAndCandidateNested()
		{
			string runtime = RuntimeSource();
			Assert.AreEqual(4, Count(runtime, "internal static partial class KingdomDistanceRuntime"));
			Assert.AreEqual(1, Count(runtime, "private sealed class Candidate"));
			StringAssert.Contains("private sealed class Candidate", Source("KingdomDistanceRuntime.cs"));
			StringAssert.DoesNotContain("private sealed class Candidate",
				Source("KingdomDistanceRuntime.PlanningAndTransfer.cs"));
			StringAssert.DoesNotContain("private sealed class Candidate",
				Source("KingdomDistanceRuntime.CandidatesAndSelection.cs"));
			StringAssert.DoesNotContain("private sealed class Candidate",
				Source("KingdomDistanceRuntime.PortalsAndHashing.cs"));
		}

		private static string RuntimeSource()
		{
			return string.Join("\n",
				Source("KingdomDistanceRuntime.cs"),
				Source("KingdomDistanceRuntime.PlanningAndTransfer.cs"),
				Source("KingdomDistanceRuntime.CandidatesAndSelection.cs"),
				Source("KingdomDistanceRuntime.PortalsAndHashing.cs"));
		}

		private static string Source(string file)
		{
			return TestMain.ReadRepositoryText(Path.Combine("Simulation", "City", file));
		}

		private static string Between(string source, string startTerm, string endTerm)
		{
			int start = source.IndexOf(startTerm, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, "missing source boundary: " + startTerm);
			int end = source.IndexOf(endTerm, start + startTerm.Length, StringComparison.Ordinal);
			Assert.Greater(end, start, "missing source boundary: " + endTerm);
			return source.Substring(start, end - start);
		}

		private static int Count(string source, string term)
		{
			int count = 0;
			int at = 0;
			while ((at = source.IndexOf(term, at, StringComparison.Ordinal)) >= 0)
			{
				count++;
				at += term.Length;
			}
			return count;
		}

		private static void AssertOrdered(string source, params string[] terms)
		{
			int previous = -1;
			for (int i = 0; i < terms.Length; i++)
			{
				int found = source.IndexOf(terms[i], previous + 1, StringComparison.Ordinal);
				Assert.Greater(found, previous, "missing/out-of-order source term: " + terms[i]);
				previous = found;
			}
		}
	}
}
#endif
