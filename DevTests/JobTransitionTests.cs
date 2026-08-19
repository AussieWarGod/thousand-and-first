#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Tests
{
	public class JobTransitionTests
	{
		// SemanticJobState is byte-backed, so its entire representable domain is 0..255 and a cast
		// from any wider integer wraps into it. Probing "-1" or int.MinValue therefore does not test
		// an out-of-domain value at all — int.MinValue wraps to 0, a perfectly valid Scheduled. The
		// only honest exhaustive probe is the byte domain itself.
		private const int MinProbe = 0;
		private const int MaxProbe = 255;

		/// <summary>
		/// The expected matrix, written out by hand from the card. Deriving it from the production
		/// table would prove only that the table agrees with itself.
		/// </summary>
		private static readonly string[] AllowedEdges =
		{
			"Scheduled>Due", "Scheduled>Cancelled",
			"Due>Prepared", "Due>Blocked", "Due>Cancelled",
			"Blocked>Prepared", "Blocked>Cancelled",
			"Prepared>Committed", "Prepared>Blocked", "Prepared>Cancelled",
			"Committed>Materialized", "Committed>Recoverable",
			"Recoverable>Materialized", "Recoverable>Compensated", "Recoverable>Archived",
			"Materialized>Notified",
			"Cancelled>Notified", "Cancelled>Archived",
			"Compensated>Notified", "Compensated>Archived",
			"Notified>Archived"
		};

		private static bool IsExpectedAllowed(SemanticJobState from, SemanticJobState to)
		{
			string probe = from.ToString() + ">" + to.ToString();
			for (int i = 0; i < AllowedEdges.Length; i++)
			{
				if (string.Equals(AllowedEdges[i], probe, StringComparison.Ordinal))
				{
					return true;
				}
			}
			return false;
		}

		private static bool IsKnownValue(int value)
		{
			return value >= 0 && value <= 10;
		}

		[Test]
		public void EveryNumericPairInTheWholeByteDomainMatchesTheHandWrittenMatrix()
		{
			for (int from = MinProbe; from <= MaxProbe; from++)
			{
				for (int to = MinProbe; to <= MaxProbe; to++)
				{
					JobTransitionVerdict actual = JobTransitions.Classify((SemanticJobState)from, (SemanticJobState)to);
					JobTransitionVerdict expected;
					if (!IsKnownValue(from) || !IsKnownValue(to))
					{
						expected = JobTransitionVerdict.Rejected;
					}
					else if (from == to)
					{
						expected = JobTransitionVerdict.Idempotent;
					}
					else
					{
						expected = IsExpectedAllowed((SemanticJobState)from, (SemanticJobState)to)
							? JobTransitionVerdict.Allowed
							: JobTransitionVerdict.Rejected;
					}
					Assert.AreEqual(expected, actual, "edge " + from + " -> " + to);
				}
			}
		}

		[Test]
		public void EveryValidSelfEdgeIsIdempotent()
		{
			for (int i = 0; i <= 10; i++)
			{
				Assert.AreEqual(JobTransitionVerdict.Idempotent, JobTransitions.Classify((SemanticJobState)i, (SemanticJobState)i), "state " + i);
			}
		}

		[Test]
		public void UnknownNumericStatesAreAlwaysRejectedAndNeverTerminal()
		{
			// Values that are genuinely unknown once narrowed to the underlying byte. Wider garbage
			// such as int.MinValue is deliberately absent: it wraps to 0, which is a real Scheduled,
			// so asserting it is unknown would assert something false about the type.
			int[] garbage = { 11, 12, 99, 128, 254, 255 };
			foreach (int value in garbage)
			{
				SemanticJobState state = (SemanticJobState)value;
				Assert.IsFalse(JobTransitions.IsTerminal(state), "an unrecognised value must not read as a finished job: " + value);
				Assert.AreEqual(JobTransitionVerdict.Rejected, JobTransitions.Classify(state, state), "self edge on garbage " + value);

				// Unknown on either side rejects, so no edge can be reached into or out of it.
				for (int other = 0; other <= 10; other++)
				{
					Assert.AreEqual(JobTransitionVerdict.Rejected, JobTransitions.Classify(state, (SemanticJobState)other),
						"garbage " + value + " -> " + other);
					Assert.AreEqual(JobTransitionVerdict.Rejected, JobTransitions.Classify((SemanticJobState)other, state),
						other + " -> garbage " + value);
				}
			}
		}

		[Test]
		public void OnlyArchivedIsTerminal()
		{
			// The whole byte domain, not just the valid range: a terminal test that reads true for
			// an unrecognised value would let a corrupt job be treated as finished business.
			for (int i = MinProbe; i <= MaxProbe; i++)
			{
				SemanticJobState state = (SemanticJobState)i;
				Assert.AreEqual(i == (int)SemanticJobState.Archived, JobTransitions.IsTerminal(state), "state " + i);
			}
			Assert.AreEqual(JobTransitionVerdict.Idempotent, JobTransitions.Classify(SemanticJobState.Archived, SemanticJobState.Archived));
			for (int i = 0; i <= 10; i++)
			{
				if (i == (int)SemanticJobState.Archived)
				{
					continue;
				}
				Assert.AreEqual(JobTransitionVerdict.Rejected, JobTransitions.Classify(SemanticJobState.Archived, (SemanticJobState)i), "Archived has no exit to " + (SemanticJobState)i);
			}
		}

		[Test]
		public void CommittedWorkCanNeverBeCancelledOrBlocked()
		{
			// Cancelled means nothing was committed. Committed work reaches an equivalent end only
			// through a successful compensation receipt.
			Assert.AreEqual(JobTransitionVerdict.Rejected, JobTransitions.Classify(SemanticJobState.Committed, SemanticJobState.Cancelled));
			Assert.AreEqual(JobTransitionVerdict.Rejected, JobTransitions.Classify(SemanticJobState.Committed, SemanticJobState.Blocked));
			Assert.AreEqual(JobTransitionVerdict.Allowed, JobTransitions.Classify(SemanticJobState.Committed, SemanticJobState.Recoverable));
			Assert.AreEqual(JobTransitionVerdict.Allowed, JobTransitions.Classify(SemanticJobState.Recoverable, SemanticJobState.Compensated));
		}

		/// <summary>
		/// The liveness property, not merely the edges. Every state must be able to reach a
		/// terminal state even when every optional transition fails — otherwise a job can sit in a
		/// non-terminal state forever and bounded retention holds it until the collection's own
		/// capacity fails, which is a corrupt-save path reached through valid play.
		/// </summary>
		[Test]
		public void EveryStateCanReachTerminalUnderAdversarialFailure()
		{
			for (int start = 0; start <= 10; start++)
			{
				SemanticJobState state = (SemanticJobState)start;
				HashSet<SemanticJobState> visited = new HashSet<SemanticJobState>();
				Queue<SemanticJobState> frontier = new Queue<SemanticJobState>();
				frontier.Enqueue(state);
				visited.Add(state);
				bool reachedTerminal = false;

				while (frontier.Count > 0)
				{
					SemanticJobState current = frontier.Dequeue();
					if (JobTransitions.IsTerminal(current))
					{
						reachedTerminal = true;
						break;
					}
					for (int next = 0; next <= 10; next++)
					{
						SemanticJobState candidate = (SemanticJobState)next;
						if (candidate == current || visited.Contains(candidate))
						{
							continue;
						}
						if (JobTransitions.Classify(current, candidate) == JobTransitionVerdict.Allowed)
						{
							visited.Add(candidate);
							frontier.Enqueue(candidate);
						}
					}
				}

				Assert.IsTrue(reachedTerminal, "no path from " + state + " reaches a terminal state");
			}
		}

		/// <summary>
		/// Liveness by hand-written witness rather than by search. The breadth-first version below
		/// proves the table is consistent with itself; this proves it against a path a person wrote
		/// down and can read. Critically, no witness for <c>Committed</c> or <c>Recoverable</c> is
		/// allowed to route through <c>Materialized</c> or <c>Compensated</c> — those are exactly
		/// the two exits that can be permanently unavailable at once, so a liveness argument that
		/// leans on them proves nothing about the case that matters.
		/// <para>
		/// The <c>Recoverable -> Archived</c> edge is receipt-gated in the design. This graph test
		/// does not and cannot show that the permanent-claim receipt was reserved before commit or
		/// written before the edge is taken; every transaction consumer must prove that separately.
		/// </para>
		/// </summary>
		[Test]
		public void EveryStateHasAHandWrittenWitnessPathToArchived()
		{
			SemanticJobState[][] witnesses =
			{
				new[] { SemanticJobState.Scheduled, SemanticJobState.Cancelled, SemanticJobState.Archived },
				new[] { SemanticJobState.Due, SemanticJobState.Cancelled, SemanticJobState.Archived },
				new[] { SemanticJobState.Prepared, SemanticJobState.Cancelled, SemanticJobState.Archived },
				new[] { SemanticJobState.Committed, SemanticJobState.Recoverable, SemanticJobState.Archived },
				new[] { SemanticJobState.Materialized, SemanticJobState.Notified, SemanticJobState.Archived },
				new[] { SemanticJobState.Notified, SemanticJobState.Archived },
				new[] { SemanticJobState.Archived },
				new[] { SemanticJobState.Blocked, SemanticJobState.Cancelled, SemanticJobState.Archived },
				new[] { SemanticJobState.Cancelled, SemanticJobState.Archived },
				new[] { SemanticJobState.Recoverable, SemanticJobState.Archived },
				new[] { SemanticJobState.Compensated, SemanticJobState.Archived }
			};

			Assert.AreEqual(11, witnesses.Length, "one witness per valid state");

			HashSet<SemanticJobState> covered = new HashSet<SemanticJobState>();
			foreach (SemanticJobState[] witness in witnesses)
			{
				SemanticJobState start = witness[0];
				covered.Add(start);

				bool unavailableRoutesBanned = start == SemanticJobState.Committed || start == SemanticJobState.Recoverable;
				for (int i = 1; i < witness.Length; i++)
				{
					if (unavailableRoutesBanned)
					{
						Assert.AreNotEqual(SemanticJobState.Materialized, witness[i],
							"the " + start + " witness must survive materialization being impossible");
						Assert.AreNotEqual(SemanticJobState.Compensated, witness[i],
							"the " + start + " witness must survive compensation being impossible");
					}
					Assert.AreEqual(JobTransitionVerdict.Allowed, JobTransitions.Classify(witness[i - 1], witness[i]),
						"witness edge " + witness[i - 1] + " -> " + witness[i]);
				}

				Assert.IsTrue(JobTransitions.IsTerminal(witness[witness.Length - 1]), "witness for " + start + " must end terminal");
				if (start == SemanticJobState.Archived)
				{
					Assert.AreEqual(1, witness.Length, "the terminal state's witness takes zero edges");
				}
			}

			for (int i = 0; i <= 10; i++)
			{
				Assert.IsTrue(covered.Contains((SemanticJobState)i), "no witness for " + (SemanticJobState)i);
			}
		}

		[Test]
		public void RecoverableHasAnEscapeThatDoesNotRequireMaterializationOrCompensation()
		{
			// The specific gap this closes: both ordinary exits can be permanently impossible at
			// once — the target gone with an uninstalled mod, and compensation unachievable
			// because the payer or sink no longer validates.
			Assert.AreEqual(JobTransitionVerdict.Allowed, JobTransitions.Classify(SemanticJobState.Recoverable, SemanticJobState.Archived));
			Assert.AreEqual(JobTransitionVerdict.Allowed, JobTransitions.Classify(SemanticJobState.Recoverable, SemanticJobState.Materialized));
			Assert.AreEqual(JobTransitionVerdict.Allowed, JobTransitions.Classify(SemanticJobState.Recoverable, SemanticJobState.Compensated));
			// It is an escape, not a general reopening.
			Assert.AreEqual(JobTransitionVerdict.Rejected, JobTransitions.Classify(SemanticJobState.Recoverable, SemanticJobState.Cancelled));
			Assert.AreEqual(JobTransitionVerdict.Rejected, JobTransitions.Classify(SemanticJobState.Recoverable, SemanticJobState.Blocked));
			Assert.AreEqual(JobTransitionVerdict.Rejected, JobTransitions.Classify(SemanticJobState.Recoverable, SemanticJobState.Prepared));
		}

		[Test]
		public void WalkTheNamedLifecyclePathsAndConfirmEveryStepRepeatsIdempotently()
		{
			SemanticJobState[][] paths =
			{
				new[] { SemanticJobState.Scheduled, SemanticJobState.Due, SemanticJobState.Prepared, SemanticJobState.Committed, SemanticJobState.Materialized, SemanticJobState.Notified, SemanticJobState.Archived },
				new[] { SemanticJobState.Scheduled, SemanticJobState.Due, SemanticJobState.Blocked, SemanticJobState.Prepared, SemanticJobState.Committed, SemanticJobState.Materialized, SemanticJobState.Notified, SemanticJobState.Archived },
				new[] { SemanticJobState.Scheduled, SemanticJobState.Due, SemanticJobState.Blocked, SemanticJobState.Cancelled, SemanticJobState.Archived },
				new[] { SemanticJobState.Scheduled, SemanticJobState.Due, SemanticJobState.Prepared, SemanticJobState.Committed, SemanticJobState.Recoverable, SemanticJobState.Materialized, SemanticJobState.Notified, SemanticJobState.Archived },
				new[] { SemanticJobState.Scheduled, SemanticJobState.Due, SemanticJobState.Prepared, SemanticJobState.Committed, SemanticJobState.Recoverable, SemanticJobState.Compensated, SemanticJobState.Notified, SemanticJobState.Archived },
				new[] { SemanticJobState.Scheduled, SemanticJobState.Due, SemanticJobState.Prepared, SemanticJobState.Committed, SemanticJobState.Recoverable, SemanticJobState.Archived }
			};

			foreach (SemanticJobState[] path in paths)
			{
				for (int i = 1; i < path.Length; i++)
				{
					Assert.AreEqual(JobTransitionVerdict.Allowed, JobTransitions.Classify(path[i - 1], path[i]), path[i - 1] + " -> " + path[i]);
					Assert.AreEqual(JobTransitionVerdict.Idempotent, JobTransitions.Classify(path[i], path[i]), "repeating " + path[i]);
				}
				Assert.IsTrue(JobTransitions.IsTerminal(path[path.Length - 1]), "path must end terminal");
			}
		}
	}
}
#endif
