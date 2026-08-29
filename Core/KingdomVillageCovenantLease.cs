using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// The seam between the covenant archive and the civic-memory authority that keeps it, and the
	/// only place a covenant becomes durable.
	/// <para>
	/// <b>One lease means one lease.</b> A rite reads the archive, seals a covenant, and records
	/// it; if the section were opened a second time for that last step, everything decided during
	/// the first reading would have been decided about a save that may since have moved. So
	/// <see cref="TryReadArchive"/> is the only place in this file that opens section nine, it
	/// hands the lease back to its caller, and <see cref="TryCommitAppended"/> is given that same
	/// object rather than a fresh reading of the same bytes. The commit decodes
	/// <c>lease.Payload()</c> and nothing else.
	/// </para>
	/// <para>
	/// <see cref="TryConfirm"/> is deliberately separate and deliberately does open the section
	/// again. It runs after the commit has been accepted and the revision has moved, and its whole
	/// job is to ask the save &mdash; not the caller's memory of the save &mdash; whether the row
	/// is really there. Reading it under the spent lease would prove only that the bytes offered
	/// were the bytes offered.
	/// </para>
	/// </summary>
	public static class KingdomVillageCovenantLease
	{
		public const int SectionId = KingdomCivicMemoryLimits.SectionVillageCovenant;

		/// <summary>
		/// Whether this exact covenant could be recorded right now, asked before anything is spent.
		/// <para>
		/// This reads and never writes, and it asks three things rather than one. Is the archive
		/// available and readable for this realm; can it take one more row; and &mdash; the part
		/// that matters most &mdash; would <i>this</i> covenant, with these identities, actually
		/// encode. A faction whose display name is lawful to charter and one byte too wide to
		/// record would otherwise be discovered after the founder had paid, which is the exact
		/// stranded rite this cut exists to make impossible.
		/// </para>
		/// <para>
		/// The candidate's standing and reservation tick are not yet knowable when this runs, so it
		/// is handed a shaped stand-in for them. That is honest: those two are the only fields whose
		/// width is fixed by their type rather than by anything a founder can influence, so a
		/// candidate that encodes with a stand-in encodes with the real thing.
		/// </para>
		/// </summary>
		public static bool TryPreflight(IKingdomCivicMemoryAuthority authority,
			string exactRealmId, KingdomVillageCovenantReceipt candidate, out string failure)
		{
			if (!TryReadArchive(authority, exactRealmId, out _,
				out KingdomVillageCovenantArchive archive, out failure)) return false;
			if (archive.Rows.Count >= KingdomVillageCovenantArchive.MaxRows)
				return KingdomVillageCovenantRules.Fail("the realm's covenant archive is full at "
					+ KingdomVillageCovenantArchive.MaxRows + " covenants and cannot record another",
					out failure);
			if (candidate == null)
			{
				failure = "";
				return true;
			}
			if (!KingdomVillageCovenantRules.TryValidateRow(candidate, out failure)) return false;
			KingdomVillageCovenantArchive bound = archive.Copy();
			if (!KingdomVillageCovenantRules.TryBindEmptyIdentity(bound, exactRealmId, out failure))
				return false;
			if (!KingdomVillageCovenantRules.TryAppend(bound, candidate, exactRealmId,
				out KingdomVillageCovenantArchive trial, out _, out _, out failure)) return false;
			// Proving the archive by writing it is the only honest check: a covenant that cannot be
			// encoded is a covenant that would be lost, and finding that out here costs nothing
			// while finding it out after the debit costs the rite.
			return KingdomVillageCovenantCodec.TryEncode(trial, out _, out failure);
		}

		/// <summary>
		/// Opens section nine exactly once and hands back both the archive and the lease it came
		/// from. An absent section is a successful, explicit answer: the realm has simply never
		/// recorded a covenant, which is a different thing from an archive that would not read.
		/// </summary>
		public static bool TryReadArchive(IKingdomCivicMemoryAuthority authority,
			string exactRealmId, out KingdomCivicMemorySectionLease lease,
			out KingdomVillageCovenantArchive archive, out string failure)
		{
			lease = null;
			archive = null;
			if (authority == null)
				return KingdomVillageCovenantRules.Fail("there is no civic-memory authority to "
					+ "read the covenant archive from", out failure);
			if (!KingdomIdentityRules.IsRealmId(exactRealmId))
				return KingdomVillageCovenantRules.Fail("the covenant archive was asked for a "
					+ "realm whose id is not canonical", out failure);
			if (!authority.TryReadSection(SectionId, out lease, out failure)) return false;
			archive = Held(lease);
			if (archive.State != KingdomVillageCovenantState.Compatible)
				return KingdomVillageCovenantRules.Fail("the realm's covenant archive is "
					+ archive.State + " and cannot vouch for anything (" + archive.Fault + ")",
					out failure);
			if (!KingdomVillageCovenantRules.TryValidate(archive, out failure)) return false;
			if (archive.IdentityBound && !string.Equals(archive.RealmId, exactRealmId,
				StringComparison.Ordinal))
				return KingdomVillageCovenantRules.Fail("the covenant archive in this save belongs "
					+ "to another realm", out failure);
			failure = "";
			return true;
		}

		/// <summary>
		/// Records one covenant under the lease its archive was read from, and returns true only
		/// once the authority has taken it.
		/// <para>
		/// This never opens a section. The transition is made on a private decode of the lease's
		/// own bytes, and offered back under the very lease that produced them. An exact replay of
		/// the same founding transaction commits nothing at all and spends no revision, because a
		/// rite retried after a crash must be able to finish without the archive counting it twice;
		/// a different covenant wearing the same transaction is refused, and every covenant already
		/// recorded is kept.
		/// </para>
		/// </summary>
		public static bool TryCommitAppended(IKingdomCivicMemoryAuthority authority,
			KingdomCivicMemorySectionLease lease, string exactRealmId,
			KingdomVillageCovenantReceipt receipt, out KingdomVillageCovenantAppend outcome,
			out KingdomVillageCovenantReceipt effective, out string failure)
		{
			outcome = KingdomVillageCovenantAppend.AlreadyRecorded;
			effective = null;
			if (authority == null)
				return KingdomVillageCovenantRules.Fail("there is no civic-memory authority to "
					+ "record this covenant with", out failure);
			if (lease == null)
				return KingdomVillageCovenantRules.Fail("there is no covenant-archive lease to "
					+ "record under", out failure);
			if (!KingdomIdentityRules.IsRealmId(exactRealmId))
				return KingdomVillageCovenantRules.Fail("this covenant names a realm whose id is "
					+ "not canonical", out failure);
			KingdomVillageCovenantArchive held = Held(lease);
			if (held.State != KingdomVillageCovenantState.Compatible)
				return KingdomVillageCovenantRules.Fail("the leased covenant archive is "
					+ held.State + " and must not be written over (" + held.Fault + ")",
					out failure);
			if (!KingdomVillageCovenantRules.TryBindEmptyIdentity(held, exactRealmId, out failure))
				return false;
			if (!KingdomVillageCovenantRules.TryAppend(held, receipt, exactRealmId,
				out KingdomVillageCovenantArchive next, out outcome, out effective, out failure))
				return false;
			if (outcome == KingdomVillageCovenantAppend.AlreadyRecorded
				&& ReferenceEquals(next, held) && lease.Present)
			{
				// Nothing moved, so nothing is offered. Spending a revision to write back the
				// bytes already in the save would turn an idempotent retry into a change.
				failure = "";
				return true;
			}
			if (!KingdomVillageCovenantCodec.TryEncode(next, out byte[] bytes, out failure))
				return false;
			return authority.TryCommitSection(lease, bytes, out failure);
		}

		/// <summary>
		/// Asks the save itself whether one covenant is durably recorded, opening the section
		/// afresh so the answer is about the save rather than about what was offered to it.
		/// </summary>
		public static bool TryConfirm(IKingdomCivicMemoryAuthority authority, string exactRealmId,
			KingdomVillageCovenantReceipt receipt, out string failure)
		{
			if (receipt == null)
				return KingdomVillageCovenantRules.Fail("there is no covenant to confirm",
					out failure);
			if (!TryReadArchive(authority, exactRealmId, out _,
				out KingdomVillageCovenantArchive archive, out failure)) return false;
			if (!archive.IdentityBound || !string.Equals(archive.RealmId, exactRealmId,
				StringComparison.Ordinal))
				return KingdomVillageCovenantRules.Fail("the covenant archive is not bound to this "
					+ "realm and cannot confirm anything", out failure);
			for (int i = 0; i < archive.Rows.Count; i++)
				if (KingdomVillageCovenantRules.Same(archive.Rows[i], receipt))
				{
					failure = "";
					return true;
				}
			return KingdomVillageCovenantRules.Fail("the save holds no covenant matching this "
				+ "exact founding transaction", out failure);
		}

		/// <summary>
		/// The archive as this lease's own payload says it stands.
		/// <para>
		/// An absent section is not a decode failure. The realm has recorded nothing, and the
		/// honest reading of that is a fresh, unbound, empty archive &mdash; not a quarantine, and
		/// not a bound archive pretending a realm once wrote an empty one.
		/// </para>
		/// </summary>
		private static KingdomVillageCovenantArchive Held(KingdomCivicMemorySectionLease lease)
		{
			byte[] payload = lease.Payload();
			if (!lease.Present || payload.Length == 0)
				return new KingdomVillageCovenantArchive();
			return KingdomVillageCovenantCodec.Decode(payload);
		}
	}
}
