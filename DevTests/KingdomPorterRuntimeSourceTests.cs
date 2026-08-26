#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Native-edge wiring proof for LIVING-CITY-ARCHITECTURE §3.7 and BUILDING-
	/// CATALOGUE-BRIEF Addendum 12(c-f). Pure itinerary tests cannot prove the engine adapter
	/// actually consumes the exact graph path, shaft receipt, and physical road cells.</summary>
	[TestFixture]
	public class KingdomPorterRuntimeSourceTests
	{
		[Test]
		public void ProductionFreezesEveryGraphHopAndRefusesTruncation()
		{
			string jobs = KingdomJobRegistryLogicalSource.Read();
			string porters = Source("KingdomPorters.cs");
			StringAssert.Contains("graph.TryPath(destination, source, resolved, out count, out fault)", jobs);
			StringAssert.Contains("count + 1 > KingdomItineraryRules.MaxLegs", jobs);
			StringAssert.Contains("KingdomJobRules.TryPorterPath(graph, Z.ZoneID, sourceZoneId", porters);
			StringAssert.Contains("for (int i = 1; i < pathCount; i++)", porters);
			StringAssert.Contains("graph.TryNode(path[i], out node)", porters);
			StringAssert.Contains("TryPassage(System, graph, path[i], path[i + 1]", porters);
			StringAssert.DoesNotContain("EdgeToward(Z.ZoneID", porters);
		}

		[Test]
		public void HorizontalAndVerticalHandoffsUseTheirRealConnectionFacts()
		{
			string jobs = KingdomJobRegistryLogicalSource.Read();
			string porters = Source("KingdomPorters.cs");
			StringAssert.Contains("KingdomDistanceRules.StepBetween(", jobs);
			StringAssert.Contains("? KingdomZoneStep.None : step", jobs);
			StringAssert.Contains("Step != KingdomZoneStep.Up && Step != KingdomZoneStep.Down", porters);
			StringAssert.Contains("KingdomJobRules.TryMirror(ExitX, ExitY, Step", porters);
			StringAssert.Contains("KingdomDelveLink.TryReadPhysicalReceipt(head.ZoneId, out receipt)", porters);
			StringAssert.Contains("receipt.FootZoneId != foot.ZoneId", porters);
			StringAssert.Contains("ExitX = EnterX = (short)receipt.X", porters);
		}

		[Test]
		public void RoadPriceReadsOnlyTheTracedPhysicalCells()
		{
			string porters = Source("KingdomPorters.cs");
			StringAssert.Contains("KingdomRoadRules.TryTrace(passable", porters);
			StringAssert.Contains("KingdomRoads.AppliedState(cell) == KingdomRoadRules.WearState.Paved", porters);
			StringAssert.Contains("(long)paved * KingdomItineraryRules.RoadDiscountPercent", porters);
			StringAssert.Contains("ResidentZone(node.ZoneId, Z)", porters);
		}

		[Test]
		public void IntermediateLegHandsOffButOnlyTheFinalLegCloses()
		{
			string porters = Source("KingdomPorters.cs");
			int final = porters.IndexOf("row.TryLeg(row.LegCount - 1, out final)",
				StringComparison.Ordinal);
			int close = porters.IndexOf("Close(system, Part.JobId", final,
				StringComparison.Ordinal);
			int handoff = porters.IndexOf("Handoff(system, Part.JobId", close,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(final, 0);
			Assert.Greater(close, final);
			Assert.Greater(handoff, close);
			int method = porters.IndexOf("private static void Handoff", StringComparison.Ordinal);
			int unbind = porters.IndexOf("KingdomResidents.Unbind", method,
				StringComparison.Ordinal);
			int remove = porters.IndexOf("Body.Obliterate()", method,
				StringComparison.Ordinal);
			Assert.Greater(unbind, method);
			Assert.Greater(remove, unbind,
				"a failed registry publication must leave the visible body repairable");
		}

		[Test]
		public void SourceEndIsAnActualGrowingWorkWhenOneExists()
		{
			string porters = Source("KingdomPorters.cs");
			StringAssert.Contains("work.RunState.Kind != KingdomWorkKind.Growing", porters);
			StringAssert.Contains("X = work.AnchorX", porters);
			StringAssert.Contains("Y = work.AnchorY", porters);
		}

		private static string Source(string file)
		{
			return TestMain.ReadRepositoryText(Path.Combine("Simulation", "City", file));
		}
	}
}
#endif
