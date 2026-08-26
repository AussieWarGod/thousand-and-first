#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;
using Capability = ThousandAndFirst.KingdomCrewRules.SettlerCapability;
using Demand = ThousandAndFirst.KingdomCrewRules.CrewDemand;
using Outcome = ThousandAndFirst.KingdomCrewRules.CrewOutcome;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Crew capability arithmetic: parsing <c>CrewNeeds</c>, the capability/headcount
	/// effectiveness tables, and the ablest-first, deterministic draw
	/// (BUILDING-CATALOGUE-BRIEF.md Addenda 6/7). Every number is asserted directly, so removing
	/// the shortfall floor, letting the draw fall back to arrival order, or crediting a settler's
	/// capability to the wrong demand fails a test here rather than only showing up as a silent
	/// stall in play (STANDARDS 7b).
	/// </summary>
	public class KingdomCrewRulesTests
	{
		private static Capability Cap(int Strength, int Intelligence, bool Tireless = false)
		{
			return new Capability(Strength, Intelligence, Tireless);
		}

		// --- Parsing: CrewNeeds is Carries' own kind:amount language, reused -------------------

		[Test]
		public void ParsesOneKindAndAmount()
		{
			Assert.IsTrue(KingdomCrewRules.TryParseCrewNeeds("strength:16", out var needs, out var error));
			Assert.IsNull(error);
			Assert.AreEqual(1, needs.Count);
			Assert.AreEqual("strength", needs[0].Kind);
			Assert.AreEqual(16, needs[0].Amount);
		}

		[Test]
		public void ParsesMultipleKinds()
		{
			Assert.IsTrue(KingdomCrewRules.TryParseCrewNeeds("strength:16,intelligence:20", out var needs, out _));
			Assert.AreEqual(2, needs.Count);
			Assert.AreEqual(16, KingdomCrewRules.ThresholdOf(needs, "strength"));
			Assert.AreEqual(20, KingdomCrewRules.ThresholdOf(needs, "intelligence"));
		}

		[Test]
		public void BlankCrewNeedsIsAnEmptyListNotAFault()
		{
			Assert.IsTrue(KingdomCrewRules.TryParseCrewNeeds(null, out var needs, out var error));
			Assert.IsNull(error);
			Assert.AreEqual(0, needs.Count);
			Assert.AreEqual(0, KingdomCrewRules.ThresholdOf(needs, "strength"));
		}

		[Test]
		public void MalformedCrewNeedsFails()
		{
			Assert.IsFalse(KingdomCrewRules.TryParseCrewNeeds("strength", out _, out var error));
			Assert.IsNotNull(error);
		}

		[Test]
		public void RepeatedKindsSumLikeCarriesDoes()
		{
			Assert.IsTrue(KingdomCrewRules.TryParseCrewNeeds("strength:10,strength:6", out var needs, out _));
			Assert.AreEqual(16, KingdomCrewRules.ThresholdOf(needs, "strength"));
		}

		// --- SettlerCapability: derive before authoring -----------------------------------------

		[Test]
		public void OrdinarySettlerReadsExactlyTheirOwnStats()
		{
			Capability c = Cap(12, 9);
			Assert.AreEqual(12, c.ValueOf(KingdomCrewRules.KindStrength));
			Assert.AreEqual(9, c.ValueOf(KingdomCrewRules.KindIntelligence));
		}

		[Test]
		public void CarriedSkillsEnterTheExistingCapabilityThresholdWithoutChangingStats()
		{
			KingdomCrewRules.WorkerSkills practiced = new KingdomCrewRules.WorkerSkills(
				Tinkering: true, Harvestry: false, Customs: false, Physic: false,
				Wayfaring: false);
			Capability tinker = new Capability(8, 12, false,
				default(KingdomIdentityAffinityRules.WorkerIdentity), practiced);
			Capability willing = Cap(18, 18);
			Assert.AreEqual(1, tinker.ValueOf(KingdomCrewRules.KindTinkering));
			Assert.AreEqual(0, willing.ValueOf(KingdomCrewRules.KindTinkering));
			Assert.AreEqual(12, tinker.ValueOf(KingdomCrewRules.KindIntelligence));

			Outcome[] outcome = KingdomCrewRules.AssignCrew(new[] { willing, tinker },
				new[] { new Demand(1, false, KingdomCrewRules.KindTinkering, 1, "craft") });
			Assert.AreEqual(1, outcome[0].SettlerIndices[0]);
			Assert.AreEqual(1, outcome[0].BestCapability);
		}

		[Test]
		public void ShippedNamedSettlersCarryTheSkillsTheirWorksAskFor()
		{
			string objects = TestMain.ReadRepositoryText("ObjectBlueprints.xml");
			StringAssert.Contains("Name=\"r_KingdomSettlerTinker\"", objects);
			StringAssert.Contains("Name=\"Tinkering_Tinker1\"", objects);
			StringAssert.Contains("Name=\"CookingAndGathering_Harvestry\"", objects);
			StringAssert.Contains("Name=\"Customs_Tactful\"", objects);
			StringAssert.Contains("Name=\"r_KingdomSettlerPhysicker\"", objects);
			StringAssert.Contains("Name=\"Physic_Nostrums\"", objects);

			string catalogue = TestMain.ReadRepositoryText("KingdomBuildings.xml");
			StringAssert.Contains("CrewNeeds=\"skill.tinkering:1\"", catalogue);
			StringAssert.Contains("CrewNeeds=\"skill.harvestry:1\"", catalogue);
			StringAssert.Contains("CrewNeeds=\"skill.customs:1\"", catalogue);
			StringAssert.Contains("CrewNeeds=\"skill.physic:1\"", catalogue);
		}

		[Test]
		public void UnknownCapabilityKindReadsZero()
		{
			Capability c = Cap(30, 30);
			Assert.AreEqual(0, c.ValueOf("agility"));
			Assert.AreEqual(0, c.ValueOf(""));
			Assert.AreEqual(0, c.ValueOf(null));
		}

		[TestCase(1, KingdomCrewRules.TirelessStrengthFloor)]
		[TestCase(19, KingdomCrewRules.TirelessStrengthFloor)]
		[TestCase(20, 20)]
		[TestCase(25, 25)]
		public void ARobotsStrengthNeverFallsUnderTheTirelessFloor(int RawStrength, int Expected)
		{
			Capability c = Cap(RawStrength, 10, Tireless: true);
			Assert.AreEqual(Expected, c.ValueOf(KingdomCrewRules.KindStrength));
		}

		[Test]
		public void TirelessNeverBoostsIntelligence()
		{
			Capability c = Cap(5, 5, Tireless: true);
			Assert.AreEqual(5, c.ValueOf(KingdomCrewRules.KindIntelligence), "being tireless says nothing about being certified");
		}

		[Test]
		public void ANonTirelessSettlerGetsNoFloor()
		{
			Capability c = Cap(3, 10, Tireless: false);
			Assert.AreEqual(3, c.ValueOf(KingdomCrewRules.KindStrength));
		}

		// --- CapabilityEffectiveness: slower, never stalled -------------------------------------

		[Test]
		public void NoThresholdIsAlwaysFullEffectiveness()
		{
			Assert.AreEqual(100, KingdomCrewRules.CapabilityEffectiveness(0, 0));
			Assert.AreEqual(100, KingdomCrewRules.CapabilityEffectiveness(0, -1));
		}

		[Test]
		public void MeetingOrExceedingTheThresholdIsFullEffectiveness()
		{
			Assert.AreEqual(100, KingdomCrewRules.CapabilityEffectiveness(16, 16));
			Assert.AreEqual(100, KingdomCrewRules.CapabilityEffectiveness(30, 16));
		}

		[Test]
		public void PartialCapabilityScalesTowardTheThreshold()
		{
			// 8 of 16 scales to 50, well clear of the floor.
			Assert.AreEqual(50, KingdomCrewRules.CapabilityEffectiveness(8, 16));
		}

		[Test]
		public void NoCapableHandsRunsSlowNeverStalls()
		{
			// The pure arithmetic case the brief names by name: no capable hands at all still
			// never reads zero -- it floors, and never drops the work to idle by capability alone.
			Assert.AreEqual(KingdomCrewRules.MinCapabilityEffectiveness, KingdomCrewRules.CapabilityEffectiveness(0, 16));
			Assert.Greater(KingdomCrewRules.CapabilityEffectiveness(0, 16), 0);
		}

		[Test]
		public void ATinyShortfallFloorsRatherThanReadingNearZero()
		{
			// 1 of 16 scales to 6, under the floor -- floors up rather than crediting almost nothing.
			Assert.AreEqual(KingdomCrewRules.MinCapabilityEffectiveness, KingdomCrewRules.CapabilityEffectiveness(1, 16));
		}

		[TestCase(0, 100)]
		[TestCase(50, 60)]
		[TestCase(100, 100)]
		public void CombinedEffectivenessIsTheLesserOfTheTwo(int Headcount, int CapabilityEff)
		{
			Assert.AreEqual(System.Math.Min(Headcount, CapabilityEff), KingdomCrewRules.CombinedEffectiveness(Headcount, CapabilityEff));
		}

		// --- AssignCrew: ablest-first, deterministic --------------------------------------------

		[Test]
		public void ADemandNamingACapabilityKindDrawsTheAblestFirst()
		{
			Capability[] pool = new Capability[] { Cap(5, 0), Cap(20, 0), Cap(10, 0) };
			Demand[] demands = new Demand[] { new Demand(2, false, KingdomCrewRules.KindStrength, 16) };
			Outcome[] outcomes = KingdomCrewRules.AssignCrew(pool, demands);
			Assert.AreEqual(2, outcomes[0].Assigned);
			CollectionAssert.AreEqual(new int[] { 1, 2 }, outcomes[0].SettlerIndices, "index 1 (20) and index 2 (10) are the two ablest, in that order");
			Assert.AreEqual(20, outcomes[0].BestCapability);
		}

		[Test]
		public void TiedCapabilityBreaksOnStableAscendingIndex()
		{
			Capability[] pool = new Capability[] { Cap(10, 0), Cap(10, 0), Cap(10, 0) };
			Demand[] demands = new Demand[] { new Demand(2, false, KingdomCrewRules.KindStrength, 5) };
			Outcome[] first = KingdomCrewRules.AssignCrew(pool, demands);
			Outcome[] second = KingdomCrewRules.AssignCrew(pool, demands);
			CollectionAssert.AreEqual(new int[] { 0, 1 }, first[0].SettlerIndices);
			CollectionAssert.AreEqual(first[0].SettlerIndices, second[0].SettlerIndices, "the same pool always yields the same draw");
		}

		[Test]
		public void ADemandNamingNoCapabilityKindDrawsInArrivalOrder()
		{
			Capability[] pool = new Capability[] { Cap(1, 0), Cap(99, 0), Cap(50, 0) };
			Demand[] demands = new Demand[] { new Demand(2, false, null, 0) };
			Outcome[] outcomes = KingdomCrewRules.AssignCrew(pool, demands);
			CollectionAssert.AreEqual(new int[] { 0, 1 }, outcomes[0].SettlerIndices, "no capability kind named -- plain arrival order, same as headcount-only allocation");
			Assert.AreEqual(0, outcomes[0].BestCapability, "capability is never credited to a demand that never asked for it");
		}

		[Test]
		public void HandsAreSpentOnceAcrossPriorityOrderedDemands()
		{
			Capability[] pool = new Capability[] { Cap(20, 0), Cap(15, 0), Cap(5, 0) };
			Demand[] demands = new Demand[]
			{
				new Demand(2, false, KingdomCrewRules.KindStrength, 10),
				new Demand(2, false, KingdomCrewRules.KindStrength, 10)
			};
			Outcome[] outcomes = KingdomCrewRules.AssignCrew(pool, demands);
			CollectionAssert.AreEqual(new int[] { 0, 1 }, outcomes[0].SettlerIndices, "the first, higher-priority demand gets the two ablest hands");
			CollectionAssert.AreEqual(new int[] { 2 }, outcomes[1].SettlerIndices, "only the one hand nobody else has taken is left");
			Assert.AreEqual(1, outcomes[1].Assigned, "a settler crewed elsewhere this pass is not double-booked");
		}

		[Test]
		public void AThresholdDemandGivesAllOrNothingByHeadcountRegardlessOfCapability()
		{
			Capability[] pool = new Capability[] { Cap(99, 0) };
			Demand[] demands = new Demand[] { new Demand(3, true, KingdomCrewRules.KindStrength, 5) };
			Outcome[] outcomes = KingdomCrewRules.AssignCrew(pool, demands);
			Assert.AreEqual(0, outcomes[0].Assigned, "one able hand is still short of the three-strong threshold this work needs at all");
			Assert.AreEqual(0, outcomes[0].BestCapability);
		}

		[Test]
		public void AScaledDemandTakesWhateverHandsAreLeft()
		{
			Capability[] pool = new Capability[] { Cap(1, 0) };
			Demand[] demands = new Demand[] { new Demand(3, false, KingdomCrewRules.KindStrength, 5) };
			Outcome[] outcomes = KingdomCrewRules.AssignCrew(pool, demands);
			Assert.AreEqual(1, outcomes[0].Assigned, "scaled work runs at whatever fraction it has hands for");
		}

		[Test]
		public void AZeroHeadcountDemandIsAlwaysAnEmptyOutcome()
		{
			Capability[] pool = new Capability[] { Cap(99, 99) };
			Demand[] demands = new Demand[] { new Demand(0, false, KingdomCrewRules.KindStrength, 5) };
			Outcome[] outcomes = KingdomCrewRules.AssignCrew(pool, demands);
			Assert.AreEqual(0, outcomes[0].Assigned);
			Assert.AreEqual(0, outcomes[0].SettlerIndices.Length);
		}

		[Test]
		public void NullPoolAndDemandsReadAsEmptyRatherThanThrowing()
		{
			Assert.AreEqual(0, KingdomCrewRules.AssignCrew(null, null).Length);
			Assert.AreEqual(0, KingdomCrewRules.AssignCrew(null, new Demand[] { new Demand(1, false, null, 0) })[0].Assigned);
		}

		[Test]
		public void OutcomeCarriesTheDemandsOwnKindAndThresholdForwardForEffectivenessAndAnnouncement()
		{
			Capability[] pool = new Capability[] { Cap(8, 0) };
			Demand[] demands = new Demand[] { new Demand(1, false, KingdomCrewRules.KindStrength, 16) };
			Outcome[] outcomes = KingdomCrewRules.AssignCrew(pool, demands);
			Assert.AreEqual(KingdomCrewRules.KindStrength, outcomes[0].CapabilityKind);
			Assert.AreEqual(16, outcomes[0].CapabilityThreshold);
			Assert.AreEqual(8, outcomes[0].BestCapability);
			int effectiveness = KingdomCrewRules.CapabilityEffectiveness(outcomes[0].BestCapability, outcomes[0].CapabilityThreshold);
			Assert.AreEqual(50, effectiveness);
		}

		// --- Naming the shortfall (STANDARDS 7b) ------------------------------------------------

		[Test]
		public void ShortfallLineNamesTheWorkTheWantAndWhatWasBrought()
		{
			string line = KingdomCrewRules.ShortfallLine("The Smithy", KingdomCrewRules.KindStrength, 8, 16);
			StringAssert.Contains("The Smithy", line);
			StringAssert.Contains("16", line);
			StringAssert.Contains("8", line);
		}

		[Test]
		public void ShortfallLineNamesNoHandsDistinctlyFromAWeakHand()
		{
			string line = KingdomCrewRules.ShortfallLine("The Smithy", KingdomCrewRules.KindStrength, 0, 16);
			StringAssert.DoesNotContain(" 0", line, "no hand there has any -- never a literal zero in the sentence");
		}

		[Test]
		public void ShortfallLineFallsBackToAWorkForABlankName()
		{
			string line = KingdomCrewRules.ShortfallLine(null, KingdomCrewRules.KindStrength, 4, 16);
			StringAssert.StartsWith("A work", line);
		}

		[Test]
		public void DisplayKindNamesTheTwoKnownKindsAndFallsBackForAnUnknownOne()
		{
			Assert.AreEqual("strength", KingdomCrewRules.DisplayKind(KingdomCrewRules.KindStrength));
			Assert.AreEqual("a certified mind", KingdomCrewRules.DisplayKind(KingdomCrewRules.KindIntelligence));
			Assert.AreEqual("theirmod:quickness", KingdomCrewRules.DisplayKind("theirmod:quickness"));
			Assert.AreEqual("capability", KingdomCrewRules.DisplayKind(null));
		}

		// --- Stat name mapping: what KingdomCrews reads off a real GameObject ------------------

		[Test]
		public void StatNameForMapsTheTwoKnownKindsToVanillaStatNames()
		{
			Assert.AreEqual("Strength", KingdomCrewRules.StatNameFor(KingdomCrewRules.KindStrength));
			Assert.AreEqual("Intelligence", KingdomCrewRules.StatNameFor(KingdomCrewRules.KindIntelligence));
			Assert.IsNull(KingdomCrewRules.StatNameFor("theirmod:quickness"));
		}

		[Test]
		public void KnownKindsListsAttributesThenSkillsInAssignWorksPriorityOrder()
		{
			CollectionAssert.AreEqual(new string[]
			{
				KingdomCrewRules.KindStrength, KingdomCrewRules.KindIntelligence,
				KingdomCrewRules.KindTinkering, KingdomCrewRules.KindHarvestry,
				KingdomCrewRules.KindCustoms, KingdomCrewRules.KindPhysic,
				KingdomCrewRules.KindWayfaring
			}, KingdomCrewRules.KnownKinds);
		}
	}
}
#endif
