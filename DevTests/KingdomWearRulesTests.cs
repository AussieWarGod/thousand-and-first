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
