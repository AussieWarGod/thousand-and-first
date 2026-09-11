#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
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
		// The Case class (commission/await/strike/salvage) moved into its own partial-class file
		// once the crew-departure diagnostic pushed Checks.cs past the harness line cap; this
		// reads both as one logical source for pins that span the split, exactly as if it were
		// still one file.
		private const string Cases = "Harness/KingdomTeardownNativeChecks.Case.cs";
		private const string Persona = "Tools/personas/teardown-native-check.persona";
		private static string Read(string path) => TestMain.ReadRepositoryText(path);
		private static string ReadChecksAndCases() => Read(Checks) + Read(Cases);

		private static string ConstValue(string Source, string Name)
		{
			string marker = "internal const string " + Name + " = \"";
			int from = Source.IndexOf(marker, StringComparison.Ordinal);
			Assert.That(from, Is.GreaterThanOrEqualTo(0), "missing const: " + Name);
			from += marker.Length;
			int to = Source.IndexOf("\";", from, StringComparison.Ordinal);
			Assert.That(to, Is.GreaterThan(from), "unterminated const: " + Name);
			return Source.Substring(from, to - from);
		}

		/// <summary>Parses the provider's own Script array literal into the exact ordered step
		/// names it seals, resolving the SetupVerb/CheckVerb identifiers against their own const
		/// declarations in the same file rather than hardcoding either a second time here.
		/// </summary>
		private static List<string> ProviderScriptSteps(string ProviderSource)
		{
			string setupVerb = ConstValue(ProviderSource, "SetupVerb");
			string checkVerb = ConstValue(ProviderSource, "CheckVerb");
			int from = ProviderSource.IndexOf("private static readonly string[] Script = {",
				StringComparison.Ordinal);
			Assert.That(from, Is.GreaterThanOrEqualTo(0), "missing sealed Script array");
			from += "private static readonly string[] Script = {".Length;
			int to = ProviderSource.IndexOf("};", from, StringComparison.Ordinal);
			Assert.That(to, Is.GreaterThan(from), "unterminated sealed Script array");
			string body = ProviderSource.Substring(from, to - from);
			List<string> steps = new List<string>();
			foreach (string rawToken in body.Split(','))
			{
				string token = rawToken.Trim().Replace("\r", "").Replace("\n", "").Trim();
				if (token.Length == 0) continue;
				if (token.StartsWith("\"", StringComparison.Ordinal)
					&& token.EndsWith("\"", StringComparison.Ordinal))
					steps.Add(token.Substring(1, token.Length - 2));
				else if (token == "SetupVerb") steps.Add(setupVerb);
				else if (token == "CheckVerb") steps.Add(checkVerb);
				else Assert.Fail("unrecognised Script token: " + token);
			}
			return steps;
		}

		private static List<string> PersonaScriptSteps(string PersonaSource)
		{
			foreach (string line in PersonaSource.Split('\n'))
			{
				string trimmed = line.Trim();
				if (trimmed.StartsWith("SCRIPT=", StringComparison.Ordinal))
					return trimmed.Substring("SCRIPT=".Length).Split(';').ToList();
			}
			Assert.Fail("persona has no SCRIPT= line");
			return null;
		}

		/// <summary>
		/// review-ead2ede-teardown-findings.md REQUIRED B: nothing bound the persona's own
		/// SCRIPT to the provider's sealed Script array -- they were byte-identical only because
		/// both were hand-edited together this pass; a future one-sided edit would have passed
		/// every other test. This parses both files and compares the real step lists, the same
		/// pattern DevTests/KingdomQuoteSitingOccupancyNativeSourceTests.cs uses for its own
		/// sealed script.
		/// </summary>
		[Test]
		public void TheSealedProviderScriptIsExactlyThePersonaScript()
		{
			List<string> providerSteps = ProviderScriptSteps(Read(Provider));
			List<string> personaSteps = PersonaScriptSteps(Read(Persona));
			Assert.That(providerSteps, Is.EqualTo(personaSteps),
				"provider Script = [" + string.Join(";", providerSteps) + "] but persona SCRIPT = ["
				+ string.Join(";", personaSteps) + "]");
		}

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
			// sealed teardown-check verbs. Cumulative ticks 2400/6000/9600/13200 (deltas
			// 2400/3600/3600/3600) per review-bba51c4-teardown-findings.md nit 1 -- widened from
			// the prior zero-slack 2000/4800/7600/10800.
			string source = Read(Provider);
			Assert.That(source, Does.Contain("\"advance 2400\""));
			Assert.That(source, Does.Contain("CheckVerb, \"advance 3600\", CheckVerb, \"advance 3600\", CheckVerb, \"advance 3600\","));
			int checkVerbCount = 0;
			int index = 0;
			while ((index = source.IndexOf("CheckVerb,", index)) >= 0) { checkVerbCount++; index++; }
			Assert.That(checkVerbCount, Is.EqualTo(4), "exactly four sealed CheckVerb tokens");
		}

		[Test]
		public void SetupRunsTwoParallelCasesThroughRealProductionApis()
		{
			string source = ReadChecksAndCases();
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

		/// <summary>
		/// Native run 12 on 3272cff: teardown-setup REFUSED with InvalidOperationException
		/// "The exact realm sources cannot cover this construction input (InsufficientMaterial)".
		/// review-fb02900-coverage-findings.md finding 2: the reservation path honours Count end
		/// to end (Growth/KingdomConstruction.InputObservationRegistry.cs:141 -> InputPlannerScan
		/// .cs:173 -> KingdomMaterialDebitRules.Planning.cs:57), so the single-object-Count
		/// hypothesis for run 12's failure was NEVER proven and is not asserted here; the real
		/// cause remains UNPROVEN. Single-unit minting is kept only because it is
		/// KingdomCampHeartNativeFixture.Mint's own proven shape, not because it is known to fix
		/// run 12. What IS pinned: the bill is read live (never hardcoded), minted as N real
		/// single-unit objects, bounded by the dedicated store's own declared capacity
		/// (finding 2A) before anything is minted, and CanPayBill refuses by name with the exact
		/// missing tally if the freshly minted store still cannot cover it.
		/// </summary>
		[Test]
		public void TheBillIsMintedAsSeparateUnitsCapacityBoundedAndCanPayBillRefusesByName()
		{
			string source = ReadChecksAndCases();
			Assert.That(source, Does.Contain(
				"KingdomMaterialTally bill = KingdomMaterials.CostFor(BuildKey);"));
			Assert.That(source, Does.Contain("MintBill(bill, Require, Journal);"));
			Assert.That(source, Does.Contain(
				"private void MintBill(KingdomMaterialTally Bill, Action<bool, string> Require,"));
			Assert.That(source, Does.Contain(
				"int capacity = KingdomSurvey.StockCapacityOf(Chest);"));
			Assert.That(source, Does.Contain(
				"Require(totalUnits <= capacity,"));
			Assert.That(source, Does.Contain(
				"GameObject unit = GameObject.Create(blueprint);"));
			Assert.That(source, Does.Contain(
				"Require(GameObject.Validate(unit) && unit.Count == 1,"));
			Assert.That(source, Does.Contain(
				"Chest.Inventory.AddObject(unit, null,\n\t\t\t\t\t\t\tSilent: true, NoStack: true);"));
			Assert.That(source, Does.Contain("\"; synthetic-bill design=\""));
			Assert.That(source, Does.Contain(
				"private bool CanPayBill(KingdomMaterialTally Bill, out string Shortfall)"));
			Assert.That(source, Does.Contain("KingdomMaterialRules.Covers(stock.Tally, Bill)"));
			Assert.That(source, Does.Contain("KingdomMaterialRules.Missing(stock.Tally, Bill)"));
			Assert.That(source, Does.Contain(
				"Require(CanPayBill(bill, out shortfall),"));
			// The withdrawn hypothesis must never be re-asserted as a proven cause.
			Assert.That(source, Does.Not.Contain("did not answer a real material reservation"));
			// Dropped in an earlier pass; restored per house law (never weaken a pin) --
			// guards the anti-pattern itself, independent of whether it caused native run 12.
			Assert.That(source, Does.Not.Contain(".Count = TimberCost"));
			Assert.That(source, Does.Not.Contain("SetIntProperty(\"NeverStack\""));
		}

		[Test]
		public void CheckPollsRealBuiltStateBeforeOrderingTheRealStrike()
		{
			string source = ReadChecksAndCases();
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
			string source = ReadChecksAndCases();
			Assert.That(source, Does.Contain("awaiting-struck=true"));
			// The expected delta is COMPUTED from the same production rule OrderStrike itself
			// uses, never hardcoded: KingdomMaterials.CostFor + KingdomMaterialRules.
			// StrikeSalvagePercent (Growth/KingdomMaterialRules.Clearance.cs:193,211-219),
			// mirroring Cost.Scaled's own integer-floor arithmetic
			// (Growth/KingdomMaterialTally.cs:101-111). The single CostFor read is now shared
			// (bound to `bill`) with MintBill/CanPayBill below, never re-read separately.
			Assert.That(source, Does.Contain(
				"KingdomMaterialTally bill = KingdomMaterials.CostFor(BuildKey);"));
			Assert.That(source, Does.Contain("TimberCost = bill.Get(KingdomMaterial.Timber);"));
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
			string source = ReadChecksAndCases();
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
			string source = ReadChecksAndCases();
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
			string source = ReadChecksAndCases();
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
			string source = ReadChecksAndCases();
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
			string source = ReadChecksAndCases();
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

		/// <summary>
		/// Required per review-3f010e3-teardown-findings.md finding 3: departure previously
		/// stalled both cases at Phase 1 with Ok=true and no named diagnostic. Check() must
		/// re-Require the crew is STILL on the roll (production KingdomResidents.OnRollCount,
		/// never a cached or harness-local count) before touching either case, and refuse by
		/// name -- naming the exact case still open, never a generic "labour" message -- through
		/// the same Require/throw/Fail() pipeline every other named refusal in this scenario
		/// already uses (so Ok=false follows for free). The real boundary/format proof is the
		/// value test on KingdomTeardownCrewDepartureClaims (an engine-free predicate); this pins
		/// only that Check() actually reads the real production count and calls it, with no
		/// departure freeze (nothing tries to stop production ending the crew's stay) and no
		/// re-enrolment (a departed body is never replaced).
		/// </summary>
		[Test]
		public void CheckReRequiresTheCrewIsStillOnTheRollAndRefusesByNameNeverFreezingOrReenrolling()
		{
			string source = Read(Checks);
			Assert.That(source, Does.Contain("private KingdomSystem System;"));
			Assert.That(source, Does.Contain("System = system;"));
			Assert.That(source, Does.Contain("int onRoll = KingdomResidents.OnRollCount(System);"));
			Assert.That(source, Does.Contain(
				"KingdomTeardownCrewDepartureClaims.HasDeparted(onRoll,"));
			Assert.That(source, Does.Contain(
				"Require(false, KingdomTeardownCrewDepartureClaims.Diagnostic(c.Name,"));
			// No departure freeze: nothing here reads or writes a lodging/brink/grace state.
			Assert.That(source, Does.Not.Contain("GraceDays"));
			Assert.That(source, Does.Not.Contain("Lodging"));
			// No re-enrolment: the crew-departure guard never calls Enroll again -- only Start()
			// does, exactly once, before any case begins.
			int enrollCalls = 0;
			int index = 0;
			while ((index = source.IndexOf("KingdomTeardownCrewEnrollment.Enroll(", index)) >= 0)
			{
				enrollCalls++;
				index++;
			}
			Assert.That(enrollCalls, Is.EqualTo(1),
				"the crew must be enrolled exactly once, in Start(), never re-enrolled on departure");
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

		/// <summary>
		/// review-teardown-run15-neverbuilt.md finding 4c: OnRollCount alone counts every
		/// non-Dead row and is blind to a standing or posting change. RequireAvailable is the
		/// stronger, re-askable proof: read-only membership in the production
		/// KingdomCrews.AvailableSettlers projection plus KingdomStations.PostOf==0, asserted
		/// once at Enroll (setup) and again every Frame.Check() -- never minting or forcing
		/// standing.
		/// </summary>
		[Test]
		public void CrewAvailabilityIsReAskedEveryCheckNeverMintedOrForced()
		{
			string enrollment = Read("Harness/KingdomTeardownCrewEnrollment.cs");
			Assert.That(enrollment, Does.Contain(
				"internal static void RequireAvailable(KingdomSystem System, Zone Zone,"));
			Assert.That(enrollment, Does.Contain(
				"KingdomCrews.AvailableSettlers(System, survey)"));
			Assert.That(enrollment, Does.Contain("out List<GameObject> Bodies)"));
			Assert.That(enrollment, Does.Contain(
				"RequireAvailable(System, Zone, Bodies, Require, null, null);"));
			string checks = Read(Checks);
			Assert.That(checks, Does.Contain(
				"Require, out Crew) == 2,"));
			Assert.That(checks, Does.Contain(
				"KingdomTeardownCrewEnrollment.RequireAvailable(System, Zone, Crew, Require,"));
			Assert.That(checks, Does.Contain(
				"acceptablePosts, line => Evidence.Append(line));"));
		}

		/// <summary>
		/// review-bba51c4-teardown-findings.md REQUIRED 1: a strict PostOf==0 re-ask on every
		/// Check refused a healthy crew mid-raise, since production posts the selected hands and
		/// only un-posts at the NEXT Assign pass. A post is now accepted on a per-Check re-ask
		/// only when it names one of this fixture's own live raisings (fire's/larder's WorksId
		/// via KingdomCityRules.StableId, the same id the allocator itself posts with), the raw
		/// post journaled per body rather than asserted -- PostOf==0 stays a strict setup-only
		/// assertion, before any raising exists.
		/// </summary>
		[Test]
		public void ReAskedPostIsAcceptedOnlyWhenItNamesThisFixturesOwnRaisingNeverAssertedZero()
		{
			string enrollment = Read("Harness/KingdomTeardownCrewEnrollment.cs");
			Assert.That(enrollment, Does.Contain("ISet<int> AcceptablePostIds,"));
			Assert.That(enrollment, Does.Contain("Action<string> Journal)"));
			Assert.That(enrollment, Does.Contain("int post = KingdomStations.PostOf(body);"));
			Assert.That(enrollment, Does.Contain("if (AcceptablePostIds == null)"));
			Assert.That(enrollment, Does.Contain("Require(post == 0,"));
			Assert.That(enrollment, Does.Contain(
				"AcceptablePostIds != null && post != 0\n\t\t\t\t\t&& AcceptablePostIds.Contains(post)"));
			Assert.That(enrollment, Does.Contain(
				"== (int)KingdomWorkKind.Construction;"));
			Assert.That(enrollment, Does.Contain(
				"Journal?.Invoke(\"; crew=\" + (i + 1) + \" posted-to=\" + post\n"
					+ "\t\t\t\t\t+ \" own-raising=\" + ownRaising);"));
			string checks = Read(Checks);
			Assert.That(checks, Does.Contain(
				"acceptablePosts.Add(KingdomCityRules.StableId(Fire.WorksId));"));
			Assert.That(checks, Does.Contain(
				"acceptablePosts.Add(KingdomCityRules.StableId(Larder.WorksId));"));
			// The exact prior-round defect (bba51c4): no unconditional PostOf==0 assertion may
			// survive on the re-ask path -- the setup call (AcceptablePostIds == null) is the
			// only lawful strict use, verified above.
			Assert.That(enrollment, Does.Not.Contain(
				"Require(free || postedToThisFixture,"));
		}

		/// <summary>
		/// review-ead2ede-teardown-findings.md residual: Growth/KingdomGrowth.z15.
		/// WorkAssignment.cs re-posts EVERY AvailableSettler to whatever work it drew each pass,
		/// before KingdomConstructionPresence.Assign ever runs, so a body can legitimately carry
		/// a post naming neither 0 nor one of this fixture's own raisings. RequireAvailable must
		/// never assert the post value once a raising can exist -- only journal it -- and must
		/// refuse only on liveness/AvailableSettlers grounds.
		/// </summary>
		[Test]
		public void RequireAvailableNeverAssertsThePostValueOnlyLivenessAndAvailability()
		{
			string enrollment = Read("Harness/KingdomTeardownCrewEnrollment.cs");
			Assert.That(enrollment, Does.Contain(
				"Require(GameObject.Validate(body) && body.IsAlive,"));
			Assert.That(enrollment, Does.Contain(
				"is dead or no longer a valid object"));
			Assert.That(enrollment, Does.Contain(
				"is not present in the production AvailableSettlers projection"));
			Assert.That(enrollment, Does.Contain("bool ownRaising = AcceptablePostIds != null"));
			// No Require may name the post value once a raising can exist -- disclosure only.
			Assert.That(enrollment, Does.Not.Contain("neither free nor posted"));
		}

		/// <summary>
		/// review-teardown-run15-neverbuilt.md findings 1/2: sequence the two cases (one gang,
		/// one raising) and journal per-check telemetry so a future stall is diagnosable instead
		/// of a bare "awaiting-built=true".
		/// </summary>
		[Test]
		public void LarderIsSequencedAfterFireAndEveryCheckJournalsRealTelemetry()
		{
			string checks = Read(Checks);
			Assert.That(checks, Does.Contain("private bool LarderStarted;"));
			Assert.That(checks, Does.Contain("if (!LarderStarted && Fire.Phase >= 2)"));
			Assert.That(checks, Does.Contain("Cases.Add(Larder);"));
			Assert.That(checks, Does.Contain("Done = LarderStarted;"));
			Assert.That(checks, Does.Contain("Append(\"; available=\")"));
			Assert.That(checks, Does.Contain("Append(\" free=\")"));
			Assert.That(checks, Does.Contain("Append(\" assigned-crew=\")"));
			Assert.That(checks, Does.Contain("Append(\" on-roll=\")"));
			Assert.That(checks, Does.Contain("Append(\" labours=\")"));
			string cases = Read(Cases);
			Assert.That(cases, Does.Contain("private string Telemetry(GameObject Root)"));
			Assert.That(cases, Does.Contain("KingdomPlots.PlotWorkRequiredProperty"));
			Assert.That(cases, Does.Contain("KingdomPlots.PlotWorkRemainingProperty"));
			Assert.That(cases, Does.Contain("KingdomPlots.PlotWorkLastTickProperty"));
			Assert.That(cases, Does.Contain("KingdomPlots.PlotWorkWindowProperty"));
			Assert.That(cases, Does.Contain("KingdomConstructionPresence.SelectedProperty"));
			Assert.That(cases, Does.Contain("KingdomConstructionPresence.HandsProperty"));
			Assert.That(cases, Does.Contain("KingdomConstructionPresence.EffectivenessProperty"));
			Assert.That(cases, Does.Contain("KingdomConstruction.TryFind(JobId, out KingdomConstructionJob row)"));
			Assert.That(cases, Does.Contain("internal int LastHands;"));
		}
	}
}
#endif
