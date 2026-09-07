#if TAF_TESTS
using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source wiring only; lifecycle and physical claims require the native persona.</summary>
	[TestFixture]
	public sealed class KingdomRaidRecoveryDeathNativeSourceTests
	{
		[Test]
		public void ProviderRequiresFreshSealedRecoveryScriptBeforeIntent()
		{
			string source = Read("Harness/KingdomRaidRecoveryDeathNativeProvider.cs");
			StringAssert.Contains("r_TAF_ScenarioRaidRecoveryDeathNative_v1", source);
			Ordered(Method(source, "public string RunScenarioVerb("), "if (!Eligible(game, zone, out string failure))",
				"game.SetStringGameState(Receipt, \"intent\")", "ProvesExactText(Receipt, \"intent\")",
				"KingdomRaidRecoveryDeathNativeChecks.Run(game, zone, out Ok)", "ReferenceEquals(The.Game, game)",
				"game.SetStringGameState(Receipt, report)", "ProvesExactText(Receipt, report)");
			string gate = Method(source, "private static bool Eligible(");
			foreach (string token in new[] { "HasQuickstartState(game)", "HasAnyState(game, Receipt)",
				"KingdomRaidLaunchNativeFixture.LastAttempt != null", "!r_TAF_RaidMintProbe.Vacant",
				"script.Count != 3", "script[0] != \"stagedigest\"", "script[1] != Verb",
				"script[2] != \"stagedigest\"", "KingdomScenarioTransactionShape.None" }) StringAssert.Contains(token, gate);
		}

		[Test]
		public void RealDestroyVetoDoesNotDependOnExpectedRecoveryState()
		{
			string provider = Read("Harness/KingdomRaidRecoveryDeathNativeProvider.cs");
			StringAssert.Contains("ID == BeforeDestroyObjectEvent.ID", provider);
			foreach (string name in new[] { "Prefix", "Postfix" })
			{
				string observer = Method(provider, "internal static void " + name + "(");
				StringAssert.Contains("KingdomRaidRecoveryDeathNativeChecks.Observe(", observer);
				foreach (string token in new[] { "return false", "__result", "ref ", "throw ", "RaiderDying(" })
					StringAssert.DoesNotContain(token, observer);
			}
			string checks = Read("Harness/KingdomRaidRecoveryDeathNativeChecks.cs");
			string veto = Method(checks, "internal bool BeforeDestroy(");
			Ordered(veto, "ReferenceEquals(part, Veto)", "ReferenceEquals(actor, Actors[2])", "Entry[Attempt] && Exit[Attempt]",
				"DeathOwner()", "bool veto = part.Armed", "if (veto) Vetoes++", "try { Boundary(\"BeforeDestroy\"); }",
				"catch (Exception error)", "Latch(", "return veto");
			foreach (string token in new[] { "Pending()", "PendingWire", "RecoveryState.Active", "RecoveryState.Ready" })
			{
				StringAssert.DoesNotContain(token, veto);
				StringAssert.DoesNotContain(token, Method(checks, "private void DeathOwner()"));
			}
		}

		[Test]
		public void NativeSequenceEarnsRecoveryAndProvesSurvivorBeforeStateAssertion()
		{
			string checks = Read("Harness/KingdomRaidRecoveryDeathNativeChecks.cs");
			Ordered(Method(checks, "internal void Run()"), "LaunchAndContact()", "Recovery(KingdomRaidRecoveryState.Offered)",
				"KingdomRaids.TryAcceptRecovery(", "new KingdomRaidRecoveryQuestEvidence(", "KillFirst(0); KillFirst(1)",
				"Veto.Armed = true", "TryFinal(0); Bodies[2].Exact(Veto); NoGrave()", "Callbacks(0)", "Pending()",
				"Fixture.Activate(); Rows.Record(Actors); Pending(); NoGrave()", "Veto.Armed = false", "TryFinal(1)",
				"Rows.Dead(Actors[2]); Dead[2] = true", "Callbacks(1)", "Pending()", "Fixture.Activate()",
				"Recovery(KingdomRaidRecoveryState.Ready)", "KingdomRaids.TryResolveRecovery(",
				"Recovery(KingdomRaidRecoveryState.Resolved)", "Quest.Exact(true)", "!KingdomRaids.TryResolveRecovery(",
				"Same(settled, Wire())");
			StringAssert.Contains("Actors[2].Die(Force: true)", Method(checks, "private void TryFinal("));
			StringAssert.Contains("KingdomRaids.StepRaider(", Method(checks, "private void LaunchAndContact()"));
			foreach (string token in new[] { "KingdomRaids.RaiderDying(", ".RemoveObject(", ".Destroy(", ".Obliterate(",
				".BindPass(", "ResumeOpen(", ".FinishQuest(", ".FinishQuestStep(" }) StringAssert.DoesNotContain(token, checks);
			ClassicAssert.IsFalse(Regex.IsMatch(checks, @"\.RecoveryState\s*=(?!=)"), "Fixture must not assign recovery state.");
			StringAssert.Contains("ordinary-acceptance=false; save-load=untested", checks);
		}

		[Test]
		public void PersonaKeepsRecoveryCaseIsolatedWithExactExpectation()
		{
			string source = Read("Tools/personas/raid-recovery-death-native-check.persona");
			ClassicAssert.AreEqual("founding-first-city", Setting(source, "REQUEST"));
			ClassicAssert.AreEqual("8.22@40,12", Setting(source, "START"));
			ClassicAssert.AreEqual("raid-recovery-death-native-check", Setting(source, "VERBS"));
			ClassicAssert.AreEqual("stagedigest;raid-recovery-death-native-check;stagedigest", Setting(source, "SCRIPT"));
			ClassicAssert.AreEqual("stagedigest:OK~founded=false,raid-recovery-death-native-check:OK~cases=1 passed=1 failed=0,"
				+ "stagedigest:OK~founded=true,COMPLETE", Setting(source, "EXPECT"));
			StringAssert.Contains("save/load remain unsigned", source);
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		private static string Setting(string source, string key)
		{
			string[] rows = source.Split('\n').Select(row => row.Trim())
				.Where(row => row.StartsWith(key + "=", StringComparison.Ordinal)).ToArray();
			ClassicAssert.AreEqual(1, rows.Length); return rows[0].Substring(key.Length + 1);
		}
		private static string Method(string source, string signature)
		{
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, signature); int open = source.IndexOf('{', start), depth = 0;
			ClassicAssert.GreaterOrEqual(open, 0, signature);
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0) return Regex.Replace(source.Substring(start, i - start + 1), @"\s+", " ");
			}
			Assert.Fail("Unclosed method: " + signature); return null;
		}
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
