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
			Assert.That(source, Does.Not.Contain("private const int ThirdsPerUnit"));
			Assert.That(source, Does.Contain("private const int InitialThirds = WaterCount * KingdomCatchUpRules.ThirdsPerUnit;"));
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
		public void SetupTimeIdMintingAndOrdinaryHeldInAreDisclosedNotSilent()
		{
			string source = Read(Main);
			foreach (string token in new[] {
				"Setup only -- not a conservation observation",
				"item.ID</c> below intentionally MINTS",
				"the one legitimate assignment-time mint, distinct from every later",
				"observation, which reads <c>IDIfAssigned</c> and refuses rather than mints" })
				Assert.That(source, Does.Contain(token), token);
		}

		[Test]
		public void ConservationNeverMintsAnIdentityAndUsesTheRawSeamNeverTheOrdinaryCount()
		{
			string source = Read(Food);
			// IDIfAssigned everywhere an identity is read; the minting GameObject.ID getter
			// ([decompile] XRL/World/GameObject.cs:436-449) must never appear as an observation.
			foreach (string token in new[] { "item.IDIfAssigned", "Larder.IDIfAssigned" })
				Assert.That(source, Does.Contain(token), token);
			Assert.That(source, Does.Not.Contain("item.ID;"));
			Assert.That(source, Does.Not.Contain("item.ID "));
			Assert.That(source, Does.Not.Contain("item.ID)"));
			foreach (string token in new[] {
				"KingdomMaterials.RawCensusCountOf(item)",
				"item.Physics != null && ReferenceEquals(item.Physics.InInventory, Larder)",
				"item.Physics != null && ReferenceEquals(item.Physics.InInventory, larder)" })
				Assert.That(source, Does.Contain(token), token);
			// "raw > 0" must guard BOTH the bind-time and the re-verify-time raw read -- a
			// single-site regression (one guard dropped, the phrase still present at the other
			// call site) must still be caught, not hidden behind a bare substring-presence check.
			int rawPositiveGuards = System.Text.RegularExpressions.Regex.Matches(source,
				System.Text.RegularExpressions.Regex.Escape("raw > 0")).Count;
			Assert.That(rawPositiveGuards, Is.EqualTo(2),
				"expected exactly one 'raw > 0' guard in BindFoodBodies and one in VerifyFoodConserved");
			// The proof must never be the ordinary, dispatching Count or HeldIn.
			Assert.That(source, Does.Not.Contain("item.Count"));
			Assert.That(source, Does.Not.Contain("KingdomSurvey.HeldIn"));
		}

		[Test]
		public void ConservationRefusesRatherThanSkipsOrGuessesOnAnyUnknownCustody()
		{
			string source = Read(Food);
			foreach (string token in new[] {
				// No silent skip: every object in a larder is classified or refused by name.
				"an invalid object stands in the larder",
				"an unexpected non-food object stands in the larder: blueprint=",
				"an invalid object stands in a freshly populated larder",
				"an unexpected non-food object stands in a freshly populated larder: blueprint=",
				// Missing / extra bodies named individually, not a bare count mismatch.
				"an extra, unbound food body stands in the larder: id=",
				"a bound food body is missing -- relocated or deleted: id=",
				"a bound food body's blueprint changed: id=",
				"a bound food body's raw count changed: id=",
				// The larder's own ground (zone/cell) and un-held custody are re-proved.
				"the larder itself moved off its recorded ground",
				"the larder itself is now held rather than standing on its own ground",
				"the larder itself is held rather than standing on its own ground",
				"custody no longer names this exact larder" })
				Assert.That(source, Does.Contain(token), token);
			// No silent continue/skip anywhere in the classification loops.
			Assert.That(source, Does.Not.Contain("continue;"));
		}

		[Test]
		public void CrossUnloadHonestyIsDisclosedNotJustCoded()
		{
			string source = Read(Food);
			// No retained GameObject/Zone field crosses the away leg -- every lookup is fresh.
			Assert.That(source, Does.Contain("zone.FindObjectByID(larderId)"));
			Assert.That(source, Does.Not.Contain("private static GameObject"));
			Assert.That(source, Does.Not.Contain("private static Zone"));
			foreach (string token in new[] {
				"reference is retained across the", "carry CLR reference identity across a reload",
				"a same-id replacement during travel is ambiguous under this evidence",
				"not a proven pass" })
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
