#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class LocusRulesTests
	{
		// ---- Keeper mood: precedence, worst-first ----

		[TestCase(false, false, false, false, KingdomLocusRules.KeeperMood.Peaceful)]
		[TestCase(false, false, false, true, KingdomLocusRules.KeeperMood.Growing)]
		[TestCase(false, false, true, false, KingdomLocusRules.KeeperMood.Raided)]
		[TestCase(false, false, true, true, KingdomLocusRules.KeeperMood.Raided)]
		[TestCase(false, true, false, false, KingdomLocusRules.KeeperMood.Threatened)]
		[TestCase(false, true, true, true, KingdomLocusRules.KeeperMood.Threatened)]
		[TestCase(true, false, false, false, KingdomLocusRules.KeeperMood.Thirsty)]
		[TestCase(true, true, true, true, KingdomLocusRules.KeeperMood.Thirsty)]
		public void ClassifyMood_PicksWorstFirst(bool dryStreak, bool raidIncoming, bool recentlyRaided, bool grew, KingdomLocusRules.KeeperMood expected)
		{
			Assert.AreEqual(expected, KingdomLocusRules.ClassifyMood(dryStreak, raidIncoming, recentlyRaided, grew));
		}

		[Test]
		public void ClassifyMood_ThirstAloneOutranksEverythingElseAtOnce()
		{
			// If any single condition were inverted here the thirsty branch would stop firing;
			// this pins thirst as the true top of the precedence order, not an artefact of the
			// paired cases above.
			Assert.AreEqual(KingdomLocusRules.KeeperMood.Thirsty, KingdomLocusRules.ClassifyMood(true, true, true, true));
		}

		// ---- Recent raid window ----

		[TestCase(0L, 5000L, false)]
		[TestCase(-1L, 5000L, false)]
		[TestCase(4000L, 4000L, true)]
		[TestCase(4000L, 4000L + KingdomLocusRules.RecentRaidWindowTicks - 1, true)]
		[TestCase(4000L, 4000L + KingdomLocusRules.RecentRaidWindowTicks, false)]
		[TestCase(4000L, 4000L + KingdomLocusRules.RecentRaidWindowTicks * 10, false)]
		public void WasRecentlyRaided_RespectsWindowAndNeverFiresWithoutARaid(long lastRaidTick, long timeTicks, bool expected)
		{
			Assert.AreEqual(expected, KingdomLocusRules.WasRecentlyRaided(lastRaidTick, timeTicks));
		}

		// ---- Keeper selection ----

		[Test]
		public void SelectKeeper_EmptyCandidateListYieldsNull()
		{
			Assert.IsNull(KingdomLocusRules.SelectKeeper(new System.Collections.Generic.List<string>(), null));
		}

		[Test]
		public void SelectKeeper_NoCurrentKeeperPicksFirstCandidate()
		{
			System.Collections.Generic.List<string> candidates = new System.Collections.Generic.List<string> { "a", "b", "c" };
			Assert.AreEqual("a", KingdomLocusRules.SelectKeeper(candidates, null));
		}

		[Test]
		public void SelectKeeper_KeepsCurrentKeeperEvenWhenNotFirst()
		{
			System.Collections.Generic.List<string> candidates = new System.Collections.Generic.List<string> { "a", "b", "c" };
			Assert.AreEqual("b", KingdomLocusRules.SelectKeeper(candidates, "b"));
		}

		[Test]
		public void SelectKeeper_FallsBackToFirstWhenCurrentKeeperHasLeft()
		{
			System.Collections.Generic.List<string> candidates = new System.Collections.Generic.List<string> { "a", "b", "c" };
			Assert.AreEqual("a", KingdomLocusRules.SelectKeeper(candidates, "gone"));
		}

		// ---- Guest cadence and eligibility ----

		[TestCase(999L, 1000L, false)]
		[TestCase(1000L, 1000L, true)]
		[TestCase(1001L, 1000L, true)]
		public void GuestShouldArrive_TripsAtTheDueTickNotBeforeIt(long timeTicks, long nextGuestTick, bool expected)
		{
			Assert.AreEqual(expected, KingdomLocusRules.GuestShouldArrive(timeTicks, nextGuestTick));
		}

		[Test]
		public void NextGuestDueTick_AddsTheFullInterval()
		{
			Assert.AreEqual(5000L + KingdomLocusRules.GuestIntervalTicks, KingdomLocusRules.NextGuestDueTick(5000L));
		}

		[Test]
		public void GuestDepartTickFor_AddsTheFullPatience()
		{
			Assert.AreEqual(2000L + KingdomLocusRules.GuestPatienceTicks, KingdomLocusRules.GuestDepartTickFor(2000L));
		}

		[TestCase(5000L, 0L, false)]
		[TestCase(5000L, -1L, false)]
		[TestCase(4999L, 5000L, false)]
		[TestCase(5000L, 5000L, true)]
		[TestCase(5001L, 5000L, true)]
		public void GuestShouldDepartUnattended_NeverFiresWithoutATrackedGuest(long timeTicks, long departTick, bool expected)
		{
			Assert.AreEqual(expected, KingdomLocusRules.GuestShouldDepartUnattended(timeTicks, departTick));
		}

		// ---- Flavor text: distinct per branch, and never loses the settlement's name ----

		[Test]
		public void BenchDescription_UnstaffedNamesNoKeeper()
		{
			string text = KingdomLocusRules.BenchDescription(false, null);
			StringAssert.DoesNotContain("null", text);
			Assert.IsFalse(string.IsNullOrEmpty(text));
		}

		[Test]
		public void BenchDescription_StaffedNamesTheKeeper()
		{
			string text = KingdomLocusRules.BenchDescription(true, "Ashwe");
			StringAssert.Contains("Ashwe", text);
		}

		[TestCase(KingdomLocusRules.KeeperMood.Peaceful)]
		[TestCase(KingdomLocusRules.KeeperMood.Growing)]
		[TestCase(KingdomLocusRules.KeeperMood.Raided)]
		[TestCase(KingdomLocusRules.KeeperMood.Threatened)]
		[TestCase(KingdomLocusRules.KeeperMood.Thirsty)]
		public void KeeperSpeechFor_NamesTheSettlementInTheAnswer(KingdomLocusRules.KeeperMood mood)
		{
			KingdomLocusRules.KeeperSpeech speech = KingdomLocusRules.KeeperSpeechFor(mood, "Tamsketh");
			StringAssert.Contains("Tamsketh", speech.Answer);
			Assert.AreEqual(KingdomLocusRules.KeeperQuestion, speech.Question);
		}

		[Test]
		public void GuestChronicleLine_DiffersByWhetherTheGuestWasGreeted()
		{
			string greeted = KingdomLocusRules.GuestChronicleLine(true, "Tamsketh");
			string ignored = KingdomLocusRules.GuestChronicleLine(false, "Tamsketh");
			Assert.AreNotEqual(greeted, ignored);
			StringAssert.Contains("Tamsketh", greeted);
			StringAssert.Contains("Tamsketh", ignored);
		}

		// ---- Travellers who came and went through an absence ----

		[Test]
		public void GuestPatienceIsShorterThanTheIntervalSoOnlyOneIsEverStanding()
		{
			// Load-bearing, not a coincidence. It is what makes KingdomRules.PassagesThrough's
			// "at most one still at the gate" true of this clock, and it is what an existing
			// guest blocking the next one used to buy by accident.
			Assert.Less(KingdomLocusRules.GuestPatienceTicks, KingdomLocusRules.GuestIntervalTicks);
			Assert.Greater(KingdomLocusRules.GuestPatienceTicks, 0L);
		}

		[Test]
		public void ASeasonAwayIsAWholeSeasonOfTravellersAndNobodyAtTheGate()
		{
			// The row. A two-hundred-day absence and a three-day one used to produce the same
			// single guest, standing in the square as though they had just walked up. Now the
			// road runs through the absence: everybody's patience ran out, so what the founder
			// finds is the news and not a stranger.
			long due = KingdomLocusRules.GuestIntervalTicks;
			long now = due + KingdomRules.TicksPerDay * 200;
			KingdomRules.Passages passages = KingdomRules.PassagesThrough(
				due, now, KingdomLocusRules.GuestIntervalTicks, KingdomLocusRules.GuestPatienceTicks);
			Assert.Greater(passages.Departed, 60, "two hundred days at a three-day cadence is not one traveller");
			Assert.AreEqual(0L, passages.StandingSince, "somebody was left waiting at the gate for a season");
		}

		[Test]
		public void AGuestWhoJustWalkedUpIsStillThereAndKeepsTheirOwnArrivalTick()
		{
			// The other side of it: the founder who comes home minutes after a traveller arrived
			// still meets them, and that traveller's patience is already partly spent because it
			// started when they actually arrived.
			long due = KingdomLocusRules.GuestIntervalTicks;
			KingdomRules.Passages passages = KingdomRules.PassagesThrough(
				due, due + 60L, KingdomLocusRules.GuestIntervalTicks, KingdomLocusRules.GuestPatienceTicks);
			Assert.AreEqual(due, passages.StandingSince);
			Assert.AreEqual(0, passages.Departed);
			Assert.AreEqual(due + KingdomLocusRules.GuestPatienceTicks, KingdomLocusRules.GuestDepartTickFor(passages.StandingSince),
				"their patience was restarted at the homecoming instead of at their arrival");
		}

		[TestCase(0, "the last of them today")]
		[TestCase(-4, "the last of them today")]
		[TestCase(1, "the last of them a day before you saw it")]
		[TestCase(9, "the last of them 9 days before you saw it")]
		public void PassageWhen_DatesAgainstTheDayTheFounderIsBeingTold(int daysAgo, string expected)
		{
			Assert.AreEqual(expected, KingdomLocusRules.PassageWhen(daysAgo));
		}

		[Test]
		public void PassagesLedgerNote_IsOneDatedLineForTheWholeRun()
		{
			// Honest dating, and a chronicle budget: a season of ambient traffic is one line, and
			// the line carries a real number of days rather than "recently".
			string many = KingdomLocusRules.PassagesLedgerNote(31, 2);
			StringAssert.Contains("31", many);
			StringAssert.Contains("2 days before you saw it", many);
			StringAssert.Contains("Nothing was lost", many);
			string one = KingdomLocusRules.PassagesLedgerNote(1, 2);
			StringAssert.Contains("A traveller", one);
			Assert.IsFalse(one.Contains("1 travellers"), "the singular case read as a plural");
		}

		[Test]
		public void PassagesLedgerNote_SaysNothingWhenNobodyCame()
		{
			// STANDARDS 7b's "not applicable" case: an absence with no traffic in it is not news,
			// and the null is the caller's signal to stay quiet rather than say "0 travellers".
			Assert.IsNull(KingdomLocusRules.PassagesLedgerNote(0, 4));
			Assert.IsNull(KingdomLocusRules.PassagesLedgerNote(-2, 4));
			Assert.IsNull(KingdomLocusRules.PassagesChronicleLine(0, "Tamsketh", 4));
		}

		[Test]
		public void PassagesChronicleLine_NamesThePlaceAndBlamesNobody()
		{
			string line = KingdomLocusRules.PassagesChronicleLine(4, "Tamsketh", 6);
			StringAssert.Contains("Tamsketh", line);
			StringAssert.Contains("4 travellers", line);
			StringAssert.Contains("6 days before you saw it", line);
			// An unanswered gate is a missed pleasantry, never a fault logged against the founder.
			Assert.IsFalse(line.Contains("failed"), "the register started blaming somebody");
			Assert.IsFalse(line.Contains("lost"), "the register started counting a loss");
		}

		[Test]
		public void GuestLedgerNote_DatesTheDepartureAgainstTheDayItActuallyHappened()
		{
			// A guest standing at the gate when the founder left gave up when their patience ran
			// out, not when somebody finally walked back in. The undated overload is the same
			// sentence for a departure noticed the day it happened.
			string dated = KingdomLocusRules.GuestLedgerNote("Aeru", 12);
			StringAssert.Contains("Aeru", dated);
			StringAssert.Contains("12 days before you saw it", dated);
			StringAssert.Contains("Nothing was lost", dated);
			Assert.AreEqual(KingdomLocusRules.GuestLedgerNote("Aeru"), KingdomLocusRules.GuestLedgerNote("Aeru", 0));
			Assert.AreEqual(KingdomLocusRules.GuestLedgerNote("Aeru"), KingdomLocusRules.GuestLedgerNote("Aeru", -5));
			StringAssert.Contains("a day before you saw it", KingdomLocusRules.GuestLedgerNote("Aeru", 1));
		}

		// ---- Pilgrims of the told story: typed cause, one opportunity, one heart ----

		[Test]
		public void ThreeQualifyingStoriesMintExactlyOneCausalOpportunity()
		{
			KingdomLocusRules.PilgrimAccrual one = KingdomLocusRules.AccruePilgrim(
				0, KingdomLocusRules.PilgrimState.None);
			Assert.AreEqual(1, one.Loudness);
			Assert.AreEqual(KingdomLocusRules.PilgrimState.None, one.State);
			Assert.IsFalse(one.Minted);

			KingdomLocusRules.PilgrimAccrual two = KingdomLocusRules.AccruePilgrim(
				one.Loudness, one.State);
			Assert.AreEqual(2, two.Loudness);
			Assert.IsFalse(two.Minted);

			KingdomLocusRules.PilgrimAccrual three = KingdomLocusRules.AccruePilgrim(
				two.Loudness, two.State);
			Assert.AreEqual(0, three.Loudness);
			Assert.AreEqual(KingdomLocusRules.PilgrimState.Waiting, three.State);
			Assert.IsTrue(three.Minted);
		}

		[TestCase(KingdomLocusRules.PilgrimState.Waiting)]
		[TestCase(KingdomLocusRules.PilgrimState.Standing)]
		public void AnOpenPilgrimIsNeverOverwrittenByLaterHistory(
			KingdomLocusRules.PilgrimState state)
		{
			KingdomLocusRules.PilgrimAccrual result = KingdomLocusRules.AccruePilgrim(2, state);
			Assert.AreEqual(state, result.State);
			Assert.AreEqual(2, result.Loudness);
			Assert.IsFalse(result.Minted);
		}

		[Test]
		public void PilgrimWindowHasTravelThenOneExactPatienceSpan()
		{
			long cause = 5000L;
			Assert.IsTrue(KingdomLocusRules.TryPilgrimWindow(cause,
				out long arrival, out long depart));
			Assert.AreEqual(cause + KingdomLocusRules.PilgrimTravelTicks, arrival);
			Assert.AreEqual(arrival + KingdomLocusRules.GuestPatienceTicks, depart);
			Assert.IsFalse(KingdomLocusRules.TryPilgrimWindow(0L, out _, out _));
			Assert.IsFalse(KingdomLocusRules.TryPilgrimWindow(long.MaxValue, out _, out _));
		}

		[Test]
		public void PilgrimCauseIsNamedBoundedAndSafeForOneLine()
		{
			string cause = KingdomLocusRules.PilgrimCause("Ides\nof Nivvun Ut", "Tamsketh",
				new string('q', KingdomLocusRules.MaxPilgrimCauseChars * 2));
			StringAssert.Contains("Ides of Nivvun Ut", cause);
			StringAssert.Contains("Tamsketh", cause);
			Assert.LessOrEqual(cause.Length, KingdomLocusRules.MaxPilgrimCauseChars);
			Assert.IsFalse(cause.Contains("\n"));
			Assert.IsNull(KingdomLocusRules.PilgrimCause(null, "Tamsketh", "starapple"));
		}

		[Test]
		public void PilgrimTellingCarriesTheFrozenCauseAndOutcome()
		{
			string cause = "the Ides feast kept at Tamsketh over starapple jam";
			string greeted = KingdomLocusRules.PilgrimChronicleLine("Aeru", "Tamsketh",
				cause, true);
			string missed = KingdomLocusRules.PilgrimChronicleLine("Aeru", "Tamsketh",
				cause, false);
			StringAssert.Contains("Aeru", greeted);
			StringAssert.Contains(cause, greeted);
			StringAssert.Contains("given water", greeted);
			StringAssert.Contains("unmet", missed);
			Assert.AreNotEqual(greeted, missed);
		}

		[Test]
		public void ProductionPilgrimIsCausalAndHeartBoundNotAGenericRoll()
		{
			string populations = TestMain.ReadRepositoryText("PopulationTables.xml");
			StringAssert.DoesNotContain("Blueprint=\"r_KingdomGuestPilgrim\"", populations);

			string happenings = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomHappenings.cs");
			StringAssert.Contains("RecordDisputed", happenings);
			StringAssert.Contains("KingdomLocusRules.PilgrimCause", happenings);
			string physical = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomPhysicalHappenings.cs");
			StringAssert.Contains("KingdomHappenings.AccruePilgrim", physical);
			StringAssert.Contains("BeginUninspectable(book, operation.EventId, SinkLane.Effect",
				physical);

			string locus = TestMain.ReadRepositoryText("Experience/KingdomLocus.cs");
			StringAssert.Contains("RunPilgrimPass", locus);
			StringAssert.Contains("KingdomPlots.TryRiteGround", locus);
			StringAssert.Contains("HeartArrivalCell", locus);
			StringAssert.Contains("KingdomGuestLifecycle.PublishSpawn", locus);
			StringAssert.Contains("KingdomGuestLifecycle.PublishMissedCausal", locus);
			StringAssert.Contains("KingdomGuestLifecycle.PublishOfferWater", locus);
			StringAssert.DoesNotContain("GetEmptyCells", locus);
		}
	}
}
#endif
