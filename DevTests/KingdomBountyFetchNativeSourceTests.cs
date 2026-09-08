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
				"SCRIPT=stagedigest;bounty-fetch-setup;advance 2400;bounty-fetch-check;advance 1200;bounty-fetch-revisit;stagedigest",
				"VERBS=bounty-fetch-setup,bounty-fetch-check,bounty-fetch-revisit",
				"EXPECT=stagedigest:OK~founded=false,bounty-fetch-setup:OK~native-bounty-fetch phase=0,advance:OK,bounty-fetch-check:OK~native-bounty-fetch cases=8 passed=8 failed=0,advance:OK,bounty-fetch-revisit:OK~native-bounty-fetch cases=4 passed=4 failed=0,stagedigest:OK~founded=true,COMPLETE" })
				ClassicAssert.AreEqual(1, Regex.Matches(persona, "(?m)^" + Regex.Escape(row) + "$").Count, row);
		}

		[Test]
		public void ProviderRefusesAnyHaulHookAndSealsItsOwnScript()
		{
			string source = Read(Provider);
			foreach (string token in new[] { "KingdomBounty.HaulHook == null", "script.Count == Script.Length",
				"script[i] == Script[i]", "HasAnyState(game, Receipt)", "HasQuickstartState(game)",
				"KingdomScenarioTransactionShape.None",
				"KingdomScenarioDurableState.ProvesExactText(Receipt, result)" })
				StringAssert.Contains(token, source);
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
				"data.TransferPhase == 0 && data.TransferredUnits == 0" })
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

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
	}
}
#endif
