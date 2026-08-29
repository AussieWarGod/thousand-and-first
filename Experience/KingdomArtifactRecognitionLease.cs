using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// The seam between one explicit recognition and the civic-memory authority that keeps it, and
	/// the only place a recognition becomes durable.
	/// <para>
	/// <b>One lease means one lease.</b> <see cref="TryReadAuthority"/> is the only call in the
	/// commit path that opens section one, it hands the lease back to its caller, and
	/// <see cref="KingdomArtifactRecognitionCommit.TryCommitPlanned"/> is given that same object
	/// rather than a fresh reading of the same bytes. Everything the founder was shown was decided
	/// about the payload that lease carries; opening the section again to write would mean writing
	/// against a save that may since have moved.
	/// </para>
	/// <para>
	/// <see cref="TryReadBack"/> is deliberately separate and deliberately does open the section
	/// again. It runs after a commit has been accepted, and its whole job is to ask the save
	/// &mdash; not the caller's memory of the save &mdash; whether the row is really there. That is
	/// also the only authority D6 recognises: the committed receipt. Nothing is ever inferred from
	/// what the founder is carrying, what is standing nearby, or what the original later becomes.
	/// </para>
	/// </summary>
	public static class KingdomArtifactRecognitionLease
	{
		public const int SectionId = KingdomCivicMemoryLimits.SectionCivicArtifacts;

		/// <summary>
		/// Opens section one exactly once and hands back both the realm's artifact authority and
		/// the lease it came from.
		/// <para>
		/// An absent section is a successful, explicit answer: this realm has simply never recorded
		/// an artifact, which is a different thing from an authority that would not read. A
		/// quarantined or newer-than-this-build authority is neither, and is refused rather than
		/// carried into a transition that would write over evidence.
		/// </para>
		/// </summary>
		public static bool TryReadAuthority(IKingdomCivicMemoryAuthority Authority,
			string ExactRealmId, out KingdomCivicMemorySectionLease Lease,
			out KingdomCivicArtifactsEnvelope Held, out string Failure)
		{
			Lease = null;
			Held = null;
			Failure = null;
			if (Authority == null)
				return Fail("there is no civic-memory authority to read recognitions from",
					out Failure);
			if (!KingdomIdentityRules.IsRealmId(ExactRealmId))
				return Fail("the recognition authority was asked for a realm whose id is not "
					+ "canonical", out Failure);
			if (!Authority.TryReadSection(SectionId, out Lease, out Failure)) return false;
			if (Lease == null || Lease.SectionId != SectionId)
			{
				Lease = null;
				return Fail("civic memory returned the wrong section lease for recognitions",
					out Failure);
			}
			if (TryInterpret(Lease.Payload(), ExactRealmId, out Held, out Failure)) return true;
			Lease = null;
			Held = null;
			return false;
		}

		/// <summary>
		/// What a lease's own bytes say this realm's artifact authority holds.
		/// <para>
		/// Absent and empty are the same lawful answer, and both produce a fresh authority bound to
		/// this realm. Every other disagreement &mdash; unreadable bytes, a quarantine, a payload a
		/// newer build wrote, or an authority belonging to another realm &mdash; is a refusal.
		/// A refusal keeps the evidence; it never becomes an empty book that could be written back.
		/// </para>
		/// </summary>
		public static bool TryInterpret(byte[] Payload, string ExactRealmId,
			out KingdomCivicArtifactsEnvelope Held, out string Failure)
		{
			Held = null;
			Failure = null;
			if (!KingdomIdentityRules.IsRealmId(ExactRealmId))
				return Fail("the recognition authority was asked for a realm whose id is not "
					+ "canonical", out Failure);
			KingdomCivicArtifactsEnvelope held = KingdomCivicArtifactsStore.ReadForRealm(
				Payload == null || Payload.Length == 0 ? null : Payload, ExactRealmId,
				out string readFailure);
			if (held == null)
				return Fail(readFailure ?? "the realm's artifact authority is absent after its "
					+ "section read", out Failure);
			if (held.IsOpaqueFuture)
				return Fail("the realm's artifact authority was written by a newer build and is "
					+ "carried, never edited", out Failure);
			// Belonging is asked before refusal, and deliberately. Another realm's authority is
			// refused for a reason the founder can act on; saying only that something was
			// unreadable would send them looking for corruption that is not there.
			if (held.IdentityBound && !string.Equals(held.RealmId, ExactRealmId,
				StringComparison.Ordinal))
				return Fail("the artifact authority in this save belongs to another realm",
					out Failure);
			if (held.Quarantined || !string.IsNullOrEmpty(readFailure))
				return Fail(readFailure ?? held.Fault ?? "the realm's artifact authority is held "
					+ "as evidence and cannot be written over", out Failure);
			if (!held.IdentityBound)
				return Fail("the artifact authority in this save is bound to no realm at all",
					out Failure);
			if (!KingdomCivicArtifactsStore.TryValidateIdentity(held, out string identityFailure))
				return Fail(identityFailure ?? "the realm's artifact authority is invalid",
					out Failure);
			Held = held;
			return true;
		}

		/// <summary>
		/// Asks the save itself what this realm has recognized, opening the section afresh so the
		/// answer is about the save rather than about what was offered to it.
		/// </summary>
		public static bool TryReadBack(IKingdomCivicMemoryAuthority Authority, string ExactRealmId,
			out KingdomCivicArtifactsEnvelope Held, out string Failure)
		{
			return TryReadAuthority(Authority, ExactRealmId, out _, out Held, out Failure);
		}

		/// <summary>
		/// Whether one exact recognition is durably in the save, named by its own id.
		/// <para>
		/// This is the whole of D6's unlock rule. A later move, sale, or destruction of the
		/// original cannot reach this answer, because this answer never looks at the original.
		/// </para>
		/// </summary>
		public static bool TryReadBackRow(IKingdomCivicMemoryAuthority Authority,
			string ExactRealmId, string RecognitionId,
			out KingdomArtifactRecognitionReceipt Receipt, out string Failure)
		{
			Receipt = null;
			if (string.IsNullOrEmpty(RecognitionId))
				return Fail("there is no recognition id to confirm", out Failure);
			if (!TryReadBack(Authority, ExactRealmId, out KingdomCivicArtifactsEnvelope held,
				out Failure)) return false;
			for (int i = 0; i < held.Recognitions.Rows.Count; i++)
				if (string.Equals(held.Recognitions.Rows[i].RecognitionId, RecognitionId,
					StringComparison.Ordinal))
				{
					Receipt = held.Recognitions.Rows[i];
					return true;
				}
			return Fail("the save holds no recognition with that exact id", out Failure);
		}

		internal static bool Fail(string Text, out string Failure)
		{
			Failure = Text;
			return false;
		}
	}
}
