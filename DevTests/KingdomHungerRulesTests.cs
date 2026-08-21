#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The food lane as a flow (Wave B): what the settlement eats, what the wild gives it, the
	/// hunger ladder, and the rule that keeps two scarcity ladders from biting twice.
	/// <para>
	/// Written to be mutation-resistant against the specific mistakes this lane invites: a stage
	/// term creeping onto the ration bill, foraging's ceiling moving from the rate to the total,
	/// and the composition rule quietly becoming a sum.
	/// </para>
	/// </summary>
	public class KingdomHungerRulesTests
	{
		// --- RationsPerDay: one a settler a day, at EVERY rung -----------------------------

		[TestCase(0, 0)]
		[TestCase(-7, 0)]
		[TestCase(1, 1)]
		[TestCase(4, 4)]
		[TestCase(50, 50)]
		public void RationsPerDay_IsOneRationPerSettler(int population, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.RationsPerDay(population));
		}

		[Test]
		public void RationsPerDay_HasNoStageTerm_WhichIsWhatMakesTheLevelAndTheBillTheSameNumber()
		{
			// The load-bearing divergence from the water lane. UpkeepDrams multiplies by
			// StageUpkeepPercent and KingdomSubsidenceRules.LevelFromWater divides it back out;
			// food does neither, so the food arm of Equilibrium IS the daily ration bill. A
			// mutation that scales this by stage would silently invalidate every food figure in
			// KingdomBuildings.xml, and nothing else in the suite would notice.
			foreach (GrowthStage stage in Enum.GetValues(typeof(GrowthStage)))
			{
				Assert.AreEqual(20, KingdomRules.RationsPerDay(20),
					"a settlement's ration bill must not depend on what it has become (" + stage + ")");
			}
			// And the identity itself, stated where a reader will find it: a settlement whose
			// works carry N food eats exactly N a day, so it is neutral at its own level.
			Assert.AreEqual(37, KingdomRules.RationsPerDay(37));
		}

		[Test]
		public void RationsForElapsed_ChargesWholeDaysAndNeverAPartOne()
		{
			Assert.AreEqual(0, KingdomRules.RationsForElapsed(10, KingdomRules.TicksPerDay - 1));
			Assert.AreEqual(10, KingdomRules.RationsForElapsed(10, KingdomRules.TicksPerDay));
			Assert.AreEqual(10, KingdomRules.RationsForElapsed(10, KingdomRules.TicksPerDay * 2L - 1L));
			Assert.AreEqual(20, KingdomRules.RationsForElapsed(10, KingdomRules.TicksPerDay * 2L));
		}

		[Test]
		public void RationsForElapsed_IsUncappedOverAnAbsenceOfAnyLength()
		{
			// Addendum 8 clause 1, in food's voice: people go on eating whether anyone watched.
			// A cap reappearing here would make a season away cheaper than a season present.
			Assert.AreEqual(365 * 6, KingdomRules.RationsForElapsed(6, KingdomRules.TicksPerDay * 365L));
			Assert.Greater(
				KingdomRules.RationsForElapsed(6, KingdomRules.TicksPerDay * 100L),
				KingdomRules.RationsForElapsed(6, KingdomRules.TicksPerDay * 10L));
		}

		[TestCase(0)]
		[TestCase(-1)]
		[TestCase(-100000)]
		public void RationsForElapsed_MintsNothingFromANonForwardStretch(long elapsed)
		{
			Assert.AreEqual(0, KingdomRules.RationsForElapsed(10, elapsed));
		}

		[Test]
		public void RationsForElapsed_SaturatesRatherThanWrapping()
		{
			int bill = KingdomRules.RationsForElapsed(KingdomRules.MaxPopulation, long.MaxValue / 2L);
			Assert.GreaterOrEqual(bill, 0, "a corrupt stamp must ask for everything, never for a negative");
		}

		// --- ForagedRations: hands off the land, under a flat daily ceiling ----------------

		[TestCase(0, 5, 0)]
		[TestCase(-3, 5, 0)]
		[TestCase(5, 0, 0)]
		[TestCase(5, -2, 0)]
		public void ForagedRations_GivesNothingWithoutHandsAndDays(int hands, int days, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.ForagedRations(hands, days));
		}

		[Test]
		public void ForagedRations_PaysPerHandUntilTheGroundRunsOut()
		{
			Assert.AreEqual(KingdomRules.ForageRationsPerHand, KingdomRules.ForagedRations(1, 1));
			Assert.AreEqual(KingdomRules.MaxForagedRationsPerDay, KingdomRules.ForagedRations(2, 1));
			// Every further hand adds nothing: the wild does not care how many baskets you bring.
			Assert.AreEqual(KingdomRules.MaxForagedRationsPerDay, KingdomRules.ForagedRations(40, 1));
		}

		[Test]
		public void ForagedRations_ClampsTheRateAndNotTheTotal()
		{
			// The mutation this exists to catch: clamping after the days multiply out would let a
			// hundred-day absence forage a hundred days of a CITY's worth in one homecoming, and
			// foraging would stop being a Camp-only answer.
			Assert.AreEqual(KingdomRules.MaxForagedRationsPerDay * 10, KingdomRules.ForagedRations(40, 10));
			Assert.AreNotEqual(KingdomRules.MaxForagedRationsPerDay, KingdomRules.ForagedRations(40, 10));
		}

		[Test]
		public void ForagingsCeilingIsTheCampItIsMeantToCarry()
		{
			// Pinned by test rather than by a code dependency: KingdomCatalogueRules reads
			// KingdomRules and not the other way round, so the two numbers agreeing is an
			// authored fact that has to be asserted somewhere or it will drift.
			Assert.AreEqual(KingdomCatalogueRules.FloorLevel, KingdomRules.MaxForagedRationsPerDay,
				"the wild feeds exactly the camp a place carries with nothing standing");
		}

		[Test]
		public void ACampFeedsItselfOffTheLandWithHalfItsPeopleOnTheWater()
		{
			// The floor this whole lane had to clear, and the exact mirror of Q7's "Camp wants
			// half its people on water": four people, two on the detail, two foraging, four
			// rations covered. A founder who has commissioned nothing is never starved.
			int camp = KingdomCatalogueRules.FloorLevel;
			int onTheWater = camp / 2;
			Assert.GreaterOrEqual(
				KingdomRules.ForagedRations(camp - onTheWater, 1),
				KingdomRules.RationsPerDay(camp));
		}

		[Test]
		public void NothingAboveACampCanBeFedByForagingAlone()
		{
			// The other half of the same claim, and the reason the ceiling exists: if the wild
			// could feed a Village the food lane would never bind and the whole catalogue's food
			// ladder would be decoration.
			for (int population = KingdomCatalogueRules.FloorLevel + 1; population <= KingdomRules.MaxPopulation; population++)
			{
				Assert.Less(
					KingdomRules.ForagedRations(population, 1),
					KingdomRules.RationsPerDay(population),
					"foraging must not carry a settlement of " + population);
			}
		}

		// --- ResolveHunger: the thirst ladder's shape, in food's voice ---------------------

		[TestCase(0, GrowthStage.City, 40, KingdomRules.HungerOutcome.Fed)]
		[TestCase(-4, GrowthStage.City, 40, KingdomRules.HungerOutcome.Fed)]
		[TestCase(1, GrowthStage.City, 40, KingdomRules.HungerOutcome.Warned)]
		[TestCase(2, GrowthStage.City, 40, KingdomRules.HungerOutcome.Emigration)]
		[TestCase(3, GrowthStage.City, 40, KingdomRules.HungerOutcome.Famine)]
		[TestCase(99, GrowthStage.City, 40, KingdomRules.HungerOutcome.Famine)]
		public void ResolveHunger_ClimbsItsOwnLadder(int streak, GrowthStage stage, int population, KingdomRules.HungerOutcome expected)
		{
			Assert.AreEqual(expected, KingdomRules.ResolveHunger(streak, stage, population));
		}

		[Test]
		public void ResolveHunger_NeverMarksACampBecauseThereIsNoRungBeneathIt()
		{
			Assert.AreEqual(KingdomRules.HungerOutcome.Emigration,
				KingdomRules.ResolveHunger(99, GrowthStage.Camp, 40));
		}

		[Test]
		public void ResolveHunger_NeverEmptiesTheLoyalCore()
		{
			Assert.AreEqual(KingdomRules.HungerOutcome.Warned,
				KingdomRules.ResolveHunger(2, GrowthStage.Camp, KingdomRules.LoyalCoreSettlers));
			Assert.AreEqual(KingdomRules.HungerOutcome.Warned,
				KingdomRules.ResolveHunger(99, GrowthStage.Camp, 1));
		}

		[Test]
		public void ResolveHunger_IsTheSameLadderAsResolveThirstRungForRung()
		{
			// Not decoration: KingdomRules.ComposeScarcity takes a maximum over the two, and a
			// ladder with a rung the other does not have could not be composed with it.
			Assert.AreEqual(KingdomRules.DryIntervalsToEmigrate, KingdomRules.HungryIntervalsToEmigrate);
			Assert.AreEqual(KingdomRules.DryIntervalsToWither, KingdomRules.HungryIntervalsToFamine);
			Assert.AreEqual(
				Enum.GetValues(typeof(KingdomRules.ThirstOutcome)).Length,
				Enum.GetValues(typeof(KingdomRules.HungerOutcome)).Length);
			for (int streak = -2; streak <= 6; streak++)
			{
				Assert.AreEqual(
					KingdomRules.BiteOfThirst(KingdomRules.ResolveThirst(streak, GrowthStage.Town, 20)),
					KingdomRules.BiteOfHunger(KingdomRules.ResolveHunger(streak, GrowthStage.Town, 20)),
					"the two ladders must bite identically at streak " + streak);
			}
		}

		// --- ComposeScarcity: the worse of the two, never their sum ------------------------

		private static readonly KingdomRules.ThirstOutcome[] EveryThirst = (KingdomRules.ThirstOutcome[])
			Enum.GetValues(typeof(KingdomRules.ThirstOutcome));

		private static readonly KingdomRules.HungerOutcome[] EveryHunger = (KingdomRules.HungerOutcome[])
			Enum.GetValues(typeof(KingdomRules.HungerOutcome));

		private static int DeparturesOf(KingdomRules.ScarcityBite Bite)
		{
			return (Bite >= KingdomRules.ScarcityBite.Departure) ? 1 : 0;
		}

		[Test]
		public void ComposeScarcity_NeverCostsMoreThanTheWorseLadderAloneWould()
		{
			// THE no-death-spiral property, asserted exhaustively rather than sampled: a dry AND
			// starving city must never empty faster than whichever of the two alone would empty
			// it. A mutation turning the maximum into a sum fails on the four rows where both
			// ladders are at Emigration or worse.
			for (int t = 0; t < EveryThirst.Length; t++)
			{
				for (int h = 0; h < EveryHunger.Length; h++)
				{
					int alone = Math.Max(
						DeparturesOf(KingdomRules.BiteOfThirst(EveryThirst[t])),
						DeparturesOf(KingdomRules.BiteOfHunger(EveryHunger[h])));
					int together = DeparturesOf(KingdomRules.ComposeScarcity(EveryThirst[t], EveryHunger[h]).Bite);
					Assert.AreEqual(alone, together,
						EveryThirst[t] + " with " + EveryHunger[h] + " must cost exactly what the worse of them alone costs");
				}
			}
		}

		[Test]
		public void ComposeScarcity_NeverCostsMoreThanOneSettlerInASingleResolve()
		{
			for (int t = 0; t < EveryThirst.Length; t++)
			{
				for (int h = 0; h < EveryHunger.Length; h++)
				{
					Assert.LessOrEqual(
						DeparturesOf(KingdomRules.ComposeScarcity(EveryThirst[t], EveryHunger[h]).Bite), 1);
				}
			}
		}

		[Test]
		public void ComposeScarcity_TakesTheWorseBiteOfTheTwo()
		{
			for (int t = 0; t < EveryThirst.Length; t++)
			{
				for (int h = 0; h < EveryHunger.Length; h++)
				{
					KingdomRules.ScarcityBite expected = KingdomRules.BiteOfThirst(EveryThirst[t]);
					KingdomRules.ScarcityBite fromHunger = KingdomRules.BiteOfHunger(EveryHunger[h]);
					if (fromHunger > expected)
					{
						expected = fromHunger;
					}
					Assert.AreEqual(expected, KingdomRules.ComposeScarcity(EveryThirst[t], EveryHunger[h]).Bite);
				}
			}
		}

		[Test]
		public void ComposeScarcity_LetsBothMarksStandBecauseAMarkIsAStateAndNotACost()
		{
			KingdomRules.ScarcityVerdict both = KingdomRules.ComposeScarcity(
				KingdomRules.ThirstOutcome.Withering, KingdomRules.HungerOutcome.Famine);
			Assert.IsTrue(both.Withering);
			Assert.IsTrue(both.Famishing);
			Assert.AreEqual(KingdomRules.ScarcityBite.Terminal, both.Bite);
			Assert.AreEqual(1, DeparturesOf(both.Bite), "two marks, one departure");
		}

		[Test]
		public void ComposeScarcity_ReportsWhichLadderIsShortSoTheDepartureCanBeNamed()
		{
			KingdomRules.ScarcityVerdict dry = KingdomRules.ComposeScarcity(
				KingdomRules.ThirstOutcome.Emigration, KingdomRules.HungerOutcome.Fed);
			Assert.IsTrue(dry.Thirsting);
			Assert.IsFalse(dry.Starving);
			KingdomRules.ScarcityVerdict hungry = KingdomRules.ComposeScarcity(
				KingdomRules.ThirstOutcome.Sustained, KingdomRules.HungerOutcome.Warned);
			Assert.IsFalse(hungry.Thirsting);
			Assert.IsTrue(hungry.Starving);
		}

		[Test]
		public void ComposeScarcity_IsHealthyOnlyWhenBothLaddersArePaid()
		{
			Assert.IsTrue(KingdomRules.ComposeScarcity(
				KingdomRules.ThirstOutcome.Sustained, KingdomRules.HungerOutcome.Fed).Healthy);
			// A merely WARNED settlement is already unhealthy, which is what stops a settler
			// walking into a place that could not feed or water the people already in it.
			Assert.IsFalse(KingdomRules.ComposeScarcity(
				KingdomRules.ThirstOutcome.Sustained, KingdomRules.HungerOutcome.Warned).Healthy);
			Assert.IsFalse(KingdomRules.ComposeScarcity(
				KingdomRules.ThirstOutcome.Warned, KingdomRules.HungerOutcome.Fed).Healthy);
		}

		[Test]
		public void ScarcityBiteIsOrderedSoComposingIsAMaximum()
		{
			Assert.Less(KingdomRules.ScarcityBite.None, KingdomRules.ScarcityBite.Warned);
			Assert.Less(KingdomRules.ScarcityBite.Warned, KingdomRules.ScarcityBite.Departure);
			Assert.Less(KingdomRules.ScarcityBite.Departure, KingdomRules.ScarcityBite.Terminal);
		}

		// --- The departure clauses ---------------------------------------------------------

		[Test]
		public void ScarcityDepartureClause_KeepsTheShippedDroughtSentenceWordForWord()
		{
			// KingdomGrowth.Emigrate's own default, which this now supplies. If it drifts, every
			// drought departure in every existing chronicle stops matching the new ones.
			Assert.AreEqual("for wetter country, the cisterns having run dry",
				KingdomRules.ScarcityDepartureClause(Thirsting: true, Starving: false));
			Assert.AreEqual("for wetter country",
				KingdomRules.ScarcityDepartureNote(Thirsting: true, Starving: false));
		}

		[Test]
		public void ScarcityDepartureClause_NamesHungerAndBothInTheirOwnWords()
		{
			string hunger = KingdomRules.ScarcityDepartureClause(Thirsting: false, Starving: true);
			string both = KingdomRules.ScarcityDepartureClause(Thirsting: true, Starving: true);
			string thirst = KingdomRules.ScarcityDepartureClause(Thirsting: true, Starving: false);
			Assert.AreNotEqual(thirst, hunger);
			Assert.AreNotEqual(thirst, both);
			Assert.AreNotEqual(hunger, both);
			Assert.IsNotEmpty(hunger);
			Assert.IsNotEmpty(both);
		}

		[Test]
		public void ScarcityDepartureClause_HasNothingToSayWhenNothingIsWrong()
		{
			Assert.IsNull(KingdomRules.ScarcityDepartureClause(Thirsting: false, Starving: false));
			Assert.IsNull(KingdomRules.ScarcityDepartureNote(Thirsting: false, Starving: false));
		}

		// --- Where the settlement keeps its food -------------------------------------------

		[TestCase(0)]
		[TestCase(-1)]
		[TestCase(-9999)]
		public void LarderCapacity_FallsBackRatherThanEverReadingAsZero(int declared)
		{
			// A dedicated larder that holds nothing is a silent black hole for a harvest, and
			// there is no surface anywhere that would show the founder why.
			Assert.AreEqual(KingdomRules.DefaultLarderCapacity, KingdomRules.LarderCapacity(declared));
			Assert.Greater(KingdomRules.LarderCapacity(declared), 0);
		}

		[TestCase(1)]
		[TestCase(64)]
		[TestCase(288)]
		public void LarderCapacity_TakesADeclaredSizeAtItsWord(int declared)
		{
			Assert.AreEqual(declared, KingdomRules.LarderCapacity(declared));
		}

		[Test]
		public void IsCivicLarderBlueprint_KnowsTheTwoPantriesAndNothingElse()
		{
			Assert.IsTrue(KingdomRules.IsCivicLarderBlueprint("r_KingdomLarder"));
			Assert.IsTrue(KingdomRules.IsCivicLarderBlueprint("r_KingdomGranary"));
			// The charging post carries a Container/Inventory pair and is not a pantry, which is
			// the whole reason this is a named list rather than "has an Inventory".
			Assert.IsFalse(KingdomRules.IsCivicLarderBlueprint("r_KingdomChargingPost"));
			Assert.IsFalse(KingdomRules.IsCivicLarderBlueprint("Chest"));
			Assert.IsFalse(KingdomRules.IsCivicLarderBlueprint(null));
			Assert.IsFalse(KingdomRules.IsCivicLarderBlueprint(""));
		}

		[Test]
		public void CivicLarderBlueprints_StillNamesTheOneTheScaffoldPathHardcoded()
		{
			// r_KingdomScaffold.LarderBlueprint is that same string; if this list ever stopped
			// containing it, a commissioned larder shed would stop being dedicated by the
			// settlement pass and the two paths would disagree about what a pantry is.
			CollectionAssert.Contains(KingdomRules.CivicLarderBlueprints, "r_KingdomLarder");
		}
	}
}
#endif
