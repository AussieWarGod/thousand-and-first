using System;

namespace ThousandAndFirst.Simulation.Kernel
{
	internal enum SemanticJobState : byte
	{
		Scheduled = 0,
		Due = 1,
		Prepared = 2,
		Committed = 3,
		Materialized = 4,
		Notified = 5,
		Archived = 6,
		Blocked = 7,
		Cancelled = 8,
		Recoverable = 9,
		Compensated = 10
	}

	internal enum JobTransitionVerdict : byte
	{
		Rejected = 0,
		Idempotent = 1,
		Allowed = 2
	}

	/// <summary>
	/// The generic semantic-job lifecycle, as graph shape only.
	/// <para>
	/// This table classifies state edges and nothing else. Later job rules must additionally
	/// validate event ID, payload, payment and reservation, and receipt preconditions before
	/// applying an edge this classifies as <see cref="JobTransitionVerdict.Allowed"/> — an allowed
	/// shape is permission to ask, not permission to act.
	/// </para>
	/// </summary>
	internal static class JobTransitions
	{
		/// <summary>
		/// Classifies one requested edge.
		/// <para>
		/// Every valid same-state request is idempotent, so a retried step is safe. Every unlisted
		/// edge and every unknown numeric value is rejected — an unrecognised state fails closed
		/// rather than falling through to a default that would grant a transition nobody defined.
		/// </para>
		/// <para>
		/// Note that <c>Committed</c> reaches neither <c>Blocked</c> nor <c>Cancelled</c>:
		/// cancellation means nothing was committed, so committed work can only reach an
		/// equivalent end through a successful compensation receipt. A physical projection that
		/// fails after commit is <c>Recoverable</c>, never <c>Blocked</c>.
		/// </para>
		/// </summary>
		internal static JobTransitionVerdict Classify(SemanticJobState from, SemanticJobState to)
		{
			if (!IsKnown(from) || !IsKnown(to))
			{
				return JobTransitionVerdict.Rejected;
			}
			if (from == to)
			{
				return JobTransitionVerdict.Idempotent;
			}

			switch (from)
			{
			case SemanticJobState.Scheduled:
				return Allow(to == SemanticJobState.Due || to == SemanticJobState.Cancelled);
			case SemanticJobState.Due:
				return Allow(to == SemanticJobState.Prepared || to == SemanticJobState.Blocked || to == SemanticJobState.Cancelled);
			case SemanticJobState.Blocked:
				return Allow(to == SemanticJobState.Prepared || to == SemanticJobState.Cancelled);
			case SemanticJobState.Prepared:
				return Allow(to == SemanticJobState.Committed || to == SemanticJobState.Blocked || to == SemanticJobState.Cancelled);
			case SemanticJobState.Committed:
				return Allow(to == SemanticJobState.Materialized || to == SemanticJobState.Recoverable);
			case SemanticJobState.Recoverable:
				// Archived here is the liveness escape: without it a job whose materialization and
				// compensation are both permanently impossible could never leave this state, and
				// bounded retention would hold it until the collection's own capacity failed.
				// It is receipt-gated at a higher layer, not by this table.
				return Allow(to == SemanticJobState.Materialized
					|| to == SemanticJobState.Compensated
					|| to == SemanticJobState.Archived);
			case SemanticJobState.Materialized:
				return Allow(to == SemanticJobState.Notified);
			case SemanticJobState.Cancelled:
				return Allow(to == SemanticJobState.Notified || to == SemanticJobState.Archived);
			case SemanticJobState.Compensated:
				return Allow(to == SemanticJobState.Notified || to == SemanticJobState.Archived);
			case SemanticJobState.Notified:
				return Allow(to == SemanticJobState.Archived);
			case SemanticJobState.Archived:
				// Terminal. The same-state case above already answered Idempotent.
				return JobTransitionVerdict.Rejected;
			default:
				return JobTransitionVerdict.Rejected;
			}
		}

		/// <summary>
		/// True only for a valid <see cref="SemanticJobState.Archived"/>. Every other valid state
		/// and every unknown numeric value is non-terminal, so retention logic never mistakes a
		/// corrupt value for a finished job.
		/// <para>
		/// Archival ends the executable job, not the debt: a permanent unresolved claim recorded
		/// on the way out lives under its own lifecycle and is never deleted or summarized away by
		/// job compaction.
		/// </para>
		/// </summary>
		internal static bool IsTerminal(SemanticJobState state)
		{
			return IsKnown(state) && state == SemanticJobState.Archived;
		}

		/// <summary>
		/// Private on purpose. The observable contract is <see cref="Classify"/> and
		/// <see cref="IsTerminal"/>; an unrecognised value is already rejected by the first and
		/// reported non-terminal by the second, so exposing the predicate would add a second way
		/// to ask the same question and a second thing a caller could branch on.
		/// </summary>
		private static bool IsKnown(SemanticJobState state)
		{
			switch (state)
			{
			case SemanticJobState.Scheduled:
			case SemanticJobState.Due:
			case SemanticJobState.Prepared:
			case SemanticJobState.Committed:
			case SemanticJobState.Materialized:
			case SemanticJobState.Notified:
			case SemanticJobState.Archived:
			case SemanticJobState.Blocked:
			case SemanticJobState.Cancelled:
			case SemanticJobState.Recoverable:
			case SemanticJobState.Compensated:
				return true;
			default:
				return false;
			}
		}

		private static JobTransitionVerdict Allow(bool allowed)
		{
			return allowed ? JobTransitionVerdict.Allowed : JobTransitionVerdict.Rejected;
		}
	}
}
