#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	// Wiring/absence pins only. Root must compile old overlays and run every native leg.
	[TestFixture]
	public sealed class KingdomUpgradeUnattendedSourceTests
	{
		[Test]
		public void OldSourceUsesProductionAuthoritiesInsteadOfWritingReceiptFields()
		{
			string provider = Read("Harness/KingdomUpgradeSourceProvider.cs");
			string driver = Read("Harness/KingdomUpgradeSourceDriver.cs");
			foreach (string token in new[] { "KingdomScenarioScript.TryRead", "script.Count == 3",
				"KingdomScenarioTransactionMarker.TryBegin", "KingdomScenarioFoundingStep.TryFound",
				"KingdomCitizenship.TryEnroll", "KingdomResidents.TryEnsureRow", "KingdomSurvey.Take",
				"KingdomResidentIdentity.Reconcile", "SetIntProperty(\"KingdomBorn\", 1)",
				"using ThousandAndFirst.Simulation.City;", "KingdomPolityProfileRuntime.TryReconcile", "KingdomSeal.TryFoundingCompleted" })
				StringAssert.Contains(token, provider);
			foreach (string token in new[] { "KingdomSeal.TryRetireGeneration", "PromoteRetirement(stage)",
				"KingdomInheritancePhase.Reserved", "lease.IsHeld",
				"KingdomUpgradeSource.Arm(null)", "Game.SaveGame(\"Primary\")", "state.Matches(saved)",
				"taf-upgrade-source-link-v1", "cache-bind-after-quit" }) StringAssert.Contains(token, driver);
			foreach (string forbidden in new[] { ".SetValue(", "new KingdomInheritanceState",
				"SetObjectGameState(", "Game.TimeTicks = ", "system.Population = ", "new KingdomSealRecord",
				"File.Delete(", "Directory.Delete(", "FileShare.ReadWrite" })
			{ StringAssert.DoesNotContain(forbidden, provider); StringAssert.DoesNotContain(forbidden, driver); }
		}

		[Test]
		public void SourceChecksSwallowedArmRefusalAndActualNativeSaveOutcome()
		{
			string source = Read("Harness/KingdomUpgradeSourceDriver.cs");
			foreach (string token in new[] { "SourceField(\"Fault\") == null", "SourceField(\"Armed\")",
				"SourceField(\"Completed\")", "save.IsCompleted && !save.IsFaulted && !save.IsCanceled",
				"PrimaryPaths > 0 && InfoPaths > 0", "SaveFault == null", "donor[2] != GameId",
				"ReferenceEquals(KingdomInheritanceLeaseOwner.Get(GameId, receipt), lease)",
				"KingdomUpgradeFiles.New" }) StringAssert.Contains(token, source);
			StringAssert.DoesNotContain(".SaveGame(", Read("Harness/KingdomUpgradeSource.cs"));
		}

		/// <summary>Issue #87. The 0.3.1 save-system roster marker is committed by a
		/// [PlayerMutator], which QudGameBootModule.BootGame runs strictly after every
		/// IGameStateSingleton.Initialize. An inheritor that opts in and initializes afterwards
		/// therefore carries an Inheritance carrier the marker never recorded, and 0.3.1's own
		/// SaveSystems prefix refuses the write. The opt-in must be the birth option instead.
		/// </summary>
		[Test]
		public void ReservedInheritorIsBornOptedInAndNeverArmsImportItself()
		{
			string driver = Read("Harness/KingdomUpgradeSourceDriver.cs");
			string provider = Read("Harness/KingdomUpgradeSourceProvider.cs");
			foreach (string forbidden in new[] { "SetOption(\"r_TAF_OptionLegacyImport\"",
				"Inheritance.Initialize()" })
			{ StringAssert.DoesNotContain(forbidden, driver); StringAssert.DoesNotContain(forbidden, provider); }
			foreach (string token in new[] { "internal void Armed()",
				"Options.GetOption(\"r_TAF_OptionLegacyImport\", \"No\") == \"Yes\"",
				"0.3.1 boot did not claim the exact copied donor legacy for this source",
				"boot-armed Reserved state does not hold its own live process-local lease",
				"if (verb == KingdomUpgradeSourceProvider.ReservedVerb) Armed(); else Pristine();" })
				StringAssert.Contains(token, driver);
			StringAssert.Contains("(verb == ReservedVerb ? \"Yes\" : \"No\")", provider);
			StringAssert.Contains("reserved source requires legacy import enabled at birth", provider);
			string options = Read("Tools/upgrade_profile_options.py");
			StringAssert.Contains("the v2 inheritor must be born with legacy import enabled", options);
			StringAssert.Contains("old donor must be born with legacy import disabled", options);
		}

		[Test]
		public void EmptyCampUsesRealTurnsAndRealSaveWithoutSerializedFixtureIntent()
		{
			string provider = Read("Harness/KingdomUpgradeStageProvider.cs");
			string checks = Read("Harness/KingdomUpgradeStageChecks.cs");
			foreach (string token in new[] { "advance 2400", "script.Count == Script.Length",
				"upgrade-stage-source.txt", "upgrade-stage-receipt.txt", "upgrade-stage-failure.txt" })
				StringAssert.Contains(token, provider);
			foreach (string token in new[] { "new KingdomWaterMaintenanceSetup(Game, Zone).Found()",
				"KingdomWaterMaintenanceSealEvidence.Verify", "Game.SaveGame(\"Primary\")",
				"taf-upgrade-stage-receipt-v1", "cache-bind-after-quit" }) StringAssert.Contains(token, checks);
			foreach (string forbidden in new[] { "SetObjectGameState(", "SetStringGameState(",
				"Game.TimeTicks = ", "System.Population = ", "new KingdomSealRecord", ".CloseCache(" })
				StringAssert.DoesNotContain(forbidden, checks);
		}

		[Test]
		public void FourFixedRecipesAreExplicitlyDeveloperOnly()
		{
			foreach (string name in new[] { "upgrade-source-donor", "upgrade-source-reserved",
				"upgrade-stage-source", "upgrade-downgrade-check" })
			{
				string persona = Read("Tools/personas/cross-version/" + name + ".persona");
				StringAssert.Contains("prepare-upgrade-profile.py", persona);
				StringAssert.Contains("SCRIPT=", persona);
				StringAssert.Contains("TIMEOUT=600", persona);
			}
			string inputs = Read("Tools/upgrade_profile_inputs.py");
			StringAssert.Contains("taf-upgrade-profile-v2", inputs);
			StringAssert.Contains("unattended detached-transition source is not implemented", inputs);
		}

		[Test]
		public void ConfinementViolationsLatchBeforeTheyCanBeCaughtByTheEngine()
		{
			string checks = Read("Harness/KingdomUpgradeStageChecks.cs");
			StringAssert.Contains("Enforcing boundary, not a passive observer", checks);
			StringAssert.Contains("f.Fault = f.Fault ?? error.GetType().Name + \": \" + error.Message; throw;", checks);
			StringAssert.Contains("even if an engine caller catches this exception", checks);
		}

		[Test]
		public void ReaderClaimsOnlyTheDedicatedMainMenuRouteBeforeAutoStart()
		{
			string script = Read("Harness/KingdomDowngradeScript.cs");
			foreach (string token in new[] { "typeof(KingdomScenarioTestGameEntry), \"Autostart\"",
				"Claimed = true", "return false;", "KingdomDowngradeProbe.AdmitScript(menu, root, requestHash)",
				"The.Game == null", "ExactScriptWithoutRequest" }) StringAssert.Contains(token, script);
			foreach (string forbidden in new[] { "NewGame(", "LoadGame(", "Popup.", "SetOption(",
				"KingdomScenarioVerbs.Invoke", ".Show(" }) StringAssert.DoesNotContain(forbidden, script);
			string probe = Read("Harness/KingdomDowngradeProbe.cs");
			StringAssert.DoesNotContain("KingdomDowngradeScript", probe);
			StringAssert.Contains("ReferenceEquals(ScriptMenu, __instance)", probe);
			StringAssert.Contains("RequestAdmission(root, requestHash)", probe);
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
	}
}
#endif
