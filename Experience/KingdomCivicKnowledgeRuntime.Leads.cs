#if !TAF_TESTS
using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>D7: a completed, physically standing city delve authors one optional map lead.</summary>
	internal static partial class KingdomCivicKnowledgeRuntime
	{
		internal static bool TryObserveCompletedDelve(KingdomSystem system, string headZoneId,
			long completedTick, out KingdomCivicLeadReceipt receipt, out string failure)
		{
			receipt = null; failure = null;
			string settlementId = system?.SettlementIdForOwnedZone(headZoneId);
			if (settlementId == null || !KingdomCivicLeadRuntime.TryCauseFromCompletedDelve(
				settlementId, headZoneId, completedTick, out KingdomCivicLeadCause cause)
				|| !KingdomCuriosityRuntime.TryReadMapNoteCount(out int journalCount, out failure)
				|| !TryUniqueMemory(out KingdomCivicMemorySystem memory, out failure)
				|| !TryRetireForeignRows(system, memory, out failure)
				|| !KingdomCuriosityLeadTransactions.TryPlanLead(memory, cause, journalCount,
					out KingdomCuriosityLeadPlan plan, out failure))
				return FailLead(failure ?? "the completed delve could not be reproved", out failure);
			KingdomCivicLeadReceipt planned = plan.CivicLeadReceipt;
			bool created = false;
			if (plan.AttentionRequired && !TryEnsureAttention(system, settlementId,
				cause.SourceId, cause.CompletedTick, out created, out failure)) return false;
			if (!KingdomCuriosityLeadTransactions.TryCommit(plan, memory, system.Experience,
				out bool _, out failure))
			{
				ReleaseProvisionalAttentionIfAbsent(system, memory, cause.SourceId, created);
				return false;
			}
			return KingdomCuriosityLeadTransactions.TryReadExactLead(memory, planned,
				out receipt, out failure);
		}

		internal static void ObserveCurrentDelveBestEffort(KingdomSystem system, Zone zone,
			long completedTick)
		{
			if (system == null || zone == null || !TryHeadForLoadedZone(zone.ZoneID,
				out string head)) return;
			if (!TryObserveCompletedDelve(system, head, completedTick,
				out KingdomCivicLeadReceipt _, out string failure))
				KingdomLog.Log("civic lead: completed delve remains directly reviewable ("
					+ failure + ")");
		}

		internal static void ReconcileLoadedDelveBestEffort(KingdomSystem system, Zone zone)
		{
			if (system == null || zone == null || !ReferenceEquals(zone, The.Player?.CurrentZone)
				|| !TryHeadForLoadedZone(zone.ZoneID, out string head)
				|| system.SettlementIdForOwnedZone(head) == null
				|| !KingdomDelveLink.TryReadLoadedCompletion(head,
					out KingdomDelveLinkReceipt _, out long completedTick, out string _)) return;
			if (!TryObserveCompletedDelve(system, head, completedTick,
				out KingdomCivicLeadReceipt _, out string failure))
				KingdomLog.Log("civic lead: loaded delve remains directly reviewable ("
					+ failure + ")");
		}

		internal static bool TryReproveLead(KingdomSystem system, KingdomCivicLeadReceipt row,
			out bool sourceMissing, out string failure)
		{
			sourceMissing = false; failure = null;
			if (system == null || row == null || !TryHeadFromFoot(row.Locator, out string head)
				|| system.SettlementIdForOwnedZone(head) != row.SettlementId)
				return FailLead("the civic lead does not name exact current owned ground", out failure);
			string current = The.Player?.CurrentZone?.ZoneID;
			if (current != head && current != row.Locator)
				return FailLead("visit the delve's upper or lower landing before reviewing this lead",
					out failure);
			if (!KingdomDelveLink.TryReadPhysicalReceipt(head,
				out KingdomDelveLinkReceipt physical))
			{
				sourceMissing = true;
				return FailLead("the delve link this lead recorded no longer stands", out failure);
			}
			if (!KingdomCivicLeadRuntime.LinkZonesLoaded(physical))
				return FailLead("visit both landings once before the link is reproved", out failure);
			if (!KingdomCivicLeadRuntime.TryCauseFromCompletedDelve(row.SettlementId, head,
				row.CompletedTick, out KingdomCivicLeadCause cause)
				|| !SameLead(row, cause))
			{
				sourceMissing = true;
				return FailLead("the exact physical delve source changed or was removed", out failure);
			}
			return true;
		}

		private static bool TryHeadForLoadedZone(string zoneId, out string head)
		{
			head = null;
			if (KingdomDelveLink.TryReadPhysicalReceipt(zoneId,
				out KingdomDelveLinkReceipt receipt))
			{ head = receipt.HeadZoneId; return true; }
			return TryHeadFromFoot(zoneId, out head)
				&& KingdomDelveLink.TryReadPhysicalReceipt(head, out receipt)
				&& receipt.FootZoneId == zoneId;
		}

		private static bool TryHeadFromFoot(string foot, out string head)
		{
			head = null;
			if (!KingdomCuriosityRules.TryFullLocator(foot, out string world,
				out int px, out int py, out int zx, out int zy, out int zz) || zz <= 0) return false;
			head = KingdomCuriosityRules.Assemble(world, px, py, zx, zy, zz - 1);
			return KingdomDelveRules.IsShaftPair(head, foot);
		}

		private static bool SameLead(KingdomCivicLeadReceipt row, KingdomCivicLeadCause cause)
		{
			return row.SourceId == cause.SourceId && row.SourceVersion == cause.SourceVersion
				&& row.SettlementId == cause.SettlementId && row.Locator == cause.Locator
				&& row.Title == cause.Title && row.AuthoredReason == cause.AuthoredReason
				&& row.CompletedTick == cause.CompletedTick;
		}

		private static bool FailLead(string message, out string failure)
		{ failure = message; return false; }
	}
}
#endif
