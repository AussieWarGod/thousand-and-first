#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source wiring only; the sealed real-engine run owns behavioral evidence.</summary>
	[TestFixture]
	public sealed class KingdomBountyFetchNativeSourceTests
	{
		private const string Provider = "Harness/KingdomBountyFetchNativeProvider.cs";
		private const string Fixture = "Harness/KingdomBountyFetchNativeFixture.cs";
		private const string Checks = "Harness/KingdomBountyFetchNativeChecks.cs";
		private const string Persona = "Tools/personas/bounty-fetch-native-check.persona";

		[Test]
		public void PersonaSealsTheExactSetupCarryAndRevisitScript()
		{
			string persona = Read(Persona);
			foreach (string row in new[] { "REQUEST=founding-first-city", "START=8.22@40,12",
				"SCRIPT=stagedigest;bounty-fetch-setup;advance 7200;bounty-fetch-check;advance 1200;bounty-fetch-revisit;stagedigest",
				"VERBS=bounty-fetch-setup,bounty-fetch-check,bounty-fetch-revisit",
				"EXPECT=stagedigest:OK~founded=false,bounty-fetch-setup:OK~native-bounty-fetch phase=0,advance:OK,bounty-fetch-check:OK~native-bounty-fetch cases=13 passed=13 failed=0,advance:OK,bounty-fetch-revisit:OK~native-bounty-fetch cases=6 passed=6 failed=0,stagedigest:OK~founded=true,COMPLETE" })
				ClassicAssert.AreEqual(1, Regex.Matches(persona, "(?m)^" + Regex.Escape(row) + "$").Count, row);
			// The provider enforces this same sealed sequence itself, so the two cannot drift:
			// setup, the due-time carry pass, the check, the revisit pass, then the revisit check.
			string provider = Read(Provider);
			foreach (string token in new[] { "{ \"stagedigest\", SetupVerb, \"advance 7200\",",
				"CheckVerb, \"advance 1200\", RevisitVerb, \"stagedigest\" };",
				"script.Count == Script.Length", "script[i] == Script[i]" })
				StringAssert.Contains(token, provider);
		}

		[Test]
		public void ProviderRefusesAnyHaulHookAndSealsItsOwnScript()
		{
			string source = Read(Provider);
			foreach (string token in new[] { "KingdomBounty.HaulHook == null", "script.Count == Script.Length",
				"script[i] == Script[i]", "HasAnyState(game, Receipt)", "HasQuickstartState(game)",
				"KingdomScenarioTransactionShape.None",
				"KingdomScenarioDurableState.ProvesExactText(Receipt, expected)",
				"KingdomScenarioDurableState.ProvesExactText(Receipt, written)" })
				StringAssert.Contains(token, source);
		}

		/// <summary>Each verb requires the phase its predecessor wrote and writes its own, so the
		/// sealed setup/check/revisit sequence can actually be walked end to end.</summary>
		[Test]
		public void ProviderRequiresAnExactPredecessorPhaseAndNeverOverwritesTheIntentWithItsReport()
		{
			string source = Read(Provider);
			foreach (string token in new[] { "IntentPhase = \"intent\", CarriedPhase = \"carried\"",
				"RevisitedPhase = \"revisited\"",
				"game.SetStringGameState(Receipt, IntentPhase)",
				"string expected = Required(verb)", "string written = Written(verb)",
				"game.SetStringGameState(Receipt, written)",
				"return (verb == RevisitVerb) ? CarriedPhase : IntentPhase;",
				"return (verb == CheckVerb) ? CarriedPhase : RevisitedPhase;" })
				StringAssert.Contains(token, source);
			StringAssert.DoesNotContain("SetStringGameState(Receipt, result)", source);
		}

		[Test]
		public void FixtureStakesOnlyThePostingFieldsAndNeverAWorkerCreditOrTransferPhase()
		{
			string source = Read(Fixture);
			foreach (string token in new[] { "data.PostPhase = (int)BountyPostPhase.Bound",
				"Pile.SetStringProperty(KingdomBounty.FetchMarkProperty, Notice.ID)",
				"KingdomMaterials.IsStockpile(Destination) && !KingdomMaterials.IsStockpile(Pile)",
				"KingdomMaterials.TryOrdinaryMaterialOf(Loads[i], out _)",
				"!KingdomMaterials.TryOrdinaryMaterialOf(Sentinels[i], out _)",
				"data.TransferPhase == 0 && data.TransferredUnits == 0",
				"Store.SetIntProperty(\"KingdomStores\", 1)",
				"KingdomLiquids.Fill(StoreWater, \"water\", StoredDrams) == StoredDrams",
				"Price = KingdomBountyRules.MaxPrice" })
				StringAssert.Contains(token, source);
			foreach (string token in new[] { "data.WorkerName = ", "data.TakenTick = ", "data.DueTick = ",
				"data.TransferPhase = ", "data.TransferredUnits = ", "data.Done = ", "data.Paid = " })
				StringAssert.DoesNotContain(token, source);
		}

		[Test]
		public void ObserversRetainProductionControlFlowAndProveNoRepeatCreditOnRevisit()
		{
			string provider = Read(Provider), checks = Read(Checks), all = provider + checks;
			foreach (string token in new[] { "\"OnSettlementPass\"", "\"ContinueTransfer\"", "\"Finish\"",
				"internal static void Prefix", "internal static void Postfix", "bool __result" })
				StringAssert.Contains(token, all);
			foreach (string token in new[] { "static bool Prefix", "ref bool __result", "__result = ",
				"KingdomBounty.OnSettlementPass(", "Data.TransferPhase = ", "Data.TransferredUnits = " })
				StringAssert.DoesNotContain(token, all);
			foreach (string token in new[] { "Transfers == CheckedTransfers", "Finishes == CheckedFinishes",
				"destination-unmoved", "sentinels-unmoved", "credited-sum-exact",
				"source-subtracted-to-its-sentinels", "destination-holds-the-exact-loads" })
				StringAssert.Contains(token, checks);
		}

		/// <summary>Every observation is bound to this fixture's own game, realm, ground and data
		/// part, and payment is judged only after the shipped completion has returned.</summary>
		[Test]
		public void ObserversBindTheExactIdentitiesAndProveTerminalPaymentAndCredit()
		{
			string checks = Read(Checks);
			foreach (string token in new[] { "ReferenceEquals(data, Fixture.Data)",
				"ReferenceEquals(The.Game, Fixture.Game)", "ReferenceEquals(system, Fixture.System)",
				"ReferenceEquals(zone, Fixture.Zone)", "if (!Bound(data)) return;",
				"internal static void ObserveFinished(r_KingdomNotice data)",
				"[HarmonyPostfix] internal static void Postfix(r_KingdomNotice Data)",
				"[HarmonyPatch(typeof(KingdomBounty), \"ContinuePayment\")]",
				"accepted-worker-is-on-the-roll", "carried-after-its-own-due-tick",
				"paid-the-exact-price-and-completed", "stores-debited-the-exact-price",
				"not-quarantined-after-payment", "credit-and-payment-unmoved",
				"still-retired-and-unquarantined",
				"StoredBeforePay - StoredAfterPay == Fixture.Price" })
				StringAssert.Contains(token, checks);
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
	}
}
#endif
