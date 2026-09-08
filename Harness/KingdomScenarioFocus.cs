using System;
using System.Reflection;

using HarmonyLib;

using XRL.Core;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Asserts the engine's OWN focus flag for the duration of a frame yield, so a sealed scripted
	/// profile can draw real frames with its window deliberately unfocused.
	/// <para>
	/// THE PARK, AND WHY IT IS THE WHOLE PROBLEM. <c>XRLCore.PlayerTurn</c> opens its energy loop
	/// with <c>while (!GameManager.focused &amp;&amp; Game.Running) Thread.Sleep(200)</c>
	/// (<c>D/XRL/Core/XRLCore.cs:756</c>) - upstream of the end-of-turn callbacks
	/// (<c>D/XRL/Core/XRLCore.cs:2374</c>), of <c>RenderBase</c> (<c>:2387</c> and <c>:2392</c>) and
	/// of <c>Keyboard.IdleWait()</c> (<c>:2408</c>). <c>Tools/run-scenario.ps1</c> launches a
	/// scripted profile WITHOUT activation on purpose, so the operator keeps their foreground
	/// window; the consequence is that the parked loop draws no frame and fires no seam, and no
	/// render-dependent observer could ever be proven unattended.
	/// </para>
	/// <para>
	/// WHAT IS ACTUALLY OVERRIDDEN, AND WHAT IS NOT. <c>GameManager.focused</c>
	/// (<c>D/GameManager.cs:404</c>, over the field <c>_focused</c> at <c>:118</c>) is read by
	/// exactly ONE site in the whole engine - that park. Input is gated by a DIFFERENT flag,
	/// <c>XRLCore.bThreadFocus</c> (<c>D/XRL/Core/XRLCore.cs:223</c>), which
	/// <c>Keyboard.kbhit()</c> (<c>D/ConsoleLib/Console/Keyboard.cs:940</c>),
	/// <c>Keyboard.GetNextKey</c> (<c>:1015</c>) and the mouse paths in <c>GameManager</c>
	/// (<c>:1162</c>, <c>:1259</c>, <c>:1807</c>, <c>:2157</c>, <c>:3100</c>) all consult. This file
	/// NEVER writes <c>bThreadFocus</c>: an unfocused window keeps every input gate shut while the
	/// render loop runs, so the override cannot send or accept a keystroke or a click. The one
	/// side effect the setter has is in the safe direction - a false-to-true transition calls
	/// <c>Keyboard.ClearInput()</c> and <c>Keyboard.ClearMouseEvents()</c> and sets
	/// <c>mouseDisable</c> (<c>D/GameManager.cs:412-416</c>), which DISCARDS pending input.
	/// </para>
	/// <para>
	/// THE FIELD, NOT THE GETTER. Patching the property's own getter would be narrower still, but
	/// that getter is a one-line static field read and <c>PlayerTurn</c> is JITted long before any
	/// yield arms: an inlined read would never see the patch, and the override would fail
	/// SILENTLY. Writing the property instead lands in <c>_focused</c>, which an inlined getter
	/// reads too, so what is asserted is what the park observes either way.
	/// </para>
	/// <para>
	/// STAYING TRUE. The only writers of <c>focused</c> are Unity's two message handlers,
	/// <c>GameManager.OnApplicationFocus(bool)</c> (<c>D/GameManager.cs:1912</c>) and
	/// <c>GameManager.OnApplicationPause(bool)</c> (<c>:1918</c>). Both take a postfix that
	/// re-asserts the flag while a hold stands, so a focus event arriving mid-yield cannot re-park
	/// the loop; <c>OnApplicationFocus</c>'s own <c>bThreadFocus</c> write is left exactly as the
	/// engine made it. <see cref="Release" /> hands the flag back to that engine truth rather than
	/// to a guess.
	/// </para>
	/// <para>
	/// SCOPE. <see cref="TryHold" /> refuses unless <see cref="KingdomScenarioScript.Present" />,
	/// so an ordinary attended game is never touched, and the patch installs LAZILY on the first
	/// hold - this class carries no Harmony attribute, because Qud <c>PatchAll</c>s a mod assembly
	/// at load and an attributed patch would arm for every persona in every game. Nothing here
	/// patches <c>BeforeRenderEvent.Send</c>, which crashed the game natively; see
	/// <see cref="KingdomScenarioFrameObserver" /> for that account.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioFocus
	{
		/// <summary>Patch owner id, distinct from the frame seam's and from the loader's own.</summary>
		internal const string HarmonyId = "com.thousandandfirst.harness.scenario-focus";

		/// <summary>Unity's focus message: the engine's primary writer of the parked flag.</summary>
		internal const string FocusMessage = "OnApplicationFocus";

		/// <summary>Unity's pause message: the engine's second writer of the same flag.</summary>
		internal const string PauseMessage = "OnApplicationPause";

		private static bool Installed;
		private static bool Held;
		private static string Fault;

		/// <summary>True while a yield's hold stands. Session state, never durable.</summary>
		internal static bool Holding { get { return Held; } }

		/// <summary>
		/// Raises the hold for a sealed scripted profile: asserts the seal, installs the two
		/// postfixes on first use, writes the flag, and READS IT BACK. A hold that did not take is
		/// refused here rather than discovered as a hang, because the park is upstream of every
		/// seam a pending yield could otherwise be failed from.
		/// </summary>
		internal static bool TryHold(out string Refusal)
		{
			Refusal = null;
			if (!KingdomScenarioScript.Present())
			{
				Refusal = "this profile carries no sealed scenario script, and focus is asserted "
					+ "for sealed scripted profiles only";
				return false;
			}
			if (!TryInstall(out Refusal)) return false;
			Fault = null;
			Held = true;
			Reassert();
			if (Held && GameManager.focused) return true;
			Held = false;
			Refusal = "GameManager.focused did not read back true after the override wrote it"
				+ (Fault == null ? "" : ": " + Fault);
			return false;
		}

		/// <summary>
		/// Lowers the hold and hands the flag back to the engine's own last focus signal,
		/// <c>XRLCore.bThreadFocus</c> - a field this file never writes, so what is restored is
		/// what Unity actually reported. A true-to-false write takes the setter's other branch and
		/// clears no input.
		/// </summary>
		internal static void Release()
		{
			if (!Held) return;
			Held = false;
			GameManager.focused = XRLCore.bThreadFocus;
		}

		/// <summary>
		/// The postfix, on BOTH Unity focus messages and on nothing else. It re-asserts the parked
		/// flag and touches no other engine state. A throw out of a Unity message would be the
		/// engine's problem, not ours, so it lowers the hold instead: the pending yield then fails
		/// closed on its own deadline with a named code rather than repeating a throw per event.
		/// </summary>
		internal static void Reassert()
		{
			if (!Held || GameManager.focused) return;
			try
			{
				GameManager.focused = true;
			}
			catch (Exception error)
			{
				Held = false;
				Fault = KingdomScenarioRules.Bounded(
					error.GetType().Name + ": " + error.Message);
			}
		}

		/// <summary>
		/// Installs both postfixes on first use, and only then. The flag is set after Harmony
		/// returned, so a failed install is retried by the next hold rather than counted as done.
		/// </summary>
		private static bool TryInstall(out string Refusal)
		{
			Refusal = null;
			if (Installed) return true;
			try
			{
				Type[] signature = new Type[] { typeof(bool) };
				MethodInfo focus = AccessTools.Method(typeof(GameManager), FocusMessage, signature);
				MethodInfo pause = AccessTools.Method(typeof(GameManager), PauseMessage, signature);
				if (focus == null || pause == null)
				{
					Refusal = "this engine exposes no GameManager." + FocusMessage + "(bool) and "
						+ PauseMessage + "(bool) for the override to ride";
					return false;
				}
				MethodInfo seam = AccessTools.Method(typeof(KingdomScenarioFocus), "Reassert");
				if (seam == null)
				{
					Refusal = "the harness focus seam method is missing";
					return false;
				}
				Harmony harmony = new Harmony(HarmonyId);
				harmony.Patch(focus, postfix: new HarmonyMethod(seam));
				harmony.Patch(pause, postfix: new HarmonyMethod(seam));
				Installed = true;
				return true;
			}
			catch (Exception error)
			{
				Refusal = KingdomScenarioRules.Bounded(
					error.GetType().Name + ": " + error.Message);
				return false;
			}
		}
	}
}
