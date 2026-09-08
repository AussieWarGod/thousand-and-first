#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Source contracts for the scenario harness's frame yield, the primitive that lets an
	/// unattended observer assert against a genuinely rendered frame.
	/// <para>
	/// The whole mechanism is a NEGATIVE: the verb must NOT spend the player's action opportunity,
	/// because unspent energy is the only thing that carries <c>ActionManager.RunSegment</c> into
	/// <c>XRLCore.PlayerTurn</c>, which is where the per-frame <c>BeforeRenderEvent</c> dispatch
	/// lives. A well-meant "spend the turn like advance does" edit would restore exactly the bug
	/// two native runs found - a 2400-turn advance that rendered nothing - and would still pass
	/// every other suite, because nothing else in the tree can see the omission. These contracts
	/// pin the omission, the two seams that put the script back in control, and the three places
	/// the verb's name and bound are restated.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioFrameYieldSourceTests
	{
		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		/// <summary>
		/// The verb arms and returns. Spending the opportunity here would keep the engine out of
		/// its own render loop, which is the precise reason `advance` cannot serve this purpose.
		/// </summary>
		[Test]
		public void TheFrameYieldNeverSpendsTheActionOpportunityItArmsOn()
		{
			string frames = Read("Harness/KingdomScenarioFrames.cs");
			StringAssert.DoesNotContain("Spend(", frames);
			int run = frames.IndexOf("internal static string Run(", StringComparison.Ordinal);
			int pump = frames.IndexOf("internal static bool Pump(", StringComparison.Ordinal);
			ClassicAssert.Greater(run, -1, "the verb entry is missing");
			ClassicAssert.Greater(pump, run, "the per-opportunity pump is missing");
			// The ONE call belongs to the resume seam, which runs inside PlayerTurn - never to the
			// verb or the pump, both of which run inside BeginTakeActionEvent.
			ClassicAssert.AreEqual(1, Occurrences(frames, "player.PassTurn();"),
				"the frame yield must pass exactly one turn, and only from its resume seam");
			int yield = frames.IndexOf("private static void Yield(", StringComparison.Ordinal);
			ClassicAssert.Greater(yield, pump, "the resume seam is missing");
			ClassicAssert.Greater(frames.IndexOf("player.PassTurn();", StringComparison.Ordinal),
				yield, "the only PassTurn must sit inside the resume seam");
		}

		/// <summary>Both seams are public engine extension points, and both are void observers.</summary>
		[Test]
		public void BothSeamsAreRegisteredEngineExtensionPointsAndReplaceNothing()
		{
			string frames = Read("Harness/KingdomScenarioFrames.cs");
			StringAssert.Contains("XRLCore.RegisterOnEndPlayerTurnCallback(Yield)", frames);
			// Registered once per process: the engine's callback list is static and never emptied,
			// so a second game in the same process must not stack a second resume seam.
			StringAssert.Contains("if (Hooked) return;", frames);
			string observer = Read("Harness/KingdomScenarioFrameObserver.cs");
			StringAssert.Contains("[HarmonyPostfix]", observer);
			StringAssert.DoesNotContain("[HarmonyPrefix]", observer);
			StringAssert.DoesNotContain("__result", observer);
			StringAssert.Contains("BeforeRenderEvent", observer);
		}

		/// <summary>
		/// The runner suspends on the yield exactly as it suspends on an advance, and stops the
		/// script when the yield is abandoned rather than asserting against frames nobody drew.
		/// </summary>
		[Test]
		public void TheAutoRunnerSuspendsAndResumesOnTheYield()
		{
			string runner = Read("Harness/KingdomScenarioAutoRunner.cs");
			StringAssert.Contains("KingdomScenarioFrames.ArmDriver();", runner);
			// Symmetric refusal: neither mechanism may run while the other is armed, because they
			// drive the player's energy in opposite directions.
			StringAssert.Contains("if (KingdomScenarioAdvance.Pending)",
				Read("Harness/KingdomScenarioFrames.cs"));
			StringAssert.Contains("if (KingdomScenarioFrames.Pending)",
				Read("Harness/KingdomScenarioAdvance.cs"));
			StringAssert.Contains("else if (KingdomScenarioFrames.Pending)", runner);
			StringAssert.Contains("if (KingdomScenarioFrames.Pump(out faulted)) return;", runner);
			StringAssert.Contains("Abandon(\"frame yield\")", runner);
			StringAssert.Contains("was abandoned", runner);
			StringAssert.Contains(
				"if (KingdomScenarioAdvance.Pending || KingdomScenarioFrames.Pending) return;",
				runner);
		}

		/// <summary>
		/// The verb's name and its frame bound are restated in the runtime, the profile tool, and
		/// the persona engine. A count one of them would seal and another refuse costs a whole
		/// non-retryable profile to discover.
		/// </summary>
		[Test]
		public void TheYieldVerbAndItsBoundAreIdenticalInAllThreePlaces()
		{
			string frames = Read("Harness/KingdomScenarioFrames.cs");
			string profile = Read("Tools/scenario_profile.py");
			string matrix = Read("Tools/personas/persona_matrix.py");
			StringAssert.Contains("internal const string Verb = \"yield-frames\";", frames);
			StringAssert.Contains("internal const int MaxFrames = 240;", frames);
			foreach (string tool in new[] { profile, matrix })
			{
				StringAssert.Contains("FRAMES_VERB = \"yield-frames\"", tool);
				StringAssert.Contains("MAX_YIELD_FRAMES = 240", tool);
				StringAssert.Contains("\"yield-frames\",", tool);
			}
			StringAssert.Contains("\"yield-frames\"", Read("Harness/KingdomScenarioVerbProvider.cs"));
			// The completion row is bookkeeping, so a persona describes the run and not the runner.
			StringAssert.Contains("internal const string CompleteRow = \"yield-frames-complete\";",
				frames);
			StringAssert.Contains("\"yield-frames-complete\",", matrix);
		}

		/// <summary>
		/// The claimed-light observer is the first caller, and it must wait on frames rather than
		/// on turns: its sealed script and its own precondition have to agree about that.
		/// </summary>
		[Test]
		public void TheClaimedLightObserverWaitsOnFramesRatherThanTurns()
		{
			string provider = Read("Harness/KingdomClaimedLightNativeProvider.cs");
			string checks = Read("Harness/KingdomClaimedLightNativeChecks.cs");
			string persona = Read("Tools/personas/claimed-light-native-check.persona");
			StringAssert.Contains("\"yield-frames 3\"", provider);
			StringAssert.DoesNotContain("\"advance 2400\"", provider);
			StringAssert.Contains("!KingdomScenarioFrames.Pending", checks);
			StringAssert.Contains("frames are still owed", checks);
			StringAssert.Contains("SCRIPT=stagedigest;claimed-light-setup;yield-frames 3;"
				+ "claimed-light-check;stagedigest", persona);
		}

		private static int Occurrences(string Text, string Needle)
		{
			int found = 0;
			int at = Text.IndexOf(Needle, StringComparison.Ordinal);
			while (at >= 0)
			{
				found++;
				at = Text.IndexOf(Needle, at + Needle.Length, StringComparison.Ordinal);
			}
			return found;
		}
	}
}
#endif
