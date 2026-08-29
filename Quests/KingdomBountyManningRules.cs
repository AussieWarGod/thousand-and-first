namespace ThousandAndFirst
{
	/// <summary>Pure serviced-time arithmetic for one exact manning contract.</summary>
	public static class KingdomBountyManningRules
	{
		public static long RequiredTicks =>
			(long)KingdomBountyRules.ManningSeasonDays * KingdomRules.TicksPerDay;

		public static long ClampServed(long ServedTicks)
		{
			if (ServedTicks <= 0L) return 0L;
			return ServedTicks >= RequiredTicks ? RequiredTicks : ServedTicks;
		}

		/// <summary>Credits one prior interval only when the last reservation was published and
		/// both exact endpoints are still witnessed at the new checkpoint.</summary>
		public static long Accrue(long ServedTicks, long CheckpointTick, long NowTick,
			bool WasAssigned, bool ExactEndpointsPresent)
		{
			long served;
			return TryAccrue(ServedTicks, CheckpointTick, NowTick, WasAssigned,
				ExactEndpointsPresent, out served) ? served : ClampServed(ServedTicks);
		}

		/// <summary>Fail-closed variant for persisted runtime clocks. A regressed clock is not a
		/// zero-length interval: accepting it and rewinding the checkpoint would credit the same
		/// future span twice after time recovered.</summary>
		public static bool TryAccrue(long ServedTicks, long CheckpointTick, long NowTick,
			bool WasAssigned, bool ExactEndpointsPresent, out long Result)
		{
			Result = ClampServed(ServedTicks);
			if (ServedTicks < 0L || CheckpointTick < 0L || NowTick < 0L
				|| NowTick < CheckpointTick) return false;
			long served = Result;
			if (!WasAssigned || !ExactEndpointsPresent || CheckpointTick <= 0L
				|| NowTick == CheckpointTick || served >= RequiredTicks) return true;
			long elapsed = NowTick - CheckpointTick;
			long remaining = RequiredTicks - served;
			Result = elapsed >= remaining ? RequiredTicks : served + elapsed;
			return true;
		}

		public static long RemainingTicks(long ServedTicks)
		{
			return RequiredTicks - ClampServed(ServedTicks);
		}

		public static int RemainingDays(long ServedTicks)
		{
			long left = RemainingTicks(ServedTicks);
			if (left <= 0L) return 0;
			long days = (left + KingdomRules.TicksPerDay - 1L) / KingdomRules.TicksPerDay;
			return days > int.MaxValue ? int.MaxValue : (int)days;
		}

		public static long ForecastDueTick(long NowTick, long ServedTicks, bool Assigned)
		{
			long now = NowTick > 0L ? NowTick : 0L;
			long left = RemainingTicks(ServedTicks);
			if (!Assigned || left <= 0L) return 0L;
			return now > long.MaxValue - left ? long.MaxValue : now + left;
		}
	}
}
