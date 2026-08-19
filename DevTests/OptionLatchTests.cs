#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Tests
{
	public class OptionLatchTests
	{
		private static OptionLatchState Unobserved()
		{
			return new OptionLatchState(OptionLatchValue.Unobserved, 0L);
		}

		[Test]
		public void FirstObservationInitializesAtNow()
		{
			OptionLatchState next;
			OptionTransitionKind transition;
			KernelFaultCode fault;

			Assert.IsTrue(OptionLatchRules.TryObserve(Unobserved(), true, 7L, out next, out transition, out fault));
			Assert.AreEqual(OptionLatchValue.Enabled, next.Value);
			Assert.AreEqual(7L, next.ChangedAtTick);
			Assert.AreEqual(OptionTransitionKind.InitializedEnabled, transition);

			Assert.IsTrue(OptionLatchRules.TryObserve(Unobserved(), false, 7L, out next, out transition, out fault));
			Assert.AreEqual(OptionLatchValue.Disabled, next.Value);
			Assert.AreEqual(OptionTransitionKind.InitializedDisabled, transition);
		}

		[Test]
		public void UnchangedObservationReturnsThePriorLatchWithItsTickUnrewritten()
		{
			OptionLatchState prior = new OptionLatchState(OptionLatchValue.Enabled, 4L);
			OptionLatchState next;
			OptionTransitionKind transition;
			KernelFaultCode fault;

			Assert.IsTrue(OptionLatchRules.TryObserve(prior, true, 900L, out next, out transition, out fault));
			Assert.AreEqual(OptionTransitionKind.None, transition);
			Assert.AreEqual(OptionLatchValue.Enabled, next.Value);
			// Rewriting the tick here would make a setting that never changed look as though it had
			// just changed, on every single load.
			Assert.AreEqual(4L, next.ChangedAtTick, "the change tick must not be refreshed by observing");
		}

		[Test]
		public void AChangeRecordsNow()
		{
			OptionLatchState prior = new OptionLatchState(OptionLatchValue.Enabled, 4L);
			OptionLatchState next;
			OptionTransitionKind transition;
			KernelFaultCode fault;

			Assert.IsTrue(OptionLatchRules.TryObserve(prior, false, 11L, out next, out transition, out fault));
			Assert.AreEqual(OptionLatchValue.Disabled, next.Value);
			Assert.AreEqual(11L, next.ChangedAtTick);
			Assert.AreEqual(OptionTransitionKind.Disabled, transition);

			Assert.IsTrue(OptionLatchRules.TryObserve(next, true, 12L, out next, out transition, out fault));
			Assert.AreEqual(OptionTransitionKind.Enabled, transition);
			Assert.AreEqual(12L, next.ChangedAtTick);
		}

		[Test]
		public void AChangeAtTheSameTickAsTheLastChangeIsAllowed()
		{
			OptionLatchState prior = new OptionLatchState(OptionLatchValue.Enabled, 5L);
			OptionLatchState next;
			OptionTransitionKind transition;
			KernelFaultCode fault;
			Assert.IsTrue(OptionLatchRules.TryObserve(prior, false, 5L, out next, out transition, out fault));
			Assert.AreEqual(OptionTransitionKind.Disabled, transition);
			Assert.AreEqual(5L, next.ChangedAtTick);
		}

		[Test]
		public void FaultOrderIsFrozenAndNothingIsPublishedOnFailure()
		{
			OptionLatchState prior = new OptionLatchState(OptionLatchValue.Enabled, 10L);
			OptionLatchState next;
			OptionTransitionKind transition;
			KernelFaultCode fault;

			// Negative now is checked first, even when the prior state is also malformed.
			Assert.IsFalse(OptionLatchRules.TryObserve(new OptionLatchState(OptionLatchValue.Unobserved, 3L), true, -1L, out next, out transition, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidTick, fault);

			// Then malformed prior state.
			Assert.IsFalse(OptionLatchRules.TryObserve(new OptionLatchState(OptionLatchValue.Unobserved, 3L), true, 0L, out next, out transition, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidOptionLatch, fault);

			Assert.IsFalse(OptionLatchRules.TryObserve(new OptionLatchState((OptionLatchValue)99, 0L), true, 0L, out next, out transition, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidOptionLatch, fault);

			// Then regression.
			Assert.IsFalse(OptionLatchRules.TryObserve(prior, true, 9L, out next, out transition, out fault));
			Assert.AreEqual(KernelFaultCode.ClockRegression, fault);

			// On every failure the caller gets its own state back and no transition.
			Assert.AreEqual(prior.Value, next.Value);
			Assert.AreEqual(prior.ChangedAtTick, next.ChangedAtTick);
			Assert.AreEqual(OptionTransitionKind.None, transition);
		}

		[TestCase(0, 0L, true, "canonical unobserved")]
		[TestCase(0, 1L, false, "unobserved claiming a change time never happened")]
		[TestCase(2, 0L, true, "enabled at zero")]
		[TestCase(1, 55L, true, "disabled at any nonnegative tick")]
		[TestCase(2, -1L, false, "negative change tick")]
		[TestCase(7, 0L, false, "unknown enum")]
		public void WellFormedness(int valueCode, long changedAt, bool expected, string why)
		{
			OptionLatchValue value = (OptionLatchValue)valueCode;
			Assert.AreEqual(expected, OptionLatchRules.IsWellFormed(new OptionLatchState(value, changedAt)), why);
		}

		/// <summary>
		/// Every representable latch byte against both configured values. The three known values
		/// cover the ordinary first/unchanged/transition paths; the other 253 are what a corrupt or
		/// forward-version save can hand us, and every one must fail closed rather than compare its
		/// way into looking enabled.
		/// </summary>
		[Test]
		public void EveryLatchByteIsClassifiedAgainstBothConfiguredValues()
		{
			OptionLatchState next;
			OptionTransitionKind transition;
			KernelFaultCode fault;
			int knownSeen = 0;

			for (int raw = 0; raw <= 255; raw++)
			{
				OptionLatchValue value = (OptionLatchValue)raw;
				bool known = raw == (int)OptionLatchValue.Unobserved
					|| raw == (int)OptionLatchValue.Disabled
					|| raw == (int)OptionLatchValue.Enabled;

				// Unobserved is well-formed only at tick zero, so each value gets a tick it could
				// legitimately carry; the malformed pairing is covered by WellFormedness above.
				long changedAt = raw == (int)OptionLatchValue.Unobserved ? 0L : 12L;
				OptionLatchState prior = new OptionLatchState(value, changedAt);
				Assert.AreEqual(known, OptionLatchRules.IsWellFormed(prior), "well-formed? " + raw);

				foreach (bool configured in new bool[] { false, true })
				{
					bool ok = OptionLatchRules.TryObserve(prior, configured, 20L, out next, out transition, out fault);
					Assert.AreEqual(known, ok, "observe " + raw + " with configured " + configured);

					if (!known)
					{
						Assert.AreEqual(KernelFaultCode.InvalidOptionLatch, fault, "raw " + raw);
						Assert.AreEqual(OptionTransitionKind.None, transition, "raw " + raw);
						Assert.AreEqual(value, next.Value, "the caller's own state comes back untouched");
						Assert.AreEqual(changedAt, next.ChangedAtTick);
						continue;
					}

					OptionLatchValue expected = configured ? OptionLatchValue.Enabled : OptionLatchValue.Disabled;
					Assert.AreEqual(expected, next.Value, "raw " + raw + ", configured " + configured);

					if (raw == (int)OptionLatchValue.Unobserved)
					{
						Assert.AreEqual(configured ? OptionTransitionKind.InitializedEnabled : OptionTransitionKind.InitializedDisabled, transition);
						Assert.AreEqual(20L, next.ChangedAtTick, "a first observation stamps now");
					}
					else if (value == expected)
					{
						Assert.AreEqual(OptionTransitionKind.None, transition, "raw " + raw);
						Assert.AreEqual(changedAt, next.ChangedAtTick, "an unchanged observation must not refresh the tick");
					}
					else
					{
						Assert.AreEqual(configured ? OptionTransitionKind.Enabled : OptionTransitionKind.Disabled, transition);
						Assert.AreEqual(20L, next.ChangedAtTick, "a real change stamps now");
					}
				}

				if (known)
				{
					knownSeen++;
				}
			}
			Assert.AreEqual(3, knownSeen, "exactly three of the 256 byte values are known");
		}

		/// <summary>
		/// The boundary the card pins as a required counterexample: with interval 10, a change at
		/// tick 11 must let the tick-10 pulse through, while a change at tick 10 suppresses it —
		/// and both must match what a wake partitioned at every tick would have produced.
		/// </summary>
		[Test]
		public void TransitionAtADeadlineSuppressesThatPulseButOneTickLaterDoesNot()
		{
			ToyAdvanceResult created = FixedPeriodToyRules.Create(
				KernelCanonicalTests.GoldenSeed(), 3, "taf:settlement:test", 0L, 10L, true);
			Assert.IsTrue(created.Succeeded);

			ToyAdvanceResult changedAt11 = FixedPeriodToyRules.AdvanceThrough(created.State, 11L, false);
			Assert.IsTrue(changedAt11.Succeeded);
			Assert.AreEqual(1uL, changedAt11.State.NextOrdinal, "the tick-10 pulse happened before the transition");

			ToyAdvanceResult changedAt10 = FixedPeriodToyRules.AdvanceThrough(created.State, 10L, false);
			Assert.IsTrue(changedAt10.Succeeded);
			Assert.AreEqual(0uL, changedAt10.State.NextOrdinal, "disabling exactly at the deadline wins");

			// The same answers must come out of a wake at every single tick.
			FixedPeriodToyState walked = created.State;
			for (long t = 1L; t <= 11L; t++)
			{
				ToyAdvanceResult step = FixedPeriodToyRules.AdvanceThrough(walked, t, t < 11L);
				Assert.IsTrue(step.Succeeded, "tick " + t);
				walked = step.State;
			}
			Assert.AreEqual(changedAt11.State.NextOrdinal, walked.NextOrdinal, "partitioned wakes must agree with the direct advance");
		}
	}
}
#endif
