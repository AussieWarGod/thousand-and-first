#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.AreEqual(KingdomPlotLabourVerdict.Attended, step.Verdict);
			ClassicAssert.AreEqual(expected, step.WorkedTicks);
			ClassicAssert.AreEqual(100L - expected, step.RemainingTicks);
			ClassicAssert.AreEqual(100L, step.NextTick);
			ClassicAssert.IsTrue(step.WriteReceipt);
		}

		[Test]
		public void MaterialInfrastructureShortfallMultipliesAvailableHands()
		{
			KingdomPlotLabourStep step = KingdomPlotLabourRules.Advance(
				Current(200L, 200L, 10L), 110L, 50, 40);
			ClassicAssert.AreEqual(20L, step.WorkedTicks);
			ClassicAssert.AreEqual(180L, step.RemainingTicks);
			ClassicAssert.IsFalse(step.Complete);
		}

		[Test]
		public void LongAbsenceUsesBoundedElapsedIntervalAndExactCompletionTick()
		{
			KingdomPlotLabourStep step = KingdomPlotLabourRules.Advance(
				Current(400000L, 400000L, 10L), 1000010L, 50, 100);
			ClassicAssert.IsTrue(step.Complete);
			ClassicAssert.AreEqual(400000L, step.WorkedTicks);
			ClassicAssert.AreEqual(800010L, step.CompletionTick);
			ClassicAssert.AreEqual(1000010L, step.NextTick);
		}

		[Test]
		public void ClockReversalFreezesWithoutRewritingReceipt()
		{
			KingdomPlotLabourStep step = KingdomPlotLabourRules.Advance(
				Current(100L, 80L, 100L), 90L, 100, 100);
			ClassicAssert.AreEqual(KingdomPlotLabourVerdict.Attended, step.Verdict);
			ClassicAssert.IsFalse(step.NeedsAttendance);
			ClassicAssert.IsFalse(step.WriteReceipt);
			ClassicAssert.AreEqual(100L, step.NextTick);
			ClassicAssert.AreEqual(80L, step.RemainingTicks);
		}

		[Test]
		public void ZeroEffectivenessQueuedRootSpendsIdleIntervalWithoutBankingIt()
		{
			KingdomPlotLabourStep idle = KingdomPlotLabourRules.Advance(
				Current(100L, 100L, 100L), 200L, 0, 100);
			ClassicAssert.AreEqual(0L, idle.WorkedTicks);
			ClassicAssert.AreEqual(200L, idle.NextTick);
			KingdomPlotLabourStep resumed = KingdomPlotLabourRules.Advance(
				Current(100L, idle.RemainingTicks, idle.NextTick), 250L, 100, 100);
			ClassicAssert.AreEqual(50L, resumed.WorkedTicks);
			ClassicAssert.AreEqual(50L, resumed.RemainingTicks);
		}

		[Test]
		public void ContradictoryIncompleteAndUnknownReceiptsRefuse()
		{
			KingdomPlotLabourReceipt contradictory = Current(100L, 101L, 0L);
			ClassicAssert.AreEqual(KingdomPlotLabourVerdict.Invalid,
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
			ClassicAssert.AreEqual(KingdomPlotLabourVerdict.LegacyCalendar, step.Verdict);
			ClassicAssert.AreEqual(200L, step.CompletedTicks);
			ClassicAssert.AreEqual(400L, step.RequiredTicks);
			ClassicAssert.IsFalse(step.NeedsAttendance);
			ClassicAssert.IsFalse(step.WriteReceipt);
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
