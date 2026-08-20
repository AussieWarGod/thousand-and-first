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
	}
}
#endif
