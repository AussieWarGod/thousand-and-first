using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomBountyRules
	{
		/// <summary>First opportunity strictly after posting. False only at tick exhaustion.</summary>
		public static bool TryFirstAttemptTick(long PostedTick, out long Tick)
		{
			Tick = 0L;
			long posted = (PostedTick > 0L) ? PostedTick : 0L;
			if (posted > long.MaxValue - AttemptIntervalTicks)
			{
				return false;
			}
			Tick = posted + AttemptIntervalTicks;
			return true;
		}

		/// <summary>Next opportunity in the same absolute daily lane.</summary>
		public static bool TryAdvanceAttemptTick(long CurrentTick, out long NextTick)
		{
			NextTick = 0L;
			if (CurrentTick < 0L || CurrentTick > long.MaxValue - AttemptIntervalTicks)
			{
				return false;
			}
			NextTick = CurrentTick + AttemptIntervalTicks;
			return true;
		}

		/// <summary>
		/// First aligned opportunity strictly after Now. Used only to migrate visit-counted legacy
		/// notices: old outcomes remain consumed, and loading the new build cannot immediately roll
		/// another reader.
		/// </summary>
		public static bool TryAttemptAfter(long NowTick, long PostedTick, out long Tick)
		{
			Tick = 0L;
			long now = (NowTick > 0L) ? NowTick : 0L;
			long first;
			if (!TryFirstAttemptTick(PostedTick, out first))
			{
				return false;
			}
			if (now < first)
			{
				Tick = first;
				return true;
			}
			long elapsed = now - first;
			long steps = elapsed / AttemptIntervalTicks + 1L;
			if (steps > (long.MaxValue - first) / AttemptIntervalTicks)
			{
				return false;
			}
			Tick = first + steps * AttemptIntervalTicks;
			return true;
		}

		/// <summary>
		/// Bounded prefix arithmetic retained for diagnostics and compatibility. It does not decide
		/// which roster may answer those opportunities; runtime uses <see cref="TryLatestDueAttempt"/>
		/// and consumes older opportunities without drawing them.
		/// </summary>
		public static int DueAttemptPrefix(long NowTick, long NextTick, bool Exhausted, int Cap)
		{
			if (Exhausted || Cap <= 0 || NextTick < 0L || NowTick < NextTick)
			{
				return 0;
			}
			long count = (NowTick - NextTick) / AttemptIntervalTicks + 1L;
			return (count > Cap) ? Cap : (int)count;
		}

		/// <summary>
		/// Selects only the latest due opportunity. Earlier unattended opportunities are skipped,
		/// because resolving them against a future roster lets a newcomer act before they arrived.
		/// The returned skip count is durable audit truth; callers advance both cursor and consumed
		/// count before asking the current roster about <paramref name="LatestTick"/>.
		/// </summary>
		public static bool TryLatestDueAttempt(long NowTick, long NextTick, bool Exhausted,
			out long LatestTick, out long Skipped)
		{
			LatestTick = 0L;
			Skipped = 0L;
			if (Exhausted || NextTick < 0L || NowTick < NextTick)
			{
				return false;
			}
			Skipped = (NowTick - NextTick) / AttemptIntervalTicks;
			if (Skipped > 0L && Skipped > (long.MaxValue - NextTick) / AttemptIntervalTicks)
			{
				return false;
			}
			LatestTick = NextTick + Skipped * AttemptIntervalTicks;
			return LatestTick <= NowTick && NowTick - LatestTick < AttemptIntervalTicks;
		}

	}
}
