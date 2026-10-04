#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Source and value contracts for the heart's fifth rung (issue #160). These prove what a pin
	/// can prove -- that the seam drives nothing, that the arcology's extra gates are actually
	/// asked, that the composite bill is priced as production prices it, and that the sealed
	/// five-rung script is a strict extension of the accepted four-rung one. They are NOT native
	/// evidence: the persona camp-heart-rung5-native-check owes a real run.
	/// </summary>
	public class KingdomCampHeartRung5NativeSourceTests
	{
		private const string Script = "Harness/KingdomCampHeartChainScript.cs";
		private const string Chain = "Harness/KingdomCampHeartChain.cs";
		private const string Capital = "Harness/KingdomCampHeartChainCapital.cs";
		private const string HighCraft = "Harness/KingdomCampHeartChainHighCraft.cs";
		private const string Rung5 = "Harness/KingdomCampHeartChainRung5.cs";
		private const string Payment = "Harness/KingdomCampHeartChainPayment.cs";
		private const string Support = "Harness/KingdomCampHeartChainSupport.cs";
		private const string Crown = "Harness/KingdomCampHeartChainCrown.cs";
		private const string Input = "Harness/KingdomCampHeartChainInput.cs";
		private const string Observation = "Harness/KingdomCampHeartChainObservation.cs";
		private const string Trace = "Harness/KingdomCampHeartChainTrace.cs";
		private const string Handover = "Harness/KingdomCampHeartChainHandoverOccupancy.cs";
		private const string Persona = "Tools/personas/camp-heart-rung5-native-check.persona";
		private const string ChainPersona = "Tools/personas/camp-heart-chain.persona";

		private static string Read(string path) => TestMain.ReadRepositoryText(path);

		private static string[] Steps(string persona)
		{
			foreach (string line in Read(persona).Split('\n'))
				if (line.StartsWith("SCRIPT="))
					return line.Substring("SCRIPT=".Length).Trim().Split(';');
			return new string[0];
		}

		[Test]
		public void FiveRungScriptIsAStrictExtensionOfTheAcceptedFourRungScript()
		{
			string[] four = Steps(ChainPersona), five = Steps(Persona);
			Assert.That(four, Is.Not.Empty);
			Assert.That(five.Length, Is.GreaterThan(four.Length));
			Assert.That(five.Take(four.Length), Is.EqualTo(four));
			Assert.That(KingdomCampHeartChainScript.Matches(four), Is.True);
			Assert.That(KingdomCampHeartChainScript.Matches(five), Is.False);
			Assert.That(KingdomCampHeartChainScript.MatchesRung5(five), Is.True);
			Assert.That(KingdomCampHeartChainScript.MatchesRung5(four), Is.False);
			Assert.That(KingdomCampHeartChainScript.SealedTargetRung(four), Is.EqualTo(4));
			Assert.That(KingdomCampHeartChainScript.SealedTargetRung(five), Is.EqualTo(5));
			Assert.That(KingdomCampHeartChainScript.SealedTargetRung(null), Is.EqualTo(0));
			// The four-rung form keeps admitting the camp prefix, and neither form is a save form.
			Assert.That(KingdomCampHeartScript.Matches(five), Is.True);
			Assert.That(KingdomCampHeartScript.Matches(five, true), Is.False);
		}

		[Test]
		public void EveryEditedOrMissingStepBreaksTheFiveRungSeal()
		{
			string[] five = Steps(Persona);
			for (int i = 0; i < five.Length; i++)
			{
				var changed = (string[])five.Clone();
				changed[i] += " ";
				Assert.That(KingdomCampHeartChainScript.MatchesRung5(changed), Is.False);
				Assert.That(KingdomCampHeartChainScript.SealedTargetRung(changed), Is.EqualTo(0));
				Assert.That(KingdomCampHeartChainScript.MatchesRung5(
					five.Where((_, at) => at != i).ToArray()), Is.False);
			}
			Assert.That(KingdomCampHeartChainScript.MatchesRung5(
				five.Concat(new[] { "camp-heart-save" }).ToArray()), Is.False);
		}

		[Test]
		public void TheFifthLegRequestsAFiniteDisclosedTurnBudget()
		{
			string[] five = Steps(Persona);
			var waits = five.Where(x => x.StartsWith("advance ")).Select(x => int.Parse(x.Substring(8)));
			Assert.That(waits.Sum(), Is.EqualTo(60000));
			Assert.That(waits.All(x => x <= 10000), Is.True);
			Assert.That(five.Count(x => x == "camp-heart-chain-capital"), Is.EqualTo(1));
			Assert.That(five.Count(x => x == "camp-heart-chain-supply"), Is.EqualTo(3));
		}

		[Test]
		public void TheCapitalSeedAsksEveryGateTheArcologyAddsAndWritesNoCrown()
		{
			string capital = Read(Capital), crown = Read(Crown);
			foreach (string read in new[] {
				"KingdomZoning.Learn(System, KingdomZoningRules.KindNode, ArclightNode)",
				"KingdomZoning.Tech(System) == TechLevel.Arclight",
				"KingdomFounding.ClaimZone(neighbour)",
				"KingdomFounding.ZonesAdjacent(Zone.ZoneID, neighbour.ZoneID)",
				"System.ClaimedZones.Count >= ArcologyZones",
				"KingdomCrown.Enabled",
				"KingdomCrown.CrownedOn(System, Zone.ZoneID)",
				"KingdomHostedArcology.CanReserveAt(System, Zone.ZoneID, out string failure)",
				"verdict.Verdict == ZoningVerdict.Permitted",
				"KingdomPlots.Stake(System, Ground, rect, Entry, Spec, grid," })
				Assert.That(capital, Does.Contain(read), read);
			// The crown is READ, never written, and the seam drives no improvement and no turns.
			foreach (string source in new[] { capital, crown })
				foreach (string forbidden in new[] { "RegisterStateKey", "KingdomCrown.TakeUp",
					"KingdomUpgrade.Begin", "KingdomArchitectureStamper.TryApplyUpgrade",
					"SetStringGameState", "KingdomHostedArcology.TryReserve" })
					Assert.That(source, Does.Not.Contain(forbidden), forbidden);
			Assert.That(capital, Does.Contain("the capital seed advanced the real clock"));
		}

		/// <summary>Review of #264: the crown resolves from the city book plus the ACTIVE zone's
		/// survey only, and the hall stands on a neighbour the seed never enters in zero turns, so
		/// the seed must make that zone's own production read before any crown gate - and the gate
		/// must ask the book before it asks whether the ground is crowned.</summary>
		[Test]
		public void TheCrownHallEntersTheBookByItsZonesOwnReadBeforeAnyCrownGate()
		{
			string capital = Read(Capital), crown = Read(Crown);
			int seed = capital.IndexOf("private void SeedChainCapital()", StringComparison.Ordinal);
			int last = -1;
			foreach (string step in new[] { "TeachArclightCraft();", "TeachArclightNode();",
				"ClaimChainTerritory();", "RaiseChainCrownHall();", "PublishChainCrownZone();",
				"ChainCapitalReport = DescribeChainCapital();", "RequireChainCapitalGates();" })
			{
				int at = capital.IndexOf(step, seed, StringComparison.Ordinal);
				Assert.That(at, Is.GreaterThan(last), step);
				last = at;
			}
			int publish = crown.IndexOf("private void PublishChainCrownZone()", StringComparison.Ordinal);
			last = publish;
			foreach (string step in new[] { "ChainCrownedBefore = KingdomCrown.CrownedOn(System, Zone.ZoneID);",
				"ChainCrownBookBefore = ChainCrownBookHolds();", "KingdomCity.OnSuspending(System, ground);",
				"KingdomCrown.ClearCache();", "ChainCrownBookAfter = ChainCrownBookHolds();" })
			{
				int at = crown.IndexOf(step, publish, StringComparison.Ordinal);
				Assert.That(at, Is.GreaterThan(last), step);
				last = at;
			}
			Assert.That(crown, Does.Contain("KingdomCampHeartChainRules.CrownBookHolds(book.WorkIds,"));
			Assert.That(crown, Does.Contain("!ReferenceEquals(ground, Zone)"));
			int gates = capital.IndexOf("private void RequireChainCapitalGates()", StringComparison.Ordinal);
			int book = capital.IndexOf("Require(ChainCrownBookAfter,", gates, StringComparison.Ordinal);
			Assert.That(book, Is.GreaterThan(gates));
			Assert.That(capital.IndexOf("Require(KingdomCrown.CrownedOn(System, Zone.ZoneID),", gates,
				StringComparison.Ordinal), Is.GreaterThan(book));
			foreach (string field in new[] { "\"; crowned-before=\"", "\"; book-before=\"", "\"; book-after=\"",
				"\"; synthetic-book-read=true; crown-cache-cleared=true\"" })
				Assert.That(capital, Does.Contain(field), field);
		}

		/// <summary>Review of #264: the five-rung form refuses an impossible territory at chain
		/// setup from what is known WITHOUT building a zone, never after the 1->4 prefix.</summary>
		[Test]
		public void TheFiveRungFormReadsItsTerritoryAtSetupWithoutBuildingAZone()
		{
			string crown = Read(Crown), chain = Read(Chain);
			int setup = chain.IndexOf("if (Verb == KingdomCampHeartChainScript.Setup)", StringComparison.Ordinal);
			int preflight = chain.IndexOf("if (ChainFinalRung == 5) PreflightChainTerritory();", setup,
				StringComparison.Ordinal);
			Assert.That(preflight, Is.GreaterThan(setup));
			Assert.That(chain.IndexOf("SeedChainSupport();", setup, StringComparison.Ordinal),
				Is.GreaterThan(preflight));
			foreach (string read in new[] { "Zone.GetZoneIDFromDirection(direction)",
				"KingdomFounding.ZonesAdjacent(Zone.ZoneID, id)", "System.FindNonSeatSettlementByZone(id)",
				"System.ExiledRealmHolds(id)", "The.ZoneManager.GetZoneProperty(id, \"faction\")",
				"taf-camp-rung5-territory-infeasible" })
				Assert.That(crown, Does.Contain(read), read);
			// GetZoneFromDirection builds the zone; the preflight must never call it.
			Assert.That(crown, Does.Not.Contain("GetZoneFromDirection("));
			Assert.That(crown, Does.Not.Contain("ClaimZone("));
		}

		/// <summary>Review of #264: a neighbour the city already holds is still crown-hall ground
		/// (after the seed's own claims, and without building any zone for siting alone), and the
		/// founder check names a lost cell instead of dereferencing it.</summary>
		[Test]
		public void AlreadyHeldNeighboursStaySitingGroundAndTheFounderCheckIsNullSafe()
		{
			string capital = Read(Capital);
			Assert.That(capital, Does.Contain(
				"{ ChainClaimNotes.Add(direction + \":already-held\"); ChainHeld.Add(neighbour); continue; }"));
			Assert.That(capital, Does.Contain("var grounds = new List<Zone>(ChainClaimed);"));
			Assert.That(capital, Does.Contain("grounds.AddRange(ChainHeld);"));
			Assert.That(capital, Does.Contain("foreach (Zone ground in grounds)"));
			Assert.That(capital, Does.Contain("(player.CurrentCell != null"));
			Assert.That(capital, Does.Contain("ReferenceEquals(The.ZoneManager?.ActiveZone, Zone) && player != null"));
		}

		[Test]
		public void TheArcologyBillIsPricedAsACompositeAndNothingBelowItIs()
		{
			string payment = Read(Payment);
			Assert.That(payment, Does.Contain("Target == 5 ? KingdomMaterials.BitCostFor(ChainTo) : null"));
			Assert.That(payment, Does.Contain("Target == 5 ? KingdomMaterials.ExoticCostFor(ChainTo) : null"));
			Assert.That(payment, Does.Contain("if (Target == 5) SupplyChainHighCraft();"));
			Assert.That(payment, Does.Contain("Target == 4 ? 121 : 133"));
			Assert.That(payment, Does.Contain("ChainWater = Target == 3 ? 28 : Target == 4 ? 50 : 94;"));
			Assert.That(payment, Does.Contain("ChainTarget == 4 ? \"512\" : \"1024\""));
		}

		/// <summary>Review of #264: the paid check compares the job against the ONE cost the supply
		/// step built - composite at the fifth rung - and never against the plain tally; whole bit
		/// bodies are judged by the fall in production's own bit tally. The predicates themselves
		/// execute in KingdomQuickstartBuildClaimsTests.</summary>
		[Test]
		public void ThePaidCheckJudgesTheOneCompositeCostTheSupplyStepBuilt()
		{
			string payment = Read(Payment), craft = Read(HighCraft);
			foreach (string pin in new[] {
				"ChainSupplyCost = new KingdomMaterialDebitCost(tally,",
				"ChainSupplyClaim = ChainSupplyCost.ToClaimString();",
				"return KingdomQuickstartBuildClaims.CleanFirstPayment(Claims, ChainWater, ChainSupplyCost);",
				"&& KingdomQuickstartBuildClaims.CleanFirstCompositePayment(Claims, ChainWater, ChainSupplyCost, lost);",
				"var after = KingdomMaterials.Stock(Zone).Bits;",
				"!ChainPaidExactly(found.Claims)" })
				Assert.That(payment, Does.Contain(pin), pin);
			Assert.That(payment, Does.Not.Contain("new KingdomMaterialDebitCost(KingdomMaterials.UpgradeCostFor(ChainFrom))"));
			int mint = craft.IndexOf("MintBits(bits);", StringComparison.Ordinal);
			int before = craft.IndexOf("ChainBitsBefore = stock.Bits.Copy();", StringComparison.Ordinal);
			Assert.That(before, Is.GreaterThan(mint));
			Assert.That(craft.IndexOf("\"camp-heart-chain-exotics\"", StringComparison.Ordinal),
				Is.GreaterThan(before));
		}

		/// <summary>Review of #264: the post-payment resident probe arms with the exact design the
		/// paid job's handover carries, so the arcology's renovation handover is watched as
		/// "arcology" and not as the great court. The keys execute in KingdomCampHeartChainRulesTests.</summary>
		[Test]
		public void TheHandoverProbeArmsWithThePaidRungsOwnSuccessorKey()
		{
			string payment = Read(Payment), probe = Read(Handover);
			Assert.That(payment, Does.Contain("ChainFrom = KingdomCampHeartChainRules.PredecessorKey(Target);"));
			Assert.That(payment, Does.Contain("ChainTo = KingdomCampHeartChainRules.SuccessorKey(Target);"));
			Assert.That(payment, Does.Contain(
				"KingdomCampHeartChainHandoverOccupancy.Arm(FixtureResidents[0], ChainJobId, ChainTo, ChainTarget >= 4);"));
			Assert.That(probe, Does.Contain("Key == Successor"));
			Assert.That(probe, Does.Contain("Successor = SuccessorKey;"));
			foreach (string literal in new[] { "\"heartmoot\"", "\"heartcourt\"", "\"arcology\"" })
				Assert.That(probe, Does.Not.Contain(literal), literal);
			Assert.That(KingdomCampHeartChainRules.SuccessorKey(5), Is.EqualTo("arcology"));
		}

		[Test]
		public void HighCraftIsClassifiedByProductionAndNeverByAHandWrittenTable()
		{
			string craft = Read(HighCraft);
			foreach (string reader in new[] {
				"KingdomMaterials.TryExoticOf(unit, out var read)",
				"KingdomMaterials.UnitBits(unit)",
				"KingdomMaterialRules.CoversExotics(stock.Exotics, exotics)",
				"KingdomMaterialRules.CoversBits(stock.Bits, bits)",
				"XRL.World.Parts.TinkerItem.GetBitCostFor(pair.Key)",
				"taf-camp-rung5-bits-unbounded" })
				Assert.That(craft, Does.Contain(reader), reader);
			Assert.That(craft, Does.Not.Contain("SetIntProperty(\"KingdomStockpile\""));
		}

		[Test]
		public void TheStandingCheckProvesARenovationAndARetiredCourt()
		{
			string rung5 = Read(Rung5);
			foreach (string proof in new[] {
				"KingdomPlots.HeartRung(Zone) == 5",
				"KingdomUpgrade.DesignKeyOf(standing) == KingdomHostedArcology.ArcologyKey",
				"taf-camp-rung5-footprint-grew",
				"taf-camp-rung5-lot-grew",
				"KingdomArchitectureStamper.TryVerifyComplete(standing, Zone,",
				"XRL.World.Parts.r_KingdomScaffold.HasRemovalProof(Standing, job.SubjectId)",
				"KingdomPlots.TryChainedWorkSuccessor(Zone,",
				"KingdomConstruction.FindGlobalLiveId(ChainHeartId, out var court)",
				"row.Phase == KingdomHostedAuthorityPhase.Active",
				"taf-camp-rung5-hosted-lot-started" })
				Assert.That(rung5, Does.Contain(proof), proof);
			// Absence is consulted LAST: a court merely gone must still refuse as unproved.
			Assert.That(rung5.IndexOf("HasRemovalProof"),
				Is.LessThan(rung5.IndexOf("FindGlobalLiveId")));
			Assert.That(rung5.IndexOf("TryChainedWorkSuccessor"),
				Is.LessThan(rung5.IndexOf("FindGlobalLiveId")));
			// The renovate delta strikes the rostrum, the benches and both founder statues by
			// design, so no rung-4 fixture may be asserted by identity except the two protected
			// placements the tier keeps.
			foreach (string stale in new[] { "r_KingdomGreatCourtRostrum", "r_KingdomFounderStatue",
				"r_KingdomCivicTorchpost", "r_KingdomFixtureBenchTimber" })
				Assert.That(rung5, Does.Not.Contain(stale), stale);
		}

		/// <summary>Review of #264: every sealed form's setup seeds foundry exactly as the accepted
		/// four-rung run did, so the 1->2->3->4 prefix keeps its inputs, and only the capital seed
		/// raises the craft to arclight, after rung four.</summary>
		[Test]
		public void ThePrefixSeedsFoundryAndOnlyTheCapitalSeedRaisesArclight()
		{
			string support = Read(Support), crown = Read(Crown), chain = Read(Chain);
			Assert.That(support, Does.Contain(
				"for (int i = 0; i < KingdomZoningRules.PointsForLevel(TechLevel.Foundry); i++)"));
			Assert.That(support, Does.Contain(
				"Require(KingdomZoning.Tech(System) == TechLevel.Foundry, \"synthetic lessons did not reach foundry craft\");"));
			Assert.That(support, Does.Not.Contain("Arclight"));
			Assert.That(support, Does.Not.Contain("ChainFinalRung"));
			Assert.That(crown, Does.Contain("for (int i = KingdomZoningRules.PointsForLevel(TechLevel.Foundry);"));
			Assert.That(crown, Does.Contain("i < KingdomZoningRules.PointsForLevel(TechLevel.Arclight); i++)"));
			foreach (string source in new[] { support, crown })
				Assert.That(source, Does.Contain("KingdomZoning.Learn(System, \"disk\", \"paid-heart-chain-fixture-\" + i)"));
			Assert.That(chain, Does.Contain("\"; craft=\" + KingdomZoning.Tech(System)"));
			Assert.That(chain, Does.Contain(
				"ChainFinalRung = KingdomCampHeartChainScript.SealedTargetRung(sealedScript);"));
			Assert.That(chain, Does.Contain("Require(ChainPhase == 8 && ChainFinalRung == 5,"));
			Assert.That(chain, Does.Contain("Require(ChainPhase == 12 && ChainFinalRung == 5,"));
			// The four-rung persona's terminal line is unchanged.
			Assert.That(chain, Does.Contain("\"paid-heart-chain complete; paid-rungs=1->2->3->4;"));
			Assert.That(Read(Payment), Does.Contain(
				"Require(Target <= ChainFinalRung, \"the sealed script does not drive this target rung\");"));
		}

		/// <summary>The lessons' arithmetic, executed: nine setup lessons are foundry exactly, all
		/// five capital lessons are needed for arclight, and the node key adds no craft.</summary>
		[Test]
		public void NineLessonsAreFoundryAndFourteenAreArclightWhileTheNodeAddsNothing()
		{
			var roster = new List<string>();
			int foundry = KingdomZoningRules.PointsForLevel(TechLevel.Foundry);
			int arclight = KingdomZoningRules.PointsForLevel(TechLevel.Arclight);
			for (int i = 0; i < foundry; i++)
				roster.Add(KingdomZoningRules.ComposeKey("disk", "paid-heart-chain-fixture-" + i));
			Assert.That(KingdomZoningRules.LevelForPoints(KingdomZoningRules.TechPoints(roster)),
				Is.EqualTo(TechLevel.Foundry));
			for (int i = foundry; i < arclight; i++)
			{
				Assert.That(KingdomZoningRules.LevelForPoints(KingdomZoningRules.TechPoints(roster)),
					Is.EqualTo(TechLevel.Foundry), "lesson " + i);
				roster.Add(KingdomZoningRules.ComposeKey("disk", "paid-heart-chain-fixture-" + i));
			}
			int points = KingdomZoningRules.TechPoints(roster);
			Assert.That(KingdomZoningRules.LevelForPoints(points), Is.EqualTo(TechLevel.Arclight));
			// The capital seed's node key (Harness/KingdomCampHeartChainCapital.cs ArclightNode).
			roster.Add(KingdomZoningRules.ComposeKey(KingdomZoningRules.KindNode, "arclight"));
			Assert.That(KingdomZoningRules.TechPoints(roster), Is.EqualTo(points));
		}

		/// <summary>Review of #264: three gates keyed on the four-rung form switched the paid
		/// chain's input isolation, its journal row, its trace and the founder's commission walk
		/// OFF for the sealed five-rung script. No harness shard but the script's own may ask the
		/// four-rung form; every chain consumer asks the sealed rung.</summary>
		[Test]
		public void NoHarnessShardSwitchesPaidChainBehaviourOnTheFourRungFormAlone()
		{
			var offenders = new List<string>();
			int scanned = 0;
			foreach (string path in Directory.EnumerateFiles(Path.Combine(TestMain.RepositoryRoot, "Harness"),
				"*.cs", SearchOption.AllDirectories))
			{
				scanned++;
				if (Path.GetFileName(path) == "KingdomCampHeartChainScript.cs") continue;
				if (File.ReadAllText(path).Contains("KingdomCampHeartChainScript.Matches("))
					offenders.Add(Path.GetFileName(path));
			}
			Assert.That(scanned, Is.GreaterThan(100), "the harness tree was not found");
			Assert.That(offenders, Is.Empty, "four-rung-only chain gates: " + string.Join(", ", offenders));
			Assert.That(Read(Input), Does.Contain(
				"&& Harness.KingdomCampHeartChainScript.SealedTargetRung(script) > 0;"));
			Assert.That(Read(Input), Does.Contain(
				"if (Harness.KingdomCampHeartChainScript.SealedTargetRung(Verbs) == 0) return true;"));
			Assert.That(Read(Observation), Does.Contain(
				"ChainCommission = KingdomCampHeartChainScript.SealedTargetRung(script) > 0;"));
			// The trace, the retry fault and the handover probe are all switched by input ownership.
			Assert.That(Read(Trace), Does.Contain("KingdomScenarioAutoRunner.ChainInputOwner() != null"));
			Assert.That(KingdomCampHeartChainScript.SealedTargetRung(Steps(Persona)), Is.EqualTo(5));
		}

		[Test]
		public void ThePersonaDisclosesItsSyntheticSeedAndItsOwedRun()
		{
			string persona = Read(Persona);
			foreach (string line in new[] { "node:arclight", "FOUR claimed zones", "THE CROWN",
				"COMPOSITE bill", "SAME-FOOTPRINT RENOVATION", "BUDGET, DISCLOSED AND MEASURED",
				"no save/load", "# Physical input isolated for this exact dedicated game, through owned shutdown.",
				"KingdomCity.OnSuspending", "five more disk lessons", "WITHOUT building any zone" })
				Assert.That(persona, Does.Contain(line), line);
			Assert.That(persona, Does.Contain("LOG_FORBID=[\"construction: founding heart recovery requires inspection\""));
			Assert.That(persona, Does.Contain("TIMEOUT=7000"));
		}
	}
}
#endif
