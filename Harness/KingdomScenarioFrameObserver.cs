using System;
using System.Runtime.CompilerServices;

using HarmonyLib;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Void witness of one completed engine frame dispatch, for
	/// <see cref="KingdomScenarioFrames" />. No argument, result, event or callback is replaced:
	/// the postfix observes only that <c>BeforeRenderEvent.Send</c> ran, and for which zone. It is
	/// inert unless a sealed script has armed a yield.
	/// </summary>
	[HarmonyPatch(typeof(BeforeRenderEvent), "Send", new Type[] { typeof(Zone) })]
	internal static class KingdomScenarioFrameObserver
	{
		/// <summary>
		/// Runs <c>BeforeRenderEvent</c>'s type initializer BEFORE its method is replaced, learned
		/// live from a boot that never reached the first player turn.
		/// <para>
		/// WHY. <c>Send</c> reads its OWN type's <c>static readonly Instance</c>
		/// (<c>D/XRL/World/BeforeRenderEvent.cs</c>). While that method lives inside
		/// <c>BeforeRenderEvent</c> the runtime guarantees the class is initialized before any of
		/// its methods run, so the compiler emits no initializer check. Harmony re-hosts the body
		/// in a dynamic method OUTSIDE the type, where that guarantee does not hold and the
		/// missing check is not re-added - and nothing in a new game touches
		/// <c>BeforeRenderEvent.ID</c> or <c>.Instance</c> before the first render, because every
		/// engine reference to them sits in a <c>WantEvent</c> that only runs INSIDE a dispatch.
		/// Unpatched the first <c>Send</c> initialized the class by calling it; patched, it read a
		/// null <c>Instance</c> and threw <c>NullReferenceException</c> out of
		/// <c>XRLCore.RunGame</c> on the first frame, before any script verb could run.
		/// </para>
		/// <para>
		/// WHY THIS IS SAFE THIS EARLY. The engine already initializes every <c>MinEvent</c> this
		/// exact way (<c>MinEvent.InitializeEvents</c> / <c>ResetEvents</c>,
		/// <c>D/XRL/World/MinEvent.cs:101,135</c>), and event IDs are FNV1A32 hashes of the type's
		/// full name (<c>D/XRL/World/MinEvent.cs:158</c>), never a registration counter, so
		/// registering earlier cannot move any ID.
		/// </para>
		/// </summary>
		[HarmonyPrepare]
		internal static bool Prepare()
		{
			RuntimeHelpers.RunClassConstructor(typeof(BeforeRenderEvent).TypeHandle);
			return true;
		}

		[HarmonyPostfix]
		internal static void Postfix(Zone Z)
		{
			KingdomScenarioFrames.Observe(Z);
		}
	}
}
