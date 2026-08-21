#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The brink (Addendum 8 clause 3): the last arrestable window in front of every irreversible
	/// consequence in the mod. Asserted as a SHAPE rather than as three separate graces, because
	/// the whole point of moving it here was that the roof, the creed and the city cannot drift
	/// apart &mdash; a mutation to the window arithmetic must fail once, here, and not silently
	/// change what a settler's grace means while leaving a city's alone.
	/// </summary>
	public class KingdomBrinkRulesTests
	{
		private const long Day = KingdomRules.TicksPerDay;

		// --- The three windows -----------------------------------------------------------

		[TestCase(BrinkKind.Roof, KingdomBrinkRules.RoofBrinkWindow)]
		[TestCase(BrinkKind.Creed, KingdomBrinkRules.CreedBrinkWindow)]
		[TestCase(BrinkKind.City, KingdomBrinkRules.CityBrinkWindow)]
		public void WindowFor_NamesTheLengthTheOwningDesignAsksFor(BrinkKind kind, int expected)
		{
			Assert.AreEqual(expected, KingdomBrinkRules.WindowFor(kind));
		}

		[Test]
		public void TheWindowsAreTwoSixAndThreeAndEachIsTheLengthItsDesignArguedFor()
		{
			Assert.AreEqual(2, KingdomBrinkRules.RoofBrinkWindow, "a roof is tonight's problem");
			Assert.AreEqual(6, KingdomBrinkRules.CreedBrinkWindow, "a creed is a life's");
			Assert.AreEqual(3, KingdomBrinkRules.CityBrinkWindow, "a city is three visits, one rung under the rupture span");
		}

		[Test]
		public void EveryWindowIsPositiveSoNoBrinkFiresOnThePassItIsAnnouncedOn()
		{
			foreach (BrinkKind kind in new BrinkKind[3] { BrinkKind.Roof, BrinkKind.Creed, BrinkKind.City })
			{
				Assert.Greater(KingdomBrinkRules.WindowFor(kind), 0, kind + " must leave the founder something to do");
				Assert.IsFalse(KingdomBrinkRules.WindowSpent(kind, 0), kind + " must not fire on the announcing pass");
			}
		}

		[Test]
		public void TheCreedWindowIsThreeTimesTheRoofWindowBecauseACreedIsNotARoof()
		{
			Assert.AreEqual(KingdomBrinkRules.RoofBrinkWindow * 3, KingdomBrinkRules.CreedBrinkWindow);
		}

		// --- Rule 4: the window is spent in attended passes and in nothing else -----------

		[Test]
		public void AfterAttendedPass_TheFirstPassAnnouncesAndSpendsNothing()
		{
			Assert.AreEqual(0, KingdomBrinkRules.AfterAttendedPass(KingdomBrinkRules.Unannounced));
			Assert.IsTrue(KingdomBrinkRules.ShouldAnnounce(KingdomBrinkRules.Unannounced));
			Assert.IsFalse(KingdomBrinkRules.ShouldAnnounce(0), "announced once, and never nagged about again");
			Assert.IsFalse(KingdomBrinkRules.ShouldAnnounce(5));
		}

		[TestCase(-7, 0)]
		[TestCase(-1, 0)]
		[TestCase(0, 1)]
		[TestCase(5, 6)]
		public void AfterAttendedPass_StepsByExactlyOnePerPassAndAnyNegativeEntersAtZero(int before, int after)
		{
			Assert.AreEqual(after, KingdomBrinkRules.AfterAttendedPass(before));
		}

		[TestCase(BrinkKind.Roof)]
		[TestCase(BrinkKind.Creed)]
		[TestCase(BrinkKind.City)]
		public void TheWindowIsExactlyItsLengthOfAttendedPassesAfterTheOneThatAnnouncedIt(BrinkKind kind)
		{
			// Driven as the pass drives it, so a mutation to either half -- the advance or the
			// threshold -- moves the firing and fails here.
			int spent = KingdomBrinkRules.Unannounced;
			int passes = 0;
			while (!KingdomBrinkRules.WindowSpent(kind, spent))
			{
				spent = KingdomBrinkRules.AfterAttendedPass(spent);
				passes++;
				Assert.Less(passes, 100, "the window must terminate");
			}
			Assert.AreEqual(KingdomBrinkRules.WindowFor(kind) + 1, passes,
				"the announcing pass plus a whole window of attended passes after it");
		}

		[TestCase(BrinkKind.Roof)]
		[TestCase(BrinkKind.Creed)]
		[TestCase(BrinkKind.City)]
		public void AbsenceNeverSpendsAWindowBecauseNothingButAnAttendedPassAdvancesIt(BrinkKind kind)
		{
			// The founder is away: no attended pass runs, so AfterAttendedPass is never called,
			// and the window is exactly where they left it however long they are gone. Nothing in
			// this file reads a clock as an INPUT to the window -- DaysStood only ever reports.
			int spent = KingdomBrinkRules.AfterAttendedPass(KingdomBrinkRules.Unannounced);
			Assert.AreEqual(0, spent);
			Assert.IsFalse(KingdomBrinkRules.WindowSpent(kind, spent), "still held after any amount of absence");
			Assert.AreEqual(1, KingdomBrinkRules.AfterAttendedPass(spent), "and one attended pass advances it by exactly one");
		}

		[TestCase(BrinkKind.Roof)]
		[TestCase(BrinkKind.Creed)]
		[TestCase(BrinkKind.City)]
		public void PassesLeft_CountsDownToZeroAndStops(BrinkKind kind)
		{
			int window = KingdomBrinkRules.WindowFor(kind);
			Assert.AreEqual(window, KingdomBrinkRules.PassesLeft(kind, KingdomBrinkRules.Unannounced));
			Assert.AreEqual(window, KingdomBrinkRules.PassesLeft(kind, 0));
			Assert.AreEqual(1, KingdomBrinkRules.PassesLeft(kind, window - 1));
			Assert.AreEqual(0, KingdomBrinkRules.PassesLeft(kind, window));
			Assert.AreEqual(0, KingdomBrinkRules.PassesLeft(kind, window + 9), "never negative");
		}

		// --- Rule 1: reaching the threshold stops the accrual -----------------------------

		[Test]
		public void HoldAtBrink_AThousandDaysAndTenDaysArriveAtTheSamePlace()
		{
			// The whole of clause 3, as arithmetic. Two accruals that both crossed the line stand
			// in exactly the same place, so the founder who was away a season and the founder who
			// was away a week come home to the same settlement.
			int tenDays = KingdomBrinkRules.HoldAtBrink(72 + 10, 72);
			int aThousand = KingdomBrinkRules.HoldAtBrink(72 + 3000, 72);
			Assert.AreEqual(72, tenDays);
			Assert.AreEqual(tenDays, aThousand);
		}

		[Test]
		public void HoldAtBrink_LeavesEverythingShortOfTheLineExactlyWhereItIs()
		{
			Assert.AreEqual(71, KingdomBrinkRules.HoldAtBrink(71, 72));
			Assert.AreEqual(0, KingdomBrinkRules.HoldAtBrink(0, 72));
			Assert.AreEqual(0, KingdomBrinkRules.HoldAtBrink(-9, 72), "a negative reads as none");
		}

		[Test]
		public void HoldAtBrink_ALineAtNothingIsNotALine()
		{
			Assert.AreEqual(500, KingdomBrinkRules.HoldAtBrink(500, 0));
			Assert.AreEqual(500, KingdomBrinkRules.HoldAtBrink(500, -3));
		}

		// --- Rule 3: the honest elapsed ---------------------------------------------------

		[Test]
		public void CrossingTick_DatesTheBrinkOnTheDayItWasCrossedAndNotTheDayItWasNoticed()
		{
			// Standing at 90, breaking at 100, four points a day: the fourth day is the one that
			// crosses. Noticed on day 40 of the absence; dated to day 3, not to day 40.
			long start = 10L * Day;
			long now = start + 40L * Day;
			Assert.AreEqual(start + 3L * Day, KingdomBrinkRules.CrossingTick(start, now, 90, 100, 4));
		}

		[Test]
		public void CrossingTick_ARateThatCrossesOnTheFirstDayIsDatedToTheFirstDay()
		{
			long start = 5L * Day;
			Assert.AreEqual(start + Day, KingdomBrinkRules.CrossingTick(start, start + 900L * Day, 96, 100, 4));
		}

		[Test]
		public void CrossingTick_NeverDatesABrinkInTheFuture()
		{
			// A rate that overshoots inside a stretch shorter than the arithmetic wants is clamped
			// to the moment it was resolved rather than being dated after it.
			long start = 5L * Day;
			long now = start + Day;
			Assert.AreEqual(now, KingdomBrinkRules.CrossingTick(start, now, 0, 100, 1000));
		}

		[Test]
		public void CrossingTick_SomethingAlreadyOverTheLineWasOverItWhenTheStretchBegan()
		{
			long start = 5L * Day;
			Assert.AreEqual(start, KingdomBrinkRules.CrossingTick(start, start + 50L * Day, 100, 100, 4));
		}

		[Test]
		public void CrossingTick_IsZeroWhenNothingCouldEverCrossIt()
		{
			long start = 5L * Day;
			Assert.AreEqual(0L, KingdomBrinkRules.CrossingTick(start, start + 50L * Day, 0, 100, 0), "a rate of nothing crosses nothing");
			Assert.AreEqual(0L, KingdomBrinkRules.CrossingTick(start, start + 50L * Day, 0, 0, 4), "a line at nothing is not a line");
			Assert.AreEqual(0L, KingdomBrinkRules.CrossingTick(0L, 50L * Day, 0, 100, 4), "an unplanted stamp dates nothing");
			Assert.AreEqual(0L, KingdomBrinkRules.CrossingTick(start, start - Day, 0, 100, 4), "a clock that ran backwards dates nothing");
		}

		[Test]
		public void DaysStood_ReportsTheRealNumberHoweverLargeItIs()
		{
			long reached = 100L * Day;
			Assert.AreEqual(31, KingdomBrinkRules.DaysStood(reached, reached + 31L * Day));
			Assert.AreEqual(1000, KingdomBrinkRules.DaysStood(reached, reached + 1000L * Day),
				"the founder is owed the real number; this is the one clock in the brink that is uncapped on purpose");
		}

		[Test]
		public void DaysStood_IsZeroForABrinkReachedTonightOrNeverDated()
		{
			Assert.AreEqual(0, KingdomBrinkRules.DaysStood(100L * Day, 100L * Day));
			Assert.AreEqual(0, KingdomBrinkRules.DaysStood(100L * Day, 100L * Day + Day - 1L), "a part day is not a day");
			Assert.AreEqual(0, KingdomBrinkRules.DaysStood(0L, 900L * Day), "an undated brink reads as tonight, never as the age of the world");
			Assert.AreEqual(0, KingdomBrinkRules.DaysStood(100L * Day, 50L * Day), "a clock that went backwards stands for nothing");
		}

		// --- The recalibration ------------------------------------------------------------

		[Test]
		public void CohabitationDaysPerAttendedPass_IsTheCadenceTheDesignAlwaysAssumed()
		{
			// Three, and not a new guess: it is the number the retired absence cap was, and the
			// number KingdomCreedRules.RiteCooldownDays still is, because both were the same
			// statement about how often a present founder comes home.
			Assert.AreEqual(3, KingdomBrinkRules.CohabitationDaysPerAttendedPass);
			Assert.AreEqual(KingdomBrinkRules.CohabitationDaysPerAttendedPass, KingdomCreedRules.RiteCooldownDays);
		}

		[Test]
		public void InCohabitationDays_RestatesAPassFigureAtExactlyThatCadence()
		{
			Assert.AreEqual(72 * 3, KingdomBrinkRules.InCohabitationDays(72));
			Assert.AreEqual(3, KingdomBrinkRules.InCohabitationDays(1));
			Assert.AreEqual(0, KingdomBrinkRules.InCohabitationDays(0), "a threshold of nothing must never be minted by a change of unit");
			Assert.AreEqual(0, KingdomBrinkRules.InCohabitationDays(-4));
		}

		[Test]
		public void EveryMigratedThresholdIsItsOldSelfTimesTheCadenceAndNothingElse()
		{
			// The recalibration, pinned end to end. If any of these three drifts, an attentive
			// founder's road silently changed length and the change of unit stopped being a
			// change of unit.
			Assert.AreEqual(KingdomBrinkRules.InCohabitationDays(KingdomConversionRules.SharedLivingInPasses),
				KingdomConversionRules.SharedLivingForConversion, "osmosis");
			Assert.AreEqual(KingdomBrinkRules.InCohabitationDays(KingdomFaithRules.ConversionPullInPasses),
				KingdomFaithRules.ConversionPullThreshold, "the shrine's pull");
			Assert.AreEqual(KingdomBrinkRules.InCohabitationDays(KingdomWaterRiteRules.SharedPassesForFullReach),
				KingdomWaterRiteRules.MaxCountedDays, "the water rite's shared living");
		}

		// --- Prose: said once, and unsaid ------------------------------------------------

		[TestCase(BrinkKind.Roof)]
		[TestCase(BrinkKind.Creed)]
		[TestCase(BrinkKind.City)]
		public void AnnounceNote_NamesTheSubjectTheElapsedAndWhatIsLeftOfTheWindow(BrinkKind kind)
		{
			string line = KingdomBrinkRules.AnnounceNote(kind, "Aeru", "the Barathrumites", 31, 2);
			StringAssert.Contains("Aeru", line);
			StringAssert.Contains("31 days", line);
			StringAssert.Contains("2 more visits", line);
		}

		[TestCase(BrinkKind.Roof)]
		[TestCase(BrinkKind.Creed)]
		[TestCase(BrinkKind.City)]
		public void AnnounceNote_ANamelessSubjectStillReadsAsASentence(BrinkKind kind)
		{
			string line = KingdomBrinkRules.AnnounceNote(kind, null, null, 0, 1);
			Assert.IsFalse(string.IsNullOrEmpty(line));
			StringAssert.DoesNotContain("  ", line);
		}

		[TestCase(BrinkKind.Roof)]
		[TestCase(BrinkKind.Creed)]
		[TestCase(BrinkKind.City)]
		public void AnnounceTelling_IsALowerCaseClauseTheChronicleCanDateAndClose(BrinkKind kind)
		{
			string line = KingdomBrinkRules.AnnounceTelling(kind, "Aeru", "the Barathrumites", 31);
			Assert.IsFalse(string.IsNullOrEmpty(line));
			Assert.IsFalse(line.EndsWith("."), "the chronicle closes its own sentences");
			StringAssert.Contains("31 days", line);
		}

		[TestCase(BrinkKind.Roof)]
		[TestCase(BrinkKind.Creed)]
		[TestCase(BrinkKind.City)]
		public void LiftedNote_SaysTheThingIsOffRatherThanLeavingAWarningStanding(BrinkKind kind)
		{
			string line = KingdomBrinkRules.LiftedNote(kind, "Aeru");
			Assert.IsFalse(string.IsNullOrEmpty(line));
			Assert.AreNotEqual(KingdomBrinkRules.AnnounceNote(kind, "Aeru", "the Barathrumites", 0, 1), line);
		}

		[TestCase(0, "since tonight")]
		[TestCase(-4, "since tonight")]
		[TestCase(1, "since yesterday")]
		public void ElapsedPhrase_SaysTheShortSpansTheWayAPersonWould(int days, string expected)
		{
			Assert.AreEqual(expected, KingdomBrinkRules.ElapsedPhrase(days));
		}

		[Test]
		public void ElapsedPhrase_QuotesTheRealNumberOnceItIsWorthQuoting()
		{
			StringAssert.Contains("31 days", KingdomBrinkRules.ElapsedPhrase(31));
			StringAssert.Contains("1000 days", KingdomBrinkRules.ElapsedPhrase(1000));
		}

		[TestCase(0, "no more time")]
		[TestCase(1, "one more visit")]
		[TestCase(3, "3 more visits")]
		public void WindowPhrase_IsSingularWhereItShouldBe(int left, string expected)
		{
			StringAssert.Contains(expected, KingdomBrinkRules.WindowPhrase(left));
		}

		// --- The consumers derive, they do not duplicate -----------------------------------

		[Test]
		public void TheRoofsGraceIsTheRoofWindowAndNotACopyOfIt()
		{
			Assert.AreEqual(KingdomBrinkRules.RoofBrinkWindow, KingdomLodgingRules.GracePasses);
			Assert.AreEqual(KingdomBrinkRules.Unannounced, KingdomLodgingRules.NoGrace);
			Assert.AreEqual(KingdomBrinkRules.AfterAttendedPass(KingdomBrinkRules.Unannounced), KingdomLodgingRules.GraceAfterPass(KingdomLodgingRules.NoGrace));
			Assert.AreEqual(KingdomBrinkRules.WindowSpent(BrinkKind.Roof, KingdomBrinkRules.RoofBrinkWindow), KingdomLodgingRules.GraceRunOut(KingdomLodgingRules.GracePasses));
		}

		[Test]
		public void TheResentedCreedsGraceIsTheCreedWindowAndNotACopyOfIt()
		{
			Assert.AreEqual(KingdomBrinkRules.CreedBrinkWindow, KingdomConversionRules.ResentedPasses);
			Assert.AreEqual(KingdomBrinkRules.Unannounced, KingdomConversionRules.NoResentment);
			Assert.AreEqual(KingdomBrinkRules.AfterAttendedPass(3), KingdomConversionRules.ResentmentAfterPass(3));
			Assert.IsTrue(KingdomConversionRules.ResentmentRunOut(KingdomBrinkRules.CreedBrinkWindow));
			Assert.IsFalse(KingdomConversionRules.ResentmentRunOut(KingdomBrinkRules.CreedBrinkWindow - 1));
		}

		[Test]
		public void TheCitysWindowIsTheCityWindowAndNotACopyOfIt()
		{
			Assert.AreEqual(KingdomBrinkRules.CityBrinkWindow, KingdomCreedRules.SecessionWindowPasses);
		}

		// --- The ledger lane: announced above the housekeeping, unsaid on arrest -----------

		[Test]
		public void TheLedgerCarriesABrinkAboveEverythingTheFounderCannotActOn()
		{
			// The founder must not have to read past six lines of drams to find the one person
			// who is leaving. The lane is printed first, and the day count above it dates it.
			KingdomLedger ledger = new KingdomLedger();
			ledger.Note("ordinary housekeeping");
			ledger.NoteBrink(KingdomBrinkRules.AnnounceNote(BrinkKind.Roof, "Aeru", null, 31, 2));
			string digest = ledger.Digest("Kavvat", 31);
			int brink = digest.IndexOf("Aeru");
			int note = digest.IndexOf("ordinary housekeeping");
			Assert.Greater(brink, 0);
			Assert.Greater(note, brink, "the brink lane comes first");
			StringAssert.Contains("31 days", digest);
		}

		[Test]
		public void ABrinkAloneIsWorthComingHomeFor()
		{
			KingdomLedger ledger = new KingdomLedger();
			Assert.IsFalse(ledger.Any);
			ledger.NoteBrink(KingdomBrinkRules.AnnounceNote(BrinkKind.City, "Basra", "Nesh", 12, 3));
			Assert.IsTrue(ledger.Any, "a realm one window from splitting has news even if no water moved");
		}

		[Test]
		public void TheUnsayingIsCarriedInTheSameLaneAndInADifferentColour()
		{
			KingdomLedger ledger = new KingdomLedger();
			ledger.NoteBrinkLifted(KingdomBrinkRules.LiftedNote(BrinkKind.Creed, "Aeru"));
			string digest = ledger.Digest("Kavvat", 2);
			StringAssert.Contains("Aeru", digest);
			StringAssert.Contains("{{G|", digest, "the only good news in the lane reads as good news");
		}

		[Test]
		public void TheBrinkLaneIsClearedBetweenVisitsSoNobodyIsToldTwice()
		{
			KingdomLedger ledger = new KingdomLedger();
			ledger.NoteBrink(KingdomBrinkRules.AnnounceNote(BrinkKind.Roof, "Aeru", null, 1, 2));
			ledger.Reset();
			Assert.IsFalse(ledger.Any);
			Assert.AreEqual(0, ledger.BrinkLines.Count);
		}

		[Test]
		public void TheBrinkLaneStopsListingRatherThanRunningAwayWithTheReport()
		{
			KingdomLedger ledger = new KingdomLedger();
			for (int i = 0; i < KingdomLedger.MaxBrinkLines + 20; i++)
			{
				ledger.NoteBrink(KingdomBrinkRules.AnnounceNote(BrinkKind.Creed, "settler-" + i, "the Barathrumites", i, 6));
			}
			Assert.AreEqual(KingdomLedger.MaxBrinkLines, ledger.BrinkLines.Count);
		}

		[Test]
		public void AnEmptyLineIsNeverCarried()
		{
			KingdomLedger ledger = new KingdomLedger();
			ledger.NoteBrink(null);
			ledger.NoteBrink("");
			ledger.NoteBrinkLifted(null);
			Assert.IsFalse(ledger.Any);
		}
	}
}
#endif
