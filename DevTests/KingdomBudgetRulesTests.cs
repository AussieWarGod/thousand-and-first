#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The performance constitution as arithmetic. LIVING-CITY-ARCHITECTURE §0.0 is a table, and a
	/// table nobody checks is a sentiment: these pin every figure by value, so a threshold cannot
	/// drift without a test saying which one moved. §6.5's receipt line is pinned the same way,
	/// because a BUDGET line in a playtest log is a bug report and has to be greppable.
	/// </summary>
	public class KingdomBudgetRulesTests
	{
		/// <summary>Every lane in the enum has a row, and the row it has is its own. A lane that
		/// fell out of the table would otherwise judge everything as within budget.</summary>
		[Test]
		public void EveryLaneHasItsOwnRow()
		{
			Array lanes = Enum.GetValues(typeof(KingdomBudgetLane));
			Assert.AreEqual(lanes.Length, KingdomBudgetRules.LaneCount, "a lane has no row in the table");
			foreach (object value in lanes)
			{
				KingdomBudgetLane lane = (KingdomBudgetLane)value;
				KingdomBudgetRow row;
				Assert.IsTrue(KingdomBudgetRules.TryRow(lane, out row), lane + " has no row");
				Assert.AreEqual(lane, row.Lane);
				Assert.IsFalse(string.IsNullOrEmpty(row.LogName), lane + " has no log name");
			}
		}

		[Test]
		public void ALaneOutsideTheTableIsRefusedRatherThanDefaulted()
		{
			KingdomBudgetRow row;
			Assert.IsFalse(KingdomBudgetRules.TryRow((KingdomBudgetLane)200, out row));
		}

		/// <summary>LIVING-CITY-ARCHITECTURE §0.0, the time rungs, in microseconds.</summary>
		[TestCase((int)KingdomBudgetLane.Reckon, 2000L, 8000L)]
		[TestCase((int)KingdomBudgetLane.Reify, 1000L, 2000L)]
		[TestCase((int)KingdomBudgetLane.Heartbeat, 300L, 500L)]
		public void TheTimeRungsAreTheConstitutionsOwn(int laneCode, long warn, long fail)
		{
			KingdomBudgetRow row;
			Assert.IsTrue(KingdomBudgetRules.TryRow((KingdomBudgetLane)laneCode, out row));
			Assert.AreEqual(warn, row.WarnMicroseconds);
			Assert.AreEqual(fail, row.FailMicroseconds);
		}

		/// <summary>LIVING-CITY-ARCHITECTURE §0.0, the count rungs.</summary>
		[TestCase((int)KingdomBudgetLane.Reckon, -1L, 512L)]
		[TestCase((int)KingdomBudgetLane.Reify, -1L, 8L)]
		[TestCase((int)KingdomBudgetLane.Heartbeat, -1L, 4L)]
		[TestCase((int)KingdomBudgetLane.HeartbeatAmortised, 10L, 20L)]
		[TestCase((int)KingdomBudgetLane.CatchUpDrain, 40L, -1L)]
		// 56 KiB, not 48: §0.0 raised the ADVISORY rung in W1 because the formula's own total at
		// today's caps sat above the old one, and a warning a design is permanently inside tells a
		// tester nothing. The ceiling is untouched at 64 KiB -- warn is advice, the ceiling is the
		// contract.
		[TestCase((int)KingdomBudgetLane.ModelBytes, 57344L, 65536L)]
		[TestCase((int)KingdomBudgetLane.SaveBytes, 32768L, 98304L)]
		[TestCase((int)KingdomBudgetLane.RoutePlan, 2000L, -1L)]
		[TestCase((int)KingdomBudgetLane.NetworkSolve, 8000L, 12000L)]
		[TestCase((int)KingdomBudgetLane.ResidentZones, 1L, 2L)]
		public void TheCountRungsAreTheConstitutionsOwn(int laneCode, long warn, long fail)
		{
			KingdomBudgetRow row;
			Assert.IsTrue(KingdomBudgetRules.TryRow((KingdomBudgetLane)laneCode, out row));
			Assert.AreEqual(warn, row.WarnCount);
			Assert.AreEqual(fail, row.FailCount);
		}

		/// <summary>Every named cap the design quotes, pinned where it lives.</summary>
		[Test]
		public void TheNamedCapsAreTheConstitutionsOwn()
		{
			Assert.AreEqual(64, KingdomBudgetRules.MaxBreakpoints);
			Assert.AreEqual(512, KingdomBudgetRules.MaxDrawsPerCityPass);
			Assert.AreEqual(8, KingdomBudgetRules.ReifyUnitsPerTurn);
			Assert.AreEqual(4, KingdomBudgetRules.ReifyHeavyMintsPerTurn);
			Assert.AreEqual(24, KingdomBudgetRules.ReifyLightUnitsPerTurn);
			Assert.AreEqual(50, KingdomBudgetRules.HeartbeatCadenceTicks);
			Assert.AreEqual(4, KingdomBudgetRules.HeartbeatStepsPerSlice);
			Assert.AreEqual(1, KingdomBudgetRules.HeartbeatToldLinesPerSlice);
			Assert.AreEqual(65536L, KingdomBudgetRules.ModelBytesCeiling);
			Assert.AreEqual(16, KingdomBudgetRules.PlannerMaxJobs);
			Assert.AreEqual(8, KingdomBudgetRules.PlannerMaxStops);
			Assert.AreEqual(50, KingdomBudgetRules.PlannerMaxSwapTests);
			Assert.AreEqual(0, KingdomBudgetRules.PlannerMaxDraws, "routing is arithmetic, not chance");
			Assert.AreEqual(4, KingdomBudgetRules.NetworksPerCity);
			Assert.AreEqual(32, KingdomBudgetRules.NetworkMaxNodes);
			Assert.AreEqual(48, KingdomBudgetRules.NetworkMaxEdges);
		}

		/// <summary>The network solve's own budget, composed rather than quoted: four networks of
		/// 32 nodes and 48 edges is 5,120 node-visits, comfortably inside the 8,000 warn rung.
		/// LIVING-CITY-ARCHITECTURE §0.0 / §3.11.</summary>
		[Test]
		public void TheNetworkBudgetComposesToFiveThousandOneHundredAndTwenty()
		{
			int visits = KingdomBudgetRules.NetworksPerCity * (KingdomBudgetRules.NetworkMaxNodes + KingdomBudgetRules.NetworkMaxEdges) * 16;
			Assert.AreEqual(5120, visits);
			Assert.AreEqual(KingdomBudgetVerdict.Within, KingdomBudgetRules.JudgeCount(KingdomBudgetLane.NetworkSolve, visits));
		}

		/// <summary>A budget of eight passes at eight and fails at nine: strictly greater, never
		/// "at or above", or every budget would be one short of what the table says.</summary>
		[TestCase(0L, (int)KingdomBudgetVerdict.Within)]
		[TestCase(2000L, (int)KingdomBudgetVerdict.Within)]
		[TestCase(2001L, (int)KingdomBudgetVerdict.Warn)]
		[TestCase(8000L, (int)KingdomBudgetVerdict.Warn)]
		[TestCase(8001L, (int)KingdomBudgetVerdict.Over)]
		public void AVerdictIsReachedByStrictComparison(long microseconds, int expected)
		{
			Assert.AreEqual((KingdomBudgetVerdict)expected, KingdomBudgetRules.JudgeMicroseconds(KingdomBudgetLane.Reckon, microseconds));
		}

		/// <summary>A rung the table gives no number for never fires. The catch-up lane's failure is
		/// a counter that never reaches zero, which is a shape and not a threshold.</summary>
		[TestCase(41L, (int)KingdomBudgetVerdict.Warn)]
		[TestCase(100000L, (int)KingdomBudgetVerdict.Warn)]
		public void ALaneWithNoNumericFailNeverReadsOver(long turns, int expected)
		{
			Assert.AreEqual((KingdomBudgetVerdict)expected, KingdomBudgetRules.JudgeCount(KingdomBudgetLane.CatchUpDrain, turns));
		}

		[Test]
		public void ALaneWithNoTimeBudgetIsNeverJudgedOnTime()
		{
			Assert.AreEqual(KingdomBudgetVerdict.Within, KingdomBudgetRules.JudgeMicroseconds(KingdomBudgetLane.Thaw, 60000L));
			Assert.AreEqual(KingdomBudgetVerdict.Within, KingdomBudgetRules.JudgeMicroseconds(KingdomBudgetLane.ModelBytes, 60000L));
		}

		[TestCase((int)KingdomBudgetVerdict.Within, (int)KingdomBudgetVerdict.Over, (int)KingdomBudgetVerdict.Over)]
		[TestCase((int)KingdomBudgetVerdict.Warn, (int)KingdomBudgetVerdict.Within, (int)KingdomBudgetVerdict.Warn)]
		[TestCase((int)KingdomBudgetVerdict.Warn, (int)KingdomBudgetVerdict.Warn, (int)KingdomBudgetVerdict.Warn)]
		[TestCase((int)KingdomBudgetVerdict.Within, (int)KingdomBudgetVerdict.Within, (int)KingdomBudgetVerdict.Within)]
		public void TheWorseOfTwoRungsIsWhatALogLineReports(int left, int right, int expected)
		{
			Assert.AreEqual((KingdomBudgetVerdict)expected, KingdomBudgetRules.Worse((KingdomBudgetVerdict)left, (KingdomBudgetVerdict)right));
		}

		/// <summary>The row-visit ceiling is B x 2R over the live R, not the 14,848 the table quotes
		/// for today's caps. LIVING-CITY-ARCHITECTURE §0.0(f).</summary>
		[Test]
		public void TheRowVisitCeilingSurvivesTheCapMoving()
		{
			long ceiling;
			Assert.IsTrue(KingdomBudgetRules.TryMaxRowVisits(116, out ceiling));
			Assert.AreEqual(14848L, ceiling);
			Assert.IsTrue(KingdomBudgetRules.TryMaxRowVisits(246, out ceiling));
			Assert.AreEqual(31488L, ceiling);
			Assert.IsFalse(KingdomBudgetRules.TryMaxRowVisits(-1, out ceiling));
		}

		// ---- The receipt line ------------------------------------------------------------

		[Test]
		public void AHealthyReckonReadsLikeSectionSixFivesExample()
		{
			KingdomPerfReceipt receipt = new KingdomPerfReceipt(
				KingdomBudgetLane.Reckon,
				"Kavvat",
				1400L,
				new KingdomComputeCounters(41, 4756L, 118, 0, 0L),
				118L,
				KingdomBudgetVerdict.Within,
				KingdomBudgetVerdict.Within);
			Assert.AreEqual("[TAF] perf reckon label=Kavvat steps=41 rows=4756 draws=118 ms=1.4",
				KingdomBudgetRules.FormatReceipt(receipt));
		}

		/// <summary>A figure that crosses a budget is prefixed BUDGET and names the budget it broke,
		/// so a failure is legible without the tester holding the table in their head.</summary>
		[Test]
		public void AnOverBudgetReifyNamesTheBudgetItBroke()
		{
			KingdomPerfReceipt receipt = new KingdomPerfReceipt(
				KingdomBudgetLane.Reify,
				"taf:zone:a",
				2400L,
				KingdomComputeCounters.None,
				0L,
				KingdomBudgetVerdict.Over,
				KingdomBudgetVerdict.Within);
			Assert.AreEqual("[TAF] perf BUDGET reify label=taf:zone:a ms=2.4 over=2",
				KingdomBudgetRules.FormatReceipt(receipt));
		}

		[Test]
		public void AnOverBudgetCountNamesItsOwnCeiling()
		{
			KingdomPerfReceipt receipt = new KingdomPerfReceipt(
				KingdomBudgetLane.Reckon,
				"Kavvat",
				100L,
				new KingdomComputeCounters(0, 0L, 513, 0, 0L),
				513L,
				KingdomBudgetVerdict.Within,
				KingdomBudgetVerdict.Over);
			Assert.AreEqual("[TAF] perf BUDGET reckon label=Kavvat draws=513 ms=0.1 over=512",
				KingdomBudgetRules.FormatReceipt(receipt));
		}

		/// <summary>Deterministic and invariant, so a receipt reads the same on every machine.</summary>
		[TestCase(0L, "0")]
		[TestCase(90L, "0.09")]
		[TestCase(600L, "0.6")]
		[TestCase(1400L, "1.4")]
		[TestCase(31200L, "31.2")]
		public void MillisecondsPrintTheSameEverywhere(long microseconds, string expected)
		{
			Assert.AreEqual(expected, KingdomBudgetRules.FormatMilliseconds(microseconds));
		}

		[Test]
		public void ZeroCountersAreLeftOutOfTheLineAndTheTimeNeverIs()
		{
			KingdomPerfReceipt receipt = new KingdomPerfReceipt(
				KingdomBudgetLane.Thaw,
				"taf:zone:a",
				31200L,
				KingdomComputeCounters.None,
				0L,
				KingdomBudgetVerdict.Within,
				KingdomBudgetVerdict.Within);
			Assert.AreEqual("[TAF] perf thaw label=taf:zone:a ms=31.2", KingdomBudgetRules.FormatReceipt(receipt));
		}
	}
}
#endif
