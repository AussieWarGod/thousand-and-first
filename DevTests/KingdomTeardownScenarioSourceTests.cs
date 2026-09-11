#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Behavioural-coverage-matrix scenario: building teardown
	/// (Harness/KingdomTeardownNativeProvider.cs, Harness/KingdomTeardownNativeChecks.cs).
	/// Game-coupled logic is source-pinned here; there is no pure predicate to value-test.
	/// <para>
	/// SOURCE PINS ONLY. These tests prove the fixture's call shape and exact salvage-rule
	/// computation are present in the file; they do NOT execute the scenario, do NOT prove
	/// either the "fire" or "larder" build or strike ever actually completes on real turns, and
	/// do NOT sign either case's negative path as observed -- that requires a real native run,
	/// which this pass does not perform. Status for this whole scenario is
	/// "implemented-unexecuted", never "covered" or "PASS", until a native evidence id exists.
	/// </para>
	/// </summary>
	public class KingdomTeardownScenarioSourceTests
	{
		private const string Provider = "Harness/KingdomTeardownNativeProvider.cs";
		private const string Checks = "Harness/KingdomTeardownNativeChecks.cs";
		private static string Read(string path) => TestMain.ReadRepositoryText(path);

		[Test]
		public void ProviderRegistersBothVerbsAndSealsAnExactScript()
		{
			string source = Read(Provider);
			Assert.That(source, Does.Contain("[KingdomScenarioVerbProvider]"));
			Assert.That(source, Does.Contain("\"teardown-setup\""));
			Assert.That(source, Does.Contain("\"teardown-check\""));
			Assert.That(source, Does.Contain("KingdomScenarioScript.TryRead(out script, out _)"));
		}

		[Test]
		public void SetupRunsTwoParallelCasesThroughRealProductionApis()
		{
			string source = Read(Checks);
			foreach (string token in new[]
			{
				"KingdomNativeCampFounding.Found(Game, Zone, Require)",
				"KingdomNativeCampFounding.Dedicate(Game, Zone, system,",
				"KingdomMaterials.DedicateStockpile(System, Zone, Chest, out failure)",
				"KingdomCommission.Commission(System, BuildKey, null,",
				"new Case(\"fire\", \"fire\", system, Zone, Game, Owned)",
				"new Case(\"larder\", \"larder\", system, Zone, Game, Owned)",
			}) Assert.That(source, Does.Contain(token), token);
			// The building object itself is never forced: no direct BuiltProperty/KingdomBuilt
			// write anywhere in this file.
			Assert.That(source, Does.Not.Contain("BuiltProperty"));
			Assert.That(source, Does.Not.Contain("\"KingdomBuilt\""));
		}

		[Test]
		public void CheckPollsRealBuiltStateBeforeOrderingTheRealStrike()
		{
			string source = Read(Checks);
			Assert.That(source, Does.Contain(
				"KingdomUpgrade.IsFunctionallyBuilt(works)"));
			Assert.That(source, Does.Contain(
				"KingdomMaterials.OrderStrike(System, Zone, Works, out string failure)"));
			// Never forces the transition: a not-yet-built poll must return without asserting.
			Assert.That(source, Does.Contain("awaiting-built=true"));
		}

		[Test]
		public void BothCasesComputeExactSalvageDeltaFromTheProductionRuleNeverAssumed()
		{
			string source = Read(Checks);
			Assert.That(source, Does.Contain("awaiting-struck=true"));
			// The expected delta is COMPUTED from the same production rule OrderStrike itself
			// uses, never hardcoded: KingdomMaterials.CostFor + KingdomMaterialRules.
			// StrikeSalvagePercent (Growth/KingdomMaterialRules.Clearance.cs:193,211-219),
			// mirroring Cost.Scaled's own integer-floor arithmetic
			// (Growth/KingdomMaterialTally.cs:101-111).
			Assert.That(source, Does.Contain(
				"KingdomMaterials.CostFor(BuildKey).Get(KingdomMaterial.Timber)"));
			Assert.That(source, Does.Contain("KingdomMaterialRules.StrikeSalvagePercent"));
			Assert.That(source, Does.Contain(
				"timberAfter - TimberBeforeStrike == ExpectedSalvageDelta"));
			Assert.That(source, Does.Contain("KingdomMaterials.RawCensusCountOf(item)"));
			// Never the ordinary, dispatching Count for the material-return proof.
			Assert.That(source, Does.Not.Contain("item.Count"));
			// The "fire" case is the explicit ZERO-SALVAGE BOUNDARY (1 timber cost floors to 0);
			// "larder" (3 timber cost) is the POSITIVE-SALVAGE case this fixture was missing
			// before -- neither is a bare hardcoded literal standing in for the computed rule.
			Assert.That(source, Does.Not.Contain("ExpectedSalvageDelta = 0"));
			Assert.That(source, Does.Not.Contain("ExpectedSalvageDelta = 1"));
		}

		[Test]
		public void NegativePathRefusesASecondStrikeOnTheAbsentBuildingForBothCases()
		{
			string source = Read(Checks);
			Assert.That(source, Does.Contain("secondOrder = KingdomMaterials.OrderStrike("));
			Assert.That(source, Does.Contain(
				"!secondOrder && !string.IsNullOrEmpty(secondFailure)"));
			Assert.That(source, Does.Contain(
				"a second strike order against the absent building was not refused"));
		}

		[Test]
		public void FrameOnlyCompletesOnceEveryCaseHasFinished()
		{
			string source = Read(Checks);
			Assert.That(source, Does.Contain("foreach (Case c in Cases) if (!c.Done) Done = false;"));
		}
	}
}
#endif
