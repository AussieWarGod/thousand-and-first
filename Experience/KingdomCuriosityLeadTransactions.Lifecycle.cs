using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomCuriosityLeadTransactions
	{
		/// <summary>Removes rows whose exact settlement is outside current proved topology.
		/// Books predate realm binding, so this is their bounded realm-replacement migration.</summary>
		internal static bool TryRetireForeignSettlements(IKingdomCivicMemoryAuthority authority,
			IList<string> currentSettlementIds, out bool committed, out int retired,
			out string failure)
		{
			committed = false; retired = 0; failure = null;
			if (!TrySettlementSet(currentSettlementIds, out HashSet<string> current, out failure)
				|| !TryRead(authority, out long revision, out KingdomCuriosityBook curiosity,
					out KingdomCivicLeadBook leads, out failure)) return false;
			int curiosityRemoved = RemoveForeign(curiosity.Rows, current);
			int leadRemoved = RemoveForeign(leads.Rows, current);
			if ((curiosityRemoved > 0 && curiosity.Revision == long.MaxValue)
				|| (leadRemoved > 0 && leads.Revision == long.MaxValue))
				return Fail("civic-knowledge retirement revision is exhausted", out failure);
			if (curiosityRemoved > 0) curiosity.Revision++;
			if (leadRemoved > 0) leads.Revision++;
			retired = curiosityRemoved + leadRemoved;
			if (retired == 0) return true;
			if (!KingdomCuriosityLeadCommit.TryCommit(authority, curiosity, leads, revision,
				out KingdomCuriosityLeadCommitReport _, out failure))
			{ retired = 0; return false; }
			committed = true;
			return true;
		}

		internal static bool TryReadExactCuriosity(IKingdomCivicMemoryAuthority authority,
			KingdomCuriosityReceipt expected, out KingdomCuriosityReceipt durable,
			out string failure)
		{
			durable = null; failure = null;
			if (expected == null || !TryRead(authority, out long _,
				out KingdomCuriosityBook curiosity, out KingdomCivicLeadBook _, out failure)
				|| !TryFind(curiosity, expected.SourceId, out durable, out failure)) return false;
			if (Same(expected, durable)) return true;
			durable = null;
			return Fail("the durable curiosity row differs from the caller receipt", out failure);
		}

		internal static bool TryReadExactLead(IKingdomCivicMemoryAuthority authority,
			KingdomCivicLeadReceipt expected, out KingdomCivicLeadReceipt durable,
			out string failure)
		{
			durable = null; failure = null;
			if (expected == null || !TryRead(authority, out long _,
				out KingdomCuriosityBook _, out KingdomCivicLeadBook leads, out failure)) return false;
			for (int i = 0; i < leads.Rows.Count; i++)
				if (leads.Rows[i].SourceId == expected.SourceId)
				{
					if (Same(expected, leads.Rows[i]))
					{ durable = leads.Rows[i].Copy(); return true; }
					return Fail("the durable civic-lead row differs from the caller receipt", out failure);
				}
			return Fail("the durable civic-lead source is absent", out failure);
		}

		/// <summary>Only this exact readable absence permits provisional audience rollback.</summary>
		internal static bool TryProveSourceAbsent(IKingdomCivicMemoryAuthority authority,
			string sourceId, out bool absent, out string failure)
		{
			absent = false; failure = null;
			if (!KingdomCuriosityRules.ValidId(sourceId)
				|| !TryRead(authority, out long _, out KingdomCuriosityBook curiosity,
					out KingdomCivicLeadBook leads, out failure)) return false;
			for (int i = 0; i < curiosity.Rows.Count; i++)
				if (curiosity.Rows[i].SourceId == sourceId) return true;
			for (int i = 0; i < leads.Rows.Count; i++)
				if (leads.Rows[i].SourceId == sourceId) return true;
			absent = true;
			return true;
		}

		private static int RemoveForeign(List<KingdomCuriosityReceipt> rows,
			HashSet<string> current)
		{
			int removed = 0;
			for (int i = rows.Count - 1; i >= 0; i--)
				if (!current.Contains(rows[i].SettlementId)) { rows.RemoveAt(i); removed++; }
			return removed;
		}

		private static int RemoveForeign(List<KingdomCivicLeadReceipt> rows,
			HashSet<string> current)
		{
			int removed = 0;
			for (int i = rows.Count - 1; i >= 0; i--)
				if (!current.Contains(rows[i].SettlementId)) { rows.RemoveAt(i); removed++; }
			return removed;
		}

		private static bool TrySettlementSet(IList<string> ids,
			out HashSet<string> current, out string failure)
		{
			current = new HashSet<string>(StringComparer.Ordinal); failure = null;
			if (ids == null || ids.Count == 0)
				return Fail("current settlement topology is absent", out failure);
			for (int i = 0; i < ids.Count; i++)
				if (!KingdomCuriosityRules.ValidId(ids[i]) || !current.Add(ids[i]))
					return Fail("current settlement topology is invalid", out failure);
			return true;
		}

		private static bool Same(KingdomCuriosityReceipt a, KingdomCuriosityReceipt b)
		{
			return a != null && b != null && a.Version == b.Version && a.State == b.State
				&& a.SourceId == b.SourceId && a.SourceVersion == b.SourceVersion
				&& a.SettlementId == b.SettlementId
				&& a.CuratorResidentId == b.CuratorResidentId
				&& a.CuratorName == b.CuratorName && a.CuratorObjectId == b.CuratorObjectId
				&& a.NoteId == b.NoteId && a.Locator == b.Locator && a.NoteText == b.NoteText
				&& a.NoteCategory == b.NoteCategory && a.Reason == b.Reason
				&& a.PreparedTick == b.PreparedTick && a.ClosedTick == b.ClosedTick;
		}

		private static bool Same(KingdomCivicLeadReceipt a, KingdomCivicLeadReceipt b)
		{
			return a != null && b != null && a.Version == b.Version && a.Phase == b.Phase
				&& a.SourceId == b.SourceId && a.SourceVersion == b.SourceVersion
				&& a.SettlementId == b.SettlementId && a.LeadId == b.LeadId
				&& a.Locator == b.Locator && a.Title == b.Title
				&& a.AuthoredReason == b.AuthoredReason
				&& a.CompletedTick == b.CompletedTick && a.Fault == b.Fault;
		}
	}
}
