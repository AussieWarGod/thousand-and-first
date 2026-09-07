#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Wiring tripwires only. Native runner owns actual movement and turn-in evidence.</summary>
	[TestFixture]
	public sealed class KingdomRaidRecoveryReturnNativeSourceTests
	{
		[Test]
		public void ReturnFixtureEarnsReadyThenProvesLiveCustodyBeforeRefusalAssertion()
		{
			string source = Read("Harness/KingdomRaidRecoveryReturnNativeChecks.cs");
			Ordered(source, "Foreign = Manager.GetZone(ForeignId)", "LaunchAndContact()",
				"KingdomRaids.TryAcceptRecovery(", "KillFirst(0, active); KillFirst(1, active)",
				"Move(AwayCell, KingdomRaidRecoveryState.Active, active)", "Fixture.Activate()",
				"Recovery(KingdomRaidRecoveryState.Ready)", "Move(ReturnCell, KingdomRaidRecoveryState.Ready, ready)",
				"bool resolved = KingdomRaids.TryResolveRecovery(", "Survivor.Exact(ReturnCell)", "NoGrave()",
				"RefusedWire = Wire()", "RefusedQuest = Quest.Snapshot()", "returned-turn-in accepted=",
				"Require(!resolved && !string.IsNullOrEmpty(failure)", "Proof(KingdomLifecycleAction.RaidRecoveryResolve, 0)",
				"Actors[2].Die(Force: true)", "Rows.Dead(Actors[2]); Dead[2] = true",
				"post-removal turn-in refused", "Recovery(KingdomRaidRecoveryState.Resolved)", "Quest.Exact(true)");
			int returned = source.IndexOf("Move(ReturnCell,", StringComparison.Ordinal);
			int turnIn = source.IndexOf("bool resolved = KingdomRaids.TryResolveRecovery(", returned, StringComparison.Ordinal);
			string seam = source.Substring(returned, turnIn - returned);
			foreach (string token in new[] { "Fixture.Activate(", ".Take(", ".BindPass(", "OnWorldWake(" })
				StringAssert.DoesNotContain(token, seam);
		}

		[Test]
		public void FixtureDoesNotForceRecoveryOrTestSideBodyRemoval()
		{
			string source = Read("Harness/KingdomRaidRecoveryReturnNativeChecks.cs");
			foreach (string token in new[] { "KingdomRaids.RaiderDying(", ".RemoveObject(", ".Destroy(", ".Obliterate(",
				".BindPass(", "ResumeOpen(", ".FinishQuest(", ".FinishQuestStep(" }) StringAssert.DoesNotContain(token, source);
			ClassicAssert.IsFalse(Regex.IsMatch(source, @"\.RecoveryState\s*=(?!=)"));
			StringAssert.Contains("SystemMoveTo(destination, energyCost: 0, forced: false", source);
			StringAssert.Contains("ordinary-acceptance=false; save-load=untested", source);
			StringAssert.Contains("KingdomRaids.HasWatchDisarray(Fixture.System)", source);
		}

		[Test]
		public void FreshProviderAndPersonaSealExactReturnCase()
		{
			string source = Read("Harness/KingdomRaidRecoveryReturnNativeProvider.cs");
			Ordered(source, "if (!Eligible(game, zone, out string failure))", "game.SetStringGameState(Receipt, \"intent\")",
				"ProvesExactText(Receipt, \"intent\")", "KingdomRaidRecoveryReturnNativeChecks.Run(", "ReferenceEquals(The.Game, game)",
				"game.SetStringGameState(Receipt, report)", "ProvesExactText(Receipt, report)");
			foreach (string token in new[] { "r_TAF_ScenarioRaidRecoveryReturnNative_v1", "HasQuickstartState(game)",
				"HasAnyState(game, Receipt)", "KingdomRaidRecoveryDeathNativeProvider.Receipt", "!r_TAF_RaidMintProbe.Vacant",
				"script.Count != 3", "script[0] != \"stagedigest\"", "script[1] != Verb", "script[2] != \"stagedigest\"",
				"KingdomScenarioTransactionShape.None" }) StringAssert.Contains(token, source);
			string persona = Read("Tools/personas/raid-recovery-return-native-check.persona");
			foreach (string row in new[] { "REQUEST=founding-first-city", "START=8.22@40,12", "VERBS=raid-recovery-return-native-check",
				"SCRIPT=stagedigest;raid-recovery-return-native-check;stagedigest",
				"EXPECT=stagedigest:OK~founded=false,raid-recovery-return-native-check:OK~cases=1 passed=1 failed=0,stagedigest:OK~founded=true,COMPLETE" })
				ClassicAssert.AreEqual(1, Regex.Matches(persona, "(?m)^" + Regex.Escape(row) + "$" ).Count);
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
