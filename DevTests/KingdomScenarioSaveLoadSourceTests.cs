#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	// Source contracts only. Native save, stopped-process import and load journals are separate gates.
	[TestFixture]
	public sealed class KingdomScenarioSaveLoadSourceTests
	{
		private const string Save = "Harness/KingdomScenarioSaveChecks.cs";
		private const string Files = "Harness/KingdomScenarioSaveFiles.cs";
		private const string Load = "Harness/KingdomScenarioLoadEntry.cs";
		private const string Witness = "Harness/KingdomScenarioLoadWitness.cs";
		private const string Menu = "Harness/KingdomScenarioTestGameEntry.cs";
		private const string Authority = "Harness/KingdomScenarioSaveAuthorityChecks.cs";
		private const string HeartAuthority = "Harness/KingdomScenarioSaveAuthorityHeart.cs";

		[Test]
		public void SourceContract_CurrentD5EnvelopeCannotRelabelAHistoricalSs4Save()
		{
			string versions = Read("Harness/KingdomSubsidenceRungSaveSnapshotCodec.Versions.cs");
			Contains(versions, "Prefix = \"taf-rung-save-v2:\"", "LegacyPrefix = \"taf-rung-save-v1:\"",
				"StepWirePrefix = \"ss5:\"", "LegacyStepWirePrefix = \"ss4:\"", "RungWirePrefix = \"sr2:\"");
			string codec = Read("Harness/KingdomSubsidenceRungSaveSnapshotCodec.cs");
			Contains(codec, "int version = StepVersion(value?.StepWire)", "writer.Write(Magic); writer.Write(version)",
				"VersionPrefix(version) + Convert.ToBase64String", "int version = EnvelopeVersion(wire)",
				"reader.ReadInt32() != version", "!Valid(decoded, version)", "canonical != wire");
			Ordered(Read("Harness/KingdomSubsidenceRungSaveChecks.cs"), "TryEncode(snapshot, out string wire)",
				"MatchesCurrentPrefix(wire)", "Game.SetStringGameState(KingdomScenarioSaveFiles.SnapshotKey, wire)");
		}

		[Test]
		public void SourceContract_Ss5JournalClaimsFollowTheExactCurrentNativeSnapshotProof()
		{
			string rung = Read("Harness/KingdomSubsidenceRungLoadWitness.cs");
			Contains(rung, "exact ss5/sr2 bytes and 35 loaded bodies", "native-write-cut=2; wear-fields=10",
				"MatchesCurrentPrefix(KingdomScenarioLoadEntry.SnapshotWire)", "System.City.SubsidenceModel == Snapshot.StepWire");
			Ordered(rung, "KingdomSubsidenceRungPlan plan = Frozen(system, snapshot)", "Receipt(system, zone, snapshot, plan)",
				"KingdomScenarioJournal.Append(\"LOAD-PREACTIVATION\", true, PreactivationLine)");
			Ordered(Read(Witness), "snapshot.StepWire.StartsWith(\"ss5:\", StringComparison.Ordinal)",
				"KingdomSubsidenceStepCodec.TryDecode(snapshot.StepWire", "exact ss5 bytes and 49 loaded bodies");
			Contains(Read("Tools/verify-scenario-rung-load.py"), "exact ss5/sr2 bytes and 35 loaded bodies");
			Contains(Read("Tools/verify-scenario-load.py"), "exact ss5 bytes and 49 loaded bodies");
		}

		[Test]
		public void SourceContract_RungHeartProofPrecedesTheSuccessfulPreactivationJournal()
		{
			string source = Read("Harness/KingdomSubsidenceRungLoadWitness.cs");
			string prefix = Between(source, "internal static void Prefix()", "internal static string VerifyRecovered(");
			Ordered(prefix, "KingdomScenarioSaveAuthorityChecks.VerifyExact(system, zone)",
				"KingdomSubsidenceRungLoadHeartWitness.Arm(game, system, zone, snapshot, plan)",
				"WitnessedGame = game", "KingdomScenarioJournal.Append(\"LOAD-PREACTIVATION\", true, PreactivationLine)");
			string heart = Read("Harness/KingdomSubsidenceRungSaveHeartProof.cs");
			Contains(heart, "Heart.GetPart<r_KingdomWear>() == null", "WearCopies(Heart) == 0",
				"KingdomSubsidenceRungSaveShape.TryMatch(", "CompanionPublishedIntent",
				"CompanionPublishedReleased");
			ClassicAssert.IsFalse(Regex.IsMatch(heart, @"\.(?:AddPart|RequirePart|RemovePart|Destroy|Obliterate)\s*\("));
		}

		[Test]
		public void SourceContract_CompanionObserverCannotCutOrConflateThePrimaryRelease()
		{
			string source = Read("Harness/KingdomSubsidenceRungReleaseCut.cs");
			Contains(source, "string companionWorkObjectId = null",
				"companionWorkObjectId == null || mode == KingdomSubsidenceRungReleaseCutMode.Observe",
				"string.CompareOrdinal(workObjectId, companionWorkObjectId) < 0",
				"plan.Works[index].ObjectId != objectId", "ObservedIndex == index",
				"Published(next, ObservedIndex)", "Wrote(field, ObservedIndex)",
				"CompanionObjectId = null", "CompanionPublishedReleased = false");
			string writes = Between(source, "private static void Wrote(", "private static void Published(");
			Ordered(writes, "if (index != Index)", "if (Subject(index) == null) return",
				"CompanionWrites++", "return;", "Writes++", "throw new KingdomSubsidenceRungReleaseCutException()");
			string published = Between(source, "private static void Published(", "private static void Require(");
			Ordered(published, "next.Works[index].ObjectId != objectId", "book.Active.RungModel != canonical",
				"if (index == Index)", "PublishedReleased = true", "CompanionPublishedReleased = true");
		}

		[Test]
		public void SourceContract_SavePersonaIsTheFreshFirstLegAndMakesNoReloadClaim()
		{
			Contains(Read("Tools/personas/subsidence-save-native-check.persona"),
				"First leg only", "No reload claim", "REQUEST=founding-first-city", "START=8.22@40,12",
				"SCRIPT=stagedigest;subsidence-save-check;stagedigest", "VERBS=subsidence-save-check",
				"stagedigest:OK~founded=false", "subsidence-save-check:OK~cases=1 passed=1 failed=0",
				"stagedigest:OK~founded=true,COMPLETE", "SET=growth,native-regression,save-load");
			Contains(Read(Save), "synthetic-setup=true", "load=unproved", "ordinary-acceptance=false");
		}

		[Test]
		public void SourceContract_ActualOneOfFiveDeparturePrecedesPartyReleaseAndSnapshot()
		{
			string source = Read(Save);
			Ordered(source, "KingdomSubsidenceNativeFixture.TryCreate(",
				"now - KingdomSubsidenceStepRules.StepTicks", "hold.Brain.PartyLeader = player",
				"KingdomSubsidence.TryReckon(system, Zone, fixture.Survey, now, out failure)",
				"book.Active.Completed == 1", "book.Active.Quota == 5", "Release(held, player, Zone)",
				"new KingdomScenarioSaveSnapshot(", "Game.SaveGame(\"Primary\")");
			Contains(source, "!finished", "!GameObject.Validate(departing)",
				"KingdomResidents.DepartureCarriersAbsent(system, system.City, missingResident)",
				"system.Population == 49", "KingdomResidents.OnRollCount(system) == 49",
				"system.Ledger.Departures == departures + 1", "system.LastSubsidenceTick == anchor",
				"book.Sequence == 1", "book.Active.AnchorTick == anchor", "book.Active.DueTick == now",
				"book.Active.PendingDepartureId == \"\"", "KingdomResidentDepartureRules.IsEmpty(system.ResidentDeparture)");
		}

		[Test]
		public void SourceContract_ProfileAndHeartBeforeStateAreCheckedAcrossSaveLoadAndReconciliation()
		{
			Ordered(Read(Save), "KingdomSubsidenceNativeFixture.TryCreate(", "fixture.Survey.BindPass()",
				"KingdomResidentIdentity.Reconcile(system, fixture.Survey.Settlers)",
				"system.SpeciesCounts.TryGetValue(\"human\", out int counted) && counted == 50",
				"KingdomScenarioSaveAuthorityChecks.Prepare(system, Zone)", "KingdomSubsidenceNativeClockSeed.TrySeed(",
				"KingdomSubsidenceNativeClockChecks.Verify(",
				"Release(held, player, Zone)", "KingdomScenarioSaveAuthorityChecks.Capture(system, Zone)",
				"Game.SaveGame(\"Primary\")", "KingdomScenarioSaveAuthorityChecks.VerifyExact(system, Zone)");
			Ordered(Read(Witness), "KingdomScenarioSaveAuthorityChecks.VerifyExact(system, zone)",
				"WitnessedGame = game", "KingdomScenarioSaveAuthorityChecks.VerifyReconciled(system, zone)",
				"KingdomSubsidenceStepRuntime.TryBeforePass(");
			Ordered(Read("Harness/KingdomSubsidenceNativeFixture.cs"), "GameObject body = Create(\"NPC\")",
				"body.SetStringProperty(\"Species\", \"human\")", "body.GetSpecies() == \"human\"",
				"KingdomCitizenship.TryEnroll(");
		}

		[Test]
		public void SourceContract_AuthoritySnapshotUsesExactRawProfileAndSevenTypedHeartRows()
		{
			string source = Read(Authority);
			Contains(source, "KingdomPolityCodec.EncodeEnvelope(Owner.Ledger)", "sha.ComputeHash(envelope)",
				"SortedDictionary<string, string>(StringComparer.Ordinal)", "expected.Remove(row.Key)",
				"rows.Count == 7 && expected.Count == 0", "KingdomScenarioDurableState.ProvesExactText(row.Key, row.Value)",
				"NoReservations(Owner.Game.IntGameState)", "NoReservations(Owner.Game.Int64GameState)",
				"NoReservations(Owner.Game.ObjectGameState)", "NoReservations(Owner.Game.BooleanGameState)",
				"KingdomPlots.FoundingHeartReceiptProperty", "KingdomPlots.FoundingHeartSealProperty",
				"SettlementIdentityFirstClaimedZone", "SettlementIdentityOrigin", "SettlementIdentityTransactionId",
				"if (Value == null) { Text.Append(\"-1:\"); return; }",
				"Wire.Length != Header.Length + 131", "lines.Length == 4", "lines[3] == \"\"",
				"PrimarySHA protects this saved baseline; the external snapshot does not independently",
				"KingdomPolityProfileRules.IsCommittedLegacyProfileSchema(record.ProfileSchema)");
			Ordered(source, "internal static void Capture(", "!KingdomNativeRegressionContext.HasAnyState(",
				"string wire = CurrentWire(owner)", "owner.Game.SetStringGameState(StateKey, wire)",
				"VerifyExact(System, Zone)");
			Ordered(source, "internal static void VerifyReconciled(", "CanonicalProfile(owner, out revision)",
				"KingdomPlots.AuditFoundingHeartReservations(System, Zone)",
				"KingdomSealProfileCaptureRules.StillMatches(");
		}

		[Test]
		public void SourceContract_CurrentWireProvesCanonicalProfileBeforeHashingAndRechecksSameRevisionAfterward()
		{
			string source = Read(Authority);
			string canonical = Between(source, "private static KingdomSealRecord CanonicalProfile(",
				"private static string CurrentWire(");
			Ordered(canonical, "Owner.Check()", "KingdomSealRecord record = new KingdomSealRecord()",
				"Require(KingdomSealProfileCaptureRules.TryCapture(Owner.Ledger, Owner.Realm, record, out Revision, out failure)",
				"Require(KingdomPolityProfileRules.IsCommittedLegacyProfileSchema(record.ProfileSchema)",
				"Owner.Check()", "return record");
			string current = Between(source, "private static string CurrentWire(", "private static string HeartDigest(");
			Ordered(current, "Owner.Check()", "KingdomSealRecord record = CanonicalProfile(Owner, out revision)",
				"KingdomPolityCodec.EncodeEnvelope(Owner.Ledger)", "sha.ComputeHash(envelope)", "HeartDigest(Owner)",
				"Owner.Check()", "Require(KingdomSealProfileCaptureRules.StillMatches(Owner.Ledger, Owner.Realm, record, revision, out failure)",
				"Owner.Check()", "return Header + \"\\n\" + profile + \"\\n\" + heart + \"\\n\"");
		}

		[Test]
		public void SourceContract_ExactAuthorityObservationCannotReconcileRepairOrPublishItsBeforeState()
		{
			string source = Read(Authority);
			string exact = Between(source, "internal static void VerifyExact(", "internal static void VerifyReconciled(");
			Ordered(exact, "ValidWire(wire)", "string current = CurrentWire(owner)", "owner.Check()",
				"KingdomScenarioDurableState.ProvesExactText(StateKey, wire)", "string.Equals(wire, current, StringComparison.Ordinal)");
			string[] observation = { exact,
				Between(source, "private static KingdomSealRecord CanonicalProfile(", "private static string CurrentWire("),
				Between(source, "private static string CurrentWire(", "private static string HeartDigest("),
				Between(source, "private static string HeartDigest(", "private static void NoReservations<"),
				Read(HeartAuthority) };
			foreach (string body in observation)
			{
				ClassicAssert.IsFalse(Regex.IsMatch(body, @"\b(?:TryReconcile|AuditFoundingHeartReservations|RecoverFoundingHeart|EnsureFoundingHeartProjection|VerifyReconciled|TryVerifyComplete|Quarantine\w*|Prepare|Capture|TryPublish|Set\w*GameState|Remove\w*GameState|Set\w*Property|Remove\w*Property|AddPart|RemovePart|RequirePart)\s*\("));
				ClassicAssert.IsFalse(Regex.IsMatch(body, @"\b(?:Owner|owner)\.(?:Game|System|Ledger|Zone)\.[\w.]+\s*=(?!=)"));
				ClassicAssert.IsFalse(Regex.IsMatch(body, @"\b(?:Final|final|Body|body)\.[\w.]+(?:\[[^\]]*\])?\s*=(?!=)"));
				ClassicAssert.IsFalse(Regex.IsMatch(body, @"\.(?:Clear|Reset|Destroy|Obliterate)\s*\("));
				StringAssert.DoesNotContain("KingdomScenarioCompletedHeart.Complete(", body);
				StringAssert.DoesNotContain("KingdomPlots.Advance(", body);
			}
			Contains(Between(source, "internal static void Prepare(", "internal static void Capture("),
				"KingdomPolityProfileRuntime.TryReconcile(");
		}

		[Test]
		public void SourceContract_V2HeartDigestIncludesTheObservedTerminalBeforeHashingWithoutExternalCommitClaim()
		{
			string source = Read(Authority);
			Contains(source, "internal static partial class KingdomScenarioSaveAuthorityChecks",
				"StateKey = \"r_TAF_ScenarioSaveAuthority_v2\"", "Header = \"taf-scenario-save-authority-v2\"",
				"PrimarySHA protects this saved baseline; the external snapshot does not independently",
				"commit these authority fields", "not historical-save compatibility proof");
			Contains(Read(HeartAuthority), "internal static partial class KingdomScenarioSaveAuthorityChecks");
			Ordered(Between(source, "private static string HeartDigest(", "private static void NoReservations<"),
				"Field(text, \"taf-scenario-founding-heart-v2\")", "Field(text, row.Key); Field(text, row.Value)",
				"Field(text, Owner.System.SettlementIdentityTransactionId)", "AppendCompletedHeart(text, Owner)",
				"Owner.Check()", "KingdomScenarioSaveFiles.HashText(text.ToString())");
			StringAssert.DoesNotContain("\"r_TAF_ScenarioSaveAuthority_v1\"", source);
			StringAssert.DoesNotContain("\"taf-scenario-save-authority-v1\"", source);
			StringAssert.DoesNotContain("\"taf-scenario-founding-heart-v1\"", source);
		}

		[Test]
		public void SourceContract_CompletedHeartObservationRequiresCanonicalOwnerTerminalAndTypedPhysicalMirror()
		{
			string source = Read(HeartAuthority);
			Contains(source, "KingdomFoundingHeartRules.TryDecode(receipt, out KingdomFoundingHeartPlan plan)",
				"KingdomFoundingHeartRules.Complete(plan)", "KingdomFoundingHeartRules.Encode(plan) == receipt",
				"plan.TransactionId == Owner.Transaction && plan.ZoneId == Owner.ZoneId",
				"KingdomFoundingHeartTerminalRules.Encode(terminal) == raw",
				"terminal.Phase == KingdomFoundingHeartTerminalPhase.EffectsSettled",
				"terminal.Raising == KingdomFoundingHeartSinkDisposition.Settled",
				"terminal.Heart == KingdomFoundingHeartSinkDisposition.Settled",
				"terminal.TransactionId == Owner.Transaction && terminal.ZoneId == Owner.ZoneId",
				"terminal.CompletionSeal == seal && terminal.PlotId == plan.PlotId",
				"terminal.PredecessorId == KingdomFoundingHeartRules.SlotId(plan, KingdomFoundingHeartRules.WorksSlot)",
				"terminal.FinalId == KingdomFoundingHeartRules.StableId(Owner.Transaction, Owner.ZoneId, \"final\")",
				"terminal.BuildKey == stake.BuildKey && terminal.Blueprint == stake.Blueprint",
				"terminal.X == frozen.MainWorldX && terminal.Y == frozen.MainWorldY",
				"CompletedHeartText(Text, final, KingdomPlots.FoundingHeartTerminalProperty, raw)",
				"CompletedHeartText(Text, final, r_KingdomScaffold.RemovalProofProperty, terminal.PredecessorId)",
				"r_KingdomScaffold.HasRemovalProof(final, terminal.PredecessorId)",
				"CompletedHeartInt(Text, final, \"KingdomBuilt\", 1)",
				"Body.HasStringProperty(Key) && !Body.HasIntProperty(Key) && Body.GetStringProperty(Key) == Expected",
				"Body.HasIntProperty(Key) && !Body.HasStringProperty(Key) && Body.GetIntProperty(Key) == Expected",
				"ReferenceEquals(strings, final.Property) && ReferenceEquals(ints, final.IntProperty)");
			Contains(source, "KingdomPlots.TryDecodePlotPayload(plan.Payload", "KingdomFoundingHeartStakeRules.TryDecode(plan.StakeTruth",
				"KingdomArchitectureStamper.TryReadOwner(Final", "observed.EncodedSnapshot == Frozen.EncodedSnapshot",
				"observed.SnapshotHash == Frozen.SnapshotHash", "SameHeartRect(observed.Rect, Frozen.Rect)",
				"KingdomArchitectureStamper.NextLayerProperty, 3", "Component simulation is not independently reproved here",
				"verifier can quarantine on failure", "later real audit, not capture");
		}

		[Test]
		public void SourceContract_CompletedHeartDigestBindsUniqueLiveEndpointAndAllSavedRootAbsenceShapes()
		{
			string source = Read(HeartAuthority);
			Contains(Between(source, "private static GameObject CompletedHeartLive(", "private static void AppendAbsentHeartRoot("),
				"KingdomConstruction.FindGlobalLiveId(Terminal.FinalId, out GameObject final)",
				"KingdomPhysicalLookupState.Exact && GameObject.Validate(final)",
				"final.IDIfAssigned == Terminal.FinalId && final.Blueprint == Terminal.Blueprint",
				"ReferenceEquals(final.CurrentZone, Owner.Zone)", "ReferenceEquals(final.CurrentCell, Owner.Zone.GetCell(Terminal.X, Terminal.Y))",
				"final.InInventory == null && final.Equipped == null && final.Count == 1", "bodies.Count <= 65536",
				"body.IDIfAssigned == Terminal.FinalId", "ReferenceEquals(body, final)", "copies == 1",
				"List<GameObject> cellBodies = final.CurrentCell.GetObjects()", "cellBodies.Count <= 65536",
				"foreach (GameObject body in cellBodies) if (ReferenceEquals(body, final)) inCell++", "inCell == 1");
			Ordered(Between(source, "private static void AppendCompletedHeart(", "private static void AppendCompletedArchitecture("),
				"Field(Text, KingdomPlots.FoundingHeartTerminalProperty); Field(Text, raw)",
				"Field(Text, terminal.PredecessorId); Field(Text, final.IDIfAssigned)",
				"Field(Text, final.Blueprint); Field(Text, final.CurrentZone.ZoneID)",
				"KingdomConstruction.FindGlobalLiveId(terminal.PredecessorId, out _)", "KingdomPhysicalLookupState.Absent",
				"Field(Text, \"live-predecessor-absent\"); Field(Text, terminal.PredecessorId)",
				"slot < KingdomFoundingHeartRules.SlotCount", "KingdomPlots.FoundingHeartRootPrefix",
				"KingdomPlots.FoundingHeartFinalRootPrefix + terminal.FinalId", "\"r_TAF_PlotFinalRoot:\" + terminal.FinalId");
			Contains(source, "!KingdomNativeRegressionContext.HasAnyState(Owner.Game, Key)",
				"Field(Text, Key); Field(Text, \"absent-across-five-tables\")");
		}

		[Test]
		public void SourceContract_ColdLoadObservesSavedAuthorityBeforeWitnessSuccessAndAnyReconciliation()
		{
			string source = Read(Witness);
			string prefix = Between(source, "internal static void Prefix(GameObject __instance, string __0)",
				"internal static string VerifyRecovered(");
			Ordered(prefix, "if (!KingdomScenarioLoadEntry.Armed || __0 != \"GameRestored\") return",
				"ReferenceEquals(__instance, The.Player)", "system.City.SubsidenceModel == snapshot.StepWire",
				"KingdomScenarioSaveAuthorityChecks.VerifyExact(system, zone)", "Bodies.Add(body)", "WitnessedGame = game",
				"KingdomScenarioJournal.Append(\"LOAD-PREACTIVATION\", true");
			ClassicAssert.IsFalse(Regex.IsMatch(prefix, @"\b(?:VerifyReconciled|TryReconcile|AuditFoundingHeartReservations|RecoverFoundingHeart|Prepare)\s*\("));
			Ordered(Between(source, "internal static string VerifyRecovered(", "private static void VerifyAfter("),
				"Failure == null && Attempts == 1 && ReferenceEquals(WitnessedGame, Game)",
				"KingdomScenarioSaveAuthorityChecks.VerifyReconciled(system, zone)", "KingdomSubsidenceStepRuntime.TryBeforePass(");
		}

		[Test]
		public void SourceContract_ExactSnapshotPublishesDurablyAndExternallyBeforeRealSave()
		{
			Ordered(Read(Save), "KingdomScenarioSaveSnapshotCodec.TryEncode(snapshot, out string wire)",
				"Game.SetStringGameState(KingdomScenarioSaveFiles.SnapshotKey, wire)",
				"KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey, wire)",
				"KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile), wire)",
				"The.ZoneManager.CheckCached(true, true)", "Game.SaveGame(\"Primary\")",
				"system.City.SubsidenceModel == snapshot.StepWire",
				"KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey, wire)");
			Contains(Read(Save), "!File.Exists(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile))",
				"!KingdomNativeRegressionContext.HasAnyState(Game, KingdomScenarioSaveFiles.SnapshotKey)",
				"!fixture.Bodies[i].IsPlayerLed()", "fixture.Bodies[i].CurrentZone == Zone");
		}

		[Test]
		public void SourceContract_SaveReceiptMeasuresFilesInsteadOfTrustingTaskReturn()
		{
			Ordered(Read(Save), "KingdomScenarioSaveFiles.SaveDirectory(root, Game.GameID)",
				"Directory.GetDirectories(directory).Length == 0", "foreach (string existing in Directory.GetFiles(directory))",
				"Path.GetFileName(existing) == \"Cache.db\"", "Game.SaveGame(\"Primary\")",
				"Directory.GetFiles(directory).Length == 3 && Directory.GetDirectories(directory).Length == 0",
				"File.Exists(Path.Combine(directory, \"Cache.db\"))",
				"file.ReadByte() == 31 && file.ReadByte() == 139", "string primaryHash = KingdomScenarioSaveFiles.HashFile(",
				"string infoHash = KingdomScenarioSaveFiles.HashFile(", "\"taf-scenario-save-v1\\n\" + Game.GameID",
				"KingdomScenarioSaveFiles.HashText(wire)",
				"KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), receipt)",
				"KingdomScenarioSaveFiles.ReadText(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), 512) == receipt");
			Contains(Read(Save), "\"Primary.sav.gz\"", "\"Primary.json\"", "Cache.db remains engine-owned",
				"stopped-profile copier hashes it after shutdown");
		}

		[Test]
		public void SourceContract_SaveEvidenceUsesBoundedSinglyOwnedFilesAndCreateNewWrites()
		{
			Contains(Read(Files), "PlatformID.Win32NT", "StartsWith(\"taf-scenario.\", StringComparison.Ordinal)",
				"Same(XRLCore.SavePath, Path.Combine(root, \"Save\"))",
				"Same(XRLCore.LocalPath, Path.Combine(root, \"Local\"))",
				"Same(XRLCore.SyncedPath, Path.Combine(root, \"Synced\"))",
				"Guid.TryParseExact(GameId, \"D\", out id)", "id.ToString(\"D\") == GameId",
				"saves.Length == 1", "FileAttributes.ReparsePoint", "info.Length > 0 && info.Length <= MaxBytes",
				"FileMode.Open, FileAccess.Read, FileShare.Read", "GetFileInformationByHandleEx(",
				"facts.NumberOfLinks == 1", "!facts.DeletePending && !facts.Directory", "file.Length == info.Length",
				"new UTF8Encoding(false, true)", "FileMode.CreateNew, FileAccess.Write, FileShare.None", "file.Flush(true)");
		}

		[Test]
		public void SourceContract_LoadFilePresenceClaimsTheMenuBeforeAnyNewGameFallback()
		{
			Contains(Read(Files), "return File.Exists(path) || Directory.Exists(path)");
			Ordered(Read(Load), "if (!KingdomScenarioSaveFiles.LoadPresent()) return false",
				"if (!Barrier.TryClaim()) return true", "SavesAPI.HasSavedGameInfo()",
				"KingdomScenarioLoadMenuPatch.Installed(target)", "Keyboard.PushMouseEvent(\"Pick:Continue\")", "return true");
			string start = Between(Read(Load), "internal static bool TryStart()", "internal static bool Dispatch(");
			ClassicAssert.IsFalse(start.Contains("Load()") || start.Contains("Task.Run") || start.Contains("Barrier.Start"));
			Ordered(Read(Menu), "if (AutostartConsumed || Menu == null) return",
				"if (KingdomScenarioLoadEntry.TryStart()) { AutostartConsumed = true; return; }",
				"if (!KingdomScenarioScript.Present()) return", "selected.Invoke(Menu, new object[] { Row })");
		}

		[TestCase("Qud.UI.SaveManagement", "ContinueMenu")]
		[TestCase("XRLCore", "SaveManagement")]
		public void SourceContract_ExactContinueHooksUseTheCoreCallerBarrier(string owner, string method)
		{
			string source = Read(Load);
			Contains(source, "AccessTools.Method(typeof(" + owner + "), \"" + method + "\", Type.EmptyTypes)",
				"method.ReturnType != typeof(Task<XRLGame>)", "Harmony.GetPatchInfo(Target)",
				"patch.PatchMethod == expected", "return KingdomScenarioLoadEntry.Dispatch(ref __result)");
			string dispatch = Between(source, "internal static bool Dispatch(", "private static void Refuse(");
			Ordered(dispatch, "if (!Barrier.Claimed && !KingdomScenarioSaveFiles.LoadPresent()) return true", "Result = Barrier.Pending",
				"Barrier.Claimed && Thread.CurrentThread == XRLCore.CoreThread", "Result = Barrier.Start(Load)",
				"catch (Exception error) { Result = Barrier.Pending; Refuse(error); }", "return false");
			Contains(source, "private static async Task Load()", "await The.UiContext", "Task.Run(() => XRLGame.LoadGame(");
			ClassicAssert.IsFalse(source.Contains("async void") || source.Contains(".Wait(") || source.Contains(".Running ="));
		}

		[Test]
		public void SourceContract_AllImportedSaveHashesAndExactSelectionPrecedeLoadGame()
		{
			Contains(Read(Load), "\"exact sealed save; game-id=\" + Request.GameId + \"; new-game=false; mod-restore=false\") == null");
			Ordered(Read(Load), "KingdomScenarioSaveFiles.Root()", "Check(The.Game == null",
				"KingdomScenarioLoadRules.TryParse(text, out Request)",
				"KingdomScenarioSaveFiles.HashText(SnapshotWire) == Request.SnapshotSha256",
				"KingdomScenarioSaveSnapshotCodec.TryDecode(SnapshotWire, out Snapshot)", "Snapshot.GameId == Request.GameId",
				"KingdomScenarioSaveFiles.SaveDirectory(root, Request.GameId)",
				"files.Length == 3 && Directory.GetDirectories(save).Length == 0",
				"Request.PrimarySha256", "Request.InfoSha256", "Request.CacheSha256",
				"KingdomScenarioJournal.Append(\"LOAD-BEGIN\", true", "Armed = true",
				"XRLGame.LoadGame(Path.Combine(save, \"Primary\"), Session: false, ShowPopup: false)",
				"loaded.GameID == Request.GameId", "KingdomScenarioLoadWitness.VerifyRecovered(loaded, Snapshot)");
			Contains(Read(Load), "\"Primary.sav.gz\"", "\"Primary.json\"", "\"Cache.db\"", "no backup fallback");
		}

		[Test]
		public void SourceContract_RungSnapshotVariantBindsBeforeArmingWithoutLegacyFallback()
		{
			string source = Read(Load);
			Ordered(source, "internal static KingdomSubsidenceRungSaveSnapshot RungSnapshot",
				"KingdomScenarioSaveFiles.HashText(SnapshotWire) == Request.SnapshotSha256",
				"if (KingdomSubsidenceRungSaveSnapshotCodec.MatchesPrefix(SnapshotWire))",
				"KingdomSubsidenceRungSaveSnapshotCodec.MatchesCurrentPrefix(SnapshotWire)",
				"KingdomSubsidenceRungSaveSnapshotCodec.TryDecode(SnapshotWire, out RungSnapshot)",
				"RungSnapshot.GameId == Request.GameId", "else",
				"KingdomScenarioSaveSnapshotCodec.TryDecode(SnapshotWire, out Snapshot)",
				"Armed = true", "XRLGame.LoadGame(");
			Contains(source, "Math.Max(KingdomScenarioSaveSnapshotCodec.MaxWireChars, "
				+ "Math.Max(KingdomSubsidenceRungSaveSnapshotCodec.MaxWireChars, "
				+ "Math.Max(KingdomQuickstartSaveSnapshotCodec.MaxWireChars, "
				+ "KingdomUpgradeSnapshotCodec.MaxWireChars)))",
				"string route = RungSnapshot == null ? KingdomScenarioLoadWitness.VerifyRecovered(loaded, Snapshot)",
				": KingdomSubsidenceRungLoadWitness.VerifyRecovered(loaded, RungSnapshot)");
			Ordered(source, "finally", "if (RungSnapshot != null) KingdomSubsidenceRungReleaseCut.Disarm()",
				"Armed = false");
		}

		[Test]
		public void SourceContract_RungWitnessSharesExactPlayerRestoredBoundaryAndCannotRunLegacyWitness()
		{
			string source = Read(Witness);
			Ordered(source, "if (!KingdomScenarioLoadEntry.Armed || __0 != \"GameRestored\") return",
				"if (KingdomScenarioLoadEntry.RungSnapshot != null)",
				"if (ReferenceEquals(__instance, The.Player)) KingdomSubsidenceRungLoadWitness.Prefix()",
				"return", "try", "Attempts++");
		}

		[Test]
		public void SourceContract_RungSavePreservesNullableLineAndOnlyExpectsUnfinishedWrites()
		{
			string save = Read("Harness/KingdomSubsidenceRungSaveChecks.cs");
			string load = Read("Harness/KingdomSubsidenceRungLoadWitness.cs");
			Contains(save, "wear.IncidentLine == row.ReleaseBefore.Line",
				"field == KingdomSubsidenceRungSaveSnapshot.WriteCut");
			Contains(load, "wear.IncidentLine == Plan.Works[0].ReleaseBefore.Line",
				"KingdomSubsidenceRungReleaseCut.Writes == (Snapshot.Work.IncidentLine == null ? 1 : 2)",
				"KingdomSubsidenceRungReleaseCut.Fields == (Snapshot.Work.IncidentLine == null ? \"2\" : \"2,3\")");
			ClassicAssert.IsFalse(save.Contains("wear.IncidentLine != null"));
			ClassicAssert.IsFalse(load.Contains("wear.IncidentLine != null"));
		}

		[Test]
		public void SourceContract_WitnessObservesOnlyPlayersGameRestoredWithoutPatchingSingletonSend()
		{
			Contains(Read(Witness), "[HarmonyPatch(typeof(GameObject), \"FireEvent\", new Type[] { typeof(string) })]",
				"[HarmonyPrefix, HarmonyPriority(Priority.First)]", "internal static void Prefix(GameObject __instance, string __0)",
				"Attempts == 1 && WitnessedGame == null", "XRLGame game = The.Game",
				"KingdomScenarioJournal.Append(\"LOAD-PREACTIVATION\", true",
				"before-AfterGameLoaded-handlers-and-zone-activation=true");
			Ordered(Read(Witness), "if (!KingdomScenarioLoadEntry.Armed || __0 != \"GameRestored\") return",
				"try", "if (!ReferenceEquals(__instance, The.Player)) return", "Attempts++",
				"WitnessedGame = game", "\"LOAD-PREACTIVATION\", true");
			ClassicAssert.IsFalse(Read(Witness).Contains("HarmonyPatch(typeof(AfterGameLoadedEvent)"));
		}

		[Test]
		public void SourceContract_ReaderClearPrefixObservesExactSharedCacheErrorsBeforeErasure()
		{
			string source = Read(Load);
			Contains(source, "[HarmonyPatch(typeof(SerializationReader), \"Clear\")]",
				"internal static class KingdomScenarioLoadReaderWitness", "[HarmonyPrefix]",
				"internal static void Prefix(SerializationReader __instance)");
			int start = source.IndexOf("internal static class KingdomScenarioLoadReaderWitness", StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0);
			string reader = source.Substring(start);
			Ordered(reader, "if (!KingdomScenarioLoadEntry.Armed || !ReferenceEquals(__instance.Cache, FastSerialization.SharedCache)) return",
				"Releases++", "HadErrors |= __instance.Errors != 0");
			ClassicAssert.IsFalse(Regex.IsMatch(reader, @"\b__instance\.(?:Errors|Cache)\s*=(?!=)"));
			ClassicAssert.IsFalse(Regex.IsMatch(reader, @"\b(?:Clear|ReleaseShared)\s*\("));
		}

		[Test]
		public void SourceContract_ExactReaderCompletionGatesPreactivationAndErrorsBlockFinalSuccess()
		{
			Ordered(Read(Witness), "Attempts == 1 && WitnessedGame == null",
				"KingdomScenarioLoadReaderWitness.Releases == 1 && !KingdomScenarioLoadReaderWitness.HadErrors",
				"KingdomScenarioSaveSnapshot snapshot = KingdomScenarioLoadEntry.Snapshot", "WitnessedGame = game",
				"KingdomScenarioJournal.Append(\"LOAD-PREACTIVATION\", true");
			Ordered(Read(Load), "string route = RungSnapshot == null ? KingdomScenarioLoadWitness.VerifyRecovered(loaded, Snapshot)",
				"KingdomSubsidenceRungLoadWitness.VerifyRecovered(loaded, RungSnapshot)",
				"Check(!KingdomScenarioLoadReaderWitness.HadErrors, \"engine reported deserialization errors\")",
				"KingdomScenarioJournal.Append(\"SCRIPT-COMPLETE\", true", "\"; recovery=\" + route");
		}

		[Test]
		public void SourceContract_PreactivationRequiresRestoredAutoOnceAndExactSavedClockAndWire()
		{
			Contains(Read(Witness), "ReferenceEquals(The.Game, game)", "game.GameID == snapshot.GameId",
				"game.TimeTicks == snapshot.Now", "game.GetSystem<KingdomScenarioAutoRunner>()?.HasConsideredScript == true",
				"KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey, KingdomScenarioLoadEntry.SnapshotWire)",
				"system.City.SubsidenceModel == snapshot.StepWire", "system.City.HasValidSubsidenceStorage()",
				"book.Sequence == 1", "book.Active.Completed == 1 && book.Active.Quota == 5",
				"book.Active.DueTick == snapshot.Now", "system.LastSubsidenceTick == book.Active.AnchorTick");
			Contains(Read("Harness/KingdomScenarioAutoRunner.cs"),
				"internal bool HasConsideredScript { get { return ScriptConsidered; } }");
		}

		[Test]
		public void SourceContract_PreactivationBindsAllFortyNineBodiesAndTheAlreadyMissingResident()
		{
			Contains(Read(Witness), "system.Population == 49 && KingdomResidents.OnRollCount(system) == 49",
				"system.Ledger.Departures == snapshot.LedgerDepartures", "zone.ZoneID == snapshot.ZoneId",
				"KingdomScenarioSaveFiles.Same(System.IO.Path.GetFullPath(game.GetCacheDirectory())",
				"KingdomPlots.TryCaptureGlobalLiveIds(ids, out Dictionary<string, GameObject> live)",
				"live.Count == 49 && !live.ContainsKey(snapshot.MissingObjectId)", "for (int i = 0; i < 49; i++)",
				"snapshot.ResidentIds[i], snapshot.ObjectIds[i]", "Bodies.Add(body)",
				"KingdomResidents.DepartureCarriersAbsent(system, system.City, snapshot.MissingResidentId)",
				"GameObject.Validate(Body) && Body.IsAlive", "Body.IDIfAssigned == ObjectId", "!Body.IsPlayerLed()",
				"KingdomCitizenship.BelongsTo(System, Body)", "System.City.TryResidentRow(Id, out _)",
				"bindings.TryGet(Id, KingdomBindingKind.Resident, out KingdomBinding binding)",
				"binding.ObjectId == ObjectId && binding.ZoneId == Zone.ZoneID");
		}

		[Test]
		public void SourceContract_PostLoadProductionPrepassAndRetryRequireTheExplicitWitness()
		{
			Ordered(Read(Witness), "Failure == null && Attempts == 1 && ReferenceEquals(WitnessedGame, Game) && Bodies.Count == 49",
				"activationRecovered || system.City.SubsidenceModel == Snapshot.StepWire", "survey.BindPass()",
				"KingdomSubsidenceStepRuntime.TryBeforePass(system, zone, survey, out string refusal)",
				"VerifyAfter(system, zone, Snapshot)", "string wire = system.City.SubsidenceModel",
				"KingdomSubsidenceStepRuntime.TryBeforePass(system, zone, survey, out refusal)",
				"VerifyAfter(system, zone, Snapshot)", "system.City.SubsidenceModel == wire",
				"Same(system.ChronicleEntries, official)", "Same(system.OutsiderEntries, outsider)",
				"Game.TimeTicks == Snapshot.Now", "KingdomScenarioJournal.Append(\"LOAD-RECOVERY\", true");
			Contains(Read(Witness), "activation-already-recovered=", "original-step-retired=true; replay=false",
				"internal static string VerifyRecovered(XRLGame Game, KingdomScenarioSaveSnapshot Snapshot)",
				"return activationRecovered ? \"native-zone-activation\" : \"explicit-production-prepass\"");
		}

		[Test]
		public void SourceContract_RecoveryMeasuresFourPhysicalRemovalsAndExactRetirement()
		{
			Contains(Read(Witness), "System.Founded && System.Population == 45 && KingdomResidents.OnRollCount(System) == 45",
				"System.Stage == GrowthStage.City", "System.Ledger.Departures == Snapshot.LedgerDepartures + 4",
				"System.LastSubsidenceTick == Snapshot.Now", "KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture)",
				"book.Active == null && book.Sequence == 1 && book.LastRetiredTick == Snapshot.Now",
				"book.BatchModel == KingdomSubsidenceBatchRules.None", "book.FailureModel == KingdomSubsidenceReportArchive.None",
				"if (!GameObject.Validate(Bodies[i]))", "gone++",
				"KingdomResidents.DepartureCarriersAbsent(System, System.City, Snapshot.ResidentIds[i])",
				"ExactBody(System, Zone, Bodies[i], Snapshot.ResidentIds[i], Snapshot.ObjectIds[i])",
				"gone == 4 && KingdomSubsidenceStepRuntime.Status(System) == \"\"");
		}

		[Test]
		public void SourceContract_LoadFailuresRemainTerminalAndNeverStartOrdinaryTurnsOrRestoreMods()
		{
			Contains(Read(Load), "KingdomScenarioJournal.Append(\"SCRIPT-STOPPED\", false", "native-load refused; evidence retained",
				"Armed = false", "if (!priorPopup && Popup.Suppress) Popup.Suppress = false",
				"\"; recovery=\" + route", "ordinary-acceptance=false", "profiles-and-effects-retained=true");
			foreach (string path in new[] { Save, Files, Load, Witness })
			{
				string source = Read(path);
				if (path == Load)
				{
					const string command = "Keyboard.PushMouseEvent(\"Pick:Continue\")";
					ClassicAssert.AreEqual(1, Regex.Matches(source, Regex.Escape(command)).Count);
					source = source.Replace(command, "");
				}
				ClassicAssert.IsFalse(Regex.IsMatch(source, @"\b(?:Continue(?:Menu)?|SaveManagement|RestoreMods\w*|TryRestoreModsAndLoadAsync|RunGame|PushMouseEvent)\s*\("), path);
				ClassicAssert.IsFalse(Regex.IsMatch(source, @"\.(?:Destroy|Obliterate)\s*\("), path);
				ClassicAssert.IsFalse(Regex.IsMatch(source, @"\b(?:Game|game)\.TimeTicks\s*=(?!=)"), path);
			}
		}

		[Test]
		public void SourceContract_ProductionCompilationAndRuntimeInventoryExcludeHarness()
		{
			ClassicAssert.IsFalse(Regex.IsMatch(Read("manifest.json"), @"Harness", RegexOptions.IgnoreCase));
			Match exclusions = Regex.Match(Read("Tools/stage.sh"), @"(?m)^EXCLUDE_DIRS=\(([^)]*)\)");
			ClassicAssert.IsTrue(exclusions.Success, "runtime exclusions missing");
			ClassicAssert.IsTrue(Regex.IsMatch(exclusions.Groups[1].Value, @"(?:^|\s)Harness(?:\s|$)"));
			Contains(Read("Tools/portable-check.sh"), "\"Harness/\"", "development-only path entered runtime inventory");
		}

		private static string Read(string Path) { return TestMain.ReadRepositoryText(Path); }
		private static string Compact(string Value) { return Regex.Replace(Value, @"\s+", ""); }
		private static string Between(string Source, string Start, string End)
		{
			string source = Compact(Source), start = Compact(Start), end = Compact(End);
			int first = source.IndexOf(start, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(first, 0, Start);
			int last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
			ClassicAssert.Greater(last, first, End);
			MatchCollection characters = Regex.Matches(Source, @"\S");
			return Source.Substring(characters[first].Index, characters[last].Index - characters[first].Index);
		}
		private static void Contains(string Source, params string[] Tokens)
		{
			string source = Compact(Source);
			foreach (string token in Tokens) StringAssert.Contains(Compact(token), source, token);
		}
		private static void Ordered(string Source, params string[] Tokens)
		{
			string source = Compact(Source);
			int cursor = 0;
			foreach (string token in Tokens)
			{
				string expected = Compact(token);
				int at = source.IndexOf(expected, cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(at, cursor, token);
				cursor = at + expected.Length;
			}
		}
	}
}
#endif
