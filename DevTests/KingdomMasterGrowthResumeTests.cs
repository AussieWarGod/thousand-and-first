#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed partial class KingdomMasterGrowthResumeTests
	{
		private static KingdomLifecycleBook Bound()
		{
			var parent = new KingdomLifecycleBook();
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(parent,
				"master-growth-city", false, null, new List<string>()));
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
			return parent;
		}

		private static KingdomLifecycleBook Active(bool modern = true, int rulesVersion = 3)
		{
			KingdomLifecycleBook parent = Bound();
			KingdomGrowthBook growth = parent.Growth;
			ClassicAssert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth,
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, true, true, 100L, 20L)));
			if (modern) ClassicAssert.IsTrue(KingdomLifecycleRules.TryBindHistoricalGrowthArrivalCadence(
				growth, 100L, 20L, 0, rulesVersion, out string failure), failure);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
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
			ClassicAssert.IsTrue(KingdomMasterGrowthResumePlan.TryCreate(parent, disabledAt, now,
				enabled, true, 20L, 0, rulesVersion, out KingdomMasterGrowthResumePlan plan,
				out string failure), failure);
			CollectionAssert.AreEqual(before, Wire(parent.Growth), "preparation is detached");
			return plan;
		}

		private static void Publish(KingdomLifecycleBook parent, KingdomMasterGrowthResumePlan plan)
		{
			KingdomGrowthBook growth = parent.Growth;
			ClassicAssert.IsTrue(plan.TryPublish(parent, out string failure), failure);
			ClassicAssert.AreSame(growth, parent.Growth);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
			byte[] wire = Wire(growth);
			KingdomGrowthBook loaded = KingdomLifecycleWireCodec.ReadGrowthPayload(wire);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(loaded, parent.SettlementId));
			CollectionAssert.AreEqual(wire, Wire(loaded));
		}

		[Test]
		public void FormerDeadlineOnlyPublicationInvalidatesFreshAndCurrentBooks()
		{
			foreach (KingdomLifecycleBook parent in new[] { Bound(), Active() })
			{
				ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
				parent.Growth.NextArrivalTick = 230L;
				ClassicAssert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(parent),
					"characterizes the old production write reproduced by the native master test");
			}
		}

		[Test]
		public void FreshUnknownHealthRemainsUnknownAndLaterObservationCanStartGrowth()
		{
			KingdomLifecycleBook parent = Bound();
			KingdomGrowthBook growth = parent.Growth;
			Publish(parent, Prepare(parent));
			ClassicAssert.AreEqual(20L, growth.ArrivalIntervalTicks);
			ClassicAssert.AreEqual(0L, growth.NextArrivalTick);
			ClassicAssert.AreEqual(KingdomGrowthHealthState.Unknown, growth.HealthState);
			ClassicAssert.AreEqual(0L, growth.HealthTick);
			ClassicAssert.IsTrue(growth.WorkPaused);
			ClassicAssert.AreEqual(110L, growth.WorkPauseStartedTick);
			ClassicAssert.AreEqual(0UL, growth.ArrivalOrdinalHighWater);
			ClassicAssert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth,
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, true, true, 211L, 20L)));
			ClassicAssert.AreEqual(101L, growth.WorkPausedTicks);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryBindHistoricalGrowthArrivalCadence(
				growth, 211L, 20L, 0, 3, out string failure), failure);
			ClassicAssert.AreEqual(231L, growth.NextArrivalTick);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
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
			ClassicAssert.AreEqual(230L, growth.NextArrivalTick);
			ClassicAssert.AreEqual(100L, growth.WorkPausedTicks);
			ClassicAssert.AreEqual(healthTick, growth.HealthTick);
			ClassicAssert.AreEqual(effective, growth.EffectiveWorkTick);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryEffectiveWorkElapsed(growth, 215L,
				out long elapsed));
			ClassicAssert.AreEqual(15L, elapsed, "ten earned ticks plus five after resume, not paused time");
			if (!modern) return;
			ClassicAssert.AreEqual(2L, growth.ArrivalRateEpoch);
			ClassicAssert.AreEqual(210L, growth.ArrivalProcessedThroughTick);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryAdvanceGrowthArrivalCadence(growth,
				229L, 20L, 0, 3, out string failure), failure);
			ClassicAssert.AreEqual(0UL, growth.ArrivalOrdinalHighWater);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryAdvanceGrowthArrivalCadence(growth,
				230L, 20L, 0, 3, out failure), failure);
			ClassicAssert.AreEqual(1UL, growth.ArrivalOrdinalHighWater);
		}

		[TestCase(105L, 105L)]
		[TestCase(110L, 100L)]
		public void ExistingLocalPauseAndGlobalPauseCountTheirUnionOnce(long localPause, long expected)
		{
			KingdomLifecycleBook parent = Active();
			KingdomGrowthBook growth = parent.Growth;
			ClassicAssert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth,
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, false, true, localPause, 20L)));
			Publish(parent, Prepare(parent));
			ClassicAssert.AreEqual(expected, growth.WorkPausedTicks);
			ClassicAssert.IsFalse(growth.WorkPaused);
			ClassicAssert.AreEqual(0L, growth.WorkPauseStartedTick);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void ResumeNeverRewritesAnEarnedEffectiveClockToHideConflictingPauseEvidence(bool fieldBacked)
		{
			KingdomLifecycleBook parent = Active();
			KingdomGrowthBook growth = parent.Growth;
			ClassicAssert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth,
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, false, true, 120L, 20L)));
			if (fieldBacked)
			{
				ClassicAssert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "retained-field"));
				// A retained field clock is independent evidence; this fixture does not claim a native field commit.
				growth.FieldOps[0].ClockTick = 120L;
			}
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
			byte[] before = Wire(growth);
			bool accepted = KingdomMasterGrowthResumePlan.TryCreate(parent, 110L, 210L,
				true, true, 20L, 0, 3, out KingdomMasterGrowthResumePlan plan, out _);
			CollectionAssert.AreEqual(before, Wire(growth));
			ClassicAssert.AreEqual(fieldBacked, accepted,
				"unbacked effective progress after global disable cannot be silently rewound or credited twice");
			if (accepted) Publish(parent, plan);
			ClassicAssert.AreEqual(120L, growth.EffectiveWorkTick);
			if (fieldBacked) ClassicAssert.AreEqual(120L, growth.FieldOps[0].ClockTick);
		}

		[TestCase(false, true)]
		[TestCase(true, false)]
		[TestCase(false, false)]
		public void DisabledOrUnhealthyResumeRetainsPauseUntilRealAvailability(bool enabled, bool healthy)
		{
			KingdomLifecycleBook parent = Active();
			KingdomGrowthBook growth = parent.Growth;
			ClassicAssert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth,
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, true, healthy, 105L, 20L)));
			long healthTick = growth.HealthTick;
			Publish(parent, Prepare(parent, enabled: enabled));
			ClassicAssert.IsTrue(growth.WorkPaused);
			ClassicAssert.IsFalse(growth.ArrivalCadenceResumePending);
			ClassicAssert.AreEqual(healthTick, growth.HealthTick);
			ClassicAssert.AreEqual(healthy ? KingdomGrowthHealthState.Healthy : KingdomGrowthHealthState.Unhealthy,
				growth.HealthState);
			ClassicAssert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth,
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, true, true, 220L, 20L)));
			ClassicAssert.AreEqual(healthy ? 110L : 115L, growth.WorkPausedTicks);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryRestartGrowthArrivalCadenceAfterPause(growth,
				220L, 20L, 0, 3, out string failure), failure);
			ClassicAssert.AreEqual(240L, growth.ArrivalCadenceNextDueTick);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
		}

		[Test]
		public void PublicationRefusesChangedSourceAndDoesNotPartiallyWrite()
		{
			KingdomLifecycleBook parent = Active();
			KingdomMasterGrowthResumePlan plan = Prepare(parent);
			parent.Growth.ScarcityOptionTick = 109L;
			byte[] changed = Wire(parent.Growth);
			ClassicAssert.IsFalse(plan.TryPublish(parent, out _));
			CollectionAssert.AreEqual(changed, Wire(parent.Growth));
		}

		[Test]
		public void PublicationRefusesByteEquivalentReplacementAndRepeatedPlan()
		{
			KingdomLifecycleBook parent = Active();
			KingdomMasterGrowthResumePlan plan = Prepare(parent);
			KingdomGrowthBook source = parent.Growth;
			parent.Growth = KingdomLifecycleWireCodec.ReadGrowthPayload(Wire(source));
			ClassicAssert.IsFalse(plan.TryPublish(parent, out _));
			parent.Growth = source;
			parent.Growth.FieldOps = new List<KingdomGrowthFieldSlot>();
			ClassicAssert.IsFalse(plan.TryPublish(parent, out _), "equal bytes do not replace retained child owner");
			plan = Prepare(parent);
			Publish(parent, plan);
			byte[] after = Wire(parent.Growth);
			ClassicAssert.IsFalse(plan.TryPublish(parent, out _));
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
			ClassicAssert.IsFalse(KingdomMasterGrowthResumePlan.TryCreate(parent, disabledAt, now,
				true, true, 20L, 0, 3, out _, out _));
			CollectionAssert.AreEqual(before, Wire(parent.Growth));
		}

		[Test]
		public void PreFoundingNoOpAcceptsOnlyAnExactlyPristineLifecycle()
		{
			var parent = new KingdomLifecycleBook();
			ClassicAssert.IsTrue(KingdomLifecycleRules.IsPristineMasterResumeLifecycle(parent));
			ClassicAssert.IsFalse(KingdomLifecycleRules.IsPristineMasterResumeLifecycle(Bound()));
			parent.Growth.OptionTick = 1L;
			ClassicAssert.IsFalse(KingdomLifecycleRules.IsPristineMasterResumeLifecycle(parent));
		}

		[Test]
		public void RuntimeWiringPreflightsEveryGrowthOwnerAndUsesOnlyItsAuthoritativeMirror()
		{
			string coordinator = TestMain.ReadRepositoryText("Core/KingdomMaster.ResumeAtomicity.cs");
			int seat = coordinator.IndexOf("Seat.CanPublish(System.LifecycleBook)", StringComparison.Ordinal);
			int away = coordinator.IndexOf("NonSeatPlans[i].CanPublish(currentNonSeat[i].LifecycleBook)", StringComparison.Ordinal);
			int gate = coordinator.IndexOf("KingdomMasterPublicationGate.TryOpen", StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(seat, 0); ClassicAssert.Greater(away, seat); ClassicAssert.Greater(gate, away);
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
