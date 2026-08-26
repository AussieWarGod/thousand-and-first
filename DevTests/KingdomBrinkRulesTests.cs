#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The brink (Addendum 8 clause 3, moderated by Addendum 10(a)): the last arrestable window in
	/// front of every irreversible consequence in the mod. Asserted as a SHAPE rather than as three
	/// separate graces, because the whole point of moving it here was that the roof, the creed and
	/// the city cannot drift apart &mdash; a mutation to the window arithmetic must fail once,
	/// here, and not silently change what a settler's window means while leaving a city's alone.
	/// <para>
	/// The doctrine this file pins is the NEW one: the warning is pushed at the crossing, the
	/// window runs in world-days from that delivery, and the consequence fires when it is spent
	/// whether or not the founder came back. What did not move, and is still pinned verbatim: a
	/// thousand-day absence and a ten-day absence arrive at the same place, the warning is said
	/// exactly once, removing the cause lifts and unsays it, and nothing irreversible fires
	/// UNWARNED.
	/// </para>
	/// </summary>
	public class KingdomBrinkRulesTests
	{
		private const long Day = KingdomRules.TicksPerDay;

		[Test]
		public void BrinkRecordKeepsPublicReadonlyAbi()
		{
			System.Type type = typeof(BrinkRecord);
			Assert.IsTrue(type.IsPublic);
			Assert.IsTrue(type.IsValueType);
			Assert.IsNotNull(type.GetConstructor(new System.Type[]
				{ typeof(bool), typeof(long), typeof(long), typeof(string), typeof(int) }));
			string[] names = new string[] { "Stands", "ReachedTick", "WarnedTick", "Cause", "Channel" };
			System.Type[] types = new System.Type[]
				{ typeof(bool), typeof(long), typeof(long), typeof(string), typeof(int) };
			for (int i = 0; i < names.Length; i++)
			{
				System.Reflection.FieldInfo field = type.GetField(names[i]);
				Assert.IsNotNull(field, names[i]);
				Assert.AreEqual(types[i], field.FieldType, names[i]);
				Assert.IsTrue(field.IsPublic, names[i]);
				Assert.IsTrue(field.IsInitOnly, names[i]);
			}
			Assert.AreEqual(names.Length, type.GetFields().Length);
		}

		// --- The three windows, and the derivation that produced them ---------------------

		[TestCase(BrinkKind.Roof, KingdomBrinkRules.RoofBrinkWindowDays)]
		[TestCase(BrinkKind.Creed, KingdomBrinkRules.CreedBrinkWindowDays)]
		[TestCase(BrinkKind.City, KingdomBrinkRules.CityBrinkWindowDays)]
		public void WindowDays_NamesTheLengthTheOwningDesignAsksFor(BrinkKind kind, int expected)
		{
			Assert.AreEqual(expected, KingdomBrinkRules.WindowDays(kind));
		}

		[Test]
		public void TheWindowsAreSixEighteenAndNineWorldDays()
		{
			Assert.AreEqual(6, KingdomBrinkRules.RoofBrinkWindowDays, "a roof is tonight's problem");
			Assert.AreEqual(18, KingdomBrinkRules.CreedBrinkWindowDays, "a creed is a life's");
			Assert.AreEqual(9, KingdomBrinkRules.CityBrinkWindowDays, "a city is nine days, one rung under the rupture span");
		}

		[TestCase(BrinkKind.Roof)]
		[TestCase(BrinkKind.Creed)]
		[TestCase(BrinkKind.City)]
		public void EveryWindowIsItsOldAttendedPassRopeTimesTheCadenceAndNothingElse(BrinkKind kind)
		{
			// The migration from passes to time is a MULTIPLICATION with an argument, never a
			// re-guess. If any window stops being derivable this way, an attentive founder's rope
			// silently changed length and the change of unit stopped being a change of unit.
			Assert.AreEqual(KingdomBrinkRules.InCohabitationDays(KingdomBrinkRules.WindowPasses(kind)),
				KingdomBrinkRules.WindowDays(kind));
		}

		[Test]
		public void TheOldRopesAreKeptAsTheInputSoEachWindowShowsItsWorking()
		{
			Assert.AreEqual(2, KingdomBrinkRules.RoofBrinkWindowPasses);
			Assert.AreEqual(6, KingdomBrinkRules.CreedBrinkWindowPasses);
			Assert.AreEqual(3, KingdomBrinkRules.CityBrinkWindowPasses);
		}

		[Test]
		public void TheCreedWindowIsThreeTimesTheRoofWindowInEitherUnitBecauseACreedIsNotARoof()
		{
			Assert.AreEqual(KingdomBrinkRules.RoofBrinkWindowPasses * 3, KingdomBrinkRules.CreedBrinkWindowPasses);
			Assert.AreEqual(KingdomBrinkRules.RoofBrinkWindowDays * 3, KingdomBrinkRules.CreedBrinkWindowDays);
		}

		[Test]
		public void EveryWindowIsPositiveSoNoBrinkFiresOnTheDayTheWordGoesOut()
		{
			long warned = 400L * Day;
			foreach (BrinkKind kind in new BrinkKind[3] { BrinkKind.Roof, BrinkKind.Creed, BrinkKind.City })
			{
				Assert.Greater(KingdomBrinkRules.WindowDays(kind), 0, kind + " must leave the founder something to do");
				Assert.IsFalse(KingdomBrinkRules.WindowSpent(kind, warned, warned), kind + " must not fire on the warning day");
			}
		}

		// --- Rule 4: the window runs in world-days from the warning ------------------------

		[TestCase(BrinkKind.Roof)]
		[TestCase(BrinkKind.Creed)]
		[TestCase(BrinkKind.City)]
		public void TheWindowIsSpentAtExactlyItsLengthOfWorldDaysAndNotOneDayEarlier(BrinkKind kind)
		{
			long warned = 90L * Day;
			int window = KingdomBrinkRules.WindowDays(kind);
			Assert.IsFalse(KingdomBrinkRules.WindowSpent(kind, warned, warned + (window - 1L) * Day), "the window ran out early");
			Assert.IsFalse(KingdomBrinkRules.WindowSpent(kind, warned, warned + window * Day - 1L), "a part day is not a day");
			Assert.IsTrue(KingdomBrinkRules.WindowSpent(kind, warned, warned + window * Day), "the window never ran out");
			Assert.IsTrue(KingdomBrinkRules.WindowSpent(kind, warned, warned + 4000L * Day), "and it stays spent");
		}

		[TestCase(BrinkKind.Roof)]
		[TestCase(BrinkKind.Creed)]
		[TestCase(BrinkKind.City)]
		public void AbsenceSPENDSTheWindowNowAndThatIsTheWholeOfAddendumTenA(BrinkKind kind)
		{
			// The inversion of the rule this file used to pin. The founder is warned and then goes
			// away: no pass runs, and the window spends anyway, because it is the world's clock
			// and not the founder's attendance. Presence has stopped being a shield.
			long warned = 12L * Day;
			long away = warned + (long)KingdomBrinkRules.WindowDays(kind) * Day;
			Assert.IsTrue(KingdomBrinkRules.WindowSpent(kind, warned, away),
				"a warned founder who stays away must not be able to hold the window open by being elsewhere");
			Assert.AreEqual(0, KingdomBrinkRules.DaysLeft(kind, warned, away));
		}

		[TestCase(BrinkKind.Roof)]
		[TestCase(BrinkKind.Creed)]
		[TestCase(BrinkKind.City)]
		public void NothingIrreversibleEverFiresUnwarnedHoweverOldTheBrinkIs(BrinkKind kind)
		{
			// Ignorance is the shield that replaced presence. A brink recorded and never announced
			// has no deadline at all: not after its own window, not after a thousand days.
			Assert.IsFalse(KingdomBrinkRules.Warned(KingdomBrinkRules.Unwarned));
			Assert.IsFalse(KingdomBrinkRules.WindowSpent(kind, KingdomBrinkRules.Unwarned, 5000L * Day));
			Assert.AreEqual(0L, KingdomBrinkRules.ExpiryTick(kind, KingdomBrinkRules.Unwarned), "an unwarned brink has no deadline");
			Assert.AreEqual(KingdomBrinkRules.WindowDays(kind), KingdomBrinkRules.DaysLeft(kind, KingdomBrinkRules.Unwarned, 5000L * Day),
				"and its whole window is still in front of the founder on the day they are told");
		}

		[TestCase(BrinkKind.Roof)]
		[TestCase(BrinkKind.Creed)]
		[TestCase(BrinkKind.City)]
		public void ExpiryTick_IsTheDayItHappensSoTheAftermathCanBeDatedToIt(BrinkKind kind)
		{
			long warned = 200L * Day;
			long expiry = warned + (long)KingdomBrinkRules.WindowDays(kind) * Day;
			Assert.AreEqual(expiry, KingdomBrinkRules.ExpiryTick(kind, warned));
			// The founder walks back in a season later: the consequence is dated to the expiry and
			// not to the homecoming, which is what FiredClause and FiredNote quote.
			Assert.AreEqual(80, KingdomBrinkRules.DaysStood(expiry, expiry + 80L * Day));
		}

		[TestCase(BrinkKind.Roof)]
		[TestCase(BrinkKind.Creed)]
		[TestCase(BrinkKind.City)]
		public void DaysLeft_CountsDownToZeroAndStops(BrinkKind kind)
		{
			long warned = 30L * Day;
			int window = KingdomBrinkRules.WindowDays(kind);
			Assert.AreEqual(window, KingdomBrinkRules.DaysLeft(kind, warned, warned));
			Assert.AreEqual(window - 1, KingdomBrinkRules.DaysLeft(kind, warned, warned + Day));
			Assert.AreEqual(1, KingdomBrinkRules.DaysLeft(kind, warned, warned + (window - 1L) * Day));
			Assert.AreEqual(0, KingdomBrinkRules.DaysLeft(kind, warned, warned + window * Day));
			Assert.AreEqual(0, KingdomBrinkRules.DaysLeft(kind, warned, warned + (window + 900L) * Day), "never negative");
		}

		[Test]
		public void DaysSinceWarning_IsZeroForAnUnwarnedBrinkAndForOneWarnedTonight()
		{
			Assert.AreEqual(0, KingdomBrinkRules.DaysSinceWarning(KingdomBrinkRules.Unwarned, 900L * Day));
			Assert.AreEqual(0, KingdomBrinkRules.DaysSinceWarning(100L * Day, 100L * Day));
			Assert.AreEqual(0, KingdomBrinkRules.DaysSinceWarning(100L * Day, 50L * Day), "a clock that went backwards spends nothing");
			Assert.AreEqual(31, KingdomBrinkRules.DaysSinceWarning(100L * Day, 131L * Day));
		}

		[Test]
		public void DayNumber_FloorsToWholeDaysSoTheOneIntStoreCanHoldAWarning()
		{
			Assert.AreEqual(0, KingdomBrinkRules.DayNumber(0L));
			Assert.AreEqual(0, KingdomBrinkRules.DayNumber(-9L), "an unplanted stamp is not day minus one");
			Assert.AreEqual(0, KingdomBrinkRules.DayNumber(Day - 1L));
			Assert.AreEqual(1, KingdomBrinkRules.DayNumber(Day));
			Assert.AreEqual(413, KingdomBrinkRules.DayNumber(413L * Day + 17L));
		}

		// --- Rule 1: reaching the threshold stops the accrual -----------------------------

		[Test]
		public void HoldAtBrink_AThousandDaysAndTenDaysArriveAtTheSamePlace()
		{
			// The half of clause 3 that Addendum 10(a) did NOT move. Two accruals that both crossed
			// the line stand in exactly the same place, so the founder who was away a season and
			// the founder who was away a week find the same settlement at the brink -- what
			// happens AFTER the warning is what changed, never what happens before it.
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
			// The recalibration, pinned end to end -- the roads AND the windows in front of them.
			// If any of these drifts, an attentive founder's road silently changed length and the
			// change of unit stopped being a change of unit.
			Assert.AreEqual(KingdomBrinkRules.InCohabitationDays(KingdomConversionRules.SharedLivingInPasses),
				KingdomConversionRules.SharedLivingForConversion, "osmosis");
			Assert.AreEqual(KingdomBrinkRules.InCohabitationDays(KingdomFaithRules.ConversionPullInPasses),
				KingdomFaithRules.ConversionPullThreshold, "the shrine's pull");
			Assert.AreEqual(KingdomBrinkRules.InCohabitationDays(KingdomWaterRiteRules.SharedPassesForFullReach),
				KingdomWaterRiteRules.MaxCountedDays, "the water rite's shared living");
			Assert.AreEqual(KingdomBrinkRules.InCohabitationDays(KingdomBrinkRules.RoofBrinkWindowPasses),
				KingdomBrinkRules.RoofBrinkWindowDays, "the roof's window");
			Assert.AreEqual(KingdomBrinkRules.InCohabitationDays(KingdomBrinkRules.CreedBrinkWindowPasses),
				KingdomBrinkRules.CreedBrinkWindowDays, "the creed's window");
			Assert.AreEqual(KingdomBrinkRules.InCohabitationDays(KingdomBrinkRules.CityBrinkWindowPasses),
				KingdomBrinkRules.CityBrinkWindowDays, "the city's window");
		}

		// --- Prose: coached, said once, unsaid, and dated when it fires late ---------------

		[TestCase(BrinkKind.Roof)]
		[TestCase(BrinkKind.Creed)]
		[TestCase(BrinkKind.City)]
		public void AnnounceNote_NamesTheSubjectTheElapsedAndTheDaysLeft(BrinkKind kind)
		{
			string line = KingdomBrinkRules.AnnounceNote(kind, "Aeru", "the Barathrumites", 31, 4);
			StringAssert.Contains("Aeru", line);
			StringAssert.Contains("31 days", line);
			StringAssert.Contains("4 days", line);
			StringAssert.DoesNotContain("visit", line, "the window is the world's now, not a count of homecomings");
		}

		[TestCase(BrinkKind.Roof)]
		[TestCase(BrinkKind.Creed)]
		[TestCase(BrinkKind.City)]
		public void AnnounceNote_AlwaysCarriesTheArrestBecauseAWarningThatOnlyReportsIsAnAmbush(BrinkKind kind)
		{
			// Addendum 10(a)'s coaching clause. A consequence that may land while the founder is
			// away must be announced by a line that names what would stop it -- every kind, every
			// time, including when the cause has no name to give.
			string arrest = KingdomBrinkRules.ArrestNote(kind, "the Barathrumites");
			Assert.IsFalse(string.IsNullOrEmpty(arrest));
			StringAssert.Contains(arrest, KingdomBrinkRules.AnnounceNote(kind, "Aeru", "the Barathrumites", 3, 2));
			StringAssert.Contains(KingdomBrinkRules.ArrestNote(kind, null), KingdomBrinkRules.AnnounceNote(kind, null, null, 0, 1));
		}

		[Test]
		public void EachKindNamesADifferentArrestBecauseTheyAreDifferentThingsToDo()
		{
			Assert.AreNotEqual(KingdomBrinkRules.ArrestNote(BrinkKind.Roof, null), KingdomBrinkRules.ArrestNote(BrinkKind.Creed, null));
			Assert.AreNotEqual(KingdomBrinkRules.ArrestNote(BrinkKind.Creed, null), KingdomBrinkRules.ArrestNote(BrinkKind.City, null));
			Assert.AreNotEqual(KingdomBrinkRules.ArrestNote(BrinkKind.City, null), KingdomBrinkRules.ArrestNote(BrinkKind.Roof, null));
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
		[TestCase(1, "one day")]
		[TestCase(6, "6 days")]
		public void WindowPhrase_CountsDaysAndIsSingularWhereItShouldBe(int left, string expected)
		{
			StringAssert.Contains(expected, KingdomBrinkRules.WindowPhrase(left));
		}

		[TestCase(0, "today")]
		[TestCase(-3, "today")]
		[TestCase(1, "yesterday")]
		[TestCase(12, "12 days ago")]
		public void FiredPhrase_DatesAConsequenceThatLandedWhileNobodyWasWatching(int ago, string expected)
		{
			Assert.AreEqual(expected, KingdomBrinkRules.FiredPhrase(ago));
		}

		[Test]
		public void FiredClause_IsEmptyForSomethingThatHappenedTodayAndDatedForAnythingOlder()
		{
			Assert.AreEqual("", KingdomBrinkRules.FiredClause(0), "a present-tense line is already dated correctly");
			Assert.AreEqual("", KingdomBrinkRules.FiredClause(-2));
			StringAssert.Contains("12 days ago", KingdomBrinkRules.FiredClause(12));
			StringAssert.Contains("warned", KingdomBrinkRules.FiredClause(12), "the founder is reminded it is the window they were told about");
		}

		[TestCase(BrinkKind.Roof)]
		[TestCase(BrinkKind.Creed)]
		[TestCase(BrinkKind.City)]
		public void FiredNote_SaysNothingOnTheDayAndNamesTheSubjectAndTheDateAfterIt(BrinkKind kind)
		{
			Assert.AreEqual("", KingdomBrinkRules.FiredNote(kind, "Aeru", 0),
				"the consequence's own prose already said it; a second line would be a second telling");
			string late = KingdomBrinkRules.FiredNote(kind, "Aeru", 12);
			StringAssert.Contains("Aeru", late);
			StringAssert.Contains("12 days ago", late);
			Assert.IsFalse(string.IsNullOrEmpty(KingdomBrinkRules.FiredNote(kind, null, 5)), "a nameless subject still reads as a sentence");
		}

		// --- The push channel's framing ----------------------------------------------------

		[Test]
		public void WordFrom_NamesTheCityTheNewsCameOutOfAndKeepsTheLineWhole()
		{
			string line = KingdomBrinkRules.AnnounceNote(BrinkKind.Roof, "Aeru", null, 3, 4);
			string pushed = KingdomBrinkRules.WordFrom("Kavvat", line);
			StringAssert.Contains("Kavvat", pushed);
			StringAssert.Contains(line, pushed, "the framing wraps the warning; it never replaces or truncates it");
			StringAssert.Contains("finds you", pushed);
		}

		[Test]
		public void WordFrom_StillReadsWhenTheCityHasNoNameAndCarriesNothingWhenThereIsNothingToSay()
		{
			Assert.IsFalse(string.IsNullOrEmpty(KingdomBrinkRules.WordFrom(null, "Aeru has no roof.")));
			Assert.AreEqual("", KingdomBrinkRules.WordFrom("Kavvat", null));
			Assert.AreEqual("", KingdomBrinkRules.WordFrom("Kavvat", ""));
		}

		// --- The consumers derive, they do not duplicate -----------------------------------

		[Test]
		public void TheRoofsGraceIsTheRoofWindowAndNotACopyOfIt()
		{
			Assert.AreEqual(KingdomBrinkRules.RoofBrinkWindowDays, KingdomLodgingRules.GraceDays);
		}

		[Test]
		public void TheResentedCreedsWindowIsTheCreedWindowAndNotACopyOfIt()
		{
			Assert.AreEqual(KingdomBrinkRules.CreedBrinkWindowDays, KingdomConversionRules.ResentedWindowDays);
			Assert.AreEqual(0, KingdomConversionRules.NotWarned, "an absent map entry and an unwarned one must read the same");
		}

		[Test]
		public void TheCitysWindowIsTheCityWindowAndNotACopyOfIt()
		{
			Assert.AreEqual(KingdomBrinkRules.CityBrinkWindowDays, KingdomCreedRules.SecessionWindowDays);
		}

		// --- The ledger lane: announced above the housekeeping, unsaid on arrest -----------

		[Test]
		public void TheLedgerCarriesABrinkAboveEverythingTheFounderCannotActOn()
		{
			// The founder must not have to read past six lines of drams to find the one person
			// who is leaving. The lane is printed first, and the day count above it dates it.
			KingdomLedger ledger = new KingdomLedger();
			ledger.Note("ordinary housekeeping");
			ledger.NoteBrink(KingdomBrinkRules.AnnounceNote(BrinkKind.Roof, "Aeru", null, 31, 6));
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
			ledger.NoteBrink(KingdomBrinkRules.AnnounceNote(BrinkKind.City, "Basra", "Nesh", 12, 9));
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
			ledger.NoteBrink(KingdomBrinkRules.AnnounceNote(BrinkKind.Roof, "Aeru", null, 1, 6));
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
				ledger.NoteBrink(KingdomBrinkRules.AnnounceNote(BrinkKind.Creed, "settler-" + i, "the Barathrumites", i, 18));
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
