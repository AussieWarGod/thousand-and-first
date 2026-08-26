#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomGatehouseRulesTests
	{
		[TestCase(KingdomRules.Frontier.North, KingdomGatehouseOrientation.North, 10, 1)]
		[TestCase(KingdomRules.Frontier.East, KingdomGatehouseOrientation.East, 18, 10)]
		[TestCase(KingdomRules.Frontier.South, KingdomGatehouseOrientation.South, 10, 18)]
		[TestCase(KingdomRules.Frontier.West, KingdomGatehouseOrientation.West, 1, 10)]
		public void RoadEndpointFreezesDeterministicInwardOrientation(
			KingdomRules.Frontier edge, KingdomGatehouseOrientation expected, int x, int y)
		{
			Assert.IsTrue(KingdomGatehouseRules.TryPlan(20, 20, edge, 10, 10,
				out KingdomGatehousePlan plan));
			Assert.AreEqual(expected, plan.Orientation);
			Assert.AreEqual(x, plan.GateX);
			Assert.AreEqual(y, plan.GateY);
			Assert.AreEqual(3, plan.X2 - plan.X1 + 1);
			Assert.AreEqual(3, plan.Y2 - plan.Y1 + 1);
		}

		[Test]
		public void StoneGuardsTimberWatchesAndOpenCenterlineAreExact()
		{
			Assert.IsTrue(KingdomGatehouseRules.TryPlan(80, 25,
				KingdomRules.Frontier.North, 40, 12, out KingdomGatehousePlan plan));
			HashSet<string> occupied = new HashSet<string>();
			int stone = 0;
			int timber = 0;
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
			{
				Assert.IsTrue(KingdomGatehouseRules.TrySatellite(plan, i,
					out KingdomGatehouseCell cell));
				Assert.IsTrue(occupied.Add(cell.X + "," + cell.Y));
				if (cell.Blueprint == KingdomGatehouseRules.StoneBlueprint) stone++;
				if (cell.Blueprint == KingdomGatehouseRules.WatchBlueprint) timber++;
			}
			Assert.AreEqual(4, stone, "four material-honest stone guard walls");
			Assert.AreEqual(2, timber, "two functional timber watch benches");
			for (int i = 0; i < KingdomGatehouseRules.PassageCount; i++)
			{
				Assert.IsTrue(KingdomGatehouseRules.TryPassage(plan, i,
					out KingdomGatehouseCell passage));
				Assert.IsFalse(occupied.Contains(passage.X + "," + passage.Y),
					"the road centerline must never receive a wall or fixture");
			}
			Assert.AreEqual(KingdomGatehouseRules.FootprintCells,
				occupied.Count + KingdomGatehouseRules.PassageCount);
		}

		[Test]
		public void RoadPassageHasAnApproachOnBothSidesOfTheDoor()
		{
			Assert.IsTrue(KingdomGatehouseRules.TryPlan(20, 20,
				KingdomRules.Frontier.West, 10, 10, out KingdomGatehousePlan plan));
			Assert.IsTrue(KingdomGatehouseRules.TryApproach(plan, 0,
				out KingdomGatehouseCell outside));
			Assert.IsTrue(KingdomGatehouseRules.TryPassage(plan, 0,
				out KingdomGatehouseCell door));
			Assert.IsTrue(KingdomGatehouseRules.TryPassage(plan, 1,
				out KingdomGatehouseCell throat));
			Assert.IsTrue(KingdomGatehouseRules.TryPassage(plan, 2,
				out KingdomGatehouseCell room));
			Assert.IsTrue(KingdomGatehouseRules.TryApproach(plan, 1,
				out KingdomGatehouseCell inside));
			Assert.AreEqual(outside.X + 1, door.X);
			Assert.AreEqual(door.X + 1, throat.X);
			Assert.AreEqual(throat.X + 1, room.X);
			Assert.AreEqual(room.X + 1, inside.X);
			Assert.AreEqual(outside.Y, inside.Y);
		}

		[Test]
		public void FrozenPlanRoundTripsCanonicallyAndRejectsMutation()
		{
			Assert.IsTrue(KingdomGatehouseRules.TryPlan(80, 25,
				KingdomRules.Frontier.South, 40, 12, out KingdomGatehousePlan plan));
			Assert.IsTrue(KingdomGatehouseRules.TryEncode(plan, out string encoded));
			Assert.IsTrue(KingdomGatehouseRules.TryDecode(encoded,
				out KingdomGatehousePlan decoded));
			Assert.AreEqual(plan.GateX, decoded.GateX);
			Assert.AreEqual(plan.GateY, decoded.GateY);
			Assert.IsFalse(KingdomGatehouseRules.TryDecode(encoded + ",0", out _));
			Assert.IsFalse(KingdomGatehouseRules.TryDecode(
				encoded.Replace("v1,3", "v1,03"), out _));
		}

		[Test]
		public void TypedStrikeIsNonPlotExactAndCannotBePartiallyInvented()
		{
			Assert.IsTrue(KingdomGatehouseRules.IsNetworkStrike("gatehouse", false,
				9, 1, 11, 3, "root-id", 6));
			Assert.IsFalse(KingdomGatehouseRules.IsNetworkStrike("gatehouse", true,
				9, 1, 11, 3, "root-id", 6));
			Assert.IsFalse(KingdomGatehouseRules.IsNetworkStrike("stone-house", false,
				9, 1, 11, 3, "root-id", 6));
			Assert.IsFalse(KingdomGatehouseRules.IsNetworkStrike("gatehouse", false,
				9, 1, 11, 3, "root-id", 5));
		}

		[Test]
		public void ConstructionWireRoundTripsSixNonPlotOwnedTargetsExactly()
		{
			KingdomStrikeIntent intent = new KingdomStrikeIntent
			{
				DisplayName = "gatehouse gate",
				BuildKey = "gatehouse",
				TargetDisplayName = null,
				SalvageClaim = new KingdomMaterialDebitCost().ToClaimString(),
				HasPlot = false,
				X1 = 9,
				Y1 = 1,
				X2 = 11,
				Y2 = 3,
				PlotId = "root-id",
				Effort = 17,
				Targets = new List<KingdomStrikeTarget>()
			};
			for (int i = 0; i < 6; i++)
			{
				intent.Targets.Add(new KingdomStrikeTarget
				{
					Id = "sat-" + i,
					Blueprint = i < 4 ? KingdomGatehouseRules.StoneBlueprint
						: KingdomGatehouseRules.WatchBlueprint,
					X = 9 + i % 3,
					Y = 1 + i / 3
				});
			}
			Assert.IsTrue(KingdomConstructionRules.TryEncodeStrikeIntent(intent,
				out string encoded));
			Assert.IsTrue(KingdomConstructionRules.TryDecodeStrikeIntent(encoded,
				out KingdomStrikeIntent decoded));
			Assert.IsFalse(decoded.HasPlot);
			Assert.AreEqual("root-id", decoded.PlotId);
			Assert.AreEqual(6, decoded.Targets.Count);
			intent.Targets.RemoveAt(5);
			Assert.IsFalse(KingdomConstructionRules.TryEncodeStrikeIntent(intent, out _));
		}

		[Test]
		public void AFrontierEndpointWithoutBothApproachesRefusesInsteadOfMoving()
		{
			Assert.IsFalse(KingdomGatehouseRules.TryPlan(3, 3,
				KingdomRules.Frontier.North, 1, 0, out _));
		}
	}
}
#endif
