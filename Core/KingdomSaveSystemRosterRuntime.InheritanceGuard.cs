#if !TAF_TESTS
using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Session-only, false-to-true guard for every inheritance carrier implicated by roster
	/// recovery. Mandatory durable carrier latches veto saving; this guard also stops gameplay mutation.
	/// Weak keys avoid retaining systems after their game ends. First cause is never overwritten.</summary>
	internal static class KingdomSaveSystemRosterInheritanceGuard
	{
		private static readonly object Sync = new object();
		private static readonly ConditionalWeakTable<KingdomInheritanceLifecycle, Witness>
			Refused = new ConditionalWeakTable<KingdomInheritanceLifecycle, Witness>();

		internal static void Refuse(KingdomInheritanceLifecycle System, string Cause)
		{
			if (System == null) return;
			lock (Sync)
			{
				if (!Refused.TryGetValue(System, out Witness _))
					Refused.Add(System, new Witness(Cause));
			}
		}

		internal static bool IsRefused(KingdomInheritanceLifecycle System)
		{
			return System != null && Refused.TryGetValue(System, out Witness witness)
				&& !string.IsNullOrEmpty(witness.Cause);
		}

		private sealed class Witness
		{
			internal readonly string Cause;

			internal Witness(string Cause)
			{
				this.Cause = string.IsNullOrEmpty(Cause)
					? "the saved inheritance lifecycle roster could not be proved" : Cause;
			}
		}
	}

	[HarmonyPatch(typeof(KingdomInheritanceLifecycle), "Register",
		new Type[] { typeof(XRLGame), typeof(IEventRegistrar) })]
	internal static class KingdomSaveRosterInheritanceRegisterPatch
	{
		private static bool Prefix(KingdomInheritanceLifecycle __instance)
		{
			return !KingdomSaveSystemRosterInheritanceGuard.IsRefused(__instance);
		}
	}

	[HarmonyPatch(typeof(KingdomInheritanceLifecycle), "AfterLoad",
		new Type[] { typeof(XRLGame) })]
	internal static class KingdomSaveRosterInheritanceAfterLoadPatch
	{
		private static bool Prefix(KingdomInheritanceLifecycle __instance)
		{
			return !KingdomSaveSystemRosterInheritanceGuard.IsRefused(__instance);
		}
	}

	[HarmonyPatch(typeof(KingdomInheritanceLifecycle), "HandleEvent",
		new Type[] { typeof(AfterGameLoadedEvent) })]
	internal static class KingdomSaveRosterInheritanceLoadedPatch
	{
		private static bool Prefix(KingdomInheritanceLifecycle __instance, ref bool __result)
		{
			if (!KingdomSaveSystemRosterInheritanceGuard.IsRefused(__instance)) return true;
			__result = true; return false;
		}
	}

	[HarmonyPatch(typeof(KingdomInheritanceLifecycle), "HandleEvent",
		new Type[] { typeof(EndTurnEvent) })]
	internal static class KingdomSaveRosterInheritanceTurnPatch
	{
		private static bool Prefix(KingdomInheritanceLifecycle __instance, ref bool __result)
		{
			if (!KingdomSaveSystemRosterInheritanceGuard.IsRefused(__instance)) return true;
			__result = true; return false;
		}
	}

	[HarmonyPatch(typeof(KingdomInheritanceLifecycle), "HandleEvent",
		new Type[] { typeof(ZoneBuiltEvent) })]
	internal static class KingdomSaveRosterInheritanceZonePatch
	{
		private static bool Prefix(KingdomInheritanceLifecycle __instance, ref bool __result)
		{
			if (!KingdomSaveSystemRosterInheritanceGuard.IsRefused(__instance)) return true;
			__result = true; return false;
		}
	}
}
#endif
