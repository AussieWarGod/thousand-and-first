using HarmonyLib;
using XRL.Core;
using XRL.World.ZoneParts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The draw scope city sight is allowed to exist inside. The projection opens the claimed zone
	/// for one drawn frame and the engine's after-render pass ordinarily closes it again, but that
	/// pass is not guaranteed: <c>RenderBaseToBuffer</c> has no <c>finally</c>, it calls
	/// <c>Zone.Render</c> and then walks the after-render callbacks in a bare loop
	/// (D/XRL/Core/XRLCore.cs:2524-2528), and both the render and any callback registered ahead of
	/// this mod's can throw. A frame that throws there would leave the zone open until the
	/// end-of-turn backstop &mdash; a whole turn of rest, autoexplore, targeting and hostile
	/// perception reading a map that shows the founder their whole city.
	///
	/// A Harmony finalizer is the <c>finally</c> the engine does not write. It runs on every exit
	/// from the drawn frame &mdash; ordinary return, the debug early returns at
	/// D/XRL/Core/XRLCore.cs:2520 and :2529, and a thrown render alike &mdash; so the projection
	/// can outlive at most the draw it was taken for. It returns <c>void</c>, so it neither
	/// swallows nor rewrites whatever the renderer was already throwing; the restore it calls is a
	/// no-op when nothing is outstanding, which is every frame this mod took no part in.
	/// </summary>
	[HarmonyPatch(typeof(XRLCore), nameof(XRLCore.RenderBaseToBuffer))]
	internal static class KingdomCitySightDrawScope
	{
		private static void Finalizer()
		{
			KingdomClaimedGroundLight.RestoreHonestVisibility();
		}
	}
}
