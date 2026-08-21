#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.Kernel;
using Cause = ThousandAndFirst.KingdomWearRules.WearCause;
using Verdict = ThousandAndFirst.KingdomWearRules.RepairVerdict;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Wear and repair (Addendum 7: "maintenance/wear translation"). What this file asserts:
	/// wear comes from events and only events (every draw is a pure function of a real ordinal,
	/// never a clock), a work is never destroyed (the ceiling is
	/// <c>KingdomMaterialRules.MaxWearPercent</c>, always short of 100), a reload never re-rolls a
	/// question already answered (determinism across repeated calls), and a repair job's
	/// readiness is a clean function of the founder's own wish, this pass's hands, and whether the
	/// stockpiles cover it. The material-cost math itself (<c>RepairCost</c>/<c>RepairBits</c>/
	/// <c>RepairEffort</c>/<c>AddWear</c>/<c>ConditionPercent</c>) is
	/// <c>KingdomMaterialRules</c>' own and is asserted in its own test file; this one asserts
	/// that <c>KingdomWearRules</c> calls it correctly and adds nothing that contradicts it.
	/// </summary>
	public class KingdomWearRulesTests
	{
		private const string City = "taf:settlement:test-city";

		// --- Causes: named, and each with its own increment ------------------------------------

		[TestCase(Cause.Raid, KingdomWearRules.RaidDamageIncrement)]
		[TestCase(Cause.HardRunning, KingdomWearRules.HardRunDamageIncrement)]
		[TestCase(Cause.TemperamentalTech, KingdomWearRules.TemperamentalDamageIncrement)]
		public void IncrementFor_MatchesTheNamedConstantPerCause(Cause cause, int expected)
		{
			Assert.AreEqual(expected, KingdomWearRules.IncrementFor(cause));
		}

		[Test]
		public void IncrementFor_NoneAddsNothing()
		{
			Assert.AreEqual(0, KingdomWearRules.IncrementFor(Cause.None));
		}

		[TestCase(Cause.Raid)]
		[TestCase(Cause.HardRunning)]
		[TestCase(Cause.TemperamentalTech)]
		public void CauseVerb_NeverEmptyForARealCause(Cause cause)
		{
			Assert.IsFalse(string.IsNullOrEmpty(KingdomWearRules.CauseVerb(cause)));
		}

		[Test]
		public void CauseVerb_EveryRealCauseReadsDifferently()
		{
			string raid = KingdomWearRules.CauseVerb(Cause.Raid);
			string hardRun = KingdomWearRules.CauseVerb(Cause.HardRunning);
			string temper = KingdomWearRules.CauseVerb(Cause.TemperamentalTech);
			Assert.AreNotEqual(raid, hardRun);
			Assert.AreNotEqual(hardRun, temper);
			Assert.AreNotEqual(raid, temper);
		}

		// --- Combined effectiveness: crew stretch reduced again by wear -----------------------

		[Test]
		public void CombinedEffectiveness_SoundWorkReadsExactlyItsCrewStretch()
		{
			Assert.AreEqual(100, KingdomWearRules.CombinedEffectiveness(100, 0));
			Assert.AreEqual(64, KingdomWearRules.CombinedEffectiveness(64, 0));
			Assert.AreEqual(0, KingdomWearRules.CombinedEffectiveness(0, 0));
		}

		[Test]
		public void CombinedEffectiveness_AtTheWearCeilingMatchesConditionPercent()
		{
			int expected = 100 * KingdomMaterialRules.ConditionPercent(KingdomMaterialRules.MaxWearPercent) / 100;
			Assert.AreEqual(expected, KingdomWearRules.CombinedEffectiveness(100, KingdomMaterialRules.MaxWearPercent));
			Assert.Greater(KingdomWearRules.CombinedEffectiveness(100, KingdomMaterialRules.MaxWearPercent), 0,
				"a damaged work runs reduced, never dead");
		}

		[Test]
		public void CombinedEffectiveness_ClampsAnOutOfRangeCrewStretch()
		{
			Assert.AreEqual(0, KingdomWearRules.CombinedEffectiveness(-5, 0));
			Assert.AreEqual(100, KingdomWearRules.CombinedEffectiveness(500, 0));
		}

		[Test]
		public void CombinedEffectiveness_TwoIndependentShortfallsCombineByMultiplying()
		{
			// Half-crewed AND half-wrecked must read worse than either alone, never merely as
			// bad as the worse of the two -- that would erase one of the two real reasons.
			int halfCrew = KingdomWearRules.CombinedEffectiveness(50, 0);
			int halfWear = KingdomWearRules.CombinedEffectiveness(100, KingdomMaterialRules.MaxWearPercent / 2);
			int both = KingdomWearRules.CombinedEffectiveness(50, KingdomMaterialRules.MaxWearPercent / 2);
			Assert.Less(both, halfCrew);
			Assert.Less(both, halfWear);
		}

		// --- Addendum 10(b): the ruling. Wear reduces EVERY work, staffed or not --------------

		[Test]
		public void WorkEffectiveness_ASoundWorkIsWholeWhetherItAsksForCrewOrNot()
		{
			Assert.AreEqual(100, KingdomWearRules.WorkEffectiveness(0, 0, 0), "a staffless work asks for nobody and is whole");
			Assert.AreEqual(100, KingdomWearRules.WorkEffectiveness(3, 100, 0), "a fully crewed sound work is whole");
		}

		[Test]
		public void WorkEffectiveness_AStafflessWorkRunsAtItsOwnCondition()
		{
			// The ruling: the staffed-only ternary handed this arm a flat 100, so wear reached the
			// level exclusively through crewed designs.
			for (int wear = 0; wear <= KingdomMaterialRules.MaxWearPercent; wear += 5)
			{
				Assert.AreEqual(KingdomMaterialRules.ConditionPercent(wear),
					KingdomWearRules.WorkEffectiveness(0, 0, wear));
			}
		}

		[Test]
		public void WorkEffectiveness_AStafflessWorkIgnoresWhateverCrewStretchIsStampedOnIt()
		{
			// A design that asks for nobody never carries a meaningful stretch. Reading one would
			// make the answer depend on whichever pass last stamped the property.
			Assert.AreEqual(KingdomWearRules.WorkEffectiveness(0, 0, 20), KingdomWearRules.WorkEffectiveness(0, 100, 20));
			Assert.AreEqual(KingdomWearRules.WorkEffectiveness(0, 37, 20), KingdomWearRules.WorkEffectiveness(0, 0, 20));
		}

		[Test]
		public void WorkEffectiveness_ARuinedStafflessWorkCarriesLessThanASoundOne()
		{
			int sound = KingdomWearRules.WorkEffectiveness(0, 0, 0);
			int ruined = KingdomWearRules.WorkEffectiveness(0, 0, KingdomMaterialRules.MaxWearPercent);
			Assert.Less(ruined, sound, "a ruined reservoir does not carry its full drams");
			Assert.Greater(ruined, 0, "and it is not gone either: a damaged work stands");
		}

		[Test]
		public void WorkEffectiveness_TheReservoirCase()
		{
			// KingdomBuildings.xml, Key="reservoir": Carries="water:26", no Staff attribute. The
			// named case the ruling overturned - the grand water design automates to staffless, so
			// under the old ternary it was the one work a collapse could never touch.
			const int ReservoirDrams = 26;
			int sound = KingdomCatalogueRules.Carried(ReservoirDrams, KingdomWearRules.WorkEffectiveness(0, 0, 0));
			int wrecked = KingdomCatalogueRules.Carried(ReservoirDrams, KingdomWearRules.WorkEffectiveness(0, 0, KingdomMaterialRules.MaxWearPercent));
			Assert.AreEqual(ReservoirDrams, sound, "a sound reservoir carries every dram it declares");
			Assert.Less(wrecked, ReservoirDrams, "a half-wrecked reservoir carries fewer");
			Assert.AreEqual(ReservoirDrams * KingdomMaterialRules.ConditionPercent(KingdomMaterialRules.MaxWearPercent) / 100, wrecked);
		}

		[Test]
		public void WorkEffectiveness_ACrewedWorkStillCombinesBothShortfalls()
		{
			Assert.AreEqual(KingdomWearRules.CombinedEffectiveness(50, 30), KingdomWearRules.WorkEffectiveness(2, 50, 30));
		}

		[Test]
		public void WorkEffectiveness_MendingRestoresTheWholeFigureForEitherKind()
		{
			// The consequences are of damage, not of history: zero wear reads exactly as a work
			// that was never damaged at all.
			Assert.AreEqual(KingdomWearRules.WorkEffectiveness(0, 0, 0), KingdomWearRules.WorkEffectiveness(0, 0, 0));
			Assert.AreEqual(100, KingdomWearRules.WorkEffectiveness(0, 0, 0));
			Assert.AreEqual(80, KingdomWearRules.WorkEffectiveness(2, 80, 0));
		}

		// --- Addendum 10(b): storage leaks -----------------------------------------------------

		[Test]
		public void Leaked_ASoundStoreLosesNothingHoweverLongTheStretch()
		{
			Assert.AreEqual(0, KingdomWearRules.Leaked(1024, 1024, 0, 1));
			Assert.AreEqual(0, KingdomWearRules.Leaked(1024, 1024, 0, 100000));
			Assert.AreEqual(0, KingdomWearRules.Leaked(1024, 1024, -5, 100000));
		}

		[Test]
		public void Leaked_NoDaysMeansNoLossSoAnUnplantedStampCostsNothing()
		{
			// The stamp is planted before the first count (r_KingdomWear.LastLeakTick). A caller
			// that has just planted it hands in zero days and must be told zero.
			Assert.AreEqual(0, KingdomWearRules.Leaked(1024, 1024, KingdomMaterialRules.MaxWearPercent, 0));
			Assert.AreEqual(0, KingdomWearRules.Leaked(1024, 1024, KingdomMaterialRules.MaxWearPercent, -3));
		}

		[Test]
		public void Leaked_AnEmptyStoreLosesNothing()
		{
			Assert.AreEqual(0, KingdomWearRules.Leaked(1024, 0, KingdomMaterialRules.MaxWearPercent, 90));
			Assert.AreEqual(0, KingdomWearRules.Leaked(0, 100, KingdomMaterialRules.MaxWearPercent, 90));
		}

		[Test]
		public void Leaked_NeverTakesMoreThanIsActuallyInThere()
		{
			Assert.AreEqual(7, KingdomWearRules.Leaked(1024, 7, KingdomMaterialRules.MaxWearPercent, 100000));
			Assert.AreEqual(1, KingdomWearRules.Leaked(1024, 1, KingdomMaterialRules.MaxWearPercent, 100000));
		}

		[Test]
		public void Leaked_AWholeCapacityIsGoneAtTheCeilingInTheStatedNumberOfDays()
		{
			int capacity = 1024;
			int days = KingdomWearRules.LeakDaysToEmptyAtCeiling;
			Assert.AreEqual(capacity,
				KingdomWearRules.Leaked(capacity, capacity, KingdomMaterialRules.MaxWearPercent, days),
				"the tuning constant has to mean what it says");
			Assert.Less(KingdomWearRules.Leaked(capacity, capacity, KingdomMaterialRules.MaxWearPercent, days - 1), capacity);
		}

		[Test]
		public void Leaked_HalfTheWearIsHalfTheRate()
		{
			int capacity = 1200;
			int full = KingdomWearRules.Leaked(capacity, capacity, KingdomMaterialRules.MaxWearPercent, 10);
			int half = KingdomWearRules.Leaked(capacity, capacity, KingdomMaterialRules.MaxWearPercent / 2, 10);
			Assert.AreEqual(full / 2, half);
			Assert.Greater(full, half, "leak rate scales with wear or it is not a consequence of damage");
		}

		[Test]
		public void Leaked_GrowsWithTheDaysSoALongAbsenceIsAnHonestLoss()
		{
			int capacity = 640;
			int wear = 30;
			int shortStretch = KingdomWearRules.Leaked(capacity, capacity, wear, 3);
			int longStretch = KingdomWearRules.Leaked(capacity, capacity, wear, 60);
			Assert.Greater(longStretch, shortStretch);
			Assert.Greater(longStretch, 0, "a season with a hole in the cistern costs the settlement something");
		}

		[Test]
		public void Leaked_ASmallStoreLosesNothingInADayAndSomethingOverASeason()
		{
			// Why a caller BANKS days that produced no loss instead of spending them: the division
			// is done last, so a small store's share only becomes a whole dram once enough days
			// have accumulated. Spending those days would make a leak the founder could defeat by
			// walking in and out of the zone.
			Assert.AreEqual(0, KingdomWearRules.Leaked(16, 16, 15, 1));
			Assert.Greater(KingdomWearRules.Leaked(16, 16, 15, 90), 0);
		}

		[Test]
		public void Leaked_IsTheSameAnswerEveryTimeItIsAsked()
		{
			for (int wear = 1; wear <= KingdomMaterialRules.MaxWearPercent; wear++)
			{
				int first = KingdomWearRules.Leaked(256, 200, wear, 17);
				int second = KingdomWearRules.Leaked(256, 200, wear, 17);
				Assert.AreEqual(first, second, "the leak is arithmetic, not a draw: no reload ever changes it");
			}
		}

		[Test]
		public void Leaked_DoesNotOverflowOnAnAbsenceOfAnyLength()
		{
			// The days are uncapped (Addendum 8 clause 1), so the arithmetic has to survive one.
			Assert.AreEqual(1024, KingdomWearRules.Leaked(1024, 1024, KingdomMaterialRules.MaxWearPercent, int.MaxValue));
		}

		[Test]
		public void LeakBegunLine_NamesTheWorkAndReadsDifferentlyPerKind()
		{
			string water = KingdomWearRules.LeakBegunLine("the reservoir", KingdomWearRules.LeakKind.Water);
			string charge = KingdomWearRules.LeakBegunLine("the salt store", KingdomWearRules.LeakKind.Charge);
			StringAssert.Contains("the reservoir", water);
			StringAssert.Contains("the salt store", charge);
			Assert.AreNotEqual(water, charge, "a cistern and a bed of salt fail in different sentences");
		}

		[Test]
		public void LeakStoppedLine_IsTheUnsayingAndNeverReadsLikeTheBeginning()
		{
			foreach (KingdomWearRules.LeakKind kind in new[] { KingdomWearRules.LeakKind.Water, KingdomWearRules.LeakKind.Charge })
			{
				string begun = KingdomWearRules.LeakBegunLine("the reservoir", kind);
				string stopped = KingdomWearRules.LeakStoppedLine("the reservoir", kind);
				StringAssert.Contains("the reservoir", stopped);
				Assert.AreNotEqual(begun, stopped);
			}
		}

		// --- Hard-running: a streak, re-eligible once per whole further streak ----------------

		[Test]
		public void AtHardRunMilestone_FalseBelowTheThreshold()
		{
			Assert.IsFalse(KingdomWearRules.AtHardRunMilestone(KingdomWearRules.HardRunStreakThreshold - 1));
		}

		[Test]
		public void AtHardRunMilestone_TrueAtAndPastTheThreshold()
		{
			Assert.IsTrue(KingdomWearRules.AtHardRunMilestone(KingdomWearRules.HardRunStreakThreshold));
			Assert.IsTrue(KingdomWearRules.AtHardRunMilestone(KingdomWearRules.HardRunStreakThreshold + 1));
		}

		[Test]
		public void HardRunMilestone_ZeroBelowTheFirstAndIncrementsAtEachWholeStreak()
		{
			int threshold = KingdomWearRules.HardRunStreakThreshold;
			Assert.AreEqual(0uL, KingdomWearRules.HardRunMilestone(threshold - 1));
			Assert.AreEqual(1uL, KingdomWearRules.HardRunMilestone(threshold));
			Assert.AreEqual(1uL, KingdomWearRules.HardRunMilestone((2 * threshold) - 1),
				"a milestone that answered no is not asked again until a whole further streak is run");
			Assert.AreEqual(2uL, KingdomWearRules.HardRunMilestone(2 * threshold));
		}

		[Test]
		public void RollHardRun_FalseBelowTheFirstMilestoneHoweverCloseTheStreakIs()
		{
			Assert.IsFalse(KingdomWearRules.RollHardRun(City, "mill-1", KingdomWearRules.HardRunStreakThreshold - 1));
		}

		[Test]
		public void RollHardRun_AnswersTheSameWayEveryTimeItIsAsked()
		{
			int streak = KingdomWearRules.HardRunStreakThreshold;
			for (int i = 0; i < 40; i++)
			{
				string workId = "mill-" + i;
				bool first = KingdomWearRules.RollHardRun(City, workId, streak);
				bool second = KingdomWearRules.RollHardRun(City, workId, streak);
				Assert.AreEqual(first, second, "a reload must never re-roll a question already answered");
			}
		}

		[Test]
		public void RollHardRun_FailsClosedForAMalformedSettlementId()
		{
			Assert.IsFalse(KingdomWearRules.RollHardRun("not a taf id", "mill-1", KingdomWearRules.HardRunStreakThreshold));
			Assert.IsFalse(KingdomWearRules.RollHardRun(null, "mill-1", KingdomWearRules.HardRunStreakThreshold));
		}

		[Test]
		public void RollHardRun_TurnsRoughlyTheStatedShareAtOneMilestone()
		{
			int streak = KingdomWearRules.HardRunStreakThreshold;
			int wore = 0;
			const int sample = 500;
			for (int i = 0; i < sample; i++)
			{
				if (KingdomWearRules.RollHardRun(City, "mill-" + i, streak))
				{
					wore++;
				}
			}
			int percent = wore * 100 / sample;
			Assert.Greater(wore, 0, "a threshold nobody ever wears at is not a threshold");
			Assert.Less(wore, sample, "reaching a milestone buys a draw, not a certainty");
			Assert.GreaterOrEqual(percent, KingdomWearRules.HardRunChancePercent - 8);
			Assert.LessOrEqual(percent, KingdomWearRules.HardRunChancePercent + 8);
		}

		[Test]
		public void RollHardRun_ANewMilestoneIsAFreshQuestion()
		{
			int threshold = KingdomWearRules.HardRunStreakThreshold;
			bool differed = false;
			for (int i = 0; i < 60 && !differed; i++)
			{
				string workId = "mill-" + i;
				differed = KingdomWearRules.RollHardRun(City, workId, threshold)
					!= KingdomWearRules.RollHardRun(City, workId, threshold * 2);
			}
			Assert.IsTrue(differed, "the milestone ordinal must actually reach the draw");
		}

		// --- Temperamental tech: an independent question every pass it runs --------------------

		[Test]
		public void RollTemperamental_AnswersTheSameWayForTheSameTick()
		{
			for (int i = 0; i < 40; i++)
			{
				string workId = "salvage-" + i;
				bool first = KingdomWearRules.RollTemperamental(City, workId, 9000L);
				bool second = KingdomWearRules.RollTemperamental(City, workId, 9000L);
				Assert.AreEqual(first, second);
			}
		}

		[Test]
		public void RollTemperamental_FailsClosedForAMalformedSettlementId()
		{
			Assert.IsFalse(KingdomWearRules.RollTemperamental("nope", "salvage-1", 9000L));
		}

		[Test]
		public void RollTemperamental_DifferentTicksAreIndependentQuestions()
		{
			bool differed = false;
			for (long tick = 1L; tick < 60L && !differed; tick++)
			{
				differed = KingdomWearRules.RollTemperamental(City, "salvage-1", tick)
					!= KingdomWearRules.RollTemperamental(City, "salvage-1", tick + 1000L);
			}
			Assert.IsTrue(differed, "every pass a certified machine runs is its own question, not a milestone to wait out");
		}

		[Test]
		public void RollTemperamental_TurnsRoughlyTheStatedSmallShare()
		{
			int actedUp = 0;
			const int sample = 800;
			for (int i = 0; i < sample; i++)
			{
				if (KingdomWearRules.RollTemperamental(City, "salvage-" + i, 12345L))
				{
					actedUp++;
				}
			}
			int percent = actedUp * 100 / sample;
			Assert.GreaterOrEqual(percent, KingdomWearRules.TemperamentalChancePercent - 4);
			Assert.LessOrEqual(percent, KingdomWearRules.TemperamentalChancePercent + 4);
		}

		// --- Raid damage: bounded per raid, an independent question per candidate work --------

		[TestCase(0, 0)]
		[TestCase(-3, 0)]
		[TestCase(1, 1)]
		[TestCase(3, 1)]
		[TestCase(4, 2)]
		[TestCase(400, KingdomWearRules.MaxWorksDamagedPerRaid)]
		public void WorksToDamage_GrowsGentlyAndNeverPastTheCeiling(int raidersThrough, int expected)
		{
			Assert.AreEqual(expected, KingdomWearRules.WorksToDamage(raidersThrough));
		}

		[Test]
		public void RollRaidDamage_AnswersTheSameWayForTheSameRaid()
		{
			for (int i = 0; i < 40; i++)
			{
				string workId = "wall-" + i;
				bool first = KingdomWearRules.RollRaidDamage(City, workId, 5000L);
				bool second = KingdomWearRules.RollRaidDamage(City, workId, 5000L);
				Assert.AreEqual(first, second);
			}
		}

		[Test]
		public void RollRaidDamage_FailsClosedForAMalformedSettlementId()
		{
			Assert.IsFalse(KingdomWearRules.RollRaidDamage("nope", "wall-1", 5000L));
		}

		[Test]
		public void RollRaidDamage_DifferentWorksAreIndependentQuestionsOnTheSameRaid()
		{
			bool differed = false;
			for (int i = 0; i < 60 && !differed; i++)
			{
				differed = KingdomWearRules.RollRaidDamage(City, "wall-" + i, 5000L)
					!= KingdomWearRules.RollRaidDamage(City, "granary-" + i, 5000L);
			}
			Assert.IsTrue(differed, "two different works must not be forced to share one raid's answer");
		}

		// --- WorkStream: the semantic-id fold ---------------------------------------------------

		[Test]
		public void WorkStream_IsAlwaysAValidSemanticId()
		{
			Assert.IsTrue(KernelSemanticId.IsValid(KingdomWearRules.WorkStream("mill-1")));
			Assert.IsTrue(KernelSemanticId.IsValid(KingdomWearRules.WorkStream(null)));
			Assert.IsTrue(KernelSemanticId.IsValid(KingdomWearRules.WorkStream("")));
			Assert.IsTrue(KernelSemanticId.IsValid(KingdomWearRules.WorkStream("MiXeD-Case ID!!")));
		}

		[Test]
		public void WorkStream_TwoDifferentIdsFoldToDifferentStreams()
		{
			Assert.AreNotEqual(KingdomWearRules.WorkStream("mill-1"), KingdomWearRules.WorkStream("mill-2"));
		}

		[Test]
		public void WorkStream_IsStableForTheSameId()
		{
			Assert.AreEqual(KingdomWearRules.WorkStream("mill-1"), KingdomWearRules.WorkStream("mill-1"));
		}

		// --- Repair readiness -------------------------------------------------------------------

		[Test]
		public void AssessRepair_HeldOverridesEverythingElse()
		{
			Assert.AreEqual(Verdict.Held, KingdomWearRules.AssessRepair(true, 0, false));
			Assert.AreEqual(Verdict.Held, KingdomWearRules.AssessRepair(true, 5, true));
		}

		[Test]
		public void AssessRepair_NoHandsWhenNotHeldAndNobodyIsFree()
		{
			Assert.AreEqual(Verdict.NoHands, KingdomWearRules.AssessRepair(false, 0, true));
			Assert.AreEqual(Verdict.NoHands, KingdomWearRules.AssessRepair(false, -1, true));
		}

		[Test]
		public void AssessRepair_NoMaterialsWhenHandedButUncovered()
		{
			Assert.AreEqual(Verdict.NoMaterials, KingdomWearRules.AssessRepair(false, 2, false));
		}

		[Test]
		public void AssessRepair_ReadyOnlyWhenNotHeldAndHandedAndCovered()
		{
			Assert.AreEqual(Verdict.Ready, KingdomWearRules.AssessRepair(false, 1, true));
		}

		[TestCase(Verdict.NoHands, true)]
		[TestCase(Verdict.NoMaterials, true)]
		[TestCase(Verdict.Ready, false)]
		[TestCase(Verdict.Held, false)]
		[TestCase(Verdict.OtherWorkUnderway, false)]
		public void IsBlocked_OnlyTrueForAnActualShortage(Verdict verdict, bool expected)
		{
			Assert.AreEqual(expected, KingdomWearRules.IsBlocked(verdict));
		}

		[TestCase(Verdict.Ready)]
		[TestCase(Verdict.Held)]
		public void ReasonLine_NullForAVerdictThatIsNeverAnnounced(Verdict verdict)
		{
			Assert.IsNull(KingdomWearRules.ReasonLine(verdict, "the mill"));
		}

		[TestCase(Verdict.NoHands)]
		[TestCase(Verdict.NoMaterials)]
		[TestCase(Verdict.OtherWorkUnderway)]
		public void ReasonLine_NamesTheWorkForEveryTellableVerdict(Verdict verdict)
		{
			string line = KingdomWearRules.ReasonLine(verdict, "the mill");
			Assert.IsNotNull(line);
			StringAssert.Contains("the mill", line);
		}

		[Test]
		public void ReasonLine_EachTellableVerdictReadsDifferently()
		{
			string noHands = KingdomWearRules.ReasonLine(Verdict.NoHands, "the mill");
			string noMaterials = KingdomWearRules.ReasonLine(Verdict.NoMaterials, "the mill");
			string queued = KingdomWearRules.ReasonLine(Verdict.OtherWorkUnderway, "the mill");
			Assert.AreNotEqual(noHands, noMaterials);
			Assert.AreNotEqual(noMaterials, queued);
			Assert.AreNotEqual(noHands, queued);
		}

		// --- Prose: composed once, asserted directly --------------------------------------------

		[Test]
		public void DamagedLine_NamesTheWorkTheCauseAndTheChainsOwnConditionWording()
		{
			string line = KingdomWearRules.DamagedLine("the mill", Cause.Raid, 30);
			StringAssert.Contains("the mill", line);
			StringAssert.Contains(KingdomWearRules.CauseVerb(Cause.Raid), line);
			StringAssert.Contains(KingdomMaterialRules.ConditionWord(30), line);
			StringAssert.Contains(KingdomMaterialRules.ConditionPercent(30).ToString(), line);
		}

		[Test]
		public void RepairBegunLine_NamesTheWork()
		{
			StringAssert.Contains("the mill", KingdomWearRules.RepairBegunLine("the mill"));
		}

		[Test]
		public void RepairCompleteLine_NamesTheWork()
		{
			StringAssert.Contains("the mill", KingdomWearRules.RepairCompleteLine("the mill"));
		}

		[Test]
		public void StatusSuffix_EmptyWhenNothingIsDamaged()
		{
			Assert.AreEqual("", KingdomWearRules.StatusSuffix(0));
		}

		[Test]
		public void StatusSuffix_NamesTheCountWhenSomethingIs()
		{
			StringAssert.Contains("3", KingdomWearRules.StatusSuffix(3));
		}

		[Test]
		public void NextNeedLine_EmptyWhenNothingIsDamaged()
		{
			Assert.AreEqual("", KingdomWearRules.NextNeedLine(0));
		}

		[Test]
		public void NextNeedLine_NamesTheCountWhenSomethingIs()
		{
			StringAssert.Contains("2", KingdomWearRules.NextNeedLine(2));
		}

		[Test]
		public void AMendingWithNoHandsSaysSoAndPutsNothingBackHoweverLongTheStretch()
		{
			// Mending runs the full elapsed now (Addendum 8 clause 1), and this is why that is
			// safe: the labour term is hands, so four hundred days of nobody puts back exactly
			// nothing -- and AdvanceRepair reads the gate, names the block once (STANDARDS 7b),
			// and only then spends the days, so the idle stretch is gone rather than banked for
			// a crew that was never there.
			Assert.AreEqual(0, KingdomMaterialRules.EffortWorked(0, 400));
			Assert.IsNotNull(KingdomWearRules.ReasonLine(Verdict.NoHands, "the mill"));
			// A real crew over the same stretch does real work, linearly in the days.
			Assert.AreEqual(KingdomMaterialRules.EffortWorked(2, 1) * 400, KingdomMaterialRules.EffortWorked(2, 400));
		}
	}
}
#endif
