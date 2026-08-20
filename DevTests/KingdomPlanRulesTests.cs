#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomPlanRulesTests
	{
		private static KingdomPendingPlan Plan(long placedTick, long placedOrder, int cost, bool defensive = false)
		{
			return new KingdomPendingPlan(placedTick, placedOrder, cost, defensive);
		}

		// --- CompareOrder: oldest first, placement order breaks a tick tie --------------------

		[Test]
		public void CompareOrder_EarlierTickSortsFirstRegardlessOfPlacementOrder()
		{
			KingdomPendingPlan older = Plan(placedTick: 100, placedOrder: 50, cost: 1);
			KingdomPendingPlan newer = Plan(placedTick: 200, placedOrder: 1, cost: 1);
			Assert.Less(KingdomPlanRules.CompareOrder(older, newer), 0);
			Assert.Greater(KingdomPlanRules.CompareOrder(newer, older), 0);
		}

		[Test]
		public void CompareOrder_SameTickBreaksTieByPlacementOrder()
		{
			KingdomPendingPlan first = Plan(placedTick: 100, placedOrder: 1, cost: 1);
			KingdomPendingPlan second = Plan(placedTick: 100, placedOrder: 2, cost: 1);
			Assert.Less(KingdomPlanRules.CompareOrder(first, second), 0);
			Assert.AreEqual(0, KingdomPlanRules.CompareOrder(first, first));
		}

		[Test]
		public void CompareOrder_SortsAShuffledQueueIntoStakingOrder()
		{
			// A mutation that flips either comparison (tick or order) would still compile and
			// would still "sort" -- just into the wrong order. Sorting a queue that is not
			// already in order is what actually exercises the comparator's direction.
			List<KingdomPendingPlan> queue = new List<KingdomPendingPlan>
			{
				Plan(placedTick: 300, placedOrder: 0, cost: 1), // staked 3rd
				Plan(placedTick: 100, placedOrder: 0, cost: 1), // staked 1st
				Plan(placedTick: 100, placedOrder: 1, cost: 1), // staked 2nd (same tick as 1st)
			};
			queue.Sort(KingdomPlanRules.CompareOrder);
			Assert.AreEqual(100L, queue[0].PlacedTick);
			Assert.AreEqual(0L, queue[0].PlacedOrder);
			Assert.AreEqual(100L, queue[1].PlacedTick);
			Assert.AreEqual(1L, queue[1].PlacedOrder);
			Assert.AreEqual(300L, queue[2].PlacedTick);
		}

		// --- CanAfford: water in full, and room under the cap unless the design is defensive --

		[Test]
		public void CanAfford_EnoughWaterAndRoomUnderCap()
		{
			Assert.IsTrue(KingdomPlanRules.CanAfford(Plan(0, 0, cost: 10), StoredWater: 10, BuiltCount: 5, CapForStage: 10));
		}

		[Test]
		public void CanAfford_WaterOneShortRefuses()
		{
			Assert.IsFalse(KingdomPlanRules.CanAfford(Plan(0, 0, cost: 10), StoredWater: 9, BuiltCount: 0, CapForStage: 40));
		}

		[Test]
		public void CanAfford_NeverPartial_ExactWaterSucceedsOneLessFails()
		{
			KingdomPendingPlan plan = Plan(0, 0, cost: 25);
			Assert.IsTrue(KingdomPlanRules.CanAfford(plan, StoredWater: 25, BuiltCount: 0, CapForStage: 40));
			Assert.IsFalse(KingdomPlanRules.CanAfford(plan, StoredWater: 24, BuiltCount: 0, CapForStage: 40));
		}

		[Test]
		public void CanAfford_NonDefensiveBlockedAtOrAboveCap()
		{
			KingdomPendingPlan plan = Plan(0, 0, cost: 1);
			Assert.IsFalse(KingdomPlanRules.CanAfford(plan, StoredWater: 100, BuiltCount: 10, CapForStage: 10));
			Assert.IsFalse(KingdomPlanRules.CanAfford(plan, StoredWater: 100, BuiltCount: 11, CapForStage: 10));
			Assert.IsTrue(KingdomPlanRules.CanAfford(plan, StoredWater: 100, BuiltCount: 9, CapForStage: 10));
		}

		[Test]
		public void CanAfford_DefensiveIgnoresTheCapEntirely()
		{
			// A wall never eats the plan, the same way KingdomCommission.Commission never charges
			// one against it. A mutation that dropped the Defensive short-circuit would fail this
			// even though the settlement is nowhere near its water limit.
			KingdomPendingPlan wall = Plan(0, 0, cost: 1, defensive: true);
			Assert.IsTrue(KingdomPlanRules.CanAfford(wall, StoredWater: 100, BuiltCount: 999, CapForStage: 10));
		}

		// --- PlansToRealize: the settlement's own scheduling pass --------------------------

		[Test]
		public void PlansToRealize_EmptyQueueRealizesNothing()
		{
			CollectionAssert.IsEmpty(KingdomPlanRules.PlansToRealize(new List<KingdomPendingPlan>(), StoredWater: 1000, BuiltCount: 0, CapForStage: 40));
		}

		[Test]
		public void PlansToRealize_NullQueueRealizesNothing()
		{
			CollectionAssert.IsEmpty(KingdomPlanRules.PlansToRealize(null, StoredWater: 1000, BuiltCount: 0, CapForStage: 40));
		}

		[Test]
		public void PlansToRealize_SingleAffordablePlanIsReturned()
		{
			List<KingdomPendingPlan> plans = new List<KingdomPendingPlan> { Plan(0, 0, cost: 10) };
			CollectionAssert.AreEqual(new[] { 0 }, KingdomPlanRules.PlansToRealize(plans, StoredWater: 10, BuiltCount: 0, CapForStage: 40));
		}

		[Test]
		public void PlansToRealize_SettlementThatCanNeverAffordItRealizesNothingEveryTime()
		{
			// The settlement whose stores will never reach a design's cost: the queue is not an
			// error state, it just never resolves. Calling this repeatedly with the same inputs
			// must keep returning empty rather than drifting, throwing, or eventually "giving up"
			// and building something anyway.
			List<KingdomPendingPlan> plans = new List<KingdomPendingPlan> { Plan(0, 0, cost: 1000) };
			for (int i = 0; i < 5; i++)
			{
				CollectionAssert.IsEmpty(KingdomPlanRules.PlansToRealize(plans, StoredWater: 3, BuiltCount: 0, CapForStage: 40));
			}
		}

		[Test]
		public void PlansToRealize_AnOlderUnaffordablePlanBlocksAYoungerCheaperOne()
		{
			// The load-bearing FIFO guarantee: an expensive plan staked first must never be
			// skipped over in favour of a cheap plan staked after it, even though the cheap one
			// alone is easily affordable. A mutation that turned this into a "skip unaffordable,
			// keep looking" scan would return [1] here instead of nothing.
			List<KingdomPendingPlan> plans = new List<KingdomPendingPlan>
			{
				Plan(placedTick: 100, placedOrder: 0, cost: 500), // staked first, too costly yet
				Plan(placedTick: 200, placedOrder: 0, cost: 5),   // staked after, easily affordable alone
			};
			CollectionAssert.IsEmpty(KingdomPlanRules.PlansToRealize(plans, StoredWater: 5, BuiltCount: 0, CapForStage: 40));
		}

		[Test]
		public void PlansToRealize_DrawsSequentially_NotAgainstTheOriginalTotal()
		{
			// water covers plan0 (10) plus plan1 (5) minus one dram. A scheduler that checked
			// every plan against the ORIGINAL 14 (rather than deducting as it goes) would wrongly
			// also realise plan1, since 14 >= 5. The correct answer realises only plan0.
			List<KingdomPendingPlan> plans = new List<KingdomPendingPlan>
			{
				Plan(placedTick: 100, placedOrder: 0, cost: 10),
				Plan(placedTick: 200, placedOrder: 0, cost: 5),
			};
			CollectionAssert.AreEqual(new[] { 0 }, KingdomPlanRules.PlansToRealize(plans, StoredWater: 14, BuiltCount: 0, CapForStage: 40));
		}

		[Test]
		public void PlansToRealize_RealizesBothWhenTheStoresCoverTheExactSum()
		{
			List<KingdomPendingPlan> plans = new List<KingdomPendingPlan>
			{
				Plan(placedTick: 100, placedOrder: 0, cost: 10),
				Plan(placedTick: 200, placedOrder: 0, cost: 5),
			};
			CollectionAssert.AreEqual(new[] { 0, 1 }, KingdomPlanRules.PlansToRealize(plans, StoredWater: 15, BuiltCount: 0, CapForStage: 40));
		}

		[Test]
		public void PlansToRealize_CapCountsEachRealizationBeforeTheNextIsJudged()
		{
			// Two non-defensive plans, both individually affordable in water, but only one slot
			// of cap room to begin with. A scheduler that judged the cap only once up front
			// (rather than incrementing BuiltCount as each plan is realised) would wrongly return
			// both.
			List<KingdomPendingPlan> plans = new List<KingdomPendingPlan>
			{
				Plan(placedTick: 100, placedOrder: 0, cost: 1),
				Plan(placedTick: 200, placedOrder: 0, cost: 1),
			};
			CollectionAssert.AreEqual(new[] { 0 }, KingdomPlanRules.PlansToRealize(plans, StoredWater: 100, BuiltCount: 9, CapForStage: 10));
		}

		[Test]
		public void PlansToRealize_ADefensivePlanStuckBehindABlockedOneStillWaits()
		{
			// FIFO applies to defensive plans too: a wall staked after a stuck civic plan does
			// not leapfrog it just because the wall itself would have been affordable and cap-
			// exempt on its own.
			List<KingdomPendingPlan> plans = new List<KingdomPendingPlan>
			{
				Plan(placedTick: 100, placedOrder: 0, cost: 500, defensive: false), // stuck: too costly
				Plan(placedTick: 200, placedOrder: 0, cost: 1, defensive: true),    // would be free to build alone
			};
			CollectionAssert.IsEmpty(KingdomPlanRules.PlansToRealize(plans, StoredWater: 50, BuiltCount: 0, CapForStage: 40));
		}

		[Test]
		public void PlansToRealize_TicksTiesBreakByPlacementOrderUnderScarcity()
		{
			List<KingdomPendingPlan> plans = new List<KingdomPendingPlan>
			{
				Plan(placedTick: 100, placedOrder: 5, cost: 10), // staked second at the same tick
				Plan(placedTick: 100, placedOrder: 1, cost: 10), // staked first at the same tick
			};
			// Only enough water for one of them; the lower PlacedOrder (staked first) must win.
			CollectionAssert.AreEqual(new[] { 1 }, KingdomPlanRules.PlansToRealize(plans, StoredWater: 10, BuiltCount: 0, CapForStage: 40));
		}

		[Test]
		public void PlansToRealize_NeverReturnsMoreThanMaxPlansPerVisit()
		{
			List<KingdomPendingPlan> plans = new List<KingdomPendingPlan>();
			for (int i = 0; i < KingdomPlanRules.MaxPlansPerVisit + 4; i++)
			{
				plans.Add(Plan(placedTick: i, placedOrder: 0, cost: 1));
			}
			List<int> realized = KingdomPlanRules.PlansToRealize(plans, StoredWater: 10000, BuiltCount: 0, CapForStage: 10000);
			Assert.AreEqual(KingdomPlanRules.MaxPlansPerVisit, realized.Count);
			// And it is still the oldest ones, in order -- the cap on the batch never reaches
			// into the middle of the queue to pick a different set.
			for (int i = 0; i < realized.Count; i++)
			{
				Assert.AreEqual(i, realized[i]);
			}
		}

		[Test]
		public void PlansToRealize_IsDeterministicAcrossRepeatedCalls()
		{
			List<KingdomPendingPlan> plans = new List<KingdomPendingPlan>
			{
				Plan(placedTick: 100, placedOrder: 0, cost: 10),
				Plan(placedTick: 200, placedOrder: 0, cost: 5),
			};
			List<int> first = KingdomPlanRules.PlansToRealize(plans, StoredWater: 15, BuiltCount: 0, CapForStage: 40);
			List<int> second = KingdomPlanRules.PlansToRealize(plans, StoredWater: 15, BuiltCount: 0, CapForStage: 40);
			CollectionAssert.AreEqual(first, second);
		}

		[Test]
		public void MaxPlansPerVisit_IsPositive()
		{
			Assert.Greater(KingdomPlanRules.MaxPlansPerVisit, 0);
		}
	}
}
#endif
