using System;

namespace ThousandAndFirst
{
	/// <summary>What one append attempt did to the archive, and why.</summary>
	public enum KingdomVillageCovenantAppend : byte
	{
		/// <summary>The covenant was new. A row was added and the revision advanced.</summary>
		Recorded = 0,

		/// <summary>This exact covenant was already recorded. Nothing changed, including the
		/// revision: a retry of the same transaction must cost the archive nothing.</summary>
		AlreadyRecorded = 1
	}

	public static partial class KingdomVillageCovenantRules
	{
		/// <summary>Whether a whole archive is one this build may read, write, or merely carry.</summary>
		public static bool TryValidate(KingdomVillageCovenantArchive archive, out string failure)
		{
			failure = null;
			if (archive == null) return Fail("the covenant archive is absent", out failure);
			if (!Defined(archive.State))
				return Fail("the covenant archive reports state " + (int)archive.State
					+ ", which this build does not define", out failure);
			if (archive.State != KingdomVillageCovenantState.Compatible)
				return ValidateOpaque(archive, out failure);
			if (archive.OpaqueVersion != 0 || archive.OpaquePayload != null)
				return Fail("a readable covenant archive is carrying opaque bytes as well",
					out failure);
			// The revision is canonical rather than free: an append-only history that only ever
			// grows by one row has exactly one honest counter, and letting it be anything else
			// would give one set of covenants several lawful spellings on the wire and let a
			// forged archive claim an exhausted counter it could never have reached.
			if (archive.Revision != archive.Rows.Count)
				return Fail("the covenant archive holds " + archive.Rows.Count
					+ " covenants and calls itself revision " + archive.Revision
					+ "; an append-only history has one counter and it is its own length",
					out failure);
			if (archive.Rows.Count > KingdomVillageCovenantArchive.MaxRows)
				return Fail("the covenant archive holds " + archive.Rows.Count
					+ " covenants, past the " + KingdomVillageCovenantArchive.MaxRows
					+ " it reserves room for", out failure);
			if (!ValidateIdentity(archive, out failure)) return false;
			return ValidateRows(archive, out failure);
		}

		/// <summary>
		/// Rows arrive in one order and one only: ascending by receipt id, no duplicates, and no
		/// two covenants from one founding transaction.
		/// <para>
		/// The order is not tidiness. A receipt id is derived from the covenant, so sorting by it
		/// gives every archive holding the same covenants the same bytes regardless of the order
		/// they were sealed in &mdash; which is what lets an exact replay be recognised as a replay
		/// instead of appearing as a second, differently-arranged archive.
		/// </para>
		/// </summary>
		private static bool ValidateRows(KingdomVillageCovenantArchive archive, out string failure)
		{
			for (int i = 0; i < archive.Rows.Count; i++)
			{
				if (!TryValidateRow(archive.Rows[i], out failure)) return false;
				if (!string.Equals(archive.Rows[i].RealmId, archive.RealmId,
					StringComparison.Ordinal))
					return Fail("a covenant in this archive names another realm than the archive "
						+ "holding it", out failure);
				if (i == 0) continue;
				int order = string.CompareOrdinal(archive.Rows[i - 1].ReceiptId,
					archive.Rows[i].ReceiptId);
				if (order == 0)
					return Fail("the covenant archive holds the same receipt twice", out failure);
				if (order > 0)
					return Fail("the covenant archive's receipts are out of their canonical order",
						out failure);
			}
			for (int i = 0; i < archive.Rows.Count; i++)
				for (int j = i + 1; j < archive.Rows.Count; j++)
					if (string.Equals(archive.Rows[i].TransactionId,
						archive.Rows[j].TransactionId, StringComparison.Ordinal))
						return Fail("two covenants in the archive claim one founding transaction",
							out failure);
			failure = "";
			return true;
		}

