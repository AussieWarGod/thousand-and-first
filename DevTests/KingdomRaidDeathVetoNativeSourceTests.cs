#if TAF_TESTS
using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source wiring only; actual veto/removal behavior belongs to the native persona.</summary>
	[TestFixture]
	public sealed class KingdomRaidDeathVetoNativeSourceTests
	{
		[Test]
		public void ProviderAdmitsOnlyFreshSealedVetoScriptBeforePublishingIntent()
		{
			string source = Read("Harness/KingdomRaidDeathVetoNativeProvider.cs");
			StringAssert.Contains("r_TAF_ScenarioRaidDeathVetoNative_v1", source);
			Ordered(Method(source, "public string RunScenarioVerb("), "if (!Eligible(game, zone, out string failure))",
				"game.SetStringGameState(Receipt, \"intent\")", "ProvesExactText(Receipt, \"intent\")",
				"KingdomRaidDeathVetoNativeChecks.Run(game, zone, out Ok)", "ReferenceEquals(The.Game, game)",
				"game.SetStringGameState(Receipt, report)", "ProvesExactText(Receipt, report)");
			string gate = Method(source, "private static bool Eligible(");
			foreach (string required in new[] { "HasQuickstartState(game)", "HasAnyState(game, Receipt)",
				"KingdomRaidLaunchNativeFixture.LastAttempt != null", "!r_TAF_RaidMintProbe.Vacant",
				"script.Count != 3", "script[0] != \"stagedigest\"", "script[1] != Verb",
				"script[2] != \"stagedigest\"", "KingdomScenarioTransactionShape.None" }) StringAssert.Contains(required, gate);
		}

		[Test]
		public void VetoUsesRealDestroyEventAndDeathObserversCannotSuppressProduction()
		{
			string provider = Read("Harness/KingdomRaidDeathVetoNativeProvider.cs");
			StringAssert.Contains("ID == BeforeDestroyObjectEvent.ID", provider);
			StringAssert.Contains("KingdomRaidDeathVetoNativeChecks.BeforeDestroy(this, E.Object)", provider);
			foreach (string name in new[] { "Prefix", "Postfix" })
			{
				string observer = Method(provider, "internal static void " + name + "(");
				StringAssert.Contains("KingdomRaidDeathVetoNativeChecks.Observe(", observer);
				foreach (string forbidden in new[] { "return false", "__result", "ref ", "throw ", "RaiderDying(" })
					StringAssert.DoesNotContain(forbidden, observer);
			}
			string checks = Read("Harness/KingdomRaidDeathVetoNativeChecks.cs");
			Ordered(Method(checks, "internal static bool BeforeDestroy("), "if (frame == null || !frame.Observing) return false;",
				"frame.BeforeDestroy(part, actor)", "catch (Exception error)", "frame.Latch(");
			string dispatch = Method(checks, "internal bool BeforeDestroy(");
			Ordered(dispatch, "ReferenceEquals(part, Veto)", "ReferenceEquals(actor, Final.Body)",
				"Entry[Attempt] && Exit[Attempt]", "Owner(); Final.Exact(Veto)", "Same(PendingWire, Wire())",
				"bool veto = part.Armed", "veto == (Attempt == 0)", "if (veto) Vetoes++", "return veto");
		}

		[Test]
		public void NativeSequenceProvesVetoedCustodyThenActualRemovalAndUniqueSettlement()
		{
			string checks = Read("Harness/KingdomRaidDeathVetoNativeChecks.cs");
			Ordered(Method(checks, "internal void Run()"), "PrepareRetention(Game, Manager, Zone, Evidence)",
				"new KingdomRaidDeathZoneEvidence(Zone)", "KillFirst(0); KillFirst(1)", "Actors[2].AddPart(Veto)",
				"Veto.Armed = true", "TryFinal(0); Final.Exact(Veto); NoGrave(); Pending()",
				"Fixture.Activate(); Final.Exact(Veto); NoGrave(); Pending()", "Veto.Armed = false",
				"TryFinal(1); Final.Identity(); Rows.Dead(Actors[2])", "Fixture.Activate(); Owner(); Physical()",
				"KingdomRaidResolution.RaidersDefeated", "Require(proofs == 1", "byte[] settled = Wire()",
				"Fixture.Activate()", "Same(settled, Wire())");
			Ordered(Method(checks, "private void TryFinal("), "Observing = true", "Final.Body.Die(Force: true)",
				"finally { Observing = false; }", "Rows.Record(Actors)", "Entry[attempt] && Exit[attempt] && Destroy[attempt]");
			foreach (string forbidden in new[] { "KingdomRaids.RaiderDying(", ".RemoveObject(", ".Destroy(",
				".Obliterate(", ".BindPass(", "ResumeOpen(" }) StringAssert.DoesNotContain(forbidden, checks);
			StringAssert.Contains("ordinary-acceptance=false; save-load=untested", checks);
			StringAssert.Contains("Manager.Graveyard.Objects", Method(checks, "private void NoGrave()"));
		}

		[Test]
		public void PersonaKeepsVetoRetryIsolatedAndHasExactTerminalExpectation()
		{
			string source = Read("Tools/personas/raid-death-veto-native-check.persona");
			Assert.AreEqual("founding-first-city", Setting(source, "REQUEST"));
			Assert.AreEqual("8.22@40,12", Setting(source, "START"));
			Assert.AreEqual("raid-death-veto-native-check", Setting(source, "VERBS"));
			Assert.AreEqual("stagedigest;raid-death-veto-native-check;stagedigest", Setting(source, "SCRIPT"));
			Assert.AreEqual("stagedigest:OK~founded=false,raid-death-veto-native-check:OK~cases=1 passed=1 failed=0,"
				+ "stagedigest:OK~founded=true,COMPLETE", Setting(source, "EXPECT"));
			StringAssert.Contains("save/load remain unsigned", source);
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		private static string Setting(string source, string key)
		{
			string[] rows = source.Split('\n').Select(row => row.Trim())
				.Where(row => row.StartsWith(key + "=", StringComparison.Ordinal)).ToArray();
			Assert.AreEqual(1, rows.Length); return rows[0].Substring(key.Length + 1);
		}
		private static string Method(string source, string signature)
		{
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, signature);
			int open = source.IndexOf('{', start), depth = 0;
			Assert.GreaterOrEqual(open, 0, signature);
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
				Assert.GreaterOrEqual(at, cursor, "Missing or reordered: " + token); cursor = at + token.Length;
			}
		}
	}
}
#endif
