#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Wiring tripwires only; the persona supplies actual native forage evidence.</summary>
	public class KingdomForageNativeSourceTests
	{
		private const string Provider = "Harness/KingdomForageNativeProvider.cs";
		private const string Checks = "Harness/KingdomForageNativeChecks.cs";
		private const string Fixture = "Harness/KingdomForageNativeFixture.cs";
		private const string Phases = "Harness/KingdomForageNativeChecksPhases.cs";
		private static string Read(string path) => TestMain.ReadRepositoryText(path);

		[Test]
		public void ProviderIsRegisteredWithBothVerbsAndTheSealedScript()
		{
			string provider = Read(Provider);
			Assert.That(provider, Does.Contain("[KingdomScenarioVerbProvider]"));
			Assert.That(provider, Does.Contain("internal const string SetupVerb = \"forage-setup\";"));
			Assert.That(provider, Does.Contain("internal const string CheckVerb = \"forage-check\";"));
			Assert.That(provider, Does.Contain(
				"KingdomForageNativeChecks.Run(Verb, game, zone, out complete)"));
			Assert.That(provider, Does.Not.Contain("WorkForage("));
		}

		[Test]
		public void SetupFoundsTheCampAndPlantsOneObjectPerExclusionCategory()
		{
			string checks = Read(Checks), fixture = Read(Fixture);
			foreach (string token in new[] { "KingdomNativeCampFounding.Found(Game, Zone, Require)",
				"EnrollFour()", "System.Population == 4", "KingdomPlots.TryRiteGround(Zone",
				"stock.Stockpiles.Count > 0", "FindCanvas()" })
				Assert.That(checks, Does.Contain(token), token);
			foreach (string token in new[] { "Plant(\"Plant\", NearRite(1))", "Plant(\"Tree\", NearRite(4))",
				"Owned.GetPart<Physics>().Owner = ", "Plant(\"Yuckwheat\", NearRite(6))",
				"Food.HasPart(\"Harvestable\")", "Protected.SetIntProperty(\"KingdomStores\", 1)",
				"PlotPlant = Plant(\"Plant\", InHeartRect())", "GetTag(\"BodyType\") == \"ClothWall\"" })
				Assert.That(fixture, Does.Contain(token), token);
		}

		[Test]
		public void PhasesNeverCallTheDutyDirectlyAndProveRemovalStockAndExclusions()
		{
			string phases = Read(Phases);
			Assert.That(phases, Does.Not.Contain("WorkForage("));
			foreach (string token in new[] { "!GameObject.Validate(Eligible[i])", "RequireUntouched(Tree",
				"RequireUntouched(Owned", "RequireUntouched(Food", "RequireUntouched(Protected",
				"RequireUntouched(PlotPlant", "RequireUntouched(Canvas",
				"held == BrushBefore + Eligible.Length" })
				Assert.That(phases, Does.Contain(token), token);
		}

		[Test]
		public void ExhaustionRearmsAndTheCeilingCountsReservedUnitsNotJustTally()
		{
			string phases = Read(Phases);
			Assert.That(phases.IndexOf("state.NoBrushAnnounced", StringComparison.Ordinal),
				Is.LessThan(phases.IndexOf("ExtraPlant = Plant(", StringComparison.Ordinal)));
			Assert.That(phases, Does.Contain("!state.NoBrushAnnounced"));
			Assert.That(phases, Does.Contain(
				"brush.SetStringProperty(KingdomConstruction.InputMarkerProperty,"));
			Assert.That(phases.IndexOf("InputMarkerProperty", StringComparison.Ordinal),
				Is.LessThan(phases.IndexOf("heldAfterTopUp == afterExtra", StringComparison.Ordinal)));
			Assert.That(phases, Does.Contain("GameObject.Validate(FinalPlant)"));
			Assert.That(phases, Does.Contain("state.EnoughAnnounced"));
		}

		[Test]
		public void PersonaBracketsExactPhasesAndDisclosesEverySyntheticInput()
		{
			string persona = Read("Tools/personas/forage-native-check.persona");
			Assert.That(persona, Does.Contain("REQUEST=founding-first-city"));
			Assert.That(persona, Does.Contain("VERBS=forage-setup,forage-check"));
			Assert.That(persona, Does.Contain("SCRIPT=stagedigest;forage-setup;advance 2400;"
				+ "forage-check;advance 2400;forage-check;advance 2400;forage-check;advance 2400;"
				+ "forage-check;advance 2400;forage-check;stagedigest"));
			Assert.That(persona, Does.Contain("EXPECT=stagedigest:OK~founded=false,"
				+ "forage-setup:OK~native-forage phase=1,advance:OK,"
				+ "forage-check:OK~native-forage phase=2,advance:OK,"
				+ "forage-check:OK~native-forage phase=3,advance:OK,"
				+ "forage-check:OK~native-forage phase=4,advance:OK,"
				+ "forage-check:OK~native-forage phase=5,advance:OK,"
				+ "forage-check:OK~native-forage cases=1 passed=1 failed=0,"
				+ "stagedigest:OK~founded=true,COMPLETE"));
			Assert.That(Read(Checks), Does.Contain("synthetic-camp=true; synthetic-plants=true; "
				+ "synthetic-reservation=true"));
			Assert.That(Read(Checks), Does.Contain("ordinary-acceptance=false; charter=untested; "
				+ "save-load=untested"));
		}
	}
}
#endif
