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
		public void SealedScriptRunsFourChecksAtTheWidenedCumulativeCadence()
		{
			// Corrects the false "polls every tick" claim: Check() is driven only by these four
			// sealed teardown-check verbs, at cumulative ticks 2000/4800/7600/10800.
			string source = Read(Provider);
			Assert.That(source, Does.Contain("\"advance 2000\""));
			Assert.That(source, Does.Contain("CheckVerb, \"advance 2800\", CheckVerb, \"advance 2800\", CheckVerb, \"advance 3200\","));
			int checkVerbCount = 0;
			int index = 0;
			while ((index = source.IndexOf("CheckVerb,", index)) >= 0) { checkVerbCount++; index++; }
			Assert.That(checkVerbCount, Is.EqualTo(4), "exactly four sealed CheckVerb tokens");
		}

		[Test]
		public void SetupRunsTwoParallelCasesThroughRealProductionApis()
		{
			string source = Read(Checks);
			foreach (string token in new[]
			{
				"KingdomNativeCampFounding.Found(Game, Zone, Require)",
				"KingdomNativeCampFounding.Dedicate(Game, Zone, system,",
				"KingdomTeardownCrewEnrollment.Enroll(Game, Zone, system, Owned.Add,",
				"KingdomMaterials.DedicateStockpile(System, Zone, Chest, out failure)",
				"KingdomCommission.Commission(System, BuildKey, null,",
				"new Case(\"fire\", \"fire\", system, Zone, Game, Owned)",
				"new Case(\"larder\", \"larder\", system, Zone, Game, Owned)",
			}) Assert.That(source, Does.Contain(token), token);
			// The building object itself is never forced: no direct BuiltProperty write, and no
			// SetIntProperty("KingdomBuilt" write, anywhere in this file. Reading it (the
			// removal-census sweep) is legitimate and allowed.
			Assert.That(source, Does.Not.Contain("BuiltProperty"));
			Assert.That(source, Does.Not.Contain("SetIntProperty(\"KingdomBuilt\""));
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
			Assert.That(source, Does.Contain("salvaged == ExpectedSalvageDelta"));
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
		public void SalvageIsAttributedByStrikeReceiptNeverByOwnChestDelta()
		{
			// Two cases strike in parallel and production returns salvage to the FIRST eligible
			// stockpile in the zone (Growth/KingdomMaterials.13.StrikeRemovalAndSalvage.cs:
			// 114-126), not the original payer -- an own-chest before/after delta is unsound.
			string source = Read(Checks);
			Assert.That(source, Does.Contain(
				"item.GetStringProperty(KingdomMaterials.StrikeSalvageReceiptProperty)"));
			Assert.That(source, Does.Contain("!= StrikeReceiptId) continue;"));
			Assert.That(source, Does.Contain("KingdomMaterials.Stock(Zone)"));
			Assert.That(source, Does.Contain("foreach (GameObject stockpile in stock.Stockpiles)"));
			Assert.That(source, Does.Contain(
				"more than one salvage item carries this exact strike receipt"));
		}

		/// <summary>
		/// ORDER PIN ONLY. OrderStrike mints a new strike-route registry row and rebinds the
		/// works to it inside the same call (Growth/KingdomMaterials.08.StrikeOrdering.cs:
		/// 257-261, 09.StrikeStampAndCancellation.cs:35), superseding the paid-construction
		/// receipt salvage is actually tagged with. This proves the receipt capture line comes
		/// AFTER OrderStrike in source and that a distinctness check exists; it does NOT prove
		/// the runtime behaviour -- that the old receipt would truly misattribute -- since no
		/// pure predicate exists to value-test this without the game. The distinctness Require
		/// is the executable half; this pin is the ordering half.
		/// </summary>
		[Test]
		public void ReceiptIsCapturedAfterTheStrikeNeverBeforeAndMustDifferFromThePreStrikeOne()
		{
			string source = Read(Checks);
			int preStrike = source.IndexOf("preStrikeReceiptId = works.GetStringProperty(");
			int order = source.IndexOf("KingdomMaterials.OrderStrike(System, Zone, Works, out string failure)");
			int postStrike = source.IndexOf(
				"StrikeReceiptId = works.GetStringProperty(KingdomConstruction.ReceiptProperty);");
			Assert.That(preStrike, Is.GreaterThanOrEqualTo(0));
			Assert.That(order, Is.GreaterThan(preStrike));
			Assert.That(postStrike, Is.GreaterThan(order),
				"the strike-job receipt capture must be textually AFTER OrderStrike, never before");
			Assert.That(source, Does.Contain("StrikeReceiptId != preStrikeReceiptId"));
			Assert.That(source, Does.Contain(
				"the old paid-construction receipt would misattribute salvage"));
		}

		[Test]
		public void TheNewRegistryRowIsResolvedByReferenceAndClaimChecked()
		{
			// The real behavioural proof lives in KingdomTeardownStrikeRowClaimsTests (value
			// tests on the pure predicate); this pin only proves the harness actually calls it
			// with the strike receipt id and the live works/owner/zone, right after the strike.
			string source = Read(Checks);
			Assert.That(source, Does.Contain(
				"KingdomConstruction.TryFind(StrikeReceiptId, out KingdomConstructionJob row)"));
			Assert.That(source, Does.Contain(
				"KingdomTeardownStrikeRowClaims.IsExpectedStrikeRow(row,"));
			Assert.That(source, Does.Contain(
				"works.IDIfAssigned, KingdomConstruction.OwnerOf(System), Zone.ZoneID,"));
		}

		[Test]
		public void RemovalRefusesASameIdReplacementRatherThanCountingItAsGone()
		{
			// A same-ID object that is not the exact struck reference must REFUSE, not be
			// silently read as a valid removal.
			string source = Read(Checks);
			Assert.That(source, Does.Contain(
				"stillThere == null || ReferenceEquals(stillThere, Works)"));
			Assert.That(source, Does.Contain(
				"a same-ID replacement is never a valid removal"));
			Assert.That(source, Does.Contain(
				"onCell.GetIntProperty(\"KingdomBuilt\") != 1"));
			Assert.That(source, Does.Contain(
				"onCell.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != BuildKey"));
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

		[Test]
		public void CrewEnrollmentReusesTheRealProductionCitizenshipCallShape()
		{
			// Harness/KingdomTeardownCrewEnrollment.cs replicates the exact production call
			// sequence Harness/KingdomBountyFetchNativeFixture.cs:109-136 already uses (private
			// instance method on a sealed unrelated class, so a call-through was not possible).
			string source = Read("Harness/KingdomTeardownCrewEnrollment.cs");
			Assert.That(source, Does.Contain("internal const int CrewSize = 2;"));
			Assert.That(source, Does.Contain(
				"KingdomCitizenship.TryEnroll(System, body,"));
			Assert.That(source, Does.Contain(
				"KingdomCitizenshipEnrollmentReason.Arrival, tick, out string failure)"));
			Assert.That(source, Does.Contain("body.SetIntProperty(\"KingdomBorn\", 1);"));
			Assert.That(source, Does.Contain("KingdomResidents.TryEnsureRow(System, body,"));
			Assert.That(source, Does.Contain("KingdomResidents.OnRollCount(System) >= CrewSize"));
			// No direct Population/Working/Built write anywhere in the crew fixture.
			Assert.That(source, Does.Not.Contain("Population ="));
			Assert.That(source, Does.Not.Contain("\"KingdomBuilt\""));
		}
	}
}
#endif
