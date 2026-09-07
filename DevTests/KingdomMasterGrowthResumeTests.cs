#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed partial class KingdomMasterGrowthResumeTests
	{
		private static KingdomLifecycleBook Bound()
		{
			var parent = new KingdomLifecycleBook();
			Assert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(parent,
				"master-growth-city", false, null, new List<string>()));
			Assert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
			return parent;
		}

		private static KingdomLifecycleBook Active(bool modern = true, int rulesVersion = 3)
		{
			KingdomLifecycleBook parent = Bound();
			KingdomGrowthBook growth = parent.Growth;
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth,
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, true, true, 100L, 20L)));
			if (modern) Assert.IsTrue(KingdomLifecycleRules.TryBindHistoricalGrowthArrivalCadence(
				growth, 100L, 20L, 0, rulesVersion, out string failure), failure);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
			return parent;
		}

		private static byte[] Wire(KingdomGrowthBook growth)
		{
			return KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
		}

		private static KingdomMasterGrowthResumePlan Prepare(KingdomLifecycleBook parent,
			long disabledAt = 110L, long now = 210L, bool enabled = true)
		{
			byte[] before = Wire(parent.Growth);
			int rulesVersion = parent.Growth.ArrivalRulesVersion > 0 ? parent.Growth.ArrivalRulesVersion : 3;
			Assert.IsTrue(KingdomMasterGrowthResumePlan.TryCreate(parent, disabledAt, now,
				enabled, true, 20L, 0, rulesVersion, out KingdomMasterGrowthResumePlan plan,
				out string failure), failure);
			CollectionAssert.AreEqual(before, Wire(parent.Growth), "preparation is detached");
			return plan;
		}

		private static void Publish(KingdomLifecycleBook parent, KingdomMasterGrowthResumePlan plan)
		{
			KingdomGrowthBook growth = parent.Growth;
			Assert.IsTrue(plan.TryPublish(parent, out string failure), failure);
			Assert.AreSame(growth, parent.Growth);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
			byte[] wire = Wire(growth);
			KingdomGrowthBook loaded = KingdomLifecycleWireCodec.ReadGrowthPayload(wire);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(loaded, parent.SettlementId));
			CollectionAssert.AreEqual(wire, Wire(loaded));
		}

		[Test]
		public void FormerDeadlineOnlyPublicationInvalidatesFreshAndCurrentBooks()
		{
			foreach (KingdomLifecycleBook parent in new[] { Bound(), Active() })
			{
				Assert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
				parent.Growth.NextArrivalTick = 230L;
				Assert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(parent),
					"characterizes the old production write reproduced by the native master test");
			}
		}

		[Test]
		public void FreshUnknownHealthRemainsUnknownAndLaterObservationCanStartGrowth()
		{
			KingdomLifecycleBook parent = Bound();
			KingdomGrowthBook growth = parent.Growth;
			Publish(parent, Prepare(parent));
			Assert.AreEqual(20L, growth.ArrivalIntervalTicks);
			Assert.AreEqual(0L, growth.NextArrivalTick);
			Assert.AreEqual(KingdomGrowthHealthState.Unknown, growth.HealthState);
			Assert.AreEqual(0L, growth.HealthTick);
			Assert.IsTrue(growth.WorkPaused);
			Assert.AreEqual(110L, growth.WorkPauseStartedTick);
			Assert.AreEqual(0UL, growth.ArrivalOrdinalHighWater);
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth,
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, true, true, 211L, 20L)));
			Assert.AreEqual(101L, growth.WorkPausedTicks);
			Assert.IsTrue(KingdomLifecycleRules.TryBindHistoricalGrowthArrivalCadence(
				growth, 211L, 20L, 0, 3, out string failure), failure);
			Assert.AreEqual(231L, growth.NextArrivalTick);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void ActiveResumeRestartsFullIntervalWithoutCountingDisabledTime(bool modern)
		{
			KingdomLifecycleBook parent = Active(modern);
			KingdomGrowthBook growth = parent.Growth;
			long healthTick = growth.HealthTick;
			long effective = growth.EffectiveWorkTick;
			Publish(parent, Prepare(parent));
			Assert.AreEqual(230L, growth.NextArrivalTick);
			Assert.AreEqual(100L, growth.WorkPausedTicks);
			Assert.AreEqual(healthTick, growth.HealthTick);
			Assert.AreEqual(effective, growth.EffectiveWorkTick);
			Assert.IsTrue(KingdomLifecycleRules.TryEffectiveWorkElapsed(growth, 215L,
				out long elapsed));
			Assert.AreEqual(15L, elapsed, "ten earned ticks plus five after resume, not paused time");
			if (!modern) return;
			Assert.AreEqual(2L, growth.ArrivalRateEpoch);
			Assert.AreEqual(210L, growth.ArrivalProcessedThroughTick);
			Assert.IsTrue(KingdomLifecycleRules.TryAdvanceGrowthArrivalCadence(growth,
				229L, 20L, 0, 3, out string failure), failure);
			Assert.AreEqual(0UL, growth.ArrivalOrdinalHighWater);
			Assert.IsTrue(KingdomLifecycleRules.TryAdvanceGrowthArrivalCadence(growth,
				230L, 20L, 0, 3, out failure), failure);
			Assert.AreEqual(1UL, growth.ArrivalOrdinalHighWater);
		}

		[TestCase(105L, 105L)]
		[TestCase(110L, 100L)]
		public void ExistingLocalPauseAndGlobalPauseCountTheirUnionOnce(long localPause, long expected)
		{
			KingdomLifecycleBook parent = Active();
			KingdomGrowthBook growth = parent.Growth;
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth,
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, false, true, localPause, 20L)));
			Publish(parent, Prepare(parent));
			Assert.AreEqual(expected, growth.WorkPausedTicks);
			Assert.IsFalse(growth.WorkPaused);
			Assert.AreEqual(0L, growth.WorkPauseStartedTick);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void ResumeNeverRewritesAnEarnedEffectiveClockToHideConflictingPauseEvidence(bool fieldBacked)
		{
			KingdomLifecycleBook parent = Active();
			KingdomGrowthBook growth = parent.Growth;
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth,
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, false, true, 120L, 20L)));
			if (fieldBacked)
			{
				Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "retained-field"));
				// A retained field clock is independent evidence; this fixture does not claim a native field commit.
				growth.FieldOps[0].ClockTick = 120L;
			}
			Assert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
			byte[] before = Wire(growth);
			bool accepted = KingdomMasterGrowthResumePlan.TryCreate(parent, 110L, 210L,
				true, true, 20L, 0, 3, out KingdomMasterGrowthResumePlan plan, out _);
			CollectionAssert.AreEqual(before, Wire(growth));
			Assert.AreEqual(fieldBacked, accepted,
				"unbacked effective progress after global disable cannot be silently rewound or credited twice");
			if (accepted) Publish(parent, plan);
			Assert.AreEqual(120L, growth.EffectiveWorkTick);
			if (fieldBacked) Assert.AreEqual(120L, growth.FieldOps[0].ClockTick);
		}

		[TestCase(false, true)]
		[TestCase(true, false)]
		[TestCase(false, false)]
		public void DisabledOrUnhealthyResumeRetainsPauseUntilRealAvailability(bool enabled, bool healthy)
		{
			KingdomLifecycleBook parent = Active();
			KingdomGrowthBook growth = parent.Growth;
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth,
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, true, healthy, 105L, 20L)));
			long healthTick = growth.HealthTick;
			Publish(parent, Prepare(parent, enabled: enabled));
			Assert.IsTrue(growth.WorkPaused);
			Assert.IsFalse(growth.ArrivalCadenceResumePending);
			Assert.AreEqual(healthTick, growth.HealthTick);
			Assert.AreEqual(healthy ? KingdomGrowthHealthState.Healthy : KingdomGrowthHealthState.Unhealthy,
				growth.HealthState);
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth,
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, true, true, 220L, 20L)));
			Assert.AreEqual(healthy ? 110L : 115L, growth.WorkPausedTicks);
			Assert.IsTrue(KingdomLifecycleRules.TryRestartGrowthArrivalCadenceAfterPause(growth,
				220L, 20L, 0, 3, out string failure), failure);
			Assert.AreEqual(240L, growth.ArrivalCadenceNextDueTick);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
		}

		[Test]
		public void PublicationRefusesChangedSourceAndDoesNotPartiallyWrite()
		{
			KingdomLifecycleBook parent = Active();
			KingdomMasterGrowthResumePlan plan = Prepare(parent);
			parent.Growth.ScarcityOptionTick = 109L;
			byte[] changed = Wire(parent.Growth);
			Assert.IsFalse(plan.TryPublish(parent, out _));
			CollectionAssert.AreEqual(changed, Wire(parent.Growth));
		}

		[Test]
		public void PublicationRefusesByteEquivalentReplacementAndRepeatedPlan()
		{
			KingdomLifecycleBook parent = Active();
			KingdomMasterGrowthResumePlan plan = Prepare(parent);
			KingdomGrowthBook source = parent.Growth;
			parent.Growth = KingdomLifecycleWireCodec.ReadGrowthPayload(Wire(source));
			Assert.IsFalse(plan.TryPublish(parent, out _));
			parent.Growth = source;
			parent.Growth.FieldOps = new List<KingdomGrowthFieldSlot>();
			Assert.IsFalse(plan.TryPublish(parent, out _), "equal bytes do not replace retained child owner");
			plan = Prepare(parent);
			Publish(parent, plan);
			byte[] after = Wire(parent.Growth);
			Assert.IsFalse(plan.TryPublish(parent, out _));
			CollectionAssert.AreEqual(after, Wire(parent.Growth));
		}

		[TestCase(-1L, 210L)]
		[TestCase(211L, 210L)]
		[TestCase(90L, 99L)]
		[TestCase(110L, long.MaxValue)]
		public void ClockRegressionAndDeadlineOverflowRefuseBeforePublication(long disabledAt, long now)
		{
			KingdomLifecycleBook parent = Active();
			byte[] before = Wire(parent.Growth);
			Assert.IsFalse(KingdomMasterGrowthResumePlan.TryCreate(parent, disabledAt, now,
				true, true, 20L, 0, 3, out _, out _));
			CollectionAssert.AreEqual(before, Wire(parent.Growth));
		}

		[Test]
		public void PreFoundingNoOpAcceptsOnlyAnExactlyPristineLifecycle()
		{
			var parent = new KingdomLifecycleBook();
			Assert.IsTrue(KingdomLifecycleRules.IsPristineMasterResumeLifecycle(parent));
			Assert.IsFalse(KingdomLifecycleRules.IsPristineMasterResumeLifecycle(Bound()));
			parent.Growth.OptionTick = 1L;
			Assert.IsFalse(KingdomLifecycleRules.IsPristineMasterResumeLifecycle(parent));
		}

		[Test]
		public void RuntimeWiringPreflightsEveryGrowthOwnerAndUsesOnlyItsAuthoritativeMirror()
		{
			string coordinator = TestMain.ReadRepositoryText("Core/KingdomMaster.ResumeAtomicity.cs");
			int seat = coordinator.IndexOf("Seat.CanPublish(System.LifecycleBook)", StringComparison.Ordinal);
			int away = coordinator.IndexOf("NonSeatPlans[i].CanPublish(currentNonSeat[i].LifecycleBook)", StringComparison.Ordinal);
			int gate = coordinator.IndexOf("KingdomMasterPublicationGate.TryOpen", StringComparison.Ordinal);
			Assert.GreaterOrEqual(seat, 0); Assert.Greater(away, seat); Assert.Greater(gate, away);
			string recovery = TestMain.ReadRepositoryText("Core/KingdomMasterRecoveryPlans.cs");
			StringAssert.Contains("Growth.PublishPrevalidated();", recovery);
			StringAssert.DoesNotContain("growth.NextArrivalTick = Arrival", recovery);
			StringAssert.Contains("GrowthEnabled == KingdomGrowth.Enabled", recovery);
			StringAssert.Contains("ScarcityEnabled == KingdomGrowth.ScarcityEnabled", recovery);
			string settlement = TestMain.ReadRepositoryText("Core/KingdomMasterSettlementPlan.cs");
			StringAssert.Contains("lifecyclePlan.HasArrivalAuthority", settlement);
			StringAssert.Contains("? lifecyclePlan.NextArrivalTick : oldArrival", settlement);
		}
	}
}
#endif
