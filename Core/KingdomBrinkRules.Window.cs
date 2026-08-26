namespace ThousandAndFirst
{
	public static partial class KingdomBrinkRules
	{
		// ==================================================================================
		// The window: anchored at the warning, spent by the world's own clock. Nothing in here
		// counts passes, and nothing in here needs the founder to be standing anywhere.
		// ==================================================================================

		/// <summary>Whether the founder has actually been told about this brink. A brink that has
		/// not been warned has no window running and can never fire.</summary>
		public static bool Warned(long WarnedTick)
		{
			return WarnedTick > Unwarned;
		}

		/// <summary>Whole world-days since the word was delivered. Zero for a brink nobody has
		/// been warned about, and for one warned tonight.</summary>
		public static int DaysSinceWarning(long WarnedTick, long NowTick)
		{
			if (!Warned(WarnedTick) || NowTick <= WarnedTick)
			{
				return 0;
			}
			return KingdomRules.ElapsedDays(NowTick - WarnedTick);
		}

		/// <summary>
		/// The tick the window runs out on &mdash; the moment the consequence actually happens,
		/// whether or not anybody is there to watch it. This is what the aftermath is dated to on
		/// the founder's return, so the settlement's account of itself matches the world's.
		/// </summary>
		/// <returns>Zero for an unwarned brink: an unwarned brink has no deadline at all.</returns>
		public static long ExpiryTick(BrinkKind Kind, long WarnedTick)
		{
			if (!Warned(WarnedTick))
			{
				return 0L;
			}
			return WarnedTick + (long)WindowDays(Kind) * KingdomRules.TicksPerDay;
		}

		/// <summary>
		/// Whether the window is spent and the consequence has happened: a whole
		/// <see cref="WindowDays"/> of world time since the warning was delivered.
		/// <para>
		/// False for an unwarned brink however old it is. That is not a nicety &mdash; it is the
		/// clause that keeps ignorance a shield now that presence has stopped being one. A brink
		/// reached deep inside an absence is warned about on the pass that discovers it and gets
		/// its whole window from there.
		/// </para>
		/// </summary>
		public static bool WindowSpent(BrinkKind Kind, long WarnedTick, long NowTick)
		{
			return Warned(WarnedTick) && DaysSinceWarning(WarnedTick, NowTick) >= WindowDays(Kind);
		}

		/// <summary>World-days the founder has left. Zero once the window is spent; never
		/// negative; the whole window for a brink nobody has been warned about, because their
		/// window has not started.</summary>
		public static int DaysLeft(BrinkKind Kind, long WarnedTick, long NowTick)
		{
			int window = WindowDays(Kind);
			if (!Warned(WarnedTick))
			{
				return window;
			}
			int left = window - DaysSinceWarning(WarnedTick, NowTick);
			return (left > 0) ? left : 0;
		}

		/// <summary>
		/// The world's day number at a tick, for the one counter that must live inside an
		/// already-serialized <c>int</c> store rather than a tick field of its own
		/// (<c>KingdomSystem.ConversionResented</c>). Whole days, floored, and zero for an
		/// unplanted stamp.
		/// </summary>
		public static int DayNumber(long Tick)
		{
			if (Tick <= 0L)
			{
				return 0;
			}
			long days = Tick / KingdomRules.TicksPerDay;
			return (days > int.MaxValue) ? int.MaxValue : (int)days;
		}

		/// <summary>
		/// Rule 1, as arithmetic: an accrual that has reached its threshold stops there.
		/// <para>
		/// Everything a caller would otherwise bank past the line is discarded rather than
		/// remembered, which is exactly what makes a thousand-day absence and a ten-day absence
		/// arrive at the same place. A caller that banked the overflow would be holding a debt the
		/// founder could not see and could not pay.
		/// </para>
		/// </summary>
		/// <param name="Value">What the accrual would have reached.</param>
		/// <param name="Threshold">The irreversible line. Non-positive leaves the value alone,
		/// because a line at nothing is not a line.</param>
		public static int HoldAtBrink(int Value, int Threshold)
		{
			if (Threshold <= 0)
			{
				return (Value < 0) ? 0 : Value;
			}
			if (Value < 0)
			{
				return 0;
			}
			return (Value > Threshold) ? Threshold : Value;
		}

	}
}
