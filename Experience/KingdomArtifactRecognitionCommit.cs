using System;

namespace ThousandAndFirst
{
	/// <summary>What a recognition attempt actually did to the save.</summary>
	public enum KingdomArtifactRecognitionOutcome
	{
		/// <summary>Nothing was offered because the save already held this exact recognition.</summary>
		AlreadyKept = 0,

		/// <summary>The authority took a new row and its revision moved exactly once.</summary>
		Recorded = 1
	}

	/// <summary>
	/// Planning and committing one explicit recognition. Planning spends nothing; committing spends
	/// exactly one section commit under exactly one lease.
	/// </summary>
	public static class KingdomArtifactRecognitionCommit
	{
		/// <summary>
		/// Builds the exact durable row against a private copy of the held authority, and proves it
		/// would survive being written before the founder is asked to agree to it.
		/// <para>
		/// Proving the plan by encoding it is the only honest check. A recognition whose text is one
		/// byte too wide for its own wire is a recognition that would be refused after the founder
		/// had already chosen; finding that out here costs nothing, and finding it out there costs
		/// the whole rite. Nothing in this method reaches the authority: the copy it works on is
		/// made from bytes, and the caller's authority is never handed to it.
		/// </para>
		/// </summary>
		public static bool TryPlan(KingdomCivicArtifactsEnvelope Held, string SettlementName,
			KingdomArtifactSnapshot Snapshot, KingdomArtifactRecognitionKind Kind,
			int AttributedResidentId, string AttributionName, long Tick,
			out KingdomArtifactRecognitionPlan Plan, out string Failure)
		{
			Plan = null;
			Failure = null;
			if (Held == null || Held.Recognitions == null || Held.Recognitions.Rows == null)
				return KingdomArtifactRecognitionLease.Fail("the realm's artifact authority is "
					+ "absent and cannot be planned against", out Failure);
			// A recognition the realm cannot place is not disclosed. The owning settlement's own
			// name is required here rather than defaulted, so no page can quietly fall back to the
			// seat's name or the realm's.
			if (string.IsNullOrWhiteSpace(SettlementName))
				return KingdomArtifactRecognitionLease.Fail("this ground's city has no name of its "
					+ "own to record a recognition under", out Failure);
			int retained = Held.Recognitions.Rows.Count;
			long before = Held.Recognitions.Revision;
			if (!TryProject(Held, Snapshot, Kind, AttributedResidentId, AttributionName, Tick,
				out KingdomCivicArtifactsEnvelope next,
				out KingdomArtifactRecognitionReceipt receipt, out _, out Failure))
				return false;
			Plan = new KingdomArtifactRecognitionPlan(receipt, retained,
				next.Recognitions.Revision == before, SettlementName);
			return true;
		}

		/// <summary>
		/// Records one recognition under the lease its authority was read from, and returns true
		/// only once civic memory has taken it.
		/// <para>
		/// This never opens a section. The transition is made on a private copy of the lease's own
		/// bytes and offered back under the very lease that produced them. An exact repeat commits
		/// nothing at all and spends no revision, because a founder who retried after an
		/// interruption must be able to finish without the register counting the same object twice;
		/// the same object under a different attribution is refused outright, and every row already
		/// kept survives either way.
		/// </para>
		/// </summary>
		public static bool TryCommitPlanned(IKingdomCivicMemoryAuthority Authority,
			KingdomCivicMemorySectionLease Lease, string ExactRealmId,
			KingdomArtifactSnapshot Snapshot, KingdomArtifactRecognitionKind Kind,
			int AttributedResidentId, string AttributionName, long Tick,
			out KingdomArtifactRecognitionReceipt Receipt,
			out KingdomArtifactRecognitionOutcome Outcome, out string Failure)
		{
			Receipt = null;
			Outcome = KingdomArtifactRecognitionOutcome.AlreadyKept;
			Failure = null;
			if (Authority == null)
				return KingdomArtifactRecognitionLease.Fail("there is no civic-memory authority to "
					+ "record this recognition with", out Failure);
			if (Lease == null)
				return KingdomArtifactRecognitionLease.Fail("there is no artifact-authority lease "
					+ "to record under", out Failure);
			if (Lease.SectionId != KingdomArtifactRecognitionLease.SectionId)
				return KingdomArtifactRecognitionLease.Fail("that lease does not name the realm's "
					+ "artifact section", out Failure);
			if (!KingdomArtifactRecognitionLease.TryInterpret(Lease.Payload(), ExactRealmId,
				out KingdomCivicArtifactsEnvelope held, out Failure)) return false;
			long before = held.Recognitions.Revision;
			if (!TryProject(held, Snapshot, Kind, AttributedResidentId, AttributionName, Tick,
				out KingdomCivicArtifactsEnvelope next, out Receipt, out byte[] bytes, out Failure))
			{
				Receipt = null;
				return false;
			}
			if (next.Recognitions.Revision == before)
			{
				// Nothing moved, so nothing is offered. Spending a revision to write back the bytes
				// already in the save would turn an idempotent retry into a change.
				Outcome = KingdomArtifactRecognitionOutcome.AlreadyKept;
				return true;
			}
			if (before == long.MaxValue || next.Recognitions.Revision != before + 1L)
			{
				Receipt = null;
				return KingdomArtifactRecognitionLease.Fail("the recognition revision did not "
					+ "advance exactly once", out Failure);
			}
			if (!Authority.TryCommitSection(Lease, bytes, out Failure))
			{
				Receipt = null;
				return false;
			}
			Outcome = KingdomArtifactRecognitionOutcome.Recorded;
			return true;
		}

		/// <summary>
		/// The whole transition on a private copy: never the caller's authority, never the save.
		/// <para>
		/// The copy is made by writing and reading the authority's own bytes, so a field that a
		/// later build adds without teaching the wire about it cannot survive this crossing. That
		/// is deliberate. A silent partial copy is how a register quietly loses a column.
		/// </para>
		/// </summary>
		private static bool TryProject(KingdomCivicArtifactsEnvelope Held,
			KingdomArtifactSnapshot Snapshot, KingdomArtifactRecognitionKind Kind,
			int AttributedResidentId, string AttributionName, long Tick,
			out KingdomCivicArtifactsEnvelope Next,
			out KingdomArtifactRecognitionReceipt Receipt, out byte[] Bytes, out string Failure)
		{
			Next = null;
			Receipt = null;
			Bytes = null;
			Failure = null;
			KingdomCivicArtifactsEnvelope candidate;
			try
			{
				candidate = KingdomCivicArtifactsStore.Copy(Held);
			}
			catch (Exception error) when (error is System.IO.InvalidDataException
				|| error is ArgumentException || error is NotSupportedException)
			{
				return KingdomArtifactRecognitionLease.Fail("the realm's artifact authority could "
					+ "not be copied (" + error.Message + ")", out Failure);
			}
			if (candidate == null || candidate.Recognitions == null)
				return KingdomArtifactRecognitionLease.Fail("the realm's artifact authority copied "
					+ "to nothing", out Failure);
			if (!KingdomArtifactRecognitionSelectionRuntime.TryPrepareRecognition(
				candidate.Recognitions, candidate.Recognitions.Revision, Snapshot, Kind,
				AttributedResidentId, AttributionName, Tick,
				out KingdomArtifactRecognitionBook book, out Receipt, out Failure))
			{
				Receipt = null;
				return false;
			}
			candidate.Recognitions = book;
			if (!KingdomCivicArtifactsStore.TryWrite(candidate, out Bytes, out Failure))
			{
				Receipt = null;
				Bytes = null;
				return false;
			}
			Next = candidate;
			return true;
		}
	}
}
