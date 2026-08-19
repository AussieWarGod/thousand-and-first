#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The properties that make the substrate trustworthy: however a run of time is chopped into
	/// wakes, the result must be identical. That is the whole claim the architecture rests on —
	/// that a sparse observer and a dense one see the same settlement.
	/// </summary>
	public class KernelPropertyTests
	{
		private const string Settlement = "taf:settlement:test";

		/// <summary>
		/// Deterministic, seeded, and local to the test: never the engine RNG, and never
		/// <c>Random</c> without a fixed seed, or a failure would not reproduce.
		/// </summary>
		private sealed class Lcg
		{
			private ulong _state;

			internal Lcg(ulong seed)
			{
				_state = seed | 1uL;
			}

			internal ulong Next()
			{
				_state = unchecked((_state * 6364136223846793005uL) + 1442695040888963407uL);
				ulong x = _state;
				x ^= x >> 33;
				return x;
			}

			internal int NextInt(int exclusiveMax)
			{
				return (int)(Next() % (ulong)exclusiveMax);
			}

			internal bool NextBool()
			{
				return (Next() & 1uL) == 1uL;
			}
		}

		private static string Encode(FixedPeriodToyState state)
		{
			byte[] bytes;
			KernelFaultCode fault;
			Assert.IsTrue(FixedPeriodToyRules.TryEncodeCanonical(state, out bytes, out fault), "encode fault " + fault);
			return KernelDigest.ToLowercaseHex(bytes);
		}

		[Test]
		public void DirectAdvanceEqualsArbitraryPartitionedAdvanceOverTenThousandCases()
		{
			Lcg rng = new Lcg(0x5EEDuL);
			int cases = 0;

			for (int i = 0; i < 10000; i++)
			{
				long interval = 1L + rng.NextInt(23);
				bool startEnabled = rng.NextBool();
				long end = 1L + rng.NextInt(400);

				ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, interval, startEnabled);
				Assert.IsTrue(created.Succeeded);

				// Direct: one wake straight to the end.
				ToyAdvanceResult direct = FixedPeriodToyRules.AdvanceThrough(created.State, end, startEnabled);
				Assert.IsTrue(direct.Succeeded, "direct advance");

				// Partitioned: an arbitrary set of intermediate wakes, same option throughout, so
				// the input history is identical and only the wake partition differs. The cut set
				// deliberately mixes the four shapes that break naive implementations — duplicates,
				// wakes landing exactly on a deadline, zero-length spans, and long jumps.
				List<long> cuts = new List<long>();
				int cutCount = rng.NextInt(6);
				for (int c = 0; c < cutCount; c++)
				{
					long cut = 1L + rng.NextInt((int)end);
					switch (rng.NextInt(4))
					{
					case 0:
						// Snap onto a deadline: the boundary case.
						cut = ((cut + interval - 1L) / interval) * interval;
						if (cut < 1L) { cut = 1L; }
						if (cut > end) { cut = end; }
						break;
					case 1:
						// A duplicate of a cut already present, producing a zero-length span.
						if (cuts.Count > 0) { cut = cuts[rng.NextInt(cuts.Count)]; }
						break;
					case 2:
						// A long jump straight to the far end.
						cut = end;
						break;
					}
					cuts.Add(cut);
					// And sometimes ask the very same tick twice in a row.
					if (rng.NextInt(4) == 0)
					{
						cuts.Add(cut);
					}
				}
				cuts.Sort();

				FixedPeriodToyState walked = created.State;
				long last = 0L;
				foreach (long cut in cuts)
				{
					// A repeated or non-advancing wake is a legal thing to ask for, not something
					// to filter out: it must succeed and change nothing.
					if (cut < last)
					{
						continue;
					}
					string beforeRepeat = cut == last ? Encode(walked) : null;
					ToyAdvanceResult step = FixedPeriodToyRules.AdvanceThrough(walked, cut, startEnabled);
					Assert.IsTrue(step.Succeeded, "partition step at " + cut);
					walked = step.State;
					if (beforeRepeat != null)
					{
						Assert.AreEqual(beforeRepeat, Encode(walked), "a zero-length span must change nothing at tick " + cut);
					}
					last = cut;
				}
				ToyAdvanceResult final = FixedPeriodToyRules.AdvanceThrough(walked, end, startEnabled);
				Assert.IsTrue(final.Succeeded, "partition final");

				if (!string.Equals(Encode(direct.State), Encode(final.State), StringComparison.Ordinal))
				{
					Assert.Fail("partition mismatch at case " + i + ": interval " + interval + ", end " + end + ", enabled " + startEnabled);
				}
				cases++;
			}

			Assert.AreEqual(10000, cases);
		}

		/// <summary>
		/// The same claim as above, but over a running history of settings changes rather than a
		/// single one. Moving a change to a different tick is a different input history, not a wake
		/// partition, so every change tick is held exactly fixed and only the wakes around them
		/// vary — including duplicates, and including changes that land exactly on a deadline.
		/// <para>
		/// External load observations are deliberately absent: those are a separate input class
		/// with their own tests, and mixing them in here would blur what this property proves.
		/// </para>
		/// </summary>
		[Test]
		public void EveryTransitionHistorySurvivesArbitraryExtraWakesAcrossTenThousandSeeds()
		{
			Lcg rng = new Lcg(0xC0FFEEuL);
			int cases = 0;
			int deadlineCoincidences = 0;

			for (int seed = 0; seed < 10000; seed++)
			{
				long interval = 1L + rng.NextInt(12);
				bool initial = rng.NextBool();

				// A strictly increasing history in which every entry flips the value, so the value
				// in force is fully determined by how many changes have happened.
				int transitionCount = 1 + rng.NextInt(5);
				long[] at = new long[transitionCount];
				long tick = 0L;
				for (int t = 0; t < transitionCount; t++)
				{
					long gap = 1L + rng.NextInt(15);
					if (rng.NextBool())
					{
						// Land this one exactly on a deadline: the coincidence case, where the
						// change must win at that tick rather than the pulse.
						long candidate = tick + gap;
						long snapped = ((candidate + interval - 1L) / interval) * interval;
						if (snapped > tick)
						{
							gap = snapped - tick;
							deadlineCoincidences++;
						}
					}
					tick += gap;
					at[t] = tick;
				}
				long end = tick + 1L + rng.NextInt(20);

				ToyAdvanceResult created = FixedPeriodToyRules.Create(
					KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, interval, initial);
				Assert.IsTrue(created.Succeeded);

				// Baseline: wake only where the history says something changed, plus the end.
				FixedPeriodToyState sparse = created.State;
				bool sparseValue = initial;
				for (int t = 0; t < transitionCount; t++)
				{
					sparseValue = !sparseValue;
					ToyAdvanceResult step = FixedPeriodToyRules.AdvanceThrough(sparse, at[t], sparseValue);
					Assert.IsTrue(step.Succeeded, "baseline transition " + t);
					sparse = step.State;
				}
				ToyAdvanceResult sparseEnd = FixedPeriodToyRules.AdvanceThrough(sparse, end, sparseValue);
				Assert.IsTrue(sparseEnd.Succeeded, "baseline end");

				// Comparison: the identical history observed far more often. Every extra wake
				// carries the value actually in force at that moment, and no wake at a change tick
				// ever carries the old value.
				FixedPeriodToyState dense = created.State;
				bool denseValue = initial;
				long last = 0L;
				for (int t = 0; t < transitionCount; t++)
				{
					int extra = rng.NextInt(3);
					for (int e = 0; e < extra; e++)
					{
						long span = at[t] - last;
						if (span <= 1L)
						{
							break;
						}
						long mid = last + 1L + rng.NextInt((int)(span - 1L));
						ToyAdvanceResult between = FixedPeriodToyRules.AdvanceThrough(dense, mid, denseValue);
						Assert.IsTrue(between.Succeeded, "dense intermediate wake");
						dense = between.State;
						last = mid;
					}

					denseValue = !denseValue;
					ToyAdvanceResult step = FixedPeriodToyRules.AdvanceThrough(dense, at[t], denseValue);
					Assert.IsTrue(step.Succeeded, "dense transition " + t);
					dense = step.State;
					last = at[t];

					// The same wake again at the same tick, under the now-current value.
					ToyAdvanceResult duplicate = FixedPeriodToyRules.AdvanceThrough(dense, at[t], denseValue);
					Assert.IsTrue(duplicate.Succeeded, "duplicate wake at a change tick");
					dense = duplicate.State;
				}

				int tail = rng.NextInt(3);
				for (int e = 0; e < tail; e++)
				{
					long span = end - last;
					if (span <= 1L)
					{
						break;
					}
					long mid = last + 1L + rng.NextInt((int)(span - 1L));
					ToyAdvanceResult between = FixedPeriodToyRules.AdvanceThrough(dense, mid, denseValue);
					Assert.IsTrue(between.Succeeded, "dense tail wake");
					dense = between.State;
					last = mid;
				}
				ToyAdvanceResult denseEnd = FixedPeriodToyRules.AdvanceThrough(dense, end, denseValue);
				Assert.IsTrue(denseEnd.Succeeded, "dense end");

				if (!string.Equals(Encode(sparseEnd.State), Encode(denseEnd.State), StringComparison.Ordinal))
				{
					Assert.Fail("history partition mismatch at seed " + seed + ": interval " + interval
						+ ", initial " + initial + ", changes at [" + string.Join(",", at) + "], end " + end);
				}
				cases++;
			}

			Assert.AreEqual(10000, cases);
			Assert.IsTrue(deadlineCoincidences > 1000,
				"the deadline-coincidence case must actually be exercised, not merely possible; saw " + deadlineCoincidences);
		}

		[Test]
		public void RepeatingAWakeAtTheSameTickIsIdempotent()
		{
			Lcg rng = new Lcg(0xBEEFuL);
			for (int i = 0; i < 2000; i++)
			{
				long interval = 1L + rng.NextInt(13);
				long now = 1L + rng.NextInt(300);
				bool enabled = rng.NextBool();

				ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, interval, enabled);
				ToyAdvanceResult once = FixedPeriodToyRules.AdvanceThrough(created.State, now, enabled);
				Assert.IsTrue(once.Succeeded);
				ToyAdvanceResult twice = FixedPeriodToyRules.AdvanceThrough(once.State, now, enabled);
				Assert.IsTrue(twice.Succeeded);

				Assert.AreEqual(Encode(once.State), Encode(twice.State), "a repeated wake must emit nothing further, case " + i);
			}
		}

		[Test]
		public void EveryEmittedOrdinalHasAStableDistinctIdentity()
		{
			ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 0L, 1L, true);
			ToyAdvanceResult advanced = FixedPeriodToyRules.AdvanceThrough(created.State, 200L, true);
			Assert.IsTrue(advanced.Succeeded);
			Assert.AreEqual(200uL, advanced.State.NextOrdinal);

			Dictionary<string, ulong> seen = new Dictionary<string, ulong>();
			for (ulong ordinal = 0uL; ordinal < 200uL; ordinal++)
			{
				SemanticEventKey key;
				KernelFaultCode fault;
				Assert.IsTrue(FixedPeriodToyRules.TryGetEventKey(advanced.State, ordinal, out key, out fault), "ordinal " + ordinal);
				string id;
				Assert.IsTrue(SemanticEventIdentity.TryCreateId(KernelCanonicalTests.GoldenSeed(), key, out id, out fault));
				if (seen.ContainsKey(id))
				{
					Assert.Fail("identity collision between ordinals " + seen[id] + " and " + ordinal);
				}
				seen[id] = ordinal;

				// Stable: asking again gives the same answer.
				SemanticEventKey again;
				string idAgain;
				FixedPeriodToyRules.TryGetEventKey(advanced.State, ordinal, out again, out fault);
				SemanticEventIdentity.TryCreateId(KernelCanonicalTests.GoldenSeed(), again, out idAgain, out fault);
				Assert.AreEqual(id, idAgain);
			}
			Assert.AreEqual(200, seen.Count);
		}

		[Test]
		public void TenThousandDistinctKeysProduceTenThousandDistinctIdentities()
		{
			HashSet<string> ids = new HashSet<string>();
			KernelFaultCode fault;
			for (int i = 0; i < 10000; i++)
			{
				SemanticEventKey key;
				Assert.IsTrue(SemanticEventKey.TryCreate(3, Settlement, "taf:stream:test", 1u, (ulong)i, out key, out fault));
				string id;
				Assert.IsTrue(SemanticEventIdentity.TryCreateId(KernelCanonicalTests.GoldenSeed(), key, out id, out fault));
				Assert.IsTrue(ids.Add(id), "collision at ordinal " + i);
			}
			Assert.AreEqual(10000, ids.Count);
		}

		[Test]
		public void AdvanceNeverPublishesAPartialStateOnFailure()
		{
			Lcg rng = new Lcg(0xFA11uL);
			for (int i = 0; i < 1000; i++)
			{
				long interval = 1L + rng.NextInt(9);
				ToyAdvanceResult created = FixedPeriodToyRules.Create(KernelCanonicalTests.GoldenSeed(), 3, Settlement, 100L, interval, true);
				string before = Encode(created.State);

				// Any regressed wake must leave the source exactly as it was.
				long regressed = rng.NextInt(100);
				ToyAdvanceResult failed = FixedPeriodToyRules.AdvanceThrough(created.State, regressed, rng.NextBool());
				Assert.IsFalse(failed.Succeeded, "case " + i);
				Assert.AreEqual(KernelFaultCode.ClockRegression, failed.Fault);
				Assert.AreEqual(before, Encode(created.State));
				Assert.AreEqual(before, Encode(failed.State), "the failed result carries the untouched source");
			}
		}
	}
}
#endif
