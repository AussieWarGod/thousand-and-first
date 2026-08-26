using System;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPetitionRules
	{
		public static long CanonicalMonthOrdinal(long Tick)
		{
			long tick = Tick > 0L ? Tick : 0L;
			long year = tick / TicksPerYear;
			long withinYear = tick % TicksPerYear;
			int month = 0;
			while (month < MonthStarts.Length && withinYear >= MonthStarts[month]) month++;
			return year * MonthsPerYear + month;
		}

		/// <summary>Documented district interval, rounded up so scaling never fires early.</summary>
		public static long ScaledInterval(long BaseTicks, int Percent)
		{
			if (BaseTicks <= 0L || Percent <= 0 || Percent > 100) return -1L;
			if (BaseTicks > (long.MaxValue - 99L) / Percent) return -1L;
			return (BaseTicks * Percent + 99L) / 100L;
		}

		public static bool CanOfferAt(long NowTick, long LastOfferTick, long EnabledTick,
			long Interval)
		{
			if (NowTick < 0L || LastOfferTick < 0L || EnabledTick < 0L || Interval <= 0L)
				return false;
			long anchor = Math.Max(LastOfferTick, EnabledTick);
			return NowTick >= anchor && NowTick - anchor >= Interval;
		}

		/// <summary>Older public gate retained for source compatibility.</summary>
		public static bool CanOffer(long NowTick, long LastOfferMonth, long LegacyLastTick,
			PetitionLifecycle State, KingdomRules.PetitionKind Kind)
		{
			if (IsActive(State) || Kind != KingdomRules.PetitionKind.None) return false;
			long last = LastOfferMonth;
			if (last < 0L && LegacyLastTick > 0L)
				last = CanonicalMonthOrdinal(LegacyLastTick);
			return CanonicalMonthOrdinal(NowTick) > last;
		}

		public static bool TryDeadline(long IssuedTick, long Lifetime, out long Deadline)
		{
			Deadline = 0L;
			if (IssuedTick < 0L || Lifetime <= 0L || IssuedTick > long.MaxValue - Lifetime)
				return false;
			Deadline = IssuedTick + Lifetime;
			return true;
		}

		public static long PauseRemaining(long NowTick, long Deadline)
		{
			if (NowTick < 0L || Deadline < 0L) return -1L;
			return Deadline > NowTick ? Deadline - NowTick : 1L;
		}

		public static bool TryResumeDeadline(long NowTick, long Remaining, out long Deadline)
		{
			Deadline = 0L;
			if (NowTick < 0L || Remaining <= 0L || NowTick > long.MaxValue - Remaining)
				return false;
			Deadline = NowTick + Remaining;
			return true;
		}

		public static bool IsExpired(long NowTick, long Deadline)
		{
			return NowTick >= 0L && Deadline >= 0L && NowTick > Deadline;
		}

		public static bool IsExpired(long NowTick, long IssuedTick, long Lifetime)
		{
			return TryDeadline(IssuedTick, Lifetime, out long deadline)
				&& IsExpired(NowTick, deadline);
		}

	}
}
