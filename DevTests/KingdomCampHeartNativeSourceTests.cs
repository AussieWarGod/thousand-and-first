#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>Wiring tripwires only; the persona supplies the actual native camp-heart
	/// evidence. These pin the two things a source pin can prove: that the seam drives nothing
	/// itself, and that every identity claim it makes is an exact-identity claim.</summary>
	public class KingdomCampHeartNativeSourceTests
	{
		private const string Provider = "Harness/KingdomCampHeartNativeProvider.cs";
		private const string Checks = "Harness/KingdomCampHeartNativeChecks.cs";
		private const string Fixture = "Harness/KingdomCampHeartNativeFixture.cs";
		private const string Phases = "Harness/KingdomCampHeartNativeChecksPhases.cs";
		private const string Reads = "Harness/KingdomCampHeartNativeReads.cs";
		private const string Bill = "Harness/KingdomCampHeartNativeBill.cs";
		private const string Claim = "Harness/KingdomCampHeartNativeClaim.cs";
		private const string Persona = "Tools/personas/camp-heart-native-checks.persona";
		private static string Read(string path) => TestMain.ReadRepositoryText(path);

		[Test]
		public void ProviderIsRegisteredWithBothVerbsAndTheSealedScript()
		{
			string provider = Read(Provider);
			Assert.That(provider, Does.Contain("[KingdomScenarioVerbProvider]"));
			Assert.That(provider, Does.Contain(
				"internal const string SetupVerb = \"camp-heart-setup\";"));
			Assert.That(provider, Does.Contain(
				"internal const string CheckVerb = \"camp-heart-check\";"));
			Assert.That(provider, Does.Contain("\"stagedigest\", SetupVerb, \"advance 1200\","));
			Assert.That(provider, Does.Contain(
				"KingdomCampHeartNativeChecks.Run(Verb, game, zone, out complete)"));
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(327)]
		[TestCase(1199)]
		public void FirstAdvanceCrossesDailyBoundaryAtEachSetupPhase(int Phase)
		{
			string script = Array.Find(Read(Persona).Split('\n'),
				line => line.StartsWith("SCRIPT=", StringComparison.Ordinal));
			int turns = int.Parse(script.Split(';')[2].Substring("advance ".Length));
			long start = 273600L + Phase;
			Assert.That(KingdomSemanticClockRules.AbsoluteBoundary(start + turns),
				Is.GreaterThan(KingdomSemanticClockRules.AbsoluteBoundary(start)));
		}

		[Test]
		public void NoShardEverDrivesTheUpgradeItself()
		{
			foreach (string path in new[] { Provider, Checks, Fixture, Phases, Reads, Bill, Claim })
			{
				string text = Read(path);
				foreach (string forbidden in new[] { "KingdomUpgrade.Begin(",
					"KingdomUpgrade.BeginPrepared(", "KingdomPlots.Advance(",
					"TryApplyUpgrade(", "TryStage(", "KingdomConstruction.TryFundNew(" })
					Assert.That(text, Does.Not.Contain(forbidden), path + " / " + forbidden);
			}
		}

		[Test]
		public void SetupFoundsARealCampCompletesRungOneAndFillsTheAuthoredStore()
		{
			string checks = Read(Checks), fixture = Read(Fixture);
			foreach (string token in new[] {
				"KingdomNativeCampFounding.Found(Game, Zone, RequirePair)",
				"KingdomScenarioCompletedHeart.Complete(Game, System, Zone)",
				"KingdomPlots.HeartRung(Zone) == 1", "EnrollResidents()",
				"KingdomNativeCampFounding.Dedicate(Game, Zone, System, DedicatedDrams,",
				"MintStoreContents()",
				"Game.SetIntGameState(KingdomUpgrade.NoticedState, 1)" })
				Assert.That(checks, Does.Contain(token), token);
			foreach (string token in new[] { "KingdomCitizenship.TryEnroll(System, body,",
				"KingdomResidents.TryEnsureRow(System, body, FixtureOrigin, null, tick,",
				"KingdomArchitectureStamper.TryExactAnchoredComponent(Heart, Zone,",
				"Store.Inventory.Objects.Count == 0",
				"Mint(KingdomMaterial.Stone, MintedStoneUnits, MintedStone)",
				"Mint(KingdomMaterial.Timber, MintedTimberUnits, MintedTimber)",
				"Mint(KingdomMaterial.Brush, MintedBrushUnits, MintedBrush)" })
				Assert.That(fixture, Does.Contain(token), token);
		}

		[Test]
		public void AResidentStandsOnClaimedGroundBeforeItsRowIsPublished()
		{
			string fixture = Read(Fixture);
			int born = fixture.IndexOf("body.SetIntProperty(\"KingdomBorn\", 1)",
				StringComparison.Ordinal);
			int placed = fixture.IndexOf("cell.AddObject(body, NoStack: true)",
				StringComparison.Ordinal);
			int rowed = fixture.IndexOf("KingdomResidents.TryEnsureRow(System, body,",
				StringComparison.Ordinal);
			Assert.That(born, Is.GreaterThan(0), "the fixture must stamp born provenance");
			Assert.That(placed, Is.GreaterThan(0), "the fixture must place the body");
			// Production's roster gate (KingdomResidents.Enrollable) requires KingdomBorn == 1,
			// which TryEnroll never sets, and row publication has a physical-ground precondition:
			// a body still in no zone is enrolled into a roll it cannot appear on. Proved live
			// 2026-09-10 on the forage seam, which refused at setup with the opposite order.
			Assert.That(born, Is.LessThan(rowed), "the born stamp must precede TryEnsureRow");
			Assert.That(placed, Is.LessThan(rowed), "AddObject must precede TryEnsureRow");
			foreach (string token in new[] {
				"System.ClaimedZones.Contains(Zone.ZoneID)",
				"taf-camp-resident-unplaced", "RequireRowInBook(book, body, id, i + 1)",
				"Book.TryResidentRow(Id, out index)", "rows[i].BoundZoneId",
				"!body.IsPlayer() && !body.IsPlayerLed()",
				"KingdomResidents.RollRows(System)",
				"KingdomResidents.OnRollCount(System) == Expected" })
				Assert.That(fixture, Does.Contain(token), token);
			// The row is READ back, never written: no book state is stamped here.
			foreach (string forbidden in new[] { "System.City.Write", "state.ResidentCount = ",
				"System.Population = ", "System.ResidentCounter = ", "System.Stage = " })
				Assert.That(fixture, Does.Not.Contain(forbidden), forbidden);
		}

		[Test]
		public void TheMintedStoreContentsAreExactlyTheAuthoredBillPlusUnaskedUnits()
		{
			string checks = Read(Checks);
			Assert.That(checks, Does.Contain("internal const int MintedStoneUnits = 24;"));
			Assert.That(checks, Does.Contain("internal const int MintedTimberUnits = 1;"));
			Assert.That(checks, Does.Contain("internal const int MintedBrushUnits = 23;"));
			Assert.That(checks, Does.Contain("internal const int ResidentCount = 6;"));
			Assert.That(checks, Does.Contain("internal const int DedicatedDrams = 400;"));
		}

		[Test]
		public void ThePhasesProveTheBillWasPaidAndTheStoreKeptItsExactIdentity()
		{
			string phases = Read(Phases);
			foreach (string token in new[] { "System.Stage >= GrowthStage.Steading",
				"RequireBillDebited();",
				"RequireAbsent(MintedStone, present, \"stone\")",
				"RequireAbsent(MintedTimber, present, \"timber\")",
				"RequireHeld(RetainedBrush, present, \"brush\")",
				"KingdomUpgrade.DesignKeyOf(standing) == SecondRungKey",
				"ReferenceEquals(store, Store) && store.IDIfAssigned == StoreId",
				"Offset(fire.CurrentCell) == Offset(FireCell)" })
				Assert.That(phases, Does.Contain(token), token);
			Assert.That(phases.IndexOf("RequireStoreIdentity();", StringComparison.Ordinal),
				Is.GreaterThan(0));
		}

		[Test]
		public void CustodyIsReadRawAndAnInvalidEntryFailsRatherThanBeingSkipped()
		{
			string reads = Read(Reads);
			foreach (string token in new[] { "taf-camp-store-invalid-entry",
				"taf-camp-store-foreign-holder",
				"KingdomMaterials.RawCensusCountOf(item)",
				"KingdomCampHeartNativeCensus.Duplicates(units)",
				"KingdomSurvey.TryTakeUnboundRecovery(Zone, out survey)",
				"taf-camp-heart-census-incomplete",
				"taf-camp-store-contradictory-custody",
				"Require(physics.CurrentCell == null,",
				"Require(ReferenceEquals(physics.InInventory, Store),",
				"taf-camp-store-unassigned-identity", "taf-camp-store-nonpositive-count",
				"taf-camp-store-body-replaced", "ReferenceEquals(Present[j], want)" })
				Assert.That(reads, Does.Contain(token), token);
			// The settlement-wide reading must never be the publishing survey, and must never be
			// the bare custody-only call that cannot prove a complete index.
			Assert.That(reads, Does.Not.Contain("KingdomSurvey.Take(Zone"));
			Assert.That(reads, Does.Not.Contain("KingdomSurvey.TakeCustodyOnly("));
			Assert.That(Read(Phases), Does.Not.Contain("KingdomSurvey.Take(Zone"));
		}

		[Test]
		public void IdentityIsProvedByWholeCustodyNotByCounting()
		{
			string reads = Read(Reads);
			Assert.That(reads, Does.Contain("KingdomCampHeartNativeCensus.Faults(Wanted, Present)"));
			Assert.That(reads, Does.Contain(
				"KingdomCampHeartNativeCensus.Surviving(Wanted, Present)"));
			Assert.That(reads, Does.Contain("ReferenceEquals(Store.CurrentCell, StoreCell)"));
			Assert.That(reads, Does.Contain("Store.IDIfAssigned == StoreId"));
			// The water bill is read from the production catalogue, never repeated as a number.
			Assert.That(reads, Does.Contain("KingdomUpgradeRules.CostDrams(successor.CostDrams,"));
			Assert.That(reads, Does.Not.Contain("== 18"));
		}

		[Test]
		public void TheSpentBillIsProvedFromTheProductionConstructionClaim()
		{
			string bill = Read(Bill);
			foreach (string token in new[] {
				"KingdomConstruction.TryOwnedActive(System, Zone, out jobs)",
				"job.Route != KingdomConstructionRoute.Improvement",
				"job.SubjectId != HeartId", "found.TargetKey == SecondRungKey",
				"found.Claims.WaterSpent", "BilledWater == AuthoredWaterCost",
				"KingdomCampHeartNativeCensus.BillFaults(want, got)",
				"KingdomMaterials.UpgradeCostFor(FirstRungKey)",
				"KingdomMaterialDebitCost.TryParseClaim(BilledMaterial, out spent)" })
				Assert.That(bill, Does.Contain(token), token);
			// An overcharge must never pass: no >= comparison survives in the bill shard.
			Assert.That(bill, Does.Not.Contain(">= Authored"));
			Assert.That(bill, Does.Not.Contain(">= Units"));
			// Absence from the store is only ever claimed as absence from the store.
			Assert.That(Read(Reads), Does.Contain("ABSENT FROM THE DEDICATED STORE"));
		}

		[Test]
		public void ARealCommissionIsPutToTheSettlementAndTheHeartGroundIsNeverTaken()
		{
			string claim = Read(Claim);
			foreach (string token in new[] {
				"KingdomPlots.Commission(System, Zone, entry, null,",
				"KingdomPlots.ReadPlots(Zone)",
				"Require(!KingdomPlotRules.Overlaps(laid,",
				"KingdomPlotRules.Reserved(heart)",
				"taf-camp-claim-took-heart-ground",
				"Require(KingdomPlotRules.CrowdsExisting(overStore, standing),",
				"taf-camp-claim-store-not-crowded",
				"taf-camp-claim-staked-on-refusal",
				"RequireStoreIdentity();", "taf-camp-claim-fire-disturbed" })
				Assert.That(claim, Does.Contain(token), token);
			Assert.That(Read(Phases), Does.Contain("RequireHeartGroundNeverTaken(standing);"));
		}

		[Test]
		public void NoStockpileSpecificRefusalReasonIsEverClaimed()
		{
			string claim = Read(Claim);
			// #107's "refuses for stockpile reason" is deliberately NOT asserted: production
			// records no such reason on this path. The refusal text is recorded verbatim and
			// never required to name the store.
			Assert.That(claim, Does.Contain("NO STOCKPILE-SPECIFIC REASON IS CLAIMED"));
			Assert.That(claim, Does.Contain("KingdomScenarioRules.Bounded(failure)"));
			foreach (string forbidden in new[] { "RefuseObstruction", "camp stockpile\"",
				"Does.Contain(\"stockpile\")" })
				Assert.That(claim, Does.Not.Contain(forbidden), forbidden);
			// The founder-facing answer when nothing fits is RefuseRoom, and that literal lives
			// where the siting path emits it, not in the harness.
			string siting = Read("Growth/KingdomPlot2.08.Siting.cs");
			Assert.That(siting, Does.Contain("Refusal = KingdomPlotRules.RefuseRoom(staked);"));
			Assert.That(siting, Does.Contain(
				"if (KingdomPlotRules.CrowdsExisting(rect, laid)) continue;"));
			Assert.That(Read(Checks),
				Does.Contain("stockpile-refusal-reason-claimed=false"));
			Assert.That(Read(Phases), Does.Contain("stockpile-reason-claimed=false"));
		}

		[Test]
		public void FreshMaterialIdentityIsAllocatedBeforeInsertionAndOnlyObservedAfterwards()
		{
			string source = Read(Fixture);
			int mint = source.IndexOf("private void Mint(", StringComparison.Ordinal);
			int allocate = source.IndexOf("string id = unit.ID;", mint, StringComparison.Ordinal);
			int insert = source.IndexOf("Store.Inventory.AddObject(unit", mint, StringComparison.Ordinal);
			int reproof = source.IndexOf("Require(unit.IDIfAssigned == id && !Ids.Contains(id)", mint, StringComparison.Ordinal);
			Assert.That(allocate, Is.GreaterThan(mint));
			Assert.That(insert, Is.GreaterThan(allocate));
			Assert.That(reproof, Is.GreaterThan(insert));
			Assert.That(System.Text.RegularExpressions.Regex.IsMatch(Read(Reads), @"\b(?:Item|item)\.ID\b"), Is.False);
			Assert.That(Read(Checks), Does.Contain("synthetic-material-identities=true"));
		}

		[Test]
		public void PersonaBracketsTheExactPhasesAndDisclosesEverySyntheticInput()
		{
			string persona = Read(Persona);
			Assert.That(persona, Does.Contain("REQUEST=founding-first-city"));
			Assert.That(persona, Does.Contain("VERBS=camp-heart-setup,camp-heart-check"));
			Assert.That(persona, Does.Contain("SCRIPT=stagedigest;camp-heart-setup;advance 1200;"
				+ "camp-heart-check;advance 2400;camp-heart-check;stagedigest"));
			Assert.That(persona, Does.Contain("EXPECT=stagedigest:OK~founded=false,"
				+ "camp-heart-setup:OK~native-camp-heart phase=1,advance:OK,"
				+ "camp-heart-check:OK~native-camp-heart phase=2,advance:OK,"
				+ "camp-heart-check:OK~native-camp-heart cases=1 passed=1 failed=0,"
				+ "stagedigest:OK~founded=true,COMPLETE"));
			Assert.That(persona, Does.Contain("cases 1, 3, 4, 5 and 6 remain owed"));
			string checks = Read(Checks);
			Assert.That(checks, Does.Contain("synthetic-camp=true; synthetic-residents=true; "
				+ "synthetic-store-contents=true"));
			Assert.That(checks, Does.Contain("synthetic-drams=true; synthetic-born-provenance=true"));
			Assert.That(checks, Does.Contain("improvement-notice-premarked=true"));
			Assert.That(checks, Does.Contain("ordinary-acceptance=false; charter=untested; "
				+ "save-load=untested"));
		}
	}
}
#endif
