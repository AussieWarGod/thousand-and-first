using HarmonyLib;
using XRL.World;
using XRL.World.ZoneParts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The seat city sight's projection is taken from: after the render dispatch has returned,
	/// never from inside it.
	///
	/// <c>BeforeRenderEvent.Send</c> runs pass 1 over every zone part and then every cell and the
	/// objects standing on them, and only then walks the <c>AfterHandlers</c> a pass-1 handler
	/// queued itself into, in the order they were queued
	/// (D/XRL/World/BeforeRenderEvent.cs:40-61). The one native contributor to that second pass is
	/// <c>Blackout</c>, and what it does there is REMOVE light
	/// (D/XRL/World/Parts/Blackout.cs:47-67). It hangs on an object, and objects are dispatched
	/// behind zone parts (D/XRL/World/Zone.cs:7632-7677), so a zone part that queued itself would
	/// always take its turn ahead of it. That is not a detail: <c>Zone.AddVisibility</c> reads the
	/// light map, opening a cell further off than a neighbour only where
	/// <c>GetLight(i, j) &gt; 1</c> (D/XRL/World/Zone.cs:5084-5100). A snapshot taken from inside
	/// the second pass would therefore call cells honestly visible that a Blackout was about to
	/// darken, and the restore &mdash; subtractive on purpose, it never closes a cell the snapshot
	/// held open &mdash; would leave them lit for the turn that follows.
	///
	/// A postfix comes back after the whole dispatch, so the maps read there are the ones every
	/// native contributor has finished writing. It is still ahead of the engine's own player
	/// reckoning (D/XRL/Core/XRLCore.cs:2511-2512), the wizard whole-map toggle (:2514-2518) and
	/// the abandoned-frame return (:2520-2522), which is exactly what the projection's own guards
	/// answer for. A postfix and not a finalizer: a dispatch that threw drew no frame, so there is
	/// nothing to project onto and nothing owing.
	/// </summary>
	[HarmonyPatch(typeof(BeforeRenderEvent), nameof(BeforeRenderEvent.Send))]
	internal static class KingdomCitySightRenderSeam
	{
		private static void Postfix(Zone Z)
		{
			Z?.GetPart<KingdomClaimedGroundLight>()?.ProjectCitySight();
		}
	}
}
