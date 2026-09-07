#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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

			ClassicAssert.IsTrue(OptionLatchRules.TryObserve(Unobserved(), true, 7L, out next, out transition, out fault));
			ClassicAssert.AreEqual(OptionLatchValue.Enabled, next.Value);
			ClassicAssert.AreEqual(7L, next.ChangedAtTick);
			ClassicAssert.AreEqual(OptionTransitionKind.InitializedEnabled, transition);

			ClassicAssert.IsTrue(OptionLatchRules.TryObserve(Unobserved(), false, 7L, out next, out transition, out fault));
			ClassicAssert.AreEqual(OptionLatchValue.Disabled, next.Value);
			ClassicAssert.AreEqual(OptionTransitionKind.InitializedDisabled, transition);
		}

		[Test]
		public void UnchangedObservationReturnsThePriorLatchWithItsTickUnrewritten()
		{
			OptionLatchState prior = new OptionLatchState(OptionLatchValue.Enabled, 4L);
			OptionLatchState next;
			OptionTransitionKind transition;
			KernelFaultCode fault;

			ClassicAssert.IsTrue(OptionLatchRules.TryObserve(prior, true, 900L, out next, out transition, out fault));
			ClassicAssert.AreEqual(OptionTransitionKind.None, transition);
			ClassicAssert.AreEqual(OptionLatchValue.Enabled, next.Value);
			// Rewriting the tick here would make a setting that never changed look as though it had
			// just changed, on every single load.
			ClassicAssert.AreEqual(4L, next.ChangedAtTick, "the change tick must not be refreshed by observing");
		}

		[Test]
		public void AChangeRecordsNow()
		{
			OptionLatchState prior = new OptionLatchState(OptionLatchValue.Enabled, 4L);
			OptionLatchState next;
			OptionTransitionKind transition;
			KernelFaultCode fault;

			ClassicAssert.IsTrue(OptionLatchRules.TryObserve(prior, false, 11L, out next, out transition, out fault));
			ClassicAssert.AreEqual(OptionLatchValue.Disabled, next.Value);
			ClassicAssert.AreEqual(11L, next.ChangedAtTick);
			ClassicAssert.AreEqual(OptionTransitionKind.Disabled, transition);

			ClassicAssert.IsTrue(OptionLatchRules.TryObserve(next, true, 12L, out next, out transition, out fault));
			ClassicAssert.AreEqual(OptionTransitionKind.Enabled, transition);
			ClassicAssert.AreEqual(12L, next.ChangedAtTick);
		}

		[Test]
		public void AChangeAtTheSameTickAsTheLastChangeIsAllowed()
		{
			OptionLatchState prior = new OptionLatchState(OptionLatchValue.Enabled, 5L);
			OptionLatchState next;
			OptionTransitionKind transition;
			KernelFaultCode fault;
			ClassicAssert.IsTrue(OptionLatchRules.TryObserve(prior, false, 5L, out next, out transition, out fault));
			ClassicAssert.AreEqual(OptionTransitionKind.Disabled, transition);
			ClassicAssert.AreEqual(5L, next.ChangedAtTick);
		}

		[Test]
		public void FaultOrderIsFrozenAndNothingIsPublishedOnFailure()
		{
			OptionLatchState prior = new OptionLatchState(OptionLatchValue.Enabled, 10L);
			OptionLatchState next;
			OptionTransitionKind transition;
			KernelFaultCode fault;

			// Negative now is checked first, even when the prior state is also malformed.
			ClassicAssert.IsFalse(OptionLatchRules.TryObserve(new OptionLatchState(OptionLatchValue.Unobserved, 3L), true, -1L, out next, out transition, out fault));
			ClassicAssert.AreEqual(KernelFaultCode.InvalidTick, fault);

			// Then malformed prior state.
			ClassicAssert.IsFalse(OptionLatchRules.TryObserve(new OptionLatchState(OptionLatchValue.Unobserved, 3L), true, 0L, out next, out transition, out fault));
			ClassicAssert.AreEqual(KernelFaultCode.InvalidOptionLatch, fault);

			ClassicAssert.IsFalse(OptionLatchRules.TryObserve(new OptionLatchState((OptionLatchValue)99, 0L), true, 0L, out next, out transition, out fault));
			ClassicAssert.AreEqual(KernelFaultCode.InvalidOptionLatch, fault);

			// Then regression.
			ClassicAssert.IsFalse(OptionLatchRules.TryObserve(prior, true, 9L, out next, out transition, out fault));
			ClassicAssert.AreEqual(KernelFaultCode.ClockRegression, fault);

			// On every failure the caller gets its own state back and no transition.
			ClassicAssert.AreEqual(prior.Value, next.Value);
			ClassicAssert.AreEqual(prior.ChangedAtTick, next.ChangedAtTick);
			ClassicAssert.AreEqual(OptionTransitionKind.None, transition);
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
			ClassicAssert.AreEqual(expected, OptionLatchRules.IsWellFormed(new OptionLatchState(value, changedAt)), why);
		}

		/// <summary>
		/// On every failure the out-parameters carry the caller's own prior state and
		/// <see cref="OptionTransitionKind.None"/> — and are written before any check can fail, so
		/// a caller that ignores the return value still holds something true rather than whatever
		/// was in the variable beforehand.
		/// <para>
		/// Each case seeds the out-parameters with a distinct sentinel that appears nowhere else.
		/// The earlier version of this test reused one variable across calls, so a rule that never
		/// wrote it would still have passed on the value left by the previous, successful call.
		/// </para>
		/// </summary>
		[Test]
		public void EveryFailureWritesThePriorStateAndNoTransitionOverASentinel()
		{
			object[][] cases =
			{
				new object[] { "negative now",
					new OptionLatchState(OptionLatchValue.Enabled, 4L), true, -1L, KernelFaultCode.InvalidTick },
				new object[] { "unobserved with a change tick",
					new OptionLatchState(OptionLatchValue.Unobserved, 3L), true, 10L, KernelFaultCode.InvalidOptionLatch },
				new object[] { "unknown byte",
					new OptionLatchState((OptionLatchValue)99, 0L), false, 10L, KernelFaultCode.InvalidOptionLatch },
				new object[] { "negative prior tick",
					new OptionLatchState(OptionLatchValue.Disabled, -2L), true, 10L, KernelFaultCode.InvalidOptionLatch },
				new object[] { "regression",
					new OptionLatchState(OptionLatchValue.Enabled, 10L), true, 9L, KernelFaultCode.ClockRegression },
				new object[] { "regression with a change",
					new OptionLatchState(OptionLatchValue.Enabled, 10L), false, 9L, KernelFaultCode.ClockRegression }
			};

			for (int i = 0; i < cases.Length; i++)
			{
				string label = (string)cases[i][0];
				OptionLatchState prior = (OptionLatchState)cases[i][1];
				bool configured = (bool)cases[i][2];
				long now = (long)cases[i][3];
				KernelFaultCode expected = (KernelFaultCode)cases[i][4];

				// Sentinels no rule can legitimately produce.
				OptionLatchState next = new OptionLatchState((OptionLatchValue)(170 + i), 123456L + i);
				OptionTransitionKind transition = (OptionTransitionKind)(190 + i);
				KernelFaultCode fault = (KernelFaultCode)(210 + i);

				ClassicAssert.IsFalse(OptionLatchRules.TryObserve(prior, configured, now, out next, out transition, out fault), label);
				ClassicAssert.AreEqual(expected, fault, label + ": exact fault");
				ClassicAssert.AreEqual(OptionTransitionKind.None, transition, label + ": no transition on failure");
				ClassicAssert.AreEqual(prior.Value, next.Value, label + ": the caller's own value comes back");
				ClassicAssert.AreEqual(prior.ChangedAtTick, next.ChangedAtTick, label + ": the caller's own tick comes back");
			}
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
				ClassicAssert.AreEqual(known, OptionLatchRules.IsWellFormed(prior), "well-formed? " + raw);

				foreach (bool configured in new bool[] { false, true })
				{
					bool ok = OptionLatchRules.TryObserve(prior, configured, 20L, out next, out transition, out fault);
					ClassicAssert.AreEqual(known, ok, "observe " + raw + " with configured " + configured);

					if (!known)
					{
						ClassicAssert.AreEqual(KernelFaultCode.InvalidOptionLatch, fault, "raw " + raw);
						ClassicAssert.AreEqual(OptionTransitionKind.None, transition, "raw " + raw);
						ClassicAssert.AreEqual(value, next.Value, "the caller's own state comes back untouched");
						ClassicAssert.AreEqual(changedAt, next.ChangedAtTick);
						continue;
					}

					OptionLatchValue expected = configured ? OptionLatchValue.Enabled : OptionLatchValue.Disabled;
					ClassicAssert.AreEqual(expected, next.Value, "raw " + raw + ", configured " + configured);

					if (raw == (int)OptionLatchValue.Unobserved)
					{
						ClassicAssert.AreEqual(configured ? OptionTransitionKind.InitializedEnabled : OptionTransitionKind.InitializedDisabled, transition);
						ClassicAssert.AreEqual(20L, next.ChangedAtTick, "a first observation stamps now");
					}
					else if (value == expected)
					{
						ClassicAssert.AreEqual(OptionTransitionKind.None, transition, "raw " + raw);
						ClassicAssert.AreEqual(changedAt, next.ChangedAtTick, "an unchanged observation must not refresh the tick");
					}
					else
					{
						ClassicAssert.AreEqual(configured ? OptionTransitionKind.Enabled : OptionTransitionKind.Disabled, transition);
						ClassicAssert.AreEqual(20L, next.ChangedAtTick, "a real change stamps now");
					}
				}

				if (known)
				{
					knownSeen++;
				}
			}
			ClassicAssert.AreEqual(3, knownSeen, "exactly three of the 256 byte values are known");
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
			ClassicAssert.IsTrue(created.Succeeded);

			ToyAdvanceResult changedAt11 = FixedPeriodToyRules.AdvanceThrough(created.State, 11L, false);
			ClassicAssert.IsTrue(changedAt11.Succeeded);
			ClassicAssert.AreEqual(1uL, changedAt11.State.NextOrdinal, "the tick-10 pulse happened before the transition");

			ToyAdvanceResult changedAt10 = FixedPeriodToyRules.AdvanceThrough(created.State, 10L, false);
			ClassicAssert.IsTrue(changedAt10.Succeeded);
			ClassicAssert.AreEqual(0uL, changedAt10.State.NextOrdinal, "disabling exactly at the deadline wins");

			// The same answers must come out of a wake at every single tick.
			FixedPeriodToyState walked = created.State;
			for (long t = 1L; t <= 11L; t++)
			{
				ToyAdvanceResult step = FixedPeriodToyRules.AdvanceThrough(walked, t, t < 11L);
				ClassicAssert.IsTrue(step.Succeeded, "tick " + t);
				walked = step.State;
			}
			ClassicAssert.AreEqual(changedAt11.State.NextOrdinal, walked.NextOrdinal, "partitioned wakes must agree with the direct advance");
		}
	}
}
#endif
