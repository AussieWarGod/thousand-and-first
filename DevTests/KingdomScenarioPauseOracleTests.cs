#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Collections.Generic;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	public class KingdomScenarioPauseOracleTests
	{
		[Test]
		public void RuntimeRoutesActualResumeThroughReadOnlyWitnessAndChecksDebtBeforeEnable()
		{
			string witness = TestMain.ReadRepositoryText("Harness/KingdomScenarioPauseWitness.cs");
			StringAssert.Contains("KingdomScenarioPauseOracle.TryResume", witness);
			StringAssert.Contains("Growth.WorkPausedTicks == Expected.PausedTicks", witness);
			StringAssert.Contains("Growth.NextArrivalTick == Expected.ArrivalDeadline", witness);
			StringAssert.Contains("internal static void Prefix(KingdomSystem system, long now)", witness);
			StringAssert.DoesNotContain("Options.SetOption", witness);
			StringAssert.DoesNotContain("Growth.WorkPausedTicks = ", witness);
			string controller = TestMain.ReadRepositoryText("Harness/KingdomScenarioPauseController.cs");
			int before = controller.IndexOf("KingdomScenarioContainerStress.BeforeResume();");
			ClassicAssert.Greater(before, 0);
			ClassicAssert.Greater(controller.IndexOf("Set(KingdomMaster.OptionId, OriginalMaster, ref OwnedMaster)"), before);
			string fixture = TestMain.ReadRepositoryText("Harness/KingdomScenarioContainerStress.cs");
			StringAssert.Contains("liquid.Volume == liquid.MaxVolume - 1", fixture);
			StringAssert.Contains("liquid.Volume == liquid.MaxVolume", fixture);
			StringAssert.Contains("Ready && ReturnDebtProved", fixture);
		}

		[TestCase(false, 0, 350)]
		[TestCase(true, 80, 370)]
		[TestCase(true, 100, 350)]
		[TestCase(true, 120, 350)]
		public void OpenPauseIntervalsAreUnionNotSum(bool local, long start, long expected)
		{
			ClassicAssert.IsTrue(KingdomScenarioPauseOracle.TryResume(100, 400, local, start,
				50, 70, 1200, 7, 3, 1200, out var result));
			ClassicAssert.AreEqual(expected, result.PausedTicks);
			ClassicAssert.AreEqual(70, result.EffectiveTick, "master does not advance the effective field clock");
			ClassicAssert.AreEqual(1600, result.ArrivalDeadline);
			ClassicAssert.AreEqual(1600, result.DayDeadline);
			ClassicAssert.AreEqual(8, result.ArrivalEpoch);
			ClassicAssert.AreEqual(4, result.ResumeToken);
		}

		[Test]
		public void DoubleCountedOverlapAndShiftedArrivalFailEquality()
		{
			ClassicAssert.IsTrue(KingdomScenarioPauseOracle.TryResume(100, 400, true, 80,
				50, 70, 1200, 7, 3, 1200, out var result));
			ClassicAssert.AreNotEqual(50 + (400 - 80) + (400 - 100), result.PausedTicks);
			ClassicAssert.AreNotEqual(1600 + (400 - 100), result.ArrivalDeadline);
			ClassicAssert.AreNotEqual(9, result.ArrivalEpoch);
		}

		[TestCase(90, 90)]
		[TestCase(100, 100)]
		[TestCase(150, 450)]
		public void CommittedDueDeadlineIsNotRebased(long before, long expected)
		{
			ClassicAssert.IsTrue(KingdomScenarioPauseOracle.TryCommitted(before, 100, 400, out long result));
			ClassicAssert.AreEqual(expected, result);
		}

		[Test]
		public void RefusesOverflowAndUnsupportedInput()
		{
			ClassicAssert.IsFalse(KingdomScenarioPauseOracle.TryResume(100, 99, false, 0, 0, 0, 1200, 1, 0, 1200, out _));
			ClassicAssert.IsFalse(KingdomScenarioPauseOracle.TryResume(100, 100, false, 0, 0, 0, 1200, 1, 0, 1200, out _));
			ClassicAssert.IsFalse(KingdomScenarioPauseOracle.TryResume(100, 400, true, 401, 0, 0, 1200, 1, 0, 1200, out _));
			ClassicAssert.IsFalse(KingdomScenarioPauseOracle.TryResume(100, 400, false, 1, 0, 0, 1200, 1, 0, 1200, out _));
			ClassicAssert.IsFalse(KingdomScenarioPauseOracle.TryResume(100, 400, false, 0, long.MaxValue, 0, 1200, 1, 0, 1200, out _));
			ClassicAssert.IsFalse(KingdomScenarioPauseOracle.TryResume(100, 400, false, 0, 0, 0, long.MaxValue, 1, 0, 1200, out _));
			ClassicAssert.IsFalse(KingdomScenarioPauseOracle.TryResume(100, 400, false, 0, 0, 0, 1200, long.MaxValue, 0, 1200, out _));
			ClassicAssert.IsFalse(KingdomScenarioPauseOracle.TryResume(100, 400, false, 0, 0, 0, 1200, 1, long.MaxValue, 1200, out _));
			ClassicAssert.IsFalse(KingdomScenarioPauseOracle.TryCommitted(long.MaxValue, 100, 400, out _));
		}

		[TestCase(105L)]
		[TestCase(110L)]
		public void ActualGrowthResumeEqualsIndependentOracle(long localStart)
		{
			var parent = new KingdomLifecycleBook();
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(parent,
				"travel-oracle-city", false, null, new List<string>()));
			var growth = parent.Growth;
			ClassicAssert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth,
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, true, true, 100, 20)));
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryBindHistoricalGrowthArrivalCadence(
				growth, 100, 20, 0, 3, out string failure), failure);
			ClassicAssert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth,
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, false, true, localStart, 20)));
			ClassicAssert.IsTrue(KingdomScenarioPauseOracle.TryResume(110, 210, growth.WorkPaused,
				growth.WorkPauseStartedTick, growth.WorkPausedTicks, growth.EffectiveWorkTick,
				growth.ArrivalIntervalTicks, growth.ArrivalRateEpoch, 0, 1200, out var expected));
			ClassicAssert.IsTrue(KingdomMasterGrowthResumePlan.TryCreate(parent, 110, 210, true, true,
				20, 0, 3, out var plan, out failure), failure);
			ClassicAssert.IsTrue(plan.TryPublish(parent, out failure), failure);
			ClassicAssert.AreEqual(expected.PausedTicks, growth.WorkPausedTicks);
			ClassicAssert.AreEqual(expected.EffectiveTick, growth.EffectiveWorkTick);
			ClassicAssert.AreEqual(expected.ArrivalDeadline, growth.NextArrivalTick);
			ClassicAssert.AreEqual(expected.ArrivalDeadline, growth.ArrivalCadenceNextDueTick);
			ClassicAssert.AreEqual(expected.ArrivalEpoch, growth.ArrivalRateEpoch);
			ClassicAssert.IsFalse(growth.WorkPaused);
			ClassicAssert.AreEqual(0, growth.WorkPauseStartedTick);
		}
	}
}
#endif
