#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomSaveSystemRosterRuntimeSourceTests
	{
		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		[Test]
		public void LoadPatchOccupiesTheExactPostImportPreEventSeam()
		{
			string patch = Read("Core/KingdomSaveSystemRosterRuntime.Patches.cs");
			string evidence = Read("Core/KingdomSaveSystemRosterRuntime.LoadEvidence.cs");
			StringAssert.Contains("HarmonyPatch(typeof(XRLGame), \"ImportGameState\")", patch);
			StringAssert.Contains("private static void Postfix(XRLGame __instance)", patch);
			StringAssert.Contains("XRL/XRLGame.cs:1823-1828", patch);
			StringAssert.Contains(":1910", patch);
			StringAssert.Contains(":1946-1954", patch);
			StringAssert.Contains(":2476-2508", patch);
			StringAssert.Contains("HarmonyPatch(typeof(XRLGame), \"LoadGame\")", evidence);
			StringAssert.Contains("HarmonyPatch(typeof(SerializationReader), \"Start\")", evidence);
			StringAssert.Contains("internal const string ModId = \"r_ThousandAndFirst\"", evidence);
			StringAssert.Contains("Reader.ModVersions.ContainsKey(ModId)", evidence);
			StringAssert.Contains("XRL/World/SerializationReader.cs:180-224", evidence);
			StringAssert.Contains(":2157-2178", evidence);
			AssertBefore(patch, "KingdomSaveSystemRosterLoadEvidence.Consume",
				"ValidateAfterImport(__instance");
			AssertBefore(patch, "ValidateAfterImport(__instance", "load entered recovery");
		}

		[Test]
		public void ExistingSaveFirstInstallNeedsPositiveHeaderAndNoTafFootprint()
		{
			string first = Read("Core/KingdomSaveSystemRosterRuntime.FirstInstall.cs");
			string engine = Read("Core/KingdomSaveSystemRosterRuntime.Engine.cs");
			StringAssert.Contains("SavedModEvidenceKnown && !SavedModWasPresent", first);
			StringAssert.Contains("!MarkerPresent", first);
			StringAssert.Contains("KingdomSaveSystemRosterRuntimePlan.Empty(Counts)", first);
			StringAssert.Contains("!HasKnownFootprint(Game)", first);
			StringAssert.Contains("key == KingdomIdentityFenceRules.StateKey", first);
			StringAssert.Contains("KingdomRemovalCoverage.IsOwnedGlobalState(key)", first);
			StringAssert.Contains("CleanFirstInstall(Game, SavedModEvidenceKnown",
				engine);
			StringAssert.Contains("KingdomSaveSystemRosterLoadedContext.ProvenFirstInstall",
				engine);
			StringAssert.Contains("RuleContext(loadedContext)", engine);
			AssertBefore(engine, "KingdomSaveSystemRosterLoadedContext.ProvenFirstInstall",
				"RuleContext(loadedContext)");
			StringAssert.Contains("this returns false", first);
		}

		[Test]
		public void RegistryIsCountedExactlyBeforeAnyRequireCanCreateAShell()
		{
			string registry = Read("Core/KingdomSaveSystemRosterRuntime.Registry.cs");
			string engine = Read("Core/KingdomSaveSystemRosterRuntime.Engine.cs");
			StringAssert.Contains("XRL/XRLGame.cs:286-332", registry);
			StringAssert.Contains("XRL/XRLGame.cs:1592-1603", registry);
			StringAssert.Contains("XRL/World/SerializationReader.cs:180-224", registry);
			StringAssert.Contains(":1320-1339", registry);
			StringAssert.Contains(":2120-2133", registry);
			StringAssert.Contains("Type type = system.GetType();", registry);
			StringAssert.Contains("type == typeof(KingdomSystem)", registry);
			StringAssert.Contains("type == typeof(KingdomSeal)", registry);
			StringAssert.Contains("type == typeof(KingdomCivicMemorySystem)", registry);
			StringAssert.Contains("type == typeof(KingdomSuccession)", registry);
			StringAssert.Contains("type == typeof(KingdomInheritanceLifecycle)", registry);

			int validate = engine.IndexOf("ValidateAfterImport", StringComparison.Ordinal);
			int snapshot = engine.IndexOf("Snapshot(Game)", validate, StringComparison.Ordinal);
			int ensure = engine.IndexOf("Ensure(Game, plan.EnsureMask", snapshot,
				StringComparison.Ordinal);
			Assert.That(snapshot, Is.GreaterThan(validate));
			Assert.That(ensure, Is.GreaterThan(snapshot));
			string snapshotBody = registry.Substring(
				registry.IndexOf("internal static KingdomSaveSystemRosterCounts Snapshot",
					StringComparison.Ordinal),
				registry.IndexOf("internal static void Ensure", StringComparison.Ordinal)
					- registry.IndexOf("internal static KingdomSaveSystemRosterCounts Snapshot",
						StringComparison.Ordinal));
			StringAssert.DoesNotContain("RequireSystem", snapshotBody);
		}

		[Test]
		public void RecoveryUsesOneCallbackAndLatchesEveryDuplicateCarrier()
		{
			string bindings = Read("Core/KingdomSaveSystemRosterRuntime.Bindings.cs");
			string engine = Read("Core/KingdomSaveSystemRosterRuntime.Engine.cs");
			StringAssert.Contains("internal delegate void KingdomSaveSystemRosterRecoveryCallback",
				bindings);
			StringAssert.Contains("for (int i = 0; i < Game.Systems.Count; i++)", bindings);
			StringAssert.Contains("RefuseSaveRosterLoss(cause)", bindings);
			StringAssert.Contains("RefuseRosterLoss(cause)", bindings);
			StringAssert.Contains("LoadFailed = true;", bindings);
			StringAssert.Contains("SealDisabled = true;", bindings);
			StringAssert.Contains("SuccessionDisabled = true;", bindings);
			StringAssert.Contains("Do not neutralize fields here", bindings);
			StringAssert.Contains("KingdomSaveSystemRosterRecoveryBindings.Refuse", engine);
			AssertBefore(engine, "Ensure(Game, EnsureMask", "RecoveryBindings.Refuse");
		}

		[Test]
		public void RecoveryOneWayDisablesEveryInheritanceCarrierAndMutationHook()
		{
			string bindings = Read("Core/KingdomSaveSystemRosterRuntime.Bindings.cs");
			string guard = Read(
				"Core/KingdomSaveSystemRosterRuntime.InheritanceGuard.cs");
			string lifecycle = Read("World/KingdomInheritanceLifecycle.cs");
			StringAssert.Contains("type == typeof(KingdomInheritanceLifecycle)", bindings);
			StringAssert.Contains("KingdomSaveSystemRosterInheritanceGuard.Refuse", bindings);
			StringAssert.Contains(
				"ConditionalWeakTable<KingdomInheritanceLifecycle, Witness>", guard);
			AssertBefore(guard, "if (!Refused.TryGetValue(System", "Refused.Add(System");
			StringAssert.Contains("First cause is never overwritten", guard);
			StringAssert.Contains("typeof(XRLGame), typeof(IEventRegistrar)", guard);
			StringAssert.Contains("new Type[] { typeof(XRLGame) }", guard);
			StringAssert.Contains("new Type[] { typeof(AfterGameLoadedEvent) }", guard);
			StringAssert.Contains("new Type[] { typeof(EndTurnEvent) }", guard);
			StringAssert.Contains("new Type[] { typeof(ZoneBuiltEvent) }", guard);
			Assert.That(Count(lifecycle, "public override bool HandleEvent("), Is.EqualTo(3),
				"new inheritance mutation hooks must join the one-way recovery guard");
			Assert.That(Count(guard, "__result = true; return false;"), Is.EqualTo(3));
		}

		[Test]
		public void BootstrapSeparatesLawfulLegacyAbsenceFromRecoveryAndNewGame()
		{
			string engine = Read("Core/KingdomSaveSystemRosterRuntime.Engine.cs");
			string registry = Read("Core/KingdomSaveSystemRosterRuntime.Registry.cs");
			string c18 = Read("Core/KingdomCivicMemorySystem.LoadGuard.cs");
			StringAssert.Contains("SaveRosterHasDecodedRealm", engine);
			StringAssert.Contains("KingdomSaveSystemRosterContext.LegacyDecodedRealm", engine);
			StringAssert.Contains("before.CivicMemory == 0", engine);
			StringAssert.Contains("TryInitializeNewGame", engine);
			StringAssert.Contains("Ensure(Game, plan.EnsureMask, false)", engine);
			StringAssert.Contains("if (LegacyCivicAbsence) memory.AdoptRosterLegacyAbsence();",
				registry);
			StringAssert.Contains("internal void AdoptRosterLegacyAbsence()", c18);
			AssertBefore(c18, "Records.AdoptAbsent();", "CustomReadCompleted = true;");
		}

		[Test]
		public void ExplicitNewGameHookRunsAfterNativeSystemAndSingletonInitialization()
		{
			string patch = Read("Core/KingdomSaveSystemRosterRuntime.Patches.cs");
			string loader = Read("Core/KingdomLoader.cs");
			string legacy = Read("Core/KingdomSaveSystemRosterRuntime.LegacyOptionals.cs");
			StringAssert.Contains("[PlayerMutator]", patch);
			StringAssert.Contains("KingdomSaveSystemRosterNewGameLoader", patch);
			StringAssert.Contains("TryInitializeNewGame(The.Game", patch);
			StringAssert.DoesNotContain("[PlayerMutator]", loader);
			StringAssert.DoesNotContain("RequireSystem<KingdomSystem>()", loader);
			StringAssert.DoesNotContain("RequireSystem<KingdomSeal>()", loader);
			StringAssert.DoesNotContain("RequireSystem<KingdomCivicMemorySystem>()", loader);
			StringAssert.Contains("GetSystem<KingdomSystem>()", loader);
			StringAssert.Contains("GetSystem<KingdomSeal>()", loader);
			StringAssert.Contains("GetSystem<KingdomCivicMemorySystem>()", loader);
			AssertBefore(loader, "GetSystem<KingdomCivicMemorySystem>()",
				"if (kingdomSystem == null || seal == null || memory == null) return;");
			StringAssert.Contains("QudGamemodeModule.cs:341-364", legacy);
			StringAssert.Contains("QudGameBootModule.cs:256-270,303-307", legacy);
		}

		[Test]
		public void OptionalSystemsAreOptionalOnlyWhenTheirOwnAuthorityIsAbsent()
		{
			string optional = Read("Core/KingdomSaveSystemRosterRuntime.LegacyOptionals.cs");
			string engine = Read("Core/KingdomSaveSystemRosterRuntime.Engine.cs");
			string evidence = Read(
				"Core/KingdomSaveSystemRosterRuntime.ObjectStateEvidence.cs");
			string flow = Read("Core/KingdomSaveSystemRosterRuntime.LoadEvidence.cs");
			StringAssert.Contains("only Kingdom mode adds it", optional);
			StringAssert.Contains("KingdomSuccessionRules.ModeOn", optional);
			StringAssert.Contains("Counts.Succession == 0", optional);
			StringAssert.Contains("Kingdom mode lost its required Succession carrier",
				optional);
			StringAssert.Contains("Counts.Inheritance == 0 && RequiresInheritanceLifecycle",
				optional);
			StringAssert.Contains("state.Phase != KingdomInheritancePhase.Empty", optional);
			StringAssert.Contains("if (InheritanceAuthorityUnreadable) return true;", optional);
			StringAssert.Contains("XRL/XRLGame.cs:1615-1629", evidence);
			StringAssert.Contains("XRL/World/SerializationReader.cs:362-365", evidence);
			StringAssert.Contains(":1072-1087", evidence);
			StringAssert.Contains("BeginObjectStates(Reader)", evidence);
			StringAssert.Contains("ObserveObjectStateKey(__instance, __result)", evidence);
			StringAssert.Contains("EnterObjectStateValue(__instance)", evidence);
			StringAssert.Contains("LeaveObjectStateValue(__instance, __result)", evidence);
			StringAssert.Contains("InheritanceSingletonUnreadable", flow);
			StringAssert.Contains("MissingRequiredOptionalMask(Game, before", engine);
			StringAssert.Contains("plan.EnsureMask | missing", engine);
			AssertBefore(engine, "KingdomSaveSystemRosterRuntimePlan.Create(context",
				"MissingRequiredOptionalMask(Game, before");
			StringAssert.Contains("!prepared && MissingRequiredOptionalMask(Game, counts",
				engine);
		}

		[Test]
		public void SavePrefixProvesThePostRemovalRosterBeforeWriterOutput()
		{
			string patch = Read("Core/KingdomSaveSystemRosterRuntime.Patches.cs");
			string engine = Read("Core/KingdomSaveSystemRosterRuntime.Engine.cs");
			StringAssert.Contains("HarmonyPatch(typeof(XRLGame), \"SaveSystems\")", patch);
			StringAssert.Contains("[HarmonyPriority(Priority.Last)]", patch);
			AssertBefore(patch, "__instance.RemoveFlaggedSystems();",
				"TryPrepareBeforeSave(__instance");
			StringAssert.Contains("XRL/XRLGame.cs:1580-1590", patch);
			StringAssert.Contains(":2324-2328", patch);
			StringAssert.Contains(":2335-2356", patch);
			StringAssert.Contains("bootstrap is not lawful during a save", engine);

			int method = engine.IndexOf("internal static bool TryPrepareBeforeSave",
				StringComparison.Ordinal);
			int end = engine.IndexOf("private static bool ProveFinal", method,
				StringComparison.Ordinal);
			string body = engine.Substring(method, end - method);
			StringAssert.DoesNotContain("Ensure(Game", body);
			StringAssert.DoesNotContain("RequireSystem", body);
		}

		[Test]
		public void PreparedRemovalNeedsMarkerAbsenceZeroRosterAndExactPreservedFence()
		{
			string registry = Read("Core/KingdomSaveSystemRosterRuntime.Registry.cs");
			string marker = Read("Core/KingdomSaveSystemRosterRuntime.Marker.cs");
			StringAssert.Contains("if (Game == null || MarkerPresent", registry);
			StringAssert.Contains("!KingdomSaveSystemRosterRuntimePlan.Empty(Counts)", registry);
			StringAssert.Contains("KingdomRealmRetirementCodec.TryDecodeFence", registry);
			StringAssert.Contains("fence.GameId == Game.GameID", registry);
			StringAssert.Contains(
				"fence.Disposition == KingdomIdentityFenceDisposition.PreparedForRemoval", registry);
			StringAssert.Contains("TryClearForPreparedRemoval(XRLGame Game", marker);
			StringAssert.Contains("KingdomSaveSystemRosterContext.PreparedRemoval", marker);
			AssertBefore(marker, "Snapshot(Game)", "TryCommit(Game, plan.Decision");
		}

		[Test]
		public void RemovalClearsRosterByCasBeforeKeyPreviewAndCarriers()
		{
			string finalize = Read("Core/KingdomRealmRetirementAuthority.Finalize.cs");
			int cut = finalize.IndexOf("private static bool TryCutTerminalProjections",
				StringComparison.Ordinal);
			Assert.That(cut, Is.GreaterThanOrEqualTo(0));
			string body = finalize.Substring(cut);
			StringAssert.Contains("bool rosterPresent = The.Game.HasIntGameState", body);
			AssertBefore(body, "TryClearForPreparedRemoval(The.Game",
				"TryRemoveGlobalStates(System, exactGlobals");
			AssertBefore(body, "TryRemoveGlobalStates(System, exactGlobals",
				"TryRemoveAuxiliarySystems(System");
			StringAssert.Contains("!The.Game.HasIntGameState(KingdomSaveSystemRosterRules.StateKey)",
				body);
		}

		[Test]
		public void MarkerWritesAreExactCasWithPresenceAndRawReadback()
		{
			string marker = Read("Core/KingdomSaveSystemRosterRuntime.Marker.cs");
			StringAssert.Contains("TryResolveCas(Decision, present, raw", marker);
			StringAssert.Contains("Game.SetIntGameState(KingdomSaveSystemRosterRules.StateKey",
				marker);
			StringAssert.Contains("Game.RemoveIntGameState(KingdomSaveSystemRosterRules.StateKey)",
				marker);
			StringAssert.Contains("bool retained = Marker(Game, out int retainedRaw);", marker);
			StringAssert.Contains("retainedRaw != nextRaw", marker);
			StringAssert.Contains("no code can run while the mod is absent", marker);
			StringAssert.DoesNotContain("cleans saves while the mod is absent", marker);
		}

		private static void AssertBefore(string source, string first, string second)
		{
			int a = source.IndexOf(first, StringComparison.Ordinal);
			int b = source.IndexOf(second, StringComparison.Ordinal);
			Assert.That(a, Is.GreaterThanOrEqualTo(0), first);
			Assert.That(b, Is.GreaterThan(a), second);
		}

		private static int Count(string source, string value)
		{
			int count = 0;
			for (int at = 0; (at = source.IndexOf(value, at,
				StringComparison.Ordinal)) >= 0; at += value.Length) count++;
			return count;
		}
	}
}
#endif
