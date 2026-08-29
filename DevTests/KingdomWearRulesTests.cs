#if TAF_TESTS
using System;
using System.IO;
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
		[Test]
		public void FoodLeakPlansOnlySpendableStacksButKeepsPhysicalCapacityOccupied()
		{
			string source = KingdomWearLogicalSource.Read();
			StringAssert.Contains("int held = KingdomSurvey.HeldIn(Work);", source);
			StringAssert.Contains("AvailableIn(Work, leases)", source);
			StringAssert.Contains("CanSpend(leases, food)", source);
			StringAssert.Contains("TrySpoilFromExact", source);
		}

		private const string City = "taf:settlement:test-city";

		[Test]
		public void WearEnumsKeepTheirPersistedNumericAbi()
		{
			AssertEnum(typeof(KingdomWearSinkDisposition), 0, 1, 2, 3, 4, 5);
			AssertEnum(typeof(KingdomWearPassPhase), 0, 1, 2, 3, 4, 5, 6, 7, 8);
			AssertEnum(typeof(KingdomWearPassAction), 0, 1, 2, 3);
			AssertEnum(typeof(KingdomWearIncidentPhase), 0, 1, 2, 3, 4, 5, 6, 7, 8);
			AssertEnum(typeof(KingdomWearLeakPhase), 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
			AssertEnum(typeof(KingdomWearMutationAction), 0, 1, 2, 3);
			AssertEnum(typeof(KingdomWearClockAction), 0, 1, 2, 3);
			AssertEnum(typeof(KingdomWearRules.WearCause), 0, 1, 2, 3);
			AssertEnum(typeof(KingdomWearRules.WearChannel), 1, 2, 3);
			AssertEnum(typeof(KingdomWearRules.RepairVerdict), 0, 1, 2, 3, 4);
			AssertEnum(typeof(KingdomWearRules.LeakKind), 1, 2, 3);
		}

		private static void AssertEnum(Type type, params int[] expected)
		{
			Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(type), type.FullName);
			Array values = Enum.GetValues(type);
			Assert.AreEqual(expected.Length, values.Length, type.FullName);
			for (int i = 0; i < expected.Length; i++)
			{
				Assert.AreEqual(expected[i], Convert.ToInt32(values.GetValue(i)), type.FullName + "[" + i + "]");
			}
		}

		private static string ReadRepoSource(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

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
			Assert.Less(ruined, sound, "a ruined air-well field does not carry its full drams");
			Assert.Greater(ruined, 0, "and it is not gone either: a damaged work stands");
		}

		[Test]
		public void WorkEffectiveness_TheGrandStafflessWaterWorkCase()
		{
			// KingdomBuildings.xml, Key="airwellfield": Carries="water:25", no Staff attribute.
			// The named case the ruling overturned - the grand water design automates to
			// staffless, so under the old ternary it was the one work a collapse could never
			// touch. It was the reservoir until Addendum 11(a) flipped every store to
			// storage-only; the work is a producer now and the arithmetic is identical, which is
			// the point: what a staffless work carries has always been its condition.
			const int FieldDrams = 25;
			int sound = KingdomCatalogueRules.Carried(FieldDrams, KingdomWearRules.WorkEffectiveness(0, 0, 0));
			int wrecked = KingdomCatalogueRules.Carried(FieldDrams, KingdomWearRules.WorkEffectiveness(0, 0, KingdomMaterialRules.MaxWearPercent));
			Assert.AreEqual(FieldDrams, sound, "a sound air-well field carries every dram it declares");
			Assert.Less(wrecked, FieldDrams, "a half-wrecked air-well field carries fewer");
			Assert.AreEqual(FieldDrams * KingdomMaterialRules.ConditionPercent(KingdomMaterialRules.MaxWearPercent) / 100, wrecked);
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

		/// <summary>Every kind this build knows, so a kind added later is covered by the prose
		/// tests below without anyone remembering to widen an array.</summary>
		private static readonly KingdomWearRules.LeakKind[] EveryLeakKind = (KingdomWearRules.LeakKind[])
			System.Enum.GetValues(typeof(KingdomWearRules.LeakKind));

		[Test]
		public void LeakBegunLine_NamesTheWorkAndReadsDifferentlyPerKind()
		{
			System.Collections.Generic.List<string> said = new System.Collections.Generic.List<string>();
			foreach (KingdomWearRules.LeakKind kind in EveryLeakKind)
			{
				string line = KingdomWearRules.LeakBegunLine("the reservoir", kind);
				StringAssert.Contains("the reservoir", line);
				CollectionAssert.DoesNotContain(said, line,
					"a cistern, a bed of salt and a granary fail in different sentences (" + kind + ")");
				said.Add(line);
			}
		}

		[Test]
		public void LeakStoppedLine_IsTheUnsayingAndNeverReadsLikeTheBeginning()
		{
			foreach (KingdomWearRules.LeakKind kind in EveryLeakKind)
			{
				string begun = KingdomWearRules.LeakBegunLine("the reservoir", kind);
				string stopped = KingdomWearRules.LeakStoppedLine("the reservoir", kind);
				StringAssert.Contains("the reservoir", stopped);
				Assert.AreNotEqual(begun, stopped);
			}
		}

		[Test]
		public void LeakKind_CarriesFoodNowThatFoodIsAFlow()
		{
			// Addendum 10(b) deferred food spoilage on one condition - "food spoilage waits until
			// food is a flow" - and Wave B made it one. The values are frozen: a renumbering
			// would repoint every saved leak at the wrong kind of sentence.
			Assert.AreEqual(1, (int)KingdomWearRules.LeakKind.Water);
			Assert.AreEqual(2, (int)KingdomWearRules.LeakKind.Charge);
			Assert.AreEqual(3, (int)KingdomWearRules.LeakKind.Food);
		}

		[Test]
		public void Leaked_PricesAHoledCisternAgainstTheRungItOpens()
		{
			// The sizing law the whole water lane is built to (Wave G1). A store's declared
			// MaxVolume is now the whole of what it is paid in - no vessel carries `water` any
			// more - so nothing stops an author from declaring a vessel ten times the size, and
			// what stops it is this: a holed store loses its own capacity over
			// LeakDaysToEmptyAtCeiling, and that daily loss must stay under the drinking bill of
			// the rung the store opens at. Otherwise one lost rung empties the cushion faster
			// than the settlement can refill it, which is the death spiral this lane must not
			// have. Shipped figures: cistern 256 drams at Steading (five settlers), cistern vault
			// 768 at Village (twelve), reservoir 1920 at Town (twenty-five), waterworks 4608 at
			// City (fifty).
			(int Capacity, int Population, GrowthStage Stage)[] shipped = new[]
			{
				(256, 5, GrowthStage.Steading),
				(768, 12, GrowthStage.Village),
				(1920, 25, GrowthStage.Town),
				(4608, 50, GrowthStage.City)
			};
			foreach ((int capacity, int population, GrowthStage stage) in shipped)
			{
				int lostInADay = KingdomWearRules.Leaked(capacity, capacity, KingdomMaterialRules.MaxWearPercent, 1);
				int bill = KingdomRules.UpkeepDrams(population, stage);
				Assert.Greater(lostInADay, 0, "a store at the wear ceiling must actually be losing something");
				Assert.Less(lostInADay, bill,
					"a " + capacity + "-dram store at " + stage + " leaks " + lostInADay
						+ " a day against a bill of " + bill + "; a vessel that outruns its rung's "
						+ "drinking makes one lost rung fatal");
			}
		}

		[Test]
		public void Leaked_PricesASpoilingGranaryAgainstTheRungItFeeds()
		{
			// The granary's own declared capacity (ObjectBlueprints.xml, r_KingdomLarderCapacity
			// = 288) at the wear ceiling, against the Village rung it opens at, which eats twelve
			// a day. Spoilage must thin the cushion and never outrun the fields: a ruined granary
			// losing more in a day than the settlement eats would make one bad roll fatal.
			int lostInADay = KingdomWearRules.Leaked(288, 288, KingdomMaterialRules.MaxWearPercent, 1);
			Assert.Greater(lostInADay, 0, "a granary at the ceiling must actually be losing something");
			Assert.Less(lostInADay, KingdomRules.RationsPerDay(12),
				"spoilage thins the cushion; it never outruns what the fields make");
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

		// --- Durable pass and incident fault decisions ------------------------------------

		[Test]
		public void PassAction_StartsOnceResumesEveryPhaseAndRejectsRegression()
		{
			Assert.AreEqual(KingdomWearPassAction.Start, KingdomWearRules.PassAction(
				1200L, 0L, KingdomWearPassPhase.None, 2400L));
			Assert.AreEqual(KingdomWearPassAction.AlreadyApplied, KingdomWearRules.PassAction(
				2400L, 0L, KingdomWearPassPhase.None, 2400L));
			for (int raw = (int)KingdomWearPassPhase.Bound;
				raw <= (int)KingdomWearPassPhase.TemperDone; raw++)
			{
				Assert.AreEqual(KingdomWearPassAction.Resume, KingdomWearRules.PassAction(
					1200L, 2400L, (KingdomWearPassPhase)raw, 2400L), "phase " + raw);
			}
			Assert.AreEqual(KingdomWearPassAction.Quarantine, KingdomWearRules.PassAction(
				2400L, 0L, KingdomWearPassPhase.None, 1200L));
			Assert.AreEqual(KingdomWearPassAction.Quarantine, KingdomWearRules.PassAction(
				1200L, 1800L, KingdomWearPassPhase.HardIncident, 2400L));
			Assert.AreEqual(KingdomWearPassAction.Quarantine, KingdomWearRules.PassAction(
				-1L, 0L, KingdomWearPassPhase.None, 2400L));
			Assert.AreEqual(KingdomWearPassAction.Quarantine, KingdomWearRules.PassAction(
				0L, 0L, (KingdomWearPassPhase)99, 2400L));
		}

		[Test]
		public void DamageMutationAction_AppliesOnlyTheBoundDeltaAndConfirmsItsExactResult()
		{
			Assert.AreEqual(KingdomWearMutationAction.Apply,
				KingdomWearRules.DamageMutationAction(KingdomWearIncidentPhase.Bound, 20, 20, 30));
			Assert.AreEqual(KingdomWearMutationAction.Apply,
				KingdomWearRules.DamageMutationAction(KingdomWearIncidentPhase.MutationIntent, 20, 20, 30));
			Assert.AreEqual(KingdomWearMutationAction.Confirm,
				KingdomWearRules.DamageMutationAction(KingdomWearIncidentPhase.MutationIntent, 20, 30, 30));
			Assert.AreEqual(KingdomWearMutationAction.Quarantine,
				KingdomWearRules.DamageMutationAction(KingdomWearIncidentPhase.MutationIntent, 20, 25, 30));
			for (int raw = (int)KingdomWearIncidentPhase.Mutated;
				raw <= (int)KingdomWearIncidentPhase.Complete; raw++)
			{
				Assert.AreEqual(KingdomWearMutationAction.Confirm,
					KingdomWearRules.DamageMutationAction((KingdomWearIncidentPhase)raw, 20, 30, 30),
					"phase " + raw);
			}
			Assert.AreEqual(KingdomWearMutationAction.Wait,
				KingdomWearRules.DamageMutationAction(KingdomWearIncidentPhase.Quarantined, 20, 30, 30));
		}

		[Test]
		public void LeakMutationAction_NeverReappliesAnIntentWhoseCallbackMayHaveRestoredState()
		{
			Assert.AreEqual(KingdomWearMutationAction.Apply,
				KingdomWearRules.LeakMutationAction(KingdomWearLeakPhase.Bound, 20, 20, 15));
			Assert.AreEqual(KingdomWearMutationAction.Quarantine,
				KingdomWearRules.LeakMutationAction(KingdomWearLeakPhase.MutationIntent, 20, 20, 15));
			Assert.AreEqual(KingdomWearMutationAction.Quarantine,
				KingdomWearRules.LeakMutationAction(KingdomWearLeakPhase.MutationIntent, 20, 15, 15));
			Assert.AreEqual(KingdomWearMutationAction.Quarantine,
				KingdomWearRules.LeakMutationAction(KingdomWearLeakPhase.MutationIntent, 20, 18, 15));
			for (int raw = (int)KingdomWearLeakPhase.Mutated;
				raw <= (int)KingdomWearLeakPhase.Complete; raw++)
			{
				Assert.AreEqual(KingdomWearMutationAction.Wait,
					KingdomWearRules.LeakMutationAction((KingdomWearLeakPhase)raw, 20, 15, 15),
					"phase " + raw);
			}
			Assert.AreEqual(KingdomWearMutationAction.Quarantine,
				KingdomWearRules.LeakMutationAction(KingdomWearLeakPhase.Quarantined, 20, 15, 15));
		}

		[Test]
		public void LeakClockAction_PreservesAbsoluteTimeAndQuarantinesMalformedOrRegressedClocks()
		{
			Assert.AreEqual(KingdomWearClockAction.Plant,
				KingdomWearRules.LeakClockAction(false, 0L, 1200L, 1));
			Assert.AreEqual(KingdomWearClockAction.Wait,
				KingdomWearRules.LeakClockAction(true, 1200L, 1800L, 0));
			Assert.AreEqual(KingdomWearClockAction.Advance,
				KingdomWearRules.LeakClockAction(true, 1200L, 2400L, 1));
			Assert.AreEqual(KingdomWearClockAction.Quarantine,
				KingdomWearRules.LeakClockAction(true, 2400L, 1200L, 0));
			Assert.AreEqual(KingdomWearClockAction.Quarantine,
				KingdomWearRules.LeakClockAction(true, -1L, 1200L, 1));
			Assert.AreEqual(KingdomWearClockAction.Quarantine,
				KingdomWearRules.LeakClockAction(true, 1200L, 2400L, -1));
		}

		[Test]
		public void SavedWearParsers_CapRawRowsAndFieldsBeforeSplit()
		{
			int[] numbers;
			string[] ids;
			int wear;
			bool finishing;
			Assert.IsTrue(KingdomWearRules.TryCanonicalIntRows("0|7|2147483647", out numbers));
			Assert.IsFalse(KingdomWearRules.TryCanonicalIntRows("01", out numbers));
			Assert.IsFalse(KingdomWearRules.TryCanonicalIntRows(
				new string('1', KingdomWearRules.MaxRowsChars + 1), out numbers));
			Assert.IsFalse(KingdomWearRules.TryCanonicalIntRows(
				new string('|', KingdomWearRules.MaxRows), out numbers));
			Assert.IsTrue(KingdomWearRules.TryObjectIdRows("food-a|food-b", out ids));
			Assert.IsFalse(KingdomWearRules.TryObjectIdRows("food-a|food-a", out ids));
			Assert.IsFalse(KingdomWearRules.TryObjectIdRows(
				new string('x', KingdomWearRules.MaxObjectIdChars + 1), out ids));
			Assert.IsTrue(KingdomWearRules.TryRepairPayload("v1|25|1", out wear, out finishing));
			Assert.AreEqual(25, wear);
			Assert.IsTrue(finishing);
			Assert.IsFalse(KingdomWearRules.TryRepairPayload(
				new string('1', KingdomWearRules.MaxRepairPayloadChars + 1),
				out wear, out finishing));
			Assert.IsFalse(KingdomWearRules.TryRepairPayload("v1|25|1|extra",
				out wear, out finishing));
			string rules = ReadRepoSource("Growth/KingdomWearRules.cs");
			Assert.Less(rules.IndexOf("Text.Length > MaxRowsChars", StringComparison.Ordinal),
				rules.IndexOf("Text.Split('|')", StringComparison.Ordinal));
			Assert.Less(rules.IndexOf("Payload.Length > MaxRepairPayloadChars",
				StringComparison.Ordinal), rules.IndexOf("Payload.Split('|')",
				StringComparison.Ordinal));
		}

		[Test]
		public void UninspectableWearSinks_AreLostNeverClaimedDelivered()
		{
			Assert.AreEqual(KingdomWearSinkDisposition.Lost,
				KingdomWearRules.RecoverUninspectable(KingdomWearSinkDisposition.Attempting));
			Assert.AreEqual(KingdomWearSinkDisposition.Pending,
				KingdomWearRules.RecoverUninspectable(KingdomWearSinkDisposition.Pending));
			Assert.IsTrue(KingdomWearRules.SinkSettled(KingdomWearSinkDisposition.Delivered));
			Assert.IsTrue(KingdomWearRules.SinkSettled(KingdomWearSinkDisposition.Skipped));
			Assert.IsTrue(KingdomWearRules.SinkSettled(KingdomWearSinkDisposition.Lost));
			Assert.IsFalse(KingdomWearRules.SinkSettled(KingdomWearSinkDisposition.Attempting));
		}

		[Test]
		public void WearSource_QuarantinesReloadedMutationAndPublishesOnlyAfterExactProof()
		{
			string source = KingdomWearLogicalSource.Read();
			StringAssert.Contains("NormalizeSerializedFields", source);
			StringAssert.Contains("LeakCapacity", source);
			StringAssert.Contains("TryReadStrictTick", source);
			StringAssert.Contains("KingdomChronicle.RecordOnce", source);
			StringAssert.Contains("LastCompletedIncidentId", source);
			int recovery = source.IndexOf(
				"if (phase == KingdomWearLeakPhase.MutationIntent)", StringComparison.Ordinal);
			int recoveryEnd = source.IndexOf(
				"if (phase >= KingdomWearLeakPhase.Mutated)", recovery, StringComparison.Ordinal);
			Assert.GreaterOrEqual(recovery, 0);
			StringAssert.Contains("QuarantineLeak", source.Substring(recovery,
				recoveryEnd - recovery));
			Assert.IsFalse(source.Substring(recovery, recoveryEnd - recovery)
				.Contains("TryLeakFromExact"));
			int leakIntent = source.IndexOf(
				"Wear.LeakPhase = (int)KingdomWearLeakPhase.MutationIntent",
				StringComparison.Ordinal);
			int waterMutation = source.IndexOf("Survey.TryLeakFromExact(boundVessel", leakIntent,
				StringComparison.Ordinal);
			Assert.Greater(waterMutation, leakIntent);
			int proof = source.IndexOf("!LeakWorkExact(frame", waterMutation,
				StringComparison.Ordinal);
			int checkpoint = source.IndexOf("Wear.LastLeakTick = Wear.LeakToTick", proof,
				StringComparison.Ordinal);
			Assert.Greater(proof, waterMutation);
			Assert.Greater(checkpoint, proof);
			int passComplete = source.IndexOf(
				"KingdomMaterials.WriteTick(Work, SemanticPassCompletedTickProperty",
				StringComparison.Ordinal);
			int temperIncident = source.IndexOf("ApplyDamageIncident(System, Work",
				source.IndexOf("KingdomWearPassPhase.TemperIncident", StringComparison.Ordinal),
				StringComparison.Ordinal);
			Assert.Greater(passComplete, temperIncident);
		}

		[Test]
		public void SurveySource_ProvesEveryLeakAndSpoilCallbackBeforePublishingCounters()
		{
			string survey = KingdomSurveyLogicalSource.Read();
			int spoil = survey.IndexOf("public bool TrySpoilFromExact", StringComparison.Ordinal);
			int spoilEnd = survey.IndexOf("private bool PublishSpoilCounters", spoil,
				StringComparison.Ordinal);
			string spoilBody = survey.Substring(spoil, spoilEnd - spoil);
			Assert.AreEqual(1, Count(spoilBody, "food.Destroy(null, Silent: true)"));
			int destroy = spoilBody.IndexOf("food.Destroy", StringComparison.Ordinal);
			Assert.Greater(spoilBody.IndexOf("SpoilTopologyExact(frame, expected)", destroy,
				StringComparison.Ordinal), destroy);
			Assert.Greater(spoilBody.IndexOf("PublishSpoilCounters(frame, Lost)", destroy,
				StringComparison.Ordinal), destroy);
			StringAssert.Contains("ReferenceEquals(Frame.Inventory.Objects, Frame.List)", survey);
			StringAssert.Contains("item.IDIfAssigned != Frame.ItemIds[i]", survey);
			int leak = survey.IndexOf("public bool TryLeakFromExact", StringComparison.Ordinal);
			int drain = survey.IndexOf("KingdomLiquids.Drain(Store, Drams)", leak,
				StringComparison.Ordinal);
			int leakProof = survey.IndexOf("Store.ParentObject != owner", drain,
				StringComparison.Ordinal);
			int waterCounter = survey.IndexOf("StoredWater = oldStored - Drams", leakProof,
				StringComparison.Ordinal);
			Assert.Greater(drain, leak);
			Assert.Greater(leakProof, drain);
			Assert.Greater(waterCounter, leakProof);
			StringAssert.Contains("ReferenceEquals(Store.ComponentLiquids, dictionary)", survey);
			StringAssert.Contains("owner.IDIfAssigned != ownerId", survey);
		}

		[Test]
		public void RepairSource_FreezesOutboxThenInvokesPartRemovedOnceAndDispatchesAfterProof()
		{
			string source = KingdomWearLogicalSource.Read();
			Assert.AreEqual(1, Count(source, "Work.RemovePart(WearPart)"));
			int finish = source.IndexOf("private static bool FinishRepairProjection",
				StringComparison.Ordinal);
			int prepare = source.IndexOf("KingdomCeremony.PrepareWearRepaired", finish,
				StringComparison.Ordinal);
			int attempt = source.IndexOf("RepairRemovalAttemptProperty, Updated.Id", prepare,
				StringComparison.Ordinal);
			int remove = source.IndexOf("Work.RemovePart(WearPart)", attempt,
				StringComparison.Ordinal);
			int proof = source.IndexOf("RepairRemovalProofProperty, Updated.Id", remove,
				StringComparison.Ordinal);
			int complete = source.IndexOf("KingdomConstruction.Complete(ref Updated)", proof,
				StringComparison.Ordinal);
			int dispatch = source.IndexOf("KingdomCeremony.DispatchPending(System, ref Updated)",
				complete, StringComparison.Ordinal);
			Assert.Greater(prepare, finish);
			Assert.Greater(attempt, prepare);
			Assert.Greater(remove, attempt);
			Assert.Greater(proof, remove);
			Assert.Greater(complete, proof);
			Assert.Greater(dispatch, complete);
			StringAssert.Contains("A repair part-removal callback was interrupted and will not be repeated",
				source);
			StringAssert.Contains("MarkRepairRemovalLost", source);
			Assert.IsFalse(source.Contains(
				"KingdomChronicle.Record(System, line, Accomplishment: true)"));
		}

		[Test]
		public void WearOptionSource_FreezesClocksAndReanchorsWithoutBacklog()
		{
			string source = KingdomWearLogicalSource.Read();
			StringAssert.Contains("if (!Enabled)", source);
			StringAssert.Contains("AnchorDisabledClocks(System, Z, Survey, now)", source);
			StringAssert.Contains("AnchorReenabledClocks(System, Z, Survey, now)", source);
			StringAssert.Contains("wear.LastLeakTick = Now", source);
			StringAssert.Contains("KingdomMaterials.WriteTick(work, RepairWorkedProperty, Now)",
				source);
			StringAssert.Contains("ResolveSafeReceipts(System, Survey, work)", source);
		}

		[Test]
		public void WearSource_SplitPreservesPartAbiAndAuthorityOrder()
		{
			string source = KingdomWearLogicalSource.Read();
			Assert.AreEqual(1, Count(source, "[Serializable]"));
			Assert.AreEqual(1, Count(source, "public class r_KingdomWear : IPart"));
			Assert.AreEqual(1, Count(source,
				"public override void Write(GameObject Basis, SerializationWriter Writer)"));
			Assert.AreEqual(1, Count(source,
				"public override void Read(GameObject Basis, SerializationReader Reader)"));
			Assert.AreEqual(1, Count(source, "private sealed class RepairTargetFrame"));
			Assert.AreEqual(1, Count(source, "private sealed class LeakWorkFrame"));
			Assert.AreEqual(14, Count(source, "public static partial class KingdomWear"));
			AssertOrdered(source, new string[]
			{
				"public int Wear;",
				"public int LastCause;",
				"public bool Held;",
				"public int RepairEffortLeft;",
				"public long LastLeakTick;",
				"public bool LeakAnnounced;",
				"public int AnnouncedBlock;",
				"public bool LifecycleQuarantined;",
				"public string QuarantineReason;",
				"public string IncidentId;",
				"public string LastCompletedIncidentId;",
				"public bool LeakClockInitialized;",
				"public string LeakIncidentId;",
				"public string LeakItemAllocations;",
				"public int LeakLedgerState;",
				"public int LeakMessageState;",
				"public override void Write(GameObject Basis, SerializationWriter Writer)",
				"public override void Read(GameObject Basis, SerializationReader Reader)"
			});
			AssertOrdered(source, new string[]
			{
				"internal static void RetryConstruction",
				"internal static void InspectConstruction",
				"private sealed class RepairTargetFrame",
				"public static bool CanCarryStableState",
				"public const string HardRunStreakProperty",
				"public static void OnZoneActivated",
				"private static void Resolve(",
				"private static void RollWear",
				"public static void OnRaidDamage",
				"private static bool ApplyDamageIncident",
				"private static void Leak(",
				"private sealed class LeakWorkFrame",
				"private static void ContinueBoundLeak",
				"private static void ContinueLeakOutputs",
				"private static void QuarantineLeak",
				"private static void StartRepair",
				"private static bool ProjectRepair",
				"private static void AdvanceRepair",
				"private static bool FinishRepairProjection",
				"private static KingdomWearRules.LeakKind LeakKindOf"
			});
		}

		private static void AssertOrdered(string Source, string[] Needles)
		{
			int previous = -1;
			for (int i = 0; i < Needles.Length; i++)
			{
				int current = Source.IndexOf(Needles[i], previous + 1,
					StringComparison.Ordinal);
				Assert.Greater(current, previous, Needles[i]);
				previous = current;
			}
		}

		private static int Count(string Text, string Needle)
		{
			int count = 0;
			for (int at = 0; ; )
			{
				at = Text.IndexOf(Needle, at, StringComparison.Ordinal);
				if (at < 0) return count;
				count++;
				at += Needle.Length;
			}
		}
	}
}
#endif
