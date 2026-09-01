using System;
using System.Collections.Generic;

using HarmonyLib;
using Qud.UI;
using XRL.CharacterBuilds;
using XRL.CharacterBuilds.Qud;
using XRL.UI;
using XRL.UI.Framework;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Dev-only main-menu entry: a <c>[Dev] Test Game</c> row above New Game that drops straight into
	/// the scenario embark chain, so a persona run is launch, one activation, read
	/// <c>scenario-journal.tsv</c>.
	/// <para>
	/// DEV PROFILE BY CONSTRUCTION. Nothing gates this on an option or an environment read: the file
	/// that declares it lives in <c>Harness/</c>, which <c>Tools/stage.sh</c> excludes and the shipped
	/// <c>manifest.json</c> does not select, so an ordinary build never compiles a line of it and the
	/// row cannot appear in a shipped game.
	/// </para>
	/// <para>
	/// WHY THE ROW REUSES <c>Pick:New Game</c>. <c>XRLCore</c>'s menu loop dispatches by comparing
	/// <c>Keyboard.CurrentMouseEvent.Event</c> against a fixed list of literal command strings; a row
	/// carrying a NEW command would be pushed and then matched by nothing, and the menu would simply
	/// redraw. So the row emits the command the engine already routes to <c>NewGame()</c>, and this
	/// class remembers - by object identity, never by matching the row's text - that the DEV row was
	/// the one activated. Ordinary New Game is left exactly as it was, mode carousel and all.
	/// </para>
	/// <para>
	/// WHY THE LIST IS RESTORED AROUND <c>Show</c>. <c>MainMenu.Show</c> contains a hard-coded
	/// <c>LeftOptions[1].Enabled = SavesAPI.HasSavedGameInfo()</c>, meaning the Continue row BY
	/// POSITION. Inserting ahead of it before that line runs would silently move that assignment onto
	/// New Game and grey it out. The row is therefore removed on the way in and re-inserted on the way
	/// out, after which the left scroller is handed the amended list exactly as <c>Show</c> hands it
	/// the original. The vanilla list shape is never observed changed by vanilla code.
	/// </para>
	/// <para>
	/// SCENARIO SELECTION IS OUTSIDE THE GAME BY DESIGN. <c>Tools/prepare-scenario.sh</c> resolves
	/// <c>TAF_REQUEST</c> against the authored roster, freezes its parameters and seed into this
	/// throwaway profile, and seals those bytes before launch. An in-game picker would choose after
	/// that authority was sealed and make the displayed request differ from the one the native gate
	/// proves. This row therefore starts the one sealed request; the launcher is the full roster
	/// picker for attended, automated, and persona runs alike.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioTestGameEntry
	{
		/// <summary>Row label. Bracketed and marked dev so it can never read as a shipped feature.</summary>
		internal const string RowText = "[Dev] Test Game";

		/// <summary>
		/// The command the engine already routes. See the class remarks: a new string would be
		/// matched by nothing in <c>XRLCore</c>'s menu dispatch.
		/// </summary>
		internal const string RowCommand = "Pick:New Game";

		/// <summary>
		/// The single row instance, held so activation can be recognised by REFERENCE. Matching on
		/// the displayed text would break the first time the label is reworded, and the label is the
		/// one part of this a human is expected to change.
		/// </summary>
		private static readonly MainMenuOptionData Row = new MainMenuOptionData
		{
			Text = RowText,
			Command = RowCommand,
			Enabled = true
		};

		/// <summary>
		/// Raised when the dev row is activated, consumed once by the embark builder. A one-shot, so
		/// a later ordinary New Game in the same session gets the ordinary mode carousel.
		/// </summary>
		private static bool Requested;

		/// <summary>Removes the row so vanilla code only ever sees the vanilla list shape.</summary>
		internal static void Detach()
		{
			List<MainMenuOptionData> left = MainMenu.LeftOptions;
			if (left != null) left.Remove(Row);
		}

		/// <summary>
		/// Re-inserts the row at the top and re-feeds the scroller, mirroring what
		/// <c>MainMenu.Show</c> itself does with the unamended list.
		/// </summary>
		internal static void Attach(MainMenu Menu)
		{
			List<MainMenuOptionData> left = MainMenu.LeftOptions;
			if (left == null || left.Count == 0 || left.Contains(Row)) return;
			left.Insert(0, Row);
			if (Menu == null || Menu.leftScroller == null) return;
			Menu.leftScroller.BeforeShow(null, left);
		}

		/// <summary>Notes an activation of the dev row, and only of the dev row.</summary>
		internal static void Note(FrameworkDataElement Data)
		{
			if (ReferenceEquals(Data, Row)) Requested = true;
		}

		private static bool AutostartConsumed;

		/// <summary>
		/// Starts the test game with NO input when the profile carries a sealed script: the
		/// operator asked for background-friendly runs, and a scripted profile has exactly one
		/// thing it exists to do. Fires at most once per process, drives the SAME code path a
		/// human click drives (the menu's own SelectedInfo with the dev row), and does nothing
		/// in an attended profile (no script file).
		/// </summary>
		internal static void Autostart(MainMenu Menu)
		{
			if (AutostartConsumed || Menu == null) return;
			if (!KingdomScenarioScript.Present()) return;
			AutostartConsumed = true;
			KingdomScenarioJournal.Append("AUTOSTART", true,
				"sealed script present; test game started without input");
			System.Reflection.MethodInfo selected =
				AccessTools.Method(typeof(MainMenu), "SelectedInfo");
			if (selected != null) selected.Invoke(Menu, new object[] { Row });
		}

		/// <summary>Consumes the latch. Returns true at most once per activation.</summary>
		internal static bool Take()
		{
			bool requested = Requested;
			Requested = false;
			return requested;
		}

		/// <summary>
		/// Selects the scenario mode as if the tile had been clicked.
		/// <para>
		/// Called once the builder has finished <c>InitModules</c>, which is the moment
		/// <c>EmbarkBuilder</c> has assembled every window descriptor and shown the first one - the
		/// exact state a human is looking at when they pick a mode. <c>QudGamemodeModule.SelectMode</c>
		/// then does what it does for any other mode: <c>setData</c>, which reaches
		/// <see cref="KingdomScenarioFastEmbarkModule.handleModuleDataChange" /> through
		/// <c>NotifyModuleChanges</c>, and then its own <c>builder.advance()</c>.
		/// </para>
		/// </summary>
		internal static void Select(EmbarkBuilder Builder)
		{
			if (Builder == null) return;
			QudGamemodeModule modes = Builder.GetModule<QudGamemodeModule>();
			if (modes == null || modes.GameModes == null
				|| !modes.GameModes.ContainsKey(KingdomScenarioFastEmbarkModule.ModeId)) return;
			modes.SelectMode(KingdomScenarioFastEmbarkModule.ModeId);
		}
	}

	/// <summary>
	/// Adds and removes the dev row around <c>MainMenu.Show</c>. See
	/// <see cref="KingdomScenarioTestGameEntry" /> for why the row is not simply left in the list.
	/// </summary>
	[HarmonyPatch(typeof(MainMenu), "Show")]
	internal static class KingdomScenarioTestGameMenuPatch
	{
		[HarmonyPrefix]
		internal static void Prefix()
		{
			KingdomSystem.Guard("scenario test-game menu detach", KingdomScenarioTestGameEntry.Detach);
		}

		[HarmonyPostfix]
		internal static void Postfix(MainMenu __instance)
		{
			MainMenu menu = __instance;
			KingdomSystem.Guard("scenario test-game menu attach",
				delegate { KingdomScenarioTestGameEntry.Attach(menu); });
			KingdomSystem.Guard("scenario test-game autostart",
				delegate { KingdomScenarioTestGameEntry.Autostart(menu); });
		}
	}

	/// <summary>
	/// Records that the dev row - and not the ordinary New Game row - was the one activated.
	/// <c>MainMenu.SelectedInfo</c> is an <c>async void</c> method, so a prefix runs on its
	/// synchronous entry, before the state machine that ends in <c>Keyboard.PushMouseEvent</c>.
	/// </summary>
	[HarmonyPatch(typeof(MainMenu), "SelectedInfo")]
	internal static class KingdomScenarioTestGameSelectPatch
	{
		[HarmonyPrefix]
		internal static void Prefix(FrameworkDataElement data)
		{
			FrameworkDataElement element = data;
			KingdomSystem.Guard("scenario test-game menu select",
				delegate { KingdomScenarioTestGameEntry.Note(element); });
		}
	}

	/// <summary>
	/// Preselects the scenario mode when the dev row asked for it. Patched on
	/// <c>EmbarkBuilder.InitModules</c> rather than on a window, because that method ends by showing
	/// the first window, so its postfix is the earliest point at which the builder is in exactly the
	/// state mode selection expects. The silent overload builds a character from a code and shows
	/// nothing, so it is left alone.
	/// </summary>
	[HarmonyPatch(typeof(EmbarkBuilder), "InitModules", new Type[] { typeof(bool) })]
	internal static class KingdomScenarioTestGameEmbarkPatch
	{
		[HarmonyPostfix]
		internal static void Postfix(EmbarkBuilder __instance, bool silent)
		{
			if (silent) return;
			EmbarkBuilder builder = __instance;
			KingdomSystem.Guard("scenario test-game embark entry", delegate
			{
				if (KingdomScenarioTestGameEntry.Take())
					KingdomScenarioTestGameEntry.Select(builder);
			});
		}
	}

	/// <summary>
	/// The embark overlay's per-frame Update can ask a window for DataErrors while the scripted
	/// fast-embark is mid-teardown of that exact window, and the engine method dereferences its
	/// half-retired module (crashed live 2026-08-30, seven clean launches then one NRE). Under a
	/// sealed script no human is reading the overlay, so the error text is inert; the finalizer
	/// swallows ONLY that NRE, only in a scripted dev profile, and reports no errors for the
	/// frame instead of killing the run. Attended profiles keep the engine's behavior whole.
	/// </summary>
	[HarmonyPatch(typeof(XRL.CharacterBuilds.AbstractBuilderModuleWindowBase), "DataErrors")]
	internal static class KingdomScenarioEmbarkOverlayRacePatch
	{
		[HarmonyFinalizer]
		internal static Exception Finalizer(Exception __exception, ref string __result)
		{
			if (__exception == null) return null;
			if (!(__exception is NullReferenceException)) return __exception;
			if (!KingdomScenarioScript.Present()) return __exception;
			__result = null;
			return null;
		}
	}

	/// <summary>
	/// Suppresses only vanilla <see cref="OpeningStory" />'s first modal in a sealed scenario.
	/// The scenario runner executes on <see cref="BeginTakeActionEvent" /> and restores its broad
	/// boot bracket when it writes the terminal row; OpeningStory runs later in that same action
	/// cycle on <see cref="BeforeTakeActionEvent" />. Without this narrow bracket, its otherwise
	/// correct arrival modal covers the native evidence frame after the script has finished.
	/// <para>
	/// The original handler still performs every non-UI effect and logs the suppressed message.
	/// The finalizer restores only a false value this patch changed, even when vanilla throws.
	/// Attended profiles and an already-suppressed caller are untouched.
	/// </para>
	/// </summary>
	[HarmonyPatch(typeof(OpeningStory), "HandleEvent",
		new Type[] { typeof(BeforeTakeActionEvent) })]
	internal static class KingdomScenarioOpeningStoryPatch
	{
		[HarmonyPrefix]
		internal static void Prefix(OpeningStory __instance, out bool __state)
		{
			__state = false;
			if (__instance == null || __instance.Triggered || Popup.Suppress
				|| !KingdomScenarioScript.Present()) return;
			Popup.Suppress = true;
			__state = true;
		}

		[HarmonyFinalizer]
		internal static Exception Finalizer(Exception __exception, bool __state)
		{
			if (__state) Popup.Suppress = false;
			return __exception;
		}
	}
}
