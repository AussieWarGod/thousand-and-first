#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomUpgradeRulesTests
	{
		private static string DeclaredFieldNames(Type Type)
		{
			System.Reflection.FieldInfo[] fields = Type.GetFields(
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
				| System.Reflection.BindingFlags.DeclaredOnly);
			Array.Sort(fields, delegate(System.Reflection.FieldInfo A, System.Reflection.FieldInfo B)
			{
				return A.MetadataToken.CompareTo(B.MetadataToken);
			});
			string[] names = new string[fields.Length];
			for (int i = 0; i < fields.Length; i++) names[i] = fields[i].Name;
			return string.Join("|", names);
		}

		[Test]
		public void DecomposedNestedTypes_PreserveIdentityValuesFieldsAndDefaults()
		{
			Type verdict = typeof(KingdomUpgradeRules.UpgradeVerdict);
			Assert.AreEqual("ThousandAndFirst.KingdomUpgradeRules+UpgradeVerdict", verdict.FullName);
			Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(verdict));
			Assert.AreEqual("Ready|NoSuccessor|SuccessorUnknown|StyleForbids|NotOurWork|AlreadyWorking|HeldOnThisGround|HeldByFounder|StageTooLow|NotEnoughHands|WouldSpill|NotEnoughWater|WorksElsewhere|NoGroundToGrow|CraftNotMet|NotEnoughMaterial|NoTolerableLodging|HeldOffer",
				string.Join("|", Enum.GetNames(verdict)));
			Array verdicts = Enum.GetValues(verdict);
			for (int i = 0; i < verdicts.Length; i++)
			{
				Assert.AreEqual(i, Convert.ToInt32(verdicts.GetValue(i)), "verdict " + i);
			}

			Type lodging = typeof(KingdomUpgradeRules.LodgingStandard);
			Assert.AreEqual("ThousandAndFirst.KingdomUpgradeRules+LodgingStandard", lodging.FullName);
			Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(lodging));
			Assert.AreEqual("Settler|Notable|Discerning", string.Join("|", Enum.GetNames(lodging)));
			Assert.AreEqual(0, (int)KingdomUpgradeRules.LodgingStandard.Settler);
			Assert.AreEqual(1, (int)KingdomUpgradeRules.LodgingStandard.Notable);
			Assert.AreEqual(2, (int)KingdomUpgradeRules.LodgingStandard.Discerning);

			Type chainType = typeof(KingdomUpgradeRules.UpgradeChain);
			Assert.AreEqual("ThousandAndFirst.KingdomUpgradeRules+UpgradeChain", chainType.FullName);
			Assert.AreEqual("SuccessorKey|CostDramsOverride|BuildTicksOverride|CrewOverride|HasMinStageOverride|MinStageOverride",
				DeclaredFieldNames(chainType));
			KingdomUpgradeRules.UpgradeChain chain = new KingdomUpgradeRules.UpgradeChain();
			Assert.AreEqual(KingdomUpgradeRules.Unset, chain.CostDramsOverride);
			Assert.AreEqual(KingdomUpgradeRules.UnsetTicks, chain.BuildTicksOverride);
			Assert.AreEqual(KingdomUpgradeRules.Unset, chain.CrewOverride);
			Assert.IsFalse(chain.HasMinStageOverride);
			Assert.IsFalse(chain.Defined);

			Type demandType = typeof(KingdomUpgradeRules.AbsorptionDemand);
			Assert.AreEqual("ThousandAndFirst.KingdomUpgradeRules+AbsorptionDemand", demandType.FullName);
			Assert.AreEqual("IsHousing|Residents|SpareLodging|OfferedShelter|CurrentShelter|LuxuryCarried|SupportPerDay|BuildTicks|MaterialsInHand|CraftMet|QuartersRefused",
				DeclaredFieldNames(demandType));
			Assert.IsTrue(KingdomUpgradeRules.AbsorptionDemand.None.MaterialsInHand);
			Assert.IsTrue(KingdomUpgradeRules.AbsorptionDemand.None.CraftMet);
			Assert.IsFalse(KingdomUpgradeRules.AbsorptionDemand.None.QuartersRefused);
		}

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
		public void ReserveDrams_IsTheNamedReserveDepthAtThisSettlementsOwnRate(int population, GrowthStage stage)
		{
			// It used to be described as "the whole absence the settlement is ever charged for".
			// There is no such length any more -- absence is charged in full -- so the reserve is
			// what it always physically was: a named cushion, ReserveDays deep, at this
			// settlement's own per-head rate.
			Assert.AreEqual(KingdomRules.UpkeepDrams(population, stage) * KingdomRules.ReserveDays,
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

		// =====================================================================================
		// The absorption law (brief, Addendum 3): auto-improve when the city can absorb the
		// disruption, OFFER when it cannot, and never on a timer.
		// =====================================================================================

		// --- Tolerance by standard ------------------------------------------------------------

		[TestCase(0, KingdomUpgradeRules.LodgingStandard.Settler)]
		[TestCase(KingdomUpgradeRules.NotableLuxury - 1, KingdomUpgradeRules.LodgingStandard.Settler)]
		[TestCase(KingdomUpgradeRules.NotableLuxury, KingdomUpgradeRules.LodgingStandard.Notable)]
		[TestCase(KingdomUpgradeRules.DiscerningLuxury - 1, KingdomUpgradeRules.LodgingStandard.Notable)]
		[TestCase(KingdomUpgradeRules.DiscerningLuxury, KingdomUpgradeRules.LodgingStandard.Discerning)]
		[TestCase(KingdomUpgradeRules.DiscerningLuxury + 5, KingdomUpgradeRules.LodgingStandard.Discerning)]
		public void StandardFor_ReadsTheRefinementTheDesignLiftsBy(int luxury, KingdomUpgradeRules.LodgingStandard expected)
		{
			// Both thresholds are pinned from below and above, so widening or narrowing either one
			// fails here rather than quietly rehousing every notable in a tent.
			Assert.AreEqual(expected, KingdomUpgradeRules.StandardFor(luxury));
		}

		[Test]
		public void ShelterRequired_IsABunkForASettlerAndARoomForANotable()
		{
			Assert.AreEqual(KingdomUpgradeRules.BunkShelter,
				KingdomUpgradeRules.ShelterRequired(KingdomUpgradeRules.LodgingStandard.Settler, CurrentShelter: 9));
			Assert.AreEqual(KingdomUpgradeRules.RoomShelter,
				KingdomUpgradeRules.ShelterRequired(KingdomUpgradeRules.LodgingStandard.Notable, CurrentShelter: 9));
			Assert.Less(KingdomUpgradeRules.BunkShelter, KingdomUpgradeRules.RoomShelter);
		}

		[Test]
		public void ShelterRequired_MeasuresADiscerningNotableAgainstTheirOwnRoofAndNeverBelowARoom()
		{
			Assert.AreEqual(4, KingdomUpgradeRules.ShelterRequired(KingdomUpgradeRules.LodgingStandard.Discerning, CurrentShelter: 4));
			Assert.AreEqual(KingdomUpgradeRules.RoomShelter,
				KingdomUpgradeRules.ShelterRequired(KingdomUpgradeRules.LodgingStandard.Discerning, CurrentShelter: 0));
		}

		[Test]
		public void CanDisplace_AnOrdinarySettlerToleratesABunk()
		{
			Assert.IsTrue(KingdomUpgradeRules.CanDisplace(Residents: 2, SpareLodging: 2,
				OfferedShelter: KingdomUpgradeRules.BunkShelter,
				Standard: KingdomUpgradeRules.StandardFor(0), CurrentShelter: KingdomUpgradeRules.RoomShelter));
		}

		[Test]
		public void CanDisplace_ANotableDoesNotTolerateATent()
		{
			// The author's own example, and the whole reason the standard exists: identical ground,
			// identical spare lodging, and the answer turns only on who is being moved.
			KingdomUpgradeRules.LodgingStandard notable = KingdomUpgradeRules.StandardFor(KingdomUpgradeRules.NotableLuxury);
			Assert.IsFalse(KingdomUpgradeRules.CanDisplace(Residents: 1, SpareLodging: 4,
				OfferedShelter: KingdomUpgradeRules.BunkShelter, Standard: notable,
				CurrentShelter: KingdomUpgradeRules.RoomShelter));
			Assert.IsTrue(KingdomUpgradeRules.CanDisplace(Residents: 1, SpareLodging: 4,
				OfferedShelter: KingdomUpgradeRules.RoomShelter, Standard: notable,
				CurrentShelter: KingdomUpgradeRules.RoomShelter));
		}

		[Test]
		public void CanDisplace_ADiscerningNotableWillNotBeMovedDownFromTheirOwnRoof()
		{
			KingdomUpgradeRules.LodgingStandard discerning = KingdomUpgradeRules.StandardFor(KingdomUpgradeRules.DiscerningLuxury);
			Assert.IsFalse(KingdomUpgradeRules.CanDisplace(Residents: 1, SpareLodging: 9,
				OfferedShelter: 3, Standard: discerning, CurrentShelter: 4));
			Assert.IsTrue(KingdomUpgradeRules.CanDisplace(Residents: 1, SpareLodging: 9,
				OfferedShelter: 4, Standard: discerning, CurrentShelter: 4));
		}

		[Test]
		public void CanDisplace_AnEmptyHouseDisplacesNobody()
		{
			// Nothing standing empty anywhere, no shelter offered at all, and it is still
			// improvable: there is nobody to put anywhere.
			Assert.IsTrue(KingdomUpgradeRules.CanDisplace(Residents: 0, SpareLodging: 0, OfferedShelter: 0,
				Standard: KingdomUpgradeRules.LodgingStandard.Discerning, CurrentShelter: 9));
		}

		[TestCase(3, 3, true)]
		[TestCase(3, 2, false)]
		[TestCase(3, 4, true)]
		public void CanDisplace_NeedsARoofPerResidentAndCountsExactlyAtTheBoundary(int residents, int spare, bool expected)
		{
			Assert.AreEqual(expected, KingdomUpgradeRules.CanDisplace(residents, spare,
				OfferedShelter: KingdomUpgradeRules.RoomShelter,
				Standard: KingdomUpgradeRules.LodgingStandard.Settler, CurrentShelter: KingdomUpgradeRules.RoomShelter));
		}

		[Test]
		public void CanDisplace_RoomForThemIsNotEnoughIfItIsBelowTheirStandard()
		{
			// A mutation that drops the shelter half and keeps only the count passes the test
			// above and fails this one.
			Assert.IsFalse(KingdomUpgradeRules.CanDisplace(Residents: 1, SpareLodging: 100,
				OfferedShelter: 0, Standard: KingdomUpgradeRules.LodgingStandard.Settler, CurrentShelter: 0));
		}

		// --- Addendum 4 re-basing: tolerance is also the Needs check against the quarters ------

		private static QolProfile Resident(params string[] Needs)
		{
			return new QolProfile
			{
				Needs = Needs,
				Prefers = KingdomQolRules.NoTags,
				Refuses = KingdomQolRules.NoTags,
				EatsFood = true,
				DrinksWater = true
			};
		}

		[Test]
		public void QuartersRefused_NobodyMeasuredRefusesNothing()
		{
			string tag;
			Assert.IsFalse(KingdomUpgradeRules.QuartersRefused(null, null, out tag));
			Assert.AreEqual("", tag);
			Assert.IsFalse(KingdomUpgradeRules.QuartersRefused(new string[0], new List<QolProfile>(), out tag));
		}

		[Test]
		public void QuartersRefused_AResidentWhoAsksNothingTakesQuartersThatOfferNothing()
		{
			// Every settler in an unauthored catalogue, which is why this re-basing changes nothing
			// for a city that has not written a single Provides.
			string tag;
			Assert.IsFalse(KingdomUpgradeRules.QuartersRefused(new string[0],
				new List<QolProfile> { Resident() }, out tag));
		}

		[Test]
		public void QuartersRefused_AResidentWhoseNeedTheQuartersDoNotMeetRefusesAndIsNamed()
		{
			string tag;
			Assert.IsTrue(KingdomUpgradeRules.QuartersRefused(new string[0],
				new List<QolProfile> { Resident(KingdomQolRules.TagCharge) }, out tag));
			Assert.AreEqual(KingdomQolRules.TagCharge, tag, "the founder is owed the tag that would lift it");
		}

		[Test]
		public void QuartersRefused_QuartersThatMeetTheNeedAreAccepted()
		{
			string tag;
			Assert.IsFalse(KingdomUpgradeRules.QuartersRefused(new string[1] { KingdomQolRules.TagCharge },
				new List<QolProfile> { Resident(KingdomQolRules.TagCharge) }, out tag));
			Assert.AreEqual("", tag);
		}

		[Test]
		public void QuartersRefused_OneRefusingResidentAmongManyIsEnough()
		{
			string tag;
			Assert.IsTrue(KingdomUpgradeRules.QuartersRefused(new string[1] { KingdomQolRules.TagCharge },
				new List<QolProfile> { Resident(), Resident(KingdomQolRules.TagCharge), Resident(KingdomQolRules.TagDamp) }, out tag));
			Assert.AreEqual(KingdomQolRules.TagDamp, tag);
		}

		[Test]
		public void Assess_ARefusedQuartersHoldsTheRebuildExactlyAsAMissingRoofDoes()
		{
			// A tent is tolerable lodging for a settler and no lodging whatever for the robot who
			// needs a cradle: the rank ladder passes and the vocabulary still refuses.
			KingdomUpgradeRules.AbsorptionDemand house = LeanedOn(IsHousing: true, Residents: 2, SpareLodging: 4,
				OfferedShelter: KingdomUpgradeRules.RoomShelter, SupportPerDay: 0);
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.Ready, AssessAbsorbing(house));
			house.QuartersRefused = true;
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.NoTolerableLodging, AssessAbsorbing(house));
		}

		[Test]
		public void Assess_TheVocabularyRefusalOnlyEverAppliesToHousing()
		{
			// A workshop's residents are not being moved out of it, so the Needs check has nothing
			// to say about rebuilding one. A mutation dropping the IsHousing guard fails here.
			KingdomUpgradeRules.AbsorptionDemand work = LeanedOn(SupportPerDay: 0);
			work.QuartersRefused = true;
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.Ready, AssessAbsorbing(work));
		}

		[Test]
		public void Assess_NothingMeasuredStillMeansNobodyRefused()
		{
			// The default of an unset struct and of None is "no refusal", so every caller that has
			// not measured behaves exactly as it did before this half of tolerance existed.
			Assert.IsFalse(KingdomUpgradeRules.AbsorptionDemand.None.QuartersRefused);
			Assert.IsFalse(default(KingdomUpgradeRules.AbsorptionDemand).QuartersRefused);
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.Ready, AssessAbsorbing(
				LeanedOn(IsHousing: true, Residents: 2, SpareLodging: 2, OfferedShelter: KingdomUpgradeRules.BunkShelter)));
		}

		[Test]
		public void Assess_TheShelterLadderStillRefusesOnItsOwnWithNobodyRefusingTheQuarters()
		{
			// Both halves stand: the rank ladder is untouched and still decides how GOOD the
			// lodging must be, independently of whether anybody would live in it at all.
			KingdomUpgradeRules.AbsorptionDemand notable = LeanedOn(IsHousing: true, Residents: 1, SpareLodging: 2,
				OfferedShelter: KingdomUpgradeRules.BunkShelter, LuxuryCarried: KingdomUpgradeRules.NotableLuxury, SupportPerDay: 0);
			Assert.IsFalse(notable.QuartersRefused);
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.NoTolerableLodging, AssessAbsorbing(notable));
		}

		// --- Margin arithmetic, at the boundary -----------------------------------------------

		[TestCase(0L, 0)]
		[TestCase(1L, 1)]
		[TestCase(KingdomRules.TicksPerDay, 1)]
		[TestCase(KingdomRules.TicksPerDay + 1L, 2)]
		[TestCase(KingdomRules.TicksPerDay * 3L, 3)]
		public void BuildDays_RoundsUpSoAPartDayStillCostsADaysOutput(long ticks, int expected)
		{
			Assert.AreEqual(expected, KingdomUpgradeRules.BuildDays(ticks));
		}

		[Test]
		public void OutputLost_IsTheSustainedRateForEveryDayOfLabour()
		{
			Assert.AreEqual(10, KingdomUpgradeRules.OutputLost(SupportPerDay: 5, BuildTicks: KingdomRules.TicksPerDay * 2L));
			Assert.AreEqual(15, KingdomUpgradeRules.OutputLost(SupportPerDay: 5, BuildTicks: KingdomRules.TicksPerDay * 2L + 1L));
		}

		[Test]
		public void OutputLost_IsNothingForAWorkTheSettlementDoesNotDrinkFrom()
		{
			// However long the labour, a work that sustains nothing costs nothing to go without.
			Assert.AreEqual(0, KingdomUpgradeRules.OutputLost(SupportPerDay: 0, BuildTicks: KingdomRules.TicksPerDay * 99L));
		}

		[Test]
		public void AbsorptionMargin_IsWhatIsLeftOverTheReserveOnceBothArePaid()
		{
			// 100 stored, 10 to build, 30 that must remain, 20 gone without: 40 spare.
			Assert.AreEqual(40, KingdomUpgradeRules.AbsorptionMargin(StoredWater: 100, Cost: 10, Reserve: 30, OutputLost: 20));
			Assert.AreEqual(-5, KingdomUpgradeRules.AbsorptionMargin(StoredWater: 100, Cost: 10, Reserve: 30, OutputLost: 65));
		}

		[TestCase(60, 0, true)]
		[TestCase(61, -1, false)]
		[TestCase(59, 1, true)]
		public void CoversOutage_CoveringItExactlyIsCoveringIt(int outputLost, int expectedMargin, bool expected)
		{
			// 100 - 10 - 30 = 60 spare. The boundary is the whole point: a mutation to > or to >= 1
			// changes exactly the first of these three rows.
			Assert.AreEqual(expectedMargin, KingdomUpgradeRules.AbsorptionMargin(100, 10, 30, outputLost));
			Assert.AreEqual(expected, KingdomUpgradeRules.CoversOutage(100, 10, 30, outputLost));
		}

		// --- The held offer, and what outranks it ---------------------------------------------

		/// <summary>A working building the city leans on: everything in order, and the stores
		/// cannot go without what it puts out for as long as the work would take.</summary>
		private static KingdomUpgradeRules.AbsorptionDemand LeanedOn(
			bool IsHousing = false, int Residents = 0, int SpareLodging = 0, int OfferedShelter = 0,
			int CurrentShelter = KingdomUpgradeRules.RoomShelter, int LuxuryCarried = 0,
			int SupportPerDay = 20, long BuildTicks = KingdomRules.TicksPerDay * 4L,
			bool MaterialsInHand = true, bool CraftMet = true)
		{
			KingdomUpgradeRules.AbsorptionDemand demand = default(KingdomUpgradeRules.AbsorptionDemand);
			demand.IsHousing = IsHousing;
			demand.Residents = Residents;
			demand.SpareLodging = SpareLodging;
			demand.OfferedShelter = OfferedShelter;
			demand.CurrentShelter = CurrentShelter;
			demand.LuxuryCarried = LuxuryCarried;
			demand.SupportPerDay = SupportPerDay;
			demand.BuildTicks = BuildTicks;
			demand.MaterialsInHand = MaterialsInHand;
			demand.CraftMet = CraftMet;
			return demand;
		}

		private static KingdomUpgradeRules.UpgradeVerdict AssessAbsorbing(
			KingdomUpgradeRules.AbsorptionDemand Demand,
			bool AlreadyWorking = false, bool HeldOnThisGround = false, bool HeldByFounder = false,
			GrowthStage Stage = GrowthStage.Village, GrowthStage StageNeeded = GrowthStage.Steading,
			int FreeHands = 4, int CrewNeeded = 1, bool ContentsFit = true,
			int StoredWater = 100, int Cost = 10, int Reserve = 30, bool OtherWorkUnderway = false)
		{
			return KingdomUpgradeRules.Assess(HasSuccessor: true, SuccessorKnown: true, StyleAllowed: true,
				OurWork: true, AlreadyWorking: AlreadyWorking, HeldOnThisGround: HeldOnThisGround,
				HeldByFounder: HeldByFounder, Stage: Stage, StageNeeded: StageNeeded, FreeHands: FreeHands,
				CrewNeeded: CrewNeeded, ContentsFit: ContentsFit, StoredWater: StoredWater, Cost: Cost,
				Reserve: Reserve, OtherWorkUnderway: OtherWorkUnderway, Absorption: Demand);
		}

		[Test]
		public void Assess_MeasuringNothingIsExactlyTheBehaviourThatShippedBeforeThisLaw()
		{
			// AbsorptionDemand.None grants material and craft, moves nobody, and loses no output,
			// so every caller that has not measured gets Ready where it always did.
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.Ready,
				AssessAbsorbing(KingdomUpgradeRules.AbsorptionDemand.None));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.Ready, AssessReady());
		}

		[Test]
		public void Assess_AWorkingBuildingTheCityLeansOnBecomesAHeldOffer()
		{
			// 20 drams a day for 4 days is 80; 100 stored, 10 spent, 30 reserved leaves 60.
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.HeldOffer, AssessAbsorbing(LeanedOn()));
		}

		[TestCase(60, KingdomUpgradeRules.UpgradeVerdict.Ready)]
		[TestCase(61, KingdomUpgradeRules.UpgradeVerdict.HeldOffer)]
		public void Assess_TheOfferBeginsExactlyOneDramPastWhatTheStoresCanCarry(int outputLost, KingdomUpgradeRules.UpgradeVerdict expected)
		{
			// One dram a day, for as many days as the loss asks for, so the boundary inside Assess
			// is the same boundary CoversOutage draws and neither can drift from the other.
			Assert.AreEqual(expected, AssessAbsorbing(LeanedOn(SupportPerDay: 1, BuildTicks: KingdomRules.TicksPerDay * outputLost)));
		}

		[Test]
		public void Assess_EveryRealRefusalOutranksTheOffer()
		{
			// The offer is checked last on purpose: a founder is never asked to force a work that
			// something else was going to stop anyway. Each of these spoils one thing about a work
			// the city also leans on, and each must report the refusal rather than the offer.
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.WorksElsewhere, AssessAbsorbing(LeanedOn(), OtherWorkUnderway: true));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.NotEnoughWater, AssessAbsorbing(LeanedOn(), StoredWater: 35));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.NotEnoughMaterial, AssessAbsorbing(LeanedOn(MaterialsInHand: false)));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.CraftNotMet, AssessAbsorbing(LeanedOn(CraftMet: false)));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.NotEnoughHands, AssessAbsorbing(LeanedOn(), FreeHands: 0));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.HeldByFounder, AssessAbsorbing(LeanedOn(), HeldByFounder: true));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.StageTooLow, AssessAbsorbing(LeanedOn(), Stage: GrowthStage.Camp));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.WouldSpill, AssessAbsorbing(LeanedOn(), ContentsFit: false));
		}

		[Test]
		public void Assess_CraftAndMaterialGateEverythingIncludingHousing()
		{
			KingdomUpgradeRules.AbsorptionDemand house = LeanedOn(IsHousing: true, Residents: 2, SpareLodging: 4,
				OfferedShelter: KingdomUpgradeRules.RoomShelter, SupportPerDay: 0);
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.Ready, AssessAbsorbing(house));
			house.MaterialsInHand = false;
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.NotEnoughMaterial, AssessAbsorbing(house));
			house = LeanedOn(IsHousing: true, Residents: 2, SpareLodging: 4,
				OfferedShelter: KingdomUpgradeRules.RoomShelter, SupportPerDay: 0, CraftMet: false);
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.CraftNotMet, AssessAbsorbing(house));
		}

		[Test]
		public void Assess_HousingIsJudgedByDisplacementAndNotByTheOutputMargin()
		{
			// The same crushing outage that makes a working building a held offer leaves housing
			// Ready, because a roof's own output is the people under it and displacement is the
			// question the law asks about them. A mutation inverting the IsHousing test fails here
			// and in the pair below.
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.Ready, AssessAbsorbing(
				LeanedOn(IsHousing: true, Residents: 2, SpareLodging: 2, OfferedShelter: KingdomUpgradeRules.BunkShelter)));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.HeldOffer, AssessAbsorbing(LeanedOn(IsHousing: false)));
		}

		[Test]
		public void Assess_HousingNobodyCanBeMovedOutOfIsRefusedByName()
		{
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.NoTolerableLodging, AssessAbsorbing(
				LeanedOn(IsHousing: true, Residents: 2, SpareLodging: 1, OfferedShelter: KingdomUpgradeRules.RoomShelter, SupportPerDay: 0)));
			// And the notable's own standard is what refuses it, not the count.
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.NoTolerableLodging, AssessAbsorbing(
				LeanedOn(IsHousing: true, Residents: 1, SpareLodging: 8, OfferedShelter: KingdomUpgradeRules.BunkShelter,
					LuxuryCarried: KingdomUpgradeRules.NotableLuxury, SupportPerDay: 0)));
		}

		[Test]
		public void Assess_LodgingOutranksThePacingGateAndTheOfferOutranksNothing()
		{
			// Displacement is a refusal and is checked before "the settlement is already busy";
			// the offer is checked after it. That ordering is what makes the founder's one
			// forceable decision the last thing anything can be waiting on.
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.NoTolerableLodging, AssessAbsorbing(
				LeanedOn(IsHousing: true, Residents: 2, SpareLodging: 0, SupportPerDay: 0), OtherWorkUnderway: true));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.WorksElsewhere, AssessAbsorbing(LeanedOn(), OtherWorkUnderway: true));
		}

		[Test]
		public void IsOffer_IsTrueForTheHeldOfferAndNothingElse()
		{
			foreach (KingdomUpgradeRules.UpgradeVerdict verdict in Verdicts())
			{
				Assert.AreEqual(verdict == KingdomUpgradeRules.UpgradeVerdict.HeldOffer,
					KingdomUpgradeRules.IsOffer(verdict), verdict.ToString());
			}
		}

		[Test]
		public void IsReady_IsFalseForAHeldOfferSoTheSettlementNeverActsOnItAlone()
		{
			Assert.IsFalse(KingdomUpgradeRules.IsReady(KingdomUpgradeRules.UpgradeVerdict.HeldOffer));
			Assert.IsTrue(KingdomUpgradeRules.IsBlocked(KingdomUpgradeRules.UpgradeVerdict.HeldOffer));
		}

		// --- Forced, with the dip disclosed first ---------------------------------------------

		[Test]
		public void ReasonLine_TheHeldOfferSaysItIsReadyAndSaysWhoIsLeaningOnIt()
		{
			string line = KingdomUpgradeRules.ReasonLine(KingdomUpgradeRules.UpgradeVerdict.HeldOffer,
				"cask rack", "great cistern", GrowthStage.Steading, 2, 0);
			StringAssert.Contains("ready to improve", line);
			StringAssert.Contains("held", line);
			StringAssert.Contains("the city leans on it", line);
			// And it names where the founder goes to overrule it, which is the whole of 7b here.
			StringAssert.Contains("Charter", line);
		}

		[Test]
		public void ReasonLine_EveryAbsorptionRefusalNamesWhatWouldLiftIt()
		{
			StringAssert.Contains("craft", KingdomUpgradeRules.ReasonLine(KingdomUpgradeRules.UpgradeVerdict.CraftNotMet,
				"cask rack", "great cistern", GrowthStage.Steading, 2, 0));
			StringAssert.Contains("stockpiles", KingdomUpgradeRules.ReasonLine(KingdomUpgradeRules.UpgradeVerdict.NotEnoughMaterial,
				"cask rack", "great cistern", GrowthStage.Steading, 2, 0));
			StringAssert.Contains("sleep", KingdomUpgradeRules.ReasonLine(KingdomUpgradeRules.UpgradeVerdict.NoTolerableLodging,
				"hut", "stone house", GrowthStage.Steading, 2, 0));
		}

		[Test]
		public void DipLine_DisclosesTheRateTheLabourTheWholeLossAndHowFarUnder()
		{
			// 20 drams a day for 4 days is 80 lost; the margin says the stores are 20 short of it.
			string line = KingdomUpgradeRules.DipLine("cask rack", "great cistern",
				SupportPerDay: 20, BuildTicks: KingdomRules.TicksPerDay * 4L, Margin: -20);
			StringAssert.Contains("cask rack", line);
			StringAssert.Contains("great cistern", line);
			StringAssert.Contains("20 drams a day", line);
			StringAssert.Contains("4 days", line);
			StringAssert.Contains("80 drams in all", line);
			StringAssert.Contains("20 drams further into the reserve", line);
		}

		[Test]
		public void DipLine_TheDisclosedShortfallIsExactlyTheMarginAndNotTheLoss()
		{
			// A mutation that discloses the loss where the shortfall belongs passes the test above
			// (80 and 20 both appear) and fails this one, where they cannot be confused.
			string line = KingdomUpgradeRules.DipLine("cask rack", "great cistern",
				SupportPerDay: 10, BuildTicks: KingdomRules.TicksPerDay * 5L, Margin: -3);
			StringAssert.Contains("50 drams in all", line);
			StringAssert.Contains("3 drams further into the reserve", line);
			Assert.IsFalse(line.Contains("50 drams further"), "the disclosure must name the shortfall, not the loss");
		}

		[Test]
		public void ForcedLine_RecordsThatItWasTheFoundersWordAndHowDeepItWent()
		{
			string line = KingdomUpgradeRules.ForcedLine("cask rack", "great cistern", Margin: -12);
			StringAssert.Contains("on your word", line);
			StringAssert.Contains("12 drams into its reserve", line);
		}

		[Test]
		public void DipLine_IsSingularWhereItShouldBe()
		{
			string line = KingdomUpgradeRules.DipLine("cask rack", "great cistern",
				SupportPerDay: 1, BuildTicks: 1L, Margin: -1);
			StringAssert.Contains("1 dram a day", line);
			StringAssert.Contains("1 day", line);
			StringAssert.Contains("1 dram in all", line);
			StringAssert.Contains("1 dram further into the reserve", line);
		}

		// --- Never a timer --------------------------------------------------------------------

		[Test]
		public void NoTriggerPathReadsElapsedTimeAsACause()
		{
			// The author's ruling: time is labour, never maturation. Nothing that decides whether
			// an improvement happens may take a clock, an age, or a "days since". The ONE duration
			// allowed anywhere near the law is the build's own authored labour time, allowlisted
			// here by exact parameter name so a new clock cannot slip in beside it.
			// Matched word by word rather than by substring, because a substring sweep calls
			// "SuccessorKnown" a clock and would have to be loosened until it caught nothing.
			string[] clockWords = new string[12] { "now", "elapsed", "age", "since", "today", "time", "day", "days", "clock", "duration", "wait", "waited" };
			// Two exemptions, both by exact name. LABOUR is the build's own authored time -- real
			// and felt, and the thing the author ruled time IS. A RATE names an amount the
			// settlement sustains per day; it is a quantity, not a reading of the clock, and
			// "DaysStanding" or "DaysSinceRaised" would still fail below.
			string[] labour = new string[3] { "buildticks", "successorticks", "override" };
			string[] rates = new string[1] { "supportperday" };
			string[] deciders = new string[7] { "Assess", "CanDisplace", "CoversOutage", "AbsorptionMargin", "OutputLost", "StandardFor", "ShelterRequired" };
			int checkedParameters = 0;
			foreach (string name in deciders)
			{
				foreach (System.Reflection.MethodInfo method in typeof(KingdomUpgradeRules).GetMethods())
				{
					if (method.Name != name)
					{
						continue;
					}
					foreach (System.Reflection.ParameterInfo parameter in method.GetParameters())
					{
						string lowered = parameter.Name.ToLowerInvariant();
						if (Array.IndexOf(labour, lowered) >= 0 || Array.IndexOf(rates, lowered) >= 0)
						{
							continue;
						}
						checkedParameters++;
						foreach (string word in CamelWords(parameter.Name))
						{
							Assert.IsFalse(Array.IndexOf(clockWords, word) >= 0,
								name + " takes " + parameter.Name + ", which reads like a clock");
						}
					}
				}
			}
			// The sweep has to have actually looked at something: a rename that made every decider
			// unfindable would otherwise pass silently.
			Assert.Greater(checkedParameters, 20, "the sweep found almost no parameters to check");
		}

		/// <summary>A parameter name split into its camel-case words, lowered. "SuccessorKnown"
		/// gives "successor" and "known" -- and never "now".</summary>
		private static List<string> CamelWords(string Name)
		{
			List<string> words = new List<string>();
			if (string.IsNullOrEmpty(Name))
			{
				return words;
			}
			int start = 0;
			for (int i = 1; i <= Name.Length; i++)
			{
				if (i != Name.Length && !char.IsUpper(Name[i]))
				{
					continue;
				}
				words.Add(Name.Substring(start, i - start).ToLowerInvariant());
				start = i;
			}
			return words;
		}

		[Test]
		public void Assess_NeverTakesATickCountAtAll()
		{
			// Stronger than the sweep above for the one method that actually decides: the verdict
			// is reached without any duration in the room, so no amount of waiting can produce one.
			foreach (System.Reflection.MethodInfo method in typeof(KingdomUpgradeRules).GetMethods())
			{
				if (method.Name != "Assess")
				{
					continue;
				}
				foreach (System.Reflection.ParameterInfo parameter in method.GetParameters())
				{
					Assert.IsFalse(parameter.Name.ToLowerInvariant().Contains("tick"),
						"Assess takes " + parameter.Name);
				}
			}
		}

		[Test]
		public void LabourDurationOnlyEverBlocks_ItNeverTriggers()
		{
			// Longer labour can turn Ready into an offer, because the city goes without for longer.
			// It can never turn an offer back into Ready: nothing in this mod improves because time
			// passed. Swept across the whole range rather than sampled, so a non-monotone mutation
			// -- a modulus, a wrap, an "once past N days it goes ahead" -- is caught.
			bool offered = false;
			for (int days = 0; days <= 12; days++)
			{
				KingdomUpgradeRules.UpgradeVerdict verdict = AssessAbsorbing(
					LeanedOn(SupportPerDay: 10, BuildTicks: KingdomRules.TicksPerDay * days));
				if (verdict == KingdomUpgradeRules.UpgradeVerdict.HeldOffer)
				{
					offered = true;
					continue;
				}
				Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.Ready, verdict, days + " days of labour");
				Assert.IsFalse(offered, "labour handed a held offer back as ready at " + days + " days");
			}
			Assert.IsTrue(offered, "no amount of labour ever produced the offer, so the margin is not being read");
		}

		[Test]
		public void ADurationAloneNeverDecidesAnything()
		{
			// A work the settlement does not drink from is Ready however long it takes to raise.
			// The cause is what the city goes without, never how long the waiting is.
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.Ready,
				AssessAbsorbing(LeanedOn(SupportPerDay: 0, BuildTicks: KingdomRules.TicksPerDay * 1000L)));
			Assert.AreEqual(KingdomUpgradeRules.UpgradeVerdict.Ready,
				AssessAbsorbing(LeanedOn(SupportPerDay: 0, BuildTicks: 0L)));
		}

		[Test]
		public void TheVerdictIsAPureFunctionOfWhatTheSettlementHolds()
		{
			// No hidden clock: the same question asked twice, and asked again after other
			// questions, answers the same. A trigger that consulted anything outside its arguments
			// would drift across these calls.
			KingdomUpgradeRules.AbsorptionDemand demand = LeanedOn();
			KingdomUpgradeRules.UpgradeVerdict first = AssessAbsorbing(demand);
			AssessAbsorbing(LeanedOn(SupportPerDay: 0));
			AssessAbsorbing(LeanedOn(IsHousing: true, Residents: 9, SpareLodging: 0));
			Assert.AreEqual(first, AssessAbsorbing(demand));
			Assert.AreEqual(first, AssessAbsorbing(demand));
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
