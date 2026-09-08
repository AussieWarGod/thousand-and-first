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
	/// <c>XRLCore.PlayerTurn</c>, which is where the per-frame render path lives. A well-meant
	/// "spend the turn like advance does" edit would restore exactly the bug two native runs found
	/// - a 2400-turn advance that rendered nothing - and would still pass every other suite,
	/// because nothing else in the tree can see the omission. These contracts pin the omission, the
	/// two seams that put the script back in control, and the three places the verb's name and
	/// bound are restated.
	/// </para>
	/// <para>
	/// TWO OF THEM ARE NATIVE-CRASH PINS. A postfix on <c>BeforeRenderEvent.Send</c> made Harmony
	/// rewrite that method, and the rewrite read its own type's <c>static readonly Instance</c> as
	/// null: native runs died at <c>RunGame: NullReferenceException</c> in
	/// <c>BeforeRenderEvent.Send_Patch1</c> before the game loop started, for EVERY persona,
	/// because Qud <c>PatchAll</c>s a mod assembly at load. So the target is pinned away from that
	/// method, and the install is pinned lazy - a persona that never yields must run unpatched.
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
			StringAssert.Contains("postfix: new HarmonyMethod(seam)", observer);
			StringAssert.DoesNotContain("prefix:", observer);
			StringAssert.DoesNotContain("transpiler:", observer);
			StringAssert.DoesNotContain("__result", observer);
		}

		/// <summary>
		/// The frame is counted where a frame is actually DRAWN, and never on
		/// <c>BeforeRenderEvent.Send</c>: patching that method crashed the game natively, so the
		/// forbidden target is pinned in BOTH frame-yield files rather than in a comment.
		/// </summary>
		[Test]
		public void TheFrameSeamObservesRenderBaseToBufferAndNeverPatchesBeforeRenderEvent()
		{
			string observer = Read("Harness/KingdomScenarioFrameObserver.cs");
			StringAssert.Contains("internal const string Target = \"RenderBaseToBuffer\";", observer);
			StringAssert.Contains("AccessTools.Method(typeof(XRLCore), Target,", observer);
			StringAssert.Contains("new Type[] { typeof(ScreenBuffer) })", observer);
			foreach (string file in new[] { "Harness/KingdomScenarioFrameObserver.cs",
				"Harness/KingdomScenarioFrames.cs" })
			{
				StringAssert.DoesNotContain("typeof(BeforeRenderEvent)", Read(file));
				StringAssert.DoesNotContain("HarmonyPatch(", Read(file));
			}
		}

		/// <summary>
		/// The seam is installed by the FIRST yield and by nothing else. Qud calls
		/// <c>Harmony.PatchAll</c> on a mod assembly at load, so an attribute here would arm the
		/// patch for every persona in every game - which is how the crash above reached personas
		/// that never yield a frame. Attributes are therefore absent, the install runs from the
		/// verb, and the driver that arms the resume seam must not touch it.
		/// </summary>
		[Test]
		public void TheFrameSeamIsInstalledLazilyByTheVerbAndNeverAtModLoad()
		{
			string observer = Read("Harness/KingdomScenarioFrameObserver.cs");
			StringAssert.DoesNotContain("[Harmony", observer);
			StringAssert.Contains("private static bool Installed;", observer);
			StringAssert.Contains("if (Installed) return true;", observer);
			// Set only after Harmony returned, so a failed install is retried, not assumed done.
			ClassicAssert.Greater(observer.IndexOf("Installed = true;", StringComparison.Ordinal),
				observer.IndexOf("new Harmony(HarmonyId).Patch(", StringComparison.Ordinal),
				"the installed flag must follow the patch call, never precede it");
			string frames = Read("Harness/KingdomScenarioFrames.cs");
			ClassicAssert.AreEqual(1,
				Occurrences(frames, "KingdomScenarioFrameObserver.TryInstall(out failure)"),
				"the frame seam installs from exactly one place");
			int arm = frames.IndexOf("internal static void ArmDriver()", StringComparison.Ordinal);
			int run = frames.IndexOf("internal static string Run(", StringComparison.Ordinal);
			int install = frames.IndexOf("KingdomScenarioFrameObserver.TryInstall(",
				StringComparison.Ordinal);
			ClassicAssert.Greater(arm, -1, "the driver entry is missing");
			ClassicAssert.Greater(install, run, "the install must sit inside the verb");
			ClassicAssert.Greater(run, arm, "the verb must follow the driver entry");
		}

		/// <summary>
		/// The guards have to be REACHABLE. <c>PlayerTurn</c> parks its whole energy loop on
		/// <c>while (!GameManager.focused)</c> before it renders or fires the end-of-turn
		/// callbacks, so an unfocused window can reach no seam at all: the verb refuses up front
		/// rather than hanging, and the deadline that covers a merely slow loop is evaluated in the
		/// frame seam itself, which is the one place a drawn frame is guaranteed to pass through.
		/// </summary>
		[Test]
		public void TheYieldRefusesUnfocusedAndChecksItsDeadlineInsideTheFrameSeam()
		{
			string frames = Read("Harness/KingdomScenarioFrames.cs");
			StringAssert.Contains("if (!GameManager.focused)", frames);
			StringAssert.Contains(
				"internal const string CodeUnfocused = \"taf-frames-window-unfocused\";", frames);
			StringAssert.Contains(
				"internal const string CodeNoSeam = \"taf-frames-no-frame-seam\";", frames);
			int observe = frames.IndexOf("internal static void Observe(", StringComparison.Ordinal);
			int deadline = frames.IndexOf("Clock.Elapsed.TotalSeconds > DeadlineSeconds",
				observe, StringComparison.Ordinal);
			ClassicAssert.Greater(observe, -1, "the frame seam is missing");
			ClassicAssert.Greater(deadline, observe,
				"the wall-clock deadline must be evaluated inside the frame seam");
			// A seam that threw would end the engine's render loop for the rest of the session.
			StringAssert.Contains("catch (Exception error) { Fail(error); }", frames);
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
