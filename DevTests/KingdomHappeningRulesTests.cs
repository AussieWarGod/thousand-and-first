#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The happenings layer: Qud's own calendar as arithmetic, and the verdicts that turn rows
	/// into news.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE §7.4 W4, BUILDING-CATALOGUE-BRIEF Addendum 13 lanes 4 and 6. The
	/// calendar cases are all checked against the engine's OWN boundaries
	/// (<c>D/XRL/World/Calendar.cs</c>) rather than against numbers this mod likes, because the
	/// whole promise of lane 4 is that the city keeps the day the status bar is naming.
	/// </para>
	/// </summary>
	internal class KingdomHappeningRulesTests
	{
		private const string Here = "taf:zone:here";

		private static KingdomResidentRow Settler(int id, int homeWorkId, int creedCode, long arrivedTick, KingdomResidentStanding standing)
		{
			return new KingdomResidentRow(id, "Ptoh-" + id, 2, creedCode, arrivedTick, homeWorkId, 0, 0,
				KingdomDayShape.Hearth, standing, KingdomStandingCause.None, Here,
				KingdomBrinkWindow.None, KingdomBrinkWindow.None, null, 0);
		}

		private static KingdomWorkRow Work(int id, int condition, int crew, KingdomWorkKind kind)
		{
			return new KingdomWorkRow(id, Here, 10, 10, "r_KingdomMill", condition, crew, 500L,
				new KingdomWorkRunState(kind, 0, 0, 0L));
		}

		private static KingdomCityState Book(KingdomResidentRow[] residents, KingdomWorkRow[] works)
		{
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
				"taf:city:kavvat", 900L, default(KingdomStocks), null, works, residents, null, out state, out fault), fault.ToString());
			return state;
		}

		// ==================================================================================
		// Qud's calendar
		// ==================================================================================

		/// <summary>
		/// Ut yara Ux occupies the ticks the engine's own <c>GetDay</c> tests for:
		/// <c>TimeOfYear &gt; 216000 &amp;&amp; TimeOfYear &lt; 222001</c>. One tick either side is
		/// not the festival, and a mod that drifted off that window would be keeping a feast the
		/// status bar calls the 30th of Uulu Ut.
		/// </summary>
		[TestCase(216000L, false)]
		[TestCase(216001L, true)]
		[TestCase(219000L, true)]
		[TestCase(222000L, true)]
		[TestCase(222001L, false)]
		public void Intercalary_MatchesTheEnginesOwnWindow(long yearTick, bool expected)
		{
			Assert.AreEqual(expected, KingdomHappeningRules.AnchorAt(yearTick) == KingdomFestivalAnchor.UtYaraUx);
		}

		/// <summary>
		/// The Ides is the fifteenth, and the engine's <c>GetDay</c> reaches it at
		/// <c>16800 &lt;= num &lt; 18000</c>. Checked in the first month, where no intercalary
		/// shift applies.
		/// </summary>
		[TestCase(16799L, false)]
		[TestCase(16800L, true)]
		[TestCase(17999L, true)]
		[TestCase(18000L, false)]
		public void Ides_MatchesTheEnginesOwnWindow(long yearTick, bool expected)
		{
			Assert.AreEqual(expected, KingdomHappeningRules.AnchorAt(yearTick) == KingdomFestivalAnchor.Ides);
		}

		/// <summary>
		/// Every month after the intercalary is shifted six thousand ticks, because
		/// <c>Calendar.GetDay</c> subtracts Ut yara Ux's length back out before taking its modulus
		/// (<c>D/XRL/World/Calendar.cs:160-163</c>). Getting this wrong is the single most likely
		/// way for the feast to land on the wrong day, so every one of the twelve is checked.
		/// </summary>
		[Test]
		public void EveryMonthsIdes_LandsOnTheIdes()
		{
			for (int month = 0; month < KingdomHappeningRules.NumberedMonths; month++)
			{
				long ides = KingdomHappeningRules.IdesTickOfMonth(month);
				Assert.AreEqual(KingdomFestivalAnchor.Ides, KingdomHappeningRules.AnchorAt(ides),
					"month " + month + " opens its Ides at " + ides);
				Assert.AreEqual(KingdomFestivalAnchor.Ides, KingdomHappeningRules.AnchorAt(ides + KingdomHappeningRules.TicksPerDay - 1L),
					"month " + month + " closes its Ides at " + (ides + KingdomHappeningRules.TicksPerDay - 1L));
				Assert.AreNotEqual(KingdomFestivalAnchor.Ides, KingdomHappeningRules.AnchorAt(ides - 1L),
					"month " + month + " should not be on its Ides one tick early");
			}
		}

		/// <summary>Thirteen feasts a year and not one more: twelve Ides and one Ut yara Ux.</summary>
		[Test]
		public void AYear_HoldsThirteenFeasts()
		{
			long cursor = 0L;
			int found = 0;
			long due;
			KingdomFestivalAnchor anchor;
			while (KingdomHappeningRules.TryNextFestival(cursor, out due, out anchor) && due < KingdomHappeningRules.TicksPerYear)
			{
				found++;
				cursor = due;
				Assert.AreNotEqual(KingdomFestivalAnchor.None, anchor);
			}
			Assert.AreEqual(13, found);
		}

		/// <summary>
		/// The next feast is strictly after the tick handed in, so a caller that stamps the feast
		/// it just kept and asks again cannot be handed the same feast twice. This is the whole of
		/// the announce-once guarantee for festivals.
		/// </summary>
		[Test]
		public void NextFestival_IsStrictlyAfter()
		{
			long due;
			KingdomFestivalAnchor anchor;
			Assert.IsTrue(KingdomHappeningRules.TryNextFestival(KingdomHappeningRules.IdesTickOfMonth(0), out due, out anchor));
			Assert.Greater(due, KingdomHappeningRules.IdesTickOfMonth(0));
		}

		/// <summary>Past the last Ides of a year, the next feast wraps into the next year rather
		/// than answering with nothing.</summary>
		[Test]
		public void NextFestival_WrapsTheYear()
		{
			long due;
			KingdomFestivalAnchor anchor;
			Assert.IsTrue(KingdomHappeningRules.TryNextFestival(KingdomHappeningRules.TicksPerYear - 1L, out due, out anchor));
			Assert.AreEqual(KingdomHappeningRules.TicksPerYear + KingdomHappeningRules.IdesTickOfMonth(0), due);
			Assert.AreEqual(KingdomFestivalAnchor.Ides, anchor);
		}

		/// <summary>
		/// The backward half is the forward half's inverse, which is what lets the catch-up JUMP
		/// instead of walking: for any feast, the last feast at that tick is that feast.
		/// </summary>
		[Test]
		public void LastFestival_IsTheInverseOfNext()
		{
			long cursor = KingdomHappeningRules.TicksPerYear * 3L;
			for (int i = 0; i < 20; i++)
			{
				long due;
				KingdomFestivalAnchor anchor;
				Assert.IsTrue(KingdomHappeningRules.TryNextFestival(cursor, out due, out anchor));
				long back;
				KingdomFestivalAnchor backAnchor;
				Assert.IsTrue(KingdomHappeningRules.TryLastFestival(due, out back, out backAnchor));
				Assert.AreEqual(due, back);
				Assert.AreEqual(anchor, backAnchor);
				cursor = due;
			}
		}

		/// <summary>
		/// §0.0(a): not one term contains the elapsed. A day's worth of catching up and a
		/// century's cost the same number of steps, because the answer is arithmetic and not a
		/// walk.
		/// </summary>
		[Test]
		public void FestivalArithmetic_DoesNotScaleWithTheElapsed()
		{
			long shortDue;
			long longDue;
			KingdomFestivalAnchor a;
			KingdomFestivalAnchor b;
			Assert.IsTrue(KingdomHappeningRules.TryLastFestival(KingdomHappeningRules.TicksPerYear + 1000L, out shortDue, out a));
			Assert.IsTrue(KingdomHappeningRules.TryLastFestival((KingdomHappeningRules.TicksPerYear * 100L) + 1000L, out longDue, out b));
			Assert.AreEqual(a, b);
			Assert.AreEqual(shortDue % KingdomHappeningRules.TicksPerYear, longDue % KingdomHappeningRules.TicksPerYear);
		}

		// ==================================================================================
		// Weddings
		// ==================================================================================

		private const long Settled = (long)KingdomHappeningRules.CourtshipDays * KingdomHappeningRules.TicksPerDay;

		[Test]
		public void Wedding_NeedsOneRoof()
		{
			long now = Settled + 1000L;
			Assert.IsTrue(KingdomHappeningRules.WeddingEligible(Settler(1, 7, 0, 0L, KingdomResidentStanding.Resident),
				Settler(2, 7, 0, 0L, KingdomResidentStanding.Resident), 0, now));
			Assert.IsFalse(KingdomHappeningRules.WeddingEligible(Settler(1, 7, 0, 0L, KingdomResidentStanding.Resident),
				Settler(2, 8, 0, 0L, KingdomResidentStanding.Resident), 0, now),
				"two roofs is two households, whatever the model thinks of them");
			Assert.IsFalse(KingdomHappeningRules.WeddingEligible(Settler(1, 0, 0, 0L, KingdomResidentStanding.Resident),
				Settler(2, 0, 0, 0L, KingdomResidentStanding.Resident), 0, now),
				"nobody is married under no roof at all");
		}

		[Test]
		public void Wedding_NeedsBothOnTheRoll()
		{
			long now = Settled + 1000L;
			Assert.IsFalse(KingdomHappeningRules.WeddingEligible(Settler(1, 7, 0, 0L, KingdomResidentStanding.Resident),
				Settler(2, 7, 0, 0L, KingdomResidentStanding.Abroad), 0, now));
			Assert.IsFalse(KingdomHappeningRules.WeddingEligible(Settler(1, 7, 0, 0L, KingdomResidentStanding.Dead),
				Settler(2, 7, 0, 0L, KingdomResidentStanding.Resident), 0, now));
		}

		[Test]
		public void Wedding_NeedsTheCourtship()
		{
			Assert.IsFalse(KingdomHappeningRules.WeddingEligible(Settler(1, 7, 0, 0L, KingdomResidentStanding.Resident),
				Settler(2, 7, 0, 0L, KingdomResidentStanding.Resident), 0, Settled - 1L));
			Assert.IsTrue(KingdomHappeningRules.WeddingEligible(Settler(1, 7, 0, 0L, KingdomResidentStanding.Resident),
				Settler(2, 7, 0, 0L, KingdomResidentStanding.Resident), 0, Settled));
		}

		/// <summary>The hostility ceiling is real: raise it by one and the wedding does not
		/// happen. Without this assertion the whole creed clause could be deleted unnoticed.</summary>
		[Test]
		public void Wedding_RefusesAboveTheHostilityCeiling()
		{
			long now = Settled + 1000L;
			Assert.IsFalse(KingdomHappeningRules.WeddingEligible(Settler(1, 7, 0, 0L, KingdomResidentStanding.Resident),
				Settler(2, 7, 0, 0L, KingdomResidentStanding.Resident),
				KingdomHappeningRules.WeddingHostilityCeiling + 1, now));
		}

		/// <summary>
		/// A creed code is one-way, so the model can prove agreement and never disagreement. Same
		/// code agrees; an uncreeded settler agrees with anybody; two different codes are refused
		/// rather than guessed at.
		/// </summary>
		[TestCase(5, 5, 0)]
		[TestCase(0, 5, 0)]
		[TestCase(5, 0, 0)]
		[TestCase(0, 0, 0)]
		[TestCase(5, 6, KingdomHappeningRules.UnknownCreedHostility)]
		public void CreedHostility_ProvesAgreementAndNeverDisagreement(int a, int b, int expected)
		{
			Assert.AreEqual(expected, KingdomHappeningRules.CreedHostility(a, b));
		}

		[Test]
		public void UnknownCreeds_AreAboveTheWeddingCeiling()
		{
			Assert.Greater(KingdomHappeningRules.UnknownCreedHostility, KingdomHappeningRules.WeddingHostilityCeiling);
		}

		[Test]
		public void Wedding_NeverMarriesSomebodyToThemselves()
		{
			Assert.IsFalse(KingdomHappeningRules.WeddingEligible(Settler(1, 7, 0, 0L, KingdomResidentStanding.Resident),
				Settler(1, 7, 0, 0L, KingdomResidentStanding.Resident), 0, Settled + 1000L));
		}

		// ==================================================================================
		// Funerals
		// ==================================================================================

		[TestCase(KingdomResidentStanding.Dead, KingdomStandingCause.Raid, true)]
		[TestCase(KingdomResidentStanding.Dead, KingdomStandingCause.Founder, true)]
		[TestCase(KingdomResidentStanding.Dead, KingdomStandingCause.Unwitnessed, true)]
		[TestCase(KingdomResidentStanding.Resident, KingdomStandingCause.None, false)]
		[TestCase(KingdomResidentStanding.Abroad, KingdomStandingCause.Followed, false)]
		public void FuneralDue_OnlyForADeathTheMemoryMachineryCanName(KingdomResidentStanding standing, KingdomStandingCause cause, bool expected)
		{
			KingdomResidentRow row = new KingdomResidentRow(4, "Vashti", 1, 0, 100L, 0, 0, 0,
				KingdomDayShape.Hearth, standing, cause, Here, KingdomBrinkWindow.None, KingdomBrinkWindow.None, null, 0);
			Assert.AreEqual(expected, KingdomHappeningRules.FuneralDue(row));
		}

		/// <summary>
		/// The rite clause composes onto the memory machinery's own mourning line and never
		/// restates the death: it must not contain a cause clause, because the sentence it is
		/// appended to already has one.
		/// </summary>
		[Test]
		public void FuneralClause_AddsTheRiteAndNotASecondDeath()
		{
			string mourning = KingdomOfficeRules.MourningChronicle("Vashti", "the hills", "Kavvat", KingdomOfficeRules.DeathCause.Raid);
			string clause = KingdomHappeningRules.FuneralClause("the water-keeper", "Ptoh");
			string composed = mourning + clause;
			Assert.IsTrue(composed.StartsWith(mourning));
			Assert.IsTrue(clause.Contains("Ptoh"));
			Assert.IsFalse(clause.Contains(KingdomOfficeRules.CauseClause(KingdomOfficeRules.DeathCause.Raid)),
				"the rite must not tell the death a second time");
		}

		[Test]
		public void FuneralClause_SaysSoWhenNobodyIsLeftToSpeak()
		{
			string clause = KingdomHappeningRules.FuneralClause("the water-keeper", "");
			Assert.IsTrue(clause.Contains("no one"));
			Assert.AreEqual(clause, KingdomHappeningRules.FuneralClause("", "Ptoh"));
		}

		// ==================================================================================
		// Breakdowns
		// ==================================================================================

		/// <summary>
		/// The condemned line is the housing machinery's, read from the other side. If these two
		/// ever disagree, a building is standing and broken at once.
		/// </summary>
		[Test]
		public void BreakdownFloor_IsTheCondemnedLine()
		{
			Assert.AreEqual(100 - KingdomLodgingRules.CondemnedWearPercent, KingdomHappeningRules.BreakdownConditionFloor);
			Assert.IsTrue(KingdomLodgingRules.IsCondemned(100 - KingdomHappeningRules.BreakdownConditionFloor));
		}

		[TestCase(100, 2, KingdomWorkKind.Producer, false)]
		[TestCase(61, 2, KingdomWorkKind.Producer, false)]
		[TestCase(60, 2, KingdomWorkKind.Producer, true)]
		[TestCase(100, 0, KingdomWorkKind.Producer, true)]
		[TestCase(100, 0, KingdomWorkKind.Refiner, true)]
		[TestCase(100, 0, KingdomWorkKind.Power, true)]
		[TestCase(100, 0, KingdomWorkKind.Store, false)]
		[TestCase(100, 0, KingdomWorkKind.Growing, false)]
		public void Broken_ReadsWearAndHands(int condition, int crew, KingdomWorkKind kind, bool expected)
		{
			Assert.AreEqual(expected, KingdomHappeningRules.Broken(Work(3, condition, crew, kind)));
		}

		/// <summary>A city that already believes the truth has nothing to say. This is the
		/// announce-once half of the breakdown lane, and without it a stopped mill would be
		/// announced on every slice for as long as it stood.</summary>
		[Test]
		public void Judge_SaysNothingWhenTheCityAlreadyBelievesTheTruth()
		{
			Assert.IsFalse(KingdomHappeningRules.Judge(Work(3, 100, 2, KingdomWorkKind.Producer), false, 900L).Stands);
			Assert.IsFalse(KingdomHappeningRules.Judge(Work(3, 10, 2, KingdomWorkKind.Producer), true, 900L).Stands);
		}

		[Test]
		public void Judge_BreaksAndThenUnsays()
		{
			KingdomHappening stop = KingdomHappeningRules.Judge(Work(3, 20, 2, KingdomWorkKind.Producer), false, 900L);
			Assert.IsTrue(stop.Stands);
			Assert.AreEqual(KingdomHappeningKind.Breakdown, stop.Kind);
			Assert.AreEqual(3, stop.SubjectA);
			Assert.IsFalse(KingdomHappeningRules.IsMending(stop.Outcome));
			Assert.AreEqual(20, KingdomHappeningRules.ConditionOf(stop.Outcome));

			KingdomHappening mend = KingdomHappeningRules.Judge(Work(3, 100, 2, KingdomWorkKind.Producer), true, 950L);
			Assert.IsTrue(mend.Stands);
			Assert.IsTrue(KingdomHappeningRules.IsMending(mend.Outcome));
			Assert.AreEqual(100, KingdomHappeningRules.ConditionOf(mend.Outcome));
		}

		/// <summary>The sign encoding must survive a condition of zero, which is exactly the
		/// figure a work that has fallen apart carries.</summary>
		[Test]
		public void MendingEncoding_SurvivesZeroCondition()
		{
			KingdomHappening stop = KingdomHappeningRules.Judge(Work(3, 0, 2, KingdomWorkKind.Producer), false, 900L);
			Assert.IsFalse(KingdomHappeningRules.IsMending(stop.Outcome));
			Assert.AreEqual(0, KingdomHappeningRules.ConditionOf(stop.Outcome));
			KingdomHappening mend = KingdomHappeningRules.Judge(Work(3, 0, 2, KingdomWorkKind.Producer), true, 950L);
			Assert.IsFalse(mend.Stands, "a work that is still broken has not been mended");
		}

		[Test]
		public void Judge_RefusesAWorkWithNoId()
		{
			Assert.IsFalse(KingdomHappeningRules.Judge(Work(0, 10, 0, KingdomWorkKind.Producer), false, 900L).Stands);
		}

		// ==================================================================================
		// The ring
		// ==================================================================================

		/// <summary>Every happening kind round-trips through the ring's vocabulary, so a line
		/// written to a save is read back as the thing it was.</summary>
		[TestCase(KingdomHappeningKind.Wedding)]
		[TestCase(KingdomHappeningKind.Funeral)]
		[TestCase(KingdomHappeningKind.Festival)]
		[TestCase(KingdomHappeningKind.Breakdown)]
		public void ToldKinds_RoundTrip(KingdomHappeningKind kind)
		{
			Assert.AreEqual(kind, KingdomHappeningRules.KindOf(KingdomHappeningRules.ToldKindOf(kind)));
		}

		[Test]
		public void NoHappening_HasNoToldKind()
		{
			Assert.AreEqual(KingdomToldKind.None, KingdomHappeningRules.ToldKindOf(KingdomHappeningKind.None));
			Assert.AreEqual(KingdomHappeningKind.None, KingdomHappeningRules.KindOf(KingdomToldKind.Harvest));
		}

		/// <summary>
		/// Announce-once is the ring's job. A wedding already in the ring is already told; the
		/// same two people in the other order are not the same line, which is why the engine edge
		/// always writes the lower id first.
		/// </summary>
		[Test]
		public void AlreadyTold_AsksTheRing()
		{
			KingdomCityState state = Book(new KingdomResidentRow[0], new KingdomWorkRow[0]);
			Assert.IsFalse(KingdomHappeningRules.AlreadyTold(state, KingdomHappeningKind.Wedding, 1, 2));
			KingdomCityState next;
			KingdomCityFault fault;
			Assert.IsTrue(state.TryTell(new KingdomToldRow(KingdomToldKind.Wedding, 900L, 1, 2, Here, 0), out next, out fault));
			Assert.IsTrue(KingdomHappeningRules.AlreadyTold(next, KingdomHappeningKind.Wedding, 1, 2));
			Assert.IsFalse(KingdomHappeningRules.AlreadyTold(next, KingdomHappeningKind.Wedding, 1, 3));
			Assert.IsFalse(KingdomHappeningRules.AlreadyTold(next, KingdomHappeningKind.Festival, 1, 2));
		}

		/// <summary>
		/// The pair ordering is what makes announce-once work for a wedding: the roster is rebuilt
		/// from the ground every pass, so row order is not stable and the ring has to be keyed on
		/// something that is. Store one way round, ask the other, and the same two people marry
		/// twice.
		/// </summary>
		[Test]
		public void PairOrder_IsTheSameWhicheverWayItIsAsked()
		{
			int first;
			int second;
			KingdomHappeningRules.PairOrder(9, 4, out first, out second);
			Assert.AreEqual(4, first);
			Assert.AreEqual(9, second);
			int firstAgain;
			int secondAgain;
			KingdomHappeningRules.PairOrder(4, 9, out firstAgain, out secondAgain);
			Assert.AreEqual(first, firstAgain);
			Assert.AreEqual(second, secondAgain);
		}

		[Test]
		public void PairOrder_SurvivesEqualIds()
		{
			int first;
			int second;
			KingdomHappeningRules.PairOrder(7, 7, out first, out second);
			Assert.AreEqual(7, first);
			Assert.AreEqual(7, second);
		}

		[Test]
		public void AlreadyTold_IsFalseForNothing()
		{
			Assert.IsFalse(KingdomHappeningRules.AlreadyTold(null, KingdomHappeningKind.Wedding, 1, 2));
			Assert.IsFalse(KingdomHappeningRules.AlreadyTold(Book(new KingdomResidentRow[0], new KingdomWorkRow[0]),
				KingdomHappeningKind.None, 0, 0));
		}

		// ==================================================================================
		// The prose
		// ==================================================================================

		/// <summary>The feast names Qud's day and the realm's own dish, and says how many ate.
		/// A city with nothing in the larders says so rather than pretending.</summary>
		[Test]
		public void FestivalTelling_NamesTheDayAndTheDish()
		{
			string line = KingdomHappeningRules.FestivalTelling(KingdomFestivalAnchor.UtYaraUx, "Kavvat", "apple matz", 12);
			Assert.IsTrue(line.Contains("Ut yara Ux"));
			Assert.IsTrue(line.Contains("apple matz"));
			Assert.IsTrue(line.Contains("12"));
			Assert.IsTrue(KingdomHappeningRules.FestivalTelling(KingdomFestivalAnchor.Ides, "Kavvat", "", 0).Contains("bare"));
		}

		[Test]
		public void AnchorNames_AreQudsOwn()
		{
			Assert.AreEqual("the Ides", KingdomHappeningRules.AnchorName(KingdomFestivalAnchor.Ides));
			Assert.AreEqual("the festival of Ut yara Ux", KingdomHappeningRules.AnchorName(KingdomFestivalAnchor.UtYaraUx));
			Assert.AreEqual("", KingdomHappeningRules.AnchorName(KingdomFestivalAnchor.None));
		}

		[Test]
		public void BreakdownProse_NamesTheThingThatStopped()
		{
			Assert.IsTrue(KingdomHappeningRules.BreakdownNotice("mill", 30).Contains("mill"));
			Assert.IsTrue(KingdomHappeningRules.BreakdownNotice("mill", 30).Contains("30"));
			Assert.IsTrue(KingdomHappeningRules.MendedNotice("mill", 90).Contains("mill"));
			Assert.IsTrue(KingdomHappeningRules.BreakdownTelling(null, "Kavvat", 12).Contains("works"),
				"a work nobody named still gets an honest noun");
		}

		[Test]
		public void WeddingProse_NamesBoth()
		{
			Assert.IsTrue(KingdomHappeningRules.WeddingTelling("Ptoh", "Vashti", "Kavvat").Contains("Ptoh"));
			Assert.IsTrue(KingdomHappeningRules.WeddingTelling("Ptoh", "Vashti", "Kavvat").Contains("Vashti"));
			Assert.IsTrue(KingdomHappeningRules.WeddingNotice("Ptoh", "Vashti").Contains("Vashti"));
		}

		[TestCase(KingdomToldKind.Wedding)]
		[TestCase(KingdomToldKind.Funeral)]
		[TestCase(KingdomToldKind.Festival)]
		[TestCase(KingdomToldKind.Breakdown)]
		public void ToldLine_CountsAndSaysNothingForNone(KingdomToldKind kind)
		{
			Assert.AreEqual("", KingdomHappeningRules.ToldLine(kind, 0));
			Assert.AreNotEqual("", KingdomHappeningRules.ToldLine(kind, 1));
			Assert.IsTrue(KingdomHappeningRules.ToldLine(kind, 4).Contains("4"));
		}

		[Test]
		public void ToldLine_SaysNothingAboutAKindItDoesNotReport()
		{
			Assert.AreEqual("", KingdomHappeningRules.ToldLine(KingdomToldKind.Harvest, 3));
		}
	}
}
#endif
