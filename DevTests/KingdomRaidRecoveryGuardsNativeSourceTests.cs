#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Wiring tripwires only; actual engine execution owns guard evidence.</summary>
	[TestFixture]
	public sealed class KingdomRaidRecoveryGuardsNativeSourceTests
	{
		private const string Checks = "Harness/KingdomRaidRecoveryGuardsNativeChecks.cs";
		private const string Provider = "Harness/KingdomRaidRecoveryGuardsNativeProvider.cs";
		[Test]
		public void GuardsUseRealEarnedReadyWithoutLifecycleForcing()
		{
			string source = Read(Checks);
			Ordered(source, "Foreign = Manager.GetZone(ForeignId)", "LaunchAndContact()",
				"KingdomRaids.TryAcceptRecovery(", "Actors[i].Die(Force: true)", "Rows.Dead(Actors[i])",
				"Fixture.Activate()", "Recovery(KingdomRaidRecoveryState.Ready)",
				"BoundCase(Zone, \"same-zone\")", "BoundCase(Foreign, \"foreign-zone\")");
			Assert.IsFalse(Regex.IsMatch(source, @"\.RecoveryState\s*=(?!=)"));
			foreach (string token in new[] { ".RemoveObject(", ".Destroy(", ".Obliterate(",
				"KingdomRaids.RaiderDying(", ".FinishQuest(", ".FinishQuestStep(" }) StringAssert.DoesNotContain(token, source);
			StringAssert.Contains("ordinary-acceptance=false; save-load=untested", source);
		}
		[Test]
		public void ActualBindingsRefuseWithoutScansAndAlwaysDispose()
		{
			string source = Read(Checks);
			int start = source.IndexOf("private void BoundCase(", StringComparison.Ordinal);
			string bound = source.Substring(start, source.IndexOf("private void Watch(", start, StringComparison.Ordinal) - start);
			Ordered(bound, "KingdomSurvey.TakeCustodyOnly(boundZone), 1, 1", "survey.BindPass()",
				"KingdomSurvey.HasBoundPass", "KingdomSurvey.ActiveFor(Zone)",
				"KingdomRaids.TryResolveRecovery(Fixture.System, Zone, out failure), 0, 0",
				"Require(!resolved", "KingdomSurvey.TryTakeUnboundRecovery(Zone, out fresh), 0, 0",
				"Require(!captured && fresh == null", "survey.LoadedObjects[i]",
				"finally { if (scope != null) scope.Dispose(); Unbound(); }");
			foreach (string token in new[] { "BoundSurvey =", "BoundDepth =", "BoundDepth--" }) StringAssert.DoesNotContain(token, source);
			string observer = Read(Provider);
			StringAssert.Contains("internal static void Prefix(Zone zone)", observer);
			StringAssert.Contains("internal static void Prefix(Zone __instance)", observer);
			StringAssert.Contains("current.Latch(\"foreign scan owner\")", observer);
			StringAssert.Contains("Objects == Custody", observer);
			StringAssert.DoesNotContain("__result", observer);
			StringAssert.DoesNotContain("static bool Prefix", observer);
		}
		[Test]
		public void RealPauseReprovesOwnerThenPermitsOneExplicitCompletion()
		{
			string source = Read(Checks);
			Ordered(source, "Options.SetOption(KingdomMaster.OptionId, \"No\")", "PauseRequested = true",
				"Options.GetOption(KingdomMaster.OptionId) == \"No\"", "Stable(); Unbound();",
				"KingdomMaster.ObserveAutomaticWake(Fixture.System, Tick), 0, 0", "Paused = true",
				"\"paused-automatic-wake\"", "!KingdomMaster.AutomaticWorkAllowed(Fixture.System)",
				"\"paused-explicit-turn-in\"", "KingdomRaids.TryResolveRecovery(Fixture.System, Zone, out failure), 1, 64",
				"Recovery(KingdomRaidRecoveryState.Resolved)", "Quest.Exact(true)",
				"!KingdomRaids.TryResolveRecovery(", "Same(settled, Wire())");
			foreach (string token in new[] { "system.MasterResumeToken == ResumeToken", "system.MasterAppliedResumeToken == AppliedToken",
				"Paused ? KingdomMasterLatchValue.Disabled : KingdomMasterLatchValue.Enabled",
				"PauseRequested ? !KingdomMaster.ConfiguredEnabled", "KingdomRaids.HasWatchDisarray(Fixture.System)" }) StringAssert.Contains(token, source);
			StringAssert.DoesNotContain("Options.SetOption(KingdomMaster.OptionId, \"Yes\")", source);
		}
		[Test]
		public void FreshProviderAndPersonaSealExactGuardCase()
		{
			string source = Read(Provider);
			Ordered(source, "if (!Eligible(game, zone, out string failure))", "game.SetStringGameState(Receipt, \"intent\")",
				"ProvesExactText(Receipt, \"intent\")", "KingdomRaidRecoveryGuardsNativeChecks.Run(",
				"ReferenceEquals(The.Game, game)", "game.SetStringGameState(Receipt, report)");
			foreach (string token in new[] { "HasAnyState(game, Receipt)", "HasQuickstartState(game)", "KingdomSurvey.HasBoundPass",
				"!KingdomRaidRecoveryGuardsScan.Vacant", "script.Count != 3", "script[1] != Verb",
				"KingdomScenarioTransactionShape.None" }) StringAssert.Contains(token, source);
			string persona = Read("Tools/personas/raid-recovery-guards-native-check.persona");
			foreach (string row in new[] { "REQUEST=founding-first-city", "START=8.22@40,12", "VERBS=raid-recovery-guards-native-check",
				"SCRIPT=stagedigest;raid-recovery-guards-native-check;stagedigest",
				"EXPECT=stagedigest:OK~founded=false,raid-recovery-guards-native-check:OK~cases=1 passed=1 failed=0,stagedigest:OK~founded=true,COMPLETE" })
				Assert.AreEqual(1, Regex.Matches(persona, "(?m)^" + Regex.Escape(row) + "$" ).Count);
		}
		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		private static void Ordered(string source, params string[] tokens)
		{
			int cursor = 0;
			foreach (string token in tokens)
			{
				int at = source.IndexOf(token, cursor, StringComparison.Ordinal);
				Assert.GreaterOrEqual(at, cursor, "Missing or reordered: " + token); cursor = at + token.Length;
			}
		}
	}
}
#endif
