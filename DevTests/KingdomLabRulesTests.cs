#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomLabRulesTests
	{
		private static void AssertByteEnum(Type Type, string Expected)
		{
			ClassicAssert.AreEqual(typeof(byte), Enum.GetUnderlyingType(Type), Type.Name);
			ClassicAssert.IsFalse(Type.IsNested, Type.Name);
			ClassicAssert.AreEqual("ThousandAndFirst." + Type.Name, Type.FullName);
			ClassicAssert.AreEqual(Type == typeof(KingdomPurposeVerdict), Type.IsPublic, Type.Name);
			Array values = Enum.GetValues(Type);
			List<string> actual = new List<string>();
			foreach (object value in values)
			{
				actual.Add(Convert.ToByte(value) + ":" + Enum.GetName(Type, value));
			}
			ClassicAssert.AreEqual(Expected, string.Join(",", actual.ToArray()), Type.Name);
		}

		private static void AssertFields(Type Type, string[] Names, Type[] Types,
			bool Readonly)
		{
			ClassicAssert.IsFalse(Type.IsNested, Type.Name);
			ClassicAssert.IsTrue(Type.IsNotPublic, Type.Name);
			ClassicAssert.AreEqual("ThousandAndFirst." + Type.Name, Type.FullName);
			FieldInfo[] fields = Type.GetFields(BindingFlags.Instance | BindingFlags.Public
				| BindingFlags.DeclaredOnly);
			ClassicAssert.AreEqual(Names.Length, fields.Length, Type.Name);
			for (int i = 0; i < Names.Length; i++)
			{
				ClassicAssert.AreEqual(Names[i], fields[i].Name, Type.Name + " field " + i);
				ClassicAssert.AreEqual(Types[i], fields[i].FieldType, Names[i]);
				ClassicAssert.AreEqual(Readonly, fields[i].IsInitOnly, Names[i]);
			}
		}

		[Test]
		public void ExtractedLabEnumsKeepExactByteAbi()
		{
			AssertByteEnum(typeof(KingdomVatSettlement),
				"0:Wait,1:CreateOutput,2:ConsumeInput,3:CollectOutput,4:ReturnInput,5:Missing");
			AssertByteEnum(typeof(KingdomVatOutputPhase),
				"0:None,1:AddIntent,2:Added,3:Quarantined");
			AssertByteEnum(typeof(KingdomVatRawPhase),
				"0:Present,1:DestroyIntent,2:Destroyed,3:Quarantined");
			AssertByteEnum(typeof(KingdomKeptSpendPhase),
				"0:RefusedClean,1:ApplyCounts,2:Finalize,3:SpentExact,4:Partial");
			AssertByteEnum(typeof(KingdomLabJobPhase),
				"0:Funding,1:FundingRecovery,2:Working,3:Ready,4:Applying,5:ApplicationRecovery,6:Complete,7:Cancelled");
			AssertByteEnum(typeof(KingdomLabRemovalPhase),
				"0:Funding,1:FundingRecovery,2:Paid,3:Removing,4:RemovalRecovery,5:Removed,6:Complete,7:Quarantined,8:Cancelled");
			AssertByteEnum(typeof(KingdomLabOwnedTargetState),
				"0:Present,1:Absent,2:Uncertain");
			AssertByteEnum(typeof(KingdomLabStandingPhase),
				"0:Pending,1:Bound,2:Intent,3:Applied,4:Quarantined");
			AssertByteEnum(typeof(KingdomLabMessagePhase),
				"0:Pending,1:Intent,2:Delivered,3:Skipped,4:Lost");
			AssertByteEnum(typeof(KingdomLabRegistryStatus),
				"0:Active,1:Complete,2:Cancelled,3:Abandoned,4:Quarantined");
			AssertByteEnum(typeof(KingdomVatOutputDecision),
				"0:CreateAndFreeze,1:UseExact,2:QuarantineMissing,3:QuarantineMismatch");
			AssertByteEnum(typeof(KingdomPurposeVerdict),
				"0:Allowed,1:RefusedKept,2:RefusedUncrowned");
		}

		[Test]
		public void ExtractedLabRowsKeepExactTopLevelFieldAbiAndDefaults()
		{
			ClassicAssert.IsTrue(typeof(KingdomVatAccrual).IsValueType);
			ClassicAssert.IsTrue(typeof(KingdomKeptSpendStep).IsValueType);
			ClassicAssert.IsTrue(typeof(KingdomLabJobAccrual).IsValueType);
			ClassicAssert.IsTrue(typeof(KingdomLabWaterClaim).IsValueType);
			ClassicAssert.IsTrue(typeof(KingdomKeptSpendPlan).IsSealed);
			ClassicAssert.IsTrue(typeof(KingdomLabRegistryEntry).IsSealed);
			AssertFields(typeof(KingdomVatAccrual),
				new[] { "NextTick", "RemainingTicks", "WorkedTicks", "Complete" },
				new[] { typeof(long), typeof(int), typeof(int), typeof(bool) }, true);
			AssertFields(typeof(KingdomKeptSpendStep),
				new[] { "Source", "Original", "Taken", "Remaining" },
				new[] { typeof(int), typeof(int), typeof(int), typeof(int) }, true);
			AssertFields(typeof(KingdomKeptSpendPlan), new[] { "Owed", "Steps" },
				new[] { typeof(int), typeof(List<KingdomKeptSpendStep>) }, true);
			AssertFields(typeof(KingdomLabJobAccrual),
				new[] { "NextTick", "RemainingTicks", "WorkedTicks", "Phase" },
				new[] { typeof(long), typeof(int), typeof(int), typeof(KingdomLabJobPhase) }, true);
			AssertFields(typeof(KingdomLabWaterClaim),
				new[] { "Paid", "Lost", "Outstanding", "Quarantined", "Settled" },
				new[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool) }, true);
			AssertFields(typeof(KingdomLabRegistryEntry), new[] { "JobId", "BuildingId",
				"PatientId", "GameId", "RealmId", "RealmFoundedTick", "RulerSuccessionOrdinal",
				"RulerLifeId", "ContractVersion", "ProcedureKey", "Grants", "Source",
				"Attach", "Manager", "Detail",
				"Fingerprint", "Status", "UpdatedTick" }, new[] { typeof(string), typeof(string),
				typeof(string), typeof(string), typeof(string), typeof(long), typeof(int),
				typeof(string), typeof(int), typeof(string), typeof(string), typeof(int),
				typeof(int), typeof(string),
				typeof(string), typeof(string), typeof(KingdomLabRegistryStatus), typeof(long) },
				false);

			KingdomLabRegistryEntry row = new KingdomLabRegistryEntry();
			ClassicAssert.AreEqual("", row.JobId);
			ClassicAssert.AreEqual("", row.BuildingId);
			ClassicAssert.AreEqual("", row.PatientId);
			ClassicAssert.AreEqual("", row.GameId);
			ClassicAssert.AreEqual("", row.RealmId);
			ClassicAssert.AreEqual(0L, row.RealmFoundedTick);
			ClassicAssert.AreEqual(-1, row.RulerSuccessionOrdinal);
			ClassicAssert.AreEqual("", row.RulerLifeId);
			ClassicAssert.AreEqual(0, row.ContractVersion);
			ClassicAssert.AreEqual("", row.ProcedureKey);
			ClassicAssert.AreEqual("", row.Grants);
			ClassicAssert.AreEqual(-1, row.Source);
			ClassicAssert.AreEqual(-1, row.Attach);
			ClassicAssert.AreEqual("", row.Manager);
			ClassicAssert.AreEqual("", row.Detail);
			ClassicAssert.AreEqual("", row.Fingerprint);
			ClassicAssert.AreEqual(KingdomLabRegistryStatus.Active, row.Status);
			ClassicAssert.AreEqual(0L, row.UpdatedTick);

			KingdomKeptSpendStep step = new KingdomKeptSpendStep(2, 7, 3);
			ClassicAssert.AreEqual(2, step.Source);
			ClassicAssert.AreEqual(7, step.Original);
			ClassicAssert.AreEqual(3, step.Taken);
			ClassicAssert.AreEqual(4, step.Remaining);
		}

		private static LabProcedure Procedure(string key, string cls = "II", string grants = "GasImmunity",
			string creeds = null, int cost = 30, int staffDays = 8, int preserved = 1, string bits = "002")
		{
			LabProcedure procedure;
			string error;
			ClassicAssert.IsTrue(KingdomProcedureRules.TryParseProcedureAttributes(key, null, cls, grants, "Body",
				null, "part", "body", null, cost.ToString(), bits, staffDays.ToString(), preserved.ToString(),
				creeds, null, null, out procedure, out error), error);
			return procedure;
		}

		private static KingdomLabRegistryEntry RegistryRow(string job, long updated = 1L)
		{
			string detail = "stamp:" + KingdomLabRules.ExecutionStampFingerprint("source-stamp");
			string fingerprint = KingdomLabRules.EffectFingerprint(
				KingdomLabRules.EffectContractVersion, "sporegills", "GasImmunity",
				(int)LabSource.Part, (int)LabAttach.Body, "TAF::Lab::sporegills", detail);
			return new KingdomLabRegistryEntry
			{
				JobId = job,
				BuildingId = "hall-1",
				PatientId = "patient-1",
				GameId = "game-1",
				RealmId = "realm-1",
				RealmFoundedTick = 44L,
				ContractVersion = KingdomLabRules.EffectContractVersion,
				ProcedureKey = "sporegills",
				Grants = "GasImmunity",
				Source = (int)LabSource.Part,
				Attach = (int)LabAttach.Body,
				Manager = "TAF::Lab::sporegills",
				Detail = detail,
				Fingerprint = fingerprint,
				Status = KingdomLabRegistryStatus.Active,
				UpdatedTick = updated
			};
		}

		// --- The rung ladder, from what is actually standing --------------------------------------

		[TestCase(false, false, false, false, -1)]
		[TestCase(true, false, false, false, 0)]
		[TestCase(true, true, false, false, 1)]
		[TestCase(true, true, true, false, 2)]
		[TestCase(true, true, true, true, 3)]
		public void RungReached_ClimbsOneStepAtATime(bool slab, bool vat, bool hall, bool theatre, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomLabRules.RungReached(slab, vat, hall, theatre));
		}

		[Test]
		public void RungReached_IsTheHighestUNBROKENStepAndNotTheHighestBuiltOne()
		{
			// A theatre with no vats under it can graft nothing, because the theatre's own inputs
			// come out of the vats. A founder who raised the grand thing first gets told so.
			ClassicAssert.AreEqual(0, KingdomLabRules.RungReached(Slab: true, Vat: false, Hall: true, Theatre: true));
			ClassicAssert.AreEqual(-1, KingdomLabRules.RungReached(Slab: false, Vat: true, Hall: true, Theatre: true));
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
			ClassicAssert.IsNull(KingdomLabRules.LadderGapLine(true, true, true, true));
			ClassicAssert.IsNull(KingdomLabRules.LadderGapLine(true, true, true, false));
			ClassicAssert.IsNull(KingdomLabRules.LadderGapLine(true, true, false, false));
			ClassicAssert.IsNull(KingdomLabRules.LadderGapLine(false, false, false, false));
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
			ClassicAssert.AreEqual(expected, KingdomLabRules.IsMegastructure(declared));
		}

		[Test]
		public void JudgePurpose_RefusesASecondMegastructureInACityThatAlreadyHasOne()
		{
			ClassicAssert.AreEqual(KingdomPurposeVerdict.RefusedKept,
				KingdomLabRules.JudgePurpose(Megastructure: true, Kept: "arcology", Key: "chimerictheatre"));
		}

		[Test]
		public void JudgePurpose_AllowsTheFirstOne()
		{
			ClassicAssert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(Megastructure: true, Kept: null, Key: "chimerictheatre"));
			ClassicAssert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(Megastructure: true, Kept: "", Key: "chimerictheatre"));
		}

		[Test]
		public void JudgePurpose_ReKeyingTheSameOneIsNotChoosingAgain()
		{
			// Mending, re-siting or re-staking the megastructure a city already has is not a second
			// purpose, and refusing it would make a purpose unrepairable.
			ClassicAssert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(true, "chimerictheatre", "chimerictheatre"));
			ClassicAssert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(true, "ChimericTheatre", "chimerictheatre"));
		}

		[Test]
		public void JudgePurpose_NeverStandsInTheWayOfAnOrdinaryDesign()
		{
			// The gate is one check on one attribute and it must be inert for every building in the
			// catalogue that is not a megastructure — which is all of them but one.
			ClassicAssert.AreEqual(KingdomPurposeVerdict.Allowed,
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
			ClassicAssert.IsNull(error);
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
			ClassicAssert.IsFalse(judgement.Permitted);
			ClassicAssert.AreEqual(ZoningVerdict.RefusedMegastructure, judgement.Verdict);
			// The Detail carries the KEY, because the refusal is composed one lane over where the
			// catalogue can be asked what a key is called.
			ClassicAssert.AreEqual("arcology", judgement.Detail);
			ClassicAssert.IsNotEmpty(judgement.Note);
		}

		[Test]
		public void Zoning_AllowsTheFirstMegastructure()
		{
			ClassicAssert.IsTrue(JudgeTheatre("yes", "chimerictheatre", null).Permitted);
			ClassicAssert.IsTrue(JudgeTheatre("yes", "chimerictheatre", "").Permitted);
		}

		[Test]
		public void Zoning_AllowsReKeyingTheOneTheCityAlreadyKeeps()
		{
			// Mending, re-siting or re-staking a city's own purpose is not choosing a second one,
			// and refusing it would make a purpose unrepairable.
			ClassicAssert.IsTrue(JudgeTheatre("yes", "chimerictheatre", "chimerictheatre").Permitted);
			ClassicAssert.IsTrue(JudgeTheatre("yes", "chimerictheatre", "ChimericTheatre").Permitted);
		}

		[Test]
		public void Zoning_NeverStandsInTheWayOfAnOrdinaryDesign()
		{
			// The gate must be inert for every design in the catalogue but one — which is what makes
			// one attribute and one check the whole of the vocabulary.
			ClassicAssert.IsTrue(JudgeTheatre(null, "smithy", "arcology").Permitted);
			ClassicAssert.IsTrue(JudgeTheatre("no", "smithy", "arcology").Permitted);
		}

		[Test]
		public void Zoning_FailsOPENWhenNothingCouldTellWhatTheCityKeeps()
		{
			// KingdomZoning.KeptMegastructure hands back null when it cannot read the city, and a
			// cardinality rule that could not see the city must let the founder build. The
			// alternative is a realm bricked by a book it could not open.
			ClassicAssert.IsTrue(JudgeTheatre("yes", "chimerictheatre", null).Permitted);
		}

		[Test]
		public void Zoning_TheOlderJudgeOverloadsStillPermitAMegastructure()
		{
			// Every caller written before this landed passes no CityKeeps, and must go on behaving
			// exactly as it did — the same back-compatibility promise Strata made one gate over.
			ClassicAssert.IsTrue(KingdomZoningRules.Judge(Gate("yes"), null, "craft", 0, null).Permitted);
			ClassicAssert.IsTrue(KingdomZoningRules.Judge(Gate("yes"), null, "craft", 0, null,
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
			ClassicAssert.IsNull(error);
			ZoningJudgement judgement = KingdomZoningRules.Judge(gate, null, "craft", 0, null,
				Underground: false, RequiresSky: false, Roll: BuilderRoll.Unknown,
				Stratum: null, Key: "chimerictheatre", CityKeeps: "arcology");
			ClassicAssert.AreEqual(ZoningVerdict.RefusedTechLevel, judgement.Verdict);
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
			ClassicAssert.IsNull(error);
			ClassicAssert.AreEqual(expected, gate.Megastructure);
		}

		[Test]
		public void ParseGate_AMegastructureIsNotAnOpenGate()
		{
			ClassicAssert.IsFalse(Gate("yes").IsOpen);
			ClassicAssert.IsTrue(Gate(null).IsOpen);
		}

		[Test]
		public void ZoningVerdict_TheOrdinalsBelowTheNewOneAreUnmoved()
		{
			// These are published and are switched on by third parties (STANDARDS §9). Appending is
			// additive; renumbering is a break, and this table is what would catch one.
			ClassicAssert.AreEqual(0, (int)ZoningVerdict.Permitted);
			ClassicAssert.AreEqual(1, (int)ZoningVerdict.RefusedUnlearned);
			ClassicAssert.AreEqual(2, (int)ZoningVerdict.RefusedTechLevel);
			ClassicAssert.AreEqual(3, (int)ZoningVerdict.RefusedTerritory);
			ClassicAssert.AreEqual(4, (int)ZoningVerdict.RefusedStratum);
			ClassicAssert.AreEqual(5, (int)ZoningVerdict.RefusedDistrict);
			ClassicAssert.AreEqual(6, (int)ZoningVerdict.RefusedUnaligned);
			ClassicAssert.AreEqual(7, (int)ZoningVerdict.RefusedCreedShare);
			ClassicAssert.AreEqual(8, (int)ZoningVerdict.RefusedBuilders);
			ClassicAssert.AreEqual(9, (int)ZoningVerdict.RefusedMegastructure);
		}

		// --- The petition the hall provokes (§3.6's first authored happening) -------------------------

		[Test]
		public void PetitionKind_TheOrdinalsBelowTheNewOneAreUnmoved()
		{
			// Carried in a save. Appending is additive; renumbering silently reinterprets every
			// petition standing in every existing game.
			ClassicAssert.AreEqual(0, (int)KingdomRules.PetitionKind.None);
			ClassicAssert.AreEqual(1, (int)KingdomRules.PetitionKind.Thirst);
			ClassicAssert.AreEqual(2, (int)KingdomRules.PetitionKind.Shelter);
			ClassicAssert.AreEqual(3, (int)KingdomRules.PetitionKind.Craft);
			ClassicAssert.AreEqual(4, (int)KingdomRules.PetitionKind.Peace);
			ClassicAssert.AreEqual(5, (int)KingdomRules.PetitionKind.Memorial);
			ClassicAssert.AreEqual(6, (int)KingdomRules.PetitionKind.Flesh);
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
					ClassicAssert.AreNotEqual(KingdomRules.PetitionKind.Flesh,
						KingdomRules.ChoosePetition(water, 4, beds, 3, -400, false, 2));
				}
			}
		}

		[Test]
		public void FleshPetition_IsAnsweredByBeingHeardAndByNothingElse()
		{
			// There is no correct answer to it and nothing the founder can build settles it
			// (DIVERSITY §3.6). Hearing the speech supplies this frozen target; accepting the
			// petition separately gates resolution in KingdomPetitionRules.CanResolve.
			ClassicAssert.IsFalse(KingdomRules.IsPetitionMet(KingdomRules.PetitionKind.Flesh, 0, 9999, 1, 99, 0, 500, true));
			ClassicAssert.IsTrue(KingdomRules.IsPetitionMet(KingdomRules.PetitionKind.Flesh, 1, 0, 99, 0, 9, -500, false));
		}

		[Test]
		public void FleshPetition_DoesNotDisturbTheFiveKindsAboveIt()
		{
			ClassicAssert.IsTrue(KingdomRules.IsPetitionMet(KingdomRules.PetitionKind.Thirst, 50, 60, 1, 9, 0, 0, true));
			ClassicAssert.IsFalse(KingdomRules.IsPetitionMet(KingdomRules.PetitionKind.Thirst, 50, 40, 1, 9, 0, 0, true));
			ClassicAssert.IsTrue(KingdomRules.IsPetitionMet(KingdomRules.PetitionKind.Craft, 0, 0, 1, 9, 0, 0, true));
			ClassicAssert.IsFalse(KingdomRules.IsPetitionMet(KingdomRules.PetitionKind.Craft, 0, 0, 1, 9, 2, 0, true));
			ClassicAssert.IsTrue(KingdomRules.IsPetitionMet(KingdomRules.PetitionKind.Memorial, 0, 0, 1, 9, 0, 0, true));
			ClassicAssert.IsFalse(KingdomRules.IsPetitionMet(KingdomRules.PetitionKind.None, 1, 0, 1, 9, 0, 0, true));
		}

		[Test]
		public void FleshPetition_TheProseIsTheLabsAndTheMachineryIsThePetitionsLane()
		{
			// The mesh condition: nothing parallel is built. The kind is the petitions lane's; every
			// word of it is the lab's, and these are the three the lane asks for.
			ClassicAssert.IsNotEmpty(KingdomLabRules.SpokenAgainstSubject());
			ClassicAssert.IsNotEmpty(KingdomLabRules.SpokenAgainstSpeech("the Templar"));
			ClassicAssert.IsNotEmpty(KingdomLabRules.SpokenAgainstDeed("Kavvat"));
			StringAssert.Contains("Kavvat", KingdomLabRules.SpokenAgainstDeed("Kavvat"));
		}

		// --- Creed friction (§3.6) ------------------------------------------------------------------

		[Test]
		public void StandingCost_ReadsTheRemovalIdiomTheQolVocabularyAlreadySpeaks()
		{
			List<KeyValuePair<string, int>> cost = KingdomLabRules.StandingCost("-Templar,-Mechanimists", 50);
			ClassicAssert.AreEqual(2, cost.Count);
			ClassicAssert.AreEqual("Templar", cost[0].Key);
			ClassicAssert.AreEqual(-50, cost[0].Value);
			ClassicAssert.AreEqual("Mechanimists", cost[1].Key);
			ClassicAssert.AreEqual(-50, cost[1].Value);
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
			ClassicAssert.IsFalse(KingdomLabRules.SpeaksAgainstHall(Offended: 1, People: 40, AlreadySpoken: false));
			ClassicAssert.IsTrue(KingdomLabRules.SpeaksAgainstHall(Offended: 4, People: 40, AlreadySpoken: false));
		}

		[Test]
		public void SpeaksAgainstHall_IsSilentWhereTheOffendedCreedIsTheMajority()
		{
			// That city could not staff the hall in the first place — Addendum 4d's fault-line
			// ceiling does the work, and no rule of ours says so.
			ClassicAssert.IsFalse(KingdomLabRules.SpeaksAgainstHall(Offended: 30, People: 40, AlreadySpoken: false));
			ClassicAssert.IsFalse(KingdomLabRules.SpeaksAgainstHall(Offended: 20, People: 40, AlreadySpoken: false));
		}

		[Test]
		public void SpeaksAgainstHall_SaysItOnceAndNeverAgain()
		{
			ClassicAssert.IsFalse(KingdomLabRules.SpeaksAgainstHall(Offended: 4, People: 40, AlreadySpoken: true));
		}

		[Test]
		public void SpeaksAgainstHall_IsSilentWhereNobodyMinds()
		{
			ClassicAssert.IsFalse(KingdomLabRules.SpeaksAgainstHall(0, 40, false));
			ClassicAssert.IsFalse(KingdomLabRules.SpeaksAgainstHall(4, 0, false));
			ClassicAssert.IsFalse(KingdomLabRules.SpeaksAgainstHall(-2, 40, false));
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
		public void PriceLine_DisclosesEveryStandingProjectionBeforeCommission()
		{
			string price = KingdomLabRules.PriceLine(Procedure("x",
				creeds: "-Templar,-Mechanimists"));
			StringAssert.Contains("-50 with Templar", price);
			StringAssert.Contains("-50 with Mechanimists", price);
		}

		// --- Exact kept-parts transaction ---------------------------------------------------------

		[Test]
		public void KeptSpendPlan_CountTwoOwedTwoFinalizesOneWholeStack()
		{
			KingdomKeptSpendPlan plan;
			ClassicAssert.IsTrue(KingdomLabRules.TryPlanKeptSpend(new int[] { 2 }, 2, out plan));
			ClassicAssert.AreEqual(1, plan.Steps.Count);
			ClassicAssert.AreEqual(2, plan.Steps[0].Taken);
			ClassicAssert.AreEqual(0, plan.Steps[0].Remaining);
			ClassicAssert.IsTrue(plan.Steps[0].NeedsFinalization);
			ClassicAssert.AreEqual(1, plan.Finalizers);
		}

		[Test]
		public void KeptSpendPlan_OneAndTwoOwedThreePreflightsAndFinalizesBothSources()
		{
			KingdomKeptSpendPlan plan;
			ClassicAssert.IsTrue(KingdomLabRules.TryPlanKeptSpend(new int[] { 1, 2 }, 3, out plan));
			ClassicAssert.AreEqual(2, plan.Steps.Count);
			ClassicAssert.AreEqual(0, plan.Steps[0].Remaining);
			ClassicAssert.AreEqual(0, plan.Steps[1].Remaining);
			ClassicAssert.AreEqual(2, plan.Finalizers);
		}

		[Test]
		public void KeptSpendPlan_IsExactForEverySmallSourceShapeAndDebt()
		{
			for (int first = 0; first <= 3; first++)
			{
				for (int second = 0; second <= 3; second++)
				{
					for (int third = 0; third <= 3; third++)
					{
						int[] available = new int[] { first, second, third };
						int total = first + second + third;
						for (int owed = 0; owed <= total + 1; owed++)
						{
							KingdomKeptSpendPlan plan;
							bool planned = KingdomLabRules.TryPlanKeptSpend(available, owed, out plan);
							ClassicAssert.AreEqual(owed <= total, planned,
								"shape [" + first + "," + second + "," + third + "] owed " + owed);
							if (!planned)
							{
								ClassicAssert.IsNull(plan);
								continue;
							}
							int taken = 0;
							int previous = -1;
							for (int i = 0; i < plan.Steps.Count; i++)
							{
								KingdomKeptSpendStep step = plan.Steps[i];
								ClassicAssert.Greater(step.Source, previous);
								ClassicAssert.AreEqual(available[step.Source], step.Original);
								ClassicAssert.Greater(step.Taken, 0);
								ClassicAssert.LessOrEqual(step.Taken, step.Original);
								ClassicAssert.AreEqual(step.Original - step.Taken, step.Remaining);
								if (i + 1 < plan.Steps.Count)
								{
									ClassicAssert.IsTrue(step.NeedsFinalization);
								}
								taken += step.Taken;
								previous = step.Source;
							}
							ClassicAssert.AreEqual(owed, taken);
						}
					}
				}
			}
		}

		[Test]
		public void KeptSpendPhase_ExhaustivelySeparatesCleanRefusalFromIrreversiblePartial()
		{
			for (int count = 1; count <= 3; count++)
			{
				int[] sources = new int[count];
				for (int i = 0; i < count; i++)
				{
					sources[i] = 1;
				}
				KingdomKeptSpendPlan plan;
				ClassicAssert.IsTrue(KingdomLabRules.TryPlanKeptSpend(sources, count, out plan));
				ClassicAssert.AreEqual(KingdomKeptSpendPhase.RefusedClean,
					KingdomLabRules.KeptSpendPhase(plan, false, false, 0, true, true));
				ClassicAssert.AreEqual(KingdomKeptSpendPhase.Partial,
					KingdomLabRules.KeptSpendPhase(plan, false, false, 0, true, false));
				ClassicAssert.AreEqual(KingdomKeptSpendPhase.ApplyCounts,
					KingdomLabRules.KeptSpendPhase(plan, true, false, 0, false, true));
				for (int finalized = 0; finalized <= count; finalized++)
				{
					ClassicAssert.AreEqual(finalized == count
						? KingdomKeptSpendPhase.SpentExact
						: KingdomKeptSpendPhase.Finalize,
						KingdomLabRules.KeptSpendPhase(plan, true, true, finalized, false, false));
					ClassicAssert.AreEqual(finalized == 0
						? KingdomKeptSpendPhase.RefusedClean
						: KingdomKeptSpendPhase.Partial,
						KingdomLabRules.KeptSpendPhase(plan, true, true, finalized, true,
							CountsRestored: finalized == 0));
				}
			}
		}

		[Test]
		public void ProcedureEffectChanged_UsesDurableDirectionNotEngineReturnOrThrow()
		{
			for (int before = 0; before <= 3; before++)
			{
				for (int after = 0; after <= 3; after++)
				{
					ClassicAssert.AreEqual(after > before,
						KingdomLabRules.ProcedureEffectChanged(before, after, Removing: false));
					ClassicAssert.AreEqual(after < before,
						KingdomLabRules.ProcedureEffectChanged(before, after, Removing: true));
				}
			}
			ClassicAssert.IsFalse(KingdomLabRules.ProcedureEffectChanged(-1, 1, Removing: false));
			ClassicAssert.IsFalse(KingdomLabRules.ProcedureEffectChanged(1, -1, Removing: true));
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
			ClassicAssert.AreEqual(3, KingdomLabRules.ConsentOptions.Length);
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

		// --- The vat-house's durable job ----------------------------------------------------------

		[Test]
		public void VatAccrual_FirstLookPlantsAStampAndNeverBackdatesWork()
		{
			KingdomVatAccrual result = KingdomLabRules.AccrueVat(LastTick: 0L, TimeTick: 100000L,
				RemainingTicks: 1200, CrewEffectiveness: 100, WearEffectiveness: 100,
				Settled: false, Cancelled: false);
			ClassicAssert.AreEqual(100000L, result.NextTick);
			ClassicAssert.AreEqual(1200, result.RemainingTicks);
			ClassicAssert.AreEqual(0, result.WorkedTicks);
			ClassicAssert.IsFalse(result.Complete);
		}

		[Test]
		public void VatAccrual_NoStaffSpendsTimeButDoesNoWork()
		{
			KingdomVatAccrual result = KingdomLabRules.AccrueVat(100L, 1300L, 1200,
				CrewEffectiveness: 0, WearEffectiveness: 100, Settled: false, Cancelled: false);
			ClassicAssert.AreEqual(1300L, result.NextTick);
			ClassicAssert.AreEqual(1200, result.RemainingTicks);
			ClassicAssert.AreEqual(0, result.WorkedTicks);
			ClassicAssert.IsFalse(result.Complete);
		}

		[Test]
		public void VatAccrual_PartialCrewBanksOnlyWorkActuallyDone()
		{
			KingdomVatAccrual result = KingdomLabRules.AccrueVat(100L, 1300L, 1200,
				CrewEffectiveness: 50, WearEffectiveness: 100, Settled: false, Cancelled: false);
			ClassicAssert.AreEqual(600, result.WorkedTicks);
			ClassicAssert.AreEqual(600, result.RemainingTicks);
			ClassicAssert.IsFalse(result.Complete);
		}

		[Test]
		public void VatAccrual_CompletesAtZeroAndNeverRunsBelowIt()
		{
			KingdomVatAccrual result = KingdomLabRules.AccrueVat(100L, 1300L, 400,
				CrewEffectiveness: 100, WearEffectiveness: 100, Settled: false, Cancelled: false);
			ClassicAssert.AreEqual(400, result.WorkedTicks);
			ClassicAssert.AreEqual(0, result.RemainingTicks);
			ClassicAssert.IsTrue(result.Complete);
		}

		[Test]
		public void VatAccrual_SettledOrCancelledJobsNeverCompleteAgain()
		{
			KingdomVatAccrual settled = KingdomLabRules.AccrueVat(100L, 1300L, 0,
				100, 100, Settled: true, Cancelled: false);
			KingdomVatAccrual cancelled = KingdomLabRules.AccrueVat(100L, 1300L, 1200,
				100, 100, Settled: false, Cancelled: true);
			ClassicAssert.IsFalse(settled.Complete);
			ClassicAssert.IsFalse(cancelled.Complete);
			ClassicAssert.AreEqual(0, settled.WorkedTicks);
			ClassicAssert.AreEqual(0, cancelled.WorkedTicks);
		}

		[Test]
		public void VatSettlement_CreatesThenConsumesThenOnlyCollects()
		{
			ClassicAssert.AreEqual(KingdomVatSettlement.CreateOutput,
				KingdomLabRules.VatSettlement(InputPresent: true, OutputPresent: false,
					WorkComplete: true, CancelRequested: false));
			ClassicAssert.AreEqual(KingdomVatSettlement.ConsumeInput,
				KingdomLabRules.VatSettlement(InputPresent: true, OutputPresent: true,
					WorkComplete: true, CancelRequested: false));
			ClassicAssert.AreEqual(KingdomVatSettlement.CollectOutput,
				KingdomLabRules.VatSettlement(InputPresent: false, OutputPresent: true,
					WorkComplete: true, CancelRequested: false));
			ClassicAssert.AreNotEqual(KingdomVatSettlement.CreateOutput,
				KingdomLabRules.VatSettlement(InputPresent: false, OutputPresent: true,
					WorkComplete: true, CancelRequested: false));
		}

		[Test]
		public void VatSettlement_CancellationReturnsInputAndRecoversFinishedOutput()
		{
			ClassicAssert.AreEqual(KingdomVatSettlement.ReturnInput,
				KingdomLabRules.VatSettlement(InputPresent: true, OutputPresent: false,
					WorkComplete: false, CancelRequested: true));
			ClassicAssert.AreEqual(KingdomVatSettlement.CollectOutput,
				KingdomLabRules.VatSettlement(InputPresent: true, OutputPresent: true,
					WorkComplete: true, CancelRequested: true));
			ClassicAssert.AreNotEqual(KingdomVatSettlement.ReturnInput,
				KingdomLabRules.VatSettlement(InputPresent: true, OutputPresent: true,
					WorkComplete: false, CancelRequested: true));
			ClassicAssert.AreEqual(KingdomVatSettlement.Missing,
				KingdomLabRules.VatSettlement(InputPresent: false, OutputPresent: false,
					WorkComplete: false, CancelRequested: true));
		}

		[Test]
		public void VatAccrual_HugeAbsenceSaturatesWithoutOverflow()
		{
			KingdomVatAccrual result = KingdomLabRules.AccrueVat(1L, long.MaxValue, int.MaxValue,
				CrewEffectiveness: int.MaxValue, WearEffectiveness: int.MaxValue,
				Settled: false, Cancelled: false);
			ClassicAssert.AreEqual(int.MaxValue, result.WorkedTicks);
			ClassicAssert.AreEqual(0, result.RemainingTicks);
			ClassicAssert.IsTrue(result.Complete);
		}

		[Test]
		public void VatAccrual_BackwardClockMintsNothing()
		{
			KingdomVatAccrual result = KingdomLabRules.AccrueVat(1300L, 100L, 1200,
				CrewEffectiveness: 100, WearEffectiveness: 100, Settled: false, Cancelled: false);
			ClassicAssert.AreEqual(1300L, result.NextTick);
			ClassicAssert.AreEqual(1200, result.RemainingTicks);
			ClassicAssert.AreEqual(0, result.WorkedTicks);
			ClassicAssert.IsFalse(result.Complete);
			KingdomVatAccrual retry = KingdomLabRules.AccrueVat(result.NextTick, 1300L,
				result.RemainingTicks, CrewEffectiveness: 100, WearEffectiveness: 100,
				Settled: false, Cancelled: false);
			ClassicAssert.AreEqual(1300L, retry.NextTick);
			ClassicAssert.AreEqual(1200, retry.RemainingTicks);
			ClassicAssert.AreEqual(0, retry.WorkedTicks);
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
				ClassicAssert.IsNotEmpty(lines[i]);
				StringAssert.DoesNotContain("failed", lines[i].ToLowerInvariant());
				StringAssert.DoesNotContain("error", lines[i].ToLowerInvariant());
				StringAssert.DoesNotContain("invalid", lines[i].ToLowerInvariant());
			}
		}

		[Test]
		public void ProcedureJob_FundingReceiptIsExactIffEveryLaneIsExact()
		{
			foreach (bool water in new bool[] { false, true })
			{
				foreach (bool bits in new bool[] { false, true })
				{
					foreach (KingdomKeptSpendPhase kept in System.Enum.GetValues(typeof(KingdomKeptSpendPhase)))
					{
						KingdomLabJobPhase phase = KingdomLabRules.FundingPhase(water, bits, kept);
						bool exact = water && bits && kept == KingdomKeptSpendPhase.SpentExact;
						ClassicAssert.AreEqual(exact ? KingdomLabJobPhase.Working
							: KingdomLabJobPhase.FundingRecovery, phase,
							"water=" + water + " bits=" + bits + " kept=" + kept);
					}
				}
			}
		}

		[Test]
		public void ProcedureJob_OnlyWorkingPhaseAccrues()
		{
			foreach (KingdomLabJobPhase phase in System.Enum.GetValues(typeof(KingdomLabJobPhase)))
			{
				KingdomLabJobAccrual result = KingdomLabRules.AccrueJob(100L, 1300L, 1200,
					100, 100, phase);
				if (phase == KingdomLabJobPhase.Working)
				{
					ClassicAssert.AreEqual(KingdomLabJobPhase.Ready, result.Phase);
					ClassicAssert.AreEqual(0, result.RemainingTicks);
				}
				else
				{
					ClassicAssert.AreEqual(phase, result.Phase);
					ClassicAssert.AreEqual(1200, result.RemainingTicks);
					ClassicAssert.AreEqual(100L, result.NextTick);
				}
			}
		}

		[Test]
		public void ProcedureJob_UnstaffedBoundaryAndRetryMintNoWork()
		{
			KingdomLabJobAccrual idle = KingdomLabRules.AccrueJob(100L, 1300L, 2400,
				0, 100, KingdomLabJobPhase.Working);
			ClassicAssert.AreEqual(0, idle.WorkedTicks);
			ClassicAssert.AreEqual(2400, idle.RemainingTicks);
			KingdomLabJobAccrual retry = KingdomLabRules.AccrueJob(idle.NextTick, 1300L,
				idle.RemainingTicks, 100, 100, idle.Phase);
			ClassicAssert.AreEqual(0, retry.WorkedTicks);
			ClassicAssert.AreEqual(2400, retry.RemainingTicks);
		}

		[Test]
		public void ProcedureJob_OlderResumedBoundaryNeverRewindsCommissionClock()
		{
			KingdomLabJobAccrual result = KingdomLabRules.AccrueJob(2500L, 1300L, 2400,
				100, 100, KingdomLabJobPhase.Working);
			ClassicAssert.AreEqual(2500L, result.NextTick);
			ClassicAssert.AreEqual(2400, result.RemainingTicks);
			ClassicAssert.AreEqual(0, result.WorkedTicks);
			ClassicAssert.AreEqual(KingdomLabJobPhase.Working, result.Phase);
		}

		[Test]
		public void ProcedureJob_WaterClaimCreditsPartialOnceAndQuarantineIsSticky()
		{
			KingdomLabWaterClaim partial = KingdomLabRules.MergeWaterClaim(10, 0, 0,
				Quarantined: false, AttemptSpent: 4, AttemptLost: 4, AttemptExact: true);
			ClassicAssert.AreEqual(4, partial.Paid);
			ClassicAssert.AreEqual(6, partial.Outstanding);
			ClassicAssert.AreEqual(4, partial.Lost);
			ClassicAssert.IsFalse(partial.Quarantined);
			ClassicAssert.IsFalse(partial.Settled);

			KingdomLabWaterClaim settled = KingdomLabRules.MergeWaterClaim(10,
				partial.Paid, partial.Lost, partial.Quarantined,
				AttemptSpent: 6, AttemptLost: 6, AttemptExact: true);
			ClassicAssert.AreEqual(10, settled.Paid);
			ClassicAssert.AreEqual(0, settled.Outstanding);
			ClassicAssert.AreEqual(10, settled.Lost);
			ClassicAssert.IsTrue(settled.Settled);

			KingdomLabWaterClaim uncertain = KingdomLabRules.MergeWaterClaim(10, 4, 4,
				Quarantined: false, AttemptSpent: 1, AttemptLost: 1, AttemptExact: false);
			ClassicAssert.AreEqual(5, uncertain.Paid);
			ClassicAssert.AreEqual(5, uncertain.Outstanding);
			ClassicAssert.IsTrue(uncertain.Quarantined);
			ClassicAssert.IsFalse(uncertain.Settled);
			KingdomLabWaterClaim sticky = KingdomLabRules.MergeWaterClaim(10,
				uncertain.Paid, uncertain.Lost, uncertain.Quarantined,
				AttemptSpent: 5, AttemptLost: 5, AttemptExact: true);
			ClassicAssert.IsTrue(sticky.Quarantined);
			ClassicAssert.IsFalse(sticky.Settled);
		}

		[Test]
		public void ProcedureJob_WaterClaimSaturatesAndNeverOvercreditsPrice()
		{
			KingdomLabWaterClaim result = KingdomLabRules.MergeWaterClaim(7,
				int.MaxValue, int.MaxValue, Quarantined: false,
				AttemptSpent: int.MaxValue, AttemptLost: int.MaxValue, AttemptExact: true);
			ClassicAssert.AreEqual(7, result.Paid);
			ClassicAssert.AreEqual(0, result.Outstanding);
			ClassicAssert.AreEqual(int.MaxValue, result.Lost);
			ClassicAssert.IsTrue(result.Settled);
		}

		[Test]
		public void RemovalFunding_NeverTouchesBodyBeforeExactFullPayment()
		{
			ClassicAssert.AreEqual(KingdomLabRemovalPhase.FundingRecovery,
				KingdomLabRules.RemovalFundingPhase(10, 0, Quarantined: false));
			ClassicAssert.AreEqual(KingdomLabRemovalPhase.FundingRecovery,
				KingdomLabRules.RemovalFundingPhase(10, 9, Quarantined: false));
			ClassicAssert.AreEqual(KingdomLabRemovalPhase.Paid,
				KingdomLabRules.RemovalFundingPhase(10, 10, Quarantined: false));
			ClassicAssert.AreEqual(KingdomLabRemovalPhase.Paid,
				KingdomLabRules.RemovalFundingPhase(0, 0, Quarantined: false));
			ClassicAssert.AreEqual(KingdomLabRemovalPhase.Quarantined,
				KingdomLabRules.RemovalFundingPhase(10, 10, Quarantined: true));
		}

		[Test]
		public void RemovalObservation_AbsentCommitsPresentRetriesUncertainQuarantines()
		{
			ClassicAssert.AreEqual(KingdomLabRemovalPhase.Removed,
				KingdomLabRules.RemovalObservation(KingdomLabOwnedTargetState.Absent,
					RemovingStarted: true));
			ClassicAssert.AreEqual(KingdomLabRemovalPhase.RemovalRecovery,
				KingdomLabRules.RemovalObservation(KingdomLabOwnedTargetState.Present,
					RemovingStarted: true));
			ClassicAssert.AreEqual(KingdomLabRemovalPhase.Paid,
				KingdomLabRules.RemovalObservation(KingdomLabOwnedTargetState.Present,
					RemovingStarted: false));
			ClassicAssert.AreEqual(KingdomLabRemovalPhase.Quarantined,
				KingdomLabRules.RemovalObservation(KingdomLabOwnedTargetState.Uncertain,
					RemovingStarted: true));
		}

		[Test]
		public void RemovalWaterRetry_MergesOnlyOutstandingAndUncertaintyIsSticky()
		{
			KingdomLabWaterClaim first = KingdomLabRules.MergeWaterClaim(12, 0, 0,
				Quarantined: false, AttemptSpent: 5, AttemptLost: 5, AttemptExact: true);
			ClassicAssert.AreEqual(7, first.Outstanding);
			KingdomLabWaterClaim retry = KingdomLabRules.MergeWaterClaim(12, first.Paid,
				first.Lost, first.Quarantined, AttemptSpent: first.Outstanding,
				AttemptLost: first.Outstanding, AttemptExact: true);
			ClassicAssert.IsTrue(retry.Settled);
			ClassicAssert.AreEqual(12, retry.Paid);
			KingdomLabWaterClaim uncertain = KingdomLabRules.MergeWaterClaim(12,
				first.Paid, first.Lost, first.Quarantined, AttemptSpent: 0,
				AttemptLost: 0, AttemptExact: false);
			ClassicAssert.IsTrue(uncertain.Quarantined);
			ClassicAssert.IsFalse(uncertain.Settled);
		}

		[Test]
		public void MutationPresence_DistinguishesModifierOnlyFromListedContribution()
		{
			ClassicAssert.AreEqual(0, KingdomLabRules.MutationPresence(false, false));
			ClassicAssert.AreEqual(1, KingdomLabRules.MutationPresence(false, true));
			ClassicAssert.AreEqual(2, KingdomLabRules.MutationPresence(true, true));
			ClassicAssert.AreEqual(2, KingdomLabRules.MutationPresence(true, false));
			ClassicAssert.IsTrue(KingdomLabRules.ProcedureEffectChanged(0, 2, Removing: false));
			ClassicAssert.IsTrue(KingdomLabRules.ProcedureEffectChanged(2, 1, Removing: true));
		}

		[Test]
		public void ProcedureJob_ReloadEquivalentStepsMatchUninterruptedAccrual()
		{
			KingdomLabJobAccrual direct = KingdomLabRules.AccrueJob(100L, 2500L, 3600,
				100, 100, KingdomLabJobPhase.Working);
			KingdomLabJobAccrual first = KingdomLabRules.AccrueJob(100L, 1300L, 3600,
				100, 100, KingdomLabJobPhase.Working);
			KingdomLabJobAccrual reloaded = KingdomLabRules.AccrueJob(first.NextTick, 2500L,
				first.RemainingTicks, 100, 100, first.Phase);
			ClassicAssert.AreEqual(direct.RemainingTicks, reloaded.RemainingTicks);
			ClassicAssert.AreEqual(direct.Phase, reloaded.Phase);
			ClassicAssert.AreEqual(direct.NextTick, reloaded.NextTick);
		}

		[Test]
		public void ProcedureJob_ReadyRetryIsIdempotentAndDoesNotReopenLabor()
		{
			KingdomLabJobAccrual ready = KingdomLabRules.AccrueJob(2500L, 3700L, 0,
				100, 100, KingdomLabJobPhase.Ready);
			ClassicAssert.AreEqual(KingdomLabJobPhase.Ready, ready.Phase);
			ClassicAssert.AreEqual(0, ready.WorkedTicks);
			ClassicAssert.AreEqual(2500L, ready.NextTick);
			ClassicAssert.IsTrue(KingdomLabRules.IsLiveJob(ready.Phase));
			ClassicAssert.IsFalse(KingdomLabRules.IsLiveJob(KingdomLabJobPhase.Complete));
			ClassicAssert.IsFalse(KingdomLabRules.IsLiveJob(KingdomLabJobPhase.Cancelled));
		}

		[Test]
		public void EffectContract_FingerprintFreezesEveryExecutionAxisAndSourceStamp()
		{
			KingdomLabRegistryEntry row = RegistryRow("job");
			ClassicAssert.AreEqual("098548483c949f73",
				KingdomLabRules.ExecutionStampFingerprint("source-stamp"));
			ClassicAssert.AreEqual("d0036e413cbc5fd5", row.Fingerprint);
			ClassicAssert.IsTrue(KingdomLabRules.ValidEffectContract(row.ContractVersion,
				row.ProcedureKey, row.Grants, row.Source, row.Attach, row.Manager,
				row.Fingerprint, row.Detail));
			ClassicAssert.AreNotEqual(row.Fingerprint, KingdomLabRules.EffectFingerprint(
				row.ContractVersion, row.ProcedureKey, "OtherPart", row.Source, row.Attach,
				row.Manager, row.Detail));
			ClassicAssert.AreNotEqual(row.Fingerprint, KingdomLabRules.EffectFingerprint(
				row.ContractVersion, row.ProcedureKey, row.Grants, (int)LabSource.Mutation,
				row.Attach, row.Manager, row.Detail));
			ClassicAssert.AreNotEqual(row.Fingerprint, KingdomLabRules.EffectFingerprint(
				row.ContractVersion, row.ProcedureKey, row.Grants, row.Source,
				(int)LabAttach.Weapon, row.Manager, row.Detail));
			ClassicAssert.AreNotEqual(row.Fingerprint, KingdomLabRules.EffectFingerprint(
				row.ContractVersion, row.ProcedureKey, row.Grants, row.Source, row.Attach,
				"other-manager", row.Detail));
			ClassicAssert.AreNotEqual(row.Detail,
				"stamp:" + KingdomLabRules.ExecutionStampFingerprint("changed-stamp"));
			ClassicAssert.AreEqual(KingdomLabRules.ExecutionStampFingerprint("source-stamp"),
				KingdomLabRules.ExecutionStampFingerprint("source-stamp"));
		}

		[Test]
		public void EffectContract_RejectsLegacyOrUnboundedRowsRatherThanDerivingThem()
		{
			KingdomLabRegistryEntry row = RegistryRow("job");
			ClassicAssert.IsFalse(KingdomLabRules.ValidEffectContract(0, row.ProcedureKey,
				row.Grants, row.Source, row.Attach, row.Manager, row.Fingerprint, row.Detail));
			ClassicAssert.IsFalse(KingdomLabRules.ValidEffectContract(row.ContractVersion,
				row.ProcedureKey, row.Grants, row.Source, row.Attach, row.Manager,
				row.Fingerprint, new string('x', KingdomLabRules.MaxRegistryFieldChars + 1)));
			ClassicAssert.IsFalse(KingdomLabRules.ValidEffectContract(row.ContractVersion,
				row.ProcedureKey, row.Grants, row.Source, row.Attach, row.Manager,
				row.Fingerprint, null));
		}

		[Test]
		public void CanonicalRegistry_RoundTripsFrozenAuthorityExactlyAcrossReload()
		{
			KingdomLabRegistryEntry row = RegistryRow("job");
			string serialized = KingdomLabRules.FormatRegistry(
				new List<KingdomLabRegistryEntry> { row });
			ClassicAssert.AreEqual("v1\nam9i|aGFsbC0x|cGF0aWVudC0x|Z2FtZS0x|cmVhbG0tMQ==|44|1|"
				+ "c3BvcmVnaWxscw==|R2FzSW1tdW5pdHk=|0|0|VEFGOjpMYWI6OnNwb3JlZ2lsbHM=|"
				+ "c3RhbXA6MDk4NTQ4NDgzYzk0OWY3Mw==|ZDAwMzZlNDEzY2JjNWZkNQ==|0|1",
				serialized);
			bool quarantined;
			List<KingdomLabRegistryEntry> loaded = KingdomLabRules.ParseRegistry(serialized,
				out quarantined);
			ClassicAssert.IsFalse(quarantined);
			ClassicAssert.AreEqual(1, loaded.Count);
			ClassicAssert.IsTrue(KingdomLabRules.RegistryAuthority(loaded[0], row,
				RequireActive: true));
			ClassicAssert.AreEqual(row.Detail, loaded[0].Detail);
			ClassicAssert.AreEqual(row.Fingerprint, loaded[0].Fingerprint);
			ClassicAssert.AreEqual(serialized, KingdomLabRules.FormatRegistry(loaded),
				"the canonical registry wire must survive decode and re-encode byte-for-byte");
		}

		[Test]
		public void CanonicalRegistry_CopyCannotChangePatientHallRealmOrFrozenContract()
		{
			KingdomLabRegistryEntry row = RegistryRow("job");
			KingdomLabRegistryEntry changed = row.Copy();
			changed.PatientId = "successor";
			ClassicAssert.IsFalse(KingdomLabRules.RegistryAuthority(row, changed, false));
			changed = row.Copy();
			changed.BuildingId = "successor-hall";
			ClassicAssert.IsFalse(KingdomLabRules.RegistryAuthority(row, changed, false));
			changed = row.Copy();
			changed.RealmFoundedTick++;
			ClassicAssert.IsFalse(KingdomLabRules.RegistryAuthority(row, changed, false));
			changed = row.Copy();
			changed.Detail = "stamp:" + KingdomLabRules.ExecutionStampFingerprint("other");
			changed.Fingerprint = KingdomLabRules.EffectFingerprint(changed.ContractVersion,
				changed.ProcedureKey, changed.Grants, changed.Source, changed.Attach,
				changed.Manager, changed.Detail);
			ClassicAssert.IsFalse(KingdomLabRules.RegistryAuthority(row, changed, false));
		}

		[Test]
		public void CanonicalRegistry_NeverEvictsAReceiptFromTerminalLabelAlone()
		{
			List<KingdomLabRegistryEntry> rows = new List<KingdomLabRegistryEntry>();
			for (int i = 0; i < KingdomLabRules.MaxRegistryRows; i++)
			{
				ClassicAssert.IsTrue(KingdomLabRules.UpsertRegistry(rows,
					RegistryRow("active-" + i, i + 1L)));
			}
			ClassicAssert.IsFalse(KingdomLabRules.UpsertRegistry(rows, RegistryRow("overflow")));
			KingdomLabRegistryEntry terminal = rows[7].Copy();
			terminal.Status = KingdomLabRegistryStatus.Complete;
			terminal.UpdatedTick = 0L;
			ClassicAssert.IsTrue(KingdomLabRules.UpsertRegistry(rows, terminal));
			ClassicAssert.IsFalse(KingdomLabRules.UpsertRegistry(rows, RegistryRow("replacement", 99L)));
			ClassicAssert.AreEqual(KingdomLabRules.MaxRegistryRows, rows.Count);
			ClassicAssert.GreaterOrEqual(KingdomLabRules.IndexOfRegistry(rows, terminal.JobId), 0);
			ClassicAssert.AreEqual(-1, KingdomLabRules.IndexOfRegistry(rows, "replacement"));
			ClassicAssert.IsTrue(KingdomLabRules.RemoveRegistry(rows, terminal.JobId,
				KingdomLabRegistryStatus.Complete));
			ClassicAssert.IsTrue(KingdomLabRules.UpsertRegistry(rows, RegistryRow("replacement", 99L)));
		}

		[Test]
		public void CanonicalRegistry_OldShapeAndDuplicateIdsQuarantineFailClosed()
		{
			bool quarantined;
			CollectionAssert.IsEmpty(KingdomLabRules.ParseRegistry(
				"v1\nlegacy|row|without|frozen|contract", out quarantined));
			ClassicAssert.IsTrue(quarantined);
			KingdomLabRegistryEntry row = RegistryRow("copied-job");
			string one = KingdomLabRules.FormatRegistry(new List<KingdomLabRegistryEntry> { row });
			string duplicateLine = one.Substring(one.IndexOf('\n'));
			List<KingdomLabRegistryEntry> parsed = KingdomLabRules.ParseRegistry(
				one + duplicateLine, out quarantined);
			ClassicAssert.IsTrue(quarantined);
			ClassicAssert.AreEqual(1, parsed.Count);
		}

		[Test]
		public void VatOutputIdentity_NeverCreatesAfterAnExactIdWasFrozen()
		{
			ClassicAssert.AreEqual(KingdomVatOutputDecision.CreateAndFreeze,
				KingdomLabRules.VatOutputIdentity(false, false, false));
			ClassicAssert.AreEqual(KingdomVatOutputDecision.UseExact,
				KingdomLabRules.VatOutputIdentity(true, true, true));
			ClassicAssert.AreEqual(KingdomVatOutputDecision.QuarantineMissing,
				KingdomLabRules.VatOutputIdentity(true, false, false));
			ClassicAssert.AreEqual(KingdomVatOutputDecision.QuarantineMismatch,
				KingdomLabRules.VatOutputIdentity(true, true, false));
			ClassicAssert.AreNotEqual(KingdomLabRules.VatOutputFingerprint("job", "result", 2,
				"stamp", "source"), KingdomLabRules.VatOutputFingerprint("job", "result", 3,
				"stamp", "source"));
		}

		[Test]
		public void VatCallbacks_ResumeIntentByObservationAndNeverAuthorizeReplay()
		{
			ClassicAssert.AreEqual(KingdomVatOutputPhase.Added,
				KingdomLabRules.ResumeVatOutput(KingdomVatOutputPhase.AddIntent, true));
			ClassicAssert.AreEqual(KingdomVatOutputPhase.Quarantined,
				KingdomLabRules.ResumeVatOutput(KingdomVatOutputPhase.AddIntent, false));
			ClassicAssert.AreEqual(KingdomVatRawPhase.Destroyed,
				KingdomLabRules.ResumeVatRaw(KingdomVatRawPhase.DestroyIntent,
					ExactRawPresent: false, ExactOutputInVat: true));
			ClassicAssert.AreEqual(KingdomVatRawPhase.Quarantined,
				KingdomLabRules.ResumeVatRaw(KingdomVatRawPhase.DestroyIntent,
					ExactRawPresent: true, ExactOutputInVat: true));
			ClassicAssert.AreNotEqual(KingdomLabRules.VatRawFingerprint("job", "raw", "arm", 1,
				"stamp", "source"), KingdomLabRules.VatRawFingerprint("job", "raw", "arm", 2,
				"stamp", "source"));
		}

		[Test]
		public void StandingReceipt_UsesExactCasAndQuarantinesInterleaving()
		{
			ClassicAssert.AreEqual(70, KingdomLabRules.StandingAfter(100, -30));
			ClassicAssert.AreEqual(int.MaxValue, KingdomLabRules.StandingAfter(int.MaxValue, 10));
			ClassicAssert.AreEqual(KingdomLabStandingPhase.Bound,
				KingdomLabRules.ObserveStanding(KingdomLabStandingPhase.Bound, 100, 100, 70));
			ClassicAssert.AreEqual(KingdomLabStandingPhase.Quarantined,
				KingdomLabRules.ObserveStanding(KingdomLabStandingPhase.Bound, 99, 100, 70));
			ClassicAssert.AreEqual(KingdomLabStandingPhase.Applied,
				KingdomLabRules.ObserveStanding(KingdomLabStandingPhase.Intent, 70, 100, 70));
			ClassicAssert.AreEqual(KingdomLabStandingPhase.Quarantined,
				KingdomLabRules.ObserveStanding(KingdomLabStandingPhase.Intent, 100, 100, 70));
		}

		[Test]
		public void MessageIntent_ResumesAsLostAndEveryDispositionSettlesOnce()
		{
			ClassicAssert.AreEqual(KingdomLabMessagePhase.Lost,
				KingdomLabRules.ResumeMessage(KingdomLabMessagePhase.Intent));
			ClassicAssert.IsTrue(KingdomLabRules.MessageSettled(KingdomLabMessagePhase.Delivered));
			ClassicAssert.IsTrue(KingdomLabRules.MessageSettled(KingdomLabMessagePhase.Skipped));
			ClassicAssert.IsTrue(KingdomLabRules.MessageSettled(KingdomLabMessagePhase.Lost));
			ClassicAssert.IsFalse(KingdomLabRules.MessageSettled(KingdomLabMessagePhase.Pending));
		}

		[Test]
		public void ReplayProof_RemainsBoundedAndRemembersMoreThanSixtyFourCycles()
		{
			string proof = "";
			for (int i = 0; i < 96; i++)
			{
				string written;
				ClassicAssert.IsTrue(KingdomLabRules.AddReplayProof(proof, "apply:job-" + i,
					out written));
				proof = written;
			}
			ClassicAssert.Less(proof.Length, 800);
			for (int i = 0; i < 96; i++)
			{
				bool malformed;
				ClassicAssert.IsTrue(KingdomLabRules.ReplayContains(proof, "apply:job-" + i,
					out malformed));
				ClassicAssert.IsFalse(malformed);
			}
			bool bad;
			ClassicAssert.IsTrue(KingdomLabRules.ReplayContains("not-a-proof", "old-job", out bad));
			ClassicAssert.IsTrue(bad);
		}

		[Test]
		public void ReplayProof_CanonicalWireIsIdempotentForAnExistingIdentity()
		{
			string first;
			ClassicAssert.IsTrue(KingdomLabRules.AddReplayProof("", "apply:job-1", out first));
			StringAssert.StartsWith("v1|1|", first);
			string[] fields = first.Split('|');
			ClassicAssert.AreEqual(3, fields.Length);
			byte[] bits = Convert.FromBase64String(fields[2]);
			ClassicAssert.AreEqual(KingdomLabRules.ReplayProofBytes, bits.Length);
			ClassicAssert.AreEqual(32, bits[31]);
			ClassicAssert.AreEqual(128, bits[102]);
			ClassicAssert.AreEqual(64, bits[438]);
			ClassicAssert.AreEqual(16, bits[484]);
			int nonzero = 0;
			for (int i = 0; i < bits.Length; i++) if (bits[i] != 0) nonzero++;
			ClassicAssert.AreEqual(4, nonzero, "four salted replay bits define one stable identity");
			string repeated;
			ClassicAssert.IsTrue(KingdomLabRules.AddReplayProof(first, "apply:job-1", out repeated));
			ClassicAssert.AreEqual(first, repeated);
			bool malformed;
			ClassicAssert.IsTrue(KingdomLabRules.ReplayContains(repeated, "apply:job-1",
				out malformed));
			ClassicAssert.IsFalse(malformed);
		}

		[Test]
		public void RemovalPhase_AppendsCancellationWithoutReinterpretingOldReceipts()
		{
			ClassicAssert.AreEqual(0, (int)KingdomLabRemovalPhase.Funding);
			ClassicAssert.AreEqual(6, (int)KingdomLabRemovalPhase.Complete);
			ClassicAssert.AreEqual(7, (int)KingdomLabRemovalPhase.Quarantined);
			ClassicAssert.AreEqual(8, (int)KingdomLabRemovalPhase.Cancelled);
		}

		[Test]
		public void Named_IsTotalOverNothing()
		{
			ClassicAssert.AreEqual("the work", KingdomLabRules.Named(null));
			ClassicAssert.AreEqual("the work", KingdomLabRules.Named(""));
			ClassicAssert.AreEqual("Kavvat", KingdomLabRules.Named("  Kavvat  "));
		}
	}
}
#endif
