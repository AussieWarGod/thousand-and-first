#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source wiring only; actual engine runs own pause/resume evidence.</summary>
	[TestFixture]
	public sealed class KingdomRaidMasterTurnNativeSourceTests
	{
		private const string Provider = "Harness/KingdomRaidMasterTurnNativeProvider.cs";
		private const string Checks = "Harness/KingdomRaidMasterTurnNativeChecks.cs";
		[Test]
		public void FreshProviderSealsRealPauseAndResumeTurns()
		{
			string source = Read(Provider), persona = Read("Tools/personas/raid-master-turn-native-check.persona");
			foreach (string token in new[] { "script.Count == 7", "script[2] == \"advance 1\"", "script[4] == \"advance 2\"",
				"HasAnyState(game, Receipt)", "HasQuickstartState(game)", "KingdomScenarioTransactionShape.None" }) StringAssert.Contains(token, source);
			foreach (string row in new[] { "REQUEST=founding-first-city", "START=8.22@40,12",
				"SCRIPT=stagedigest;raid-recovery-pause-setup;advance 1;raid-recovery-pause-resume;advance 2;raid-recovery-pause-check;stagedigest",
				"VERBS=raid-recovery-pause-setup,raid-recovery-pause-resume,raid-recovery-pause-check",
				"EXPECT=stagedigest:OK~founded=false,raid-recovery-pause-setup:OK~step=raid-recovery-pause-setup ok=True,advance:OK,raid-recovery-pause-resume:OK~step=raid-recovery-pause-resume ok=True,advance:OK,raid-recovery-pause-check:OK~cases=1 passed=1 failed=0,stagedigest:OK~founded=true,COMPLETE" })
				Assert.AreEqual(1, Regex.Matches(persona, "(?m)^" + Regex.Escape(row) + "$" ).Count);
		}
		[Test]
		public void ActualCallbacksAreObservedWithoutReplacingProductionControlFlow()
		{
			string source = Read(Provider);
			foreach (string token in new[] { "typeof(EndTurnEvent)", "\"ObserveAutomaticWake\"", "\"OnWorldWake\"",
				"internal static void Prefix", "internal static void Postfix", "bool __result",
				"Options.SetOption(KingdomMaster.OptionId, enabled ? \"Yes\" : \"No\")",
				"finally { Setting = false; }", "Capture(6)", "Setting && (stage == 1 || stage == 2)" }) StringAssert.Contains(token, source);
			foreach (string token in new[] { "static bool Prefix", "ref bool __result", "__result =", "KingdomMaster.ObserveAutomaticWake(" }) StringAssert.DoesNotContain(token, source);
			Assert.IsFalse(Regex.IsMatch(source, @"\.(MasterOption|MasterResumeToken|MasterAppliedResumeToken|Turns|TimeTicks|Energy)\s*=(?!=)"));
		}
		[Test]
		public void EveryDispatchReprovesTransitionAndLaterWorkAgainstActualClocks()
		{
			string source = Read(Provider);
			foreach (string token in new[] { "Dispatches < 32", "checked(StartTick + Dispatches)", "checked(StartTurns + Dispatches)",
				"ResumeApplications == 0", "ResumeApplications++", "ResumeTick = tick", "tick > ResumeTick && LastAllowed",
				"DispatchRaids == 0 && !LastAllowed", "Dispatches >= minimum && Dispatches == Ends",
				"Game.TimeTicks == checked(LastTick + 1)", "Game.Turns == checked(LastTurns + 1)", "Fault == null" }) StringAssert.Contains(token, source);
			StringAssert.DoesNotContain("Dispatches == 1", source);
			StringAssert.Contains("Require(Same(ActiveWire, PausedWire) && Witness.Raids == 0", Read(Checks));
		}
		[Test]
		public void EarnedRecoverySurvivesPauseAndCompletesOnceAfterResume()
		{
			string source = Read(Checks);
			Ordered(source, "KingdomRaids.TryAcceptRecovery(", "Actors[i].Die(Force: true)", "Rows.Dead(Actors[i])",
				"Witness.SetOption(false)", "Witness.BeginResume()", "Witness.Verify(2)", "ReadyTick > Witness.ResumeTick",
				"RetainWorld()", "KingdomRaids.TryResolveRecovery(", "Quest.Exact(true)", "!KingdomRaids.TryResolveRecovery(",
				"Same(SettledWire, Wire())", "OriginalGraves(); Witness.Verify(2)");
			foreach (string token in new[] { "KingdomRaids.OnWorldWake(", ".HandleEvent(", ".RemoveObject(", ".Obliterate(", ".FinishQuest(" }) StringAssert.DoesNotContain(token, source);
			Assert.IsFalse(Regex.IsMatch(source, @"\.(RecoveryState|Turns|TimeTicks|Energy)\s*=(?!=)"));
			StringAssert.Contains("ordinary-acceptance=false; save-load=untested; focused-7a-7c-only=true", source);
			StringAssert.Contains("if (Witness != null) Witness.Armed = false", source);
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
