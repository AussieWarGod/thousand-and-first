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

		[TestCase(Quarters.Packed, KingdomConversionRules.PackedSharedPerPass)]
		[TestCase(Quarters.Close, KingdomConversionRules.CloseSharedPerPass)]
		[TestCase(Quarters.Roomed, KingdomConversionRules.RoomedSharedPerPass)]
		[TestCase(Quarters.Private, KingdomConversionRules.PrivateSharedPerPass)]
		public void SharedLivingPerPass_ReadsTheLadderAtZeroHostility(Quarters quarters, int expected)
		{
			Assert.AreEqual(expected, KingdomConversionRules.SharedLivingPerPass(quarters, 0));
		}

		[Test]
		public void SharedLivingPerPass_PackedConvertsNobodyEvenAmongPeopleWhoAgree()
		{
			// The author's ruling, not an arithmetic consequence: one open room holds only people
			// the feelings table has nothing filed between, so there is nothing there to cross --
			// and a bunk row must never become a cheap conversion engine built on purpose.
			Assert.AreEqual(0, KingdomConversionRules.SharedLivingPerPass(Quarters.Packed, 0));
		}

		[Test]
		public void SharedLivingPerPass_TheHutIsFasterThanTheHouseAndTheHouseFasterThanTheManor()
		{
			int close = KingdomConversionRules.SharedLivingPerPass(Quarters.Close, 0);
			int roomed = KingdomConversionRules.SharedLivingPerPass(Quarters.Roomed, 0);
			int priv = KingdomConversionRules.SharedLivingPerPass(Quarters.Private, 0);
			Assert.Greater(close, roomed, "a hut converts faster than a stone house");
			Assert.Greater(roomed, priv, "a stone house converts faster than quarters of one's own");
			Assert.Greater(priv, 0, "quarters of one's own still convert, slowly");
		}

		[TestCase(Quarters.Packed)]
		[TestCase(Quarters.Close)]
		[TestCase(Quarters.Roomed)]
		[TestCase(Quarters.Private)]
		public void SharedLivingPerPass_NothingIsConvertedAcrossARefusalAtAnyRung(Quarters quarters)
		{
			int refuses = KingdomLodgingRules.RefusalHostility(quarters);
			Assert.AreEqual(0, KingdomConversionRules.SharedLivingPerPass(quarters, refuses),
				"you do not convert somebody you will not live beside");
			Assert.AreEqual(0, KingdomConversionRules.SharedLivingPerPass(quarters, 100),
				"the named fault lines convert nobody anywhere");
		}

		[Test]
		public void SharedLivingPerPass_TheStoneHouseIsWhereAnAmbientGrudgeGetsCrossed()
		{
			// Addendum 5's intended case, stated as a test: at the ambient -50 the hut refuses to
			// hold them at all, and the stone house -- the one architecture that will -- is the one
			// that does the work.
			Assert.AreEqual(0, KingdomConversionRules.SharedLivingPerPass(Quarters.Close, KingdomLodgingRules.CloseRefusalHostility));
			Assert.Greater(KingdomConversionRules.SharedLivingPerPass(Quarters.Roomed, KingdomLodgingRules.CloseRefusalHostility), 0);
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
		public void SharedLivingForConversion_IsASeasonOfComingHomeAndNotAWeekOfIt()
		{
			// The fastest rung there is, expressed in the unit the founder actually spends:
			// attended passes. A number small enough to be a week would make conversion a
			// schedule instead of a chronicle entry.
			int fastest = KingdomConversionRules.SharedLivingPerPass(Quarters.Close, 0);
			int passes = KingdomConversionRules.SharedLivingForConversion / fastest;
			Assert.GreaterOrEqual(passes, 20, "a conversion is a season of shared living");
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
		public void ResentmentAfterPass_AnnouncesOnZeroThenCountsUpOnePassAtATime()
		{
			Assert.AreEqual(0, KingdomConversionRules.ResentmentAfterPass(KingdomConversionRules.NoResentment));
			Assert.AreEqual(1, KingdomConversionRules.ResentmentAfterPass(0));
			Assert.AreEqual(4, KingdomConversionRules.ResentmentAfterPass(3));
		}

		[Test]
		public void ResentmentRunOut_FiresExactlyOnTheStatedPassAndNotOneEarlier()
		{
			Assert.IsFalse(KingdomConversionRules.ResentmentRunOut(KingdomConversionRules.ResentedPasses - 1));
			Assert.IsTrue(KingdomConversionRules.ResentmentRunOut(KingdomConversionRules.ResentedPasses));
		}

		[Test]
		public void TheGraceIsLongerThanTheHousingGraceBecauseACreedIsNotARoof()
		{
			Assert.Greater(KingdomConversionRules.ResentedPasses, KingdomLodgingRules.GracePasses);
		}

		[Test]
		public void TheGraceCountsUpFromTheAnnouncementSoTheFounderAlwaysGetsWholePassesToAct()
		{
			int count = KingdomConversionRules.NoResentment;
			int passes = 0;
			while (!KingdomConversionRules.ResentmentRunOut(count))
			{
				count = KingdomConversionRules.ResentmentAfterPass(count);
				passes++;
			}
			Assert.AreEqual(KingdomConversionRules.ResentedPasses + 1, passes,
				"the announcing pass plus a whole grace of attended passes after it");
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
