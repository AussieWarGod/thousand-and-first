#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The research system's arithmetic, tabled. Every case here is a rule somebody could quietly
	/// break: the tier that must be shut rather than slow, the seed that must never finish a node,
	/// the Intelligence cap that must never stack, and the parse that must refuse a rite in
	/// <c>TaughtBy</c> by file and key rather than by convention.
	/// </summary>
	public class KingdomResearchRulesTests
	{
		// --- The tier ladder: hard at the boundary, soft inside it -----------------------------

		[TestCase(1, 10)]
		[TestCase(2, 14)]
		[TestCase(3, 18)]
		[TestCase(4, 22)]
		public void IntelligenceForTier_IsTheAuthoredLadder(int tier, int expected)
		{
			Assert.AreEqual(expected, KingdomResearchRules.IntelligenceForTier(tier));
		}

		[TestCase(0, 10)]
		[TestCase(-3, 10)]
		public void IntelligenceForTier_BelowTheLadder_IsTheBottomRung(int tier, int expected)
		{
			// An absent tier has always meant tier 1 here, and the parse refuses anything under 1
			// outright, so this is only ever reached by a node somebody built by hand.
			Assert.AreEqual(expected, KingdomResearchRules.IntelligenceForTier(tier));
		}

		[TestCase(5, 22)]
		[TestCase(99, 22)]
		public void IntelligenceForTier_AboveTheLadder_ReadsAsTheShutAnswer(int tier, int expected)
		{
			// A tier this build does not know must be harder to reach, never easier: an unknown
			// tier that read as tier 1 would be a free top of the tree for a typo.
			Assert.AreEqual(expected, KingdomResearchRules.IntelligenceForTier(tier));
		}

		[TestCase(17, 3, false)]
		[TestCase(18, 3, true)]
		[TestCase(30, 4, true)]
		[TestCase(21, 4, false)]
		public void TierReached_IsTheThresholdAndNothingElse(int mind, int tier, bool expected)
		{
			Assert.AreEqual(expected, KingdomResearchRules.TierReached(mind, tier));
		}

		[Test]
		public void TierBonus_BelowTheThreshold_IsZeroSoTheWholeRateIsZero()
		{
			// The whole point of "a tier you cannot reach is not slow, it is shut": the bonus is a
			// FACTOR, so zero here makes the product zero without a special case anywhere.
			Assert.AreEqual(0, KingdomResearchRules.TierBonus(17, 3));
			Assert.AreEqual(0, KingdomResearchRules.InquiryRate(100, 100, KingdomResearchRules.TierBonus(17, 3), 100));
		}

		[TestCase(18, 3, 100)]
		[TestCase(20, 3, 110)]
		[TestCase(28, 3, 150)]
		[TestCase(40, 3, 150)]
		public void TierBonus_AboveTheThreshold_BuysSpeedAndIsCapped(int mind, int tier, int expected)
		{
			Assert.AreEqual(expected, KingdomResearchRules.TierBonus(mind, tier));
		}

		// --- The rate: every factor can shut the bench, by arithmetic --------------------------

		[TestCase(0, 100, 100, 100)]
		[TestCase(100, 0, 100, 100)]
		[TestCase(100, 100, 0, 100)]
		[TestCase(100, 100, 100, 0)]
		public void InquiryRate_AnyFactorAtZero_ProducesNothing(int crew, int wear, int bonus, int lab)
		{
			Assert.AreEqual(0, KingdomResearchRules.InquiryRate(crew, wear, bonus, lab));
		}

		[Test]
		public void InquiryRate_FullyCrewedSoundScriptoriumAtTheThreshold_IsOneForOne()
		{
			Assert.AreEqual(100, KingdomResearchRules.InquiryRate(100, 100, 100,
				KingdomResearchRules.ScriptoriumPercent));
		}

		[Test]
		public void InquiryRate_HalfCrewedHalvesIt_AndABetterBenchMultipliesIt()
		{
			Assert.AreEqual(50, KingdomResearchRules.InquiryRate(50, 100, 100, KingdomResearchRules.ScriptoriumPercent));
			Assert.AreEqual(150, KingdomResearchRules.InquiryRate(100, 100, 100, KingdomResearchRules.LaboratoryPercent));
			Assert.AreEqual(200, KingdomResearchRules.InquiryRate(100, 100, 100, KingdomResearchRules.ArclightAnnexePercent));
		}

		[Test]
		public void Worked_IsNothingForNoElapsedTimeAndNothingForNoRate()
		{
			Assert.AreEqual(0, KingdomResearchRules.Worked(0L, 100));
			Assert.AreEqual(0, KingdomResearchRules.Worked(-5L, 100));
			Assert.AreEqual(0, KingdomResearchRules.Worked(1200L, 0));
		}

		[Test]
		public void Worked_IsDeterministicAndDrawsNothing()
		{
			// The accrual lane must never draw. Same inputs, same answer, every time and in any
			// order -- the property a lane with PlannerMaxDraws = 0 has to be able to prove.
			int first = KingdomResearchRules.Worked(9999L, 73);
			for (int i = 0; i < 50; i++)
			{
				Assert.AreEqual(first, KingdomResearchRules.Worked(9999L, 73));
			}
		}

		[Test]
		public void EffortTicks_IsStaffDaysAtTheSettlementsOwnDay()
		{
			Assert.AreEqual((int)KingdomRules.TicksPerDay * 14, KingdomResearchRules.EffortTicks(14));
			Assert.AreEqual((int)KingdomRules.TicksPerDay, KingdomResearchRules.EffortTicks(0));
			Assert.AreEqual((int)KingdomRules.TicksPerDay, KingdomResearchRules.EffortTicks(-3));
		}

		// --- Seeds: a door, never a room -------------------------------------------------------

		[Test]
		public void Seeded_FromNothing_IsAQuarterOfTheWalk()
		{
			int effort = KingdomResearchRules.EffortTicks(20);
			Assert.AreEqual(effort * KingdomResearchRules.SeedPercent / 100, KingdomResearchRules.Seeded(20, 0));
		}

		[Test]
		public void Seeded_NeverPassesHalfHoweverManySeedsLand()
		{
			int effort = KingdomResearchRules.EffortTicks(20);
			int ceiling = effort * KingdomResearchRules.MaxSeedPercent / 100;
			int standing = 0;
			for (int i = 0; i < 10; i++)
			{
				standing = KingdomResearchRules.Seeded(20, standing);
			}
			Assert.AreEqual(ceiling, standing);
			Assert.Less(standing, effort, "a seed must never be able to finish a node");
		}

		[Test]
		public void Seeded_NeverLowersLabourAlreadyDone()
		{
			int effort = KingdomResearchRules.EffortTicks(20);
			// A city three quarters of the way through gets nothing from a rite, and loses nothing.
			int standing = effort * 3 / 4;
			Assert.AreEqual(standing, KingdomResearchRules.Seeded(20, standing));
		}

		// --- The shelf: memory, deterministic, and it says what it forgot ----------------------

		[Test]
		public void Crowded_WithRoomToSpare_ForgetsNothing()
		{
			Dictionary<string, int> shelf = new Dictionary<string, int>();
			for (int i = 0; i < KingdomResearchRules.ShelfRows - 1; i++)
			{
				shelf["node" + i] = i * 10;
			}
			Assert.IsNull(KingdomResearchRules.Crowded(shelf));
			Assert.IsNull(KingdomResearchRules.Crowded(null));
		}

		[Test]
		public void Crowded_DropsTheLeastAdvancedRow()
		{
			Dictionary<string, int> shelf = new Dictionary<string, int>();
			for (int i = 0; i < KingdomResearchRules.ShelfRows; i++)
			{
				shelf["node" + i] = 500 - i;
			}
			Assert.AreEqual("node" + (KingdomResearchRules.ShelfRows - 1), KingdomResearchRules.Crowded(shelf));
		}

		[Test]
		public void Crowded_BreaksTiesOnKeyAscending_SoAReloadNeverForgetsSomethingElse()
		{
			Dictionary<string, int> shelf = new Dictionary<string, int>();
			for (int i = 0; i < KingdomResearchRules.ShelfRows; i++)
			{
				shelf["node" + i] = 100;
			}
			Assert.AreEqual("node0", KingdomResearchRules.Crowded(shelf));
		}

		// --- The method lane -------------------------------------------------------------------

		[TestCase(0, 100)]
		[TestCase(-5, 100)]
		[TestCase(20, 120)]
		[TestCase(50, 150)]
		[TestCase(500, 150)]
		public void MethodPercent_IsNeverATaxAndIsCappedOnTheLane(int sum, int expected)
		{
			Assert.AreEqual(expected, KingdomResearchRules.MethodPercent(sum));
		}

		[Test]
		public void Efficiency_SumsOnlyEfficiencyGrants()
		{
			List<ResearchEffect> held = new List<ResearchEffect>
			{
				new ResearchEffect(KingdomResearchRules.EffectEfficiency, null, 5),
				new ResearchEffect(KingdomResearchRules.EffectStatCap, "intelligence", 1),
				new ResearchEffect(KingdomResearchRules.EffectEfficiency, null, 10),
				new ResearchEffect(KingdomResearchRules.EffectRecruitReveal, null, 1)
			};
			Assert.AreEqual(15, KingdomResearchRules.Efficiency(held));
		}

		// --- The citizen ceiling: ours, and Intelligence never stacks (Addendum 22 E2) ---------

		[Test]
		public void Headroom_Intelligence_NeverStacksHoweverManyNodesGrantIt()
		{
			// The clause that stops research raising the ceiling on the stat that gates research.
			// Enforced here rather than in the authoring, so a SECOND node granting Intelligence --
			// ours or a third party's -- cannot open the loop by addition.
			List<ResearchEffect> held = new List<ResearchEffect>
			{
				new ResearchEffect(KingdomResearchRules.EffectStatCap, "intelligence", 1),
				new ResearchEffect(KingdomResearchRules.EffectStatCap, "intelligence", 1),
				new ResearchEffect(KingdomResearchRules.EffectStatCap, KingdomResearchRules.StatAny, 1)
			};
			Assert.AreEqual(KingdomResearchRules.MaxHeadroomIntelligence,
				KingdomResearchRules.Headroom(held, "Intelligence"));
			Assert.AreEqual(1, KingdomResearchRules.MaxHeadroomIntelligence);
		}

		[Test]
		public void Headroom_OtherStats_StackToTheirOwnCapAndNoFurther()
		{
			List<ResearchEffect> held = new List<ResearchEffect>
			{
				new ResearchEffect(KingdomResearchRules.EffectStatCap, "strength", 2),
				new ResearchEffect(KingdomResearchRules.EffectStatCap, "strength", 5)
			};
			Assert.AreEqual(KingdomResearchRules.MaxHeadroomPerStat, KingdomResearchRules.Headroom(held, "Strength"));
		}

		[Test]
		public void Headroom_AnyCountsTowardEveryStat()
		{
			List<ResearchEffect> held = new List<ResearchEffect>
			{
				new ResearchEffect(KingdomResearchRules.EffectStatCap, KingdomResearchRules.StatAny, 1)
			};
			Assert.AreEqual(1, KingdomResearchRules.Headroom(held, "Strength"));
			Assert.AreEqual(1, KingdomResearchRules.Headroom(held, "Toughness"));
			Assert.AreEqual(1, KingdomResearchRules.Headroom(held, "Intelligence"));
		}

		[Test]
		public void Headroom_IsNothingWhenNothingIsHeld()
		{
			Assert.AreEqual(0, KingdomResearchRules.Headroom(null, "Strength"));
			Assert.AreEqual(0, KingdomResearchRules.Headroom(new List<ResearchEffect>(), "Strength"));
			Assert.AreEqual(0, KingdomResearchRules.Headroom(new List<ResearchEffect>(), null));
		}

		[Test]
		public void Ceiling_IsWhatTheyWalkedInWithPlusWhatTheCityTeaches()
		{
			Assert.AreEqual(17, KingdomResearchRules.Ceiling(16, 1));
			Assert.AreEqual(16, KingdomResearchRules.Ceiling(16, 0));
			Assert.AreEqual(16, KingdomResearchRules.Ceiling(16, -4));
		}

		[Test]
		public void TrainedValue_StopsAtTheCeilingAndNeverTakesAPointAway()
		{
			Assert.AreEqual(17, KingdomResearchRules.TrainedValue(16, 16, 1));
			Assert.AreEqual(17, KingdomResearchRules.TrainedValue(17, 16, 1));
			// A citizen who walked in ABOVE what the city could teach keeps everything they brought.
			Assert.AreEqual(25, KingdomResearchRules.TrainedValue(25, 16, 1));
		}

		[Test]
		public void CanTrain_IsFalseOnceTheCeilingIsReached()
		{
			Assert.IsTrue(KingdomResearchRules.CanTrain(16, 16, 1));
			Assert.IsFalse(KingdomResearchRules.CanTrain(17, 16, 1));
			Assert.IsFalse(KingdomResearchRules.CanTrain(16, 16, 0));
		}

		// --- Distance and prose: no percentage, no bar, no number for the WORK -----------------

		[TestCase(false, false, 0, 0)]
		[TestCase(true, false, 0, 1)]
		[TestCase(true, true, 2, 4)]
		public void Distance_CountsTheThingsInTheWay(bool tierShort, bool techShort, int missing, int expected)
		{
			Assert.AreEqual(expected, KingdomResearchRules.Distance(tierShort, techShort, missing));
		}

		[Test]
		public void Reach_BegunSitsBetweenWithinReachAndOneThingAway()
		{
			Assert.AreEqual("{{G|within reach}}", KingdomResearchRules.Reach(0, Begun: false));
			Assert.AreEqual("{{W|begun}}", KingdomResearchRules.Reach(0, Begun: true));
			Assert.AreEqual("{{W|one thing away}}", KingdomResearchRules.Reach(1, Begun: true));
			StringAssert.Contains("3", KingdomResearchRules.Reach(3, Begun: false));
		}

		[Test]
		public void EveryReachStringIsProseAndCarriesNoPercentage()
		{
			for (int distance = 0; distance <= 6; distance++)
			{
				StringAssert.DoesNotContain("%", KingdomResearchRules.Reach(distance, Begun: false));
				StringAssert.DoesNotContain("%", KingdomResearchRules.Reach(distance, Begun: true));
			}
		}

		// --- The parse -------------------------------------------------------------------------

		[Test]
		public void TryParseNodeAttributes_MinimalNode_GrantsItsOwnKey()
		{
			ResearchNode node;
			string error;
			Assert.IsTrue(KingdomResearchRules.TryParseNodeAttributes("kilnheat", "kiln heat", "foundry", "2",
				null, null, null, "10", null, null, null, null, null, null, out node, out error));
			Assert.IsNull(error);
			Assert.AreEqual("kilnheat", node.Key);
			Assert.AreEqual("kiln heat", node.Named);
			Assert.AreEqual(2, node.Tier);
			Assert.AreEqual(10, node.Effort);
			Assert.AreEqual("node:kilnheat", node.Grants);
			Assert.AreEqual(TechLevel.Hands, node.MinTech);
		}

		[Test]
		public void TryParseNodeAttributes_ARiteInTaughtBy_IsRefusedByFileAndKey()
		{
			// Addendum 18, enforced rather than conventional: a rite SEEDS a branch and can never
			// finish a node, so a rite in TaughtBy is a schema error and not a style preference.
			ResearchNode node;
			string error;
			Assert.IsFalse(KingdomResearchRules.TryParseNodeAttributes("arclight", null, "foundry", "4",
				null, null, null, "30", null, "rite:Barathrumites", null, null, null, null, out node, out error));
			Assert.IsNull(node);
			StringAssert.Contains("arclight", error);
			// Folded, like every roster token the gate machinery reads, so the refusal names the
			// same string the roster would have carried.
			StringAssert.Contains("rite:barathrumites", error);
			StringAssert.Contains("SeededBy", error);
		}

		[Test]
		public void TryParseNodeAttributes_ARiteInSeededBy_IsFine()
		{
			ResearchNode node;
			string error;
			Assert.IsTrue(KingdomResearchRules.TryParseNodeAttributes("arclight", null, "foundry", "4",
				null, null, null, "30", null, null, "rite:Barathrumites", null, null, null, out node, out error));
			Assert.AreEqual("rite:Barathrumites", node.SeededBy);
		}

		[TestCase("0")]
		[TestCase("5")]
		[TestCase("banana")]
		public void TryParseNodeAttributes_ATierOutsideTheLadder_IsRefused(string tier)
		{
			ResearchNode node;
			string error;
			Assert.IsFalse(KingdomResearchRules.TryParseNodeAttributes("k", null, null, tier,
				null, null, null, "4", null, null, null, null, null, null, out node, out error));
			StringAssert.Contains("Tier", error);
		}

		[TestCase("0")]
		[TestCase("-2")]
		[TestCase("soon")]
		public void TryParseNodeAttributes_AnUnreadableEffort_IsRefused(string effort)
		{
			ResearchNode node;
			string error;
			Assert.IsFalse(KingdomResearchRules.TryParseNodeAttributes("k", null, null, "1",
				null, null, null, effort, null, null, null, null, null, null, out node, out error));
			StringAssert.Contains("Effort", error);
		}

		[Test]
		public void TryParseNodeAttributes_AKeyThatCouldNotSurviveTheRoster_IsRefused()
		{
			ResearchNode node;
			string error;
			Assert.IsFalse(KingdomResearchRules.TryParseNodeAttributes("node:kiln", null, null, "1",
				null, null, null, "4", null, null, null, null, null, null, out node, out error));
			Assert.IsFalse(KingdomResearchRules.TryParseNodeAttributes("kiln|heat", null, null, "1",
				null, null, null, "4", null, null, null, null, null, null, out node, out error));
		}

		[Test]
		public void TryParseNodeAttributes_AnUnknownMinTech_IsRefusedRatherThanGatingForever()
		{
			ResearchNode node;
			string error;
			Assert.IsFalse(KingdomResearchRules.TryParseNodeAttributes("k", null, null, "1",
				null, "99", null, "4", null, null, null, null, null, null, out node, out error));
			StringAssert.Contains("MinTech", error);
		}

		[Test]
		public void TryParseEffects_ReadsAllThreeShapes()
		{
			List<ResearchEffect> effects;
			string error;
			Assert.IsTrue(KingdomResearchRules.TryParseEffects("efficiency:10,statcap:Intelligence:1,recruitreveal:1",
				out effects, out error));
			Assert.IsNull(error);
			Assert.AreEqual(3, effects.Count);
			Assert.AreEqual(KingdomResearchRules.EffectEfficiency, effects[0].Kind);
			Assert.AreEqual(10, effects[0].Amount);
			Assert.AreEqual("intelligence", effects[1].Stat);
			Assert.AreEqual(1, effects[1].Amount);
			Assert.AreEqual(KingdomResearchRules.EffectRecruitReveal, effects[2].Kind);
		}

		[Test]
		public void TryParseEffects_AnEmptyAttributeIsNotAFault()
		{
			List<ResearchEffect> effects;
			string error;
			Assert.IsTrue(KingdomResearchRules.TryParseEffects(null, out effects, out error));
			Assert.AreEqual(0, effects.Count);
			Assert.IsTrue(KingdomResearchRules.TryParseEffects("   ", out effects, out error));
			Assert.AreEqual(0, effects.Count);
		}

		[TestCase("efficiency")]
		[TestCase("efficiency:soon")]
		[TestCase("statcap:1")]
		[TestCase("statcap:Intelligence:one")]
		[TestCase("a:b:c:d")]
		public void TryParseEffects_AnUnreadableAmountIsRefusedWhole(string source)
		{
			List<ResearchEffect> effects;
			string error;
			Assert.IsFalse(KingdomResearchRules.TryParseEffects(source, out effects, out error));
			Assert.AreEqual(0, effects.Count, "a refused Effect must leave nothing half-read behind");
			Assert.IsNotNull(error);
		}

		[Test]
		public void TryParseEffects_AKindThisBuildDoesNotKnowIsCarriedRatherThanRefused()
		{
			// STANDARDS 9: an unrecognised vocabulary is somebody else's, logged and not refused.
			List<ResearchEffect> effects;
			string error;
			Assert.IsTrue(KingdomResearchRules.TryParseEffects("theirmod_glow:3", out effects, out error));
			Assert.AreEqual(1, effects.Count);
			Assert.AreEqual("theirmod_glow", effects[0].Kind);
			Assert.AreEqual(3, effects[0].Amount);
		}

		// --- The visibility law -----------------------------------------------------------------

		[Test]
		public void AnyRoadVisible_ANodeTheFounderHasNeverHeardOf_HidesTheDesignOutright()
		{
			// The law's whole bite: no greyed row, no silhouette, no count of the unseen. A design
			// waiting on a node nobody has heard of is ABSENT.
			Assert.IsFalse(KingdomResearchRules.AnyRoadVisible("node:cruciblesteel", new List<string>()));
			Assert.IsFalse(KingdomResearchRules.AnyRoadVisible("node:cruciblesteel", new List<string> { "notes" }));
		}

		[Test]
		public void AnyRoadVisible_ANodeTheFounderHasHeardOf_IsARoad()
		{
			Assert.IsTrue(KingdomResearchRules.AnyRoadVisible("node:cruciblesteel", new List<string> { "cruciblesteel" }));
		}

		[Test]
		public void AnyRoadVisible_ANonNodeArmIsAlwaysARoadTheFounderCanSee()
		{
			// A disk to carry home, a machine to certify, people to take in: every one of those is
			// a thing the founder could go and do, so the design stays on the list wearing its tag.
			Assert.IsTrue(KingdomResearchRules.AnyRoadVisible("machine:Solar Still", new List<string>()));
			Assert.IsTrue(KingdomResearchRules.AnyRoadVisible("origin:the salt marshes", new List<string>()));
			Assert.IsTrue(KingdomResearchRules.AnyRoadVisible("pattern:something", new List<string>()));
			Assert.IsTrue(KingdomResearchRules.AnyRoadVisible("theirmod:thing", new List<string>()));
		}

		[Test]
		public void AnyRoadVisible_OneSeenArmIsEnoughToKeepTheWholeTokenVisible()
		{
			Assert.IsTrue(KingdomResearchRules.AnyRoadVisible("node:vat|node:graft", new List<string> { "graft" }));
			Assert.IsFalse(KingdomResearchRules.AnyRoadVisible("node:vat|node:graft", new List<string> { "kiln" }));
			// A mixed token always has a visible road, because the non-node arm is one.
			Assert.IsTrue(KingdomResearchRules.AnyRoadVisible("node:vat|machine:Solar Still", new List<string>()));
		}

		[Test]
		public void AnyRoadVisible_AnUngatedDesignIsAlwaysVisible()
		{
			Assert.IsTrue(KingdomResearchRules.AnyRoadVisible(null, new List<string>()));
			Assert.IsTrue(KingdomResearchRules.AnyRoadVisible("", new List<string>()));
			Assert.IsTrue(KingdomResearchRules.AnyRoadVisible("machine:x", null));
		}

		// --- Registry validation ---------------------------------------------------------------

		[Test]
		public void Validate_NamesARevealIntoNothing()
		{
			List<ResearchNode> nodes = new List<ResearchNode>
			{
				Node("a", "node:a", null, "node:nowhere")
			};
			List<string> findings = KingdomResearchRules.Validate(nodes);
			Assert.AreEqual(1, findings.Count);
			StringAssert.Contains("node:nowhere", findings[0]);
		}

		[Test]
		public void Validate_NamesARequirementNoNodeGrants()
		{
			List<ResearchNode> nodes = new List<ResearchNode>
			{
				Node("b", "node:b", "node:missing", null)
			};
			List<string> findings = KingdomResearchRules.Validate(nodes);
			Assert.AreEqual(1, findings.Count);
			StringAssert.Contains("node:missing", findings[0]);
		}

		[Test]
		public void Validate_AWellFormedTreeReportsNothing()
		{
			List<ResearchNode> nodes = new List<ResearchNode>
			{
				Node("notekeeping", "node:notes", null, "node:kilnheat"),
				Node("kilnheat", "node:kiln", "node:notes", null)
			};
			Assert.AreEqual(0, KingdomResearchRules.Validate(nodes).Count);
		}

		[Test]
		public void Validate_ANonNodeRequirementIsNotItsBusiness()
		{
			// A disk, a machine, a creed or a modder's own kind is answered by the roster, not by
			// this tree, so requiring one is never a dangling reference.
			List<ResearchNode> nodes = new List<ResearchNode>
			{
				Node("a", "node:a", "machine:Solar Still,creed:Barathrumites,theirmod:thing", null)
			};
			Assert.AreEqual(0, KingdomResearchRules.Validate(nodes).Count);
		}

		[Test]
		public void Validate_TolerantOfNothingAtAll()
		{
			Assert.AreEqual(0, KingdomResearchRules.Validate(null).Count);
			Assert.AreEqual(0, KingdomResearchRules.Validate(new List<ResearchNode>()).Count);
		}

		// --- The words -------------------------------------------------------------------------

		[Test]
		public void StallLine_NamesExactlyOneLackAndTheFirstOneThatIsActuallyZero()
		{
			string empty = KingdomResearchRules.StallLine("scriptorium", "kiln heat", 0, 100, 20, 14);
			StringAssert.Contains("empty", empty);
			string broken = KingdomResearchRules.StallLine("scriptorium", "kiln heat", 100, 0, 20, 14);
			StringAssert.Contains("mending", broken);
			string dim = KingdomResearchRules.StallLine("scriptorium", "kiln heat", 100, 100, 11, 14);
			StringAssert.Contains("14", dim);
			StringAssert.Contains("11", dim);
		}

		[Test]
		public void TierRefusal_NamesTheMindWantedAndTheMindHeld()
		{
			string refusal = KingdomResearchRules.TierRefusal("Kavvat", "crucible steel", 15, 18);
			StringAssert.Contains("Kavvat", refusal);
			StringAssert.Contains("18", refusal);
			StringAssert.Contains("15", refusal);
			// The refusal has to name the act that lifts it, not only the lack (STANDARDS 7b).
			StringAssert.Contains("Take in", refusal);
		}

		[Test]
		public void NothingWrittenDown_IsOneSentenceNamingTheCity()
		{
			string line = KingdomResearchRules.NothingWrittenDown("Kavvat");
			StringAssert.Contains("Kavvat", line);
			StringAssert.DoesNotContain("%", line);
		}

		// --- The map's research chapters --------------------------------------------------------

		[Test]
		public void HeardOfChapter_CountsOnlyTheRowsItWasGiven()
		{
			// Verdict 7, in one assertion. The old chapter tailed with a count over the WHOLE locked
			// set, which let the founder count what they could not see; the tail must now be over
			// the discovered rows and nothing else.
			List<ResearchRow> rows = new List<ResearchRow>();
			for (int i = 0; i < KingdomTechMapRules.MaxHeardOf + 3; i++)
			{
				rows.Add(new ResearchRow("k" + i, "node " + i, 1, false, "something."));
			}
			string chapter = KingdomTechMapRules.HeardOfChapter(rows);
			StringAssert.Contains("And 3 further off.", chapter);
		}

		[Test]
		public void HeardOfChapter_WithNothingHeardOf_SaysSoAndCountsNothing()
		{
			string chapter = KingdomTechMapRules.HeardOfChapter(new List<ResearchRow>());
			StringAssert.Contains("have not heard of", chapter);
			StringAssert.DoesNotContain("further off", chapter);
			StringAssert.DoesNotContain("0", chapter);
		}

		[Test]
		public void SortResearch_NearestFirstThenBegunThenName()
		{
			List<ResearchRow> rows = new List<ResearchRow>
			{
				new ResearchRow("c", "chimerism", 2, false, ""),
				new ResearchRow("a", "assent", 0, false, ""),
				new ResearchRow("b", "butchery", 0, true, "")
			};
			KingdomTechMapRules.SortResearch(rows);
			Assert.AreEqual("butchery", rows[0].Name, "a subject already begun is nearer than one untouched");
			Assert.AreEqual("assent", rows[1].Name);
			Assert.AreEqual("chimerism", rows[2].Name);
		}

		[Test]
		public void MissingForNode_NamesTheMindAndTheCraftAndNothingItWasNotGiven()
		{
			string missing = KingdomTechMapRules.MissingForNode(new List<string> { "kiln" }, 18, 15, "foundry", "salvage");
			StringAssert.Contains("kiln", missing);
			StringAssert.Contains("18", missing);
			StringAssert.Contains("15", missing);
			StringAssert.Contains("foundry", missing);
			Assert.AreEqual("", KingdomTechMapRules.MissingForNode(new List<string>(), 0, 20, null, "salvage"));
		}

		[Test]
		public void WorkingChapter_NamesTheOneSubjectAndTheShelfWithNoNumbers()
		{
			string chapter = KingdomTechMapRules.WorkingChapter("kiln heat",
				KingdomResearchRules.Reach(0, Begun: true), new List<string> { "the vat", "physic" });
			StringAssert.Contains("kiln heat", chapter);
			StringAssert.Contains("begun", chapter);
			StringAssert.Contains("the vat", chapter);
			StringAssert.DoesNotContain("%", chapter);
		}

		[Test]
		public void RoadsNotTaken_NamesTheRiteRoadWithoutNamingOneThingBehindIt()
		{
			string roads = KingdomTechMapRules.RoadsNotTaken(AnyDisk: true, AnyMachine: true, AnyOrigin: true, AnyRite: false);
			StringAssert.Contains("Share water", roads);
			// The escape valve names a KIND of learning and never a node: the founder can tell
			// there is more world and cannot tell what is in it.
			StringAssert.DoesNotContain("node:", roads);
			Assert.AreEqual("", KingdomTechMapRules.RoadsNotTaken(true, true, true, true));
		}

		[Test]
		public void RoadsNotTaken_TheOlderThreeArgumentFormIsUnchanged()
		{
			// Published shape: a caller that predates the rite road must read exactly what it read.
			Assert.AreEqual("", KingdomTechMapRules.RoadsNotTaken(true, true, true));
			StringAssert.DoesNotContain("Share water", KingdomTechMapRules.RoadsNotTaken(false, true, true));
		}

		private static ResearchNode Node(string key, string grants, string requires, string reveals)
		{
			return new ResearchNode { Key = key, Grants = grants, Requires = requires, Reveals = reveals };
		}
	}
}
#endif
