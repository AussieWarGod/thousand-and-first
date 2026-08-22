#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomLabRulesTests
	{
		private static LabProcedure Procedure(string key, string cls = "II", string grants = "GasImmunity",
			string creeds = null, int cost = 30, int staffDays = 8, int preserved = 1, string bits = "002")
		{
			LabProcedure procedure;
			string error;
			Assert.IsTrue(KingdomProcedureRules.TryParseProcedureAttributes(key, null, cls, grants, "Body",
				null, "part", "body", null, cost.ToString(), bits, staffDays.ToString(), preserved.ToString(),
				creeds, null, null, out procedure, out error), error);
			return procedure;
		}

		// --- The rung ladder, from what is actually standing --------------------------------------

		[TestCase(false, false, false, false, -1)]
		[TestCase(true, false, false, false, 0)]
		[TestCase(true, true, false, false, 1)]
		[TestCase(true, true, true, false, 2)]
		[TestCase(true, true, true, true, 3)]
		public void RungReached_ClimbsOneStepAtATime(bool slab, bool vat, bool hall, bool theatre, int expected)
		{
			Assert.AreEqual(expected, KingdomLabRules.RungReached(slab, vat, hall, theatre));
		}

		[Test]
		public void RungReached_IsTheHighestUNBROKENStepAndNotTheHighestBuiltOne()
		{
			// A theatre with no vats under it can graft nothing, because the theatre's own inputs
			// come out of the vats. A founder who raised the grand thing first gets told so.
			Assert.AreEqual(0, KingdomLabRules.RungReached(Slab: true, Vat: false, Hall: true, Theatre: true));
			Assert.AreEqual(-1, KingdomLabRules.RungReached(Slab: false, Vat: true, Hall: true, Theatre: true));
		}

		[Test]
		public void LadderGapLine_SaysWhyTheGrandThingIsDoingNothing()
		{
			// STANDARDS 7b: the single most expensive silent stall this ladder could have is a
			// finished hall standing over a gap, because nothing else in the game would say why.
			StringAssert.Contains("butcher's slab",
				KingdomLabRules.LadderGapLine(Slab: false, Vat: false, Hall: true, Theatre: false));
			StringAssert.Contains("vat-house",
				KingdomLabRules.LadderGapLine(Slab: true, Vat: false, Hall: true, Theatre: false));
			StringAssert.Contains("grafting hall",
				KingdomLabRules.LadderGapLine(Slab: true, Vat: true, Hall: false, Theatre: true));
		}

		[Test]
		public void LadderGapLine_SaysNothingAboutALadderThatIsFine()
		{
			// 7b forbids telling somebody about the absence of a problem.
			Assert.IsNull(KingdomLabRules.LadderGapLine(true, true, true, true));
			Assert.IsNull(KingdomLabRules.LadderGapLine(true, true, true, false));
			Assert.IsNull(KingdomLabRules.LadderGapLine(true, true, false, false));
			Assert.IsNull(KingdomLabRules.LadderGapLine(false, false, false, false));
		}

		// --- Megastructure cardinality (Addendum 22 A1) ---------------------------------------------

		[TestCase("yes", true)]
		[TestCase("Yes", true)]
		[TestCase("YES", true)]
		[TestCase("true", true)]
		[TestCase("1", true)]
		[TestCase("no", false)]
		[TestCase("", false)]
		[TestCase(null, false)]
		[TestCase("maybe", false)]
		public void IsMegastructure_ADesignIsOrdinaryUntilItSaysOtherwise(string declared, bool expected)
		{
			Assert.AreEqual(expected, KingdomLabRules.IsMegastructure(declared));
		}

		[Test]
		public void JudgePurpose_RefusesASecondMegastructureInACityThatAlreadyHasOne()
		{
			Assert.AreEqual(KingdomPurposeVerdict.RefusedKept,
				KingdomLabRules.JudgePurpose(Megastructure: true, Kept: "arcology", Key: "chimerictheatre"));
		}

		[Test]
		public void JudgePurpose_AllowsTheFirstOne()
		{
			Assert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(Megastructure: true, Kept: null, Key: "chimerictheatre"));
			Assert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(Megastructure: true, Kept: "", Key: "chimerictheatre"));
		}

		[Test]
		public void JudgePurpose_ReKeyingTheSameOneIsNotChoosingAgain()
		{
			// Mending, re-siting or re-staking the megastructure a city already has is not a second
			// purpose, and refusing it would make a purpose unrepairable.
			Assert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(true, "chimerictheatre", "chimerictheatre"));
			Assert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(true, "ChimericTheatre", "chimerictheatre"));
		}

		[Test]
		public void JudgePurpose_NeverStandsInTheWayOfAnOrdinaryDesign()
		{
			// The gate is one check on one attribute and it must be inert for every building in the
			// catalogue that is not a megastructure — which is all of them but one.
			Assert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(Megastructure: false, Kept: "arcology", Key: "smithy"));
		}

		[Test]
		public void PurposeRefusalLine_NamesTheBuildingInTheWayRatherThanTheRule()
		{
			// A founder told "one megastructure per city" has learned a rule; a founder told which
			// building is standing between them and this one has learned what to do about it.
			string line = KingdomLabRules.PurposeRefusalLine("the arcology of Kavvat");
			StringAssert.Contains("this city already has its purpose", line.ToLowerInvariant());
			StringAssert.Contains("the arcology of Kavvat", line);
		}

		[Test]
		public void PurposeLine_ReadsBothWays()
		{
			StringAssert.Contains("nothing in particular", KingdomLabRules.PurposeLine(null));
			StringAssert.Contains("the arcology", KingdomLabRules.PurposeLine("the arcology"));
		}

		// --- The cardinality gate, end to end through the real zoning path ---------------------------

		private static ZoneGate Gate(string megastructure)
		{
			string error;
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("chimerictheatre", null, null, null, null,
				null, null, null, null, megastructure, out error);
			Assert.IsNull(error);
			return gate;
		}

		private static ZoningJudgement JudgeTheatre(string megastructure, string key, string cityKeeps)
		{
			return KingdomZoningRules.Judge(Gate(megastructure), null, "craft", 0, null,
				Underground: false, RequiresSky: false, Roll: BuilderRoll.Unknown,
				Stratum: null, Key: key, CityKeeps: cityKeeps);
		}

		[Test]
		public void Zoning_RefusesASecondMegastructureWhenTheBookSaysOneIsKept()
		{
			ZoningJudgement judgement = JudgeTheatre("yes", "chimerictheatre", "arcology");
			Assert.IsFalse(judgement.Permitted);
			Assert.AreEqual(ZoningVerdict.RefusedMegastructure, judgement.Verdict);
			// The Detail carries the KEY, because the refusal is composed one lane over where the
			// catalogue can be asked what a key is called.
			Assert.AreEqual("arcology", judgement.Detail);
			Assert.IsNotEmpty(judgement.Note);
		}

		[Test]
		public void Zoning_AllowsTheFirstMegastructure()
		{
			Assert.IsTrue(JudgeTheatre("yes", "chimerictheatre", null).Permitted);
			Assert.IsTrue(JudgeTheatre("yes", "chimerictheatre", "").Permitted);
		}

		[Test]
		public void Zoning_AllowsReKeyingTheOneTheCityAlreadyKeeps()
		{
			// Mending, re-siting or re-staking a city's own purpose is not choosing a second one,
			// and refusing it would make a purpose unrepairable.
			Assert.IsTrue(JudgeTheatre("yes", "chimerictheatre", "chimerictheatre").Permitted);
			Assert.IsTrue(JudgeTheatre("yes", "chimerictheatre", "ChimericTheatre").Permitted);
		}

		[Test]
		public void Zoning_NeverStandsInTheWayOfAnOrdinaryDesign()
		{
			// The gate must be inert for every design in the catalogue but one — which is what makes
			// one attribute and one check the whole of the vocabulary.
			Assert.IsTrue(JudgeTheatre(null, "smithy", "arcology").Permitted);
			Assert.IsTrue(JudgeTheatre("no", "smithy", "arcology").Permitted);
		}

		[Test]
		public void Zoning_FailsOPENWhenNothingCouldTellWhatTheCityKeeps()
		{
			// KingdomZoning.KeptMegastructure hands back null when it cannot read the city, and a
			// cardinality rule that could not see the city must let the founder build. The
			// alternative is a realm bricked by a book it could not open.
			Assert.IsTrue(JudgeTheatre("yes", "chimerictheatre", null).Permitted);
		}

		[Test]
		public void Zoning_TheOlderJudgeOverloadsStillPermitAMegastructure()
		{
			// Every caller written before this landed passes no CityKeeps, and must go on behaving
			// exactly as it did — the same back-compatibility promise Strata made one gate over.
			Assert.IsTrue(KingdomZoningRules.Judge(Gate("yes"), null, "craft", 0, null).Permitted);
			Assert.IsTrue(KingdomZoningRules.Judge(Gate("yes"), null, "craft", 0, null,
				Underground: false, RequiresSky: false).Permitted);
		}

		[Test]
		public void Zoning_TheCardinalityGateIsAskedLASTSoAReachableLackIsNamedFirst()
		{
			// A founder who has not reached arclight must hear about arclight, not about a purpose
			// they cannot get near. Every gate above this one is a lack they can answer.
			string error;
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("chimerictheatre", null, "4", null, "Arclight",
				null, null, null, null, "yes", out error);
			Assert.IsNull(error);
			ZoningJudgement judgement = KingdomZoningRules.Judge(gate, null, "craft", 0, null,
				Underground: false, RequiresSky: false, Roll: BuilderRoll.Unknown,
				Stratum: null, Key: "chimerictheatre", CityKeeps: "arcology");
			Assert.AreEqual(ZoningVerdict.RefusedTechLevel, judgement.Verdict);
		}

		[TestCase("yes", true)]
		[TestCase("YES", true)]
		[TestCase("no", false)]
		[TestCase(null, false)]
		[TestCase("nonsense", false)]
		public void ParseGate_ReadsTheMegastructureFlagAndNeverFaultsOnIt(string declared, bool expected)
		{
			// No fault branch, deliberately: a typo can make a design un-special, never unbuildable,
			// which is the safe direction for the one attribute that takes a city's purpose away.
			string error;
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("k", null, null, null, null,
				null, null, null, null, declared, out error);
			Assert.IsNull(error);
			Assert.AreEqual(expected, gate.Megastructure);
		}

		[Test]
		public void ParseGate_AMegastructureIsNotAnOpenGate()
		{
			Assert.IsFalse(Gate("yes").IsOpen);
			Assert.IsTrue(Gate(null).IsOpen);
		}

		[Test]
		public void ZoningVerdict_TheOrdinalsBelowTheNewOneAreUnmoved()
		{
			// These are published and are switched on by third parties (STANDARDS §9). Appending is
			// additive; renumbering is a break, and this table is what would catch one.
			Assert.AreEqual(0, (int)ZoningVerdict.Permitted);
			Assert.AreEqual(1, (int)ZoningVerdict.RefusedUnlearned);
			Assert.AreEqual(2, (int)ZoningVerdict.RefusedTechLevel);
			Assert.AreEqual(3, (int)ZoningVerdict.RefusedTerritory);
			Assert.AreEqual(4, (int)ZoningVerdict.RefusedStratum);
			Assert.AreEqual(5, (int)ZoningVerdict.RefusedDistrict);
			Assert.AreEqual(6, (int)ZoningVerdict.RefusedUnaligned);
			Assert.AreEqual(7, (int)ZoningVerdict.RefusedCreedShare);
			Assert.AreEqual(8, (int)ZoningVerdict.RefusedBuilders);
			Assert.AreEqual(9, (int)ZoningVerdict.RefusedMegastructure);
		}

		// --- The petition the hall provokes (§3.6's first authored happening) -------------------------

		[Test]
		public void PetitionKind_TheOrdinalsBelowTheNewOneAreUnmoved()
		{
			// Carried in a save. Appending is additive; renumbering silently reinterprets every
			// petition standing in every existing game.
			Assert.AreEqual(0, (int)KingdomRules.PetitionKind.None);
			Assert.AreEqual(1, (int)KingdomRules.PetitionKind.Thirst);
			Assert.AreEqual(2, (int)KingdomRules.PetitionKind.Shelter);
			Assert.AreEqual(3, (int)KingdomRules.PetitionKind.Craft);
			Assert.AreEqual(4, (int)KingdomRules.PetitionKind.Peace);
			Assert.AreEqual(5, (int)KingdomRules.PetitionKind.Memorial);
			Assert.AreEqual(6, (int)KingdomRules.PetitionKind.Flesh);
		}

		[Test]
		public void FleshPetition_IsNeverChosenByTheSettlementsOwnState()
		{
			// The five above it answer a lack. This one answers a thing the founder DID, and is
			// pushed by the lab — so no state of thirst, shelter, idleness, standing or grief may
			// ever raise it by accident.
			for (int water = 0; water <= 200; water += 40)
			{
				for (int beds = 0; beds <= 6; beds += 2)
				{
					Assert.AreNotEqual(KingdomRules.PetitionKind.Flesh,
						KingdomRules.ChoosePetition(water, 4, beds, 3, -400, false, 2));
				}
			}
		}

		[Test]
		public void FleshPetition_IsAnsweredByBeingHeardAndByNothingElse()
		{
			// There is no correct answer to it and nothing the founder can build settles it
			// (DIVERSITY §3.6). What meets it is standing in front of them; KingdomPetitions.Heard
			// writes the target this reads.
			Assert.IsFalse(KingdomRules.IsPetitionMet(KingdomRules.PetitionKind.Flesh, 0, 9999, 1, 99, 0, 500, true));
			Assert.IsTrue(KingdomRules.IsPetitionMet(KingdomRules.PetitionKind.Flesh, 1, 0, 99, 0, 9, -500, false));
		}

		[Test]
		public void FleshPetition_DoesNotDisturbTheFiveKindsAboveIt()
		{
			Assert.IsTrue(KingdomRules.IsPetitionMet(KingdomRules.PetitionKind.Thirst, 50, 60, 1, 9, 0, 0, true));
			Assert.IsFalse(KingdomRules.IsPetitionMet(KingdomRules.PetitionKind.Thirst, 50, 40, 1, 9, 0, 0, true));
			Assert.IsTrue(KingdomRules.IsPetitionMet(KingdomRules.PetitionKind.Craft, 0, 0, 1, 9, 0, 0, true));
			Assert.IsFalse(KingdomRules.IsPetitionMet(KingdomRules.PetitionKind.Craft, 0, 0, 1, 9, 2, 0, true));
			Assert.IsTrue(KingdomRules.IsPetitionMet(KingdomRules.PetitionKind.Memorial, 0, 0, 1, 9, 0, 0, true));
			Assert.IsFalse(KingdomRules.IsPetitionMet(KingdomRules.PetitionKind.None, 1, 0, 1, 9, 0, 0, true));
		}

		[Test]
		public void FleshPetition_TheProseIsTheLabsAndTheMachineryIsThePetitionsLane()
		{
			// The mesh condition: nothing parallel is built. The kind is the petitions lane's; every
			// word of it is the lab's, and these are the three the lane asks for.
			Assert.IsNotEmpty(KingdomLabRules.SpokenAgainstSubject());
			Assert.IsNotEmpty(KingdomLabRules.SpokenAgainstSpeech("the Templar"));
			Assert.IsNotEmpty(KingdomLabRules.SpokenAgainstDeed("Kavvat"));
			StringAssert.Contains("Kavvat", KingdomLabRules.SpokenAgainstDeed("Kavvat"));
		}

		// --- Creed friction (§3.6) ------------------------------------------------------------------

		[Test]
		public void StandingCost_ReadsTheRemovalIdiomTheQolVocabularyAlreadySpeaks()
		{
			List<KeyValuePair<string, int>> cost = KingdomLabRules.StandingCost("-Templar,-Mechanimists", 50);
			Assert.AreEqual(2, cost.Count);
			Assert.AreEqual("Templar", cost[0].Key);
			Assert.AreEqual(-50, cost[0].Value);
			Assert.AreEqual("Mechanimists", cost[1].Key);
			Assert.AreEqual(-50, cost[1].Value);
		}

		[Test]
		public void StandingCost_IgnoresAnythingThatIsNotARemoval()
		{
			// A procedure cannot BUY standing. If a record wants to, that is a design question and
			// not a parse.
			CollectionAssert.IsEmpty(KingdomLabRules.StandingCost("Templar,+Barathrumites", 50));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("-")]
		[TestCase("  ")]
		public void StandingCost_CostsNothingWhenNothingIsNamed(string creeds)
		{
			CollectionAssert.IsEmpty(KingdomLabRules.StandingCost(creeds, 50));
		}

		[Test]
		public void StandingCost_CostsNothingAtAZeroRate()
		{
			CollectionAssert.IsEmpty(KingdomLabRules.StandingCost("-Templar", 0));
		}

		[Test]
		public void SpeaksAgainstHall_NeedsAMinorityLargeEnoughToBeMoreThanOnePersonsOpinion()
		{
			Assert.IsFalse(KingdomLabRules.SpeaksAgainstHall(Offended: 1, People: 40, AlreadySpoken: false));
			Assert.IsTrue(KingdomLabRules.SpeaksAgainstHall(Offended: 4, People: 40, AlreadySpoken: false));
		}

		[Test]
		public void SpeaksAgainstHall_IsSilentWhereTheOffendedCreedIsTheMajority()
		{
			// That city could not staff the hall in the first place — Addendum 4d's fault-line
			// ceiling does the work, and no rule of ours says so.
			Assert.IsFalse(KingdomLabRules.SpeaksAgainstHall(Offended: 30, People: 40, AlreadySpoken: false));
			Assert.IsFalse(KingdomLabRules.SpeaksAgainstHall(Offended: 20, People: 40, AlreadySpoken: false));
		}

		[Test]
		public void SpeaksAgainstHall_SaysItOnceAndNeverAgain()
		{
			Assert.IsFalse(KingdomLabRules.SpeaksAgainstHall(Offended: 4, People: 40, AlreadySpoken: true));
		}

		[Test]
		public void SpeaksAgainstHall_IsSilentWhereNobodyMinds()
		{
			Assert.IsFalse(KingdomLabRules.SpeaksAgainstHall(0, 40, false));
			Assert.IsFalse(KingdomLabRules.SpeaksAgainstHall(4, 0, false));
			Assert.IsFalse(KingdomLabRules.SpeaksAgainstHall(-2, 40, false));
		}

		[Test]
		public void SpokenAgainstSpeech_IsAPersonAndNotAMeter()
		{
			// §3.6's closing rule: friction is placement constraints and named people. A line that
			// reported a number would be the revulsion score that section forbids by name.
			string speech = KingdomLabRules.SpokenAgainstSpeech("the Templar");
			StringAssert.Contains("the Templar", speech);
			StringAssert.DoesNotContain("%", speech);
			StringAssert.StartsWith("\"", speech);
		}

		// --- The slate ---------------------------------------------------------------------------------

		[Test]
		public void SlateIntro_LeadsWithTheEmptyHallBecauseThatIsWhatStopsEverything()
		{
			string intro = KingdomLabRules.SlateIntro(null, null, 11);
			StringAssert.Contains("No savant is lodged here", intro);
			StringAssert.Contains("11", intro);
		}

		[Test]
		public void SlateIntro_NamesTheSavantAndWhatTheyWere()
		{
			string intro = KingdomLabRules.SlateIntro("Nuntu", "a bone-surgeon at Ezra", 11);
			StringAssert.Contains("Nuntu", intro);
			StringAssert.Contains("bone-surgeon", intro);
		}

		[Test]
		public void SlateIntro_SaysNoneRatherThanZero()
		{
			StringAssert.Contains("none", KingdomLabRules.SlateIntro("Nuntu", null, 0));
		}

		[Test]
		public void SlotRow_MarksAPlaceWithSomethingOnItDifferentlyFromAnEmptyOne()
		{
			StringAssert.Contains(KingdomLabRules.MarkFilled,
				KingdomLabRules.SlotRow("your left arm", "the envenomed sting", true));
			StringAssert.Contains(KingdomLabRules.MarkEmpty,
				KingdomLabRules.SlotRow("your face", null, true));
		}

		[Test]
		public void SlotRow_SaysWhenTheHallHasNothingForAPlaceRatherThanShowingAnEmptyMark()
		{
			string row = KingdomLabRules.SlotRow("your feet", null, Offers: false);
			StringAssert.Contains("nothing the hall knows", row);
			StringAssert.DoesNotContain(KingdomLabRules.MarkEmpty, row);
		}

		[Test]
		public void CandidateRow_ShowsEveryEffectBeforeCommitment()
		{
			// The fix for the one documented complaint about the vanilla picker: players treat the
			// golem's atzmus as a lottery because the payoff is opaque at the point of choosing.
			LabProcedure procedure = Procedure("sporegills");
			procedure.Discloses.Add("your body puffs spore-gas at anything adjacent that is not your ally");
			procedure.Discloses.Add("the gas does not spare your city");
			string row = KingdomLabRules.CandidateRow(procedure, 3);
			StringAssert.Contains("spore-gas", row);
			StringAssert.Contains("does not spare your city", row);
			StringAssert.Contains(KingdomLabRules.EffectPrefix, row);
			StringAssert.Contains("[kept x3]", row);
		}

		[Test]
		public void CandidateRow_NeverUsesTheRandomnessMarkerBecauseThereIsNoRandomnessToDisclose()
		{
			// §3.1 rejects golem randomness by name, so the {{rules|OR}} prefix that discloses it
			// must never appear. A slate that used it would be promising a lottery we do not run.
			LabProcedure procedure = Procedure("sporegills");
			procedure.Discloses.Add("a thing happens");
			StringAssert.DoesNotContain("{{rules|OR}}", KingdomLabRules.CandidateRow(procedure, 1));
		}

		[Test]
		public void PriceLine_StatesTheWholePriceInTheUnitsTheFounderAlreadyReads()
		{
			string price = KingdomLabRules.PriceLine(Procedure("x", cost: 20, staffDays: 6, preserved: 1, bits: "002"));
			StringAssert.Contains("20 drams", price);
			StringAssert.Contains("002 in bits", price);
			StringAssert.Contains("1 kept part", price);
			StringAssert.Contains("6 days", price);
		}

		[Test]
		public void PriceLine_CountsInSingularAndPlural()
		{
			StringAssert.Contains("1 day", KingdomLabRules.PriceLine(Procedure("a", staffDays: 1, preserved: 1)));
			StringAssert.Contains("1 kept part", KingdomLabRules.PriceLine(Procedure("b", staffDays: 1, preserved: 1)));
			StringAssert.Contains("4 kept parts", KingdomLabRules.PriceLine(Procedure("c", staffDays: 3, preserved: 4)));
		}

		[Test]
		public void PriceLine_LeavesOutTheBitsWhenThereAreNone()
		{
			StringAssert.DoesNotContain("in bits", KingdomLabRules.PriceLine(Procedure("x", bits: null)));
		}

		[Test]
		public void ReversibilityLine_AnswersTheQuestionThatStrandedTheOtherModsPlayers()
		{
			// Playable Golem's dominant complaint is that a body change locked players out of
			// content. The consent story is that nothing the lab does is permanent against the
			// founder's will, and it is stated before commitment or it is not a consent story.
			string line = KingdomLabRules.ReversibilityLine();
			StringAssert.Contains("take it off", line);
			StringAssert.Contains("returns nothing", line);
		}

		[Test]
		public void ConsentOptions_AreTheThreeWayPromptAndTheThirdIsPermanent()
		{
			Assert.AreEqual(3, KingdomLabRules.ConsentOptions.Length);
			StringAssert.Contains("Have it done", KingdomLabRules.ConsentOptions[0]);
			StringAssert.Contains("Not now", KingdomLabRules.ConsentOptions[1]);
			StringAssert.Contains("Never", KingdomLabRules.ConsentOptions[2]);
		}

		[Test]
		public void StakedLine_SaysThatCommissioningIsNotClicking()
		{
			// The whole mod's grammar: crews work it over world-days and the founder may walk away.
			// The lab may not be the one place that breaks it.
			string line = KingdomLabRules.StakedLine("the envenomed sting", 6);
			StringAssert.Contains("6", line);
			StringAssert.Contains("days", line);
			StringAssert.Contains("Go and do something else", line);
		}

		[Test]
		public void EveryLine_IsWrittenInRegisterAndNoneOfThemSaysThatSomethingFailed()
		{
			List<string> lines = new List<string>
			{
				KingdomLabRules.PurposeRefusalLine("the arcology"),
				KingdomLabRules.ReversibilityLine(),
				KingdomLabRules.StakedLine("the tarry grip", 4),
				KingdomLabRules.DoneLine("the tarry grip", "Kavvat"),
				KingdomLabRules.DoneTelling("the tarry grip", "Kavvat"),
				KingdomLabRules.RemovedTelling("the tarry grip", "Kavvat"),
				KingdomLabRules.NothingMeetsRequirement("your left arm"),
				KingdomLabRules.SpokenAgainstSpeech("the Templar"),
				KingdomLabRules.SpokenAgainstDeed("Kavvat")
			};
			for (int i = 0; i < lines.Count; i++)
			{
				Assert.IsNotEmpty(lines[i]);
				StringAssert.DoesNotContain("failed", lines[i].ToLowerInvariant());
				StringAssert.DoesNotContain("error", lines[i].ToLowerInvariant());
				StringAssert.DoesNotContain("invalid", lines[i].ToLowerInvariant());
			}
		}

		[Test]
		public void Named_IsTotalOverNothing()
		{
			Assert.AreEqual("the work", KingdomLabRules.Named(null));
			Assert.AreEqual("the work", KingdomLabRules.Named(""));
			Assert.AreEqual("Kavvat", KingdomLabRules.Named("  Kavvat  "));
		}
	}
}
#endif
