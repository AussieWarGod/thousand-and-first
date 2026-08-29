#if !TAF_TESTS
using System;
using XRL;

namespace ThousandAndFirst
{
	/// <summary>C19's engine seam: one exact C18 owner and one source-owned D10 audience lease.</summary>
	internal static partial class KingdomCivicKnowledgeRuntime
	{
		internal static bool TryUniqueMemory(out KingdomCivicMemorySystem memory,
			out string failure)
		{
			memory = null; failure = null;
			if (The.Game == null || The.Game.Systems == null)
			{ failure = "the save-system roster is unavailable"; return false; }
			int count = 0;
			for (int i = 0; i < The.Game.Systems.Count; i++)
			{
				IGameSystem candidate = The.Game.Systems[i];
				if (candidate != null && candidate.GetType() == typeof(KingdomCivicMemorySystem)
					&& !candidate.Removed)
				{ memory = (KingdomCivicMemorySystem)candidate; count++; }
			}
			if (count == 1) return true;
			memory = null;
			failure = "civic knowledge requires exactly one civic-memory save system";
			return false;
		}

		internal static bool TryEnsureAttention(KingdomSystem system, string settlementId,
			string sourceId, long earliestCauseTick, out bool created, out string failure)
		{
			created = false; failure = null;
			if (system?.Experience == null || earliestCauseTick < 0L)
			{ failure = "the civic-knowledge attention context is invalid"; return false; }
			string reservationId = KingdomCuriosityRules.AttentionReservationId(sourceId);
			if (reservationId == null || !KingdomExperienceRules.TryReadAudienceLease(
				system.Experience, reservationId, out KingdomExperienceAudienceReceipt held,
				out KingdomExperienceLeaseState state, out failure)) return false;
			if (held != null)
			{
				if (!ExactAttention(system, settlementId, sourceId, earliestCauseTick, held))
				{ failure = "the civic-knowledge audience identity names different evidence"; return false; }
				if (state == KingdomExperienceLeaseState.Active) return true;
				if (!KingdomExperienceRuntime.TryReleaseAudience(system, reservationId, sourceId,
					out KingdomExperienceCapacityFault _, out failure)) return false;
				failure = "the former civic-knowledge audience is retired; review after the option "
					+ "is enabled again";
				return false;
			}

			long now = Now();
			if (!KingdomExperienceRuntime.TryObserveConfiguredOptions(system, now, out failure)
				|| !KingdomExperienceRules.TryGetEnableEpoch(system.Experience,
					KingdomExperienceOptionKind.CivicKnowledge, now, out long epoch, out failure)
				) return false;
			KingdomExperienceAudienceReceipt request = new KingdomExperienceAudienceReceipt
			{
				ReservationId = reservationId, RealmId = system.RealmId,
				SettlementId = settlementId, SourceId = sourceId,
				Lane = KingdomExperienceLane.Curator,
				OptionKind = KingdomExperienceOptionKind.CivicKnowledge,
				CauseTick = now, ReservedTick = now, EnableEpoch = epoch
			};
			if (!KingdomExperienceRuntime.TryReserveAudience(system, request,
				out KingdomExperienceCapacityFault _, out failure)) return false;
			created = true; return true;
		}

		internal static void ReleaseProvisionalAttentionIfAbsent(KingdomSystem system,
			KingdomCivicMemorySystem memory, string sourceId, bool created)
		{
			if (!created) return;
			if (!KingdomCuriosityLeadTransactions.TryProveSourceAbsent(memory, sourceId,
				out bool absent, out string proof) || !absent)
			{
				KingdomLog.Log("civic knowledge: provisional attention retained until exact "
					+ "C18 absence is proved (" + (proof ?? "source stands") + ")");
				return;
			}
			string reservationId = KingdomCuriosityRules.AttentionReservationId(sourceId);
			if (!KingdomExperienceRuntime.TryReleaseAudience(system, reservationId, sourceId,
				out KingdomExperienceCapacityFault _, out string failure))
				KingdomLog.Log("civic knowledge: provisional attention remains for recovery ("
					+ failure + ")");
		}

		internal static bool TryRetireForeignRows(KingdomSystem system,
			KingdomCivicMemorySystem memory, out string failure)
		{
			failure = null;
			if (system == null || memory == null
				|| !system.TryGetCurrentIdentity(out string realmId, out string _)
				|| system.Experience == null || system.Experience.RealmId != realmId
				|| !system.TryExactSettlementIds(true, out System.Collections.Generic.List<string> ids,
					out failure))
			{
				failure = failure ?? "current realm topology cannot retire civic knowledge";
				return false;
			}
			return KingdomCuriosityLeadTransactions.TryRetireForeignSettlements(memory, ids,
				out bool _, out int _, out failure);
		}

		/// <summary>Shared semantic/Charter recovery. Every mutation remains source-proved and
		/// current-loaded; pause turns this into a no-op.</summary>
		internal static void ReconcileCurrentBestEffort(KingdomSystem system,
			XRL.World.Zone zone)
		{
			if (system == null || zone == null || !KingdomMaster.NewWorkAllowed(system)
				|| !TryUniqueMemory(out KingdomCivicMemorySystem memory, out string failure)) return;
			if (!TryRetireForeignRows(system, memory, out failure))
				KingdomLog.Log("civic knowledge: realm retirement waits (" + failure + ")");
			ReconcileFirstFeastsBestEffort(system);
			ReconcileLoadedDelveBestEffort(system, zone);
		}

		private static bool ExactAttention(KingdomSystem system, string settlementId,
			string sourceId, long earliestCauseTick, KingdomExperienceAudienceReceipt held)
		{
			return held != null && held.RealmId == system.RealmId
				&& held.SettlementId == settlementId && held.SourceId == sourceId
				&& held.Lane == KingdomExperienceLane.Curator
				&& held.OptionKind == KingdomExperienceOptionKind.CivicKnowledge
				&& held.CauseTick == held.ReservedTick && held.CauseTick >= earliestCauseTick
				&& held.EnableEpoch >= 1L;
		}

		internal static long Now()
		{
			return The.Game == null || The.Game.TimeTicks < 0L ? 0L : The.Game.TimeTicks;
		}
	}
}
#endif
