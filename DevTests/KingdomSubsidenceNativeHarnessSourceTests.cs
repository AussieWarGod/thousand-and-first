#if TAF_TESTS
using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
		public void SourceContract_PersonaRunsFiveOriginalCasesAndThreeSyntheticReportCutsAcrossRealFounding()
		{
			string persona = Read(Persona);
			ClassicAssert.AreEqual("founding-first-city", Setting(persona, "REQUEST"));
			ClassicAssert.AreEqual("stagedigest;subsidence-check;stagedigest", Setting(persona, "SCRIPT"));
			ClassicAssert.AreEqual("subsidence-check", Setting(persona, "VERBS"));
			ClassicAssert.AreEqual("stagedigest:OK~founded=false,subsidence-check:OK~cases=8 passed=8 failed=0,stagedigest:OK~founded=true,COMPLETE",
				Setting(persona, "EXPECT"));
			string[] cases = Regex.Matches(Read(Checks), @"\bcurrent\s*=\s*""([^""]+)""")
				.Cast<Match>().Select(match => match.Groups[1].Value).ToArray();
			CollectionAssert.AreEqual(new[] { "physical-city-fixture", "partial-one-of-five",
				"remaining-four-summary-interruption", "same-tick-summary-recovery", "same-tick-no-replay",
				"synthetic-report-cut-recovery" }, cases);
			ClassicAssert.AreEqual(cases.Length, cases.Distinct(StringComparer.Ordinal).Count());
			ContainsAll(Read(Provider), "internal const int ExpectedCases = 8;",
				"internal const string Verb = \"subsidence-check\";");
		}

		[Test]
		public void SourceContract_AdditionalCasesDriveRealRecoveryAfterExplicitSyntheticCuts()
		{
			string loss = Read("Harness/KingdomSubsidenceNativeLossChecks.cs");
			ContainsAll(loss, "synthetic-terminal-chronicle-loss=PASS; sink-dispositions=seeded",
				"synthetic-empty-ledger-reset-aba=PASS; pending-intent-and-news-counter=seeded",
				"synthetic-unseen-homecoming-news=PASS; callback-news=seeded",
				"KingdomSubsidenceStepRuntime.TryBeforePass(", "KingdomSubsidenceStepRuntime.TryReadHomecoming(",
				"KingdomSubsidenceReportRules.Settled(report) && !KingdomSubsidenceReportRules.Complete(report)",
				"LedgerLossKind.HomecomingReset", "after.BeforeHash == before.BeforeHash",
				"frame.Notes.Add(news)", "frame.Ledger.Fetched == 1", "frame.City.SubsidenceModel == wire");
			StringAssert.DoesNotContain(".Reset(", loss);
			StringAssert.Contains("passed += KingdomSubsidenceNativeLossChecks.Run(system, Zone, fixture.Survey, now, results);", Read(Checks));
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
			ClassicAssert.IsFalse(Regex.IsMatch(all, @"\.(?:Population|Founded|LastSubsidenceTick|TimeTicks)\s*(?:=(?!=)|\+=|-=|\+\+|--)"),
				"These fixture files must not write population, founding or clock state; only the separate synthetic clock seed/probe may write its checkpoint.");
			ClassicAssert.IsFalse(Regex.IsMatch(all, @"\.(?:Bindings|Residents)\.(?:Add|Clear|Remove)\s*\("),
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
		public void SourceContract_RealReckonUsesBoundSurveyAndPreservesPartialStepCredit()
		{
			string body = Body(Checks, "internal static string Run(");
			Ordered(body, "long now = Game.TimeTicks;", "KingdomSubsidenceNativeFixture.TryCreate(",
				"scope = fixture.Survey.BindPass();",
				"long anchor = now - KingdomSubsidenceRules.StepDays * KingdomRules.TicksPerDay;",
				"KingdomSubsidenceNativeClockSeed.TrySeed(system, Zone, fixture.Survey, anchor, out failure)",
				"KingdomSubsidenceNativeClockChecks.Verify(fixture, Zone, fixture.Survey, now)",
				"system.LastSubsidenceTick == anchor && system.Population == 50", "Game.TimeTicks == now",
				"system.ChronicleEntries.ToArray()", "KingdomSubsidenceRules.SlideDepartureSummary(",
				"KingdomPresentation.Rich(system.KingdomDisplayName), 5, 3,",
				"KingdomSubsidenceRules.DepartureCause(system.SubsidenceBinding)",
				"current = \"partial-one-of-five\";", "for (int i = 1; i < fixture.Bodies.Count; i++)",
				"GameObject body = fixture.Bodies[i];",
				"GameObject.Validate(body) && body.Brain != null && body.Brain.PartyLeader == null",
				"held.Add(body); body.Brain.PartyLeader = player;", "Check(body.IsPlayerLed(),",
				"KingdomSubsidence.Reckon(system, Zone, fixture.Survey, now);",
				"KingdomSubsidenceStepCodec.TryDecode(system.City.SubsidenceModel, out KingdomSubsidenceStepBook partial)",
				"partial.Active != null && partial.Active.Completed == 1 && partial.Active.Quota == 5",
				"partial.Active.AnchorTick == anchor && partial.Active.DueTick == now",
				"partial.Active.PendingDepartureId == \"\" && system.Population == 49",
				"KingdomResidents.OnRollCount(system) == 49 && system.LastSubsidenceTick == anchor",
				"system.Ledger.Departures == priorDepartures + 1", "string stepId = partial.Active.Id;",
				"long sequence = partial.Sequence;", "ReleaseHeld(held, player, Zone);",
				"partial-one-of-five=PASS");
			StringAssert.DoesNotContain("KingdomSubsidenceCompletionRules", Read(Checks));
		}

		[Test]
		public void SourceContract_RemainingFourRetireSameSequenceBeforeSummaryDeclarationFault()
		{
			Ordered(Body(Checks, "internal static string Run("), "current = \"remaining-four-summary-interruption\";",
				"player.GetPart<KingdomSubsidenceNativeSummaryFault>() == null",
				"fault = new KingdomSubsidenceNativeSummaryFault", "ExpectedOfficial = official",
				"ExpectedCount = officialPrefix.Length + 5", "ExpectedCheckpoint = now",
				"ExpectedDepartures = priorDepartures + 5", "player.AddPart(fault);",
				"ReferenceEquals(player.GetPart<KingdomSubsidenceNativeSummaryFault>(), fault)",
				"ReferenceEquals(fault.ParentObject, player)", "KingdomSubsidence.Reckon(system, Zone, fixture.Survey, now);",
				"Check(fault.Throws == 1 && fault.CommittedBeforeFault,",
				"system.ChronicleEntries.Count == officialPrefix.Length + 4",
				"system.OutsiderEntries.Count == outsiderPrefix.Length + 4",
				"KingdomSubsidenceStepCodec.TryDecode(system.City.SubsidenceModel, out KingdomSubsidenceStepBook settled)",
				"settled.Active == null && settled.Sequence == sequence && settled.LastRetiredTick == now",
				"KingdomSubsidenceBatchCodec.TryDecode(settled.BatchModel, out KingdomSubsidenceBatch batch)",
				"batch.Closing && batch.Departed == 5 && batch.FirstSequence == sequence",
				"Prefix(system.ChronicleEntries, officialPrefix);", "Prefix(system.OutsiderEntries, outsiderPrefix);",
				"VerifyDepartures(fixture, now, priorDepartures + 5);",
				"!string.IsNullOrEmpty(stepId)", "remaining-four-summary-interruption=PASS");
		}

		[Test]
		public void SourceContract_DisplayNameFaultIsOneShotBeforeExactFrozenSummaryAppend()
		{
			StringAssert.Contains("ID == GetDisplayNameEvent.ID", Body(Fault, "public override bool WantEvent("));
			Ordered(Body(Fault, "public override bool HandleEvent(GetDisplayNameEvent E)"),
				"if (Throws == 0 && ReferenceEquals(E.Object, ParentObject) && System != null",
				"System.ChronicleEntries.Count == ExpectedCount - 1",
				"KingdomSubsidenceStepCodec.TryDecode(System.City.SubsidenceModel, out KingdomSubsidenceStepBook book)",
				"book.Active == null",
				"KingdomSubsidenceBatchCodec.TryDecode(book.BatchModel, out KingdomSubsidenceBatch batch)",
				"batch.Closing && batch.Departed == 5",
				"KingdomSubsidenceReportCodec.TryDecode(batch.ReportModel, out KingdomSubsidenceReportPlan report)",
				"report.Entries.Count == 1 && !report.Entries[0].ChronicleProved",
				"report.Entries[0].LedgerPhase == ReportLedgerPhase.Proved",
				"Calendar.GetDay(report.Entries[0].AtTick)", "Calendar.GetMonth(report.Entries[0].AtTick)",
				"Calendar.GetYear(report.Entries[0].AtTick)", "report.Entries[0].Text + \".\", ExpectedOfficial, StringComparison.Ordinal)",
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
				"current = \"same-tick-summary-recovery\";",
				"KingdomSubsidence.Reckon(system, Zone, fixture.Survey, now);",
				"VerifyDepartures(fixture, now, priorDepartures + 5);",
				"system.ChronicleEntries.Count == officialPrefix.Length + 5",
				"system.OutsiderEntries.Count == outsiderPrefix.Length + 5",
				"system.ChronicleEntries[system.ChronicleEntries.Count - 1] == official",
				"KingdomSubsidenceStepCodec.TryDecode(system.City.SubsidenceModel, out KingdomSubsidenceStepBook recovered)",
				"recovered.Active == null && recovered.Sequence == sequence",
				"recovered.BatchModel == KingdomSubsidenceBatchRules.None", "same-tick-summary-recovery=PASS",
				"current = \"same-tick-no-replay\";",
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
			ClassicAssert.AreEqual(1, Regex.Matches(all, @"\bHeld\.Clear\(\);").Count,
				"Only the transient exact-reference party list may be cleared.");
			ClassicAssert.IsFalse(Regex.IsMatch(all.Replace("Held.Clear();", ""),
				@"\.(?:Obliterate|Destroy|Clear|RemoveObject|RemoveGameState)\s*\("),
				"Native fixture must retain the realm, actors, receipts and presentation effects.");
			Ordered(Body(Checks, "internal static string Run("), "finally", "if (fault != null)", "bool clean = false;",
				"ReferenceEquals(fault.ParentObject, player)",
				"ReferenceEquals(player.GetPart<KingdomSubsidenceNativeSummaryFault>(), fault)", "player.RemovePart(fault);",
				"clean = fault.ParentObject == null && player.GetPart<KingdomSubsidenceNativeSummaryFault>() == null;",
				"catch (Exception) { clean = false; }", "if (!clean)",
				"passed = Math.Min(passed, KingdomSubsidenceNativeProvider.ExpectedCases - 1);",
				"try { ReleaseHeld(held, player, Zone); }",
				"catch (Exception) { passed = Math.Min(passed, 4);", "party-cleanup=FAIL unknown custody retained",
				"scope?.Dispose();");
			Ordered(Body(Checks, "private static void ReleaseHeld("), "foreach (GameObject body in Held)",
				"GameObject.Validate(body) && body.CurrentZone == Zone && body.Brain != null",
				"ReferenceEquals(body.Brain.PartyLeader, Player)", "body.Brain.PartyLeader = null;", "Held.Clear();");
			StringAssert.DoesNotContain("LastAttempt = null", Read(Fixture));
		}

		[Test]
		public void SourceContract_ReportExplicitlyLimitsNativeEvidence()
		{
			ContainsAll(Body(Checks, "internal static string Run("),
				"Ok = passed == KingdomSubsidenceNativeProvider.ExpectedCases;", "native-subsidence cases=8 passed=",
				"synthetic=true", "elapsed=seeded-checkpoint", "world-turns=untested", "world-clock=",
				"Game.TimeTicks == now ? \"unchanged\" : \"changed\"",
				"ordinary-acceptance=false", "save-load=untested", "breakpoint=not-exercised",
				"partial-departure=one-plus-four", "profile-and-effects-retained=true");
		}

		[Test]
		public void SourceContract_ExpectedDiagnosticIsExactDeclarationRefusalNotOldCompletionError()
		{
			string expected = Setting(Read(Persona), "LOG_EXPECT");
			string[] lines = Regex.Matches(expected, @"""([^""]+)""")
				.Cast<Match>().Select(match => match.Groups[1].Value).ToArray();
			CollectionAssert.AreEqual(new[] {
				"[TAF] chronicle v3 refused 8:receipt-declaration",
				"MODWARN [Pets of Harvest Dawn] - Mod defining manual load order, please convert it to use the Dependencies field.",
				"MODWARN [Pets of Harvest Dawn] - XmlDataHelper:: <...>/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/DLC/PetsPack1/Freehold_Pet_Ercolano/PopulationTables.xml line 4 char 6"
			}, lines);
			ContainsAll(Body("Chronicle/KingdomChronicle.cs", "private static bool TryDeclareCore("),
				"FounderName()", "catch { return false; }");
			ContainsAll(Body("Chronicle/KingdomChronicle.Publication.cs", "private static bool RecordOnceCore("),
				"PublicationFault(KingdomChronicleRegistryFault.CryptoUnavailable, \"receipt-declaration\", true, OwnerExact)");
			StringAssert.Contains("CryptoUnavailable = 8", Read("Chronicle/KingdomChronicleReceiptRules.cs"));
			string report = Body("Chronicle/KingdomChronicle.Registry.cs", "private static void ReportFault(");
			ContainsAll(report, "KingdomLog.Log(\"chronicle v3 refused \" + code)");
			StringAssert.DoesNotContain("LogError", report);
			ContainsAll(Body("Core/KingdomLog.cs", "public static void Log("),
				"if (Enabled)", "UnityEngine.Debug.Log(\"[TAF] \" + Text)");
			StringAssert.Contains("\"r_TAF_OptionDevLog\": \"Yes\"", Read("Tools/smoke/PlayerOptions.json"));
			StringAssert.DoesNotContain("departure summary failed", Read(Persona));
		}

		private static string Read(string Path) { return TestMain.ReadRepositoryText(Path); }
		private static string Body(string Path, string Signature)
		{
			string source = Read(Path);
			int start = source.IndexOf(Signature, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, Signature);
			int open = source.IndexOf('{', start), depth = 0;
			ClassicAssert.GreaterOrEqual(open, 0, Signature);
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
				ClassicAssert.GreaterOrEqual(at, cursor, "Missing or reordered source contract: " + token);
				cursor = at + token.Length;
			}
		}
		private static string Setting(string Source, string Key)
		{
			string[] rows = Source.Split('\n').Select(line => line.Trim())
				.Where(line => line.StartsWith(Key + "=", StringComparison.Ordinal)).ToArray();
			ClassicAssert.AreEqual(1, rows.Length, Key);
			return rows[0].Substring(Key.Length + 1);
		}
	}
}
#endif
