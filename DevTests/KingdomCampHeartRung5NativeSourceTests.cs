#if TAF_TESTS
using System.Collections.Generic;
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
			string capital = Read(Capital);
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
			foreach (string forbidden in new[] { "RegisterStateKey", "KingdomCrown.TakeUp",
				"KingdomUpgrade.Begin", "KingdomArchitectureStamper.TryApplyUpgrade",
				"SetStringGameState", "KingdomHostedArcology.TryReserve" })
				Assert.That(capital, Does.Not.Contain(forbidden), forbidden);
			Assert.That(capital, Does.Contain("the capital seed advanced the real clock"));
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

		[Test]
		public void TheCraftSeedAndStepMachineAreDrivenByTheSealedRungAlone()
		{
			Assert.That(Read(Support), Does.Contain(
				"TechLevel craft = ChainFinalRung == 5 ? TechLevel.Arclight : TechLevel.Foundry;"));
			string chain = Read(Chain);
			Assert.That(chain, Does.Contain(
				"ChainFinalRung = KingdomCampHeartChainScript.SealedTargetRung(sealedScript);"));
			Assert.That(chain, Does.Contain("Require(ChainPhase == 8 && ChainFinalRung == 5,"));
			Assert.That(chain, Does.Contain("Require(ChainPhase == 12 && ChainFinalRung == 5,"));
			// The four-rung persona's terminal line is unchanged.
			Assert.That(chain, Does.Contain("\"paid-heart-chain complete; paid-rungs=1->2->3->4;"));
			Assert.That(Read(Payment), Does.Contain(
				"Require(Target <= ChainFinalRung, \"the sealed script does not drive this target rung\");"));
		}

		[Test]
		public void ThePersonaDisclosesItsSyntheticSeedAndItsOwedRun()
		{
			string persona = Read(Persona);
			foreach (string line in new[] { "node:arclight", "FOUR claimed zones", "THE CROWN",
				"COMPOSITE bill", "SAME-FOOTPRINT RENOVATION", "BUDGET, DISCLOSED AND AT RISK",
				"no save/load" })
				Assert.That(persona, Does.Contain(line), line);
			Assert.That(persona, Does.Contain("LOG_FORBID=[\"construction: founding heart recovery requires inspection\""));
			Assert.That(persona, Does.Contain("TIMEOUT=3600"));
		}
	}
}
#endif
