#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPlotLabourRulesTests
	{
		[TestCase(0, 0L)]
		[TestCase(50, 50L)]
		[TestCase(100, 100L)]
		public void NoPartialAndFullHandsUseOnlyElapsedAttendedTime(int effectiveness,
			long expected)
		{
			KingdomPlotLabourStep step = KingdomPlotLabourRules.Advance(
				Current(100L, 100L, 0L), 100L, effectiveness, 100);
			Assert.AreEqual(KingdomPlotLabourVerdict.Attended, step.Verdict);
			Assert.AreEqual(expected, step.WorkedTicks);
			Assert.AreEqual(100L - expected, step.RemainingTicks);
			Assert.AreEqual(100L, step.NextTick);
			Assert.IsTrue(step.WriteReceipt);
		}

		[Test]
		public void MaterialInfrastructureShortfallMultipliesAvailableHands()
		{
			KingdomPlotLabourStep step = KingdomPlotLabourRules.Advance(
				Current(200L, 200L, 10L), 110L, 50, 40);
			Assert.AreEqual(20L, step.WorkedTicks);
			Assert.AreEqual(180L, step.RemainingTicks);
			Assert.IsFalse(step.Complete);
		}

		[Test]
		public void LongAbsenceUsesBoundedElapsedIntervalAndExactCompletionTick()
		{
			KingdomPlotLabourStep step = KingdomPlotLabourRules.Advance(
				Current(400000L, 400000L, 10L), 1000010L, 50, 100);
			Assert.IsTrue(step.Complete);
			Assert.AreEqual(400000L, step.WorkedTicks);
			Assert.AreEqual(800010L, step.CompletionTick);
			Assert.AreEqual(1000010L, step.NextTick);
		}

		[Test]
		public void ClockReversalFreezesWithoutRewritingReceipt()
		{
			KingdomPlotLabourStep step = KingdomPlotLabourRules.Advance(
				Current(100L, 80L, 100L), 90L, 100, 100);
			Assert.AreEqual(KingdomPlotLabourVerdict.Attended, step.Verdict);
			Assert.IsFalse(step.NeedsAttendance);
			Assert.IsFalse(step.WriteReceipt);
			Assert.AreEqual(100L, step.NextTick);
			Assert.AreEqual(80L, step.RemainingTicks);
		}

		[Test]
		public void ZeroEffectivenessQueuedRootSpendsIdleIntervalWithoutBankingIt()
		{
			KingdomPlotLabourStep idle = KingdomPlotLabourRules.Advance(
				Current(100L, 100L, 100L), 200L, 0, 100);
			Assert.AreEqual(0L, idle.WorkedTicks);
			Assert.AreEqual(200L, idle.NextTick);
			KingdomPlotLabourStep resumed = KingdomPlotLabourRules.Advance(
				Current(100L, idle.RemainingTicks, idle.NextTick), 250L, 100, 100);
			Assert.AreEqual(50L, resumed.WorkedTicks);
			Assert.AreEqual(50L, resumed.RemainingTicks);
		}

		[Test]
		public void ContradictoryIncompleteAndUnknownReceiptsRefuse()
		{
			KingdomPlotLabourReceipt contradictory = Current(100L, 101L, 0L);
			Assert.AreEqual(KingdomPlotLabourVerdict.Invalid,
				KingdomPlotLabourRules.Assess(contradictory, 10L).Verdict);
			KingdomPlotLabourReceipt incomplete = Current(100L, 50L, 0L);
			incomplete.HasRemainingTicks = false;
			StringAssert.Contains("incomplete or contradictory",
				KingdomPlotLabourRules.Assess(incomplete, 10L).Failure);
			KingdomPlotLabourReceipt unknown = Current(100L, 50L, 0L);
			unknown.Schema = 9;
			StringAssert.Contains("unknown labour receipt",
				KingdomPlotLabourRules.Assess(unknown, 10L).Failure);
		}

		[Test]
		public void SchemaZeroKeepsExactLegacyCalendarAndNeverClaimsAttendance()
		{
			KingdomPlotLabourReceipt legacy = new KingdomPlotLabourReceipt
			{
				Schema = KingdomPlotLabourRules.LegacySchema,
				LegacyStartTick = 100L,
				LegacyTotalTicks = 400L
			};
			KingdomPlotLabourStep step = KingdomPlotLabourRules.Advance(
				legacy, 300L, 0, 0);
			Assert.AreEqual(KingdomPlotLabourVerdict.LegacyCalendar, step.Verdict);
			Assert.AreEqual(200L, step.CompletedTicks);
			Assert.AreEqual(400L, step.RequiredTicks);
			Assert.IsFalse(step.NeedsAttendance);
			Assert.IsFalse(step.WriteReceipt);
		}

		private static KingdomPlotLabourReceipt Current(long required, long remaining,
			long last)
		{
			return new KingdomPlotLabourReceipt
			{
				Schema = KingdomPlotLabourRules.CurrentSchema,
				HasRequiredTicks = true,
				RequiredTicks = required,
				HasRemainingTicks = true,
				RemainingTicks = remaining,
				HasLastTick = true,
				LastTick = last
			};
		}
	}
}
#endif
