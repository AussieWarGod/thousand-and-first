#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Behavioural-coverage-matrix scenario: building teardown
	/// (Harness/KingdomTeardownNativeProvider.cs, Harness/KingdomTeardownNativeChecks.cs).
	/// Game-coupled logic is source-pinned here; there is no pure predicate to value-test.
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
		public void SetupFoundsDedicatesAndCommissionsThroughRealProductionApis()
		{
			string source = Read(Checks);
			foreach (string token in new[]
			{
				"KingdomNativeCampFounding.Found(Game, Zone, Require)",
				"KingdomNativeCampFounding.Dedicate(Game, Zone, System,",
				"KingdomMaterials.DedicateStockpile(System, Zone, chest, out failure)",
				"KingdomCommission.Commission(System, BuildKey, null,",
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
		public void RemovalAndMaterialReturnAreObservedNeverAssumed()
		{
			string source = Read(Checks);
			Assert.That(source, Does.Contain("awaiting-struck=true"));
			Assert.That(source, Does.Contain(
				"struck building returned no material to the dedicated store"));
			Assert.That(source, Does.Contain("KingdomMaterials.RawCensusCountOf(item)"));
			// Never the ordinary, dispatching Count for the material-return proof.
			Assert.That(source, Does.Not.Contain("item.Count"));
		}

		[Test]
		public void NegativePathRefusesASecondStrikeOnTheAbsentBuilding()
		{
			string source = Read(Checks);
			Assert.That(source, Does.Contain("secondOrder = KingdomMaterials.OrderStrike("));
			Assert.That(source, Does.Contain(
				"!secondOrder && !string.IsNullOrEmpty(secondFailure)"));
			Assert.That(source, Does.Contain(
				"a second strike order against the absent building was not refused"));
		}
	}
}
#endif
