#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomPowerRulesTests
	{
		[Test]
		public void ChargingPost_KeepsItsLoadBearingStoreSafeAndItsIdleStateHonest()
		{
			string xml = TestMain.ReadRepositoryText("ObjectBlueprints.xml");
			int start = xml.IndexOf("<object Name=\"r_KingdomChargingPost\"", StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0);
			int end = xml.IndexOf("</object>", start, StringComparison.Ordinal);
			ClassicAssert.Greater(end, start);
			string post = xml.Substring(start, end - start);
			StringAssert.Contains(
				"<part Name=\"Capacitor\" MaxCharge=\"4000\" ChargeRate=\"0\" MinimumChargeToExplode=\"0\" />",
				post, "the hand-cranked charger needs storage, but civic furniture must not become a bomb");
			StringAssert.Contains("<part Name=\"UniversalCharger\" ChargeRate=\"150\" />", post);
			ClassicAssert.IsFalse(post.Contains("AnimatedMaterialElectric"),
				"vanilla's electric animation flashes even while this hand-cranked post is empty");

			string growth = KingdomGrowthLogicalSource.Read();
			StringAssert.Contains("if (work.GetIntProperty(\"KingdomHandCranked\") == 1)", growth);
			StringAssert.Contains("capacitor.Charge = target;", growth);

			string visual = TestMain.ReadRepositoryText("Growth/KingdomVisualState.cs");
			StringAssert.Contains("public class r_KingdomHandCrankedVisual : IPart", visual);
			StringAssert.Contains("item.RequirePart<r_KingdomHandCrankedVisual>()", visual);
			StringAssert.Contains("KingdomVisualState.StateOf(ParentObject) != KingdomVisualStateKind.Sound", visual);
			StringAssert.Contains("store != null && store.Charge > 0", visual);
			StringAssert.Contains("E.RenderEffectIndicator(ActiveGlyph, null", visual);
			ClassicAssert.IsFalse(visual.Contains("RequirePart<AnimatedMaterialElectric>"),
				"the state-aware replacement must not smuggle the unconditional vanilla flash back in");
		}

		// --- RatedChargePerDay: each kind of work is worth its own day's labour ---------------

		[TestCase(KingdomPowerRules.PowerSource.Hands, KingdomPowerRules.MillChargePerDay)]
		[TestCase(KingdomPowerRules.PowerSource.Water, KingdomPowerRules.WaterWheelChargePerDay)]
		[TestCase(KingdomPowerRules.PowerSource.Wind, KingdomPowerRules.SailvaneChargePerDay)]
		public void RatedChargePerDay_MatchesTheDocumentedRating(KingdomPowerRules.PowerSource source, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomPowerRules.RatedChargePerDay(source));
		}

		[Test]
		public void RatedChargePerDay_EveryKindIsPositiveAndDistinct()
		{
			// A mutation collapsing two switch arms would make two kinds indistinguishable, and
			// one returning zero would make a commissionable work worthless with no other test
			// noticing.
			int hands = KingdomPowerRules.RatedChargePerDay(KingdomPowerRules.PowerSource.Hands);
			int water = KingdomPowerRules.RatedChargePerDay(KingdomPowerRules.PowerSource.Water);
			int wind = KingdomPowerRules.RatedChargePerDay(KingdomPowerRules.PowerSource.Wind);
			ClassicAssert.Greater(hands, 0);
			ClassicAssert.Greater(water, 0);
			ClassicAssert.Greater(wind, 0);
			ClassicAssert.AreNotEqual(hands, water);
			ClassicAssert.AreNotEqual(water, wind);
			ClassicAssert.AreNotEqual(hands, wind);
		}

		// --- TryParseSource: third-party XML is untrusted --------------------------------------

		[TestCase("Hands", true, KingdomPowerRules.PowerSource.Hands)]
		[TestCase("hands", true, KingdomPowerRules.PowerSource.Hands)]
		[TestCase("  WATER  ", true, KingdomPowerRules.PowerSource.Water)]
		[TestCase("Wind", true, KingdomPowerRules.PowerSource.Wind)]
		[TestCase("steam", false, KingdomPowerRules.PowerSource.Hands)]
		[TestCase("", false, KingdomPowerRules.PowerSource.Hands)]
		[TestCase(null, false, KingdomPowerRules.PowerSource.Hands)]
		public void TryParseSource_AcceptsOnlyTheKindsThisBuildKnows(string text, bool expected, KingdomPowerRules.PowerSource expectedSource)
		{
			ClassicAssert.AreEqual(expected, KingdomPowerRules.TryParseSource(text, out var source));
			ClassicAssert.AreEqual(expectedSource, source);
		}

		[Test]
		public void TryParseSource_RejectionIsNotSilentlyAMill()
		{
			// The out value on failure is Hands, but the caller must be able to tell the
			// difference: a mutation returning true for unknown text would turn every
			// misspelt third-party work into a working mill.
			ClassicAssert.IsFalse(KingdomPowerRules.TryParseSource("windmill", out _));
			ClassicAssert.IsFalse(KingdomPowerRules.TryParseSource("water wheel", out _));
			ClassicAssert.IsTrue(KingdomPowerRules.TryParseSource("Hands ", out _));
		}

		// --- Clamps ---------------------------------------------------------------------------

		[TestCase(-50, 0)]
		[TestCase(0, 0)]
		[TestCase(37, 37)]
		[TestCase(100, 100)]
		[TestCase(4000, 100)]
		public void ClampPercent_HoldsZeroToOneHundred(int input, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomPowerRules.ClampPercent(input));
		}

		[TestCase(-1, 0)]
		[TestCase(0, 0)]
		[TestCase(1, 1)]
		[TestCase(3, 3)]
		[TestCase(90, 90)]
		[TestCase(int.MaxValue, int.MaxValue)]
		public void ClampDays_OnlyFailsClosedAndNoLongerForgives(int input, int expected)
		{
			// It clamped to a three-day ceiling and that WAS power's forgiveness. It now refuses
			// a nonsense negative in one place for four rules and otherwise hands back the
			// calendar it was given.
			ClassicAssert.AreEqual(expected, KingdomPowerRules.ClampDays(input));
		}

		[Test]
		public void MaxDaysCreditedIsRetiredAndNothingHereCapsAnAbsence()
		{
			// The constant is gone, and the point is what replaced it: nothing. Power was already
			// crew- and availability-gated end to end, so the uncapping needed no new bound --
			// what stops a season away from minting a season of charge is that an unstaffed work
			// makes nothing per day and the stores can only hold what was built for them.
			ClassicAssert.IsNull(typeof(KingdomPowerRules).GetField("MaxDaysCredited"), "power's local absence cap came back");
			int daily = KingdomPowerRules.DailyOutput(KingdomPowerRules.PowerSource.Hands, 100, 100);
			ClassicAssert.AreEqual(daily * 90, KingdomPowerRules.ChargeForDays(daily, 90),
				"ninety days of a fully crewed mill was not ninety days of milling");
		}

		// --- WaterAvailabilityPercent: the hydraulics, and what happens with no water ----------

		[TestCase(0, 0)]
		[TestCase(399, 0)]
		[TestCase(KingdomPowerRules.HydraulicMinimumDrams, 0)]
		[TestCase(2200, 50)]
		[TestCase(KingdomPowerRules.HydraulicRatedDrams, 100)]
		[TestCase(90000, 100)]
		public void WaterAvailabilityPercent_RisesFromTheWheelsMinimumToItsRating(int drams, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomPowerRules.WaterAvailabilityPercent(drams));
		}

		[Test]
		public void WaterAvailabilityPercent_ADryWheelMakesNothingAtAll()
		{
			// The headline case: no water, no hydraulics. A mutation turning the minimum into
			// a soft floor - returning a small positive below it - would let a wheel dropped
			// in a puddle quietly power the settlement.
			ClassicAssert.AreEqual(0, KingdomPowerRules.WaterAvailabilityPercent(0));
			ClassicAssert.AreEqual(0, KingdomPowerRules.WaterAvailabilityPercent(KingdomPowerRules.HydraulicMinimumDrams - 1));
			ClassicAssert.AreEqual(0, KingdomPowerRules.DailyOutput(KingdomPowerRules.PowerSource.Water, 100, KingdomPowerRules.WaterAvailabilityPercent(0)));
		}

		[Test]
		public void WaterAvailabilityPercent_TheMillraceTurnsTheWheelAndIsNotWorthSitingFor()
		{
			// Wave G1: r_KingdomWaterWheel carries vanilla's SpawnWithLiquid the way vanilla's own
			// Wooden Water Wheel does, so a wheel raised on dry ground digs itself one
			// SaltyWaterPuddle - 500 drams - instead of reporting HydrodynamicForceInsufficient
			// forever. Both halves of that have to stay true. It must TURN, because a work whose
			// only failure mode is silent is a work the founder cannot debug; and it must be
			// nearly worthless, because otherwise siting stops mattering and the wheel becomes a
			// free generator anybody can raise anywhere.
			const int MillraceDrams = 500;
			int race = KingdomPowerRules.WaterAvailabilityPercent(MillraceDrams);
			ClassicAssert.Greater(race, 0, "the wheel's own race must clear HydraulicMinimumDrams, or the wheel never turns");
			ClassicAssert.Less(race, 10, "and it must stay near nothing, or siting beside real water stops being the point");
			ClassicAssert.Greater(KingdomPowerRules.WaterAvailabilityPercent(KingdomPowerRules.HydraulicRatedDrams), race * 10,
				"a real pool is worth more than an order of magnitude over the race");
		}

		[Test]
		public void WaterAvailabilityPercent_NeverFallsAsWaterRises()
		{
			int previous = -1;
			for (int drams = 0; drams <= KingdomPowerRules.HydraulicRatedDrams + 200; drams += 50)
			{
				int now = KingdomPowerRules.WaterAvailabilityPercent(drams);
				ClassicAssert.GreaterOrEqual(now, previous, "availability fell at " + drams + " drams");
				ClassicAssert.LessOrEqual(now, 100);
				previous = now;
			}
		}

		[Test]
		public void HydraulicBand_IsAWidthTheRuleCanDivideBy()
		{
			// The linear stretch divides by (Rated - Minimum). Equal or inverted constants would
			// divide by zero or run backwards, and nothing else in the suite would say so.
			ClassicAssert.Greater(KingdomPowerRules.HydraulicRatedDrams, KingdomPowerRules.HydraulicMinimumDrams);
			ClassicAssert.Greater(KingdomPowerRules.HydraulicMinimumDrams, 0);
		}

		// --- WindAvailabilityPercent: one witnessed day, then the typical -----------------------

		[TestCase(60, 1, 100)]
		[TestCase(90, 1, 100)]
		[TestCase(30, 1, 50)]
		[TestCase(0, 1, 0)]
		[TestCase(60, 0, 0)]
		[TestCase(60, -4, 0)]
		public void WindAvailabilityPercent_CreditsTheGustItActuallyRead(int kph, int days, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomPowerRules.WindAvailabilityPercent(kph, days));
		}

		[TestCase(0, 3, 33)]
		[TestCase(60, 3, 66)]
		[TestCase(0, 2, 25)]
		[TestCase(60, 2, 75)]
		public void WindAvailabilityPercent_UnwitnessedDaysAreCreditedAtTheTypicalWind(int kph, int days, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomPowerRules.WindAvailabilityPercent(kph, days));
		}

		[Test]
		public void WindAvailabilityPercent_ACalmAfternoonCannotCostAWholeAbsence()
		{
			// The reason the rule exists: the wind is only evidence about the moment it was
			// read. A mutation that used the sample for every day would score a dead calm at
			// zero across three days, and a gale at a hundred.
			ClassicAssert.Greater(KingdomPowerRules.WindAvailabilityPercent(0, 3), 0);
			ClassicAssert.Less(KingdomPowerRules.WindAvailabilityPercent(KingdomPowerRules.RatedWindSpeedKph, 3), 100);
		}

		[Test]
		public void WindAvailabilityPercent_ConvergesOnTheTypicalWindOverALongAbsence()
		{
			// The doctrine's exemplar, uncapped. One witnessed gust says less and less about a
			// longer and longer stretch, so a dead calm read on the day the founder walked in
			// stops being evidence about the season and the answer settles at the typical wind.
			int calmOverASeason = KingdomPowerRules.WindAvailabilityPercent(0, 400);
			int calmOverThreeDays = KingdomPowerRules.WindAvailabilityPercent(0, 3);
			ClassicAssert.Greater(calmOverASeason, calmOverThreeDays, "the unwitnessed days were not credited");
			ClassicAssert.AreEqual(KingdomPowerRules.TypicalWindAvailabilityPercent, calmOverASeason, 1,
				"a long stretch did not converge on the typical wind");
			int galeOverASeason = KingdomPowerRules.WindAvailabilityPercent(KingdomPowerRules.RatedWindSpeedKph, 400);
			ClassicAssert.AreEqual(KingdomPowerRules.TypicalWindAvailabilityPercent, galeOverASeason, 1,
				"one gale paid for a season");
		}

		[Test]
		public void WindAvailabilityPercent_DoesNotOverflowOnANonsenseStretch()
		{
			int answer = KingdomPowerRules.WindAvailabilityPercent(60, int.MaxValue);
			ClassicAssert.GreaterOrEqual(answer, 0);
			ClassicAssert.LessOrEqual(answer, 100);
		}

		// --- DailyOutput / ChargeForDays: crew and weather both cut it -------------------------

		[TestCase(100, 100, KingdomPowerRules.MillChargePerDay)]
		[TestCase(50, 100, 1200)]
		[TestCase(33, 100, 792)]
		[TestCase(100, 50, 1200)]
		[TestCase(50, 50, 600)]
		[TestCase(0, 100, 0)]
		[TestCase(100, 0, 0)]
		[TestCase(-20, 100, 0)]
		[TestCase(400, 400, KingdomPowerRules.MillChargePerDay)]
		public void DailyOutput_ScalesByCrewThenByWhatTheGroundGives(int crew, int available, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomPowerRules.DailyOutput(KingdomPowerRules.PowerSource.Hands, crew, available));
		}

		// --- Addendum 10(b): damage dims a power work, staffed or not --------------------------

		[Test]
		public void DailyOutput_AStafflessPowerWorkDimsWithItsOwnWear()
		{
			// "Solar panels reduce power output." A design that asks for nobody used to be handed a
			// flat 100 by KingdomPower.DailyOutput's own staffed-only ternary, so a half-wrecked
			// one made a whole one's charge. The crew figure it is handed now is
			// KingdomWearRules.WorkEffectiveness, which for a staffless work IS its condition.
			int sound = KingdomPowerRules.DailyOutput(
				KingdomPowerRules.PowerSource.Wind, KingdomWearRules.WorkEffectiveness(0, 0, 0), 100);
			int wrecked = KingdomPowerRules.DailyOutput(
				KingdomPowerRules.PowerSource.Wind,
				KingdomWearRules.WorkEffectiveness(0, 0, KingdomMaterialRules.MaxWearPercent), 100);
			ClassicAssert.AreEqual(KingdomPowerRules.SailvaneChargePerDay, sound);
			ClassicAssert.Less(wrecked, sound, "a damaged vane makes less");
			ClassicAssert.Greater(wrecked, 0, "and it still turns: a damaged work stands");
			ClassicAssert.AreEqual(
				KingdomPowerRules.SailvaneChargePerDay * KingdomMaterialRules.ConditionPercent(KingdomMaterialRules.MaxWearPercent) / 100,
				wrecked, "output falls in exact proportion to condition");
		}

		[Test]
		public void DailyOutput_AStaffedPowerWorkDimsForBothReasonsAtOnce()
		{
			int wear = KingdomMaterialRules.MaxWearPercent / 2;
			int halfCrewSound = KingdomPowerRules.DailyOutput(
				KingdomPowerRules.PowerSource.Hands, KingdomWearRules.WorkEffectiveness(3, 50, 0), 100);
			int fullCrewWorn = KingdomPowerRules.DailyOutput(
				KingdomPowerRules.PowerSource.Hands, KingdomWearRules.WorkEffectiveness(3, 100, wear), 100);
			int both = KingdomPowerRules.DailyOutput(
				KingdomPowerRules.PowerSource.Hands, KingdomWearRules.WorkEffectiveness(3, 50, wear), 100);
			ClassicAssert.Less(both, halfCrewSound);
			ClassicAssert.Less(both, fullCrewWorn);
		}

		[Test]
		public void DailyOutput_MendingRestoresAWorksWholeOutput()
		{
			// Consequences are of damage, not of history: wear back at zero reads exactly as a
			// work that was never damaged.
			ClassicAssert.AreEqual(
				KingdomPowerRules.DailyOutput(KingdomPowerRules.PowerSource.Water, KingdomWearRules.WorkEffectiveness(0, 0, 0), 100),
				KingdomPowerRules.DailyOutput(KingdomPowerRules.PowerSource.Water, 100, 100));
		}

		[Test]
		public void DailyOutput_AHalfCrewedMillMakesHalfAMill()
		{
			// The pillar in one assertion: a work is worth the hands on it. A mutation dropping
			// crew from the calculation would make a lone settler worth three.
			int full = KingdomPowerRules.DailyOutput(KingdomPowerRules.PowerSource.Hands, 100, 100);
			int half = KingdomPowerRules.DailyOutput(KingdomPowerRules.PowerSource.Hands, 50, 100);
			ClassicAssert.AreEqual(full / 2, half);
			ClassicAssert.AreEqual(0, KingdomPowerRules.DailyOutput(KingdomPowerRules.PowerSource.Hands, 0, 100));
		}

		[TestCase(2400, 0, 0)]
		[TestCase(2400, 1, 2400)]
		[TestCase(2400, 2, 4800)]
		[TestCase(2400, 3, 7200)]
		[TestCase(2400, 31, 74400)]
		[TestCase(2400, -6, 0)]
		[TestCase(0, 3, 0)]
		[TestCase(-100, 3, 0)]
		public void ChargeForDays_IsOneDaysWorkTimesEveryDayThatPassed(int daily, int days, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomPowerRules.ChargeForDays(daily, days));
		}

		[Test]
		public void ChargeForDays_ASeasonAwayIsAWholeSeasonOfMilling()
		{
			// The uncapping, in the one place a founder feels it: the wheel turned while they
			// were gone.
			int daily = KingdomPowerRules.DailyOutput(KingdomPowerRules.PowerSource.Hands, 100, 100);
			ClassicAssert.AreEqual(daily * 200, KingdomPowerRules.ChargeForDays(daily, 200));
			ClassicAssert.Greater(KingdomPowerRules.ChargeForDays(daily, 200), KingdomPowerRules.ChargeForDays(daily, 3));
		}

		[Test]
		public void ChargeForDays_AnUnstaffedWorkMakesNothingHoweverLongTheStretch()
		{
			// Clause 2, and the reason uncapping power needed no ceiling of its own: a day's
			// output is already crew effectiveness times availability, so an unstaffed work
			// multiplies two hundred days by zero.
			int unstaffed = KingdomPowerRules.DailyOutput(KingdomPowerRules.PowerSource.Hands, 0, 100);
			ClassicAssert.AreEqual(0, KingdomPowerRules.ChargeForDays(unstaffed, 200));
			int becalmed = KingdomPowerRules.DailyOutput(KingdomPowerRules.PowerSource.Wind, 100, 0);
			ClassicAssert.AreEqual(0, KingdomPowerRules.ChargeForDays(becalmed, 200));
		}

		[Test]
		public void ChargeForDays_SaturatesRatherThanWrappingOnANonsenseStretch()
		{
			ClassicAssert.AreEqual(int.MaxValue, KingdomPowerRules.ChargeForDays(2400, int.MaxValue));
		}

		// --- The molten-salt store: throughput, room, and never a debt --------------------------

		[TestCase(24000, 1, 12000)]
		[TestCase(24000, 3, 36000)]
		[TestCase(24000, 90, 1080000)]
		[TestCase(24000, 0, 0)]
		[TestCase(0, 3, 0)]
		[TestCase(-1000, 3, 0)]
		public void ThroughputForDays_IsHalfTheStoreADayForEveryDayThatPassed(int capacity, int days, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomPowerRules.ThroughputForDays(capacity, days));
		}

		[Test]
		public void ThroughputForDays_SaturatesRatherThanWrappingOnANonsenseStretch()
		{
			ClassicAssert.AreEqual(int.MaxValue, KingdomPowerRules.ThroughputForDays(24000, int.MaxValue));
		}

		[Test]
		public void StorageCapacityIsWhatBoundsAnAbsenceNowAndNotTheClock()
		{
			// 022b's own answer to away-farming, and the reason retiring MaxDaysCredited took no
			// replacement: throughput rises with the days, and what may actually be KEPT does
			// not. A store the founder never enlarged holds exactly what it holds, however long
			// the wheel turned.
			int room = KingdomPowerRules.Absorbable(int.MaxValue / 2, 0, 24000, 400);
			ClassicAssert.AreEqual(24000, room, "a long absence filled more than the store could hold");
			ClassicAssert.AreEqual(0, KingdomPowerRules.Absorbable(int.MaxValue / 2, 0, 0, 400),
				"a settlement with no stores kept charge anyway");
		}

		[TestCase(5000, 0, 24000, 1, 5000)]
		[TestCase(20000, 0, 24000, 1, 12000)]
		[TestCase(20000, 20000, 24000, 1, 4000)]
		[TestCase(5000, 24000, 24000, 1, 0)]
		[TestCase(0, 0, 24000, 1, 0)]
		[TestCase(-500, 0, 24000, 1, 0)]
		[TestCase(5000, 0, 0, 1, 0)]
		[TestCase(5000, 0, 24000, 0, 0)]
		public void Absorbable_TakesWhatThereIsRoomForNoFasterThanTheCrewCanPourIt(int offered, int stored, int capacity, int days, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomPowerRules.Absorbable(offered, stored, capacity, days));
		}

		[Test]
		public void Absorbable_NeverExceedsTheOfferAndNeverOverfillsTheStore()
		{
			for (int offered = 0; offered <= 40000; offered += 2500)
			{
				for (int stored = 0; stored <= 24000; stored += 3000)
				{
					int taken = KingdomPowerRules.Absorbable(offered, stored, 24000, 3);
					ClassicAssert.GreaterOrEqual(taken, 0);
					ClassicAssert.LessOrEqual(taken, offered);
					ClassicAssert.LessOrEqual(stored + taken, 24000);
				}
			}
		}

		[TestCase(20000, 24000, 1, 12000)]
		[TestCase(5000, 24000, 1, 5000)]
		[TestCase(24000, 24000, 3, 24000)]
		[TestCase(0, 24000, 3, 0)]
		[TestCase(-9, 24000, 3, 0)]
		[TestCase(5000, 24000, 0, 0)]
		public void Releasable_GivesBackNoMoreThanItHolds(int stored, int capacity, int days, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomPowerRules.Releasable(stored, capacity, days));
		}

		[Test]
		public void Releasable_TheStoreCanNeverBeOverdrawnIntoADebt()
		{
			// Nothing in this settlement runs a deficit: a release is capped by what is
			// actually held, so a store can reach empty and stop, never go past it.
			for (int stored = 0; stored <= 24000; stored += 1000)
			{
				ClassicAssert.LessOrEqual(KingdomPowerRules.Releasable(stored, 24000, 3), stored);
				ClassicAssert.LessOrEqual(KingdomPowerRules.Releasable(stored, 24000, 400), stored);
			}
		}

		[Test]
		public void Store_AbsenceAccruesAndNeverDecays()
		{
			// The pillar, stated as arithmetic: across any span, what the store is asked to do
			// can only add to it or hand back what it already had. There is no rule here that
			// removes charge nobody drew.
			int stored = 8000;
			for (int days = 1; days <= 40; days++)
			{
				int added = KingdomPowerRules.Absorbable(3000, stored, 24000, days);
				ClassicAssert.GreaterOrEqual(added, 0);
				ClassicAssert.GreaterOrEqual(stored + added, stored);
				stored += added;
			}
			ClassicAssert.Greater(stored, 8000);
		}

		// --- ClassifySupply: none and idle are different sentences ------------------------------

		[TestCase(0, 0, 1, KingdomPowerRules.SupplyTier.None)]
		[TestCase(5000, 0, 1, KingdomPowerRules.SupplyTier.None)]
		[TestCase(0, 2, 1, KingdomPowerRules.SupplyTier.Idle)]
		[TestCase(1000, 1, 1, KingdomPowerRules.SupplyTier.Thin)]
		[TestCase(1999, 1, 1, KingdomPowerRules.SupplyTier.Thin)]
		[TestCase(2000, 1, 1, KingdomPowerRules.SupplyTier.Steady)]
		[TestCase(2400, 1, 1, KingdomPowerRules.SupplyTier.Steady)]
		[TestCase(7999, 1, 1, KingdomPowerRules.SupplyTier.Steady)]
		[TestCase(8000, 1, 1, KingdomPowerRules.SupplyTier.Ample)]
		[TestCase(8000, 1, 2, KingdomPowerRules.SupplyTier.Steady)]
		[TestCase(2400, 1, 0, KingdomPowerRules.SupplyTier.Steady)]
		public void ClassifySupply_LaddersAgainstWhatThePostsCouldSpend(int perDay, int works, int posts, KingdomPowerRules.SupplyTier expected)
		{
			ClassicAssert.AreEqual(expected, KingdomPowerRules.ClassifySupply(perDay, works, posts));
		}

		[Test]
		public void ClassifySupply_WorksThatMakeNothingAreIdleNotAbsent()
		{
			// Two states with two different remedies: build something, or crew what you built.
			// A mutation collapsing them would tell a founder with three dry wheels to go build
			// a fourth.
			ClassicAssert.AreEqual(KingdomPowerRules.SupplyTier.None, KingdomPowerRules.ClassifySupply(0, 0, 1));
			ClassicAssert.AreEqual(KingdomPowerRules.SupplyTier.Idle, KingdomPowerRules.ClassifySupply(0, 3, 1));
		}

		[Test]
		public void ClassifySupply_MorePostsNeverRaisesTheTier()
		{
			KingdomPowerRules.SupplyTier previous = KingdomPowerRules.SupplyTier.Ample;
			for (int posts = 1; posts <= 6; posts++)
			{
				KingdomPowerRules.SupplyTier now = KingdomPowerRules.ClassifySupply(9000, 2, posts);
				ClassicAssert.LessOrEqual((int)now, (int)previous, "tier rose when a post was added at " + posts);
				previous = now;
			}
		}

		[Test]
		public void SupplyTier_LaddersInTheDocumentedOrder()
		{
			ClassicAssert.Less((int)KingdomPowerRules.SupplyTier.None, (int)KingdomPowerRules.SupplyTier.Idle);
			ClassicAssert.Less((int)KingdomPowerRules.SupplyTier.Idle, (int)KingdomPowerRules.SupplyTier.Thin);
			ClassicAssert.Less((int)KingdomPowerRules.SupplyTier.Thin, (int)KingdomPowerRules.SupplyTier.Steady);
			ClassicAssert.Less((int)KingdomPowerRules.SupplyTier.Steady, (int)KingdomPowerRules.SupplyTier.Ample);
		}

		// --- Prose: every tier and every idle cause has its own words ---------------------------

		[TestCase(KingdomPowerRules.SupplyTier.None, "none")]
		[TestCase(KingdomPowerRules.SupplyTier.Idle, "idle")]
		[TestCase(KingdomPowerRules.SupplyTier.Thin, "thin")]
		[TestCase(KingdomPowerRules.SupplyTier.Steady, "steady")]
		[TestCase(KingdomPowerRules.SupplyTier.Ample, "ample")]
		public void SupplyTierName_NamesEveryTier(KingdomPowerRules.SupplyTier tier, string expected)
		{
			ClassicAssert.AreEqual(expected, KingdomPowerRules.SupplyTierName(tier));
		}

		[Test]
		public void IdleReason_NamesTheCauseAndNotJustTheSymptom()
		{
			string hands = KingdomPowerRules.IdleReason(KingdomPowerRules.PowerSource.Hands);
			string water = KingdomPowerRules.IdleReason(KingdomPowerRules.PowerSource.Water);
			string wind = KingdomPowerRules.IdleReason(KingdomPowerRules.PowerSource.Wind);
			ClassicAssert.AreNotEqual(hands, water);
			ClassicAssert.AreNotEqual(water, wind);
			ClassicAssert.AreNotEqual(hands, wind);
			ClassicAssert.AreEqual(KingdomPowerRules.IdleNoWater, water);
			ClassicAssert.AreEqual(KingdomPowerRules.IdleNoWind, wind);
			ClassicAssert.AreEqual(KingdomPowerRules.IdleNoCrew, hands);
		}

		[Test]
		public void SupplyLine_SaysNothingWhenThereIsNothingToSay()
		{
			ClassicAssert.AreEqual("", KingdomPowerRules.SupplyLine(KingdomPowerRules.SupplyTier.None, 0, 0, 0, KingdomPowerRules.IdleNoWorks));
		}

		[Test]
		public void SupplyLine_AnIdleSettlementIsToldWhy()
		{
			string line = KingdomPowerRules.SupplyLine(KingdomPowerRules.SupplyTier.Idle, 0, 0, 24000, KingdomPowerRules.IdleNoWater);
			ClassicAssert.IsTrue(line.Contains("idle"), line);
			ClassicAssert.IsTrue(line.Contains(KingdomPowerRules.IdleNoWater), line);
		}

		[Test]
		public void SupplyLine_AnIdleSettlementWithNoStatedCauseStillGetsASentence()
		{
			string line = KingdomPowerRules.SupplyLine(KingdomPowerRules.SupplyTier.Idle, 0, 0, 0, null);
			ClassicAssert.IsTrue(line.Contains(KingdomPowerRules.IdleNoCrew), line);
		}

		[Test]
		public void SupplyLine_AWorkingSettlementReadsAsOneSentenceWithItsNumbers()
		{
			string line = KingdomPowerRules.SupplyLine(KingdomPowerRules.SupplyTier.Steady, 7200, 14300, 24000, null);
			ClassicAssert.IsTrue(line.Contains("steady"), line);
			ClassicAssert.IsTrue(line.Contains("7200"), line);
			ClassicAssert.IsTrue(line.Contains("14300"), line);
			ClassicAssert.IsTrue(line.Contains("24000"), line);
			ClassicAssert.IsFalse(line.Contains("\n"), "the status line must stay one line: " + line);
		}

		[Test]
		public void SupplyLine_WithoutAStoreItSaysSoRatherThanReportingZeroOfZero()
		{
			string line = KingdomPowerRules.SupplyLine(KingdomPowerRules.SupplyTier.Steady, 4800, 0, 0, null);
			ClassicAssert.IsTrue(line.Contains("molten-salt"), line);
			ClassicAssert.IsFalse(line.Contains("0 of 0"), line);
		}

		// --- Shape guards on the tuning constants ------------------------------------------------

		[Test]
		public void EveryQuantityIsPositive()
		{
			ClassicAssert.Greater(KingdomPowerRules.MillChargePerDay, 0);
			ClassicAssert.Greater(KingdomPowerRules.WaterWheelChargePerDay, 0);
			ClassicAssert.Greater(KingdomPowerRules.SailvaneChargePerDay, 0);
			ClassicAssert.Greater(KingdomPowerRules.RatedWindSpeedKph, 0);
			ClassicAssert.Greater(KingdomPowerRules.SaltStoreThroughputDivisor, 0);
			ClassicAssert.Greater(KingdomPowerRules.PostDailyNeedCharge, 0);
			ClassicAssert.Greater(KingdomPowerRules.TypicalWindAvailabilityPercent, 0);
			ClassicAssert.LessOrEqual(KingdomPowerRules.TypicalWindAvailabilityPercent, 100);
		}
	}
}
#endif
