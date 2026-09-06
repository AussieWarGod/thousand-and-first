#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	// Source contracts only. These do not execute SaveGame, LoadGame, callbacks or process stopping.
	[TestFixture]
	public sealed class KingdomQuickstartRoundtripSourceTests
	{
		private const string Save = "Harness/KingdomQuickstartSaveTest.cs";
		private const string State = "Harness/KingdomQuickstartSaveState.cs";
		private const string Load = "Harness/KingdomQuickstartLoadTest.cs";
		private const string Entry = "Harness/KingdomScenarioLoadEntry.cs";
		private const string Boot = "Harness/KingdomQuickstartBootTest.cs";

		[Test]
		public void SourceContract_SaveRequiresAnObservedSuccessfulGenuineBoot()
		{
			string boot = Read(Boot);
			Contains(Between(boot, "internal static bool ClaimsSave(", "private static void Fail("),
				"Request?.Save == true", "Begun && Ended", "ReferenceEquals(Game, Current)",
				"Failure == null && Verified", "Observations == 1 && WorldCalls == 1 && CampCalls == 1 && RunCalls == 1",
				"RunSucceeded && ExactScript()", "Info.GameSeed == Seed", "OriginalWorldSeed",
				"ReferenceEquals(The.Player, Founder)", "ReferenceEquals(The.ZoneManager?.ActiveZone, Zone)");
			foreach (string path in new[] { Save, State, Load })
			{
				string source = Read(path);
				Assert.IsFalse(Regex.IsMatch(source, @"KingdomQuickstartBootstrap\s*\.\s*Run\s*\("), path);
				Assert.IsFalse(Regex.IsMatch(source, @"\b(?:TrySeed|Restrip|Realize|TryCreateReceipt|CreateObject)\s*\("), path);
				Assert.IsFalse(Regex.IsMatch(source, @"(?:new\s+|RequireSystem\s*<)KingdomScenarioAutoRunner\b"), path);
				StringAssert.DoesNotContain("NativeFixture", source, path);
			}
		}

		[Test]
		public void SourceContract_OwnedNullBootResultStillReachesTheExactCoreTerminalHold()
		{
			string source = Between(Read(Save), "internal static void Postfix(", "private static KingdomQuickstartSaveSnapshot Save(");
			Ordered(source, "XRLGame candidate = __instance?.Game", "ClaimsSave(candidate)",
				"Thread.CurrentThread != XRLCore.CoreThread", "Claimed = candidate", "Saving = true",
				"Check(ReferenceEquals(__result, Claimed)", "snapshot = Save(__result)",
				"KingdomQuickstartSaveState.Verify(__result, snapshot)", "VerifyCache(__result)",
				"BootstrapCalls == 0 && !SaveError && Popup.Suppress == priorPopup",
				"finally { Saving = false; }", "failure == null,", "Parked.Task.GetAwaiter().GetResult()");
			Contains(Read(Save), "HarmonyPatch(typeof(XRLCore), \"NewGame\", new Type[0])",
				"HarmonyPostfix, HarmonyPriority(Priority.Last)", "new TaskCompletionSource<bool>()");
			Assert.IsFalse(Regex.IsMatch(source, @"\bParked\.(?:SetResult|TrySetResult|SetException|TrySetCanceled)\s*\("));
			Assert.IsFalse(Regex.IsMatch(source, @"\bRunGame\s*\("));
		}

		[Test]
		public void SourceContract_CacheAuthorityIsFrozenBeforeSaveAndGuardedDuringSerialization()
		{
			string source = Read(Save);
			Contains(Between(source, "internal static void VerifyCache(", "[HarmonyPostfix"),
				"if (!Saving || !ReferenceEquals(Game, Claimed)) return", "ReferenceEquals(The.Game, Game)",
				"Game.GameID == GameId", "Game._CacheDirectory == CacheDirectory", "KingdomScenarioSaveFiles.Root() == Root",
				"Path.GetFullPath(CacheDirectory), OwnedDirectory", "SaveDirectory(Root, GameId), OwnedDirectory");
			Ordered(Between(source, "private static KingdomQuickstartSaveSnapshot Save(", "private static void Check("),
				"Game._CacheDirectory != null", "Path.GetFullPath(Game._CacheDirectory), directory",
				"CacheDirectory = Game._CacheDirectory; OwnedDirectory = directory; Root = root; GameId = Game.GameID",
				"VerifyCache(Game)", "KingdomQuickstartSaveState.Capture", "VerifyCache(Game)",
				"Game.SaveGame(\"Primary\")", "task?.GetAwaiter().GetResult()", "VerifyCache(Game)");
			Contains(source, "HarmonyPatch(typeof(XRLGame), \"GetCacheDirectory\", new Type[] { typeof(string) })",
				"internal static void Prefix(XRLGame __instance) { KingdomQuickstartSaveTest.VerifyCache(__instance); }");
		}

		[Test]
		public void SourceContract_SaveWritesExternalIntentThenRealPrimaryThenClosedArtifactReceipt()
		{
			string source = Between(Read(Save), "private static KingdomQuickstartSaveSnapshot Save(", "private static void Check(");
			Ordered(source, "KingdomQuickstartBootTest.VerifyForSave(Game", "!KingdomScenarioSaveFiles.LoadPresent()",
				"Game.Running && !Game.Transient && !Game.DontSaveThisIsAReplay", "Game.SaveTask.IsCompleted",
				"KingdomQuickstartSaveState.Capture(Game, seed, request)", "TryEncode(snapshot, out string wire)",
				"WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile), wire)", "QUICKSTART-SAVE-BEGIN",
				"Game.SaveGame(\"Primary\")", "task?.GetAwaiter().GetResult()",
				"!SaveError && BootstrapCalls == 0", "KingdomQuickstartSaveState.Verify(Game, snapshot)",
				"Directory.GetFiles(directory).Length == 3", "file.ReadByte() == 31 && file.ReadByte() == 139",
				"HashFile(primary", "HashFile(Path.Combine(directory, \"Primary.json\")",
				"KingdomScenarioSaveFiles.HashText(wire)", "WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), receipt)");
			Contains(source, "Directory.GetDirectories(directory).Length == 0", "Path.GetFileName(path) == \"Cache.db\"",
				"ReadText(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), 512) == receipt", "MaxWireChars) == wire");
			Assert.IsFalse(Regex.IsMatch(source, @"\.(?:SetStringGameState|SetIntGameState|Delete|Move)\s*\("));
		}

		[Test]
		public void SourceContract_SnapshotReproofBracketsPhysicalObservationWithoutMintingAnId()
		{
			string source = Read(State);
			Ordered(Between(source, "internal static void Verify(", "private static KingdomQuickstartSaveSnapshot Read("),
				"TryEncode(Snapshot, out string expected)", "TryEncode(Read(Game, Snapshot.Seed, request), out string before)",
				"before == expected", "KingdomQuickstartBootstrap.NativeVerifyFreshBoot(",
				"TryEncode(Read(Game, Snapshot.Seed, request), out string after)", "after == expected");
			Contains(source, "founder._BaseID > 0", "founder.Property.TryGetValue(\"id\", out founderId)",
				"founder.IntProperty?.ContainsKey(\"id\") != true", "GetSystem<KingdomScenarioAutoRunner>() == null",
				"!KingdomNativeRegressionContext.HasAnyState(Game, KingdomScenarioNewGameGate.RequestState)",
				"!KingdomNativeRegressionContext.HasAnyState(Game, KingdomScenarioSaveFiles.SnapshotKey)",
				"Game.Turns, Game.TimeTicks, Game.ActionTicks, Game.PlayerActionTicks",
				"KingdomScenarioDurableState.ProvesExactText(key, expected)");
			Assert.IsFalse(Regex.IsMatch(source, @"founder\.(?:ID|IDIfAssigned)\b"));
			Assert.IsFalse(Regex.IsMatch(source, @"\.(?:SetStringGameState|SetIntGameState|SetZoneProperty|RequirePart|AddObject)\s*\("));
		}

		[Test]
		public void SourceContract_QuickstartEnvelopeHasADistinctStrictLoadBranch()
		{
			string source = Read(Entry);
			Ordered(source, "HashText(SnapshotWire) == Request.SnapshotSha256",
				"SnapshotWire.StartsWith(KingdomQuickstartSaveSnapshotCodec.Prefix, StringComparison.Ordinal)",
				"TryDecode(SnapshotWire, out QuickstartSnapshot)", "QuickstartSnapshot.GameId == Request.GameId",
				"else if (KingdomSubsidenceRungSaveSnapshotCodec.MatchesPrefix(SnapshotWire))",
				"PrimarySha256", "InfoSha256", "CacheSha256", "Armed = true",
				"XRLGame.LoadGame(Path.Combine(save, \"Primary\"), Session: false, ShowPopup: false)",
				"ReferenceEquals(The.Game, loaded)", "if (QuickstartSnapshot != null)",
				"KingdomQuickstartLoadTest.VerifyLoaded(loaded)", "quickstartVerified = true", "return;",
				"KingdomScenarioLoadWitness.VerifyRecovered");
			Contains(source, "bool quickstartVerified = false", "Result = Barrier.Pending", "Barrier.Start(Load)",
				"finally { Armed = false; }", "KingdomQuickstartLoadTest.Finish(quickstartVerified)");
			Assert.IsFalse(Regex.IsMatch(source, @"\b(?:RunGame|TryRestoreModsAndLoadAsync)\s*\("));
		}

		[Test]
		public void SourceContract_PreRestorationCallbackUsesExactPlayerBeforeOtherWitnessLanes()
		{
			string source = Read("Harness/KingdomScenarioLoadWitness.cs");
			Ordered(source, "HarmonyPrefix, HarmonyPriority(Priority.First)",
				"!KingdomScenarioLoadEntry.Armed || __0 != \"GameRestored\"",
				"if (KingdomScenarioLoadEntry.QuickstartSnapshot != null)",
				"ReferenceEquals(__instance, The.Player)", "KingdomQuickstartLoadTest.BeforeActivation()",
				"return;", "if (KingdomScenarioLoadEntry.RungSnapshot != null)");
			Ordered(Read(Load), "Attempts++", "Attempts == 1 && Witnessed == null && BootstrapCalls == 0",
				"KingdomScenarioLoadReaderWitness.Releases == 1 && !KingdomScenarioLoadReaderWitness.HadErrors",
				"KingdomQuickstartSaveState.Verify(game, KingdomScenarioLoadEntry.QuickstartSnapshot)",
				"Check(BootstrapCalls == 0", "Witnessed = game", "QUICKSTART-LOAD-PREACTIVATION");
		}

		[Test]
		public void SourceContract_LoadReproofRequiresActualCompletionAndMakesOnlyLimitedClaims()
		{
			string source = Read(Load);
			Contains(Between(source, "internal static void VerifyLoaded(", "internal static void Finish("),
				"Failure == null && Attempts == 1 && ReferenceEquals(Witnessed, Game) && BootstrapCalls == 0",
				"KingdomQuickstartSaveState.Verify(Game, KingdomScenarioLoadEntry.QuickstartSnapshot)",
				"KingdomScenarioLoadReaderWitness.Releases == 1", "!KingdomScenarioLoadReaderWitness.HadErrors",
				"SaveDirectory(root, Game.GameID)");
			Ordered(Between(source, "internal static void Finish(", "private static void Check("),
				"Check(Verified", "VerifyLoaded(The.Game)", "QUICKSTART-LOAD-COMPLETE\", true");
			Contains(source, "graceful-quit=false", "bootstrap-replay=false", "ordinary-acceptance=false",
				"QUICKSTART-LOAD-COMPLETE\", false");
			Contains(Read(Save), "cold-load=false", "ordinary-acceptance=false",
				"HarmonyPatch(typeof(XRLGame), \"SaveGameError\")", "NoteSaveError(__instance)");
			Contains(Read(Boot), "KingdomQuickstartSaveTest.BootstrapCalled(Current)",
				"KingdomQuickstartLoadTest.BootstrapCalled(Current)");
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		private static string Compact(string value) { return Regex.Replace(value, @"\s+", ""); }
		private static string Between(string source, string first, string last)
		{
			source = Compact(source); first = Compact(first); last = Compact(last);
			int begin = source.IndexOf(first, StringComparison.Ordinal); Assert.GreaterOrEqual(begin, 0, first);
			int end = source.IndexOf(last, begin + first.Length, StringComparison.Ordinal); Assert.Greater(end, begin, last);
			return source.Substring(begin, end - begin);
		}
		private static void Contains(string source, params string[] tokens)
		{ foreach (string token in tokens) StringAssert.Contains(Compact(token), Compact(source), token); }
		private static void Ordered(string source, params string[] tokens)
		{
			source = Compact(source); int cursor = 0;
			foreach (string token in tokens)
			{
				string expected = Compact(token); int at = source.IndexOf(expected, cursor, StringComparison.Ordinal);
				Assert.GreaterOrEqual(at, cursor, token); cursor = at + expected.Length;
			}
		}
	}
}
#endif
