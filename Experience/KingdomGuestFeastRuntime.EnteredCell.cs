#if !TAF_TESTS
using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomGuestFeastRuntime
	{
		internal sealed class ZoneStamp
		{
			public ZoneStamp() { }
			internal string ZoneId;
			internal long ResumeToken;
			internal bool Suspended;
			internal bool StoryObserved;
			internal long StoryEpoch;
		}
		internal static readonly ConditionalWeakTable<GameObject, ZoneStamp> ZoneStamps =
			new ConditionalWeakTable<GameObject, ZoneStamp>();

		internal static bool TryObserveEnteredZone(KingdomSystem system, Zone zone,
			bool firstBodyObservation, bool story, out string failure)
		{
			failure = null;
			long now = The.Game?.TimeTicks ?? -1L;
			if (system == null || zone == null || now < 0L
				|| !ReferenceEquals(zone, The.Player?.CurrentZone)
				|| !KingdomMaster.NewWorkAllowed(system)) return true;
			if (!TryRead(system, out KingdomGuestFeastBook snapshot, out failure)) return false;
			if (!snapshot.IdentityBound) return true;
			string[] settlements = new string[snapshot.Rows.Count];
			for (int i = 0; i < snapshot.Rows.Count; i++)
				settlements[i] = snapshot.Rows[i].SettlementId;
			for (int i = 0; i < settlements.Length; i++)
			{
				if (!TryCycleOne(system, zone, settlements[i], story,
					firstBodyObservation, out failure)) return false;
			}
			return true;
		}

		internal static bool TryStoryState(KingdomSystem system, out bool enabled,
			out long epoch, out string failure)
		{
			enabled = false; epoch = 0L; failure = null;
			long now = The.Game?.TimeTicks ?? -1L;
			if (system == null || now < 0L
				|| !KingdomExperienceRuntime.TryObserveConfiguredOptions(system, now,
					out failure)) return false;
			enabled = KingdomExperienceRules.CanEmit(system.Experience,
				KingdomExperienceOptionKind.CivicStory, now);
			return !enabled || KingdomExperienceRules.TryGetEnableEpoch(system.Experience,
				KingdomExperienceOptionKind.CivicStory, now, out epoch, out failure);
		}

		internal static bool TryDisarmCycles(KingdomSystem system, out string failure)
		{
			failure = null;
			if (!TryRead(system, out KingdomGuestFeastBook snapshot, out failure)) return false;
			if (!snapshot.IdentityBound) return true;
			string[] settlements = new string[snapshot.Rows.Count];
			for (int i = 0; i < snapshot.Rows.Count; i++)
				settlements[i] = snapshot.Rows[i].SettlementId;
			for (int i = 0; i < settlements.Length; i++)
			{
				if (!TryRead(system, out KingdomGuestFeastBook book, out failure)
					|| !KingdomGuestFeastRules.TryFind(book, settlements[i],
						out KingdomGuestFeastReceipt row)) return false;
				if (row == null || row.Phase != KingdomGuestFeastPhase.Cycling
					|| !row.AwayArmed) continue;
				KingdomGuestFeastBook next = KingdomGuestFeastRules.Clone(book);
				if (!KingdomGuestFeastRules.TryObserveZoneCycle(next, next.Revision,
					settlements[i], false, false, out bool changed, out failure)
					|| changed && !TryPublish(system, next, out failure)) return false;
			}
			return true;
		}

		private static bool TryCycleOne(KingdomSystem system, Zone zone,
			string settlementId, bool story, bool homeOnly, out string failure)
		{
			failure = null;
			if (!TryRead(system, out KingdomGuestFeastBook book, out failure)
				|| !KingdomGuestFeastRules.TryFind(book, settlementId,
					out KingdomGuestFeastReceipt row)) return false;
			if (row == null || row.Phase != KingdomGuestFeastPhase.Cycling) return true;
			bool atHome = string.Equals(system.SettlementIdForOwnedZone(zone.ZoneID),
				settlementId, StringComparison.Ordinal);
			// Static edge memory is intentionally absent after load/body-swap. First observation
			// may close only a return whose durable AwayArmed bit already proves departure; it
			// never manufactures an away edge from where a save happened to resume.
			if (homeOnly && (!atHome || !row.AwayArmed)) return true;
			KingdomGuestFeastBook next = KingdomGuestFeastRules.Clone(book);
			if (!KingdomGuestFeastRules.TryObserveZoneCycle(next, next.Revision,
				settlementId, atHome, story, out bool changed, out failure)) return false;
			if (!changed || TryPublish(system, next, out failure))
			{
				if (changed)
				{
					KingdomGuestFeastRules.TryFind(next, settlementId,
						out KingdomGuestFeastReceipt observed);
					KingdomExperienceRuntime.TryRecord(system,
						KingdomExperienceExperiment.GuestsFeast,
						KingdomExperienceTrialArm.Integrated,
						observed?.Phase == KingdomGuestFeastPhase.Exhausted
							? KingdomExperienceObservationKind.QuietCompletion
							: KingdomExperienceObservationKind.Viewed,
						observed?.HomeCycles ?? 0);
				}
				return true;
			}
			return false;
		}
	}

	/// <summary>
	/// Qud 2.0.211.51 evidence: Cell.cs:3404-3408 adds the object before EnteredCell;
	/// GameObject.cs:14357-14363 dispatches this exact typed overload; GameObject.cs:16557-16572
	/// sends it for every player entry; EnteredCellEvent.cs:56-71 freezes Object and Cell.
	/// </summary>
	[HarmonyPatch(typeof(GameObject), "HandleEvent",
		new Type[] { typeof(EnteredCellEvent) })]
	internal static class KingdomGuestFeastEnteredCellPatch
	{
		[HarmonyPostfix]
		internal static void Postfix(GameObject __instance, EnteredCellEvent E)
		{
			try
			{
				Zone zone = E?.Cell?.ParentZone;
				if (__instance == null || !__instance.IsPlayer() || E?.Object != __instance
					|| zone == null || !ReferenceEquals(__instance.CurrentZone, zone)) return;
				KingdomGuestFeastRuntime.ZoneStamp stamp =
					KingdomGuestFeastRuntime.ZoneStamps.GetOrCreateValue(__instance);
				bool first = stamp.ZoneId == null;
				bool sameZone = !first && string.Equals(stamp.ZoneId, zone.ZoneID,
					StringComparison.Ordinal);
				// EnteredCell fires for every step. A same-zone step is not a journey edge and
				// must not read options, ledgers, city books, or allocate snapshots.
				if (sameZone) return;
				KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
				if (system == null || !system.Founded) return;
				if (!KingdomMaster.NewWorkAllowed(system))
				{
					stamp.Suspended = true; stamp.ZoneId = zone.ZoneID;
					stamp.ResumeToken = system.MasterResumeToken; return;
				}
				// A fresh body/session cannot order its missing edge memory against an earlier
				// master resume. Prefer dropping an armed edge to inventing a paused journey.
				if (first && system.MasterResumeToken != 0L) stamp.Suspended = true;
				if (stamp.ZoneId != null && stamp.ResumeToken != system.MasterResumeToken)
					stamp.Suspended = true;
				if (stamp.Suspended)
				{
					if (!KingdomGuestFeastRuntime.TryDisarmCycles(system, out string disarm))
					{
						KingdomLog.Log("guest feast: resumed-cycle disarm retained (" + disarm + ")");
						return;
					}
					stamp.Suspended = false; stamp.ZoneId = zone.ZoneID;
					stamp.ResumeToken = system.MasterResumeToken; return;
				}
				if (!KingdomGuestFeastRuntime.TryStoryState(system, out bool story,
					out long storyEpoch, out string optionFailure))
				{
					KingdomLog.Log("guest feast: option observation retained ("
						+ optionFailure + ")"); return;
				}
				bool optionChanged = stamp.StoryObserved
					? stamp.StoryEpoch != storyEpoch : !story || storyEpoch > 1L;
				if (optionChanged)
				{
					KingdomFirstFeastRuntime.ReconcileBestEffort(system);
					if (!KingdomGuestFeastRuntime.TryDisarmCycles(system,
						out string optionDisarm))
					{
						KingdomLog.Log("guest feast: option-cycle disarm retained ("
							+ optionDisarm + ")"); return;
					}
					if (!story) KingdomExperienceRuntime.TryRecord(system,
						KingdomExperienceExperiment.GuestsFeast,
						KingdomExperienceTrialArm.FactsOnly,
						KingdomExperienceObservationKind.Closed, 0);
					stamp.StoryObserved = true; stamp.StoryEpoch = storyEpoch;
					stamp.ZoneId = zone.ZoneID; stamp.ResumeToken = system.MasterResumeToken;
					return;
				}
				stamp.StoryObserved = true; stamp.StoryEpoch = storyEpoch;
				if (!KingdomGuestFeastRuntime.TryObserveEnteredZone(system, zone, first, story,
					out string failure))
				{
					KingdomLog.Log("guest feast: entered-zone observation retained (" + failure + ")");
					return;
				}
				stamp.ZoneId = zone.ZoneID; stamp.ResumeToken = system.MasterResumeToken;
			}
			catch (Exception error)
			{
				KingdomLog.Log("guest feast: entered-zone observation failed (" + error.Message + ")");
			}
		}
	}
}
#endif
