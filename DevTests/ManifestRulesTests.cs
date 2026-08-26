#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class ManifestRulesTests
	{
		[Test]
		public void SplitManifestRecordRetainsPublicFieldOrderAndDefaults()
		{
			System.Reflection.FieldInfo[] fields = typeof(KingdomManifest).GetFields(
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
			CollectionAssert.AreEqual(new string[]
			{
				"OriginName", "DestinationName", "Drams", "LoadedTick", "DeadlineTick",
				"TurnedBack"
			}, System.Array.ConvertAll(fields, field => field.Name));
			KingdomManifest manifest = new KingdomManifest();
			Assert.IsNull(manifest.OriginName);
			Assert.IsNull(manifest.DestinationName);
			Assert.AreEqual(0, manifest.Drams);
			Assert.AreEqual(0L, manifest.LoadedTick);
			Assert.AreEqual(0L, manifest.DeadlineTick);
			Assert.IsFalse(manifest.TurnedBack);
		}

		// --- ManifestReserve / ManifestAmount: the size arithmetic ---------------------------

		[TestCase(0)]
		[TestCase(40)]
		[TestCase(100)]
		public void ManifestReserve_IsThreeDaysUpkeep(int population)
		{
			int expected = KingdomRules.UpkeepDrams(population) * KingdomManifestRules.ReserveUpkeepDays;
			Assert.AreEqual(expected, KingdomManifestRules.ManifestReserve(population));
		}

		// The boundaries are computed from the rule, not written as literals. These pinned a
		// population-40 reserve of 30 drams, which silently became wrong the moment upkeep was
		// retuned - a test whose expectations are hand-copied from a tuning constant fails on
		// every retune and proves nothing about the law it claims to cover.

		[Test]
		public void ManifestAmount_NothingStoredSendsNothing()
		{
			Assert.AreEqual(0, KingdomManifestRules.ManifestAmount(0, 0));
		}

		[Test]
		public void ManifestAmount_NoMouthsMeansNoReserve()
		{
			Assert.AreEqual(10, KingdomManifestRules.ManifestAmount(10, 0));
		}

		[Test]
		public void ManifestAmount_HoldsTheReserveBackToTheDram()
		{
			const int population = 40;
			int reserve = KingdomManifestRules.ManifestReserve(population);
			Assert.Greater(reserve, 0, "a settlement with mouths must reserve something");
			Assert.AreEqual(0, KingdomManifestRules.ManifestAmount(reserve - 1, population), "sent water it needed");
			Assert.AreEqual(0, KingdomManifestRules.ManifestAmount(reserve, population), "sent the reserve itself");
			Assert.AreEqual(1, KingdomManifestRules.ManifestAmount(reserve + 1, population), "held back water it could spare");
		}

		[Test]
		public void ManifestAmount_CapsWhateverIsSpare()
		{
			const int population = 40;
			int reserve = KingdomManifestRules.ManifestReserve(population);
			int cap = KingdomManifestRules.MaximumManifestDrams;
			Assert.AreEqual(cap, KingdomManifestRules.ManifestAmount(reserve + cap, population));
			Assert.AreEqual(cap, KingdomManifestRules.ManifestAmount(reserve + cap + 1, population));
			Assert.AreEqual(cap, KingdomManifestRules.ManifestAmount(reserve + cap * 10, population));
		}

		[Test]
		public void ManifestAmount_ReserveGrowsWithTheSettlement()
		{
			// A bigger settlement holds more back, because it drinks more. If this ever inverts,
			// a large city would ship away water it needed to survive.
			Assert.Greater(KingdomManifestRules.ManifestReserve(40), KingdomManifestRules.ManifestReserve(10));
		}

		// --- ManifestDeadline / ManifestExpired: the deadline arithmetic ---------------------

		[TestCase(0L, KingdomManifestRules.ManifestWindowTicks)]
		[TestCase(5000L, 5000L + KingdomManifestRules.ManifestWindowTicks)]
		public void ManifestDeadline_AddsTheWindowToTheLoadedTick(long loadedTick, long expected)
		{
			Assert.AreEqual(expected, KingdomManifestRules.ManifestDeadline(loadedTick));
		}

		[TestCase(99L, 100L, false)]
		[TestCase(100L, 100L, false)] // arriving exactly on the deadline still delivers
		[TestCase(101L, 100L, true)] // one tick past it does not
		public void ManifestExpired_IsStrictlyPastTheDeadline(long now, long deadlineTick, bool expected)
		{
			Assert.AreEqual(expected, KingdomManifestRules.ManifestExpired(now, deadlineTick));
		}

		[Test]
		public void ManifestWindowTicks_MatchesDaysTimesTicksPerDay()
		{
			// A mutation that decouples the constant from TicksPerDay would silently detune the
			// window if TicksPerDay ever changes elsewhere.
			Assert.AreEqual(KingdomRules.TicksPerDay * KingdomManifestRules.ManifestWindowDays, KingdomManifestRules.ManifestWindowTicks);
		}

		// --- ManifestDaysLeft: rounds up, never reports a day with nothing left --------------

		[TestCase(0L, 0L, 0L)] // deadline reached exactly: no days left to report
		[TestCase(0L, 1L, 1L)] // a single tick still reads as one day, not zero
		[TestCase(0L, KingdomRules.TicksPerDay, 1L)] // exactly one day's worth of ticks
		[TestCase(0L, KingdomRules.TicksPerDay + 1, 2L)] // one tick over a day rounds up to two
		[TestCase(100L, 50L, 0L)] // already past the deadline: never negative
		public void ManifestDaysLeft_RoundsUpAndFloorsAtZero(long now, long deadlineTick, long expected)
		{
			Assert.AreEqual(expected, KingdomManifestRules.ManifestDaysLeft(now, deadlineTick));
		}

		// --- JudgeManifest: eligibility, in priority order ------------------------------------

		[TestCase(false, false, false, 0, KingdomManifestRules.ManifestVerdict.NotOnClaimedGround)]
		[TestCase(false, true, true, 50, KingdomManifestRules.ManifestVerdict.NotOnClaimedGround)] // ground outranks everything else
		[TestCase(true, true, true, 50, KingdomManifestRules.ManifestVerdict.AlreadyInFlight)] // in-flight outranks city count and amount
		[TestCase(true, false, false, 50, KingdomManifestRules.ManifestVerdict.OnlyOneCity)]
		[TestCase(true, true, false, 0, KingdomManifestRules.ManifestVerdict.StoresCannotSpare)]
		[TestCase(true, true, false, 1, KingdomManifestRules.ManifestVerdict.Allowed)]
		public void JudgeManifest_ChecksInPriorityOrder(bool onClaimedGround, bool hasSecondCity, bool alreadyInFlight, int amount, KingdomManifestRules.ManifestVerdict expected)
		{
			Assert.AreEqual(expected, KingdomManifestRules.JudgeManifest(onClaimedGround, hasSecondCity, alreadyInFlight, amount));
		}

		// --- ManifestRefusal: every non-allowed, non-in-flight verdict has something to say --

		[Test]
		public void ManifestRefusal_AllowedAndAlreadyInFlightAreEmpty()
		{
			// Allowed has nothing to refuse; AlreadyInFlight is composed by
			// ManifestInFlightStatus instead, because it needs the standing manifest's own
			// details that this verdict-only overload does not carry.
			Assert.AreEqual("", KingdomManifestRules.ManifestRefusal(KingdomManifestRules.ManifestVerdict.Allowed, "Ashkell"));
			Assert.AreEqual("", KingdomManifestRules.ManifestRefusal(KingdomManifestRules.ManifestVerdict.AlreadyInFlight, "Ashkell"));
		}

		[TestCase(KingdomManifestRules.ManifestVerdict.NotOnClaimedGround)]
		[TestCase(KingdomManifestRules.ManifestVerdict.OnlyOneCity)]
		[TestCase(KingdomManifestRules.ManifestVerdict.StoresCannotSpare)]
		public void ManifestRefusal_EveryOtherVerdictSaysSomething(KingdomManifestRules.ManifestVerdict verdict)
		{
			Assert.IsNotEmpty(KingdomManifestRules.ManifestRefusal(verdict, "Ashkell"));
		}

		[Test]
		public void ManifestRefusal_StoresCannotSpareNamesTheDestinationWhenKnown()
		{
			string named = KingdomManifestRules.ManifestRefusal(KingdomManifestRules.ManifestVerdict.StoresCannotSpare, "Ashkell");
			StringAssert.Contains("Ashkell", named);
			string unnamed = KingdomManifestRules.ManifestRefusal(KingdomManifestRules.ManifestVerdict.StoresCannotSpare, null);
			Assert.IsNotEmpty(unnamed);
			StringAssert.DoesNotContain("Ashkell", unnamed);
		}

		// --- ManifestInFlightStatus: the "one is already on the road" report -----------------

		[Test]
		public void ManifestInFlightStatus_NamesOriginDestinationAndAmount()
		{
			string status = KingdomManifestRules.ManifestInFlightStatus("Ashkell", "Kavvat", 40, 0L, KingdomManifestRules.ManifestWindowTicks);
			StringAssert.Contains("Ashkell", status);
			StringAssert.Contains("Kavvat", status);
			StringAssert.Contains("40", status);
			StringAssert.Contains(KingdomManifestRules.ManifestWindowDays.ToString(), status);
		}

		[Test]
		public void ManifestInFlightStatus_ReadsAsClosingWhenNoDaysRemain()
		{
			string status = KingdomManifestRules.ManifestInFlightStatus("Ashkell", "Kavvat", 40, 12000L, 12000L);
			StringAssert.Contains("nearly closed", status);
		}

		// --- Chronicle and ledger prose: lower-case clauses, no trailing period ---------------

		[Test]
		public void ManifestLapseDeed_IsALowerCaseClauseWithNoTrailingPeriod()
		{
			string deed = KingdomManifestRules.ManifestLapseDeed("Ashkell", "Kavvat", 40);
			Assert.IsFalse(deed.EndsWith("."), "KingdomChronicle.Record appends its own period.");
			StringAssert.Contains("40", deed);
			StringAssert.Contains("Ashkell", deed);
			StringAssert.Contains("Kavvat", deed);
		}

		[TestCase(40, 40, false)] // everything sent was held
		[TestCase(30, 40, true)] // the stores could not hold all of it
		public void ManifestArrivalDeed_NotesOverflowOnlyWhenSomeIsMissing(int delivered, int sent, bool expectOverflowNote)
		{
			string deed = KingdomManifestRules.ManifestArrivalDeed("Ashkell", "Kavvat", delivered, sent);
			Assert.IsFalse(deed.EndsWith("."));
			StringAssert.Contains(delivered.ToString(), deed);
			Assert.AreEqual(expectOverflowNote, deed.Contains("not all of it could be held"));
		}

		[TestCase(40, 40, false)]
		[TestCase(30, 40, true)]
		public void ManifestArrivalNote_MatchesTheOverflowFlagOnArrivalDeed(int delivered, int sent, bool expectOverflowNote)
		{
			string note = KingdomManifestRules.ManifestArrivalNote("Ashkell", delivered, sent);
			StringAssert.Contains(delivered.ToString(), note);
			Assert.AreEqual(expectOverflowNote, note.Contains("overflowed"));
		}

		// --- Loading against belief, not truth --------------------------------------------

		[TestCase(60, 200, 60)]
		[TestCase(60, 20, 20)]
		[TestCase(60, 0, 0)]
		[TestCase(60, -5, 0)]
		[TestCase(0, 100, 0)]
		public void CapToDestination_NeverLoadsMoreThanTheOtherCityWasKnownToHold(int amount, int space, int expected)
		{
			Assert.AreEqual(expected, KingdomManifestRules.CapToDestination(amount, space));
		}

		[Test]
		public void CapToDestination_NeverInventsWater()
		{
			// The cap only ever reduces. A destination with room to spare does not enlarge a load
			// the origin could not spare in the first place.
			for (int amount = 0; amount <= 80; amount += 7)
			{
				for (int space = 0; space <= 300; space += 37)
				{
					Assert.LessOrEqual(KingdomManifestRules.CapToDestination(amount, space), amount);
				}
			}
		}

		[Test]
		public void JudgeManifest_RefusesWhenTheOtherCityWasKnownToHaveNoRoom()
		{
			Assert.AreEqual(KingdomManifestRules.ManifestVerdict.DestinationHasNoRoom,
				KingdomManifestRules.JudgeManifest(true, true, false, 40, 0));
		}

		[Test]
		public void JudgeManifest_KnownRoomAllowsTheLoad()
		{
			Assert.AreEqual(KingdomManifestRules.ManifestVerdict.Allowed,
				KingdomManifestRules.JudgeManifest(true, true, false, 40, 40));
		}

		[Test]
		public void JudgeManifest_CannotSpareOutranksNoRoom()
		{
			// A founder who cannot spare the water is told that, not told about the other city's
			// casks: the nearer refusal is the useful one.
			Assert.AreEqual(KingdomManifestRules.ManifestVerdict.StoresCannotSpare,
				KingdomManifestRules.JudgeManifest(true, true, false, 0, 0));
		}

		[Test]
		public void EveryRefusalSaysSomething()
		{
			foreach (KingdomManifestRules.ManifestVerdict verdict in System.Enum.GetValues(typeof(KingdomManifestRules.ManifestVerdict)))
			{
				if (verdict == KingdomManifestRules.ManifestVerdict.Allowed || verdict == KingdomManifestRules.ManifestVerdict.AlreadyInFlight)
				{
					continue;
				}
				Assert.IsNotEmpty(KingdomManifestRules.ManifestRefusal(verdict, "Kavvat"),
					verdict + " refuses without telling the founder why");
			}
		}

	}
}
#endif
