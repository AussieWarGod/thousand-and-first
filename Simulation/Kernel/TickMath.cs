using System;

namespace ThousandAndFirst.Simulation.Kernel
{
	/// <summary>
	/// Checked tick and deadline arithmetic.
	/// <para>
	/// Ticks and deadlines are nonnegative and intervals are positive: a negative value here is
	/// corrupt input, never an absence duration. Every subtraction, increment, multiplication, and
	/// deadline addition is checked <i>before</i> anything is published, and a result that cannot
	/// fit its declared type fails closed with the caller's state byte-identical. Nothing wraps,
	/// nothing clamps and continues, nothing schedules <c>long.MaxValue</c> as a fake success, and
	/// nothing quietly rewrites a deadline from <c>now</c> — each of those turns a detectable fault
	/// into a wrong answer that survives into a save.
	/// </para>
	/// </summary>
	internal static class TickMath
	{
		/// <summary>
		/// Confirms the clock has not run backwards. It reports regression; it never repairs it by
		/// reanchoring either value, because silently accepting a backward clock would let a
		/// corrupted save look healthy.
		/// </summary>
		internal static bool TryValidateAdvance(long processedThroughTick, long now, out KernelFaultCode fault)
		{
			if (processedThroughTick < 0L || now < 0L)
			{
				fault = KernelFaultCode.InvalidTick;
				return false;
			}
			if (now < processedThroughTick)
			{
				fault = KernelFaultCode.ClockRegression;
				return false;
			}
			fault = KernelFaultCode.None;
			return true;
		}

		internal static bool TryAddInterval(long originTick, long intervalTicks, out long resultTick, out KernelFaultCode fault)
		{
			resultTick = 0L;
			if (originTick < 0L)
			{
				fault = KernelFaultCode.InvalidTick;
				return false;
			}
			if (intervalTicks <= 0L)
			{
				fault = KernelFaultCode.InvalidInterval;
				return false;
			}
			if (originTick > long.MaxValue - intervalTicks)
			{
				fault = KernelFaultCode.ArithmeticOverflow;
				return false;
			}
			resultTick = originTick + intervalTicks;
			fault = KernelFaultCode.None;
			return true;
		}

		/// <summary>
		/// How many fixed-period occurrences are due at <paramref name="now"/>, and when the next
		/// one falls.
		/// <para>
		/// <c>dueCount = floor((now - nextDueTick) / intervalTicks) + 1</c> and
		/// <c>followingDueTick = nextDueTick + dueCount * intervalTicks</c>. A clock that is not
		/// yet due succeeds with a count of zero and returns its deadline unchanged — that is a
		/// normal answer, not a regression.
		/// </para>
		/// <para>
		/// The returned count is deliberately not capped. A mathematically valid count may be
		/// enormous, and every consumer is required to fold it with checked arithmetic into
		/// bounded ranges and aggregates rather than looping once per occurrence. Capping here
		/// would silently discard real semantic debt, which is a worse failure than the overflow
		/// this reports honestly.
		/// </para>
		/// </summary>
		internal static bool TryCountFixedPeriodDue(
			long now,
			long nextDueTick,
			long intervalTicks,
			out ulong dueCount,
			out long followingDueTick,
			out KernelFaultCode fault)
		{
			dueCount = 0uL;
			followingDueTick = 0L;

			if (now < 0L || nextDueTick < 0L)
			{
				fault = KernelFaultCode.InvalidTick;
				return false;
			}
			if (intervalTicks <= 0L)
			{
				fault = KernelFaultCode.InvalidInterval;
				return false;
			}
			if (now < nextDueTick)
			{
				dueCount = 0uL;
				followingDueTick = nextDueTick;
				fault = KernelFaultCode.None;
				return true;
			}

			// Both operands are nonnegative and now >= nextDueTick, so the difference is
			// representable without overflow.
			long elapsed = now - nextDueTick;
			ulong count = (ulong)(elapsed / intervalTicks) + 1uL;

			// count * intervalTicks, then nextDueTick + that, both checked before publication.
			ulong interval = (ulong)intervalTicks;
			if (count > ulong.MaxValue / interval)
			{
				fault = KernelFaultCode.ArithmeticOverflow;
				return false;
			}
			ulong advance = count * interval;
			ulong headroom = (ulong)(long.MaxValue - nextDueTick);
			if (advance > headroom)
			{
				fault = KernelFaultCode.ArithmeticOverflow;
				return false;
			}

			dueCount = count;
			followingDueTick = nextDueTick + (long)advance;
			fault = KernelFaultCode.None;
			return true;
		}
	}
}
