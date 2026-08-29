#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomBountyManningRulesTests
	{
		[Test]
		public void ThirtyServicedDaysAreRequiredExactly()
		{
			Assert.AreEqual((long)KingdomBountyRules.ManningSeasonDays
				* KingdomRules.TicksPerDay, KingdomBountyManningRules.RequiredTicks);
			Assert.AreEqual(30, KingdomBountyManningRules.RemainingDays(0L));
		}

		[Test]
		public void DailyAndSingleIntervalAccrualAreIdentical()
		{
			long one = KingdomBountyManningRules.Accrue(0L, 1L,
				1L + KingdomBountyManningRules.RequiredTicks, true, true);
			long daily = 0L;
			long checkpoint = 1L;
			for (int i = 0; i < KingdomBountyRules.ManningSeasonDays; i++)
			{
				long next = checkpoint + KingdomRules.TicksPerDay;
				daily = KingdomBountyManningRules.Accrue(daily, checkpoint, next, true, true);
				checkpoint = next;
			}
			Assert.AreEqual(KingdomBountyManningRules.RequiredTicks, one);
			Assert.AreEqual(one, daily);
		}

		[TestCase(false, true)]
		[TestCase(true, false)]
		[TestCase(false, false)]
		public void MissingAssignmentOrExactEndpointCreditsNothing(bool Assigned, bool Exact)
		{
			Assert.AreEqual(17L, KingdomBountyManningRules.Accrue(17L, 1L,
				1L + 400L * KingdomRules.TicksPerDay, Assigned, Exact));
		}

		[Test]
		public void ServiceAndForecastSaturateWithoutWrapping()
		{
			Assert.AreEqual(KingdomBountyManningRules.RequiredTicks,
				KingdomBountyManningRules.ClampServed(long.MaxValue));
			Assert.AreEqual(0L, KingdomBountyManningRules.RemainingTicks(long.MaxValue));
			Assert.AreEqual(long.MaxValue, KingdomBountyManningRules.ForecastDueTick(
				long.MaxValue - 1L, 0L, true));
			Assert.AreEqual(0L, KingdomBountyManningRules.ForecastDueTick(5L, 0L, false));
		}

		[Test]
		public void RegressedClockIsRefusedWithoutAuthorizingARewoundCheckpoint()
		{
			long served;
			Assert.IsFalse(KingdomBountyManningRules.TryAccrue(17L, 100L, 90L,
				true, true, out served));
			Assert.AreEqual(17L, served);
			Assert.IsTrue(KingdomBountyManningRules.TryAccrue(served, 100L, 110L,
				true, true, out served));
			Assert.AreEqual(27L, served);
			Assert.IsFalse(KingdomBountyManningRules.TryAccrue(-1L, 0L, 1L,
				false, false, out served));
		}

		[Test]
		public void RuntimePinsOptionAndEndpointEpochsAroundOrdinaryCrewPublication()
		{
			string stations = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomStations.cs");
			StringAssert.Contains("AvailabilityEpochProperty", stations);
			StringAssert.Contains("TouchAvailability(Settler)", stations);
			string growth = TestMain.ReadRepositoryText(
				"Growth/KingdomGrowth.z15.WorkAssignment.cs");
			StringAssert.Contains("postIds[at] = postId", growth);
			StringAssert.Contains(
				"KingdomStations.Post(available[i], postIds[i], postKinds[i])", growth);
			string physical = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomPhysicalHappenings.02.ObservePrepareAndUse.cs");
			StringAssert.Contains("KingdomStations.TouchAvailability(body)", physical);
			string survey = TestMain.ReadRepositoryText(
				"Growth/KingdomSurvey.02.IndexMaintenance.cs");
			StringAssert.Contains("if (row.Work || row.ResidentId > 0)", survey);
			string bounty = KingdomBountyLogicalSource.Read();
			StringAssert.Contains("ApplyManningOption(data, now, option)", bounty);
			StringAssert.Contains("workEpoch == data.ManningWorkEpoch", bounty);
			StringAssert.Contains("data.ManningResidentEpoch", bounty);
			StringAssert.Contains("KingdomBountyManningRules.TryAccrue", bounty);
		}
	}
}
#endif
