#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Wiring tripwires for the economic stress fixture's food arm
	/// (issue #125/#127): food debt retires inert
	/// (<c>Simulation/City/KingdomCity.z05.Reify.cs:41-61</c>), so the fixture must prove
	/// CONSERVATION, never a delivery, and the demanded budget is 244 water containers only.
	/// Game-coupled logic (needs a real GameObject/Zone) is source-pinned here; the pure budget
	/// arithmetic is value-tested in <c>Tools/tests/persona_travel_test.py</c>.
	/// </summary>
	public class KingdomScenarioContainerStressSourceTests
	{
		private const string Main = "Harness/KingdomScenarioContainerStress.cs";
		private const string Food = "Harness/KingdomScenarioContainerStress.FoodConservation.cs";
		private static string Read(string path) => TestMain.ReadRepositoryText(path);

		[Test]
		public void BudgetIsDerivedFromWaterContainersOnlyNeverTheStaleEnvelopeTotal()
		{
			string source = Read(Main);
			Assert.That(source, Does.Contain("private const int ThirdsPerUnit = 3;"));
			Assert.That(source, Does.Contain("private const int InitialThirds = WaterCount * ThirdsPerUnit;"));
			Assert.That(source, Does.Contain("food=8; initial-thirds=\""));
			Assert.That(source, Does.Contain("+ InitialThirds + \"; synthetic=true"));
			Assert.That(source, Does.Contain("stress-initial-thirds=\" + InitialThirds"));
			// Never a bare literal claiming the old (wrong) 252*3=756 budget in either report string.
			Assert.That(source, Does.Not.Contain("initial-thirds=756"));
			Assert.That(source, Does.Not.Contain("stress-initial-thirds=756"));
		}

		[Test]
		public void FoodDebtMustRetireToZeroNeverBePaidOntoAContainer()
		{
			string source = Read(Main);
			Assert.That(source, Does.Contain("System.City.ZoneOwedFood[row] == 0"),
				"Check() must prove the legacy food debt retired inert, not that it was delivered");
			Assert.That(source, Does.Not.Contain("KingdomSurvey.HeldIn(item) == KingdomSurvey.CapacityOf(item), \"a larder did not receive its owed unit\""),
				"the old delivery-expectation assertion must be gone");
			Assert.That(source, Does.Contain("VerifyFoodConserved(zone, \"final\")"));
			Assert.That(source, Does.Contain("VerifyFoodConserved(zone, \"pre-resume\")"));
		}

		[Test]
		public void ConservationBindsIdentityHolderAndRawCountNeverTheOrdinaryCount()
		{
			string source = Read(Food);
			foreach (string token in new[] {
				"KingdomMaterials.RawCensusCountOf(item)",
				"item.InInventory == Larder && item.CurrentCell == null",
				"item.InInventory == larder && item.CurrentCell == null",
				"observed.Count == expected.Count",
				"observed.TryGetValue(row.Key, out int rawNow)",
				"rawNow == row.Value" })
				Assert.That(source, Does.Contain(token), token);
			// The proof must never be the ordinary, dispatching Count.
			Assert.That(source, Does.Not.Contain("item.Count"));
		}

		[Test]
		public void ConservationRefusesOnMintedDeletedOrRelocatedBodiesRatherThanGuessing()
		{
			string source = Read(Food);
			foreach (string token in new[] {
				"minted or deleted",
				"relocated or deleted",
				"raw count changed",
				"custody no longer names this exact larder" })
				Assert.That(source, Does.Contain(token), token);
		}

		[Test]
		public void PersonaDocDisclosesTheSyntheticFixtureAndZeroResidentStress()
		{
			string doc = Read("docs/BETA-ECONOMIC-TRAVEL.md");
			Assert.That(doc, Does.Contain("synthetic-fixture=true"));
			Assert.That(doc, Does.Contain("stress-residents=0"));
			Assert.That(doc, Does.Contain("ordinary-acceptance=false"));
		}
	}
}
#endif
