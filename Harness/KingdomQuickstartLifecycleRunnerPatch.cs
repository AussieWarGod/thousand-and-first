using HarmonyLib;
using XRL;
using XRL.CharacterBuilds.Qud;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Adds the scenario auto-runner to a Quickstart-mode game, but ONLY for the separate
	/// quickstart-lifecycle boot request -- never for quickstart-boot, quickstart-save or
	/// quickstart-build, whose own KingdomQuickstartBootTest.RunnerAuthorized() still demands the
	/// runner's ABSENCE and is unchanged by this file.
	///
	/// <para>WHY HERE, NOT IN THE SHIPPED MODE DESCRIPTOR. RuntimeData/EmbarkModules.xml's shipped
	/// "KingdomQuickstart" mode declares no gamesystem beyond KingdomSuccession -- correctly: that
	/// file ships in every player's real Quickstart game, and the auto-runner is a Harness-only,
	/// never-shipped class (Tools/stage.sh excludes Harness/, and this file ships with it). The
	/// runner therefore cannot be added by declaring it in that mode's static gamesystem list
	/// without shipping dev-only code, and adding it unconditionally to the mode's list even in a
	/// dev overlay would break every existing quickstart-boot/-save/-build native check, whose own
	/// invariant demands the runner be absent (KingdomQuickstartBootTest.RunnerAuthorized).</para>
	///
	/// <para>WHY THIS SEAM. QudGamemodeModule.bootGame is the exact call that adds every mode's own
	/// declared gamesystems (game.AddSystem per selectedMode.gameSystems), and it runs at the start
	/// of world boot -- before any BOOTEVENT_* fires, the same "before world init, before the
	/// starting zone is generated" timing KingdomScenarioAutoRunner's own remarks require for its
	/// popup-suppression primer to matter (though a Quickstart boot suppresses its own popups
	/// separately, KingdomQuickstartBootTest.Begin). Postfixing it here adds the runner
	/// conditionally, in code, without touching the shipped descriptor or any production file.
	/// KingdomQuickstartBootTest.SelectMode (called from the embark builder, well before bootGame)
	/// has already parsed the request by the time this postfix runs, so LifecycleRequested reads
	/// correctly.</para>
	/// </summary>
	[HarmonyPatch(typeof(QudGamemodeModule), "bootGame")]
	internal static class KingdomQuickstartLifecycleRunnerPatch
	{
		[HarmonyPostfix]
		internal static void Postfix(XRLGame game)
		{
			if (game == null || !KingdomQuickstartRules.IsMode(game.gameMode)
				|| !KingdomQuickstartBootTest.LifecycleRequested) return;
			game.RequireSystem<KingdomScenarioAutoRunner>();
		}
	}
}
