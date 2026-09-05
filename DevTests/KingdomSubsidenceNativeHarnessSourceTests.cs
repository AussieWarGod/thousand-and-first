#if TAF_TESTS
using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Native subsidence source contracts; these tests do not execute engine fixtures.</summary>
	[TestFixture]
	public sealed class KingdomSubsidenceNativeHarnessSourceTests
	{
		private const string Provider = "Harness/KingdomSubsidenceNativeProvider.cs";
		private const string Fixture = "Harness/KingdomSubsidenceNativeFixture.cs";
		private const string Checks = "Harness/KingdomSubsidenceNativeChecks.cs";
		private const string Fault = "Harness/KingdomSubsidenceNativeSummaryFault.cs";
		private const string Persona = "Tools/personas/subsidence-native-checks.persona";

		[Test]
		public void SourceContract_PersonaRunsThreeNamedCasesAcrossRealFounding()
		{
			string persona = Read(Persona);
			Assert.AreEqual("founding-first-city", Setting(persona, "REQUEST"));
			Assert.AreEqual("stagedigest;subsidence-check;stagedigest", Setting(persona, "SCRIPT"));
			Assert.AreEqual("subsidence-check", Setting(persona, "VERBS"));
			Assert.AreEqual("stagedigest:OK~founded=false,subsidence-check:OK~cases=3 passed=3 failed=0,stagedigest:OK~founded=true,COMPLETE",
				Setting(persona, "EXPECT"));
			string[] cases = Regex.Matches(Read(Checks), @"\bcurrent\s*=\s*""([^""]+)""")
				.Cast<Match>().Select(match => match.Groups[1].Value).ToArray();
			CollectionAssert.AreEqual(new[] { "physical-city-fixture", "five-departures-summary-interruption",
				"same-tick-no-replay" }, cases);
			Assert.AreEqual(cases.Length, cases.Distinct(StringComparer.Ordinal).Count());
			ContainsAll(Read(Provider), "internal const int ExpectedCases = 3;",
				"internal const string Verb = \"subsidence-check\";");
		}

		[Test]
		public void SourceContract_EligibilityRequiresFreshFiveTableReceiptAndExactStampedScript()
		{
			string body = Body(Provider, "private static bool Eligible(");
			Ordered(body, "Game == null || Zone == null || The.ZoneManager == null",
				"!ReferenceEquals(The.ZoneManager.ActiveZone, Zone)", "!MessageQueue.Enabled",
				"!KingdomSubsidence.Enabled", "!KingdomMaster.ConfiguredEnabled",
				"Game.TimeTicks <= KingdomSubsidenceRules.StepDays * KingdomRules.TicksPerDay",
				"Game.GetSystem<KingdomSystem>()?.Founded", "HasQuickstartState(Game)",
				"HasAnyState(Game, Receipt)) return false;", "TryBindStampedPlan(out plan, out stamp, out Failure)",
				"plan.Key != \"founding-first-city\"", "!KingdomScenarioScript.TryRead(out script, out Failure)",
				"script.Count != 3", "script[0] != \"stagedigest\"", "script[1] != Verb",
				"script[2] != \"stagedigest\"", "KingdomScenarioTransactionMarker.Observe(out transaction)",
				"KingdomScenarioTransactionShape.None", "TryProfile(\"marsh\", out profile)",
				"Zone.ZoneID != profile.ZoneId", "return true;");
			string shape = Body("Harness/KingdomNativeRegressionContext.cs", "internal static bool HasAnyState(");
			foreach (string table in new[] { "String", "Int", "Int64", "Boolean", "Object" })
				StringAssert.Contains("Game." + table + "GameState?.ContainsKey(Key)", shape);
		}

		[Test]
		public void SourceContract_IntentIsProvedBeforeFixtureAndBeforeResultPublication()
		{
			string body = Body(Provider, "public string RunScenarioVerb(");
			Ordered(body, "Ok = false;", "!string.IsNullOrEmpty(Argument)", "!Eligible(game, zone, out failure)",
				"game.SetStringGameState(Receipt, \"intent\");",
				"!KingdomScenarioDurableState.ProvesExactText(Receipt, \"intent\")",
				"KingdomSubsidenceNativeChecks.Run(game, zone, out Ok)",
				"!KingdomScenarioDurableState.ProvesExactText(Receipt, \"intent\")", "Ok = false;", "return report +",
				"game.SetStringGameState(Receipt, report);",
				"!KingdomScenarioDurableState.ProvesExactText(Receipt, report)", "Ok = false;", "return report +");
			ContainsAll(Body("Harness/KingdomScenarioDurableState.cs", "internal static bool ProvesExactText("),
				"Observe(Key)", "KingdomScenarioStateShape.Classify(observed, out detail)",
				"KingdomDurableKeyShape.ExactString", "string.Equals(observed.String, Expected, StringComparison.Ordinal)");
		}

		[Test]
		public void SourceContract_FixtureUsesStampedProductionFoundingTransaction()
		{
			string body = Body(Fixture, "internal static bool TryCreate(");
			Ordered(body, "Fixture = LastAttempt;", "Require(Fixture == null,",
				"TryBindStampedPlan(out plan, out stamp, out failure)", "plan.Key == \"founding-first-city\"",
				"plan.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority",
				"step.Verb == KingdomScenarioVerb.FoundFirstCity", "name == null && step.Arguments.TryGetValue(\"CityName\", out name)",
				"KingdomScenarioFoundingStep.TryProvePreconditions(Zone, name, out failure)",
				"LastAttempt = Fixture;", "KingdomScenarioTransactionMarker.TryBegin(out failure)",
				"KingdomScenarioFoundingStep.TryFound(Zone, name, out line, out failure)",
				"KingdomScenarioTransactionMarker.TryCommit(out failure)",
				"Fixture.System = game.GetSystem<KingdomSystem>();", "Fixture.Build();");
			ContainsAll(Body(Fixture, "private void RequireWorld("), "ReferenceEquals(The.Game, Game)",
				"ReferenceEquals(The.ZoneManager?.ActiveZone, Zone)", "ReferenceEquals(Game.GetSystem<KingdomSystem>(), System)",
				"System.Founded && System.OwnedZone(Zone.ZoneID)", "System.CurrentRealmId", "System.CurrentSettlementId",
				"Factions.GetIfExists(System.KingdomFactionName) != null", "KingdomMaster.NewWorkAllowed(System)");
		}

		[Test]
		public void SourceContract_AllocationIsCapturedBeforeNoLootInitializationAndPlacedByExactReference()
		{
			Ordered(Body(Fixture, "private GameObject Create("),
				"GameObject.Create(Blueprint, BeforeObjectCreated: body =>", "Owned.Add(body);",
				"Require(captured == null,", "captured = body;", "body.SetIntProperty(\"NoLoot\", 1);",
				"ReferenceEquals(created, captured)", "GameObject.Validate(created)", "created.Blueprint == Blueprint",
				"created.Count == 1", "created.CurrentCell == null", "created.InInventory == null", "created.Equipped == null",
				"created.GetIntProperty(\"NoLoot\") == 1", "RequireEmptyContents(created);", "RequireWorld();", "return created;");
			Ordered(Body(Fixture, "private void Place("), "RequireWorld();", "Require(EmptyCell(Cell),",
				"Cell.AddObject(Body, NoStack: true)", "ReferenceEquals(placed, Body) && ExactCell(Body, Cell)",
				"RequireEmptyContents(Body);");
			ContainsAll(Body(Fixture, "private bool ExactCell("), "ReferenceEquals(Body.CurrentCell, Cell)",
				"ReferenceEquals(Body.CurrentZone, Zone)", "Body.InInventory != null", "Body.Equipped != null",
				"ReferenceEquals(item, Body)", "return found == 1;");
			ContainsAll(Body(Fixture, "private static void RequireEmptyContents("),
				"Body.GetInventoryDirectAndEquipment()", "Body.Inventory.Objects.Count == 0", "contents.Count == 0");
		}

		[Test]
		public void SourceContract_ResidentsAreEnrolledThenPublishedWithoutPopulationOrRollFabrication()
		{
			string body = Body(Fixture, "private void Build(");
			Ordered(body, "System.City.ResidentCount == 0", "KingdomResidents.OnRollCount(System) == 0",
				"System.Population == 0", "System.Bindings.Count == 0", "System.ClaimedZones.Count == 1",
				"KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture)", "GameObject body = Create(\"NPC\");",
				"body.Brain != null && body.Body != null && body.Inventory != null", "!body.IsPlayer() && !body.IsPlayerLed()",
				"body.GetPart<r_KingdomCitizenship>() == null", "KingdomCitizenship.TryEnroll(System, body,",
				"KingdomCitizenshipEnrollmentReason.Arrival, tick, out failure)", "body.SetIntProperty(\"KingdomBorn\", 1);",
				"body.GiveProperName(name, Force: true);", "Place(body, Cells[i]);",
				"KingdomResidents.TryEnsureRow(System, body,", "ReferenceEquals(book, System.City) && id > 0",
				"Ids.Add(id);", "Verify();");
			string all = Read(Fixture) + Read(Checks) + Read(Fault);
			Assert.IsFalse(Regex.IsMatch(all, @"\.(?:Population|Founded|LastSubsidenceTick|TimeTicks)\s*(?:=(?!=)|\+=|-=|\+\+|--)"),
				"Fixture must use real enrollment/founding/Reckon, not writable compatibility or clock state.");
			Assert.IsFalse(Regex.IsMatch(all, @"\.(?:Bindings|Residents)\.(?:Add|Clear|Remove)\s*\("),
				"Resident authority must be published through its production APIs.");
		}

		[Test]
		public void SourceContract_PhysicalSurveyProvesFiftyEligibleResidentsAndZeroSupport()
		{
			ContainsAll(Body(Fixture, "private void Build("), "Cells.Count == ResidentCount + 1",
				"Store = Create(StoreBlueprint)", "ReferenceEquals(liquid.ParentObject, Store)",
				"liquid.MaxVolume == 1920 && liquid.Volume == 0", "Place(Store, Cells[ResidentCount]);");
			string body = Body(Fixture, "private void Verify(");
			Ordered(body, "Survey = KingdomSurvey.Take(Zone, System);", "ReferenceEquals(Survey.Ground, Zone)",
				"Survey.Settlers.Count == ResidentCount", "System.Population == ResidentCount",
				"KingdomResidents.OnRollCount(System) == ResidentCount", "System.City.ResidentCount == ResidentCount",
				"System.Bindings.Count == ResidentCount", "ReferenceEquals(Survey.Stores[0].ParentObject, Store)",
				"Survey.StorageCapacity == 1920 && Survey.StoredWater == 0", "ids.Add(Ids[i]) && objects.Add(body.IDIfAssigned)",
				"ExactCell(body, Cells[i]) && Survey.Settlers.Contains(body)", "KingdomResidents.IdOf(body) == Ids[i]",
				"KingdomResidentTransitionAuthority.CanPrepareResidentBodyDestruction(System, body, Ids[i])",
				"KingdomSubsidence.ScopedSupports(System, Zone, Survey)",
				"support.Water == 0 && support.Food == 0 && support.Roof == 0 && support.Lift == 0",
				"System.Shade == 0", "KingdomRules.StageFor(System.Population, Survey.StorageCapacity)",
				"stage == GrowthStage.City", "System.Stage = stage;");
			StringAssert.Contains("internal const int ResidentCount = 50;", Read(Fixture));
		}

		[Test]
		public void SourceContract_RealReckonSeedsCheckpointThenExecutesFullStepWithExactSummaryFault()
		{
			string body = Body(Checks, "internal static string Run(");
			Ordered(body, "long now = Game.TimeTicks;", "KingdomSubsidenceNativeFixture.TryCreate(",
				"long anchor = now - KingdomSubsidenceRules.StepDays * KingdomRules.TicksPerDay;",
				"KingdomSubsidence.Reckon(system, Zone, fixture.Survey, anchor);",
				"KingdomSubsidence.Reckon(system, Zone, fixture.Survey, anchor + 1L);",
				"system.LastSubsidenceTick == anchor && system.Population == 50", "Game.TimeTicks == now",
				"system.ChronicleEntries.ToArray()", "KingdomSubsidenceRules.SlideDepartureSummary(",
				"KingdomPresentation.Rich(system.KingdomDisplayName), 5, 3,",
				"KingdomSubsidenceRules.DepartureCause(system.SubsidenceBinding)",
				"fault = new KingdomSubsidenceNativeSummaryFault", "ExpectedOfficial = official",
				"ExpectedCount = officialPrefix.Length + 5", "ExpectedCheckpoint = now",
				"ExpectedDepartures = priorDepartures + 5", "player.AddPart(fault);",
				"ReferenceEquals(player.GetPart<KingdomSubsidenceNativeSummaryFault>(), fault)",
				"ReferenceEquals(fault.ParentObject, player)", "KingdomSubsidence.Reckon(system, Zone, fixture.Survey, now);",
				"Check(fault.Throws == 1 && fault.CommittedBeforeFault,");
			StringAssert.DoesNotContain("KingdomSubsidenceCompletionRules", Read(Checks));
		}

		[Test]
		public void SourceContract_DisplayNameFaultIsOneShotAfterExactOfficialAppendAndCommittedState()
		{
			StringAssert.Contains("ID == GetDisplayNameEvent.ID", Body(Fault, "public override bool WantEvent("));
			Ordered(Body(Fault, "public override bool HandleEvent(GetDisplayNameEvent E)"),
				"if (Throws == 0 && ReferenceEquals(E.Object, ParentObject) && System != null",
				"System.ChronicleEntries.Count == ExpectedCount",
				"string.Equals(System.ChronicleEntries[ExpectedCount - 1], ExpectedOfficial, StringComparison.Ordinal)",
				"Throws++;", "CommittedBeforeFault = System.LastSubsidenceTick == ExpectedCheckpoint",
				"System.Population == 45 && System.Stage == GrowthStage.City",
				"KingdomResidents.OnRollCount(System) == 45", "System.Ledger.Departures == ExpectedDepartures",
				"KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture)", "throw new InvalidOperationException(",
				"return base.HandleEvent(E);");
		}

		[Test]
		public void SourceContract_SameTickRetryRechecksPhysicalDestructionAndUnchangedChroniclePrefixes()
		{
			Ordered(Body(Checks, "internal static string Run("),
				"system.ChronicleEntries.Count == officialPrefix.Length + 5",
				"system.OutsiderEntries.Count == outsiderPrefix.Length + 4",
				"system.ChronicleEntries[system.ChronicleEntries.Count - 1] == official",
				"Prefix(system.ChronicleEntries, officialPrefix);", "Prefix(system.OutsiderEntries, outsiderPrefix);",
				"VerifyDepartures(fixture, now, priorDepartures + 5);", "current = \"same-tick-no-replay\";",
				"string[] afterOfficial = system.ChronicleEntries.ToArray();", "string[] afterOutsider = system.OutsiderEntries.ToArray();",
				"KingdomSubsidence.Reckon(system, Zone, KingdomSurvey.Take(Zone, system), now);",
				"VerifyDepartures(fixture, now, priorDepartures + 5);", "system.ChronicleEntries.Count == afterOfficial.Length",
				"system.OutsiderEntries.Count == afterOutsider.Length && fault.Throws == 1", "Game.TimeTicks == now",
				"Prefix(system.ChronicleEntries, afterOfficial);", "Prefix(system.OutsiderEntries, afterOutsider);");
			ContainsAll(Body(Checks, "private static void VerifyDepartures("), "system.Population == 45",
				"KingdomResidents.OnRollCount(system) == 45", "system.LastSubsidenceTick == Now",
				"system.Ledger.Departures == Departures", "KingdomResidentDepartureRules.IsEmpty(system.ResidentDeparture)",
				"if (!GameObject.Validate(body))", "KingdomResidents.DepartureCarriersAbsent(system, system.City, Fixture.ResidentIds[i])",
				"else Check(body.CurrentZone == Fixture.Zone && body.IsAlive", "KingdomResidents.IdOf(body) == Fixture.ResidentIds[i]",
				"absent == 5 && KingdomSurvey.Take(Fixture.Zone, system).Settlers.Count == 45");
			ContainsAll(Body(Checks, "private static void Prefix("), "Current.Count >= Expected.Length",
				"string.Equals(Current[i], Expected[i], StringComparison.Ordinal)");
		}

		[Test]
		public void SourceContract_FixtureAndEffectsAreRetainedOnlyExactTemporaryHandlerIsRemoved()
		{
			string all = Read(Fixture) + Read(Checks) + Read(Fault) + Read(Provider);
			Assert.IsFalse(Regex.IsMatch(all, @"\.(?:Obliterate|Destroy|Clear|RemoveObject|RemoveGameState)\s*\("),
				"Native fixture must retain the realm, actors, receipts and presentation effects.");
			Ordered(Body(Checks, "internal static string Run("), "finally", "if (fault != null)", "bool clean = false;",
				"ReferenceEquals(fault.ParentObject, player)",
				"ReferenceEquals(player.GetPart<KingdomSubsidenceNativeSummaryFault>(), fault)", "player.RemovePart(fault);",
				"clean = fault.ParentObject == null && player.GetPart<KingdomSubsidenceNativeSummaryFault>() == null;",
				"catch (Exception) { clean = false; }", "if (!clean)", "passed = Math.Min(passed, 2);");
			StringAssert.DoesNotContain("LastAttempt = null", Read(Fixture));
		}

		[Test]
		public void SourceContract_ReportExplicitlyLimitsNativeEvidence()
		{
			ContainsAll(Body(Checks, "internal static string Run("),
				"Ok = passed == KingdomSubsidenceNativeProvider.ExpectedCases;", "native-subsidence cases=3 passed=",
				"synthetic=true", "elapsed=seeded-checkpoint", "world-turns=untested", "world-clock=",
				"Game.TimeTicks == now ? \"unchanged\" : \"changed\"",
				"ordinary-acceptance=false", "save-load=untested", "breakpoint=not-exercised",
				"partial-departure=untested", "profile-and-effects-retained=true");
		}

		private static string Read(string Path) { return TestMain.ReadRepositoryText(Path); }
		private static string Body(string Path, string Signature)
		{
			string source = Read(Path);
			int start = source.IndexOf(Signature, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, Signature);
			int open = source.IndexOf('{', start), depth = 0;
			Assert.GreaterOrEqual(open, 0, Signature);
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0)
					return Regex.Replace(source.Substring(start, i - start + 1), @"\s+", " ");
			}
			Assert.Fail("Unclosed source method: " + Signature);
			return null;
		}
		private static void ContainsAll(string Source, params string[] Tokens)
		{
			foreach (string token in Tokens) StringAssert.Contains(token, Source);
		}
		private static void Ordered(string Source, params string[] Tokens)
		{
			int cursor = 0;
			foreach (string token in Tokens)
			{
				int at = Source.IndexOf(token, cursor, StringComparison.Ordinal);
				Assert.GreaterOrEqual(at, cursor, "Missing or reordered source contract: " + token);
				cursor = at + token.Length;
			}
		}
		private static string Setting(string Source, string Key)
		{
			string[] rows = Source.Split('\n').Select(line => line.Trim())
				.Where(line => line.StartsWith(Key + "=", StringComparison.Ordinal)).ToArray();
			Assert.AreEqual(1, rows.Length, Key);
			return rows[0].Substring(Key.Length + 1);
		}
	}
}
#endif
