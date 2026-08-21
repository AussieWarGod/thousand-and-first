#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.Kernel;
using Quarters = ThousandAndFirst.KingdomLodgingRules.Closeness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Conversion (Addendum 5): the two passive channels, and the guard that keeps them from ever
	/// becoming compulsion. Every ladder rung, every boundary, every named line and the kernel
	/// draw's determinism are asserted directly, because the failure mode of this system is not a
	/// crash &mdash; it is a settlement that quietly converts people faster than it should, or one
	/// that quietly walks them out of town, and neither shows up as an exception.
	/// </summary>
	public class KingdomConversionRulesTests
	{
		private const string City = "taf:settlement:test-city";

		private static Dictionary<string, int> Counts(params object[] Pairs)
		{
			Dictionary<string, int> counts = new Dictionary<string, int>();
			for (int i = 0; i + 1 < Pairs.Length; i += 2)
			{
				counts[(string)Pairs[i]] = (int)Pairs[i + 1];
			}
			return counts;
		}

		// --- The closeness ladder: how much shared living a roof buys in one attended pass ------

		[TestCase(Quarters.Packed, KingdomConversionRules.PackedSharedPerDay)]
		[TestCase(Quarters.Close, KingdomConversionRules.CloseSharedPerDay)]
		[TestCase(Quarters.Roomed, KingdomConversionRules.RoomedSharedPerDay)]
		[TestCase(Quarters.Private, KingdomConversionRules.PrivateSharedPerDay)]
		public void SharedLivingPerDay_ReadsTheLadderAtZeroHostility(Quarters quarters, int expected)
		{
			Assert.AreEqual(expected, KingdomConversionRules.SharedLivingPerDay(quarters, 0));
		}

		[Test]
		public void SharedLivingPerDay_PackedConvertsNobodyEvenAmongPeopleWhoAgree()
		{
			// The author's ruling, not an arithmetic consequence: one open room holds only people
			// the feelings table has nothing filed between, so there is nothing there to cross --
			// and a bunk row must never become a cheap conversion engine built on purpose.
			Assert.AreEqual(0, KingdomConversionRules.SharedLivingPerDay(Quarters.Packed, 0));
		}

		[Test]
		public void SharedLivingPerDay_TheHutIsFasterThanTheHouseAndTheHouseFasterThanTheManor()
		{
			int close = KingdomConversionRules.SharedLivingPerDay(Quarters.Close, 0);
			int roomed = KingdomConversionRules.SharedLivingPerDay(Quarters.Roomed, 0);
			int priv = KingdomConversionRules.SharedLivingPerDay(Quarters.Private, 0);
			Assert.Greater(close, roomed, "a hut converts faster than a stone house");
			Assert.Greater(roomed, priv, "a stone house converts faster than quarters of one's own");
			Assert.Greater(priv, 0, "quarters of one's own still convert, slowly");
		}

		[TestCase(Quarters.Packed)]
		[TestCase(Quarters.Close)]
		[TestCase(Quarters.Roomed)]
		[TestCase(Quarters.Private)]
		public void SharedLivingPerDay_NothingIsConvertedAcrossARefusalAtAnyRung(Quarters quarters)
		{
			int refuses = KingdomLodgingRules.RefusalHostility(quarters);
			Assert.AreEqual(0, KingdomConversionRules.SharedLivingPerDay(quarters, refuses),
				"you do not convert somebody you will not live beside");
			Assert.AreEqual(0, KingdomConversionRules.SharedLivingPerDay(quarters, 100),
				"the named fault lines convert nobody anywhere");
		}

		[Test]
		public void SharedLivingPerDay_TheStoneHouseIsWhereAnAmbientGrudgeGetsCrossed()
		{
			// Addendum 5's intended case, stated as a test: at the ambient -50 the hut refuses to
			// hold them at all, and the stone house -- the one architecture that will -- is the one
			// that does the work.
			Assert.AreEqual(0, KingdomConversionRules.SharedLivingPerDay(Quarters.Close, KingdomLodgingRules.CloseRefusalHostility));
			Assert.Greater(KingdomConversionRules.SharedLivingPerDay(Quarters.Roomed, KingdomLodgingRules.CloseRefusalHostility), 0);
		}

		// --- The meal: small, and capped -------------------------------------------------------

		[Test]
		public void MealCeiling_IsShortOfTheRoadSoMealsAloneNeverConvertAnybody()
		{
			Assert.Less(KingdomConversionRules.MealCeiling, KingdomConversionRules.SharedLivingForConversion,
				"culture nudges; architecture converts");
			Assert.Greater(KingdomConversionRules.MealCeiling, 0);
		}

		[Test]
		public void MealSharedFor_GivesTheWholeNudgeWhileThereIsRoom()
		{
			Assert.AreEqual(KingdomConversionRules.MealShared, KingdomConversionRules.MealSharedFor(0));
			Assert.AreEqual(KingdomConversionRules.MealShared, KingdomConversionRules.MealSharedFor(-5), "a negative reads as none");
		}

		[Test]
		public void MealSharedFor_ClampsTheLastMealToLandExactlyOnTheCeiling()
		{
			int justUnder = KingdomConversionRules.MealCeiling - 1;
			Assert.AreEqual(1, KingdomConversionRules.MealSharedFor(justUnder));
			Assert.AreEqual(KingdomConversionRules.MealCeiling, justUnder + KingdomConversionRules.MealSharedFor(justUnder));
		}

		[Test]
		public void MealSharedFor_GivesNothingAtOrPastTheCeiling()
		{
			Assert.AreEqual(0, KingdomConversionRules.MealSharedFor(KingdomConversionRules.MealCeiling));
			Assert.AreEqual(0, KingdomConversionRules.MealSharedFor(KingdomConversionRules.SharedLivingForConversion));
		}

		[Test]
		public void MealSharedFor_EatingEveryNightStallsAtTheCeilingAndNeverReachesTheRoadsEnd()
		{
			int shared = 0;
			for (int meal = 0; meal < 500; meal++)
			{
				shared += KingdomConversionRules.MealSharedFor(shared);
			}
			Assert.AreEqual(KingdomConversionRules.MealCeiling, shared);
			Assert.IsFalse(KingdomConversionRules.AtMilestone(shared), "no settlement eats its way to a conversion");
		}

		// --- The household majority ------------------------------------------------------------

		[Test]
		public void HouseholdMajority_NeedsAStrictMajorityOfEverybodyUnderTheRoof()
		{
			Assert.AreEqual("Barathrumites", KingdomConversionRules.HouseholdMajority(Counts("Barathrumites", 2), 3));
			Assert.IsNull(KingdomConversionRules.HouseholdMajority(Counts("Barathrumites", 2), 4), "half is not a majority");
			Assert.AreEqual("Barathrumites", KingdomConversionRules.HouseholdMajority(Counts("Barathrumites", 3), 4));
		}

		[Test]
		public void HouseholdMajority_APluralityShortOfHalfPullsNobody()
		{
			// Three creeds in a house of six: the largest is not a majority, and a house that is
			// merely mixed pulls in no direction at all.
			Assert.IsNull(KingdomConversionRules.HouseholdMajority(Counts("Templar", 2, "Barathrumites", 2, "Joppa", 1), 6));
		}

		[Test]
		public void HouseholdMajority_CountsTheCreedlessInTheDenominator()
		{
			// Two believers and three ordinary settlers is not a household with a creed, however
			// loud the two are.
			Assert.IsNull(KingdomConversionRules.HouseholdMajority(Counts("Templar", 2), 5));
		}

		[Test]
		public void HouseholdMajority_ATieHasNoWinner()
		{
			Assert.IsNull(KingdomConversionRules.HouseholdMajority(Counts("Templar", 2, "Barathrumites", 2), 4));
		}

		[Test]
		public void HouseholdMajority_NullEmptyAndNonPositiveEntriesAllReadAsNobody()
		{
			Assert.IsNull(KingdomConversionRules.HouseholdMajority(null, 4));
			Assert.IsNull(KingdomConversionRules.HouseholdMajority(new Dictionary<string, int>(), 4));
			Assert.IsNull(KingdomConversionRules.HouseholdMajority(Counts("Templar", 0), 0));
			Assert.IsNull(KingdomConversionRules.HouseholdMajority(Counts("Templar", 3), 0), "no household, no majority");
		}

		// --- Progress: the tug of war ------------------------------------------------------------

		[Test]
		public void Progress_NoneIsEmptyAndNegativeSharedClampsToNothing()
		{
			Assert.IsFalse(ConversionProgress.None.Any);
			Assert.IsNull(ConversionProgress.None.Creed);
			Assert.AreEqual(0, new ConversionProgress("Templar", -9).Shared);
			Assert.IsNull(new ConversionProgress("", 12).Creed);
			Assert.AreEqual(0, new ConversionProgress("", 12).Shared, "points toward nothing are no points");
		}

		[Test]
		public void Advance_StartsAFreshPullWhenNothingWasWorkingOnThem()
		{
			ConversionProgress after = KingdomConversionRules.Advance(ConversionProgress.None, "Templar", 3);
			Assert.AreEqual("Templar", after.Creed);
			Assert.AreEqual(3, after.Shared);
		}

		[Test]
		public void Advance_AccumulatesWhenThePullNamesTheSameCreed()
		{
			ConversionProgress after = KingdomConversionRules.Advance(new ConversionProgress("Templar", 10), "Templar", 3);
			Assert.AreEqual("Templar", after.Creed);
			Assert.AreEqual(13, after.Shared);
		}

		[Test]
		public void Advance_ASecondCreedTakesPointsOffTheFirstRatherThanStartingItsOwn()
		{
			// A citizen who sleeps in a Barathrumite house and eats at a Templar table converts to
			// neither. This is the whole reason the meal can erode a quarter's grip on its own.
			ConversionProgress after = KingdomConversionRules.Advance(new ConversionProgress("Barathrumites", 10), "Templar", 4);
			Assert.AreEqual("Barathrumites", after.Creed, "the contest does not change hands mid-tug");
			Assert.AreEqual(6, after.Shared);
		}

		[Test]
		public void Advance_WhenTwoPullsCancelTheSlotIsFreeAndTheRemainderIsDiscarded()
		{
			ConversionProgress after = KingdomConversionRules.Advance(new ConversionProgress("Barathrumites", 4), "Templar", 9);
			Assert.IsFalse(after.Any);
			Assert.IsNull(after.Creed, "winning a tug of war does not happen in the pass you win it");
			Assert.AreEqual(0, after.Shared);
		}

		[TestCase(null, 5)]
		[TestCase("", 5)]
		[TestCase("Templar", 0)]
		[TestCase("Templar", -3)]
		public void Advance_ANonPullChangesNothing(string creed, int points)
		{
			ConversionProgress before = new ConversionProgress("Barathrumites", 11);
			ConversionProgress after = KingdomConversionRules.Advance(before, creed, points);
			Assert.AreEqual(before.Creed, after.Creed);
			Assert.AreEqual(before.Shared, after.Shared);
		}

		// --- Milestones --------------------------------------------------------------------------

		[Test]
		public void AtMilestone_IsFalseOnePointShortAndTrueOnTheNumberItself()
		{
			Assert.IsFalse(KingdomConversionRules.AtMilestone(KingdomConversionRules.SharedLivingForConversion - 1));
			Assert.IsTrue(KingdomConversionRules.AtMilestone(KingdomConversionRules.SharedLivingForConversion));
		}

		[Test]
		public void Milestone_HoldsSteadyBetweenOneMilestoneAndTheNext()
		{
			int road = KingdomConversionRules.SharedLivingForConversion;
			Assert.AreEqual(0uL, KingdomConversionRules.Milestone(road - 1));
			Assert.AreEqual(1uL, KingdomConversionRules.Milestone(road));
			Assert.AreEqual(1uL, KingdomConversionRules.Milestone((2 * road) - 1), "a milestone that said no is not re-asked until a whole further road is walked");
			Assert.AreEqual(2uL, KingdomConversionRules.Milestone(2 * road));
		}

		[Test]
		public void SharedLivingForConversion_IsASeasonOfSharedLivingAndNotAWeekOfIt()
		{
			// The fastest rung there is, expressed in the unit the road is now walked in: days
			// actually lived under one roof. A number small enough to be a week would make
			// conversion a schedule instead of a chronicle entry.
			int fastest = KingdomConversionRules.SharedLivingPerDay(Quarters.Close, 0);
			int days = KingdomConversionRules.SharedLivingForConversion / fastest;
			Assert.GreaterOrEqual(days, 60, "a conversion is a season of shared living");
		}

		// --- The recalibration: passes to cohabitation days, at the same pace ---------------------

		[Test]
		public void EveryRungLandsOnTheExactWallClockDistanceItAlwaysHad()
		{
			// The equivalence that makes this a change of UNIT rather than a change of pace. The
			// per-rung rates did not move -- they were per attended pass and are now per day --
			// and the road moved by exactly the cadence, so a founder who comes home every third
			// day walks the identical seventy-two, hundred and eight, and two hundred and sixteen
			// days they always walked. If any of the four numbers drifts, this fails.
			int cadence = KingdomBrinkRules.CohabitationDaysPerAttendedPass;
			Assert.AreEqual(72, KingdomConversionRules.SharedLivingInPasses);
			Assert.AreEqual(KingdomConversionRules.SharedLivingInPasses * cadence, KingdomConversionRules.SharedLivingForConversion);
			foreach (Quarters rung in new Quarters[3] { Quarters.Close, Quarters.Roomed, Quarters.Private })
			{
				int perDay = KingdomConversionRules.SharedLivingPerDay(rung, 0);
				int oldPasses = KingdomConversionRules.SharedLivingInPasses / perDay;
				int newDays = KingdomConversionRules.SharedLivingForConversion / perDay;
				Assert.AreEqual(oldPasses * cadence, newDays, rung + " changed length across the migration");
			}
		}

		[Test]
		public void NineMealsStillFillTheCeilingSoCultureDidNotSilentlyTripleInCost()
		{
			// A meal is an EVENT and needed no recalibrating for its own sake, but what a meal is
			// WORTH relative to the road did: leaving MealShared at four against a road three
			// times longer would have tripled the suppers culture costs without anybody deciding
			// to. Nine before, nine now.
			Assert.AreEqual(KingdomConversionRules.MealSharedInPasses * KingdomBrinkRules.CohabitationDaysPerAttendedPass,
				KingdomConversionRules.MealShared);
			int meals = 0;
			int shared = 0;
			while (KingdomConversionRules.MealSharedFor(shared) > 0)
			{
				shared += KingdomConversionRules.MealSharedFor(shared);
				meals++;
				Assert.Less(meals, 500, "the ceiling must terminate");
			}
			Assert.AreEqual(9, meals);
			Assert.AreEqual(KingdomConversionRules.MealCeiling, shared);
		}

		// --- Rule 1: the road ends at a brink, and nothing accrues past it -----------------------

		[Test]
		public void AdvanceOverDays_ATenDayAbsenceAndAThousandDayOneArriveAtTheSamePlace()
		{
			int perDay = KingdomConversionRules.SharedLivingPerDay(Quarters.Close, 0);
			ConversionProgress ten = KingdomConversionRules.AdvanceOverDays(ConversionProgress.None, "Barathrumites", perDay, 1000);
			ConversionProgress aThousand = KingdomConversionRules.AdvanceOverDays(ConversionProgress.None, "Barathrumites", perDay, 100000);
			Assert.AreEqual(KingdomConversionRules.SharedLivingForConversion, ten.Shared);
			Assert.AreEqual(ten.Shared, aThousand.Shared, "nothing accrues past the road's end");
			Assert.AreEqual("Barathrumites", aThousand.Creed);
		}

		[Test]
		public void AdvanceOverDays_CreditsExactlyTheDaysLivedWhileShortOfTheRoadsEnd()
		{
			int perDay = KingdomConversionRules.SharedLivingPerDay(Quarters.Roomed, 0);
			ConversionProgress after = KingdomConversionRules.AdvanceOverDays(ConversionProgress.None, "Barathrumites", perDay, 10);
			Assert.AreEqual(perDay * 10, after.Shared);
			Assert.IsFalse(KingdomConversionRules.AtMilestone(after.Shared));
		}

		[Test]
		public void AdvanceOverDays_ANonPositiveStretchOrRateChangesNothing()
		{
			ConversionProgress held = new ConversionProgress("Barathrumites", 40);
			Assert.AreEqual(40, KingdomConversionRules.AdvanceOverDays(held, "Barathrumites", 3, 0).Shared);
			Assert.AreEqual(40, KingdomConversionRules.AdvanceOverDays(held, "Barathrumites", 0, 90).Shared,
				"a bunk row buys nothing however many days pass in it");
			Assert.AreEqual(40, KingdomConversionRules.AdvanceOverDays(held, null, 3, 90).Shared);
		}

		[Test]
		public void AdvanceOverDays_ACounterPullTakesPointsOffAndIsHowABrinkIsArrested()
		{
			// The arrest channel Addendum 5 names: a settler standing at the end of one road who
			// is pulled the other way is no longer at its end, and the shell lifts their brink on
			// exactly that test.
			ConversionProgress atTheEnd = new ConversionProgress("Barathrumites", KingdomConversionRules.SharedLivingForConversion);
			Assert.IsTrue(KingdomConversionRules.AtMilestone(atTheEnd.Shared));
			ConversionProgress pulledBack = KingdomConversionRules.AdvanceOverDays(atTheEnd, "Templar", 3, 10);
			Assert.AreEqual("Barathrumites", pulledBack.Creed, "winning a tug of war does not happen in the pass you win it");
			Assert.IsFalse(KingdomConversionRules.AtMilestone(pulledBack.Shared), "and they are no longer at a brink");
		}

		[Test]
		public void AFullCityDoesNotMassConvertOnTheFirstUncappedPass()
		{
			// The exact scenario the clock-rework audit feared: uncap osmosis, come home from a
			// season away, and find the city's whole minority converted in one pass. Twelve
			// settlers, every one of them in a hut with a majority pulling at them, a thousand
			// days of absence. Every one of them reaches the END OF THE ROAD -- clause 1 says
			// people go on living together -- and NOT ONE of them converts, because the road ends
			// at a brink and the brink is spent in attended passes the founder has to be there
			// for. Six of them, by name, with the honest number of days, and any of them
			// arrestable by breaking the household up.
			const int city = 12;
			int perDay = KingdomConversionRules.SharedLivingPerDay(Quarters.Close, 0);
			int atTheRoadsEnd = 0;
			for (int i = 0; i < city; i++)
			{
				ConversionProgress after = KingdomConversionRules.AdvanceOverDays(ConversionProgress.None, "Barathrumites", perDay, 1000);
				Assert.AreEqual(KingdomConversionRules.SharedLivingForConversion, after.Shared, "held at the road's end and no further");
				if (KingdomConversionRules.AtMilestone(after.Shared))
				{
					atTheRoadsEnd++;
				}
			}
			Assert.AreEqual(city, atTheRoadsEnd, "a thousand days under one roof really does walk the whole road");
			// And that is a brink, not a conversion: the whole window has to run out after the
			// warning before a single draw is asked.
			Assert.Greater(KingdomBrinkRules.CreedBrinkWindowDays, 0);
			long told = 500L * KingdomRules.TicksPerDay;
			Assert.IsFalse(KingdomBrinkRules.WindowSpent(BrinkKind.Creed, told, told),
				"the day the city is told is not the day the city turns");
		}

		// --- The road ordinal: counted, because progress no longer divides ------------------------

		[TestCase(-3, 1)]
		[TestCase(0, 1)]
		[TestCase(1, 2)]
		[TestCase(7, 8)]
		public void RoadEnd_StandsAtTheOrdinalTheDrawIsKeyedOn(int walked, int ordinal)
		{
			Assert.AreEqual((ulong)ordinal, KingdomConversionRules.Milestone(KingdomConversionRules.RoadEnd(walked)));
		}

		[Test]
		public void RoadEnd_TheFirstRoadStillDrawsOnOrdinalOneSoNoPendingAnswerWasReRolled()
		{
			// Progress now holds at the road's end, so the ordinal is counted rather than divided
			// out of it. The counting had to land on the same numbers the dividing did, or every
			// soul standing unconverted in every save would have been re-asked.
			Assert.AreEqual(KingdomConversionRules.SharedLivingForConversion, KingdomConversionRules.RoadEnd(0));
			Assert.AreEqual(1uL, KingdomConversionRules.Milestone(KingdomConversionRules.RoadEnd(0)));
			Assert.IsTrue(KingdomConversionRules.AtMilestone(KingdomConversionRules.RoadEnd(0)));
		}

		[Test]
		public void RoadEnd_ARoadThatAnsweredNoIsANewQuestionAtTheNext()
		{
			bool differed = false;
			for (int i = 0; i < 60 && !differed; i++)
			{
				string name = "settler-" + i;
				differed = KingdomConversionRules.Converts(City, ConversionChannel.Osmosis, name, KingdomConversionRules.RoadEnd(0))
					!= KingdomConversionRules.Converts(City, ConversionChannel.Osmosis, name, KingdomConversionRules.RoadEnd(1));
			}
			Assert.IsTrue(differed, "a settler who walked a whole road and did not turn must get a genuinely new question");
		}

		[Test]
		public void RoadEnd_AnswersTheSameWayEveryTimeItIsAskedHoweverManyRoadsHaveBeenWalked()
		{
			for (int road = 0; road < 6; road++)
			{
				for (int i = 0; i < 12; i++)
				{
					string name = "settler-" + i;
					bool first = KingdomConversionRules.Converts(City, ConversionChannel.Osmosis, name, KingdomConversionRules.RoadEnd(road));
					bool second = KingdomConversionRules.Converts(City, ConversionChannel.Osmosis, name, KingdomConversionRules.RoadEnd(road));
					Assert.AreEqual(first, second, "a reload must never re-roll a soul");
				}
			}
		}

		// --- The draw: rare, and never re-rolled -------------------------------------------------

		[Test]
		public void Converts_IsFalseShortOfTheFirstMilestoneHoweverCloseTheyAre()
		{
			Assert.IsFalse(KingdomConversionRules.Converts(City, ConversionChannel.Osmosis, "Dagasha",
				KingdomConversionRules.SharedLivingForConversion - 1));
		}

		[Test]
		public void Converts_AnswersTheSameWayEveryTimeItIsAsked()
		{
			int road = KingdomConversionRules.SharedLivingForConversion;
			for (int i = 0; i < 40; i++)
			{
				string name = "settler-" + i;
				bool first = KingdomConversionRules.Converts(City, ConversionChannel.Osmosis, name, road);
				bool second = KingdomConversionRules.Converts(City, ConversionChannel.Osmosis, name, road);
				Assert.AreEqual(first, second, "a reload must never re-roll a soul");
			}
		}

		[Test]
		public void Converts_FailsClosedWhenTheKernelRefusesTheKey()
		{
			// A malformed settlement id, or a machine whose crypto provider is failing, must never
			// be able to change what somebody believes.
			Assert.IsFalse(KingdomConversionRules.Converts("not a taf id", ConversionChannel.Osmosis, "Dagasha",
				KingdomConversionRules.SharedLivingForConversion));
			Assert.IsFalse(KingdomConversionRules.Converts(null, ConversionChannel.Osmosis, "Dagasha",
				KingdomConversionRules.SharedLivingForConversion));
		}

		[Test]
		public void Converts_TurnsRoughlyTheStatedShareOfPeopleWhoReachAMilestone()
		{
			int road = KingdomConversionRules.SharedLivingForConversion;
			int turned = 0;
			const int sample = 400;
			for (int i = 0; i < sample; i++)
			{
				if (KingdomConversionRules.Converts(City, ConversionChannel.Osmosis, "settler-" + i, road))
				{
					turned++;
				}
			}
			int percent = turned * 100 / sample;
			Assert.Greater(turned, 0, "a road nobody ever reaches the end of is not a road");
			Assert.Less(turned, sample, "reaching a milestone buys a draw, not a conversion");
			Assert.GreaterOrEqual(percent, KingdomConversionRules.ConversionChancePercent - 10);
			Assert.LessOrEqual(percent, KingdomConversionRules.ConversionChancePercent + 10);
		}

		[Test]
		public void Converts_AMilestoneThatSaidNoIsANewQuestionAtTheNext()
		{
			int road = KingdomConversionRules.SharedLivingForConversion;
			bool differed = false;
			for (int i = 0; i < 60 && !differed; i++)
			{
				string name = "settler-" + i;
				differed = KingdomConversionRules.Converts(City, ConversionChannel.Osmosis, name, road)
					!= KingdomConversionRules.Converts(City, ConversionChannel.Osmosis, name, road * 2);
			}
			Assert.IsTrue(differed, "the ordinal must actually reach the draw");
		}

		[Test]
		public void Converts_EachChannelDrawsOnItsOwnLane()
		{
			int road = KingdomConversionRules.SharedLivingForConversion;
			bool differed = false;
			for (int i = 0; i < 60 && !differed; i++)
			{
				string name = "settler-" + i;
				differed = KingdomConversionRules.Converts(City, ConversionChannel.Osmosis, name, road)
					!= KingdomConversionRules.Converts(City, ConversionChannel.Culture, name, road);
			}
			Assert.IsTrue(differed, "the channel must actually reach the draw");
		}

		[Test]
		public void Converts_TwoSettlersAtTheSameMilestoneAreNotForcedToShareOneAnswer()
		{
			int road = KingdomConversionRules.SharedLivingForConversion;
			bool differed = false;
			for (int i = 0; i < 60 && !differed; i++)
			{
				differed = KingdomConversionRules.Converts(City, ConversionChannel.Osmosis, "settler-" + i, road)
					!= KingdomConversionRules.Converts(City, ConversionChannel.Osmosis, "other-" + i, road);
			}
			Assert.IsTrue(differed, "the person must actually reach the draw");
		}

		[Test]
		public void Converts_TwoSettlementsDrawIndependently()
		{
			int road = KingdomConversionRules.SharedLivingForConversion;
			bool differed = false;
			for (int i = 0; i < 60 && !differed; i++)
			{
				string name = "settler-" + i;
				differed = KingdomConversionRules.Converts(City, ConversionChannel.Osmosis, name, road)
					!= KingdomConversionRules.Converts("taf:settlement:other-city", ConversionChannel.Osmosis, name, road);
			}
			Assert.IsTrue(differed, "the settlement must actually reach the draw");
		}

		// --- The stream id fold ------------------------------------------------------------------

		[TestCase("Dagasha")]
		[TestCase("Ptoh of Ezra")]
		[TestCase("")]
		[TestCase(null)]
		[TestCase("Q'uuér!! the ::Thrice:: Named")]
		public void ResidentStream_AlwaysSatisfiesTheFrozenSemanticIdGrammar(string name)
		{
			Assert.IsTrue(KernelSemanticId.IsValid(KingdomConversionRules.ResidentStream(name)),
				"a name the kernel refuses would silently cost that settler every draw they ever earn");
		}

		[Test]
		public void ResidentStream_FoldsALongNameDownRatherThanOverflowingTheGrammar()
		{
			string long_ = new string('x', 400);
			Assert.IsTrue(KernelSemanticId.IsValid(KingdomConversionRules.ResidentStream(long_)));
		}

		[Test]
		public void ResidentStream_GivesDifferentPeopleDifferentLanes()
		{
			Assert.AreNotEqual(KingdomConversionRules.ResidentStream("Dagasha"), KingdomConversionRules.ResidentStream("Ptoh"));
		}

		// --- The exit: which channels impose, and who resents them --------------------------------

		[TestCase(ConversionChannel.Shrine, true)]
		[TestCase(ConversionChannel.Osmosis, false)]
		[TestCase(ConversionChannel.Culture, false)]
		[TestCase(ConversionChannel.Diplomacy, false)]
		public void IsImposed_OnlyTheShrinePressesACreedOnPeopleWhoDidNotAskForIt(ConversionChannel channel, bool imposed)
		{
			// Osmosis and the table are chosen proximity -- a household that could push somebody
			// out for living in it would make the healing arc into the thing it was written
			// against. Diplomacy is invited and consented to, one at a time.
			Assert.AreEqual(imposed, KingdomConversionRules.IsImposed(channel));
		}

		[Test]
		public void Resents_BitesAtTheAmbientGrudgeAndNotOnePointBelowIt()
		{
			Assert.IsFalse(KingdomConversionRules.Resents(KingdomConversionRules.ResentmentHostility - 1));
			Assert.IsTrue(KingdomConversionRules.Resents(KingdomConversionRules.ResentmentHostility));
			Assert.IsTrue(KingdomConversionRules.Resents(100));
		}

		[Test]
		public void Resents_NobodyResentsACreedTheEngineHasNoQuarrelWith()
		{
			// A creedless settler and a settler who already holds the imposed creed both read zero
			// from KingdomCreed.HostilityBetween, so neither is ever walked toward the road.
			Assert.IsFalse(KingdomConversionRules.Resents(0));
		}

		[Test]
		public void ResentmentRunOut_FiresExactlyOnTheStatedDayAndNotOneEarlier()
		{
			const int told = 400;
			Assert.IsFalse(KingdomConversionRules.ResentmentRunOut(told, told), "the day the word goes out is not the day they go");
			Assert.IsFalse(KingdomConversionRules.ResentmentRunOut(told, told + KingdomConversionRules.ResentedWindowDays - 1));
			Assert.IsTrue(KingdomConversionRules.ResentmentRunOut(told, told + KingdomConversionRules.ResentedWindowDays));
			Assert.IsTrue(KingdomConversionRules.ResentmentRunOut(told, told + 900), "and it stays spent");
		}

		[Test]
		public void ResentmentNeverRunsOutForSomebodyTheFounderWasNeverWarnedAbout()
		{
			// Addendum 10(a): presence stopped being the shield and ignorance became it. An entry
			// that has never carried a warning day has no deadline at all.
			Assert.IsFalse(KingdomConversionRules.ResentmentRunOut(KingdomConversionRules.NotWarned, 9000));
			Assert.AreEqual(KingdomConversionRules.ResentedWindowDays,
				KingdomConversionRules.ResentmentDaysLeft(KingdomConversionRules.NotWarned, 9000),
				"and the whole window is still in front of the founder on the day they are told");
		}

		[Test]
		public void ResentmentDaysLeft_CountsDownToZeroAndStops()
		{
			const int told = 400;
			Assert.AreEqual(KingdomConversionRules.ResentedWindowDays, KingdomConversionRules.ResentmentDaysLeft(told, told));
			Assert.AreEqual(1, KingdomConversionRules.ResentmentDaysLeft(told, told + KingdomConversionRules.ResentedWindowDays - 1));
			Assert.AreEqual(0, KingdomConversionRules.ResentmentDaysLeft(told, told + KingdomConversionRules.ResentedWindowDays));
			Assert.AreEqual(0, KingdomConversionRules.ResentmentDaysLeft(told, told + 900), "never negative");
		}

		[Test]
		public void TheWindowIsLongerThanTheHousingWindowBecauseACreedIsNotARoof()
		{
			Assert.Greater(KingdomConversionRules.ResentedWindowDays, KingdomLodgingRules.GraceDays);
			Assert.AreEqual(KingdomBrinkRules.CreedBrinkWindowDays, KingdomConversionRules.ResentedWindowDays,
				"the two ways a settler can be one window from losing their creed must not drift apart");
		}

		[Test]
		public void AbsenceSpendsTheResentedWindowBecauseTheWordWasPushedNotLeftAtTheSeat()
		{
			// The founder is told on the road that a creed is being forced on somebody, and never
			// comes back. Eighteen days later that settler takes the road, exactly as the warning
			// said they would.
			const int told = 400;
			int passes = 0;
			int day = told;
			while (!KingdomConversionRules.ResentmentRunOut(told, day))
			{
				day++;
				passes++;
				Assert.Less(passes, 500, "the window must terminate");
			}
			Assert.AreEqual(KingdomConversionRules.ResentedWindowDays, passes,
				"a whole window of world-days after the day the word went out");
		}

		// --- Prose: two registers that disagree where the day is contested -------------------------

		[Test]
		public void Contested_BitesWhereTheWorldWouldActuallyArgue()
		{
			Assert.IsFalse(KingdomConversionRules.Contested(KingdomConversionRules.ContestedHostility - 1));
			Assert.IsTrue(KingdomConversionRules.Contested(KingdomConversionRules.ContestedHostility));
			Assert.IsFalse(KingdomConversionRules.Contested(0), "nobody contests a conversion from nothing");
		}

		[TestCase(ConversionChannel.Osmosis)]
		[TestCase(ConversionChannel.Culture)]
		[TestCase(ConversionChannel.Shrine)]
		[TestCase(ConversionChannel.Diplomacy)]
		public void ConversionTelling_NamesThePersonAndTheCreedInEveryChannel(ConversionChannel channel)
		{
			string line = KingdomConversionRules.ConversionTelling(channel, "Dagasha", "the Barathrumites");
			StringAssert.Contains("Dagasha", line);
			StringAssert.Contains("the Barathrumites", line);
			Assert.IsFalse(line.EndsWith("."), "the chronicle dates the clause and closes it");
		}

		[TestCase(ConversionChannel.Osmosis)]
		[TestCase(ConversionChannel.Culture)]
		[TestCase(ConversionChannel.Shrine)]
		[TestCase(ConversionChannel.Diplomacy)]
		public void ConversionRumour_TellsADifferentStoryThanTheFoundersBook(ConversionChannel channel)
		{
			string official = KingdomConversionRules.ConversionTelling(channel, "Dagasha", "the Barathrumites");
			string rumour = KingdomConversionRules.ConversionRumour(channel, "Dagasha", "the Barathrumites");
			StringAssert.Contains("Dagasha", rumour);
			Assert.AreNotEqual(official, rumour, "the rumour register is a rival to the founder's account, not a translation of it");
		}

		[Test]
		public void ConversionTellings_UseTheChannelsOwnWordsRatherThanOneSharedSentence()
		{
			string osmosis = KingdomConversionRules.ConversionTelling(ConversionChannel.Osmosis, "Dagasha", "the Barathrumites");
			string shrine = KingdomConversionRules.ConversionTelling(ConversionChannel.Shrine, "Dagasha", "the Barathrumites");
			string rite = KingdomConversionRules.ConversionTelling(ConversionChannel.Diplomacy, "Dagasha", "the Barathrumites");
			Assert.AreNotEqual(osmosis, shrine);
			Assert.AreNotEqual(shrine, rite);
			StringAssert.Contains("water rite", rite, "the founder's own rite is named for what it is");
		}

		[Test]
		public void ProseSurvivesAnUnnamedSettlerAndAnUnresolvableCreed()
		{
			// A creed recorded in a save can outlive the faction that named it, and a founding
			// citizen may carry no roll name. Neither may produce a line with a hole in it.
			string telling = KingdomConversionRules.ConversionTelling(ConversionChannel.Osmosis, null, null);
			string rumour = KingdomConversionRules.ConversionRumour(ConversionChannel.Shrine, "", "");
			Assert.IsFalse(string.IsNullOrEmpty(telling));
			Assert.IsFalse(string.IsNullOrEmpty(rumour));
			StringAssert.Contains("a settler", telling);
			StringAssert.Contains("a settler", rumour);
		}

		[Test]
		public void PressureLines_NameThePersonTheCreedAndWhatTheFounderCanDoAboutIt()
		{
			string telling = KingdomConversionRules.PressureTelling("Dagasha", "the Templar");
			string note = KingdomConversionRules.PressureNote("Dagasha", "the Templar");
			StringAssert.Contains("Dagasha", telling);
			StringAssert.Contains("the Templar", telling);
			StringAssert.Contains("Dagasha", note);
			StringAssert.Contains("the Templar", note);
			Assert.AreNotEqual(telling, note, "the chronicle records; the ledger tells the founder what to do (STANDARDS 7b)");
		}

		[Test]
		public void LeavingLine_NamesThePersonAndSaysTheyAreGoing()
		{
			string line = KingdomConversionRules.LeavingLine("Dagasha");
			StringAssert.Contains("Dagasha", line);
			StringAssert.Contains("leaving", line);
			StringAssert.Contains("A settler", KingdomConversionRules.LeavingLine(null));
		}

		[Test]
		public void DepartureCause_IsTheOneClauseBothRegistersName()
		{
			Assert.AreEqual("rather than take a creed they never chose", KingdomConversionRules.DepartureCause);
			Assert.AreNotEqual(KingdomLodgingRules.DepartureCause, KingdomConversionRules.DepartureCause,
				"leaving over a creed and leaving over a roof are different departures and read differently");
		}
	}
}
#endif
