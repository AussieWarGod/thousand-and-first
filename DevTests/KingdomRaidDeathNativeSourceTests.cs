#if TAF_TESTS
using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Wiring tripwires only. Real death/removal assertions run in the native persona.</summary>
	[TestFixture]
	public sealed class KingdomRaidDeathNativeSourceTests
	{
		[Test]
		public void ProviderRequiresFreshExactScriptBeforeWritingIntentOrRunningDeaths()
		{
			string source = Read("Harness/KingdomRaidDeathNativeProvider.cs");
			StringAssert.Contains("[KingdomScenarioVerbProvider]", source);
			StringAssert.Contains("internal const string Receipt = \"r_TAF_ScenarioRaidDeathNative_v1\";", source);
			string run = Flat(Method(source, "public string RunScenarioVerb("));
			Ordered(run, "if (!Eligible(game, zone, out string failure))", "game.SetStringGameState(Receipt, \"intent\")",
				"ProvesExactText(Receipt, \"intent\")", "KingdomRaidDeathNativeChecks.Run(game, zone, out Ok)",
				"ReferenceEquals(The.Game, game)", "game.SetStringGameState(Receipt, report)", "ProvesExactText(Receipt, report)");
			string gate = Flat(Method(source, "private static bool Eligible("));
			foreach (string token in new[] { "HasQuickstartState(game)", "HasAnyState(game, Receipt)",
				"KingdomRaidLaunchNativeFixture.LastAttempt != null", "!r_TAF_RaidMintProbe.Vacant",
				"script.Count != 3", "script[0] != \"stagedigest\"", "script[1] != Verb",
				"script[2] != \"stagedigest\"", "KingdomScenarioTransactionShape.None" }) StringAssert.Contains(token, gate);
		}

		[Test]
		public void ObservationalPatchCannotSuppressOrReplaceTheRealDeathCallback()
		{
			string source = Read("Harness/KingdomRaidDeathNativeProvider.cs");
			StringAssert.Contains("[HarmonyPatch(typeof(KingdomRaids), \"RaiderDying\",", source);
			StringAssert.Contains("[HarmonyPrefix]", source); StringAssert.Contains("[HarmonyPostfix]", source);
			foreach (string name in new[] { "Prefix", "Postfix" })
			{
				string method = Flat(Method(source, "internal static void " + name + "("));
				StringAssert.Contains("KingdomRaidDeathNativeChecks.Observe(" + (name == "Prefix" ? "true" : "false") + ", actor, part)", method);
				foreach (string forbidden in new[] { "ref ", "__result", "return false", "throw ", "RaiderDying(" })
					StringAssert.DoesNotContain(forbidden, method);
			}
			string observe = Flat(Method(Read("Harness/KingdomRaidDeathNativeChecks.cs"), "internal static void Observe("));
			Ordered(observe, "if (frame == null || !frame.Observing) return;", "try { frame.Observe(entry, actor, part); }",
				"catch (Exception error)", "frame.Latch(");
		}

		[Test]
		public void PersonaSealsOneNativeDeathCaseInAnIsolatedFoundingScript()
		{
			string source = Read("Tools/personas/raid-death-native-check.persona");
			ClassicAssert.AreEqual("founding-first-city", Setting(source, "REQUEST"));
			ClassicAssert.AreEqual("8.22@40,12", Setting(source, "START"));
			ClassicAssert.AreEqual("raid-death-native-check", Setting(source, "VERBS"));
			ClassicAssert.AreEqual("stagedigest;raid-death-native-check;stagedigest", Setting(source, "SCRIPT"));
			ClassicAssert.AreEqual("stagedigest:OK~founded=false,raid-death-native-check:OK~cases=1 passed=1 failed=0,"
				+ "stagedigest:OK~founded=true,COMPLETE", Setting(source, "EXPECT"));
			StringAssert.Contains("save/load remain unsigned", source);
		}

		[Test]
		public void NativeChecksUseRealDeathWithoutTestSideRemovalOrDeathNotification()
		{
			string checks = Read("Harness/KingdomRaidDeathNativeChecks.cs");
			StringAssert.Contains(".Die(Force: true)", checks);
			foreach (string forbidden in new[] { "KingdomRaids.RaiderDying(", ".RemoveObject(",
				".Obliterate(", ".Destroy(", ".BindPass(" }) StringAssert.DoesNotContain(forbidden, checks);
			StringAssert.Contains("synthetic=true; ordinary-acceptance=false; save-load=untested", checks);
		}

		[Test]
		public void DeathCaseKeepsForeignAndLastTargetBoundariesSeparate()
		{
			string source = Read("Harness/KingdomRaidDeathNativeChecks.cs");
			string run = Flat(Method(source, "internal void Run()"));
			Ordered(run, "Foreign = Manager.GetZone(foreignId)", "KingdomRaidLaunchNativeFixture.TryCreate(Target",
				"Fixture.Activate()", "PendingWire = Wire()", "Actors[0].SystemMoveTo(away, energyCost: 0",
				"Kill(0, ForeignRows); Pending()", "foreign-death pending=true", "Kill(1, TargetRows); Pending()",
				"first-target-death pending=true", "Kill(2, TargetRows); Pending()", "resolution-awaits-production-wake",
				"Fixture.Activate(); Owner(); Physical()", "KingdomRaidResolution.RaidersDefeated",
				"Require(proofs == 1", "byte[] settled = Wire()", "Fixture.Activate()", "Same(settled, Wire())");
			string kill = Flat(Method(source, "private void Kill("));
			Ordered(kill, "Expected = index; Observing = true", "actor.Die(Force: true)", "finally { Observing = false; }",
				"rows.Record(Actors)", "rows.Dead(actor)", "Dead[index] = true", "ObserverFault == null",
				"Entries[index] != null", "Exits[index] != null");
			string removed = Flat(Method(Read("Harness/KingdomRaidDeathNativeProvider.cs"), "internal void Dead("));
			foreach (string token in new[] { "ReferenceEquals(row, body)", "!Present.ContainsKey(body)", "count == 1",
				"!GameObject.Validate(body)", "body.IsInGraveyard()", "body.Physics?._CurrentCell == null" })
				StringAssert.Contains(token, removed);
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		[Test]
		public void FixtureRetentionOnlyRaisesCapacityAndReprovesOriginalQueue()
		{
			string source = Flat(Method(Read("Harness/KingdomRaidDeathNativeProvider.cs"), "internal static void PrepareRetention("));
			Ordered(source, "ReferenceEquals(The.Game, game)", "manager.CachedZones.TryGetValue(zone.ZoneID",
				"int count = queue.Count, oldMax = graveyard.MaxCount", "count <= 65520", "oldMax <= 65536", "count <= oldMax",
				"rows[i] = queue[i]", "Math.Max(oldMax, checked(count + 16))", "graveyard.MaxCount == oldMax",
				"ReferenceEquals(queue[i], rows[i])", "graveyard.MaxCount = nextMax", "ReferenceEquals(graveyard.Objects, queue)",
				"queue.Count == count", "graveyard.MaxCount == nextMax", "ReferenceEquals(queue[i], rows[i])",
				"synthetic-graveyard-retention zone=");
			foreach (string forbidden in new[] { ".Clear(", ".Pool(", ".ReleaseObjects(", ".SetCapacity(", ".Dequeue(",
				"graveyard.Objects =" }) StringAssert.DoesNotContain(forbidden, source);
			string run = Flat(Method(Read("Harness/KingdomRaidDeathNativeChecks.cs"), "internal void Run()"));
			Ordered(run, "PrepareRetention(Game, Manager, Foreign, Evidence)", "new KingdomRaidDeathZoneEvidence(Foreign)",
				"PrepareRetention(Game, Manager, Target, Evidence)", "new KingdomRaidDeathZoneEvidence(Target)", "Kill(0, ForeignRows)");
		}

		private static string Flat(string source) { return Regex.Replace(source, @"\s+", " "); }
		private static string Setting(string source, string key)
		{
			string[] rows = source.Split('\n').Select(row => row.Trim())
				.Where(row => row.StartsWith(key + "=", StringComparison.Ordinal)).ToArray();
			ClassicAssert.AreEqual(1, rows.Length); return rows[0].Substring(key.Length + 1);
		}
		private static string Method(string source, string signature)
		{
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, signature);
			int open = source.IndexOf('{', start), depth = 0;
			ClassicAssert.GreaterOrEqual(open, 0, signature);
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0) return source.Substring(start, i - start + 1);
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
