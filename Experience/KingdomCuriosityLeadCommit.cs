using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>What one commit attempt did with each book, and why.</summary>
	public enum KingdomCuriosityLeadCarriage : byte
	{
		/// <summary>Encoded by this build and offered to the authority as a new payload.</summary>
		Offered = 0,

		/// <summary>Withheld on purpose, and proven already present in the save unchanged.</summary>
		Withheld = 1,

		/// <summary>This build believed it owned the book and could not write it. Nothing commits.</summary>
		Unwritable = 2
	}

	public readonly struct KingdomCuriosityLeadCommitReport
	{
		public readonly KingdomCuriosityLeadCarriage Curiosity;
		public readonly KingdomCuriosityLeadCarriage CivicLeads;

		/// <summary>Why each book was not offered, when it was not. Empty when it was.</summary>
		public readonly string CuriosityReason, CivicLeadsReason;

		internal KingdomCuriosityLeadCommitReport(KingdomCuriosityLeadCarriage curiosity,
			string curiosityReason, KingdomCuriosityLeadCarriage civicLeads,
			string civicLeadsReason)
		{
			Curiosity = curiosity; CuriosityReason = curiosityReason ?? "";
			CivicLeads = civicLeads; CivicLeadsReason = civicLeadsReason ?? "";
		}
	}

	/// <summary>
	/// The seam between these two books and the civic-memory authority that keeps them for a save.
	/// <para>
	/// Nothing here decides anything about a curation or a lead. It answers one question &mdash;
	/// which of these two books may this build write back &mdash; and the answer turns entirely on
	/// the state the codec gave each book. A compatible book is encoded and offered. A future or
	/// quarantined book is <b>not named at all</b>, so the authority's upsert carries the bytes
	/// already in the save through untouched.
	/// </para>
	/// <para>
	/// Withholding is a claim, and it is checked. Saying "the save already holds this" is only
	/// safe if the save does hold it, so the authority's snapshot at the caller's own revision is
	/// read and the section must be there, byte for byte. A book withheld against a save that
	/// never had it would leave a founder's records nowhere at all, reported as a success.
	/// </para>
	/// <para>
	/// <b>Durability is this file's job and nobody else's.</b> Preparing a curation or a lead
	/// mutates a book in memory and nothing more; that row is not durable until it has come
	/// through here and the authority has accepted it. The civic-lead projection needs <i>two</i>
	/// commits for that reason &mdash; the prepared row must be durable before the journal is
	/// touched, or a crash leaves a note nobody owns, and the projected row must be durable after,
	/// or the next run tries to add a note that is already there. Both cuts are made in
	/// <c>KingdomCuriosityLeadCommit.Projection</c>, under one lease.
	/// </para>
	/// </summary>
	public static partial class KingdomCuriosityLeadCommit
	{
		/// <summary>Commits whichever of the two books this build is entitled to write.</summary>
		/// <param name="expectedRevision">The authority revision the caller last read.</param>
		public static bool TryCommit(IKingdomCivicMemoryAuthority authority,
			KingdomCuriosityBook curiosity, KingdomCivicLeadBook leads, long expectedRevision,
			out KingdomCuriosityLeadCommitReport report, out string failure)
		{
			report = default(KingdomCuriosityLeadCommitReport); failure = null;
			if (authority == null)
			{ failure = "there is no civic-memory authority to commit to"; return false; }
			if (curiosity == null || leads == null)
			{ failure = "both books must be present to commit either"; return false; }
			if (!KingdomCuriosityLeadCodec.Defined(curiosity.State))
			{
				failure = KingdomCuriosityLeadCodec.UndefinedState("curiosity", curiosity.State);
				return false;
			}
			if (!KingdomCuriosityLeadCodec.Defined(leads.State))
			{
				failure = KingdomCuriosityLeadCodec.UndefinedState("civic-lead", leads.State);
				return false;
			}
			if (authority.ReadOnly)
			{
				failure = "civic memory is read-only (" + authority.ReadOnlyReason + ")";
				return false;
			}
			if (expectedRevision != authority.Revision)
			{
				failure = "this commit was built against civic-memory revision " + expectedRevision
					+ " and the authority now stands at " + authority.Revision;
				return false;
			}

			KingdomCivicMemoryState snapshot = authority.Read();
			List<KingdomCivicMemorySection> offered = new List<KingdomCivicMemorySection>();
			if (!TryCarry(curiosity.State, curiosity.OpaquePayload, "curiosity",
				KingdomCivicMemoryLimits.SectionCuriosity, snapshot, offered,
				Encoded(curiosity), out KingdomCuriosityLeadCarriage curiosityCarriage,
				out string curiosityReason))
			{
				report = new KingdomCuriosityLeadCommitReport(curiosityCarriage, curiosityReason,
					KingdomCuriosityLeadCarriage.Withheld,
					"no section was offered because the curiosity book stopped this commit");
				failure = "the curiosity book could not be committed: " + curiosityReason;
				return false;
			}
			if (!TryCarry(leads.State, leads.OpaquePayload, "civic-lead",
				KingdomCivicMemoryLimits.SectionCivicLeads, snapshot, offered, Encoded(leads),
				out KingdomCuriosityLeadCarriage leadCarriage, out string leadReason))
			{
				report = new KingdomCuriosityLeadCommitReport(
					KingdomCuriosityLeadCarriage.Withheld,
					"no section was offered because the civic-lead book stopped this commit",
					leadCarriage, leadReason);
				failure = "the civic-lead book could not be committed: " + leadReason;
				return false;
			}

			report = new KingdomCuriosityLeadCommitReport(curiosityCarriage, curiosityReason,
				leadCarriage, leadReason);
			if (offered.Count == 0) { failure = ""; return true; }
			return authority.TryCommit(offered, expectedRevision, out failure);
		}

		private static byte[] Encoded(KingdomCuriosityBook book)
		{
			return KingdomCuriosityLeadCodec.TryEncode(book, out byte[] bytes, out string _)
				? bytes : null;
		}

		private static byte[] Encoded(KingdomCivicLeadBook book)
		{
			return KingdomCuriosityLeadCodec.TryEncode(book, out byte[] bytes, out string _)
				? bytes : null;
		}

		private static bool TryCarry(KingdomCuriosityBookState state, byte[] retained,
			string family, int sectionId, KingdomCivicMemoryState snapshot,
			List<KingdomCivicMemorySection> offered, byte[] encoded,
			out KingdomCuriosityLeadCarriage carriage, out string reason)
		{
			if (state == KingdomCuriosityBookState.Compatible)
			{
				if (encoded == null)
				{
					carriage = KingdomCuriosityLeadCarriage.Unwritable;
					reason = "this build owns the " + family + " book and could not write it";
					return false;
				}
				offered.Add(new KingdomCivicMemorySection(sectionId, encoded));
				carriage = KingdomCuriosityLeadCarriage.Offered; reason = "";
				return true;
			}

			carriage = KingdomCuriosityLeadCarriage.Withheld;
			string held = state == KingdomCuriosityBookState.FutureOpaque
				? "a later build wrote this " + family + " book"
				: "this " + family + " book is held as evidence";
			KingdomCivicMemorySection section = snapshot.Section(sectionId);
			if (section == null)
			{
				reason = held + ", but civic memory holds no such section to carry through; "
					+ "withholding it would lose the records entirely";
				return false;
			}
			if (!SameBytes(section.Payload(), retained))
			{
				reason = held + ", but the section civic memory holds is not the payload this "
					+ "book was read from; withholding it would leave the wrong bytes standing";
				return false;
			}
			reason = held + "; the authority carries its bytes through unchanged";
			return true;
		}

		private static bool SameBytes(byte[] a, byte[] b)
		{
			if (a == null || b == null || a.Length != b.Length) return false;
			for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
			return true;
		}
	}
}
