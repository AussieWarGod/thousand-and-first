using System;
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
	/// never-shipped class. Adding it unconditionally to the mode's list even in a dev overlay
	/// would break every existing quickstart-boot/-save/-build native check, whose own invariant
	/// demands the runner be absent (KingdomQuickstartBootTest.RunnerAuthorized).</para>
	///
	/// <para>WHY THIS SEAM. bootGame adds every mode's declared gamesystems (game.AddSystem per
	/// selectedMode.gameSystems) and runs before any BOOTEVENT_* fires, so the runner exists (and
	/// its OnAdded/Prime has run) before the starting zone is generated.</para>
	///
	/// <para>DIAGNOSED FROM NATIVE RUN 13 (529a2aa): boot and the post-boot build both completed
	/// cleanly (all QUICKSTART-BOOT-*/QUICKSTART-BUILD-* rows OK), but the AutoRunner tail never
	/// armed -- no RUNNER-ARMED/SCRIPT-BEGIN row, no crash, a 1200s idle stall. The actual order is
	/// NOT "this postfix, then Begin": KingdomQuickstartBootTest.Begin is a HarmonyPrefix on
	/// EmbarkInfo.bootGame(XRLGame), the OUTER call whose body iterates every embark module and
	/// invokes EACH module's own bootGame(game, info) -- including QudGamemodeModule's, where this
	/// postfix lives. Begin therefore runs FIRST, claims Popup.Suppress for itself
	/// (OwnSuppression = true, since nothing held it yet), and only THEN does this postfix run and
	/// add the runner, whose own Prime() claims the SAME already-true flag a second time. When
	/// KingdomQuickstartBootTest.End's finally later drops Popup.Suppress because ITS OWN
	/// OwnSuppression is true, it does so with no knowledge the runner joined afterward -- exactly
	/// the stall: the next unattended popup after boot then blocks on a keypress that never comes.
	/// FIXED at the finally itself: End now checks KingdomScenarioAutoRunner.Suppressing(Game)
	/// before dropping the flag, so a second owner survives. The LIFECYCLE-RUNNER row below and the
	/// paired Player.log line remain, so a run can still show patched/lifecycleRequested/added
	/// directly rather than requiring this reasoning to be re-derived from a future stall.</para>
	/// </summary>
	[HarmonyPatch(typeof(QudGamemodeModule), "bootGame")]
	internal static class KingdomQuickstartLifecycleRunnerPatch
	{
		[HarmonyPostfix]
		internal static void Postfix(XRLGame game)
		{
			if (game == null || !KingdomQuickstartRules.IsMode(game.gameMode)
				|| !KingdomQuickstartBootTest.LifecycleRequested) return;
			bool added = game.GetSystem<KingdomScenarioAutoRunner>() != null;
			string failure = null;
			if (!added)
			{
				try
				{
					added = game.RequireSystem<KingdomScenarioAutoRunner>() != null;
				}
				catch (Exception error)
				{
					failure = error.GetType().Name + ": " + KingdomScenarioRules.Bounded(error.Message);
				}
			}
			// Never emitted for quickstart-boot/-save/-build (the check above already returned):
			// their journals must stay byte-identical to before this file existed, and
			// Tools/check-quickstart-results.py compares them by exact position.
			string line = "LIFECYCLE-RUNNER patched=true added=" + added + " lifecycleRequested=true"
				+ (failure == null ? "" : "; requireSystemThrew=" + failure);
			KingdomScenarioJournal.Append("LIFECYCLE-RUNNER", failure == null, line);
			MetricsManager.LogInfo("[TAF] " + line);
		}
	}
}