		/// <summary>
		/// The binding is taken on an empty archive and never again.
		/// <para>
		/// An archive that already holds covenants cannot be adopted by a realm it was not written
		/// for. There would be no honest way to tell a save that inherited these records from one
		/// that took them, and the difference matters more than the convenience.
		/// </para>
		/// </summary>
		private static bool ValidateIdentity(KingdomVillageCovenantArchive archive,
			out string failure)
		{
			if (!archive.IdentityBound)
			{
				if (archive.RealmId != null)
					return Fail("an unbound covenant archive already names a realm", out failure);
				if (!archive.IsEmpty)
					return Fail("an unbound covenant archive is carrying covenants", out failure);
				failure = "";
				return true;
			}
			if (!KingdomIdentityRules.IsRealmId(archive.RealmId))
				return Fail("the covenant archive's realm id is not canonical", out failure);
			failure = "";
			return true;
		}

		/// <summary>
		/// An archive this build cannot read holds nothing this build can read.
		/// <para>
		/// The two unreadable states part company on their bytes, and deliberately. A future
		/// archive must have kept its own, because keeping them whole is the entire promise being
		/// made to the build that wrote them. A quarantined one may have none: bytes that arrived
		/// absent, or so far over the cap that copying them was itself the wrong answer, leave
		/// nothing to keep, and pretending otherwise would be inventing evidence about a failure.
		/// </para>
		/// </summary>
		private static bool ValidateOpaque(KingdomVillageCovenantArchive archive,
			out string failure)
		{
			if (archive.Rows.Count != 0 || archive.Revision != 0L || archive.IdentityBound
				|| archive.RealmId != null)
				return Fail("an unreadable covenant archive is also claiming readable content",
					out failure);
			if (string.IsNullOrEmpty(archive.Fault))
				return Fail("an unreadable covenant archive gives no reason for being unreadable",
					out failure);
			if (archive.State == KingdomVillageCovenantState.FutureOpaque)
			{
				if (archive.OpaqueVersion <= KingdomVillageCovenantCodec.CurrentWireVersion)
					return Fail("a future covenant archive declares revision "
						+ archive.OpaqueVersion + ", which is not later than this build's",
						out failure);
				if (archive.OpaquePayload == null || archive.OpaquePayload.Length == 0)
					return Fail("a future covenant archive kept none of the bytes it promised to "
						+ "carry", out failure);
				failure = "";
				return true;
			}
			if (archive.OpaqueVersion != 0)
				return Fail("a quarantined covenant archive is also claiming a readable revision",
					out failure);
			failure = "";
			return true;
		}

		/// <summary>Binds an empty archive to one realm, or confirms the realm it already names.</summary>
		public static bool TryBindEmptyIdentity(KingdomVillageCovenantArchive archive,
			string exactRealmId, out string failure)
		{
			if (!TryValidate(archive, out failure)) return false;
			if (archive.State != KingdomVillageCovenantState.Compatible)
				return Fail("an unreadable covenant archive cannot be bound to a realm", out failure);
			if (!KingdomIdentityRules.IsRealmId(exactRealmId))
				return Fail("the covenant archive was offered a realm id that is not canonical",
					out failure);
			if (archive.IdentityBound)
				return string.Equals(archive.RealmId, exactRealmId, StringComparison.Ordinal)
					|| Fail("the covenant archive belongs to another realm", out failure);
			archive.RealmId = exactRealmId;
			archive.IdentityBound = true;
			failure = "";
			return true;
		}

