using System;
using System.Diagnostics;
using HarmonyLib;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal sealed class KingdomCampHeartChainSealTrace
	{
		[ThreadStatic] internal static KingdomCampHeartChainSealTrace Current;
		internal readonly Stopwatch Watch = Stopwatch.StartNew();
		internal readonly long Tick = The.Game.TimeTicks;
		internal readonly bool WasBound = KingdomSurvey.HasBoundPass;
		internal readonly KingdomCampHeartChainSealTrace Parent = Current;
		internal int UnboundSurveys;
	}

	[HarmonyPatch(typeof(KingdomSeal), nameof(KingdomSeal.HandleEvent), new Type[] { typeof(EndTurnEvent) })]
	internal static class KingdomCampHeartChainSealTimingPatch
	{
		[HarmonyPrefix]
		internal static void Prefix(out KingdomCampHeartChainSealTrace __state)
		{
			__state = KingdomCampHeartChainTrace.Active(The.Game?.GetSystem<KingdomSystem>())
				? new KingdomCampHeartChainSealTrace() : null;
			if (__state != null) KingdomCampHeartChainSealTrace.Current = __state;
		}

		[HarmonyFinalizer]
		internal static void Finalizer(KingdomCampHeartChainSealTrace __state)
		{
			if (__state == null) return;
			KingdomCampHeartChainSealTrace.Current = __state.Parent;
			if (KingdomSurvey.HasBoundPass != __state.WasBound)
				KingdomScenarioJournal.Append("camp-heart-chain-diagnostic", false,
					"daily seal changed its caller survey lifetime");
			if (__state.Watch.ElapsedMilliseconds >= 100 || __state.UnboundSurveys > 0)
				KingdomLog.Log("chain seal timing: tick=" + __state.Tick
					+ "; ms=" + __state.Watch.ElapsedMilliseconds
					+ "; unbound-surveys=" + __state.UnboundSurveys);
		}
	}

	[HarmonyPatch(typeof(KingdomSurvey), nameof(KingdomSurvey.Take), new Type[] { typeof(Zone) })]
	internal static class KingdomCampHeartChainSealSurveyPatch
	{
		[HarmonyPrefix]
		internal static void Prefix(Zone Z)
		{
			var active = KingdomCampHeartChainSealTrace.Current;
			if (active != null && KingdomSurvey.ActiveFor(Z) == null) active.UnboundSurveys++;
		}
	}
}
