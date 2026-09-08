#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomCreedRulesTests
	{
		private static void AssertPublicIntEnum(Type type, params string[] expected)
		{
			ClassicAssert.AreEqual(typeof(int), Enum.GetUnderlyingType(type), type.Name + " backing type");
			ClassicAssert.AreEqual("ThousandAndFirst." + type.Name, type.FullName);
			ClassicAssert.IsTrue(type.IsPublic, type.Name + " accessibility changed");
			ClassicAssert.IsFalse(type.IsNested, type.Name + " became nested");
			string[] names = Enum.GetNames(type);
			string[] actual = new string[names.Length];
			for (int i = 0; i < names.Length; i++)
				actual[i] = names[i] + "=" + Convert.ToInt32(Enum.Parse(type, names[i]));
			CollectionAssert.AreEqual(expected, actual, type.Name + " values/order");
		}

		[Test]
		public void CreedPublicEnumsAndConstantsKeepExactAbiValues()
		{
			AssertPublicIntEnum(typeof(CityTemper), "Concord=0", "Muttering=1", "Quarrel=2",
				"Rupture=3", "Secession=4");
			AssertPublicIntEnum(typeof(SecessionVerdict), "Warranted=0", "OneCity=1",
				"NoClash=2", "DissentHolds=3");
			AssertPublicIntEnum(typeof(RejoinVerdict), "Allowed=0", "NothingSeceded=1",
				"RealmIsFull=2", "NotOnTheirGround=3", "ClashStillLive=4", "StandingTooLow=5");
			ClassicAssert.AreEqual("ThousandAndFirst.KingdomCreedRules", typeof(KingdomCreedRules).FullName);
			ClassicAssert.IsTrue(typeof(KingdomCreedRules).IsPublic);
			ClassicAssert.IsTrue(typeof(KingdomCreedRules).IsAbstract && typeof(KingdomCreedRules).IsSealed);

			ClassicAssert.AreEqual(100, KingdomCreedRules.OrdinaryWeight);
			ClassicAssert.AreEqual(8, KingdomCreedRules.AffinityPerResident);
			ClassicAssert.AreEqual(60, KingdomCreedRules.DeclaredBonus);
			ClassicAssert.AreEqual(3, KingdomCreedRules.MinBelievers);
			ClassicAssert.AreEqual(33, KingdomCreedRules.DominantSharePercent);
			ClassicAssert.AreEqual(3, KingdomCreedRules.MaxKeptCreeds);
			ClassicAssert.AreEqual('|', KingdomCreedRules.KeptSeparator);
			ClassicAssert.AreEqual(25, KingdomCreedRules.HostilityPerDissentPoint);
			ClassicAssert.AreEqual(100, KingdomCreedRules.DissentBreaking);
			ClassicAssert.AreEqual(70, KingdomCreedRules.DissentRupture);
			ClassicAssert.AreEqual(45, KingdomCreedRules.DissentQuarrel);
			ClassicAssert.AreEqual(20, KingdomCreedRules.DissentMuttering);
			ClassicAssert.AreEqual(3, KingdomCreedRules.RiteCooldownDays);
			ClassicAssert.AreEqual(9, KingdomCreedRules.SecessionWindowDays);
			ClassicAssert.AreEqual(12, KingdomCreedRules.MealEase);
			ClassicAssert.AreEqual(20, KingdomCreedRules.DeclarationShock);
			ClassicAssert.AreEqual(-150, KingdomCreedRules.DeclarationStandingCost);
		}

		// ---- what pulls a settler toward a creed -------------------------------------------

		// Both sides of every tier boundary. The tiers are vanilla's own reputation thresholds,
		// so an off-by-one here silently changes who walks into the city.
		[TestCase(-1000, 0)]
		[TestCase(-251, 0)]
		[TestCase(-250, 0)]
		[TestCase(-249, 10)]
		[TestCase(0, 10)]
		[TestCase(249, 10)]
		[TestCase(250, 25)]
		[TestCase(599, 25)]
		[TestCase(600, 40)]
		[TestCase(5000, 40)]
		public void StandingWeightStepsWithVanillaAttitude(int standing, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomCreedRules.StandingWeight(standing));
		}

		[Test]
		public void AFactionThatDislikesTheRealmSendsNobodyEvenWithBelieversHere()
		{
			// Not "rarely" — zero. A zero weight is skipped outright by the draw, so this is the
			// difference between a hostile faction never appearing and appearing occasionally.
			ClassicAssert.AreEqual(0, KingdomCreedRules.CreedWeight(KingdomExileRules.RegardDisliked, 0, Declared: false));
			ClassicAssert.AreEqual(KingdomCreedRules.AffinityPerResident * 4,
				KingdomCreedRules.CreedWeight(KingdomExileRules.RegardDisliked, 4, Declared: false),
				"believers already here still pull, but the faction's own standing adds nothing");
		}

		[TestCase(0, 0, false, 10)]
		[TestCase(0, 1, false, 18)]
		[TestCase(0, 5, false, 50)]
		[TestCase(0, 0, true, 70)]
		[TestCase(600, 5, true, 140)]
		[TestCase(0, -3, false, 10)]
		public void CreedWeightComposesItsThreeInputs(int standing, int alreadyHere, bool declared, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomCreedRules.CreedWeight(standing, alreadyHere, declared));
		}

		[Test]
		public void OrdinaryOutweighsAnyOneCreedAtTheOutset()
		{
			// The design promise: creed is a minority colour that accumulates. A faction the realm
			// has merely met must not out-pull "holds with nobody in particular" on day one.
			int fresh = KingdomCreedRules.CreedWeight(0, 0, Declared: false);
			ClassicAssert.Less(fresh, KingdomCreedRules.OrdinaryWeight);
			int beloved = KingdomCreedRules.CreedWeight(KingdomExileRules.RegardLoved, 0, Declared: false);
			ClassicAssert.Less(beloved, KingdomCreedRules.OrdinaryWeight, "even a beloved faction starts as a minority");
		}

		[Test]
		public void TotalWeightCountsTheOrdinarySettlerAndSkipsDeadCandidates()
		{
			ClassicAssert.AreEqual(KingdomCreedRules.OrdinaryWeight, KingdomCreedRules.TotalWeight(null));
			ClassicAssert.AreEqual(KingdomCreedRules.OrdinaryWeight, KingdomCreedRules.TotalWeight(new int[0]));
			ClassicAssert.AreEqual(KingdomCreedRules.OrdinaryWeight + 30, KingdomCreedRules.TotalWeight(new int[3] { 10, 0, 20 }));
			ClassicAssert.AreEqual(KingdomCreedRules.OrdinaryWeight + 10, KingdomCreedRules.TotalWeight(new int[2] { 10, -5 }));
		}

		[Test]
		public void EveryRollBelowTheOrdinaryWeightIsAnOrdinarySettler()
		{
			int[] weights = new int[2] { 40, 60 };
			for (int roll = 0; roll < KingdomCreedRules.OrdinaryWeight; roll++)
			{
				ClassicAssert.AreEqual(-1, KingdomCreedRules.DrawCreed(weights, roll), "roll " + roll);
			}
		}

		[TestCase(100, 0)]
		[TestCase(139, 0)]
		[TestCase(140, 1)]
		[TestCase(199, 1)]
		public void DrawCreedBandsAreContiguousAndOrdered(int roll, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomCreedRules.DrawCreed(new int[2] { 40, 60 }, roll));
		}

		[Test]
		public void DrawCreedSkipsZeroWeightCandidatesEntirely()
		{
			int[] weights = new int[3] { 0, 50, 0 };
			for (int roll = KingdomCreedRules.OrdinaryWeight; roll < KingdomCreedRules.TotalWeight(weights); roll++)
			{
				ClassicAssert.AreEqual(1, KingdomCreedRules.DrawCreed(weights, roll), "roll " + roll);
			}
		}

		[Test]
		public void DrawCreedTreatsAnImpossibleRollAsOrdinaryRatherThanThrowing()
		{
			int[] weights = new int[1] { 50 };
			ClassicAssert.AreEqual(-1, KingdomCreedRules.DrawCreed(weights, 9999));
			ClassicAssert.AreEqual(-1, KingdomCreedRules.DrawCreed(weights, -1));
			ClassicAssert.AreEqual(-1, KingdomCreedRules.DrawCreed(null, 0));
		}

		[Test]
		public void EveryRollInRangeResolvesAndTheBandsPartitionTheRange()
		{
			int[] weights = new int[3] { 11, 0, 7 };
			int total = KingdomCreedRules.TotalWeight(weights);
			int ordinary = 0;
			int first = 0;
			int third = 0;
			for (int roll = 0; roll < total; roll++)
			{
				int drawn = KingdomCreedRules.DrawCreed(weights, roll);
				if (drawn == -1) { ordinary++; }
				else if (drawn == 0) { first++; }
				else if (drawn == 2) { third++; }
				else { Assert.Fail("a zero-weight candidate was drawn at roll " + roll); }
			}
			ClassicAssert.AreEqual(KingdomCreedRules.OrdinaryWeight, ordinary);
			ClassicAssert.AreEqual(11, first);
			ClassicAssert.AreEqual(7, third);
		}

		// ---- what a city believes -----------------------------------------------------------

		[Test]
		public void ACityWithNoResidentsHasNoCreed()
		{
			ClassicAssert.IsNull(KingdomCreedRules.DominantCreed(Counts(), 0));
			ClassicAssert.IsNull(KingdomCreedRules.DominantCreed(null, 10));
			ClassicAssert.IsNull(KingdomCreedRules.DominantCreed(Counts("Templar", 5), 0),
				"a tally that outlived its people names nobody");
		}

		[Test]
		public void ATieForDominanceLeavesTheCityMixed()
		{
			ClassicAssert.IsNull(KingdomCreedRules.DominantCreed(Counts("Templar", 4, "Barathrumites", 4), 8));
			ClassicAssert.IsNull(KingdomCreedRules.DominantCreed(Counts("Templar", 5, "Barathrumites", 5, "Joppa", 1), 11));
		}

		[Test]
		public void ATieIsBrokenByNobodyWhicheverOrderTheTallyIsRead()
		{
			// The tie rule is what makes this order-independent. If it were relaxed to >=, the
			// answer would depend on Dictionary enumeration order, which is not a contract.
			ClassicAssert.IsNull(KingdomCreedRules.DominantCreed(Counts("Aaa", 4, "Zzz", 4), 8));
			ClassicAssert.IsNull(KingdomCreedRules.DominantCreed(Counts("Zzz", 4, "Aaa", 4), 8));
		}

		[TestCase(2, 4, null)]
		[TestCase(3, 9, "Templar")]
		[TestCase(3, 10, null)]
		public void DominanceNeedsBothAFloorOfBelieversAndAShareOfTheCity(int believers, int population, string expected)
		{
			// 3 of 9 is exactly a third and passes; 3 of 10 is under and fails; 2 of 4 is half the
			// city and still fails, because two people are not a faction.
			ClassicAssert.AreEqual(expected, KingdomCreedRules.DominantCreed(Counts("Templar", believers), population));
		}

		[Test]
		public void TheMinimumBelieverFloorIsExactlyWhereItSays()
		{
			ClassicAssert.IsNull(KingdomCreedRules.DominantCreed(Counts("Templar", KingdomCreedRules.MinBelievers - 1), KingdomCreedRules.MinBelievers - 1));
			ClassicAssert.AreEqual("Templar", KingdomCreedRules.DominantCreed(Counts("Templar", KingdomCreedRules.MinBelievers), KingdomCreedRules.MinBelievers));
		}

		[Test]
		public void NonPositiveAndUnnamedTalliesAreIgnored()
		{
			ClassicAssert.IsNull(KingdomCreedRules.DominantCreed(Counts("Templar", 0, "Joppa", -4), 10));
			ClassicAssert.AreEqual("Joppa", KingdomCreedRules.DominantCreed(Counts("", 9, "Joppa", 4), 10),
				"an empty key is not a creed and must not win");
		}

		[Test]
		public void ALeaderNeedsOnlyAPluralityNotAMajority()
		{
			ClassicAssert.AreEqual("Templar", KingdomCreedRules.DominantCreed(Counts("Templar", 4, "Joppa", 3, "Girsh", 2), 12));
		}

		// ---- how badly two creeds are at odds ------------------------------------------------

		[Test]
		public void TwoCitiesOfTheSameCreedAreNeverAtOdds()
		{
			// The engine answers 100 for a faction's feeling about itself. That warmth is not the
			// cities' to claim, and more importantly it must not be read as hostility's opposite
			// and then negated somewhere.
			ClassicAssert.AreEqual(0, KingdomCreedRules.Hostility(100, 100, SameCreed: true));
			ClassicAssert.AreEqual(0, KingdomCreedRules.Hostility(-100, -100, SameCreed: true));
		}

		[Test]
		public void CreedsTheEngineHasNoFeelingBetweenAreAtPeace()
		{
			ClassicAssert.AreEqual(0, KingdomCreedRules.Hostility(0, 0, SameCreed: false));
			ClassicAssert.AreEqual(0, KingdomCreedRules.DissentPerDay(KingdomCreedRules.Hostility(0, 0, SameCreed: false)));
		}

		[TestCase(100, 100, 0)]
		[TestCase(0, 50, 0)]
		[TestCase(50, -50, 50)]
		[TestCase(-50, 50, 50)]
		[TestCase(-100, -50, 100)]
		[TestCase(-50, -100, 100)]
		[TestCase(-100, 0, 100)]
		[TestCase(0, -100, 100)]
		[TestCase(-400, -50, 100)]
		public void HostilityTakesTheWorseOfTwoUnequalDirections(int aboutTheOther, int back, int expected)
		{
			// Qud's feelings are not symmetric: the Barathrumites hold the Templar at -100 while
			// the Templar return -50. A design that averaged, or that read one direction, would
			// halve the game's own fault line.
			ClassicAssert.AreEqual(expected, KingdomCreedRules.Hostility(aboutTheOther, back, SameCreed: false));
		}

		// ---- the arithmetic of falling out ---------------------------------------------------

		[TestCase(0, 0)]
		[TestCase(1, 0)]
		[TestCase(24, 0)]
		[TestCase(25, 1)]
		[TestCase(49, 1)]
		[TestCase(50, 2)]
		[TestCase(100, 4)]
		public void OrdinaryDislikeBuysNoDissentAtAll(int hostility, int expected)
		{
			// The floor is load-bearing: a great many faction pairs sit at a general -20 or -10,
			// and none of those may ever become a countdown.
			ClassicAssert.AreEqual(expected, KingdomCreedRules.DissentPerDay(hostility));
		}

		[TestCase(0, 100, 0, 0)]
		[TestCase(0, 100, 1, 4)]
		[TestCase(0, 100, 3, 12)]
		[TestCase(10, 50, 3, 16)]
		[TestCase(0, 20, 3, 0)]
		[TestCase(96, 100, 3, 100)]
		[TestCase(-5, 0, 3, 0)]
		public void DissentAccruesPerAttendedDayAndClamps(int dissent, int hostility, int days, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomCreedRules.AccrueDissent(dissent, hostility, days));
		}

		[Test]
		public void ANegativeOrZeroDayCountChangesNothing()
		{
			ClassicAssert.AreEqual(40, KingdomCreedRules.AccrueDissent(40, 100, 0));
			ClassicAssert.AreEqual(40, KingdomCreedRules.AccrueDissent(40, 100, -7));
		}

		[Test]
		public void DissentRunsTheFullElapsedNowThatSecessionHasItsWindow()
		{
			// The uncapping and the window landed together, and had to. Dissent now accrues over
			// real elapsed days (Addendum 8 clause 1) rather than the three the absence cap used
			// to forgive, so a season away really is a season of quarrelling.
			int aSeason = KingdomRules.ElapsedDays(KingdomRules.TicksPerDay * 90);
			ClassicAssert.AreEqual(90, aSeason, "the clock is uncapped");
			ClassicAssert.Greater(KingdomCreedRules.AccrueDissent(0, 100, aSeason),
				KingdomCreedRules.AccrueDissent(0, 100, 3),
				"absence is no longer forgiven here");
		}

		[Test]
		public void AbsenceCannotOutrunPresenceBecauseItStopsAtTheBreakingPoint()
		{
			// What replaced the cap. Uncapping accrual on its own would have made an absence lose
			// a city FASTER than presence does -- clause 3 exactly inverted -- so the bound moved
			// from the clock to the outcome: dissent clamps at the breaking point, records a
			// brink, and nothing happens until the founder has been WARNED and a whole
			// SecessionWindowDays has run from that warning. A founder away ninety days and one
			// away a thousand come home to exactly the same realm, told in the same words.
			int ninety = KingdomCreedRules.AccrueDissent(0, 100, 90);
			int aThousand = KingdomCreedRules.AccrueDissent(0, 100, 1000);
			ClassicAssert.AreEqual(KingdomCreedRules.DissentBreaking, ninety);
			ClassicAssert.AreEqual(ninety, aThousand, "nothing accrues past the breaking point");
			ClassicAssert.AreEqual(CityTemper.Secession, KingdomCreedRules.ClassifyTemper(aThousand));
			ClassicAssert.Greater(KingdomCreedRules.SecessionWindowDays, 0,
				"and reaching it costs the founder nothing until the window has run from their warning");
			ClassicAssert.IsFalse(KingdomBrinkRules.WindowSpent(BrinkKind.City, KingdomBrinkRules.Unwarned, 9000L * KingdomRules.TicksPerDay),
				"a realm nobody was warned about must never lose a city, however long the absence");
		}

		[Test]
		public void AccrueDissent_AnAbsenceLongEnoughToOverflowAnIntStillOnlyReachesTheBreakingPoint()
		{
			// Four points a day times an uncapped day count is a number an int cannot hold, and a
			// dissent that wrapped negative would read as concord -- a realm at the breaking point
			// silently reported as being at peace.
			ClassicAssert.AreEqual(KingdomCreedRules.DissentBreaking, KingdomCreedRules.AccrueDissent(0, 100, int.MaxValue));
			ClassicAssert.AreEqual(KingdomCreedRules.DissentBreaking, KingdomCreedRules.AccrueDissent(99, 100, int.MaxValue));
			ClassicAssert.AreEqual(0, KingdomCreedRules.AccrueDissent(0, 20, int.MaxValue),
				"and ordinary dislike still buys nothing at all, however long it is left");
		}

		[TestCase(50, -20, 30)]
		[TestCase(50, 20, 70)]
		[TestCase(5, -25, 0)]
		[TestCase(95, 25, 100)]
		[TestCase(0, -1, 0)]
		public void LeversMoveDissentWithinItsBounds(int dissent, int delta, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomCreedRules.ApplyDissent(dissent, delta));
		}

		// ---- the ladder the founder watches --------------------------------------------------

		[TestCase(0, CityTemper.Concord)]
		[TestCase(19, CityTemper.Concord)]
		[TestCase(20, CityTemper.Muttering)]
		[TestCase(44, CityTemper.Muttering)]
		[TestCase(45, CityTemper.Quarrel)]
		[TestCase(69, CityTemper.Quarrel)]
		[TestCase(70, CityTemper.Rupture)]
		[TestCase(99, CityTemper.Rupture)]
		[TestCase(100, CityTemper.Secession)]
		public void TemperBoundariesAreExactlyWhereTheConstantsSay(int dissent, CityTemper expected)
		{
			ClassicAssert.AreEqual(expected, KingdomCreedRules.ClassifyTemper(dissent));
		}

		[Test]
		public void EveryTemperShortOfSecessionIsReachedAndAnnouncedBeforeAnythingIsLost()
		{
			// The founder must be able to watch this coming. Four distinct tiers below the loss,
			// each with something to say.
			CityTemper[] seen = new CityTemper[4]
			{
				KingdomCreedRules.ClassifyTemper(0),
				KingdomCreedRules.ClassifyTemper(KingdomCreedRules.DissentMuttering),
				KingdomCreedRules.ClassifyTemper(KingdomCreedRules.DissentQuarrel),
				KingdomCreedRules.ClassifyTemper(KingdomCreedRules.DissentRupture)
			};
			ClassicAssert.AreEqual(CityTemper.Concord, seen[0]);
			ClassicAssert.AreEqual(CityTemper.Muttering, seen[1]);
			ClassicAssert.AreEqual(CityTemper.Quarrel, seen[2]);
			ClassicAssert.AreEqual(CityTemper.Rupture, seen[3]);
			for (int i = 1; i < seen.Length; i++)
			{
				ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomCreedRules.TemperSpeech(seen[i], "Nesh", "Basra")), seen[i] + " must say something");
				ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomCreedRules.TemperChronicle(seen[i], "Nesh", "Basra")), seen[i] + " must write something");
			}
		}

		[Test]
		public void TheLoudestWarningStandsForManyAttendedDaysBeforeTheCityLeaves()
		{
			// "Long and high", not merely high: at the very worst hostility the shipped data holds,
			// the founder gets a week of top-tier warning after the rupture line before the realm
			// so much as reaches the breaking point.
			int dissent = KingdomCreedRules.DissentRupture;
			int days = 0;
			while (KingdomCreedRules.ClassifyTemper(dissent) != CityTemper.Secession)
			{
				dissent = KingdomCreedRules.AccrueDissent(dissent, 100, 1);
				days++;
				ClassicAssert.Less(days, 100, "the ladder must terminate");
			}
			ClassicAssert.GreaterOrEqual(days, 7);
			// Addendum 10(a) put the window into the same unit as this span, and the derivation it
			// ordered (three attended passes x the cadence) makes it NINE days against this span's
			// eight. The old "one rung under" relation was between two different units and does
			// not survive the change of unit; what is pinned instead is the relation that actually
			// matters and is still true -- the ladder the founder can watch is longer than the
			// last chance at the end of it, counting from the first muttering.
			int fromTheFirstMuttering = 0;
			int dissentFromMuttering = KingdomCreedRules.DissentMuttering;
			while (KingdomCreedRules.ClassifyTemper(dissentFromMuttering) != CityTemper.Secession)
			{
				dissentFromMuttering = KingdomCreedRules.AccrueDissent(dissentFromMuttering, 100, 1);
				fromTheFirstMuttering++;
				ClassicAssert.Less(fromTheFirstMuttering, 200, "the ladder must terminate");
			}
			ClassicAssert.Greater(fromTheFirstMuttering, KingdomCreedRules.SecessionWindowDays,
				"the warning ladder must be longer than the window it ends in");
			ClassicAssert.GreaterOrEqual(days + KingdomCreedRules.SecessionWindowDays, 14,
				"a fortnight of visible warning between the loudest tier and losing the city");
		}

		[Test]
		public void SecessionHasAWindowAndItIsSpentInWorldDaysLikeEveryOtherBrink()
		{
			// The gap the whole package existed to close: reaching the breaking point used to BE
			// the secession. Now it is a brink, and the brink's own arithmetic -- world time from
			// the day the word reaches the founder -- decides when the city actually walks.
			ClassicAssert.AreEqual(KingdomBrinkRules.CityBrinkWindowDays, KingdomCreedRules.SecessionWindowDays);
			ClassicAssert.AreEqual(KingdomBrinkRules.InCohabitationDays(KingdomBrinkRules.CityBrinkWindowPasses),
				KingdomCreedRules.SecessionWindowDays, "the window stopped being its old rope restated");
			long told = 300L * KingdomRules.TicksPerDay;
			int days = 0;
			while (!KingdomBrinkRules.WindowSpent(BrinkKind.City, told, told + days * KingdomRules.TicksPerDay))
			{
				days++;
				ClassicAssert.Less(days, 200, "the window must terminate");
			}
			ClassicAssert.AreEqual(KingdomCreedRules.SecessionWindowDays, days,
				"a whole window of world-days after the day the word went out, and not one fewer");
		}

		[Test]
		public void SecessionBrinkSpeech_NamesTheCityTheElapsedAndWhatIsLeftOfTheWindow()
		{
			// The Secession rung of TemperSpeech is deliberately silent -- until the brink there
			// was nothing to say at that tier, because the city was already gone. This is the
			// sentence that fills it, and it must carry all three facts a founder can act on.
			ClassicAssert.AreEqual("", KingdomCreedRules.TemperSpeech(CityTemper.Secession, "Nesh", "Basra"));
			string line = KingdomCreedRules.SecessionBrinkSpeech("Basra", "Nesh", 31, 9);
			StringAssert.Contains("Basra", line);
			StringAssert.Contains("Nesh", line);
			StringAssert.Contains("31 days", line);
			StringAssert.Contains("9 days", line);
			StringAssert.DoesNotContain("visit", line, "the window is the world's now, not a count of homecomings");
		}

		[Test]
		public void SecessionBrinkSpeech_AlwaysNamesTheArrestBecauseItMayFireWhileTheFounderIsAway()
		{
			// Addendum 10(a)'s coaching clause, for the loudest line the realm ever says. The city
			// can now go while nobody is watching, so the sentence that warns of it must say what
			// would have stopped it -- with names and without them.
			StringAssert.Contains("rite", KingdomCreedRules.SecessionBrinkSpeech("Basra", "Nesh", 31, 9));
			StringAssert.Contains("rite", KingdomCreedRules.SecessionBrinkSpeech(null, null, 0, 2));
		}

		[Test]
		public void SecessionBrinkSpeech_IsSingularOnTheLastDayAndSaysSomethingWithNoNamesAtAll()
		{
			StringAssert.Contains("One day", KingdomCreedRules.SecessionBrinkSpeech("Basra", "Nesh", 2, 1));
			StringAssert.Contains("no more time", KingdomCreedRules.SecessionBrinkSpeech("Basra", "Nesh", 2, 0));
			ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomCreedRules.SecessionBrinkSpeech(null, null, 0, 2)));
		}

		[Test]
		public void AWorseningSpeaksOnceAndJitterSaysNothingFurther()
		{
			CityTemper spoken = CityTemper.Concord;
			ClassicAssert.IsTrue(KingdomCreedRules.ShouldSpeak(CityTemper.Muttering, spoken));
			spoken = KingdomCreedRules.RememberedTemper(CityTemper.Muttering, spoken);
			ClassicAssert.AreEqual(CityTemper.Muttering, spoken);
			ClassicAssert.IsFalse(KingdomCreedRules.ShouldSpeak(CityTemper.Muttering, spoken));
			// Slipping back one tier and worsening again says nothing: the ladder is not re-armed.
			spoken = KingdomCreedRules.RememberedTemper(CityTemper.Quarrel, spoken);
			ClassicAssert.IsFalse(KingdomCreedRules.ShouldSpeak(CityTemper.Muttering, spoken));
			ClassicAssert.IsTrue(KingdomCreedRules.ShouldSpeak(CityTemper.Rupture, spoken));
		}

		[Test]
		public void MendingItAllTheWayReArmsTheLadder()
		{
			CityTemper spoken = KingdomCreedRules.RememberedTemper(CityTemper.Rupture, CityTemper.Concord);
			ClassicAssert.AreEqual(CityTemper.Rupture, spoken);
			spoken = KingdomCreedRules.RememberedTemper(CityTemper.Concord, spoken);
			ClassicAssert.AreEqual(CityTemper.Concord, spoken, "easing it to nothing must forget what was said");
			ClassicAssert.IsTrue(KingdomCreedRules.ShouldSpeak(CityTemper.Muttering, spoken));
		}

		[Test]
		public void ConcordIsNeverSpokenOf()
		{
			ClassicAssert.IsFalse(KingdomCreedRules.ShouldSpeak(CityTemper.Concord, CityTemper.Concord));
			ClassicAssert.IsTrue(string.IsNullOrEmpty(KingdomCreedRules.TemperSpeech(CityTemper.Concord, "Nesh", "Basra")));
			ClassicAssert.IsTrue(string.IsNullOrEmpty(KingdomCreedRules.TemperChronicle(CityTemper.Concord, "Nesh", "Basra")));
		}

		// ---- the levers ----------------------------------------------------------------------

		[Test]
		public void ThereIsNoRiteToSellAFounderWhoseCitiesAreAtPeace()
		{
			ClassicAssert.AreEqual(0, KingdomCreedRules.RiteCost(CityTemper.Concord));
			ClassicAssert.AreEqual(0, KingdomCreedRules.RiteEase(CityTemper.Concord));
		}

		[TestCase(CityTemper.Muttering, 20, 15)]
		[TestCase(CityTemper.Quarrel, 40, 20)]
		[TestCase(CityTemper.Rupture, 80, 25)]
		[TestCase(CityTemper.Secession, 80, 25)]
		public void TheRiteCostsMoreAndBuysProportionallyLessTheLongerItIsLeft(CityTemper temper, int cost, int ease)
		{
			ClassicAssert.AreEqual(cost, KingdomCreedRules.RiteCost(temper));
			ClassicAssert.AreEqual(ease, KingdomCreedRules.RiteEase(temper));
		}

		[Test]
		public void TheRiteHoldsTheLineAtTheWorstHostilityTheDataHolds()
		{
			// The design claim, checked rather than asserted in prose: a founder who pours every
			// time the rite comes off cooldown gains ground against a flat -100, at a price.
			int gainedOverACooldown = KingdomCreedRules.DissentPerDay(100) * KingdomCreedRules.RiteCooldownDays;
			ClassicAssert.Greater(KingdomCreedRules.RiteEase(CityTemper.Rupture), gainedOverACooldown,
				"a lever that cannot outpace the accrual is not a lever, it is a delay");
		}

		[Test]
		public void TheRiteCooldownIsMeasuredInWholeDays()
		{
			long day = KingdomRules.TicksPerDay;
			ClassicAssert.IsTrue(KingdomCreedRules.RiteReady(0L, 0L), "a rite never held is always ready");
			ClassicAssert.IsFalse(KingdomCreedRules.RiteReady(1000L, 1000L));
			ClassicAssert.IsFalse(KingdomCreedRules.RiteReady(1000L, 1000L + KingdomCreedRules.RiteCooldownDays * day - 1L));
			ClassicAssert.IsTrue(KingdomCreedRules.RiteReady(1000L, 1000L + KingdomCreedRules.RiteCooldownDays * day));
		}

		[Test]
		public void TheDeclarationCostsSomethingInBothDirections()
		{
			ClassicAssert.Greater(KingdomCreedRules.DeclarationShock, 0, "picking a side must sting the side not picked");
			ClassicAssert.Less(KingdomCreedRules.DeclarationStandingCost, 0, "the slighted faction must actually think less of the realm");
			ClassicAssert.Greater(KingdomCreedRules.DeclaredBonus, 0, "and it must actually change who walks in");
		}

		// ---- a city leaving --------------------------------------------------------------------

		[Test]
		public void ARealmOfOneCityNeverEncountersAnyOfThis()
		{
			ClassicAssert.AreEqual(SecessionVerdict.OneCity, KingdomCreedRules.JudgeSecession(1, 100, 100, Forced: false));
			ClassicAssert.AreEqual(SecessionVerdict.OneCity, KingdomCreedRules.JudgeSecession(0, 100, 100, Forced: false));
			ClassicAssert.AreEqual(SecessionVerdict.OneCity, KingdomCreedRules.JudgeSecession(1, 100, 100, Forced: true),
				"not even the debug path may break a realm that has nothing to break");
		}

		[TestCase(0, 100, SecessionVerdict.NoClash)]
		[TestCase(100, 99, SecessionVerdict.DissentHolds)]
		[TestCase(100, 100, SecessionVerdict.Warranted)]
		[TestCase(25, 100, SecessionVerdict.Warranted)]
		public void SecessionNeedsBothALiveClashAndAFullMeasureOfDissent(int hostility, int dissent, SecessionVerdict expected)
		{
			ClassicAssert.AreEqual(expected, KingdomCreedRules.JudgeSecession(KingdomSettlement.MaxSettlements, hostility, dissent, Forced: false));
		}

		[Test]
		public void ACreedThatStoppedClashingStopsTheSecessionEvenAtFullDissent()
		{
			// This is the declaration lever's whole payoff: change what a city believes and the
			// accrued dissent becomes a scar rather than a countdown.
			ClassicAssert.AreEqual(SecessionVerdict.NoClash,
				KingdomCreedRules.JudgeSecession(KingdomSettlement.MaxSettlements, 0, KingdomCreedRules.DissentBreaking, Forced: false));
		}

		[Test]
		public void TheDebugPathSkipsTheDissentRequirementAndNothingElse()
		{
			ClassicAssert.AreEqual(SecessionVerdict.Warranted, KingdomCreedRules.JudgeSecession(KingdomSettlement.MaxSettlements, 0, 0, Forced: true));
		}

		[TestCase(-50, -100, 10, 10, true)]
		[TestCase(-100, -50, 10, 10, false)]
		[TestCase(-100, -100, 12, 3, true)]
		[TestCase(-100, -100, 3, 12, false)]
		[TestCase(-100, -100, 7, 7, true)]
		public void TheUnhappierCityWalksAndOnATieTheSmallerOneDoes(int seatAboutAway, int awayAboutSeat, int seatPop, int awayPop, bool expected)
		{
			ClassicAssert.AreEqual(expected, KingdomCreedRules.AwayIsTheLeaver(seatAboutAway, awayAboutSeat, seatPop, awayPop));
		}

		[Test]
		public void WhichCityLeavesIsFixedForAGivenRealm()
		{
			// Same inputs, same answer, every time: a realm must not break differently depending
			// on which city the founder happened to walk into.
			for (int i = 0; i < 5; i++)
			{
				ClassicAssert.IsFalse(KingdomCreedRules.AwayIsTheLeaver(-100, -50, 4, 9));
			}
		}

		// ---- winning it back -------------------------------------------------------------------

		[Test]
		public void ThereIsNothingToTakeBackWhenNobodyLeft()
		{
			ClassicAssert.AreEqual(RejoinVerdict.NothingSeceded, KingdomCreedRules.JudgeRejoin(false, 1, true, 0, 0));
		}

		[TestCase(true, 3, true, 0, 0, RejoinVerdict.RealmIsFull)]
		[TestCase(true, 2, true, 0, 0, RejoinVerdict.Allowed)]
		[TestCase(true, 1, false, 0, 0, RejoinVerdict.NotOnTheirGround)]
		[TestCase(true, 1, true, 100, 0, RejoinVerdict.ClashStillLive)]
		[TestCase(true, 1, true, 25, 0, RejoinVerdict.ClashStillLive)]
		[TestCase(true, 1, true, 24, 0, RejoinVerdict.Allowed)]
		[TestCase(true, 1, true, 0, -250, RejoinVerdict.StandingTooLow)]
		[TestCase(true, 1, true, 0, -249, RejoinVerdict.Allowed)]
		[TestCase(true, 1, true, 0, 0, RejoinVerdict.Allowed)]
		public void RejoinRefusalsAreCheckedInTheOrderTheFounderWouldMeetThem(bool seceded, int cities, bool onTheirGround, int hostility, int standing, RejoinVerdict expected)
		{
			ClassicAssert.AreEqual(expected, KingdomCreedRules.JudgeRejoin(seceded, cities, onTheirGround, hostility, standing));
		}

		[Test]
		public void WaitingOutAQuarrelIsNotARouteBackButChangingItIs()
		{
			// The same city, the same standing, the same ground; the only difference is whether the
			// founder actually changed what the two cities believe.
			ClassicAssert.AreEqual(RejoinVerdict.ClashStillLive, KingdomCreedRules.JudgeRejoin(true, 1, true, 100, 0));
			ClassicAssert.AreEqual(RejoinVerdict.Allowed, KingdomCreedRules.JudgeRejoin(true, 1, true, 0, 0));
		}

		[Test]
		public void TheRejoinClashGateUsesTheSameFloorAsTheAccrual()
		{
			// If these two drifted apart there would be a band where dissent accrues but the city
			// is allowed back, or the reverse: a permanent stalemate the founder cannot read.
			for (int hostility = 0; hostility <= 100; hostility++)
			{
				bool accrues = KingdomCreedRules.DissentPerDay(hostility) > 0;
				bool blocked = KingdomCreedRules.JudgeRejoin(true, 1, true, hostility, 0) == RejoinVerdict.ClashStillLive;
				ClassicAssert.AreEqual(accrues, blocked, "hostility " + hostility);
			}
		}

		// ---- prose -------------------------------------------------------------------------------

		// Listed rather than reflected: a verdict added without prose must fail this, and
		// Enum.GetValues would quietly cover it the moment someone added the member.
		private static readonly RejoinVerdict[] AllRejoinVerdicts = new RejoinVerdict[6]
		{
			RejoinVerdict.Allowed, RejoinVerdict.NothingSeceded, RejoinVerdict.RealmIsFull,
			RejoinVerdict.NotOnTheirGround, RejoinVerdict.ClashStillLive, RejoinVerdict.StandingTooLow
		};

		private static readonly CityTemper[] AllTempers = new CityTemper[5]
		{
			CityTemper.Concord, CityTemper.Muttering, CityTemper.Quarrel, CityTemper.Rupture, CityTemper.Secession
		};

		[Test]
		public void EveryRefusalAndTellingSaysSomething()
		{
			foreach (RejoinVerdict verdict in AllRejoinVerdicts)
			{
				string refusal = KingdomCreedRules.RejoinRefusal(verdict, "Basra", "the Putus Templar");
				if (verdict == RejoinVerdict.Allowed) { ClassicAssert.IsTrue(string.IsNullOrEmpty(refusal)); }
				else { ClassicAssert.IsFalse(string.IsNullOrEmpty(refusal), verdict.ToString()); }
			}
			ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomCreedRules.SecessionTelling("Basra", "Nesh", "the Putus Templar")));
			ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomCreedRules.SecessionRumour("Basra", "Hameh")));
			ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomCreedRules.SecessionNotice("Basra", "Nesh", "the Putus Templar", 7)));
			ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomCreedRules.RejoinTelling("Basra")));
			ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomCreedRules.RejoinRumour("Basra", "Hameh")));
			ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomCreedRules.RejoinNotice("Basra", "Yad")));
			ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomCreedRules.RiteTelling("Nesh", "Basra", 80)));
			ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomCreedRules.DeclarationTelling("Yad", "the Putus Templar")));
			ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomCreedRules.RecantTelling("Yad")));
			ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomCreedRules.DeclarationNotice("the Putus Templar", "the Barathrumites")));
			ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomCreedRules.DeclarationNotice("the Putus Templar", null)));
		}

		[Test]
		public void EveryPieceOfProseSurvivesMissingNames()
		{
			// Names are null on a save written before cities had them, and the creed is null for a
			// mixed city, which is the common case. Nothing may render as "the  of ".
			string[] prose = new string[10]
			{
				KingdomCreedRules.CreedClause(null),
				KingdomCreedRules.TemperReport(CityTemper.Quarrel, 50, null, null, null, null),
				KingdomCreedRules.TemperSpeech(CityTemper.Rupture, null, null),
				KingdomCreedRules.TemperChronicle(CityTemper.Muttering, null, null),
				KingdomCreedRules.SecessionTelling(null, null, null),
				KingdomCreedRules.SecessionRumour(null, null),
				KingdomCreedRules.SecessionNotice(null, null, null, 1),
				KingdomCreedRules.RejoinNotice(null, null),
				KingdomCreedRules.RejoinRefusal(RejoinVerdict.ClashStillLive, null, null),
				KingdomCreedRules.RiteNotice(CityTemper.Rupture, null)
			};
			for (int i = 0; i < prose.Length; i++)
			{
				ClassicAssert.IsFalse(string.IsNullOrEmpty(prose[i]), "prose " + i);
				ClassicAssert.IsFalse(prose[i].Contains("  "), "prose " + i + " has a hole where a name should be: " + prose[i]);
				ClassicAssert.IsFalse(prose[i].Contains("{{C|}}"), "prose " + i + " coloured an empty name: " + prose[i]);
			}
		}

		[Test]
		public void AMixedCityIsDescribedAsSomethingRatherThanAsAFailure()
		{
			string mixed = KingdomCreedRules.CreedClause(null);
			ClassicAssert.IsFalse(string.IsNullOrEmpty(mixed));
			ClassicAssert.AreNotEqual(mixed, KingdomCreedRules.CreedClause("the Putus Templar"));
		}

		[Test]
		public void TheReportAlwaysNamesBothCitiesAndTheDistanceToTheBreak()
		{
			foreach (CityTemper temper in AllTempers)
			{
				string report = KingdomCreedRules.TemperReport(temper, 42, "Nesh", "Basra", "the Putus Templar", null);
				ClassicAssert.IsTrue(report.Contains("Nesh"), temper.ToString());
				ClassicAssert.IsTrue(report.Contains("Basra"), temper.ToString());
				ClassicAssert.IsTrue(report.Contains("42"), temper + " must show how far along it is");
				ClassicAssert.IsTrue(report.Contains(KingdomCreedRules.DissentBreaking.ToString()), temper + " must show what it is counting toward");
			}
		}

		[Test]
		public void EveryTemperHasAName()
		{
			List<string> seen = new List<string>();
			foreach (CityTemper temper in AllTempers)
			{
				string name = KingdomCreedRules.TemperName(temper);
				ClassicAssert.IsFalse(string.IsNullOrEmpty(name), temper.ToString());
				ClassicAssert.IsFalse(seen.Contains(name), "two tempers share the name " + name);
				seen.Add(name);
			}
		}

		private static Dictionary<string, int> Counts(params object[] Pairs)
		{
			Dictionary<string, int> counts = new Dictionary<string, int>();
			for (int i = 0; i + 1 < Pairs.Length; i += 2)
			{
				counts[(string)Pairs[i]] = (int)Pairs[i + 1];
			}
			return counts;
		}
	}
}
#endif
