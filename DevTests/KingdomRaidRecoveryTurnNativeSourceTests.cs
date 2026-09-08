#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source wiring only; native execution proves the actual turn behavior.</summary>
	[TestFixture]
	public sealed class KingdomRaidRecoveryTurnNativeSourceTests
	{
		private const string Provider = "Harness/KingdomRaidRecoveryTurnNativeProvider.cs";
		private const string Checks = "Harness/KingdomRaidRecoveryTurnNativeChecks.cs";
		[Test]
		public void SealedScriptUsesExistingRealAdvanceBetweenSetupAndCheck()
		{
			string source = Read(Provider);
			foreach (string token in new[] { "script.Count == 5", "script[1] == SetupVerb", "script[2] == \"advance 1\"",
				"script[3] == CheckVerb", "KingdomScenarioAdvance.Pending", "HasAnyState(game, Receipt)",
				"HasQuickstartState(game)", "KingdomScenarioTransactionShape.None" }) StringAssert.Contains(token, source);
			string persona = Read("Tools/personas/raid-recovery-turn-native-check.persona");
			foreach (string row in new[] { "REQUEST=founding-first-city", "START=8.22@40,12",
				"SCRIPT=stagedigest;raid-recovery-turn-setup;advance 1;raid-recovery-turn-check;stagedigest",
				"VERBS=raid-recovery-turn-setup,raid-recovery-turn-check",
				"EXPECT=stagedigest:OK~founded=false,raid-recovery-turn-setup:OK~setup=Active originals-removed=3,advance:OK,raid-recovery-turn-check:OK~cases=1 passed=1 failed=0,stagedigest:OK~founded=true,COMPLETE" })
				ClassicAssert.AreEqual(1, Regex.Matches(persona, "(?m)^" + Regex.Escape(row) + "$" ).Count);
		}
		[Test]
		public void ActualDeathsRemainActiveUntilObservedEngineEvents()
		{
			string source = Read(Checks);
			Ordered(source, "KingdomRaids.TryAcceptRecovery(", "Actors[i].Die(Force: true)", "Rows.Dead(Actors[i])",
				"death finalized recovery before real turn", "new KingdomRaidRecoveryTurnWitness(", "Awaiting = true");
			foreach (string token in new[] { "KingdomRaids.OnWorldWake(", "KingdomHeartbeat.", ".HandleEvent(",
				"KingdomRaids.RaiderDying(", ".RemoveObject(", ".Obliterate(", ".FinishQuest(" }) StringAssert.DoesNotContain(token, source);
			ClassicAssert.IsFalse(Regex.IsMatch(source, @"\.(RecoveryState|Turns|TimeTicks|Energy|Speed)\s*=(?!=)"));
			StringAssert.Contains("ordinary-acceptance=false; save-load=untested", source);
		}
		[Test]
		public void ReadOnlyWitnessCountsContiguousDispatchesAndOneReadyProof()
		{
			string provider = Read(Provider), checks = Read(Checks);
			foreach (string token in new[] { "typeof(EndTurnEvent)", "\"OnWorldWake\"", "internal static void Prefix",
				"internal static void Postfix", "Dispatches < 32", "DispatchWakes < 8", "checked(LastDispatchTick + 1)",
				"checked(LastDispatchTurns + 1)", "if (Wakes == 1) ReadyTick = tick", "Dispatches == Ends" }) StringAssert.Contains(token, provider);
			StringAssert.DoesNotContain("static bool Prefix", provider);
			StringAssert.DoesNotContain("__result", provider);
			foreach (string token in new[] { "Turns + Witness.Dispatches", "Tick + Witness.Dispatches",
				"Witness.LastDispatchTurns + 1", "Witness.LastDispatchTick + 1",
				"ReadySequence, Witness.ReadyTick", "ReadySeen = true", "Proof(KingdomLifecycleAction.RaidRecoveryResolve, 0)" }) StringAssert.Contains(token, checks);
			StringAssert.DoesNotContain("Witness.Dispatches == 1", checks);
		}
		[Test]
		public void ExplicitCompletionRetainsPostTurnEffectsAndRejectsRepeat()
		{
			string source = Read(Checks);
			Ordered(source, "Witness.Verify()", "CheckTick = Game.TimeTicks", "CheckWater = Store.Liquid.Volume",
				"RetainWorld()", "KingdomRaids.TryResolveRecovery(Fixture.System, Zone, out string failure)",
				"Recovery(KingdomRaidRecoveryState.Resolved, CheckWater)", "Quest.Exact(true)",
				"!KingdomRaids.TryResolveRecovery(", "Same(SettledWire, Wire())", "OriginalGraves()");
			StringAssert.Contains("world-effects=retained", source);
			StringAssert.Contains("if (Witness != null) Witness.Armed = false", source);
			StringAssert.Contains("Game.TimeTicks == CheckTick", source);
			StringAssert.Contains("ReferenceEquals(Zone.Graveyard, OriginGraveyard)", source);
		}
		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		private static void Ordered(string source, params string[] tokens)
		{
			int cursor = 0;
			foreach (string token in tokens)
			{
				int at = source.IndexOf(token, cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(at, cursor, "Missing or reordered: " + token); cursor = at + token.Length;
			}
		}
	}
}
#endif
