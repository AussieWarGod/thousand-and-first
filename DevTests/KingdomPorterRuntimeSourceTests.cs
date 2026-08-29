#if TAF_TESTS
using System;
using NUnit.Framework;

using ThousandAndFirst.Simulation.City;

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
			string porters = KingdomPortersLogicalSource.Read();
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
			string porters = KingdomPortersLogicalSource.Read();
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
			string porters = KingdomPortersLogicalSource.Read();
			StringAssert.Contains("KingdomRoadRules.TryTrace(passable", porters);
			StringAssert.Contains("KingdomRoads.AppliedState(cell) == KingdomRoadRules.WearState.Paved", porters);
			StringAssert.Contains("(long)paved * KingdomItineraryRules.RoadDiscountPercent", porters);
			StringAssert.Contains("ResidentZone(node.ZoneId, Z)", porters);
		}

		[Test]
		public void IntermediateLegHandsOffButOnlyTheFinalLegCloses()
		{
			string porters = KingdomPortersLogicalSource.Read();
			int final = porters.IndexOf("row.TryLeg(row.LegCount - 1, out final)",
				StringComparison.Ordinal);
			int close = porters.IndexOf("Close(system, Part.JobId", final,
				StringComparison.Ordinal);
			int handoff = porters.IndexOf("Handoff(system, Part.JobId", close,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(final, 0);
			Assert.Greater(close, final);
			Assert.Greater(handoff, close);
			int method = porters.IndexOf("private static void Handoff(", StringComparison.Ordinal);
			int nextMethod = porters.IndexOf("\n\t\tprivate static", method + 1,
				StringComparison.Ordinal);
			int unbind = porters.IndexOf("!KingdomResidents.Unbind", method,
				StringComparison.Ordinal);
			int remove = porters.IndexOf("body.Obliterate()", method,
				StringComparison.Ordinal);
			Assert.Greater(nextMethod, method);
			Assert.Greater(unbind, method);
			Assert.Greater(remove, unbind,
				"a failed registry publication must leave the visible body repairable");
			Assert.Less(remove, nextMethod,
				"handoff proof must not borrow a removal from another method");
		}

		[Test]
		public void SourceEndIsAnActualGrowingWorkWhenOneExists()
		{
			string porters = KingdomPortersLogicalSource.Read();
			StringAssert.Contains("work.RunState.Kind != KingdomWorkKind.Growing", porters);
			StringAssert.Contains("X = work.AnchorX", porters);
			StringAssert.Contains("Y = work.AnchorY", porters);
		}

		[Test]
		public void ConstructionInputMoveKeepsFrozenGoalWhileOtherCargoReprojects()
		{
			Assert.IsFalse(KingdomPorterRouteRules.ReprojectsOnMove(
				KingdomDeliveryCargoAuthority.ConstructionInput));
			Assert.IsTrue(KingdomPorterRouteRules.ReprojectsOnMove(
				KingdomDeliveryCargoAuthority.ScalarStock));
			Assert.IsTrue(KingdomPorterRouteRules.ReprojectsOnMove(
				KingdomDeliveryCargoAuthority.CarryBookManifest));

			string porters = KingdomPortersLogicalSource.Read();
			int place = porters.IndexOf("private static void Place(", StringComparison.Ordinal);
			int reprojectMethod = porters.IndexOf("private static void Reproject(", place,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(place, 0);
			Assert.Greater(reprojectMethod, place);
			string placeBody = porters.Substring(place, reprojectMethod - place);
			int policy = placeBody.IndexOf(
				"if (!KingdomPorterRouteRules.ReprojectsOnMove(row.DeliveryCargoAuthority))",
				StringComparison.Ordinal);
			int keep = placeBody.IndexOf("KeepExactGoal(Z, bindingId);", policy,
				StringComparison.Ordinal);
			int ordinary = placeBody.IndexOf("Reproject(System, Z, row, fix, TimeTicks, bindingId);",
				keep, StringComparison.Ordinal);
			Assert.GreaterOrEqual(policy, 0);
			Assert.Greater(keep, policy);
			Assert.Greater(ordinary, keep);

			int keeper = porters.IndexOf("private static void KeepExactGoal(", place,
				StringComparison.Ordinal);
			Assert.Greater(keeper, place);
			string keeperBody = porters.Substring(keeper, reprojectMethod - keeper);
			StringAssert.Contains("brain.Wake();", keeperBody);
			StringAssert.Contains("Cell moving = brain.MovingTo();", keeperBody);
			StringAssert.Contains("Walk(body, Z, part.DestX, part.DestY);", keeperBody);
			StringAssert.DoesNotContain("System.Jobs", keeperBody);
			int mutation = porters.IndexOf("KingdomItineraryRules.TryReproject(", reprojectMethod,
				StringComparison.Ordinal);
			int defensivePolicy = porters.IndexOf(
				"if (!KingdomPorterRouteRules.ReprojectsOnMove(row.DeliveryCargoAuthority))",
				reprojectMethod, StringComparison.Ordinal);
			Assert.Greater(defensivePolicy, reprojectMethod);
			Assert.Greater(mutation, defensivePolicy,
				"authority-2 must fail closed before any itinerary mutation");
		}

	}
}
#endif
