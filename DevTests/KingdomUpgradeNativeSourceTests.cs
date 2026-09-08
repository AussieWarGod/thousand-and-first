#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	// Source wiring tripwires only; no source assertion proves native serialization or upgrade safety.
	[TestFixture]
	public sealed class KingdomUpgradeNativeSourceTests
	{
		[Test]
		public void ObserverRequiresOneShotArmAndRealSaveWithoutCallingIt()
		{
			string source = Read("Harness/KingdomUpgradeSource.cs");
			foreach (string token in new[] { "[WishCommand(\"kingdom:upgrade-arm\", null)]", "!Attempted",
				"TryRequest(Request, out Case)", "RequiresStorageExpansion", "if (!Armed || name != \"Primary\")",
				"Thread.CurrentThread == XRLCore.CoreThread", "!copyCache && !copyPrimary", "state.Exact()",
				"InheritanceWrites > 0", "TransitionWrites > 0", "LegacyWrites > 0", "PrimaryPaths > 0 && InfoPaths > 0" }) StringAssert.Contains(token, source);
			foreach (string forbidden in new[] { ".SaveGame(", ".SetStringGameState(", ".RequireSystem", ".Normalize(",
				".Delete(", ".Copy(", ".Move(", "FileShare.ReadWrite" }) StringAssert.DoesNotContain(forbidden, source);
			StringAssert.DoesNotContain("HashFile(Path.Combine(Directory, \"Cache.db\")", source);
			string patches = Read("Harness/KingdomUpgradeSourcePatches.cs");
			StringAssert.Contains("typeof(XRLGame), \"SaveGame\"", patches);
			StringAssert.Contains("typeof(XRLGame), \"SaveGameError\"", patches);
			StringAssert.DoesNotContain("ref Task", patches);
			StringAssert.DoesNotContain("static bool Prefix", patches);
		}

		[Test]
		public void ExactFieldGraphNeverInvokesNativeWritersOrReflectiveSetters()
		{
			string graph = Read("Harness/KingdomUpgradeGraph.cs"), state = Read("Harness/KingdomUpgradeState.cs");
			foreach (string token in new[] { "BindingFlags.DeclaredOnly", "GetValue(value)", "List<string>",
				"ReturnLedgerEnvelope", "CanonicalBodyKeys", "LegacyText" }) StringAssert.Contains(token, graph);
			foreach (string forbidden in new[] { ".SetValue(", "GetProperty(", "Activator.", "SerializationWriter",
				".Normalize(", "RequireSystem", "SetObjectGameState(" })
			{ StringAssert.DoesNotContain(forbidden, graph); StringAssert.DoesNotContain(forbidden, state); }
			StringAssert.Contains("ReferenceEquals(current[i], References[i])", state);
			StringAssert.Contains("legacy.Compose() == shape.LegacyText", state);
			StringAssert.Contains("TryValidateRealmTransition", state);
			StringAssert.Contains("\"2.0.211.51\"", state);
			StringAssert.Contains("new KingdomSealStore(path).ReadStage(origin)", state);
			StringAssert.Contains("seal-stage-readable=true", state);
		}

		[Test]
		public void FourthLoadRoutePreservesExistingSingleSaveGuards()
		{
			string entry = Read("Harness/KingdomScenarioLoadEntry.cs");
			StringAssert.Contains("UpgradeSnapshot == null ? KingdomScenarioSaveFiles.SaveDirectory", entry);
			StringAssert.Contains("KingdomUpgradeFiles.SaveDirectory(root, Request.GameId)", entry);
			StringAssert.Contains("UpgradeSnapshot != null || files.Length == 3 && Directory.GetDirectories(save).Length == 0", entry);
			StringAssert.Contains("XRLGame.LoadGame(Path.Combine(save, \"Primary\")", entry);
			StringAssert.Contains("Session: false, ShowPopup: false", entry);
			StringAssert.Contains("KingdomUpgradeLoad.VerifyLoaded(loaded)", entry);
			string files = Read("Harness/KingdomUpgradeFiles.cs");
			StringAssert.DoesNotContain("GetDirectories(saves)", files);
			StringAssert.Contains("FileMode.CreateNew", files);
			StringAssert.Contains("facts.NumberOfLinks == 1", files);
		}

		[Test]
		public void ObserversRequirePreRepairPreNormalizationAndPostLoadEvidence()
		{
			string patches = Read("Harness/KingdomUpgradeLoadPatches.cs"), load = Read("Harness/KingdomUpgradeLoad.cs");
			foreach (string token in new[] { "\"ReadNamedFields\"", "\"TryValidateSavedShape\"", "\"DisableRecovery\"",
				"\"SetRepair\"", "HarmonyFinalizer" }) StringAssert.Contains(token, patches);
			StringAssert.DoesNotContain("static bool Prefix", patches);
			foreach (string token in new[] { "ShapeEntries == 1", "ShapeSuccesses == 1", "Repairs == 0",
				"Count(RawTransitions, state.Transition) == 1", "Count(RawLegacies, state.Transition.Legacy) == 1",
				"PreactivationState.Matches(Expected)", "ordinary-ui-acceptance=false" }) StringAssert.Contains(token, load);
			StringAssert.Contains("KingdomUpgradeLoad.BeforeActivation()", Read("Harness/KingdomScenarioLoadWitness.cs"));
			foreach (string forbidden in new[] { ".SetStringGameState(", "RequireSystem", ".Normalize(", ".SaveGame(",
				"TryCompleteRealmReturn(", "TryCommit" }) StringAssert.DoesNotContain(forbidden, load);
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
	}
}
#endif
