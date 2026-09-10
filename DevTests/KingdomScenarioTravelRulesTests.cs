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

		[TestCase(100, 139, true)]
		[TestCase(100, 140, true)]
		[TestCase(100, 1000, true)]
		[TestCase(100, 138, false)]
		[TestCase(100, 99, false)]
		[TestCase(-1, 139, false)]
		[TestCase(long.MaxValue - 39, long.MaxValue, true)]
		public void DrainObservationMayBeLateButNeverEarly(long arrived, long observed, bool expected)
			=> ClassicAssert.AreEqual(expected, KingdomScenarioTravelRules.DrainObservationReady(arrived, observed));

		[Test]
		public void LateObservationDoesNotExtendPhysicalDrainDeadline()
		{
			ClassicAssert.IsTrue(KingdomScenarioTravelRules.DrainObservationReady(100, 140));
			ClassicAssert.IsTrue(KingdomScenarioTravelRules.Drained(100, 139, 0));
			ClassicAssert.IsFalse(KingdomScenarioTravelRules.Drained(100, 140, 0));
			ClassicAssert.IsFalse(KingdomScenarioTravelRules.Drained(100, 139, 1));
		}

		[TestCase(false, 0, 0, -1)]
		[TestCase(true, 0, 0, 0)]
		[TestCase(true, 756, 0, 756)]
		[TestCase(true, -1, 0, -1)]
		[TestCase(true, 0, -1, -1)]
		[TestCase(true, 937, 0, -1)]
		[TestCase(true, int.MaxValue, int.MaxValue, -1)]
		public void PhysicalDemandNeverTurnsUnknownOrOverflowIntoZero(bool measured, int containers,
			int bodies, int expected)
		{
			bool valid = KingdomScenarioTravelRules.TryPhysicalDemand(measured, containers, bodies, false, out int thirds);
			ClassicAssert.AreEqual(expected >= 0, valid);
			ClassicAssert.AreEqual(expected, thirds);
		}

		[Test]
		public void PhysicalDemandWeightsMisplacedBodyCount()
		{
			int weight = ThousandAndFirst.Simulation.City.KingdomCatchUpRules.WeightThirds(
				ThousandAndFirst.Simulation.City.KingdomUnitWeight.Heavy);
			ClassicAssert.IsTrue(KingdomScenarioTravelRules.TryPhysicalDemand(true, 3, 2, false, out int thirds));
			ClassicAssert.AreEqual(3 + 2 * weight, thirds);
		}

		[TestCase(0, 0, 0, true)]
		[TestCase(1, 0, 0, false)]
		[TestCase(0, 1, 0, false)]
		[TestCase(0, 0, 1, false)]
		[TestCase(-1, 0, 0, false)]
		[TestCase(0, -1, 0, false)]
		[TestCase(0, 0, -1, false)]
		public void ContainerOnlyFixtureCannotAcceptUnmeasuredResidents(int population, int rows, int bodies, bool expected)
			=> ClassicAssert.AreEqual(expected, KingdomScenarioTravelRules.ContainerOnlyAdmission(population, rows, bodies));

		[Test]
		public void BlockedDebtCannotMasqueradeAsZeroExecutableDemand()
		{
			ClassicAssert.IsFalse(KingdomScenarioTravelRules.TryPhysicalDemand(true, 0, 0, true, out int thirds));
			ClassicAssert.AreEqual(-1, thirds);
		}

		[Test]
		public void BookDebtNeverRestampsZeroAndLateSettlementRemainsLate()
		{
			ClassicAssert.IsTrue(KingdomScenarioTravelRules.TryObserveZero(100, -1, 110, 0, false, out long zero));
			ClassicAssert.AreEqual(-1, zero);
			ClassicAssert.IsTrue(KingdomScenarioTravelRules.TryObserveZero(100, zero, 140, 0, true, out zero));
			ClassicAssert.AreEqual(140, zero);
			ClassicAssert.IsFalse(KingdomScenarioTravelRules.Drained(100, zero, 0));
		}

		[Test]
		public void NewBookOrPhysicalDebtInvalidatesAnEarlierZero()
		{
			foreach (bool settled in new[] { false, true })
			{
				ClassicAssert.IsTrue(KingdomScenarioTravelRules.TryObserveZero(100, 110, 120,
					settled ? 3 : 0, settled, out long zero));
				ClassicAssert.AreEqual(-1, zero);
				ClassicAssert.IsTrue(KingdomScenarioTravelRules.TryObserveZero(100, zero, 140, 0, true, out zero));
				ClassicAssert.IsFalse(KingdomScenarioTravelRules.Drained(100, zero, 0));
			}
		}

		[TestCase(100, -1, 139, 0, true, 139)]
		[TestCase(100, 139, 140, 0, true, 139)]
		[TestCase(100, 139, 140, 1, true, -1)]
		[TestCase(100, 139, 140, 0, false, -1)]
		public void ZeroObservationKeepsOnlyUninterruptedProof(long first, long previous, long now,
			int physical, bool settled, long expected)
		{
			ClassicAssert.IsTrue(KingdomScenarioTravelRules.TryObserveZero(first, previous, now, physical, settled, out long zero));
			ClassicAssert.AreEqual(expected, zero);
		}

		[TestCase(-1, -1, 100, 0)]
		[TestCase(100, -1, 99, 0)]
		[TestCase(100, 99, 110, 0)]
		[TestCase(100, 111, 110, 0)]
		[TestCase(100, -2, 110, 0)]
		[TestCase(100, -1, 110, -1)]
		public void InvalidPhysicalObservationRefuses(long first, long previous, long now, int physical)
			=> ClassicAssert.IsFalse(KingdomScenarioTravelRules.TryObserveZero(first, previous, now, physical, true, out _));

		[TestCase(false, 0, null, 0, 7, 0, true)]
		[TestCase(true, 100, "home", 7, 7, 100, true)]
		[TestCase(true, 100, "home", 3, 7, 100, false)]
		[TestCase(true, 100, "home", 7, 7, 99, false)]
		[TestCase(true, 100, "home", 0, 7, 99, false)]
		[TestCase(true, 0, "home", 7, 7, 100, false)]
		[TestCase(true, 100, null, 7, 7, 100, false)]
		[TestCase(true, 100, "home", 7, 0, 100, false)]
		[TestCase(true, 100, "home", -1, 7, 100, false)]
		public void PauseAllowsPublishedReceiptButNotTornPass(bool active, long started, string bound,
			long completed, long required, long published, bool expected)
			=> ClassicAssert.AreEqual(expected, KingdomScenarioTravelRules.SemanticPauseReady(
				active, started, bound, completed, required, published, "home"));

		[Test]
		public void PauseUsesProductionRequiredMaskAndPublicationLaw()
		{
			StringAssert.Contains("System.NativeTravelSemanticPauseReady()",
				TestMain.ReadRepositoryText("Harness/KingdomScenarioPauseWitness.cs"));
			string source = TestMain.ReadRepositoryText("Harness/KingdomSystem.NativeTravelObservation.cs");
			StringAssert.Contains("SemanticPassCompletedMask, SemanticRequiredMask, LastSemanticTick", source);
			StringAssert.Contains("!KingdomSurvey.HasBoundPass", source);
			StringAssert.Contains("ReferenceEquals(The.Game?.GetSystem<KingdomSystem>(), this)", source);
		}

		[Test]
		public void QuietGroundUsesMeasuredPhysicalRowsNotAnInventedSpendReceipt()
		{
			string source = TestMain.ReadRepositoryText("Harness/KingdomCity.NativeTravelDemand.cs");
			StringAssert.Contains("ContainerGround.Take(survey)", source);
			StringAssert.Contains("KingdomSurvey.TryTakeUnboundRecovery(Zone, out var survey)", source);
			StringAssert.DoesNotContain("KingdomSurvey.Take(", source);
			StringAssert.Contains("receipt.WaterBlocked != 0 || receipt.FoodBlocked != 0 || receipt.MaterialsBlocked != 0", source);
			StringAssert.Contains("KingdomContainerCatchUpRules.TryMeasure", source);
			StringAssert.Contains("if (!measured) return false", source);
			StringAssert.Contains("KingdomScenarioTravelRules.ContainerOnlyAdmission(System.Population", source);
			StringAssert.Contains("book.ResidentIds.Count, citizens", source);
			StringAssert.Contains("item.GetIntProperty(\"KingdomCitizen\") != 0 || item.GetPart<r_KingdomCitizenship>() != null", source);
			StringAssert.DoesNotContain("Posted(", source);
			StringAssert.DoesNotContain("GroundDemandThirds(", source);
			StringAssert.DoesNotContain("SpendTurn(", source);
			StringAssert.DoesNotContain("Publish(", source);
			StringAssert.DoesNotContain("Reify(", source);
			StringAssert.Contains("KingdomScenarioTravelDemandObserver.ObserveGround()",
				TestMain.ReadRepositoryText("Harness/KingdomScenarioTravel.cs"));
		}

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
			StringAssert.Contains("KingdomScenarioTravelRules.DrainObservationReady(ArrivedTurn, Game.Turns)", source);
			StringAssert.Contains("KingdomScenarioPauseController.Check()", source);
			StringAssert.Contains("if (!Active) return \"; pause-effects-proved=false\"",
				TestMain.ReadRepositoryText("Harness/KingdomScenarioPauseController.cs"));
			StringAssert.Contains("!The.ZoneManager.CachedZones.ContainsKey(Home)", source);
			StringAssert.Contains("system.City.TryZoneRow(zone.ZoneID, out _)", source);
			StringAssert.Contains("Player.Move(west ? \"W\" : \"E\", AllowDashing: false, DoConfirmations: false)", source);
		}

		[Test]
		public void AllTravelRecipesWarmUpThroughRealDailyReconciliation()
		{
			ClassicAssert.AreEqual(1200, KingdomRules.TicksPerDay);
			foreach (string name in new[] { "beta-travel-away", "beta-travel-present",
				"beta-economic-away", "beta-economic-present" })
				StringAssert.Contains("SCRIPT=stagedigest;realize;advance 1200;",
					TestMain.ReadRepositoryText("Tools/personas/" + name + ".persona"));
			foreach (string name in new[] { "KingdomScenarioTravelProvider", "KingdomScenarioPauseController" })
				StringAssert.Contains("\"stagedigest\", \"realize\", \"advance 1200\"",
					TestMain.ReadRepositoryText("Harness/" + name + ".cs"));
		}

		[Test]
		public void ObserverResetsZeroWhenPhysicalDebtReturns()
		{
			string source = TestMain.ReadRepositoryText("Harness/KingdomScenarioTravelDriver.cs");
			StringAssert.Contains("[HarmonyPatch(typeof(KingdomCity), \"Receipt\")]", source);
			StringAssert.Contains("KingdomScenarioTravelRules.TryObserveZero(", source);
			StringAssert.Contains("physicalThirds, settled, out long zero", source);
			StringAssert.Contains("if (KingdomSurvey.HasBoundPass) return", source);
			StringAssert.Contains("KingdomScenarioTravel.RemainingDemand = physicalThirds", source);
		}
	}
}
#endif
