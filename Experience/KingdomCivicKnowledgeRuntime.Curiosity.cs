#if !TAF_TESTS
using System;
using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>O6: one exact First Feast proposer curates one already-revealed journal place.</summary>
	internal static partial class KingdomCivicKnowledgeRuntime
	{
		private const string SettlementCategory = "Settlements";

		internal static bool TryObserveFirstFeast(KingdomSystem system,
			KingdomFirstFeastReceipt practice, out KingdomCuriosityReceipt receipt,
			out string failure)
		{
			receipt = null; failure = null;
			if (!TryCuriosityCause(system, practice, out KingdomCuriosityCause cause, out failure)
				|| !KingdomCuriosityRuntime.TryReadKnownNotes(
					out List<KingdomCuriosityNote> notes, out failure)
				|| !TryUniqueMemory(out KingdomCivicMemorySystem memory, out failure)
				|| !TryRetireForeignRows(system, memory, out failure)
				|| !KingdomCuriosityLeadTransactions.TryPlanCuriosity(memory, cause, notes,
					out KingdomCuriosityLeadPlan plan, out failure)) return false;
			KingdomCuriosityReceipt planned = plan.CuriosityReceipt;
			bool created = false;
			if (plan.AttentionRequired && !TryEnsureAttention(system, cause.SettlementId,
				cause.SourceId, cause.CompletedTick, out created, out failure)) return false;
			if (!KingdomCuriosityLeadTransactions.TryCommit(plan, memory, system.Experience,
				out bool _, out failure))
			{
				ReleaseProvisionalAttentionIfAbsent(system, memory, cause.SourceId, created);
				return false;
			}
			return KingdomCuriosityLeadTransactions.TryReadExactCuriosity(memory, planned,
				out receipt, out failure);
		}

		internal static bool TryReproveCuriosity(KingdomSystem system,
			KingdomCuriosityReceipt receipt, out string failure)
		{
			failure = null;
			if (receipt == null || system?.Experience == null
				|| !KingdomExperienceRules.TryGetFirstFeast(system.Experience,
					receipt.SettlementId, out KingdomFirstFeastReceipt practice, out failure)
				|| !TryCuriosityCause(system, practice, out KingdomCuriosityCause cause,
					out failure)) return false;
			if (receipt.SourceId != cause.SourceId || receipt.SourceVersion != cause.SourceVersion
				|| receipt.SettlementId != cause.SettlementId
				|| receipt.CuratorResidentId != cause.CuratorResidentId
				|| receipt.CuratorName != cause.CuratorName
				|| receipt.CuratorObjectId != cause.CuratorObjectId
				|| receipt.Reason != cause.Reason || receipt.PreparedTick != cause.CompletedTick
				|| receipt.NoteCategory != cause.RequiredCategory)
			{
				failure = "the current First Feast proposer differs from this curation";
				return false;
			}
			return true;
		}

		internal static void ReconcileFirstFeastsBestEffort(KingdomSystem system)
		{
			List<KingdomFirstFeastReceipt> live = system?.Experience?.FirstFeasts;
			if (live == null || live.Count > KingdomExperienceRules.MaxFirstFeastReceipts) return;
			KingdomFirstFeastReceipt[] rows = new KingdomFirstFeastReceipt[live.Count];
			for (int i = 0; i < live.Count; i++) rows[i] = live[i]?.Copy();
			for (int i = 0; i < rows.Length; i++)
			{
				KingdomFirstFeastReceipt row = rows[i];
				if (!KingdomFirstFeastRules.IsAffirmative(row)) continue;
				if (!TryObserveFirstFeast(system, row, out KingdomCuriosityReceipt _,
					out string failure))
					KingdomLog.Log("curiosity: First Feast source remains directly reviewable ("
						+ failure + ")");
			}
			ReleaseTerminalAttentionBestEffort(system);
		}

		private static bool TryCuriosityCause(KingdomSystem system,
			KingdomFirstFeastReceipt practice, out KingdomCuriosityCause cause,
			out string failure)
		{
			cause = null; failure = null;
			if (system == null || !system.Founded || !KingdomFirstFeastRules.IsAffirmative(practice)
				|| !system.TryFindSettlement(practice.SettlementId, out bool seated,
					out KingdomSettlement settlement))
				return FailCuriosity("the exact affirmative First Feast source is unavailable", out failure);
			KingdomCityBook book = seated ? system.City : settlement?.City;
			KingdomCityFault cityFault = KingdomCityFault.None;
			if (book == null || !book.TryRead(out KingdomCityState state, out cityFault)
				|| !state.TryResidentIndex(practice.ProposerResidentId, out int at)
				|| !state.TryResident(at, out KingdomResidentRow row)
				|| row.Standing != KingdomResidentStanding.Resident
				|| row.Name != practice.ProposerName)
				return FailCuriosity("the First Feast proposer is not an exact current resident ("
					+ cityFault + ")", out failure);
			if (!KingdomResidents.TryResolveBoundBody(system, row.ResidentId, false,
				out GameObject body, out string zoneId)
				|| system.SettlementIdForOwnedZone(zoneId) != practice.SettlementId
				|| string.IsNullOrEmpty(body.IDIfAssigned))
				return FailCuriosity("the named curator is not an exact loaded resident body", out failure);
			string sourceId = practice.PracticeId;
			string objectId = "taf:object:" + body.IDIfAssigned;
			cause = new KingdomCuriosityCause
			{
				SourceId = sourceId, SourceVersion = practice.Version,
				SettlementId = practice.SettlementId,
				CuratorResidentId = row.ResidentId, CuratorName = row.Name,
				CuratorObjectId = objectId, RequiredCategory = SettlementCategory,
				CompletedTick = practice.DecidedTick,
				Reason = "Their city's adopted First Feast gives this known place civic relevance."
			};
			if (!KingdomCuriosityRules.ValidCause(cause))
				return FailCuriosity("the exact First Feast curation cause is not storable", out failure);
			return true;
		}

		private static void ReleaseTerminalAttentionBestEffort(KingdomSystem system)
		{
			if (!TryUniqueMemory(out KingdomCivicMemorySystem memory, out string _)
				|| !KingdomCuriosityLeadTransactions.TryRead(memory, out long _,
					out KingdomCuriosityBook curiosity, out KingdomCivicLeadBook leads,
					out string _)) return;
			for (int i = 0; i < curiosity.Rows.Count; i++)
				if (curiosity.Rows[i].State != KingdomCuriosityState.Available)
					KingdomCuriosityRuntime.TryReleaseTerminalAttention(system.Experience,
						curiosity, curiosity.Rows[i].SourceId, out string _);
			for (int i = 0; i < leads.Rows.Count; i++)
				if (leads.Rows[i].Phase == KingdomCivicLeadPhase.Projected
					|| leads.Rows[i].Phase == KingdomCivicLeadPhase.Invalidated)
					KingdomCivicLeadRuntime.TryReleaseTerminalAttention(system.Experience,
						leads, leads.Rows[i].SourceId, out string _);
		}

		private static bool FailCuriosity(string message, out string failure)
		{ failure = message; return false; }
	}
}
#endif
