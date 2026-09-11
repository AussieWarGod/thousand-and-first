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
			StringAssert.Contains("dying.IsPlayer() || ReferenceEquals(dying, RegisteredPlayer)", death);
			// Only a scripted run stops here; an attended game with no script journals nothing.
			StringAssert.Contains("dying != null && Verbs != null", death);
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

		/// <summary>The guard is the engine's own IgnoreMe, scoped to the lifecycle command, armed
		/// before the wait's first spend and restored (never assumed false) on every route out. It
		/// is not god mode: no invulnerability flag, no world freeze, no stat write.</summary>
		[Test]
		public void TheFounderGuardIsIgnoreMeScopedToTheLifecycleAndRestoredOnEveryExit()
		{
			string guard = Read("Harness/KingdomScenarioFounderGuard.cs");
			StringAssert.Contains("internal const string Row = \"advance-guard\";", guard);
			AssertOrder(guard, "internal static string Arm(",
				"if (!KingdomQuickstartBootTest.LifecycleRequested)",
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
			AssertOrder(advance, "internal static string Run(", "LastTurn = game.Turns;",
				"KingdomScenarioFounderGuard.Arm(player)", "Spend(player);");
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
