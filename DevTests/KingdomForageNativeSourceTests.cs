#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Wiring tripwires only; the persona supplies actual native forage evidence. Pure
	/// predicates the fixture relies on (geometry, ledger witness) are unit-tested for real in
	/// <c>KingdomForageNativeGeometryTests</c>, not pinned here.</summary>
	public class KingdomForageNativeSourceTests
	{
		private const string Provider = "Harness/KingdomForageNativeProvider.cs";
		private const string Checks = "Harness/KingdomForageNativeChecks.cs";
		private const string Fixture = "Harness/KingdomForageNativeFixture.cs";
		private const string Phases = "Harness/KingdomForageNativeChecksPhases.cs";
		private static string Read(string path) => TestMain.ReadRepositoryText(path);

		[Test]
		public void ProviderIsRegisteredWithBothVerbsAndTheSealedSevenPhaseScript()
		{
			string provider = Read(Provider);
			Assert.That(provider, Does.Contain("[KingdomScenarioVerbProvider]"));
			Assert.That(provider, Does.Contain("internal const string SetupVerb = \"forage-setup\";"));
			Assert.That(provider, Does.Contain("internal const string CheckVerb = \"forage-check\";"));
			Assert.That(provider, Does.Contain(
				"KingdomForageNativeChecks.Run(Verb, game, zone, out complete)"));
			Assert.That(provider, Does.Not.Contain("WorkForage("));
			int checkCount = 0, index = -1;
			while ((index = provider.IndexOf("CheckVerb", index + 1, StringComparison.Ordinal)) >= 0)
				checkCount++;
			// 1 const declaration + 1 ScenarioVerbs array entry + 1 Require gate + 7 Script entries.
			Assert.That(checkCount, Is.EqualTo(10), "expected exactly 7 CheckVerb script entries");
		}

		[Test]
		public void SetupFoundsTheCampPlantsOutsideEveryPlotRectAndProvesTheFreshStockIsZero()
		{
			string checks = Read(Checks), fixture = Read(Fixture);
			foreach (string token in new[] { "KingdomNativeCampFounding.Found(Game, Zone, Require)",
				"EnrollFour()", "System.Population == 4", "KingdomPlots.TryRiteGround(Zone",
				"stock.Stockpiles.Count > 0", "FindCanvas()", "RawBefore = CensusBrushRaw(StockpileContainer)",
				"Require(RawBefore == 0,", "Require(TallyBefore == 0," })
				Assert.That(checks, Does.Contain(token), token);
			foreach (string token in new[] { "CollectPlotRects()", "NextOutdoorCell(\"eligible plant 1\")",
				"Plant(\"Tree\", NextOutdoorCell(\"tree\"))", "owned.GetPart<Physics>().Owner = ",
				"Plant(\"Yuckwheat\", NextOutdoorCell(\"food plant\"))",
				"protectedPlant.SetIntProperty(\"KingdomStores\", 1)",
				"plotPlant = Plant(\"Plant\", InHeartRect())",
				"KingdomForageNativeGeometry.InsideAnyRect(", "GetTag(\"BodyType\") == \"ClothWall\"",
				"if (KingdomForageNativeGeometry.InsideAnyRect(cell.X, cell.Y, PlotRects)) continue;",
				"IsCropRowCell(cell)) continue;" })
				Assert.That(fixture, Does.Contain(token), token);
			// Never removes a world object to make room for a fixture plant.
			Assert.That(fixture, Does.Not.Contain(".Obliterate("));
			Assert.That(fixture, Does.Not.Contain(".Destroy("));
		}

		[Test]
		public void EachExclusionSnapshotBindsIdentityCellZoneOwnerCountAndCustody()
		{
			string checks = Read(Checks);
			foreach (string token in new[] { "Id = Item.IDIfAssigned", "X = Item.CurrentCell.X",
				"ZoneId = Item.CurrentZone.ZoneID", "Owner = Item.GetPart<Physics>()?.Owner",
				"Count = Item.Count" })
				Assert.That(checks, Does.Contain(token), token);
			string phases = Read(Phases);
			foreach (string token in new[] { "Snap.Item.IDIfAssigned == Snap.Id",
				"Snap.Item.CurrentCell.X == Snap.X", "Snap.Item.CurrentCell.Y == Snap.Y",
				"Snap.Item.CurrentZone.ZoneID == Snap.ZoneId", "Snap.Item.Count == Snap.Count",
				"Snap.Item.GetPart<Physics>()?.Owner == Snap.Owner",
				"KingdomOrdinaryCustody.TryProveEmpty(Snap.Item, out _)", "Snap.Fact(Snap.Item)" })
				Assert.That(phases, Does.Contain(token), token);
			// Recheck after cutting AND again after the final ceiling phase.
			Assert.That(CountOf(phases, "VerifyAllExclusions();"), Is.EqualTo(2));
		}

		[Test]
		public void PhasesNeverCallTheDutyDirectlyAndProveRemovalByDirectCensusNotArithmetic()
		{
			string phases = Read(Phases);
			Assert.That(phases, Does.Not.Contain("WorkForage("));
			foreach (string token in new[] { "!GameObject.Validate(Eligible[i])",
				"raw == RawBefore + Eligible.Length", "tally == TallyBefore + Eligible.Length" })
				Assert.That(phases, Does.Contain(token), token);
			string fixture = Read(Fixture);
			Assert.That(fixture, Does.Contain("private static int CensusBrushRaw(GameObject Container)"));
			Assert.That(fixture, Does.Contain("KingdomMaterials.TryMaterialOf(item, out var material)"));
		}

		[Test]
		public void ReservedUnitsAreCensusedDirectlyAndEachRetainsItsMarkerAndCustody()
		{
			string phases = Read(Phases);
			foreach (string token in new[] {
				"brush.SetStringProperty(KingdomConstruction.InputMarkerProperty, marker)",
				"Reserved.Add(new ReservedBrush(brush, marker))",
				"RawAtCeiling = CensusBrushRaw(StockpileContainer)",
				"RawAtCeiling == afterExtra + ReservedTopUp", "TallyAtCeiling == afterExtra",
				"GetStringProperty(KingdomConstruction.InputMarkerProperty)",
				"== reserved.Marker", "ReferenceEquals(reserved.Item.Physics.InInventory, StockpileContainer)" })
				Assert.That(phases, Does.Contain(token), token);
			Assert.That(phases.IndexOf("RawAtCeiling = CensusBrushRaw", StringComparison.Ordinal),
				Is.LessThan(phases.IndexOf("Require(RawAtCeiling ==", StringComparison.Ordinal)));
		}

		[Test]
		public void CeilingPhaseAssertsNoStockMovementOnBothCountsAndTheSurvivingPlant()
		{
			string phases = Read(Phases);
			foreach (string token in new[] { "Require(GameObject.Validate(FinalPlant),",
				"raw == RawAtCeiling", "tally == TallyAtCeiling", "state.EnoughAnnounced" })
				Assert.That(phases, Does.Contain(token), token);
		}

		[Test]
		public void OnceOnlyNoticeIsWitnessedByALedgerCountAcrossThreeExhaustedIntervals()
		{
			string phases = Read(Phases);
			Assert.That(phases, Does.Contain(
				"KingdomForageNativeGeometry.CountContaining(System.Ledger.Notes, ExhaustionMarker)"));
			foreach (string token in new[] { "notes == NotesBaseline + 1",
				"notes == NotesAfterFirstExhaustion" })
				Assert.That(CountOf(phases, token), Is.GreaterThanOrEqualTo(1), token);
			// The baseline capture plus three distinct exhausted-interval phases each read it.
			Assert.That(CountOf(phases, "NotesCount();"), Is.EqualTo(4));
		}

		[Test]
		public void PersonaBracketsExactSevenPhasesAndDisclosesEverySyntheticInput()
		{
			string persona = Read("Tools/personas/forage-native-check.persona");
			Assert.That(persona, Does.Contain("REQUEST=founding-first-city"));
			Assert.That(persona, Does.Contain("VERBS=forage-setup,forage-check"));
			Assert.That(persona, Does.Contain("SCRIPT=stagedigest;forage-setup;advance 2400;"
				+ "forage-check;advance 2400;forage-check;advance 2400;forage-check;advance 2400;"
				+ "forage-check;advance 2400;forage-check;advance 2400;forage-check;advance 2400;"
				+ "forage-check;stagedigest"));
			Assert.That(persona, Does.Contain("EXPECT=stagedigest:OK~founded=false,"
				+ "forage-setup:OK~native-forage phase=1,advance:OK,"
				+ "forage-check:OK~native-forage phase=2,advance:OK,"
				+ "forage-check:OK~native-forage phase=3,advance:OK,"
				+ "forage-check:OK~native-forage phase=4,advance:OK,"
				+ "forage-check:OK~native-forage phase=5,advance:OK,"
				+ "forage-check:OK~native-forage phase=6,advance:OK,"
				+ "forage-check:OK~native-forage phase=7,advance:OK,"
				+ "forage-check:OK~native-forage cases=1 passed=1 failed=0,"
				+ "stagedigest:OK~founded=true,COMPLETE"));
			Assert.That(Read(Checks), Does.Contain("synthetic-camp=true; synthetic-plants=true; "
				+ "synthetic-reservation=true"));
			Assert.That(Read(Checks), Does.Contain("ordinary-acceptance=false; charter=untested; "
				+ "save-load=untested"));
		}

		private static int CountOf(string source, string token)
		{
			int count = 0, index = -1;
			while ((index = source.IndexOf(token, index + 1, StringComparison.Ordinal)) >= 0) count++;
			return count;
		}
	}
}
#endif
