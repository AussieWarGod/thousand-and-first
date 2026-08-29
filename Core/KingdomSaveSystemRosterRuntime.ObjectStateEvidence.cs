#if !TAF_TESTS
using System;
using HarmonyLib;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Read-only observation of XRLGame.LoadObjectGameState's exact loop. Qud omits null
	/// values from ObjectGameState at XRL/XRLGame.cs:1615-1629, so a markerless legacy save otherwise
	/// loses proof that its inheritance singleton existed but could not resolve. ReadObject is at
	/// XRL/World/SerializationReader.cs:362-365 and ReadOptimizedString at :1072-1087; nested calls
	/// are depth-tracked and never altered.</summary>
	[HarmonyPatch(typeof(XRLGame), "LoadObjectGameState",
		new Type[] { typeof(SerializationReader) })]
	internal static class KingdomSaveRosterObjectStatesPatch
	{
		private static void Prefix(SerializationReader Reader)
		{
			KingdomSaveSystemRosterLoadEvidence.BeginObjectStates(Reader);
		}

		private static void Postfix(SerializationReader Reader)
		{
			KingdomSaveSystemRosterLoadEvidence.EndObjectStates(Reader);
		}
	}

	[HarmonyPatch(typeof(SerializationReader), "ReadOptimizedString")]
	internal static class KingdomSaveRosterObjectStateKeyPatch
	{
		private static void Postfix(SerializationReader __instance, string __result)
		{
			KingdomSaveSystemRosterLoadEvidence.ObserveObjectStateKey(__instance, __result);
		}
	}

	[HarmonyPatch(typeof(SerializationReader), "ReadObject")]
	internal static class KingdomSaveRosterObjectStateValuePatch
	{
		private static void Prefix(SerializationReader __instance)
		{
			KingdomSaveSystemRosterLoadEvidence.EnterObjectStateValue(__instance);
		}

		private static void Postfix(SerializationReader __instance, object __result)
		{
			KingdomSaveSystemRosterLoadEvidence.LeaveObjectStateValue(__instance, __result);
		}

		private static Exception Finalizer(SerializationReader __instance,
			Exception __exception)
		{
			if (__exception != null)
				KingdomSaveSystemRosterLoadEvidence.LeaveObjectStateValue(__instance, null);
			return __exception;
		}
	}
}
#endif
