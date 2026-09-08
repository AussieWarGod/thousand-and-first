using System;

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
		[HarmonyPostfix]
		internal static void Postfix(Zone Z)
		{
			KingdomScenarioFrames.Observe(Z);
		}
	}
}
