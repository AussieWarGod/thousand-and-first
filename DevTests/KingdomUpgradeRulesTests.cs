#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomUpgradeRulesTests
	{
		// --- CostDrams: what an improvement is worth, and the two clamps around it -------------

		[TestCase(16, 4, KingdomUpgradeRules.Unset, 12)]
		[TestCase(14, 6, KingdomUpgradeRules.Unset, 8)]
		[TestCase(20, 0, KingdomUpgradeRules.Unset, 20)]
		public void CostDrams_DefaultsToTheDifferenceBetweenTheTwoDesigns(int successor, int predecessor, int over, int expected)
		{
			Assert.AreEqual(expected, KingdomUpgradeRules.CostDrams(successor, predecessor, over));
		}

		[TestCase(10, 10)]
		[TestCase(10, 12)]
		[TestCase(10, 100)]
		public void CostDrams_NeverFree_EvenWhenTheSuccessorIsNoDearer(int successor, int predecessor)
		{
			// Something is always carried, mixed, and poured. A mutation dropping the floor
			// returns 0 (or a negative) for every one of these.
			Assert.AreEqual(KingdomUpgradeRules.MinimumCostDrams, KingdomUpgradeRules.CostDrams(successor, predecessor, KingdomUpgradeRules.Unset));
		}

		[TestCase(1, 0)]
		[TestCase(1, 5)]
		[TestCase(0, 0)]
		public void CostDrams_NeverDearerThanBuildingTheSuccessorFresh(int successor, int predecessor)
		{
			// The floor and the cap disagree here, and the cap must win: improving must never
			// cost more than razing and building new would have.
			int cost = KingdomUpgradeRules.CostDrams(successor, predecessor, KingdomUpgradeRules.Unset);
			Assert.AreEqual(successor, cost);
			Assert.LessOrEqual(cost, successor);
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(99)]
		public void CostDrams_AnAuthoredCostIsTakenExactly(int over)
		{
			// Including zero: an author who says an improvement is free is obeyed, because the
			// floor exists to stop the ARITHMETIC producing free, not to overrule the author.
			Assert.AreEqual(over, KingdomUpgradeRules.CostDrams(16, 4, over));
		}

		[Test]
		public void CostDrams_TreatsNegativeDesignCostsAsNothingAndLetsTheCapWin()
		{
			// A successor worth nothing costs nothing, floor or no floor: the cap is applied after
			// the floor precisely so improving can never be dearer than the design itself.
			Assert.AreEqual(0, KingdomUpgradeRules.CostDrams(-5, -5, KingdomUpgradeRules.Unset));
			Assert.AreEqual(0, KingdomUpgradeRules.CostDrams(0, -5, KingdomUpgradeRules.Unset));
		}

		// --- BuildTicks -----------------------------------------------------------------------

		[TestCase(3600L, 2700L)]
		[TestCase(1200L, 900L)]
		[TestCase(2400L, 1800L)]
		public void BuildTicks_DefaultsToAFractionOfBuildingItFresh(long successorTicks, long expected)
		{
			Assert.AreEqual(expected, KingdomUpgradeRules.BuildTicks(successorTicks, KingdomUpgradeRules.UnsetTicks));
			Assert.AreEqual(expected, successorTicks * KingdomUpgradeRules.BuildTicksPercent / 100L);
		}

		[TestCase(0L)]
		[TestCase(1L)]
		[TestCase(-100L)]
		public void BuildTicks_NeverCompletesInTheSameInstantOrThePast(long successorTicks)
		{
			Assert.AreEqual(1L, KingdomUpgradeRules.BuildTicks(successorTicks, KingdomUpgradeRules.UnsetTicks));
		}

		[TestCase(50L, 50L)]
		[TestCase(1L, 1L)]
		public void BuildTicks_AnAuthoredTimeIsTakenExactly(long over, long expected)
		{
			Assert.AreEqual(expected, KingdomUpgradeRules.BuildTicks(3600L, over));
		}

		[Test]
		public void BuildTicks_ANonPositiveOverrideIsNoOverride()
		{
			Assert.AreEqual(2700L, KingdomUpgradeRules.BuildTicks(3600L, KingdomUpgradeRules.UnsetTicks));
			Assert.AreEqual(2700L, KingdomUpgradeRules.BuildTicks(3600L, -1L));
		}

		// --- CrewRequired ---------------------------------------------------------------------

		[TestCase(3, KingdomUpgradeRules.Unset, 3)]
		[TestCase(2, KingdomUpgradeRules.Unset, 2)]
		[TestCase(0, KingdomUpgradeRules.Unset, KingdomUpgradeRules.MinimumCrew)]
		[TestCase(0, 0, KingdomUpgradeRules.MinimumCrew)]
		[TestCase(5, 0, KingdomUpgradeRules.MinimumCrew)]
		[TestCase(1, 4, 4)]
		public void CrewRequired_DefaultsToTheCrewTheSuccessorWillNeedAndNeverNobody(int successorStaff, int over, int expected)
		{
			// "Somebody does the work; nobody does it for nothing" - the floor holds against the
			// successor's own Staff AND against an authored zero.
			Assert.AreEqual(expected, KingdomUpgradeRules.CrewRequired(successorStaff, over));
		}

		// --- StageRequired: an override may raise the gate, never lower it ---------------------

		[TestCase(GrowthStage.Steading, false, GrowthStage.Camp, GrowthStage.Steading)]
		[TestCase(GrowthStage.Village, false, GrowthStage.City, GrowthStage.Village)]
		[TestCase(GrowthStage.Steading, true, GrowthStage.Town, GrowthStage.Town)]
		[TestCase(GrowthStage.Town, true, GrowthStage.Camp, GrowthStage.Town)]
		[TestCase(GrowthStage.Town, true, GrowthStage.Town, GrowthStage.Town)]
		public void StageRequired_InheritsTheSuccessorsGateAndOnlyEverTightensIt(GrowthStage successorMinStage, bool hasOverride, GrowthStage over, GrowthStage expected)
		{
			// A chain that could lower the gate would let a work sneak past the stage the
			// commission list already enforces for the same design.
			Assert.AreEqual(expected, KingdomUpgradeRules.StageRequired(successorMinStage, hasOverride, over));
		}

		// --- ReserveDrams / CanAfford / Shortfall ---------------------------------------------

		[TestCase(10, GrowthStage.Camp)]
		[TestCase(10, GrowthStage.City)]
		[TestCase(0, GrowthStage.Village)]
		[TestCase(37, GrowthStage.Town)]
		public void ReserveDrams_IsTheWholeAbsenceTheSettlementIsEverChargedFor(int population, GrowthStage stage)
		{
			Assert.AreEqual(KingdomRules.UpkeepDrams(population, stage) * KingdomRules.MaxUpkeepDaysCharged,
				KingdomUpgradeRules.ReserveDrams(population, stage));
		}

		[TestCase(100, 10, 30, true)]
		[TestCase(40, 10, 30, true)]
		[TestCase(39, 10, 30, false)]
		[TestCase(0, 0, 0, true)]
		[TestCase(10, 11, 0, false)]
		public void CanAfford_LeavesTheReserveStanding(int stored, int cost, int reserve, bool expected)
		{
			Assert.AreEqual(expected, KingdomUpgradeRules.CanAfford(stored, cost, reserve));
		}

		[TestCase(39, 10, 30, 1)]
		[TestCase(30, 10, 30, 10)]
		[TestCase(0, 12, 30, 42)]
		[TestCase(40, 10, 30, 0)]
		[TestCase(1000, 10, 30, 0)]
		public void Shortfall_IsWhatTheStoresAreShort(int stored, int cost, int reserve, int expected)
		{
			Assert.AreEqual(expected, KingdomUpgradeRules.Shortfall(stored, cost, reserve));
		}

		[TestCase(39, 10, 30)]
		[TestCase(40, 10, 30)]
		[TestCase(41, 10, 30)]
		[TestCase(0, 0, 0)]
		[TestCase(5, 100, 0)]
		public void Shortfall_IsZeroExactlyWhenItIsAffordable(int stored, int cost, int reserve)
		{
			// The sentence the founder reads and the decision the settlement makes must agree; a
			// non-zero shortfall on an affordable improvement is a lie in a message box.
			Assert.AreEqual(KingdomUpgradeRules.CanAfford(stored, cost, reserve),
				KingdomUpgradeRules.Shortfall(stored, cost, reserve) == 0);
		}

		// --- ContentsWouldFit: nothing the founder owns is ever put at risk --------------------

		[Test]
		public void ContentsWouldFit_EmptyPredecessorAlwaysFits()
		{
			Assert.IsTrue(KingdomUpgradeRules.ContentsWouldFit(0, 0, 0, SuccessorHoldsItems: false));
			Assert.IsTrue(KingdomUpgradeRules.ContentsWouldFit(0, KingdomUpgradeRules.UnknownCapacity, 0, SuccessorHoldsItems: false));
		}

		[TestCase(1)]
		[TestCase(20)]
		public void ContentsWouldFit_RefusesWhenWhatIsStoredHasNowhereToGo(int heldItems)
		{
			// A dedicated larder full of the founder's food improving into something with no
			// inventory is the whole reason this check exists.
			Assert.IsFalse(KingdomUpgradeRules.ContentsWouldFit(0, KingdomUpgradeRules.UnknownCapacity, heldItems, SuccessorHoldsItems: false));
			Assert.IsTrue(KingdomUpgradeRules.ContentsWouldFit(0, KingdomUpgradeRules.UnknownCapacity, heldItems, SuccessorHoldsItems: true));
		}

		[TestCase(64, 256, true)]
		[TestCase(64, 64, true)]
		[TestCase(65, 64, false)]
		[TestCase(256, 1, false)]
		public void ContentsWouldFit_ReadsARealDeclaredCapacityAndRefusesASmallerOne(int storedLiquid, int capacity, bool expected)
		{
			Assert.AreEqual(expected, KingdomUpgradeRules.ContentsWouldFit(storedLiquid, capacity, 0, SuccessorHoldsItems: true));
		}

		[TestCase(1)]
		[TestCase(1000)]
		public void ContentsWouldFit_AnUndeclaredCapacityIsNotEvidenceOfAProblem(int storedLiquid)
		{
			// Qud's own open pools carry a negative MaxVolume for "unbounded", and a blueprint
			// that declares no LiquidVolume at all reports the same sentinel. Neither is a reason
			// to refuse; a mutation comparing the sentinel numerically refuses both.
			Assert.IsTrue(KingdomUpgradeRules.ContentsWouldFit(storedLiquid, KingdomUpgradeRules.UnknownCapacity, 0, SuccessorHoldsItems: true));
		}

		// --- Assess: every gate fires, and in the order the founder is owed --------------------

		/// <summary>Everything ready. Each Assess test below spoils exactly one thing about it, so
		/// a verdict that stops firing is a failure and nothing else has to change.</summary>
		private static KingdomUpgradeRules.UpgradeVerdict AssessReady(
			bool HasSuccessor = true, bool SuccessorKnown = true, bool StyleAllowed = true, bool OurWork = true,
			bool AlreadyWorking = false, bool HeldOnThisGround = false, bool HeldByFounder = false,
			GrowthStage Stage = GrowthStage.Village, GrowthStage StageNeeded = GrowthStage.Steading,
			int FreeHands = 4, int CrewNeeded = 1, bool ContentsFit = true,
			int StoredWater = 100, int Cost = 10, int Reserve = 30, bool OtherWorkUnderway = false)
		{
			return KingdomUpgradeRules.Assess(HasSuccessor, SuccessorKnown, StyleAllowed, OurWork, AlreadyWorking,
				HeldOnThisGround, HeldByFounder, Stage, StageNeeded, FreeHands, CrewNeeded, ContentsFit,
				StoredWater, Cost, Reserve, OtherWorkUnderway);
		}

		[Test]
		public void Assess_EverythingInOrderIsReady()
		{
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.Ready, AssessReady());
		}

		[Test]
		public void Assess_NoSuccessorIsTheStateEveryDesignShipsIn()
		{
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.NoSuccessor, AssessReady(HasSuccessor: false));
		}

		[Test]
		public void Assess_AnUnresolvableSuccessorIsReportedRatherThanIgnored()
		{
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.SuccessorUnknown, AssessReady(SuccessorKnown: false));
			Assert.IsTrue(KingdomUpgradeRules.IsBlocked(KingdomUpgradeRules.UpgradeVerdict.SuccessorUnknown));
		}

		[Test]
		public void Assess_TheSettlementNeverRebuildsWhatThePlayerMade()
		{
			// The protection law. Adopted work outranks every settlement condition below it, so a
			// founder's own building is refused for being theirs, never for wanting water.
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.NotOurWork, AssessReady(OurWork: false));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.NotOurWork, AssessReady(OurWork: false, StyleAllowed: false, AlreadyWorking: true,
				HeldOnThisGround: true, HeldByFounder: true, Stage: GrowthStage.Camp, FreeHands: 0, ContentsFit: false, StoredWater: 0, OtherWorkUnderway: true));
		}

		[Test]
		public void Assess_AStyleThisCityNeverBuildsIsSilent()
		{
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.StyleForbids, AssessReady(StyleAllowed: false));
			Assert.IsFalse(KingdomUpgradeRules.IsBlocked(KingdomUpgradeRules.UpgradeVerdict.StyleForbids));
		}

		[Test]
		public void Assess_WorkAlreadyUnderWayIsNotAStall()
		{
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.AlreadyWorking, AssessReady(AlreadyWorking: true));
			Assert.IsFalse(KingdomUpgradeRules.IsBlocked(KingdomUpgradeRules.UpgradeVerdict.AlreadyWorking));
		}

		[Test]
		public void Assess_AFounderWhoHeldAWorkIsNeverLecturedAboutItsWater()
		{
			// Intent before arithmetic. Both holds outrank stage, hands, spill, and water, so the
			// settlement answers "you told me to leave it" rather than "it wants 12 more drams".
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.HeldOnThisGround, AssessReady(HeldOnThisGround: true,
				Stage: GrowthStage.Camp, FreeHands: 0, ContentsFit: false, StoredWater: 0, OtherWorkUnderway: true));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.HeldByFounder, AssessReady(HeldByFounder: true,
				Stage: GrowthStage.Camp, FreeHands: 0, ContentsFit: false, StoredWater: 0, OtherWorkUnderway: true));
		}

		[Test]
		public void Assess_GroundHeldOutranksTheOneWorkHeld()
		{
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.HeldOnThisGround, AssessReady(HeldOnThisGround: true, HeldByFounder: true));
		}

		[TestCase(GrowthStage.Camp, GrowthStage.Steading, KingdomUpgradeRules.UpgradeVerdict.StageTooLow)]
		[TestCase(GrowthStage.Steading, GrowthStage.Steading, KingdomUpgradeRules.UpgradeVerdict.Ready)]
		[TestCase(GrowthStage.City, GrowthStage.Steading, KingdomUpgradeRules.UpgradeVerdict.Ready)]
		[TestCase(GrowthStage.Town, GrowthStage.City, KingdomUpgradeRules.UpgradeVerdict.StageTooLow)]
		public void Assess_StageIsAFloorNotAnEquality(GrowthStage stage, GrowthStage needed, KingdomUpgradeRules.UpgradeVerdict expected)
		{
			Assert.AreEqual(expected, AssessReady(Stage: stage, StageNeeded: needed));
		}

		[TestCase(0, 1, KingdomUpgradeRules.UpgradeVerdict.NotEnoughHands)]
		[TestCase(1, 2, KingdomUpgradeRules.UpgradeVerdict.NotEnoughHands)]
		[TestCase(2, 2, KingdomUpgradeRules.UpgradeVerdict.Ready)]
		[TestCase(9, 2, KingdomUpgradeRules.UpgradeVerdict.Ready)]
		public void Assess_HandsAreSpentOnce(int freeHands, int crewNeeded, KingdomUpgradeRules.UpgradeVerdict expected)
		{
			Assert.AreEqual(expected, AssessReady(FreeHands: freeHands, CrewNeeded: crewNeeded));
		}

		[Test]
		public void Assess_WouldSpillIsCheckedBeforeTheWaterIsCounted()
		{
			// The never-lose-anything rule in its load-bearing form: an improvement whose contents
			// could not move is refused for THAT, so the founder is never charged for a build the
			// settlement was going to abandon anyway.
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.WouldSpill, AssessReady(ContentsFit: false));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.WouldSpill, AssessReady(ContentsFit: false, StoredWater: 0));
		}

		[Test]
		public void Assess_TheStoresAreNeverDrawnBelowWhatTheSettlementLivesOn()
		{
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.NotEnoughWater, AssessReady(StoredWater: 39, Cost: 10, Reserve: 30));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.Ready, AssessReady(StoredWater: 40, Cost: 10, Reserve: 30));
		}

		[Test]
		public void Assess_ThePacingGateIsLastSoAReadyWorkReportsTheHonestReason()
		{
			// "The settlement is already busy" must never be reported by a work that also wants
			// water or hands - those are the conditions the founder can act on.
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.WorksElsewhere, AssessReady(OtherWorkUnderway: true));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.NotEnoughWater, AssessReady(OtherWorkUnderway: true, StoredWater: 0));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.NotEnoughHands, AssessReady(OtherWorkUnderway: true, FreeHands: 0));
		}

		// --- IsReady / IsBlocked / ReasonLine: STANDARDS 7b, stated as an invariant ------------

		[Test]
		public void IsReady_IsTrueForExactlyOneVerdict()
		{
			int ready = 0;
			foreach (KingdomUpgradeRules.UpgradeVerdict verdict in Verdicts())
			{
				if (KingdomUpgradeRules.IsReady(verdict))
				{
					ready++;
					Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.Ready, verdict);
				}
			}
			Assert.AreEqual(1, ready);
		}

		[Test]
		public void EveryVerdictIsEitherSilentForAReasonOrCarriesASentence()
		{
			// The rule this whole file exists to keep: a work that could grow and is not growing
			// owes the founder one line. A new verdict added without a ReasonLine branch fails
			// here, which is the only place it would ever be caught before a player found it.
			foreach (KingdomUpgradeRules.UpgradeVerdict verdict in Verdicts())
			{
				string line = KingdomUpgradeRules.ReasonLine(verdict, "cask rack", "great cistern", GrowthStage.Steading, 2, 7);
				if (KingdomUpgradeRules.IsBlocked(verdict))
				{
					Assert.IsNotNull(line, verdict + " is blocked and says nothing");
					Assert.AreNotEqual("", line.Trim(), verdict + " is blocked and says nothing");
				}
				else
				{
					Assert.IsNull(line, verdict + " is not a stall and should say nothing");
				}
			}
		}

		[TestCase(KingdomUpgradeRules.UpgradeVerdict.Ready)]
		[TestCase(KingdomUpgradeRules.UpgradeVerdict.NoSuccessor)]
		[TestCase(KingdomUpgradeRules.UpgradeVerdict.StyleForbids)]
		[TestCase(KingdomUpgradeRules.UpgradeVerdict.NotOurWork)]
		[TestCase(KingdomUpgradeRules.UpgradeVerdict.AlreadyWorking)]
		public void IsBlocked_IsFalseForExactlyTheFiveThatHaveNotStalled(KingdomUpgradeRules.UpgradeVerdict verdict)
		{
			Assert.IsFalse(KingdomUpgradeRules.IsBlocked(verdict));
		}

		[Test]
		public void IsBlocked_IsTrueForEveryOtherVerdict()
		{
			int blocked = 0;
			foreach (KingdomUpgradeRules.UpgradeVerdict verdict in Verdicts())
			{
				if (KingdomUpgradeRules.IsBlocked(verdict))
				{
					blocked++;
				}
			}
			Assert.AreEqual(Verdicts().Count - 5, blocked);
		}

		[Test]
		public void ReasonLine_NamesWhatIsStandingThereAndWhatItWouldBecome()
		{
			string line = KingdomUpgradeRules.ReasonLine(KingdomUpgradeRules.UpgradeVerdict.StageTooLow, "cask rack", "great cistern", GrowthStage.Town, 1, 0);
			StringAssert.Contains("cask rack", line);
			StringAssert.Contains("great cistern", line);
			StringAssert.Contains("town", line);
		}

		[TestCase(1, "dram")]
		[TestCase(2, "drams")]
		[TestCase(12, "drams")]
		public void ReasonLine_CountsWaterInTheRightNumber(int shortfall, string expected)
		{
			string line = KingdomUpgradeRules.ReasonLine(KingdomUpgradeRules.UpgradeVerdict.NotEnoughWater, "cask rack", "great cistern", GrowthStage.Camp, 1, shortfall);
			StringAssert.Contains(shortfall + " more " + expected, line);
		}

		[TestCase(1, "no one is")]
		[TestCase(2, "2 settlers are")]
		[TestCase(5, "5 settlers are")]
		public void ReasonLine_CountsHandsInTheRightNumber(int crewNeeded, string expected)
		{
			string line = KingdomUpgradeRules.ReasonLine(KingdomUpgradeRules.UpgradeVerdict.NotEnoughHands, "cask rack", "great cistern", GrowthStage.Camp, crewNeeded, 0);
			StringAssert.Contains(expected, line);
		}

		[Test]
		public void ReasonLine_SurvivesAMissingNameRatherThanReadingAsABlank()
		{
			string line = KingdomUpgradeRules.ReasonLine(KingdomUpgradeRules.UpgradeVerdict.SuccessorUnknown, null, null, GrowthStage.Camp, 1, 0);
			Assert.IsNotNull(line);
			StringAssert.Contains("work", line);
			string spill = KingdomUpgradeRules.ReasonLine(KingdomUpgradeRules.UpgradeVerdict.WouldSpill, "", "", GrowthStage.Camp, 1, 0);
			StringAssert.Contains("something better", spill);
		}

		// --- The lines the founder actually reads ---------------------------------------------

		[Test]
		public void BegunLine_NamesBothWorksAndThePriceItIsCharging()
		{
			string line = KingdomUpgradeRules.BegunLine("cask rack", "great cistern", 12);
			StringAssert.Contains("cask rack", line);
			StringAssert.Contains("a great cistern", line);
			StringAssert.Contains("12 drams", line);
		}

		[Test]
		public void BegunLine_CountsOneDramSingular()
		{
			StringAssert.Contains("1 dram from", KingdomUpgradeRules.BegunLine("cask rack", "great cistern", 1));
		}

		[Test]
		public void FirstNoticeLine_SaysWhatTheSettlementWillDoAndWhereToStopIt()
		{
			string line = KingdomUpgradeRules.FirstNoticeLine("Sook's Rest");
			StringAssert.Contains("Sook's Rest", line);
			StringAssert.Contains("Charter", line);
		}

		[TestCase(null)]
		[TestCase("")]
		public void FirstNoticeLine_HasAName_EvenWhenTheSettlementDoesNot(string seat)
		{
			StringAssert.Contains("the settlement", KingdomUpgradeRules.FirstNoticeLine(seat));
		}

		[TestCase("great cistern", "a great cistern")]
		[TestCase("aqueduct", "an aqueduct")]
		[TestCase("Aqueduct", "an Aqueduct")]
		[TestCase("engine", "an engine")]
		[TestCase("Ironworks", "an Ironworks")]
		[TestCase("oven", "an oven")]
		[TestCase("urn", "an urn")]
		[TestCase("stone rampart", "a stone rampart")]
		[TestCase("", "something")]
		[TestCase(null, "something")]
		public void Article_ReadsLikeTheRestOfTheModsProse(string name, string expected)
		{
			Assert.AreEqual(expected, KingdomUpgradeRules.Article(name));
		}

		[TestCase(GrowthStage.Camp, "camp")]
		[TestCase(GrowthStage.Steading, "steading")]
		[TestCase(GrowthStage.Village, "village")]
		[TestCase(GrowthStage.Town, "town")]
		[TestCase(GrowthStage.City, "city")]
		public void StageWord_NamesEveryStageTheSettlementCanBe(GrowthStage stage, string expected)
		{
			Assert.AreEqual(expected, KingdomUpgradeRules.StageWord(stage));
		}

		// --- TryParseUpgradeAttributes: an entry that declares nothing keeps working -----------

		[Test]
		public void TryParseUpgradeAttributes_AllAbsentIsAnUndefinedChainAndNoError()
		{
			// Every design that shipped before this system existed is exactly here, which is why
			// none of them changed behaviour.
			Assert.IsTrue(KingdomUpgradeRules.TryParseUpgradeAttributes("caskrack", null, null, null, null, null, out var chain, out var error));
			Assert.IsNull(error);
			Assert.IsNotNull(chain);
			Assert.IsFalse(chain.Defined);
			Assert.IsNull(chain.SuccessorKey);
		}

		[Test]
		public void TryParseUpgradeAttributes_ReadsAWholeChain()
		{
			Assert.IsTrue(KingdomUpgradeRules.TryParseUpgradeAttributes("caskrack", "cistern", "9", "1500", "2", "Town", out var chain, out var error));
			Assert.IsNull(error);
			Assert.IsTrue(chain.Defined);
			Assert.AreEqual("cistern", chain.SuccessorKey);
			Assert.AreEqual(9, chain.CostDramsOverride);
			Assert.AreEqual(1500L, chain.BuildTicksOverride);
			Assert.AreEqual(2, chain.CrewOverride);
			Assert.IsTrue(chain.HasMinStageOverride);
			Assert.AreEqual(GrowthStage.Town, chain.MinStageOverride);
		}

		[Test]
		public void TryParseUpgradeAttributes_ASuccessorWithNoOverridesLeavesEverySentinelUnset()
		{
			Assert.IsTrue(KingdomUpgradeRules.TryParseUpgradeAttributes("caskrack", "cistern", null, null, null, null, out var chain, out _));
			Assert.AreEqual(KingdomUpgradeRules.Unset, chain.CostDramsOverride);
			Assert.AreEqual(KingdomUpgradeRules.UnsetTicks, chain.BuildTicksOverride);
			Assert.AreEqual(KingdomUpgradeRules.Unset, chain.CrewOverride);
			Assert.IsFalse(chain.HasMinStageOverride);
		}

		[TestCase("9", null, null, null)]
		[TestCase(null, "1500", null, null)]
		[TestCase(null, null, "2", null)]
		[TestCase(null, null, null, "Town")]
		public void TryParseUpgradeAttributes_RefusesToGuessASuccessor(string cost, string ticks, string crew, string stage)
		{
			// Guessing here would improve a building into something its author never wrote, so an
			// entry that prices an improvement without naming one disables itself instead.
			Assert.IsFalse(KingdomUpgradeRules.TryParseUpgradeAttributes("caskrack", null, cost, ticks, crew, stage, out var chain, out var error));
			Assert.IsNull(chain);
			Assert.IsNotNull(error);
		}

		[Test]
		public void TryParseUpgradeAttributes_RefusesADesignThatUpgradesIntoItself()
		{
			Assert.IsFalse(KingdomUpgradeRules.TryParseUpgradeAttributes("caskrack", "caskrack", null, null, null, null, out var chain, out var error));
			Assert.IsNull(chain);
			StringAssert.Contains("itself", error);
		}

		[TestCase("nine")]
		[TestCase("-1")]
		[TestCase("")]
		public void TryParseUpgradeAttributes_RefusesABadCost(string cost)
		{
			// The empty string is the one that has to be thought about: it is not "absent", it is
			// an author who wrote UpgradeCost="" and meant something.
			bool ok = KingdomUpgradeRules.TryParseUpgradeAttributes("caskrack", "cistern", cost, null, null, null, out var chain, out var error);
			if (cost == "")
			{
				Assert.IsTrue(ok);
				Assert.AreEqual(KingdomUpgradeRules.Unset, chain.CostDramsOverride);
				return;
			}
			Assert.IsFalse(ok);
			Assert.IsNull(chain);
			StringAssert.Contains("UpgradeCost", error);
		}

		[TestCase("0")]
		[TestCase("-1")]
		[TestCase("soon")]
		public void TryParseUpgradeAttributes_RefusesABuildTimeOfNothing(string ticks)
		{
			Assert.IsFalse(KingdomUpgradeRules.TryParseUpgradeAttributes("caskrack", "cistern", null, ticks, null, null, out var chain, out var error));
			Assert.IsNull(chain);
			StringAssert.Contains("UpgradeTicks", error);
		}

		[TestCase("-1")]
		[TestCase("some")]
		public void TryParseUpgradeAttributes_RefusesABadCrew(string crew)
		{
			Assert.IsFalse(KingdomUpgradeRules.TryParseUpgradeAttributes("caskrack", "cistern", null, null, crew, null, out var chain, out var error));
			Assert.IsNull(chain);
			StringAssert.Contains("UpgradeCrew", error);
		}

		[Test]
		public void TryParseUpgradeAttributes_AcceptsAFreeImprovementAndACrewOfNone()
		{
			// Both are legitimate authorings; the floors in CostDrams and CrewRequired are what
			// decide what they actually mean, and they are tested where they live.
			Assert.IsTrue(KingdomUpgradeRules.TryParseUpgradeAttributes("caskrack", "cistern", "0", null, "0", null, out var chain, out _));
			Assert.AreEqual(0, chain.CostDramsOverride);
			Assert.AreEqual(0, chain.CrewOverride);
		}

		[TestCase("Nowhere")]
		[TestCase("7")]
		public void TryParseUpgradeAttributes_RefusesAStageThatIsNotOne(string stage)
		{
			Assert.IsFalse(KingdomUpgradeRules.TryParseUpgradeAttributes("caskrack", "cistern", null, null, null, stage, out var chain, out var error));
			Assert.IsNull(chain);
			StringAssert.Contains("UpgradeMinStage", error);
		}

		[TestCase("town")]
		[TestCase("TOWN")]
		[TestCase("Town")]
		public void TryParseUpgradeAttributes_ReadsAStageInAnyCase(string stage)
		{
			Assert.IsTrue(KingdomUpgradeRules.TryParseUpgradeAttributes("caskrack", "cistern", null, null, null, stage, out var chain, out _));
			Assert.AreEqual(GrowthStage.Town, chain.MinStageOverride);
		}

		[Test]
		public void TryParseUpgradeAttributes_NamesTheEntryItRefused()
		{
			KingdomUpgradeRules.TryParseUpgradeAttributes(null, null, "9", null, null, null, out _, out var error);
			StringAssert.Contains("(unnamed)", error);
		}

		// --- ChooseDesignIndex: which design a work with no stamped key counts as --------------

		[Test]
		public void ChooseDesignIndex_NoCandidateIsNoDesign()
		{
			Assert.AreEqual(-1, KingdomUpgradeRules.ChooseDesignIndex(null));
			Assert.AreEqual(-1, KingdomUpgradeRules.ChooseDesignIndex(new bool[0]));
		}

		[TestCase(true)]
		[TestCase(false)]
		public void ChooseDesignIndex_OneCandidateIsAlwaysTheAnswer(bool chained)
		{
			// The key is wanted for prose and for the cost arithmetic whether or not the design
			// can grow, so an unchained lone candidate must not read as "no design".
			Assert.AreEqual(0, KingdomUpgradeRules.ChooseDesignIndex(new bool[1] { chained }));
		}

		[Test]
		public void ChooseDesignIndex_PrefersTheDesignThatCanActuallyAnswerTheQuestion()
		{
			Assert.AreEqual(2, KingdomUpgradeRules.ChooseDesignIndex(new bool[3] { false, false, true }));
			Assert.AreEqual(1, KingdomUpgradeRules.ChooseDesignIndex(new bool[3] { false, true, false }));
		}

		[Test]
		public void ChooseDesignIndex_ResolvesTwoChainsByLoadOrder()
		{
			Assert.AreEqual(0, KingdomUpgradeRules.ChooseDesignIndex(new bool[3] { true, true, false }));
			Assert.AreEqual(1, KingdomUpgradeRules.ChooseDesignIndex(new bool[3] { false, true, true }));
		}

		[Test]
		public void ChooseDesignIndex_FallsBackToTheFirstWhenNothingCanGrow()
		{
			Assert.AreEqual(0, KingdomUpgradeRules.ChooseDesignIndex(new bool[3] { false, false, false }));
		}

		private static List<KingdomUpgradeRules.UpgradeVerdict> Verdicts()
		{
			List<KingdomUpgradeRules.UpgradeVerdict> all = new List<KingdomUpgradeRules.UpgradeVerdict>();
			foreach (object value in Enum.GetValues(typeof(KingdomUpgradeRules.UpgradeVerdict)))
			{
				all.Add((KingdomUpgradeRules.UpgradeVerdict)value);
			}
			return all;
		}
	}
}
#endif
