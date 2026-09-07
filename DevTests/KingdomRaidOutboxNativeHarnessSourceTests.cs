#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Native Harness source contracts; these do not execute engine fixtures.</summary>
	[TestFixture]
	public sealed class KingdomRaidOutboxNativeHarnessSourceTests
	{
		private const string Provider = "Harness/KingdomRaidOutboxNativeProvider.cs";
		private const string Fixtures = "Harness/KingdomRaids.NativeOutbox.cs";
		private const string Context = "Harness/KingdomRaidOutboxNativeContext.cs";
		private const string SharedContext = "Harness/KingdomNativeRegressionContext.cs";
		private const string Persona = "Tools/personas/raid-outbox-native-checks.persona";

		[Test]
		public void SixUniqueCaseIdsBindExactDispatchRetryAndFourSinkCallbacks()
		{
			string body = Method(Read(Fixtures), "internal static void NativeOutboxChecks(");
			MatchCollection matches = Regex.Matches(body, @"Context\.Case\(""([^""]+)"",\s*\(\) => ([^;\r\n]+)\);");
			CollectionAssert.AreEqual(new[] {
				"dispatch-success=NativeOutboxSuccess(Context)",
				"chronicle-refusal-retry=NativeOutboxRetry(Context)",
				"throw-chronicle=NativeOutboxThrow(Context, KingdomLifecycleSinkMask.Chronicle)",
				"throw-ledger=NativeOutboxThrow(Context, KingdomLifecycleSinkMask.Ledger)",
				"throw-message=NativeOutboxThrow(Context, KingdomLifecycleSinkMask.Message)",
				"throw-deed=NativeOutboxThrow(Context, KingdomLifecycleSinkMask.Deed)"
			}, matches.Cast<Match>().Select(m => m.Groups[1].Value + "=" + Flat(m.Groups[2].Value)).ToArray());
			ClassicAssert.AreEqual(6, new HashSet<string>(matches.Cast<Match>().Select(m => m.Groups[1].Value),
				StringComparer.Ordinal).Count);
			StringAssert.Contains("internal const int ExpectedCases = 6;", Read(Provider));
			ContainsAll(Read(Provider), "[KingdomScenarioVerbProvider]",
				"public sealed class KingdomRaidOutboxNativeProvider : IKingdomScenarioVerbProvider",
				"return KingdomScenarioVerbApi.Version;", "return new[] { Verb };");
		}

		[Test]
		public void EligibilityRequiresStampedActiveUnfoundedScopeExactScriptAndAbsentKeys()
		{
			string body = Flat(Method(Read(Provider), "private static bool Eligible("));
			ContainsAll(body, "Game == null || Zone == null || The.ZoneManager == null",
				"!ReferenceEquals(The.ZoneManager.ActiveZone, Zone) || !MessageQueue.Enabled",
				"(Game.GetSystem<KingdomSystem>()?.Founded ?? false)",
				"KingdomNativeRegressionContext.HasQuickstartState(Game)",
				"KingdomNativeRegressionContext.HasAnyState(Game, Receipt)",
				"KingdomNativeRegressionContext.HasAnyState(Game, KingdomRaidOutboxNativeContext.RegistryKey)",
				"KingdomNativeRegressionContext.HasAnyState(Game, KingdomRaidOutboxNativeContext.FaultKey)");
			Ordered(body, "HasAnyState(Game, KingdomRaidOutboxNativeContext.FaultKey)",
				"if (!KingdomScenarioRealizer.TryBindStampedPlan(out plan, out stamp, out Failure)) return false;",
				"if (plan.Key != \"founding-first-city\" || !KingdomScenarioScript.TryRead(out script, out Failure) || script.Count != 3 || script[0] != \"stagedigest\" || script[1] != Verb || script[2] != \"stagedigest\")",
				"KingdomScenarioTransactionMarker.Observe(out transaction) != KingdomScenarioTransactionShape.None",
				"!KingdomQuickstartRules.TryProfile(\"marsh\", out profile) || Zone.ZoneID != profile.ZoneId");
			string presence = Method(Read(SharedContext), "internal static bool HasAnyState(");
			foreach (string table in new[] { "String", "Int", "Int64", "Boolean", "Object" })
				StringAssert.Contains("Game." + table + "GameState?.ContainsKey(Key)", presence);
		}

		[Test]
		public void ProviderProvesIntentBeforeFixtureExecutionAndExactResultBeforeSuccess()
		{
			string body = Flat(Method(Read(Provider), "public string RunScenarioVerb("));
			Ordered(body, "Ok = false;", "if (Verb != KingdomRaidOutboxNativeProvider.Verb || !string.IsNullOrEmpty(Argument))",
				"if (!Eligible(game, zone, out failure)) return", "game.SetStringGameState(Receipt, \"intent\");",
				"if (!KingdomScenarioDurableState.ProvesExactText(Receipt, \"intent\")) return",
				"new KingdomRaidOutboxNativeContext(game, zone)", "KingdomRaids.NativeOutboxChecks(context);",
				"Ok = context.Count == ExpectedCases && context.Passed == ExpectedCases && context.Failed == 0;",
				"string report = context.Report();",
				"if (!KingdomScenarioDurableState.ProvesExactText(Receipt, \"intent\")) { Ok = false; return report",
				"game.SetStringGameState(Receipt, report);",
				"if (!KingdomScenarioDurableState.ProvesExactText(Receipt, report)) { Ok = false; return report",
				"return report;");
			StringAssert.Contains("internal const string Receipt = \"r_TAF_ScenarioRaidOutboxChecks_v1\";", Read(Provider));
		}

		[Test]
		public void FixtureUsesUnregisteredSystemAndRealPublishedRaidAuthorityWithoutActors()
		{
			string body = Flat(Method(Read(Fixtures), "private static KingdomSystem NativeOutboxSystem("));
			Ordered(body, "KingdomSystem system = new KingdomSystem();", "KingdomLifecycleBook book = system.LifecycleBook;",
				"KingdomLifecycleRules.BindSettlementIdentity(book,", "KingdomLifecycleRules.PrepareOperation(book, KingdomLifecycleLane.Raid, KingdomLifecycleAction.RaidWarning, 10L)",
				"op.ZoneId = C.Zone.ZoneID;", "op.ObjectId = KingdomRaidIncidentRules.GrievanceId(op.Origin);",
				"op.ObjectMarker = KingdomRaidIncidentRules.IncidentId(op.ObjectId);",
				"op.Outbox = KingdomLifecycleRules.PrepareOutbox(op,", "!op.Outbox.ChronicleAccomplishment",
				"KingdomLifecycleRules.RaidRuntimeAdapter.PrepareLeases(book, op)", "KingdomLifecycleRules.TryPublish(book, op)",
				"KingdomLifecyclePhase.DomainIntent", "KingdomLifecycleRules.RaidRuntimeAdapter.ProveDomain(book, op)",
				"KingdomLifecyclePhase.DomainSettled", "KingdomLifecyclePhase.Sinks",
				"!system.Founded && !ReferenceEquals(system, C.Game.GetSystem<KingdomSystem>())");
			string sources = Read(Fixtures) + Read(Context) + Read(Provider);
			foreach (string forbidden in new[] { "AddSystem(", "RegisterSystem(", "GameObject.Create(",
				"GameObject.create(", ".AddObject(", ".Founded =", "Popup.Show(" })
				StringAssert.DoesNotContain(forbidden, sources);
		}

		[Test]
		public void SuccessfulActualDispatchRepeatsWithoutMutationThenRealResumeRetires()
		{
			string body = Flat(Method(Read(Fixtures), "private static void NativeOutboxSuccess("));
			Ordered(body, "C.ExpectedMessage(op.Outbox.Message);", "C.Check(DispatchOutbox(system, op)",
				"C.CaptureRegistry(op.Outbox.ChronicleReceiptId, op.Outbox.Chronicle);", "NativeOutboxEffects(C, system, op);",
				"byte[] before = NativeOutboxBytes(system.LifecycleBook);", "C.Check(DispatchOutbox(system, op)",
				"NativeOutboxSame(before, NativeOutboxBytes(system.LifecycleBook))", "ResumeOpen(system, C.Zone);",
				"system.LifecycleBook.Raid == null", "NativeOutboxEffects(C, system, op);");
			string effects = Flat(Method(Read(Fixtures), "private static void NativeOutboxEffects("));
			ContainsAll(effects, "System.ChronicleEntries.Count == 1 && System.OutsiderEntries.Count == 1",
				"System.ChronicleEntries[0].Contains(Op.Outbox.Chronicle)",
				"System.Ledger.Notes.Count == 1 && System.Ledger.Notes[0] == Op.Outbox.Ledger",
				"System.LastDeed == Op.Outbox.Deed && System.LastDeedTick == C.Game.TimeTicks",
				"Op.Outbox.GuestbookState == KingdomLifecycleSinkState.Skipped");
			foreach (string sink in new[] { "Chronicle", "Ledger", "Message", "Deed" })
				StringAssert.Contains("Op.Outbox." + sink + "State == KingdomLifecycleSinkState.Delivered", effects);
		}

		[Test]
		public void RealChronicleBoundRefusalRetainsIntentUntilRepairAndResume()
		{
			string body = Flat(Method(Read(Fixtures), "private static void NativeOutboxRetry("));
			Ordered(body, "i <= KingdomChronicle.MaxEntries", "system.ChronicleEntries.Add(\"native bound\")",
				"C.ExpectFault(\"5:list-bound\");", "C.ExpectedMessage(", "C.Check(!DispatchOutbox(system, op)",
				"op.Phase == KingdomLifecyclePhase.Sinks", "op.Outbox.ChronicleState == KingdomLifecycleSinkState.Intent",
				"system.Ledger.Notes.Count == 0 && system.LastDeed == null",
				"!KingdomNativeRegressionContext.HasAnyState(C.Game, KingdomRaidOutboxNativeContext.RegistryKey)",
				"KingdomScenarioDurableState.ProvesExactText(KingdomRaidOutboxNativeContext.FaultKey, \"5:list-bound\")",
				"system.ChronicleEntries.Clear();", "C.ExpectedMessage(op.Outbox.Message);", "ResumeOpen(system, C.Zone);",
				"C.CaptureRegistry(op.Outbox.ChronicleReceiptId, op.Outbox.Chronicle);", "system.LifecycleBook.Raid == null",
				"NativeOutboxEffects(C, system, op);");
		}

		[Test]
		public void FourInterruptionsRunRealNativeEffectsBeforeSyntheticThrow()
		{
			string body = Flat(Method(Read(Fixtures), "private static void NativeOutboxThrow("));
			Ordered(body, "if (Sink == KingdomLifecycleSinkMask.Message) C.ExpectedMessage(op.Outbox.Message);",
				"C.Check(!Deliver(system, op, Sink, delegate", "calls++;", "switch (Sink)",
				"case KingdomLifecycleSinkMask.Chronicle:", "KingdomChronicle.RecordOnce(system, op.Outbox.ChronicleReceiptId, op.Outbox.Chronicle)",
				"C.CaptureRegistry(op.Outbox.ChronicleReceiptId, op.Outbox.Chronicle);",
				"case KingdomLifecycleSinkMask.Ledger: system.Ledger.Note(op.Outbox.Ledger); break;",
				"case KingdomLifecycleSinkMask.Message: MessageQueue.AddPlayerMessage(op.Outbox.Message); break;",
				"case KingdomLifecycleSinkMask.Deed: system.RecordDeed(op.Outbox.Deed); break;", "throw interruption;",
				"calls == 1 && interruption.Read && interruption.QuarantinedFirst");
			ContainsAll(body, "system.ChronicleEntries.Count == 1", "system.Ledger.Notes.Count == 1",
				"system.LastDeed == op.Outbox.Deed", "system.LastDeedTick == C.Game.TimeTicks");
		}

		[Test]
		public void HostileExceptionTextAndEveryRecoveryRoutePreserveQuarantinedWireBytes()
		{
			string body = Flat(Method(Read(Fixtures), "private static void NativeOutboxThrow("));
			ContainsAll(body, "ReferenceEquals(book.Raid, op) && op.Phase == KingdomLifecyclePhase.Quarantined",
				"op.Fault == \"raid outbox \" + Sink + \" callback threw\"");
			foreach (string sink in new[] { "Chronicle", "Ledger", "Message", "Deed" })
				StringAssert.Contains("Sink != KingdomLifecycleSinkMask." + sink
					+ " || op.Outbox." + sink + "State == KingdomLifecycleSinkState.Intent", body);
			Ordered(body, "byte[] held = NativeOutboxBytes(book);", "!KingdomLifecycleRules.RecoverOutbox(book, op)",
				"!DispatchOutbox(system, op)", "!Deliver(system, op, Sink, () => { calls++; return true; })",
				"ResumeOpen(system, C.Zone);", "!KingdomLifecycleRules.AdvancePhase(book, op, KingdomLifecyclePhase.ScheduleIntent, 20L)",
				"!KingdomLifecycleRules.Retire(book, op, 20L)", "KingdomLifecycleRules.PrepareOperation(book, KingdomLifecycleLane.Raid, KingdomLifecycleAction.RaidWarning, 20L) == null",
				"calls == 1 && NativeOutboxSame(held, NativeOutboxBytes(book))");
			Ordered(Flat(Method(Read(Fixtures), "public override string Message")), "Read = true;",
				"QuarantinedFirst = Operation.Phase == KingdomLifecyclePhase.Quarantined;", "throw new InvalidOperationException(");
			StringAssert.Contains("KingdomLifecycleWireCodec.WriteLifecycle(writer, Book);",
				Method(Read(Fixtures), "private static byte[] NativeOutboxBytes("));
			ContainsAll(Method(Read(Fixtures), "private static bool NativeOutboxSame("),
				"First.Length != Second.Length", "First[i] != Second[i]");
		}

		[Test]
		public void RegistryOwnershipRequiresOneCanonicalExactTerminalFingerprint()
		{
			string body = Flat(Method(Read(Context), "internal void CaptureRegistry("));
			Ordered(body, "InCase && SameScope() && !OtherTypedState(RegistryKey)",
				"Game.StringGameState.TryGetValue(RegistryKey, out raw)",
				"KingdomChronicleReceiptRules.TryFingerprint(EventId, Text, false, null, out fingerprint)",
				"KingdomChronicleReceiptRules.TryParseRegistry(raw, out rows, out migrated, out fault)",
				"!migrated && fault == KingdomChronicleRegistryFault.None && rows.Count == 1",
				"rows[0].Compact && !rows[0].LegacyBlocked", "KingdomChronicleReceiptRules.IsTerminal(rows[0])",
				"rows[0].OfficialState == KingdomChronicleSinkDisposition.Delivered",
				"rows[0].OutsiderState == KingdomChronicleSinkDisposition.Delivered",
				"rows[0].JournalState == KingdomChronicleSinkDisposition.Skipped",
				"string.Equals(rows[0].EventId, EventId, StringComparison.Ordinal)",
				"string.Equals(rows[0].Fingerprint, fingerprint, StringComparison.Ordinal)",
				"KingdomChronicleReceiptRules.TryWriteRegistry(rows, out canonical, out fault)",
				"string.Equals(raw, canonical, StringComparison.Ordinal)",
				"OwnedRegistry == null || string.Equals(OwnedRegistry, raw, StringComparison.Ordinal)", "OwnedRegistry = raw;");
		}

		[Test]
		public void MessagesAreExpectedBeforeAppendAndOnlyComparedNeverRewound()
		{
			string source = Read(Context);
			Ordered(Flat(Method(source, "internal void ExpectedMessage(")), "InCase && SameScope() && ExactMessages()",
				"Raw != null && ExpectedTail.Count < 16", "Markup.Transform(ConsoleLib.Console.ColorUtility.CapitalizeExceptFormatting(Raw))",
				"expected != null && expected != \"!clear\"", "ExpectedTail.Add(expected);");
			string exact = Flat(Method(source, "private bool ExactMessages("));
			ContainsAll(exact, "Messages.Count != Prefix.Length + ExpectedTail.Count",
				"Queue.PreviousMessage != PreviousMessage", "Queue.LastMessage != LastMessage",
				"Queue.LastTurnMessages != LastTurnMessages", "Queue.Terse != Terse", "MessageQueue.Suppress != Suppress",
				"string.Equals(Messages[i], Prefix[i], StringComparison.Ordinal)",
				"string.Equals(Messages[Prefix.Length + i], ExpectedTail[i], StringComparison.Ordinal)");
			string all = source + Read(Fixtures) + Read(Provider);
			ClassicAssert.IsFalse(Regex.IsMatch(all, @"\b(?:Messages|Queue\.Messages)\.(?:Add|Clear|Remove\w*|Insert|Reset)\s*\("));
			ClassicAssert.IsFalse(Regex.IsMatch(all, @"\b(?:Queue|MessageQueue)\.[A-Za-z_]\w*\s*=(?!=)"));
			ClassicAssert.IsFalse(Regex.IsMatch(all, @"\b(?:Queue|MessageQueue)\.(?:Clear|Reset|Suppress\w*|Remove\w*)\s*\("));
			ClassicAssert.IsFalse(Regex.IsMatch(all, @"\b(?:Queue|MessageQueue)\.[A-Za-z_]*(?:Listener|Cache)[A-Za-z_]*\.(?:Clear|Remove\w*|Add)\s*\("));
		}

		[Test]
		public void ContextSnapshotsExactGamePlayerQueueZoneAndFiveDictionaryReferences()
		{
			string source = Read(Context);
			ContainsAll(Flat(Method(source, "internal KingdomRaidOutboxNativeContext(")),
				"Player = Game?.Player;", "PlayerBody = Game?.Player?.Body;", "Queue = Game?.Player?.Messages;",
				"Messages = Queue?.Messages;", "Dictionaries = StateDictionaries(Game);");
			Ordered(Flat(Method(source, "private void Snapshot(")), "SameScope() && MessageQueue.Enabled",
				"!HasAnyState(Game, RegistryKey)", "!HasAnyState(Game, FaultKey)", "Prefix = Messages.ToArray();",
				"PreviousMessage = Queue.PreviousMessage;", "LastMessage = Queue.LastMessage;",
				"LastTurnMessages = Queue.LastTurnMessages;", "Terse = Queue.Terse;", "Suppress = MessageQueue.Suppress;");
			ContainsAll(Flat(Method(source, "private bool SameScope(")), "!ReferenceEquals(The.Game, Game)",
				"!ReferenceEquals(Game.Player, Player)", "!ReferenceEquals(The.Player, PlayerBody)",
				"!ReferenceEquals(Game.Player.Messages, Queue)", "!ReferenceEquals(Queue.Messages, Messages)",
				"!ReferenceEquals(The.ZoneManager?.ActiveZone, Zone)", "!ReferenceEquals(The.Player?.CurrentZone, Zone)",
				"KingdomNativeRegressionContext.HasQuickstartState(Game)", "(Game.GetSystem<KingdomSystem>()?.Founded ?? false)",
				"!KingdomScenarioDurableState.ProvesExactText(KingdomRaidOutboxNativeProvider.Receipt, \"intent\")",
				"if (!ReferenceEquals(Dictionaries[i], current[i])) return false;");
			string dictionaries = Method(source, "private static object[] StateDictionaries(");
			foreach (string table in new[] { "String", "Int", "Int64", "Boolean", "Object" })
				StringAssert.Contains("Game?." + table + "GameState", dictionaries);
		}

		[Test]
		public void FinallyCleanupPoisonsUnprovedCasesAndRemovesOnlyExactOwnedTypedKeys()
		{
			string source = Read(Context);
			Ordered(Flat(Method(source, "internal void Case(")), "Cases.Count >= KingdomRaidOutboxNativeProvider.ExpectedCases || !Cases.Add(Id)",
				"string failure = Poisoned ?", "Snapshot();", "started = InCase = true;", "Body();", "finally",
				"InCase = false;", "if (started)", "try { clean = VerifyAndClean(); }", "if (!clean)", "Poisoned = true;",
				"else if (failure != null) Poisoned = true;", "if (failure == null) Passed++;", "else Failed++;");
			Ordered(Flat(Method(source, "private bool VerifyAndClean(")), "if (!SameScope() || !ExactMessages()",
				"!OwnedStateMatches(RegistryKey, OwnedRegistry)", "!OwnedStateMatches(FaultKey, OwnedFault)) return false;",
				"RemoveOwned(RegistryKey, OwnedRegistry) && RemoveOwned(FaultKey, OwnedFault)",
				"SameScope() && ExactMessages() && !HasAnyState(Game, RegistryKey) && !HasAnyState(Game, FaultKey)");
			ContainsAll(Flat(Method(source, "private bool OwnedStateMatches(")),
				"return Owned == null ? !HasAnyState(Game, Key) : KingdomScenarioDurableState.ProvesExactText(Key, Owned);");
			foreach (string table in new[] { "Int", "Int64", "Boolean", "Object" })
				StringAssert.Contains("Game." + table + "GameState?.ContainsKey(Key)", Method(source, "private bool OtherTypedState("));
			Ordered(Flat(Method(source, "private bool RemoveOwned(")), "if (!SameScope() || !OwnedStateMatches(Key, Owned)) return false;",
				"Dictionary<string, string> exact = (Dictionary<string, string>)Dictionaries[0];",
				"if (exact.ContainsKey(Key) && !exact.Remove(Key)) return false;", "return !HasAnyState(Game, Key);");
			StringAssert.Contains("return KingdomNativeRegressionContext.HasAnyState(Game, Key);",
				Method(source, "internal static bool HasAnyState("));
			Ordered(Flat(Method(source, "internal void ExpectFault(")), "ExactCode == \"5:list-bound\" && OwnedFault == null",
				"!HasAnyState(Game, FaultKey)", "OwnedFault = ExactCode;");
		}

		[Test]
		public void ReportDisclaimsOrdinarySaveLoadEngineFailureAndListenerReversalEvidence()
		{
			string body = Flat(Method(Read(Context), "internal string Report("));
			ContainsAll(body, "\"native-raid-outbox cases=\" + Count + \" passed=\" + Passed + \" failed=\" + Failed",
				"synthetic=true; ordinary-acceptance=false; save-load=untested; engine-raised-failure=untested",
				"messages-retained=true; ui-listeners=not-reversed");
			StringAssert.Contains("Synthetic post-effect interruption; no claim an engine event raised this exception.", Read(Fixtures));
		}

		[Test]
		public void PersonaSealsExactlyThreeVerbsAndSixPassingCasesBetweenUnfoundedObservations()
		{
			string source = Read(Persona);
			ClassicAssert.AreEqual("founding-first-city", Setting(source, "REQUEST"));
			ClassicAssert.AreEqual("8.22@40,12", Setting(source, "START"));
			ClassicAssert.AreEqual("raid-outbox-check", Setting(source, "VERBS"));
			ClassicAssert.AreEqual("raids,native-regression", Setting(source, "SET"));
			CollectionAssert.AreEqual(new[] { "stagedigest", "raid-outbox-check", "stagedigest" }, Setting(source, "SCRIPT").Split(';'));
			CollectionAssert.AreEqual(new[] { "stagedigest:OK~founded=false", "raid-outbox-check:OK~cases=6 passed=6 failed=0",
				"stagedigest:OK~founded=false", "COMPLETE" }, Setting(source, "EXPECT").Split(','));
			StringAssert.Contains("no ordinary raid progression or save/load claim", source);
			StringAssert.DoesNotContain("realize", Setting(source, "SCRIPT"));
			StringAssert.Contains("internal const string Verb = \"raid-outbox-check\";", Read(Provider));
		}

		private static string Read(string Path) { return TestMain.ReadRepositoryText(Path); }
		private static string Flat(string Source) { return Regex.Replace(Source, @"\s+", " "); }
		private static void ContainsAll(string Source, params string[] Tokens)
		{
			foreach (string token in Tokens) StringAssert.Contains(token, Source);
		}
		private static string Method(string Source, string Signature)
		{
			int start = Source.IndexOf(Signature, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, Signature);
			int open = Source.IndexOf('{', start), depth = 0;
			ClassicAssert.GreaterOrEqual(open, 0, Signature);
			for (int i = open; i < Source.Length; i++)
			{
				if (Source[i] == '{') depth++;
				else if (Source[i] == '}' && --depth == 0) return Source.Substring(start, i - start + 1);
			}
			Assert.Fail("Unclosed source method: " + Signature);
			return null;
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
			string[] rows = Source.Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith(Key + "=", StringComparison.Ordinal)).ToArray();
			ClassicAssert.AreEqual(1, rows.Length, Key);
			return rows[0].Substring(Key.Length + 1);
		}
	}
}
#endif