		/// <summary>
		/// Records one completed covenant, or recognises that it is already recorded.
		/// <para>
		/// The answers are deliberately distinct. A new covenant is added and the revision advances.
		/// A covenant already on record changes nothing at all, revision included, because a rite
		/// retried after a crash must be able to finish without the archive counting it twice.
		/// Anything else is a refusal, and every covenant already recorded is kept.
		/// </para>
		/// <para>
		/// <b>What "already recorded" means is the whole of the recovery contract.</b> A retry
		/// arrives with a candidate this build has just rebuilt, and two of that candidate's fields
		/// are read from a world that has kept moving: the standing the covenant was sealed at, and
		/// the tick its site reservation was taken at. If a callback nudged that standing between
		/// the archive commit and the basin finishing, a retry that insisted on its own recomputed
		/// values would present a <i>different</i> covenant for the same founding transaction, be
		/// told it was a conflict, and strand a rite the founder had already paid for. So the match
		/// is made on the facts that cannot move, and what the archive already holds is what the
		/// covenant is. History is not rewritten from today's ledger; it is re-proved against it.
		/// </para>
		/// </summary>
		/// <param name="archive">Read, never written. The result is a separate object.</param>
		/// <param name="next">The archive to commit, which is <paramref name="archive"/> itself
		/// when nothing changed.</param>
		/// <param name="effective">The covenant that now stands: the candidate when it was newly
		/// recorded, and the archived row when it was already there. A caller confirming its work
		/// must confirm this one, not the one it built.</param>
		public static bool TryAppend(KingdomVillageCovenantArchive archive,
			KingdomVillageCovenantReceipt row, string exactRealmId,
			out KingdomVillageCovenantArchive next, out KingdomVillageCovenantAppend outcome,
			out KingdomVillageCovenantReceipt effective, out string failure)
		{
			next = null;
			effective = null;
			outcome = KingdomVillageCovenantAppend.AlreadyRecorded;
			if (!TryValidate(archive, out failure)) return false;
			if (archive.State != KingdomVillageCovenantState.Compatible)
				return Fail("the covenant archive is " + archive.State
					+ " and no covenant may be added to it", out failure);
			if (!archive.IdentityBound || !KingdomIdentityRules.IsRealmId(exactRealmId)
				|| !string.Equals(archive.RealmId, exactRealmId, StringComparison.Ordinal))
				return Fail("the covenant archive is not bound to this exact realm", out failure);
			if (row == null) return Fail("there is no covenant to record", out failure);
			KingdomVillageCovenantReceipt candidate = row.Copy();
			if (!TryValidateRow(candidate, out failure)) return false;
			if (!string.Equals(candidate.RealmId, exactRealmId, StringComparison.Ordinal))
				return Fail("this covenant names another realm than the archive recording it",
					out failure);

			for (int i = 0; i < archive.Rows.Count; i++)
			{
				KingdomVillageCovenantReceipt held = archive.Rows[i];
				bool sameReceipt = string.Equals(held.ReceiptId, candidate.ReceiptId,
					StringComparison.Ordinal);
				bool sameTransaction = string.Equals(held.TransactionId, candidate.TransactionId,
					StringComparison.Ordinal);
				if (!sameReceipt && !sameTransaction) continue;
				if (sameTransaction && SameFrozenFacts(held, candidate))
				{
					next = archive;
					effective = held.Copy();
					outcome = KingdomVillageCovenantAppend.AlreadyRecorded;
					failure = "";
					return true;
				}
				return Fail("the covenant archive already holds a different covenant for this "
					+ "founding transaction; it is kept rather than replaced", out failure);
			}

			if (archive.Rows.Count >= KingdomVillageCovenantArchive.MaxRows)
				return Fail("the covenant archive is full at "
					+ KingdomVillageCovenantArchive.MaxRows
					+ " covenants; this one is refused and every earlier one is kept", out failure);

			KingdomVillageCovenantArchive grown = archive.Copy();
			int at = grown.Rows.Count;
			for (int i = 0; i < grown.Rows.Count; i++)
				if (string.CompareOrdinal(candidate.ReceiptId, grown.Rows[i].ReceiptId) < 0)
				{
					at = i;
					break;
				}
			grown.Rows.Insert(at, candidate);
			grown.Revision = grown.Rows.Count;
			if (!TryValidate(grown, out failure)) return false;
			next = grown;
			effective = candidate.Copy();
			outcome = KingdomVillageCovenantAppend.Recorded;
			failure = "";
			return true;
		}
	}
}
