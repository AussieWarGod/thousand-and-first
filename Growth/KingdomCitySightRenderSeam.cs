using System;
using ConsoleLib.Console;
using HarmonyLib;
using XRL.Core;
using XRL.World;
using XRL.World.ZoneParts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The seat the city-sight projection is taken from: a flag armed at the top of the drawn
	/// frame and spent, at most once, on the engine's own call to <c>Zone.Render</c> for that
	/// same zone.
	///
	/// WHY NOT THE RENDER DISPATCH ITSELF. The projection used to be a Harmony postfix on
	/// <c>BeforeRenderEvent</c>'s static dispatch entry. That crashed the game. Patching that one
	/// method makes Harmony re-host it as a dynamic method, and the re-hosted copy throws
	/// <c>NullReferenceException</c> out of itself &mdash; <c>RunGame:
	/// System.NullReferenceException ... at (wrapper dynamic-method)
	/// XRL.World.BeforeRenderEvent...Send_Patch1(XRL.World.Zone)</c>, with the native dump naming
	/// <c>List&lt;T&gt;.GetEnumerator()</c>, which is the walk over the second-pass handler list
	/// inside the method's own <c>try/finally</c>. Three of four unattended launches died on the
	/// first drawn frame; the fourth ran clean, so it is an initialisation race and not a
	/// reproducible branch. The postfix body could not have been the cause &mdash; it was two
	/// null-conditional calls and the fault was attributed to the re-hosted engine method, not to
	/// the patch &mdash; so the remedy is to stop asking for that method to be re-hosted at all.
	/// Evidence: <c>Tools/PortableOutput/player-claimed-light-native-check*.log</c> and
	/// <c>player-first-guest-native-check*.log</c> from the unattended observer runs.
	///
	/// WHAT THIS SEAT GUARANTEES INSTEAD. <c>XRLCore.RenderBaseToBuffer</c> already carries this
	/// mod's draw scope, has been re-hosted by Harmony across every one of those launches, and is
	/// well behaved; <c>Zone.Render</c> is a four-statement method with no exception handling of
	/// its own. The arming prefix records the zone the frame will draw and mutates no engine
	/// state. By the time the engine reaches <c>activeZone.Render(Buffer)</c>
	/// (D/XRL/Core/XRLCore.cs:2524) it has already run, in order: the map clears (:2505-2506),
	/// the whole <c>BeforeRenderEvent</c> dispatch (:2507) &mdash; pass 1 over every zone part and
	/// every object, and then the engine's own second pass, where <c>Blackout</c> REMOVES light
	/// (D/XRL/World/Parts/Blackout.cs:47-67) &mdash; the founder's own visibility reckoning
	/// (:2511-2512), and the wizard whole-map toggle (:2514-2518). The projection therefore runs
	/// after every native light and visibility contributor, which is the property it needs:
	/// <c>Zone.AddVisibility</c> opens a cell further off than a neighbour only where
	/// <c>GetLight(i, j) &gt; 1</c> (D/XRL/World/Zone.cs:5084-5100), so a snapshot taken before
	/// <c>Blackout</c> had spoken would call cells honestly visible that were about to be
	/// darkened, and the subtractive restore &mdash; which never closes a cell the snapshot held
	/// open &mdash; would leave them lit into the turn that follows.
	///
	/// The flag is what keeps this off the engine's other draws. <c>Zone.Render(ScreenBuffer)</c>
	/// is also reached from <c>ChavvahSystem</c>, <c>ThinWorld</c>, <c>EjectionSeat</c>,
	/// <c>ReshephsCrypt</c> and <c>XRLCore.RenderMapToBuffer</c>; none of them arms anything, so
	/// none of them projects. It is spent on the first matching draw and cleared there, and
	/// <see cref="KingdomCitySightDrawScope"/> clears it again on every exit from the frame, so a
	/// frame the engine abandons before <c>Render</c> (:2520-2522) cannot leave an armed flag
	/// behind for a later, unrelated draw of the same zone to spend.
	/// </summary>
	internal static class KingdomCitySightRenderSeam
	{
		/// <summary>The zone this frame is being drawn for, or null outside a drawn frame. One
		/// frame of presentation state and nothing else: never read by anything that persists,
		/// and cleared on every exit from the draw.</summary>
		private static Zone ArmedZone;

		/// <summary>Arm the frame. A null zone arms nothing, which is the correct reading of a
		/// frame with no active zone: the engine itself returns before drawing one.</summary>
		internal static void Arm(Zone Zone)
		{
			ArmedZone = Zone;
		}

		/// <summary>Disarm without projecting. The draw scope's guaranteed exit.</summary>
		internal static void Disarm()
		{
			ArmedZone = null;
		}

		/// <summary>Spend the flag on this zone if it is the one the frame was armed for. Single
		/// shot on purpose: a nested draw of the armed zone consumes the arming rather than
		/// projecting twice, and the frame simply goes unprojected.</summary>
		internal static bool SpendOn(Zone Zone)
		{
			if (Zone == null || !ReferenceEquals(Zone, ArmedZone)) return false;
			ArmedZone = null;
			return true;
		}
	}

	/// <summary>
	/// Arms the frame. A prefix, so it runs before the engine has touched anything: it reads the
	/// active zone off the very core whose frame this is, and writes one static reference. It
	/// mutates no engine state, returns <c>void</c> so it cannot skip the original, and takes no
	/// decision &mdash; every gate the projection stands on is asked later, at the draw, where the
	/// maps are finished.
	/// </summary>
	[HarmonyPatch(typeof(XRLCore), nameof(XRLCore.RenderBaseToBuffer))]
	internal static class KingdomCitySightFrameArming
	{
		private static void Prefix(XRLCore __instance)
		{
			KingdomCitySightRenderSeam.Arm(__instance?.Game?.ZoneManager?.ActiveZone);
		}
	}

	/// <summary>
	/// Takes the projection, immediately before the draw it is for. The one-argument overload is
	/// the one <c>RenderBaseToBuffer</c> calls (D/XRL/Core/XRLCore.cs:2524), and it is named
	/// explicitly so the five-argument sub-rectangle overload (D/XRL/World/Zone.cs:5715) is left
	/// alone. The prefix returns <c>void</c>: it can neither skip the draw nor rewrite what the
	/// draw returns, and a zone with no claimed-ground light does nothing at all here.
	/// </summary>
	[HarmonyPatch(typeof(Zone), nameof(Zone.Render), new Type[] { typeof(ScreenBuffer) })]
	internal static class KingdomCitySightDrawSeam
	{
		// Wrapped the way this mod wraps every other Harmony body (see
		// KingdomGuestFeastEnteredCellPatch): a presentation projection must not carry a fault out
		// of a prefix and into the engine's own frame. The arming has already been spent by the
		// time anything can throw, and KingdomCitySightDrawScope's finalizer closes whatever the
		// projection had opened, so the caught frame simply draws honestly.
		private static void Prefix(Zone __instance)
		{
			try
			{
				if (!KingdomCitySightRenderSeam.SpendOn(__instance)) return;
				__instance.GetPart<KingdomClaimedGroundLight>()?.ProjectCitySight();
			}
			catch (Exception error)
			{
				KingdomLog.Log("city sight: projection skipped (" + error.Message + ")");
			}
		}
	}
}
