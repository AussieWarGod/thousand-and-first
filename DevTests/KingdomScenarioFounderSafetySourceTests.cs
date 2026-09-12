#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Source contracts for the two harness-only answers to native run 36 (13122f0), where the
	/// founder was bitten to death mid-advance and the AutoRunner hung for ~50 minutes: death is
	/// terminal (a SCRIPT-STOPPED DIED row lands from the engine's own death seam) and the founder
	/// is not a combat target during the scripted advance (the engine's own IgnoreMe flag, scoped
	/// to the quickstart-lifecycle command, restored on every exit). These are text pins, not
	/// executed behaviour; the shards touch a live XRLGame. Every pin here is one where getting
	/// the seam, the order, or the scope wrong is the defect.
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioFounderSafetySourceTests
	{
		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		private static void AssertOrder(string source, params string[] terms)
		{
			int cursor = -1;
			for (int i = 0; i < terms.Length; i++)
			{
				int next = source.IndexOf(terms[i], cursor + 1, StringComparison.Ordinal);
				ClassicAssert.Greater(next, cursor, terms[i]);
				cursor = next;
			}
		}

		/// <summary>The death comes to the runner through the player registrar, the seam the
		/// production seal already observes, not through a pump that can never run again once
		/// Game.Running is lowered.</summary>
		[Test]
		public void TheRunnerObservesThePlayersDeathThroughThePlayerRegistrar()
		{
			string runner = Read("Harness/KingdomScenarioAutoRunner.cs");
			AssertOrder(runner, "public override void RegisterPlayer(",
				"Registrar.Register(BeginTakeActionEvent.ID);",
				"Registrar.Register(AfterDieEvent.ID);",
				"RegisteredPlayer = Player;");
			StringAssert.Contains("Registrar.Register(AfterDieEvent.ID);", Read("Core/KingdomSeal.cs"));
			string death = Read("Harness/KingdomScenarioAutoRunner.Death.cs");
			StringAssert.Contains("public override bool HandleEvent(AfterDieEvent E)", death);
			// Succession re-registers the runner on the heir inside AfterDieEvent, so the death of
			// any body ever registered counts, decided by the engine-free predicate.
			AssertOrder(runner, "RegisteredPlayer = Player;", "RegisteredBodies.Add(Player);");
			StringAssert.DoesNotContain("RegisteredBodies.Remove(", runner + death);
			StringAssert.DoesNotContain("RegisteredBodies.Clear(", runner + death);
			StringAssert.Contains("private readonly HashSet<GameObject> RegisteredBodies = new HashSet<GameObject>();", death);
			AssertOrder(death, "KingdomScenarioDeathRules.ShouldRecordDeath(dying.IsPlayer(),",
				"ReferenceEquals(dying, RegisteredPlayer) || RegisteredBodies.Contains(dying),",
				"Verbs != null))");
		}

		/// <summary>The decision by value. The succession shape is the load-bearing case: the
		/// founder is no longer the player and no longer the latest registered body, yet was
		/// registered, so the death is recorded; an unrelated NPC death never is.</summary>
		[Test]
		public void ShouldRecordDeathByValueIncludingTheSuccessionShape()
		{
			ClassicAssert.IsTrue(Harness.KingdomScenarioDeathRules.ShouldRecordDeath(false, true, true),
				"succession: founder re-bodied before the handler ran");
			ClassicAssert.IsTrue(Harness.KingdomScenarioDeathRules.ShouldRecordDeath(true, true, true));
			ClassicAssert.IsTrue(Harness.KingdomScenarioDeathRules.ShouldRecordDeath(true, false, true),
				"player body never seen by RegisterPlayer still counts");
			ClassicAssert.IsFalse(Harness.KingdomScenarioDeathRules.ShouldRecordDeath(false, false, true),
				"an NPC death under a running script is not a stop");
			ClassicAssert.IsFalse(Harness.KingdomScenarioDeathRules.ShouldRecordDeath(true, true, false),
				"no script running: an attended game journals nothing");
			ClassicAssert.IsFalse(Harness.KingdomScenarioDeathRules.ShouldRecordDeath(false, true, false));
			ClassicAssert.IsFalse(Harness.KingdomScenarioDeathRules.ShouldRecordDeath(false, false, false));
		}

		/// <summary>One terminal row, DIED-prefixed with the engine's own category, after the
		/// pending advance (and with it the guard) is discarded, closed through the same Finish
		/// every refusal uses so the popup bracket is released.</summary>
		[Test]
		public void APlayerDeathLandsOneDiedPrefixedTerminalRowAndClosesTheRun()
		{
			string death = Read("Harness/KingdomScenarioAutoRunner.Death.cs");
			StringAssert.Contains("internal const string DiedPrefix = \"DIED \";", death);
			StringAssert.Contains("Dying.Physics?.LastDeathCategory", death);
			AssertOrder(death, "private void Died(", "KingdomScenarioAdvance.Cancel();",
				"Finish(StoppedRow, false, KingdomScenarioRules.Bounded(DiedPrefix + category");
			StringAssert.Contains("\"armed-at-death\" : \"unarmed-at-death\"", death);
			// What the engine exposes on the death event (decompile IDeathEvent.cs:6-18) is named.
			foreach (string field in new[] { "Name(E.Killer)", "E.KillerText", "Name(E.Weapon)",
				"Name(E.Projectile)", "E.Accidental", "IDeathEvent.cs:6-18" })
				StringAssert.Contains(field, death);
			// The seal reads the same category field, so the row and the seal never disagree.
			StringAssert.Contains("Physics?.LastDeathCategory", Read("Core/KingdomSeal.Utilities.cs"));
		}

		/// <summary>The checker names the death as its own FAIL class, distinct from every stall
		/// class and from a chain refusal, outranking the blocker the unreached links would read.
		/// The executable half is Tools/tests/quickstart_lifecycle_checker_test.py FounderDeath.</summary>
		[Test]
		public void TheCheckerClassifiesTheDeathAsFounderDied()
		{
			string checker = Read("Tools/check-quickstart-lifecycle.py");
			StringAssert.Contains("STOPPED_ROW = \"SCRIPT-STOPPED\"", checker);
			StringAssert.Contains("DIED_PREFIX = \"DIED \"", checker);
			StringAssert.Contains("FOUNDER_DIED = \"founder-died\"", checker);
			StringAssert.Contains("def founder_death(", checker);
			AssertOrder(checker, "def judge(", "death = founder_death(rows)",
				"verdict, reason, fail_class = FAIL, FOUNDER_DIED + \": \" + death, FOUNDER_DIED",
				"\"failClass\": fail_class");
			StringAssert.Contains("class FounderDeath(unittest.TestCase):",
				Read("Tools/tests/quickstart_lifecycle_checker_test.py"));
			StringAssert.Contains("\"advance-guard\",", Read("Tools/personas/persona_matrix.py"));
		}

		/// <summary>The guard is the engine's own IgnoreMe, armed by every scripted advance, armed
		/// before the wait's first spend and restored (never assumed false) on every route out. It
		/// is not god mode: no invulnerability flag, no world freeze, no stat write.</summary>
		[Test]
		public void TheFounderGuardIsIgnoreMeOnEveryScriptedAdvanceAndRestoredOnEveryExit()
		{
			string guard = Read("Harness/KingdomScenarioFounderGuard.cs");
			StringAssert.Contains("internal const string Row = \"advance-guard\";", guard);
			// Every scripted advance, every road (run 39 investigation); if (!Held) before
			// Previous: a double arm must not overwrite Previous with true.
			StringAssert.DoesNotContain("LifecycleRequested", guard);
			StringAssert.Contains("scope=every-scripted-advance", guard);
			AssertOrder(guard, "internal static string Arm(", "if (!Held)",
				"Previous = The.Core.IgnoreMe;", "The.Core.IgnoreMe = true;", "Held = true;");
			AssertOrder(guard, "internal static string Release(", "The.Core.IgnoreMe = Previous;",
				"Held = false;");
			StringAssert.Contains("XRL/Core/XRLCore.cs:217", guard);
			StringAssert.Contains("XRL/World/Parts/Brain.cs:1075-1077", guard);
			StringAssert.Contains("XRL/World/GameObject.cs:11431-11433", guard);
			StringAssert.Contains("walk=none", guard);
			foreach (string forbidden in new[] { "IDKFA", "The.Core.Calm", "Invulnerable",
				"Stat(\"Hitpoints\"", "SetIntProperty", "SetStringProperty", "AddPart", "RemoveObject" })
				StringAssert.DoesNotContain(forbidden, guard);
			string advance = Read("Harness/KingdomScenarioAdvance.cs");
			// The start row is gated on Armed exactly like the end row: a non-lifecycle journal is
			// byte-identical to before the guard (Tools/upgrade_profile_witnesses.py exact sequence).
			AssertOrder(advance, "internal static string Run(", "LastTurn = game.Turns;",
				"string guard = KingdomScenarioFounderGuard.Arm(player);",
				"if (KingdomScenarioFounderGuard.Armed)",
				"KingdomScenarioJournal.Append(KingdomScenarioFounderGuard.Row, true, \"start; \" + guard);",
				"GuardedSpend(player);");
			StringAssert.DoesNotContain("\"start; \" + KingdomScenarioFounderGuard.Arm(player)", advance);
			string guarded = Read("Harness/KingdomScenarioAdvance.Guard.cs");
			AssertOrder(guarded, "internal static bool Pump(out bool Faulted)", "try", "PumpCore(out Faulted)",
				"settled = true;", "finally", "if (!settled) KingdomScenarioFounderGuard.Release(The.Player);");
			AssertOrder(guarded, "private static void GuardedSpend(", "Spend(Player);", "finally",
				"if (!settled) KingdomScenarioFounderGuard.Release(Player);");
			AssertOrder(guarded, "private static void EndGuard(", "if (!KingdomScenarioFounderGuard.Armed) return;",
				"\"end; \" + KingdomScenarioFounderGuard.Release(Player)");
			AssertOrder(Read("Harness/KingdomScenarioAutoRunner.cs"), "private void Finish(",
				"KingdomScenarioTravelDriver.Stop();", "KingdomScenarioAdvance.Cancel();",
				"KingdomScenarioJournal.Append(Row, Ok, Message);");
			AssertOrder(advance, "if (Remaining <= 0)", "EndGuard(player);",
				"KingdomScenarioJournal.Append(CompleteRow");
			AssertOrder(advance, "private static void Stop(", "EndGuard(The.Player);",
				"KingdomScenarioJournal.Append(Verb, false, Refuse(Code, Detail));", "Cancel();");
			AssertOrder(advance, "internal static void Cancel()",
				"KingdomScenarioFounderGuard.Release(The.Player);");
		}

		/// <summary>The persona that owns the road discloses both changes and the walk limit.</summary>
		[Test]
		public void TheLifecyclePersonaDisclosesTheGuardAndItsLimit()
		{
			string persona = Read("Tools/personas/lifecycle-stockpile-native-check.persona");
			StringAssert.Contains("NATIVE RUN 36 (13122f0)", persona);
			StringAssert.Contains("Harness/KingdomScenarioAutoRunner.Death.cs", persona);
			StringAssert.Contains("Harness/KingdomScenarioFounderGuard.cs", persona);
			StringAssert.Contains("XRLCore.IgnoreMe", persona);
			StringAssert.Contains("founder-died", persona);
			StringAssert.Contains("walk=none", persona);
			StringAssert.Contains("NOT invulnerability", persona);
		}
	}
}
#endif
