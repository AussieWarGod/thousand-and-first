#if TAF_TESTS
using System;
using System.Numerics;
using NUnit.Framework;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Tests
{
	public class TickMathTests
	{
		[TestCase(0L, 0L, true, 0)]
		[TestCase(0L, 1L, true, 0)]
		[TestCase(5L, 5L, true, 0)]
		[TestCase(5L, 4L, false, 3)]
		[TestCase(-1L, 0L, false, 1)]
		[TestCase(0L, -1L, false, 1)]
		[TestCase(-1L, -1L, false, 1)]
		[TestCase(long.MinValue, 0L, false, 1)]
		[TestCase(0L, long.MaxValue, true, 0)]
		public void ValidateAdvance(long processed, long now, bool expected, int expectedFaultCode)
		{
			KernelFaultCode expectedFault = (KernelFaultCode)expectedFaultCode;
			KernelFaultCode fault;
			Assert.AreEqual(expected, TickMath.TryValidateAdvance(processed, now, out fault));
			Assert.AreEqual(expectedFault, fault);
		}

		[Test]
		public void NegativeTickIsCheckedBeforeRegression()
		{
			// Fault precedence is frozen so combined-invalid input cannot vary by implementation.
			KernelFaultCode fault;
			TickMath.TryValidateAdvance(5L, -1L, out fault);
			Assert.AreEqual(KernelFaultCode.InvalidTick, fault, "negative wins over regression");
		}

		[TestCase(0L, 1L, 1L, 0)]
		[TestCase(10L, 10L, 20L, 0)]
		[TestCase(0L, long.MaxValue, long.MaxValue, 0)]
		public void AddIntervalSucceeds(long origin, long interval, long expected, int expectedFaultCode)
		{
			KernelFaultCode expectedFault = (KernelFaultCode)expectedFaultCode;
			long result;
			KernelFaultCode fault;
			Assert.IsTrue(TickMath.TryAddInterval(origin, interval, out result, out fault));
			Assert.AreEqual(expected, result);
			Assert.AreEqual(expectedFault, fault);
		}

		[TestCase(-1L, 10L, 1)]
		[TestCase(0L, 0L, 2)]
		[TestCase(0L, -1L, 2)]
		[TestCase(long.MaxValue, 1L, 4)]
		[TestCase(1L, long.MaxValue, 4)]
		public void AddIntervalFailsClosed(long origin, long interval, int expectedFaultCode)
		{
			KernelFaultCode expectedFault = (KernelFaultCode)expectedFaultCode;
			long result;
			KernelFaultCode fault;
			Assert.IsFalse(TickMath.TryAddInterval(origin, interval, out result, out fault));
			Assert.AreEqual(expectedFault, fault);
			Assert.AreEqual(0L, result, "no partial value on failure");
		}

		[TestCase(10L, 10L, 10L, 1uL, 20L, "exactly due fires once")]
		[TestCase(19L, 10L, 10L, 1uL, 20L, "one tick before the next deadline still one")]
		[TestCase(20L, 10L, 10L, 2uL, 30L, "two deadlines passed")]
		[TestCase(25L, 10L, 10L, 2uL, 30L, "the golden fixture case")]
		[TestCase(9L, 10L, 10L, 0uL, 10L, "not yet due is a normal zero, not a regression")]
		[TestCase(0L, 0L, 1L, 1uL, 1L, "zero tick zero deadline")]
		[TestCase(100L, 0L, 1L, 101uL, 101L, "unit interval from zero")]
		public void CountFixedPeriodDue(long now, long nextDue, long interval, ulong expectedCount, long expectedFollowing, string why)
		{
			ulong count;
			long following;
			KernelFaultCode fault;
			Assert.IsTrue(TickMath.TryCountFixedPeriodDue(now, nextDue, interval, out count, out following, out fault), why);
			Assert.AreEqual(expectedCount, count, why);
			Assert.AreEqual(expectedFollowing, following, why);
		}

		[Test]
		public void TheFollowingDeadlineIsAlwaysStrictlyAfterNow()
		{
			// The property the fold depends on: after processing, the next deadline is in the
			// future, so a repeated wake at the same tick emits nothing.
			KernelFaultCode fault;
			for (long now = 0; now <= 200; now++)
			{
				for (long interval = 1; interval <= 7; interval++)
				{
					ulong count;
					long following;
					if (TickMath.TryCountFixedPeriodDue(now, 0L, interval, out count, out following, out fault))
					{
						Assert.IsTrue(following > now, "now " + now + ", interval " + interval + ", following " + following);
					}
				}
			}
		}

		[TestCase(-1L, 0L, 1L, 1)]
		[TestCase(0L, -1L, 1L, 1)]
		[TestCase(0L, 0L, 0L, 2)]
		[TestCase(0L, 0L, -1L, 2)]
		public void CountFixedPeriodDueFailsClosed(long now, long nextDue, long interval, int expectedFaultCode)
		{
			KernelFaultCode expectedFault = (KernelFaultCode)expectedFaultCode;
			ulong count;
			long following;
			KernelFaultCode fault;
			Assert.IsFalse(TickMath.TryCountFixedPeriodDue(now, nextDue, interval, out count, out following, out fault));
			Assert.AreEqual(expectedFault, fault);
			Assert.AreEqual(0uL, count, "no partial count");
			Assert.AreEqual(0L, following, "no partial deadline");
		}

		[Test]
		public void AnUnrepresentableFollowingDeadlineOverflowsRatherThanWrapping()
		{
			ulong count;
			long following;
			KernelFaultCode fault;
			// The deadline sits at the top of the range, so the next one cannot be represented.
			Assert.IsFalse(TickMath.TryCountFixedPeriodDue(long.MaxValue, long.MaxValue, long.MaxValue, out count, out following, out fault));
			Assert.AreEqual(KernelFaultCode.ArithmeticOverflow, fault);
			Assert.AreEqual(0uL, count);
			Assert.AreEqual(0L, following);
		}

		[Test]
		public void AnEnormousButValidCountIsReportedRatherThanCapped()
		{
			// Deliberately not capped: capping would silently discard real semantic debt. Consumers
			// are required to fold this, never to loop once per occurrence.
			ulong count;
			long following;
			KernelFaultCode fault;
			Assert.IsTrue(TickMath.TryCountFixedPeriodDue(long.MaxValue - 1L, 0L, 1L, out count, out following, out fault));
			Assert.AreEqual((ulong)(long.MaxValue - 1L) + 1uL, count);
			Assert.AreEqual(long.MaxValue, following);
		}

		/// <summary>
		/// The closed form against the thing it replaces. A one-step loop is obviously correct and
		/// unusably slow; this proves the fast path agrees with it everywhere it can be run.
		/// </summary>
		[Test]
		public void CountMatchesAOneStepReferenceLoopAcrossTheWholeSmallDomain()
		{
			KernelFaultCode fault;
			int compared = 0;
			for (long next = 0L; next <= 32L; next++)
			{
				for (long interval = 1L; interval <= 16L; interval++)
				{
					for (long now = 0L; now <= 256L; now++)
					{
						ulong count;
						long following;
						Assert.IsTrue(TickMath.TryCountFixedPeriodDue(now, next, interval, out count, out following, out fault),
							"valid input must succeed: now " + now + ", next " + next + ", interval " + interval);

						// The reference: step one deadline at a time, exactly as a naive
						// implementation would, and count the firings.
						ulong referenceCount = 0uL;
						long deadline = next;
						while (deadline <= now)
						{
							referenceCount++;
							deadline += interval;
						}

						Assert.AreEqual(referenceCount, count, "count at now " + now + ", next " + next + ", interval " + interval);
						Assert.AreEqual(deadline, following, "following at now " + now + ", next " + next + ", interval " + interval);
						compared++;
					}
				}
			}
			Assert.AreEqual(33 * 16 * 257, compared);
		}

		/// <summary>
		/// The same claim over the full <c>long</c> range, where a reference loop cannot run.
		/// <see cref="BigInteger"/> is exact and unbounded, so it cannot share an overflow bug with
		/// the implementation under test — which is the entire point of using it rather than
		/// recomputing in <c>long</c>.
		/// </summary>
		[Test]
		public void CountMatchesABigIntegerOracleOverAHundredThousandTriples()
		{
			ulong state = 0x9E3779B97F4A7C15uL;
			int checkedTriples = 0;
			KernelFaultCode fault;

			for (int i = 0; i < 100000; i++)
			{
				state = unchecked((state * 6364136223846793005uL) + 1442695040888963407uL);
				ulong a = state;
				a ^= a >> 33;
				state = unchecked((state * 6364136223846793005uL) + 1442695040888963407uL);
				ulong b = state;
				b ^= b >> 33;

				long now;
				long next;
				long interval;
				if ((i & 3) == 0)
				{
					// Deliberately crowd the top of the range, where an intermediate product is
					// most likely to overflow.
					now = long.MaxValue - (long)(a % 1000uL);
					next = (long)(b % 1000uL);
					interval = 1L + (long)(a % 7uL);
				}
				else
				{
					now = (long)(a & 0x7FFFFFFFFFFFFFFFuL);
					next = (long)(b & 0x7FFFFFFFFFFFFFFFuL);
					interval = 1L + (long)(b % 1000000uL);
				}

				ulong count;
				long following;
				bool ok = TickMath.TryCountFixedPeriodDue(now, next, interval, out count, out following, out fault);

				BigInteger expectedCount;
				BigInteger expectedFollowing;
				if (now < next)
				{
					expectedCount = BigInteger.Zero;
					expectedFollowing = next;
				}
				else
				{
					expectedCount = (((BigInteger)now - next) / interval) + BigInteger.One;
					expectedFollowing = (BigInteger)next + (expectedCount * interval);
				}

				// Success is exactly the case where the following deadline is representable. The
				// count itself can never exceed ulong from nonnegative long ticks.
				bool expectedOk = expectedFollowing <= long.MaxValue;
				Assert.AreEqual(expectedOk, ok, "success at now " + now + ", next " + next + ", interval " + interval);

				if (expectedOk)
				{
					Assert.AreEqual((ulong)expectedCount, count, "count at now " + now + ", next " + next + ", interval " + interval);
					Assert.AreEqual((long)expectedFollowing, following, "following at now " + now + ", next " + next + ", interval " + interval);
				}
				else
				{
					Assert.AreEqual(KernelFaultCode.ArithmeticOverflow, fault);
					Assert.AreEqual(0uL, count);
					Assert.AreEqual(0L, following);
				}
				checkedTriples++;
			}
			Assert.AreEqual(100000, checkedTriples);
		}

		/// <summary>
		/// The exact boundary, and the exact reason for it. The distinction matters: if the count
		/// were what failed, a cap would be the fix; because the *deadline* is what fails, a cap
		/// would silently discard real debt while leaving the actual defect in place.
		/// </summary>
		[Test]
		public void TheLastRepresentableDeadlineSucceedsAndTheNextFailsForTheRightReason()
		{
			ulong count;
			long following;
			KernelFaultCode fault;

			Assert.IsTrue(TickMath.TryCountFixedPeriodDue(long.MaxValue - 1L, 0L, 1L, out count, out following, out fault),
				"the largest count whose following deadline still fits");
			Assert.AreEqual((ulong)long.MaxValue, count);
			Assert.AreEqual(long.MaxValue, following);

			// One tick further. The mathematical count is 2^63, which fits a ulong comfortably —
			// so this must fail on the deadline, not on the count.
			Assert.IsFalse(TickMath.TryCountFixedPeriodDue(long.MaxValue, 0L, 1L, out count, out following, out fault));
			Assert.AreEqual(KernelFaultCode.ArithmeticOverflow, fault);
			BigInteger mathematicalCount = (BigInteger)long.MaxValue + BigInteger.One;
			Assert.IsTrue(mathematicalCount <= ulong.MaxValue, "the count itself is representable, so it is not what failed");
			Assert.AreEqual((BigInteger)long.MaxValue + BigInteger.One, mathematicalCount);
			Assert.AreEqual(0uL, count);
			Assert.AreEqual(0L, following);
		}

		/// <summary>
		/// Each tick API given several invalid inputs at once, with the winning code written out.
		/// Ordering is uniform here: an unrepresentable tick is judged before an unusable interval,
		/// and both before any comparison between them, because a comparison against a nonsense
		/// value is not a meaningful answer.
		/// </summary>
		[Test]
		public void EveryTickApiResolvesCombinedInvalidInputsToOneFrozenCode()
		{
			KernelFaultCode fault;

			// Advance validation: negative on both sides, and a regression on top.
			Assert.IsFalse(TickMath.TryValidateAdvance(-5L, -10L, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidTick, fault, "negative before regression");

			// Interval addition: negative origin and non-positive interval together.
			long result;
			Assert.IsFalse(TickMath.TryAddInterval(-1L, 0L, out result, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidTick, fault, "tick before interval");
			Assert.AreEqual(0L, result);

			Assert.IsFalse(TickMath.TryAddInterval(-1L, -1L, out result, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidTick, fault);
			Assert.AreEqual(0L, result);

			// A valid origin with a bad interval finally surfaces the interval fault.
			Assert.IsFalse(TickMath.TryAddInterval(0L, 0L, out result, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidInterval, fault);

			// Due counting: all three inputs invalid at once.
			ulong count;
			long following;
			Assert.IsFalse(TickMath.TryCountFixedPeriodDue(-1L, -1L, 0L, out count, out following, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidTick, fault, "ticks before interval");
			Assert.AreEqual(0uL, count);
			Assert.AreEqual(0L, following);

			// Ticks fine, interval not.
			Assert.IsFalse(TickMath.TryCountFixedPeriodDue(10L, 0L, -1L, out count, out following, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidInterval, fault);
			Assert.AreEqual(0uL, count);
			Assert.AreEqual(0L, following);

			// Everything valid but the result unrepresentable: overflow is last, not first.
			Assert.IsFalse(TickMath.TryCountFixedPeriodDue(long.MaxValue, 0L, 1L, out count, out following, out fault));
			Assert.AreEqual(KernelFaultCode.ArithmeticOverflow, fault, "overflow only once the inputs are sound");
		}

		[Test]
		public void RegressionIsNeverSilentlyReanchored()
		{
			ulong count;
			long following;
			KernelFaultCode fault;
			// now < nextDue is a legitimate not-yet-due answer that preserves the deadline exactly.
			Assert.IsTrue(TickMath.TryCountFixedPeriodDue(3L, 99L, 10L, out count, out following, out fault));
			Assert.AreEqual(0uL, count);
			Assert.AreEqual(99L, following, "the deadline must survive untouched");
		}
	}
}
#endif
