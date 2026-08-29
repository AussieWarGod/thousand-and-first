using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// One private, revision-bound O6/D7 preparation. The mutable books never leave this type;
	/// callers receive copies of the selected receipt and must prove attention before a new row
	/// can cross into C18.
	/// </summary>
	internal sealed class KingdomCuriosityLeadPlan
	{
		private readonly IKingdomCivicMemoryAuthority Origin;
		private readonly long ExpectedMemoryRevision;
		private readonly KingdomCuriosityBook Curiosity;
		private readonly KingdomCivicLeadBook Leads;
		private readonly KingdomCuriosityReceipt CuriosityRow;
		private readonly KingdomCivicLeadReceipt LeadRow;

		internal bool MutationPending { get; private set; }
		internal bool AttentionRequired { get; private set; }
		internal bool IsCuriosity => CuriosityRow != null;
		internal KingdomCuriosityReceipt CuriosityReceipt => CuriosityRow?.Copy();
		internal KingdomCivicLeadReceipt CivicLeadReceipt => LeadRow?.Copy();

		internal KingdomCuriosityLeadPlan(IKingdomCivicMemoryAuthority origin, long revision,
			KingdomCuriosityBook curiosity, KingdomCivicLeadBook leads,
			KingdomCuriosityReceipt row, bool changed)
		{
			Origin = origin; ExpectedMemoryRevision = revision;
			Curiosity = curiosity; Leads = leads; CuriosityRow = row?.Copy();
			MutationPending = changed;
			AttentionRequired = row?.State == KingdomCuriosityState.Available;
		}

		internal KingdomCuriosityLeadPlan(IKingdomCivicMemoryAuthority origin, long revision,
			KingdomCuriosityBook curiosity, KingdomCivicLeadBook leads,
			KingdomCivicLeadReceipt row, bool changed)
		{
			Origin = origin; ExpectedMemoryRevision = revision;
			Curiosity = curiosity; Leads = leads; LeadRow = row?.Copy();
			MutationPending = changed;
			AttentionRequired = row?.Phase == KingdomCivicLeadPhase.Prepared;
		}

		internal bool TryCommit(IKingdomCivicMemoryAuthority authority,
			KingdomExperienceLedger ledger, out bool committed, out string failure)
		{
			committed = false; failure = null;
			if (authority == null || !ReferenceEquals(Origin, authority))
				return Fail("the civic-knowledge plan belongs to another authority", out failure);
			if (authority.ReadOnly || authority.Revision != ExpectedMemoryRevision)
				return Fail("civic memory changed or became read-only before publication", out failure);
			if (AttentionRequired && !ExactActiveAttention(ledger, out failure)) return false;
			if (!MutationPending) return true;
			if (!KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity, Leads,
				ExpectedMemoryRevision, out KingdomCuriosityLeadCommitReport _, out failure))
				return false;
			committed = true; return true;
		}

		private bool ExactActiveAttention(KingdomExperienceLedger ledger, out string failure)
		{
			failure = null;
			string source = CuriosityRow?.SourceId ?? LeadRow?.SourceId;
			string settlement = CuriosityRow?.SettlementId ?? LeadRow?.SettlementId;
			long earliest = CuriosityRow?.PreparedTick ?? LeadRow?.CompletedTick ?? -1L;
			string reservation = KingdomCuriosityRules.AttentionReservationId(source);
			if (reservation == null || !KingdomExperienceRules.TryReadAudienceLease(ledger,
				reservation, out KingdomExperienceAudienceReceipt held,
				out KingdomExperienceLeaseState state, out failure)) return false;
			if (state != KingdomExperienceLeaseState.Active || held == null
				|| held.ReservationId != reservation || held.RealmId != ledger.RealmId
				|| held.SettlementId != settlement || held.SourceId != source
				|| held.Lane != KingdomExperienceLane.Curator
				|| held.OptionKind != KingdomExperienceOptionKind.CivicKnowledge
				|| held.CauseTick != held.ReservedTick || held.CauseTick < earliest
				|| held.EnableEpoch < 1L)
				return Fail("civic knowledge has no exact active audience reservation", out failure);
			return true;
		}

		private static bool Fail(string message, out string failure)
		{ failure = message; return false; }
	}

	/// <summary>
	/// Engine-free fan-in from O6/D7 books to C18. Planning changes only private decoded copies;
	/// an exact retry spends no outer revision, and a new row cannot commit without its audience.
	/// </summary>
	internal static partial class KingdomCuriosityLeadTransactions
	{
		internal static bool TryPlanCuriosity(IKingdomCivicMemoryAuthority authority,
			KingdomCuriosityCause cause, IList<KingdomCuriosityNote> notes,
			out KingdomCuriosityLeadPlan plan, out string failure)
		{
			plan = null;
			if (!TryRead(authority, out long revision, out KingdomCuriosityBook curiosity,
				out KingdomCivicLeadBook leads, out failure)) return false;
			long before = curiosity.Revision;
			if (!KingdomCuriosityRules.TryPrepare(curiosity, before, cause, notes,
				out KingdomCuriosityReceipt receipt, out failure)) return false;
			plan = new KingdomCuriosityLeadPlan(authority, revision, curiosity, leads,
				receipt, curiosity.Revision != before);
			return true;
		}

		internal static bool TryPlanLead(IKingdomCivicMemoryAuthority authority,
			KingdomCivicLeadCause cause, int journalCount, out KingdomCuriosityLeadPlan plan,
			out string failure)
		{
			plan = null;
			if (!TryRead(authority, out long revision, out KingdomCuriosityBook curiosity,
				out KingdomCivicLeadBook leads, out failure)) return false;
			long before = leads.Revision;
			// This is a private trial. The real attention proof is mandatory at Commit below.
			if (!KingdomCivicLeadRules.TryPrepare(leads, before, cause, journalCount,
				attentionReserved: true, out KingdomCivicLeadReceipt receipt, out failure)) return false;
			plan = new KingdomCuriosityLeadPlan(authority, revision, curiosity, leads,
				receipt, leads.Revision != before);
			return true;
		}

		internal static bool TryCommit(KingdomCuriosityLeadPlan plan,
			IKingdomCivicMemoryAuthority authority, KingdomExperienceLedger ledger,
			out bool committed, out string failure)
		{
			committed = false; failure = null;
			return plan != null
				? plan.TryCommit(authority, ledger, out committed, out failure)
				: Fail("the civic-knowledge plan is absent", out failure);
		}

		internal static bool TryCloseCuriosity(IKingdomCivicMemoryAuthority authority,
			string sourceId, KingdomCuriosityState state, long tick, out bool committed,
			out KingdomCuriosityReceipt closed, out string failure)
		{
			committed = false; closed = null;
			if (!TryRead(authority, out long revision, out KingdomCuriosityBook curiosity,
				out KingdomCivicLeadBook leads, out failure)
				|| !TryFind(curiosity, sourceId, out KingdomCuriosityReceipt _, out failure))
				return false;
			long before = curiosity.Revision;
			if (!KingdomCuriosityRules.TryClose(curiosity, before, sourceId, state, tick,
				out failure) || !TryFind(curiosity, sourceId, out closed, out failure)) return false;
			if (curiosity.Revision == before) return true;
			if (!KingdomCuriosityLeadCommit.TryCommit(authority, curiosity, leads, revision,
				out KingdomCuriosityLeadCommitReport _, out failure)) return false;
			committed = true; return true;
		}

		internal static bool TryInvalidateLead(IKingdomCivicMemoryAuthority authority,
			string sourceId, out bool committed, out string failure)
		{
			committed = false;
			if (!TryRead(authority, out long revision, out KingdomCuriosityBook curiosity,
				out KingdomCivicLeadBook leads, out failure)) return false;
			long before = leads.Revision;
			if (!KingdomCivicLeadRules.TryInvalidate(leads, before, sourceId, out failure))
				return false;
			if (leads.Revision == before) return true;
			if (!KingdomCuriosityLeadCommit.TryCommit(authority, curiosity, leads, revision,
				out KingdomCuriosityLeadCommitReport _, out failure)) return false;
			committed = true; return true;
		}

		internal static bool TryRead(IKingdomCivicMemoryAuthority authority, out long revision,
			out KingdomCuriosityBook curiosity, out KingdomCivicLeadBook leads,
			out string failure)
		{
			revision = -1L; curiosity = null; leads = null; failure = null;
			if (authority == null || authority.ReadOnly)
				return Fail("civic memory is absent or read-only (" + authority?.ReadOnlyReason + ")",
					out failure);
			long before = authority.Revision;
			KingdomCivicMemoryState snapshot = authority.Read();
			if (snapshot == null || snapshot.ReadOnly || snapshot.Revision != before
				|| authority.Revision != before)
				return Fail("civic memory moved or could not be read", out failure);
			KingdomCivicMemorySection curiositySection = snapshot.Section(
				KingdomCivicMemoryLimits.SectionCuriosity);
			KingdomCivicMemorySection leadSection = snapshot.Section(
				KingdomCivicMemoryLimits.SectionCivicLeads);
			curiosity = curiositySection == null ? new KingdomCuriosityBook()
				: KingdomCuriosityLeadCodec.DecodeCuriosity(curiositySection.Payload());
			leads = leadSection == null ? new KingdomCivicLeadBook()
				: KingdomCuriosityLeadCodec.DecodeLeads(leadSection.Payload());
			if (curiosity == null || leads == null
				|| curiosity.State == KingdomCuriosityBookState.Quarantined
				|| leads.State == KingdomCuriosityBookState.Quarantined)
				return Fail("a civic-knowledge section is quarantined and will not be rewritten",
					out failure);
			revision = before; return true;
		}

		internal static bool TryFind(KingdomCuriosityBook book, string sourceId,
			out KingdomCuriosityReceipt receipt, out string failure)
		{
			receipt = null; failure = null;
			if (!KingdomCuriosityRules.ValidBook(book))
				return Fail("the curiosity book is not readable", out failure);
			for (int i = 0; i < book.Rows.Count; i++)
				if (book.Rows[i].SourceId == sourceId)
				{ receipt = book.Rows[i].Copy(); return true; }
			return Fail("the curiosity source is absent", out failure);
		}

		private static bool Fail(string message, out string failure)
		{ failure = message; return false; }
	}
}
