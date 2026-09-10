#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	public class KingdomScenarioTravelRulesTests
	{
		private const string Home = "JoppaWorld.8.22.0.0.10";
		private const string West = "JoppaWorld.7.22.2.0.10";

		[TestCase(null)]
		[TestCase("")]
		[TestCase("JoppaWorld.8.22.0.0.11")]
		[TestCase("JoppaWorld.8.22.3.0.10")]
		[TestCase("JoppaWorld.-8.22.0.0.10")]
		[TestCase("JoppaWorld.8.22.0.0.10.extra")]
		public void RejectMalformedOrUnsupportedZone(string id)
			=> ClassicAssert.IsFalse(KingdomScenarioTravelRules.Zone(id, out _, out _, out _, out _, out _, out _));

		[Test]
		public void ExactInteriorAndBorderStepsAreReversible()
		{
			ClassicAssert.IsTrue(KingdomScenarioTravelRules.Step(Home, 40, 12, Home, 39, 12, true));
			ClassicAssert.IsTrue(KingdomScenarioTravelRules.Step(Home, 39, 12, Home, 40, 12, false));
			ClassicAssert.IsTrue(KingdomScenarioTravelRules.Step(Home, 0, 12, West, 79, 12, true));
			ClassicAssert.IsTrue(KingdomScenarioTravelRules.Step(West, 79, 12, Home, 0, 12, false));
			ClassicAssert.IsTrue(KingdomScenarioTravelRules.DifferentParasang(Home, West));
			ClassicAssert.IsFalse(KingdomScenarioTravelRules.DifferentParasang(Home, "JoppaWorld.8.22.1.0.10"));
		}

		[TestCase(40, 12, 38, 12)]
		[TestCase(40, 12, 39, 13)]
		[TestCase(-1, 12, 0, 12)]
		[TestCase(80, 12, 79, 12)]
		[TestCase(40, 25, 39, 25)]
		public void RefusesTeleportAndInvalidCell(int x, int y, int nextX, int nextY)
			=> ClassicAssert.IsFalse(KingdomScenarioTravelRules.Step(Home, x, y, Home, nextX, nextY, true));

		[Test]
		public void RefusesWrongBorderWorldOrDirection()
		{
			ClassicAssert.IsFalse(KingdomScenarioTravelRules.Step(Home, 0, 12, Home, 79, 12, true));
			ClassicAssert.IsFalse(KingdomScenarioTravelRules.Step(Home, 0, 12, "Other.7.22.2.0.10", 79, 12, true));
			ClassicAssert.IsFalse(KingdomScenarioTravelRules.Step(Home, 0, 12, "JoppaWorld.7.23.2.0.10", 79, 12, true));
		}

		[TestCase(10, 10, 20, true)]
		[TestCase(10, 20, 20, true)]
		[TestCase(-1, 10, 20, false)]
		[TestCase(10, 9, 20, false)]
		[TestCase(10, 21, 20, false)]
		public void ClockIsMonotoneAndNotFuture(long before, long after, long now, bool expected)
			=> ClassicAssert.AreEqual(expected, KingdomScenarioTravelRules.Clock(before, after, now));

		[TestCase(24, 4, true)]
		[TestCase(0, 0, true)]
		[TestCase(25, 4, false)]
		[TestCase(24, 5, false)]
		[TestCase(-1, 0, false)]
		[TestCase(0, -1, false)]
		public void BudgetPins(int thirds, int heavy, bool expected)
			=> ClassicAssert.AreEqual(expected, KingdomScenarioTravelRules.Budget(thirds, heavy));

		[TestCase(100, 139, 0, true)]
		[TestCase(100, 100, 0, true)]
		[TestCase(100, 140, 0, false)]
		[TestCase(100, -1, 0, false)]
		[TestCase(100, 99, 0, false)]
		[TestCase(100, 110, 1, false)]
		[TestCase(-1, 0, 0, false)]
		public void DrainRequiresObservedZeroWithinEnvelope(long began, long zero, int owed, bool expected)
			=> ClassicAssert.AreEqual(expected, KingdomScenarioTravelRules.Drained(began, zero, owed));

		[TestCase(100, 3, 100, 3, true)]
		[TestCase(100, 3, 200, 4, true)]
		[TestCase(100, 3, 99, 4, false)]
		[TestCase(100, 3, 200, 2, false)]
		[TestCase(-1, 3, 200, 4, false)]
		[TestCase(100, -1, 200, 4, false)]
		public void ScheduleNeverRewinds(long before, int ordinal, long after, int nextOrdinal, bool expected)
			=> ClassicAssert.AreEqual(expected, KingdomScenarioTravelRules.Schedule(before, ordinal, after, nextOrdinal));

		[Test]
		public void RuntimeRequiresPhysicalEvidenceAndExplicitPauseProof()
		{
			string source = TestMain.ReadRepositoryText("Harness/KingdomScenarioTravel.cs");
			StringAssert.Contains("ReturnDemandObserved && RemainingDemand == 0", source);
			StringAssert.Contains("KingdomScenarioTravelRules.Drained(FirstHomeTurn, ZeroTurn, owed)", source);
			StringAssert.Contains("KingdomScenarioPauseController.Check()", source);
			StringAssert.Contains("if (!Active) return \"; pause-effects-proved=false\"",
				TestMain.ReadRepositoryText("Harness/KingdomScenarioPauseController.cs"));
			StringAssert.Contains("!The.ZoneManager.CachedZones.ContainsKey(Home)", source);
			StringAssert.Contains("Player.Move(west ? \"W\" : \"E\", AllowDashing: false, DoConfirmations: false)", source);
		}

		[Test]
		public void ObserverResetsZeroWhenPhysicalDebtReturns()
		{
			string source = TestMain.ReadRepositoryText("Harness/KingdomScenarioTravelDriver.cs");
			StringAssert.Contains("[HarmonyPatch(typeof(KingdomCity), \"Receipt\")]", source);
			StringAssert.Contains("if (owed != 0) KingdomScenarioTravel.ZeroTurn = -1", source);
			StringAssert.Contains("KingdomScenarioTravel.RemainingDemand = owed", source);
		}
	}
}
#endif
