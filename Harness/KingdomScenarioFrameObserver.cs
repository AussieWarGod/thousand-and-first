using System;
using System.Reflection;

using ConsoleLib.Console;
using HarmonyLib;
using XRL;
using XRL.Core;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Void witness of one really drawn frame, for <see cref="KingdomScenarioFrames" />, and the
	/// LAZY installer that puts it there.
	/// <para>
	/// WHY NOT <c>BeforeRenderEvent.Send</c>, learned live and expensively. A postfix there makes
	/// Harmony rewrite <c>Send</c> as a dynamic method, and the rewrite crashed the game: native
	/// runs died with <c>RunGame: NullReferenceException</c> inside
	/// <c>BeforeRenderEvent.Send_Patch1</c>, called from <c>XRLCore.RenderBaseToBuffer</c> - the
	/// FIRST draw <c>XRLCore.RunGame</c> performs, so the game never reached its loop at all.
	/// <c>Send</c> reads its own type's <c>static readonly Instance</c> and then enumerates
	/// <c>instance.AfterHandlers</c>; from a dynamic method that static read no longer carries the
	/// guarantee that the declaring type's initializer already ran, so <c>Instance</c> comes back
	/// null and the enumerator throws. Forcing the initializer before patching was tried and did
	/// not help. Nothing on this side can make an engine type initializer reliable, so this file
	/// does not patch that method, and no file in the harness may.
	/// </para>
	/// <para>
	/// WHAT IS OBSERVED INSTEAD: <c>XRLCore.RenderBaseToBuffer(ScreenBuffer)</c>
	/// (<c>D/XRL/Core/XRLCore.cs:2455</c>), whose body IS the frame - it takes the active zone,
	/// sends the render event to it, and calls <c>Zone.Render(Buffer)</c>. A postfix therefore runs
	/// once per real drawn frame, AFTER the zone rendered, and observes rather than replaces: no
	/// argument, result, event or callback changes. A draw the engine threw out of never reaches
	/// the count, because a postfix does not run when the original throws.
	/// </para>
	/// <para>
	/// AND IT IS INSTALLED LAZILY, WHICH IS HALF THE FIX. Qud patches a mod assembly with
	/// <c>Harmony.PatchAll</c> at load (<c>XRL.ModInfo</c>), so an ATTRIBUTED patch class is armed
	/// for every persona in every game whether or not it is ever wanted - which is exactly how the
	/// <c>Send</c> fault reached personas that never yield a frame. This class therefore carries no
	/// Harmony attribute of any kind: <see cref="TryInstall" /> runs from
	/// <c>KingdomScenarioFrames.Run</c> on the first <c>yield-frames</c> of the process and on no
	/// other path, so a run that never yields is byte-for-byte an unpatched run.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioFrameObserver
	{
		/// <summary>Patch owner id, distinct from the one the mod loader creates for this assembly,
		/// so what this file installed is exactly what this file installed.</summary>
		internal const string HarmonyId = "com.thousandandfirst.harness.frame-yield";

		/// <summary>The observed engine method: one real drawn frame per call.</summary>
		internal const string Target = "RenderBaseToBuffer";

		private static bool Installed;

		/// <summary>
		/// Installs the drawn-frame seam on first use, and only then. The flag is set only after
		/// Harmony returned, so a failed install is retried by the next yield rather than counted
		/// as present; the caller refuses the verb on a failure, because a yield nothing counts is
		/// a yield that would hang.
		/// </summary>
		internal static bool TryInstall(out string Failure)
		{
			Failure = null;
			if (Installed) return true;
			try
			{
				MethodInfo original = AccessTools.Method(typeof(XRLCore), Target,
					new Type[] { typeof(ScreenBuffer) });
				if (original == null)
				{
					Failure = "this engine exposes no XRLCore." + Target + "(ScreenBuffer)";
					return false;
				}
				MethodInfo seam = AccessTools.Method(typeof(KingdomScenarioFrameObserver), "Drawn");
				if (seam == null)
				{
					Failure = "the harness frame seam method is missing";
					return false;
				}
				new Harmony(HarmonyId).Patch(original, postfix: new HarmonyMethod(seam));
				Installed = true;
				return true;
			}
			catch (Exception error)
			{
				Failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
				return false;
			}
		}

		/// <summary>
		/// One completed frame. The zone is read back exactly as the method read it - nothing
		/// between the two switches the active zone - and a draw the engine suppressed
		/// (<c>GameManager.bDraw</c> is non-zero only for the debug short-circuits) is reported as
		/// NO zone, so it cannot be counted while the yield's own deadline is still evaluated.
		/// </summary>
		internal static void Drawn()
		{
			KingdomScenarioFrames.Observe(GameManager.bDraw == 0 ? The.ActiveZone : null);
		}
	}
}
