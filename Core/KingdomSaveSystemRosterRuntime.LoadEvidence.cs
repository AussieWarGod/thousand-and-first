#if !TAF_TESTS
using System.Threading;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>One-load proof from the save header. SerializationReader.Start reads the saved mod
	/// id/version table at XRL/World/SerializationReader.cs:180-224 and exposes it through
	/// ModVersions/GetSavedMods at :2157-2178. AsyncLocal follows XRLGame.LoadGame's async state
	/// machine and is replaced at every new attempt, matching the engine's overlapping-load seam.</summary>
	internal static class KingdomSaveSystemRosterLoadEvidence
	{
		internal const string ModId = "r_ThousandAndFirst";
		private static readonly AsyncLocal<Observation> Current =
			new AsyncLocal<Observation>();

		internal static void Begin()
		{
			Current.Value = new Observation();
		}

		internal static void Observe(SerializationReader Reader)
		{
			Observation current = Current.Value;
			if (current == null || current.Observed || Reader?.ModVersions == null) return;
			current.Observed = true;
			current.ModWasPresent = Reader.ModVersions.ContainsKey(ModId);
		}

		internal static void Consume(out bool Known, out bool ModWasPresent,
			out bool InheritanceAuthorityUnreadable)
		{
			Observation current = Current.Value;
			Known = current != null && current.Observed;
			ModWasPresent = Known && current.ModWasPresent;
			InheritanceAuthorityUnreadable = current != null
				&& current.InheritanceSingletonSeen
				&& current.InheritanceSingletonUnreadable;
			Current.Value = null;
		}

		internal static void BeginObjectStates(SerializationReader Reader)
		{
			Observation current = Current.Value;
			if (current == null) return;
			current.ObjectStateReader = Reader;
			current.ReadingObjectStates = true;
			current.AwaitingObjectStateKey = true;
			current.ObjectReadDepth = 0;
			current.PendingObjectStateKey = null;
		}

		internal static void ObserveObjectStateKey(SerializationReader Reader, string Key)
		{
			Observation current = Current.Value;
			if (current == null || !current.ReadingObjectStates
				|| !object.ReferenceEquals(current.ObjectStateReader, Reader)
				|| !current.AwaitingObjectStateKey || current.ObjectReadDepth != 0) return;
			current.PendingObjectStateKey = Key ?? "";
			current.AwaitingObjectStateKey = false;
		}

		internal static void EnterObjectStateValue(SerializationReader Reader)
		{
			Observation current = Current.Value;
			if (current != null && current.ReadingObjectStates
				&& object.ReferenceEquals(current.ObjectStateReader, Reader)
				&& !current.AwaitingObjectStateKey) current.ObjectReadDepth++;
		}

		internal static void LeaveObjectStateValue(SerializationReader Reader, object Value)
		{
			Observation current = Current.Value;
			if (current == null || !current.ReadingObjectStates
				|| !object.ReferenceEquals(current.ObjectStateReader, Reader)
				|| current.ObjectReadDepth <= 0) return;
			current.ObjectReadDepth--;
			if (current.ObjectReadDepth != 0) return;
			if (current.PendingObjectStateKey == KingdomInheritanceState.StateId)
			{
				current.InheritanceSingletonSeen = true;
				if (Value == null) current.InheritanceSingletonUnreadable = true;
			}
			current.PendingObjectStateKey = null;
			current.AwaitingObjectStateKey = true;
		}

		internal static void EndObjectStates(SerializationReader Reader)
		{
			Observation current = Current.Value;
			if (current == null || !object.ReferenceEquals(current.ObjectStateReader, Reader)) return;
			current.ReadingObjectStates = false;
			current.ObjectStateReader = null;
			current.PendingObjectStateKey = null;
			current.ObjectReadDepth = 0;
		}

		private sealed class Observation
		{
			internal bool Observed;
			internal bool ModWasPresent;
			internal SerializationReader ObjectStateReader;
			internal bool ReadingObjectStates;
			internal bool AwaitingObjectStateKey;
			internal int ObjectReadDepth;
			internal string PendingObjectStateKey;
			internal bool InheritanceSingletonSeen;
			internal bool InheritanceSingletonUnreadable;
		}
	}

	[HarmonyLib.HarmonyPatch(typeof(XRLGame), "LoadGame")]
	internal static class KingdomSaveSystemRosterLoadBeginPatch
	{
		private static void Prefix()
		{
			KingdomSaveSystemRosterLoadEvidence.Begin();
		}
	}

	[HarmonyLib.HarmonyPatch(typeof(SerializationReader), "Start")]
	internal static class KingdomSaveSystemRosterReaderStartPatch
	{
		private static void Postfix(SerializationReader __instance)
		{
			KingdomSaveSystemRosterLoadEvidence.Observe(__instance);
		}
	}
}
#endif
