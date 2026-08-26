namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
		/// <summary>
		/// Days out of a stretch that a work was actually running: elapsed days scaled by the
		/// effectiveness its crew brought (<see cref="CrewEffectiveness"/> for headcount,
		/// <c>KingdomCrewRules.CombinedEffectiveness</c> once capability is folded in). An
		/// unstaffed work gets none of them however long the stretch was; a fully-crewed one gets
		/// all of them.
		/// <para>
		/// This is the denominator every "how long has this been going" counter should be in
		/// once the doctrine reaches it - idleness accrues nothing, running accrues honestly.
		/// </para>
		/// </summary>
		public static int ActivityDays(int Days, int EffectivenessPercent)
		{
			if (Days <= 0 || EffectivenessPercent <= 0)
			{
				return 0;
			}
			if (EffectivenessPercent >= 100)
			{
				return Days;
			}
			return Days * EffectivenessPercent / 100;
		}

		/// <summary>
		/// The same rule at tick resolution, for work measured in labour ticks rather than days
		/// (a raising, whose authored duration is in ticks). Exact and overflow-free: the whole
		/// hundreds and the remainder are scaled separately rather than multiplying first.
		/// </summary>
		public static long LabouredTicks(long ElapsedTicks, int EffectivenessPercent)
		{
			if (ElapsedTicks <= 0 || EffectivenessPercent <= 0)
			{
				return 0;
			}
			if (EffectivenessPercent >= 100)
			{
				return ElapsedTicks;
			}
			return ElapsedTicks / 100L * EffectivenessPercent + ElapsedTicks % 100L * EffectivenessPercent / 100L;
		}

		/// <summary>
		/// Hands a raising wants standing at it to go at its authored pace. Two: a design's
		/// <c>BuildTicks</c> is the duration a properly-crewed settlement takes, so a pair of
		/// free hands is "properly crewed" and anything less is honestly slower. Small on
		/// purpose - the point of the labour term is that an EMPTY settlement raises nothing,
		/// not that raising becomes a staffing puzzle.
		/// </summary>
		public const int RaisingHandsWanted = 2;

		/// <summary>
		/// How fast a scaffold rises, 0 to 100, from the hands the water detail and the works
		/// left over (<c>KingdomMaterialRules.FreeHands</c>). Zero hands is zero: a settlement
		/// with nobody in it raises nothing, however long the founder is away - Addendum 8
		/// clause 2, and the author's ruling that a scaffold nobody works on does not rise.
		/// </summary>
		public static int RaisingEffectiveness(int FreeHands)
		{
			return CrewEffectiveness(FreeHands, RaisingHandsWanted);
		}

		/// <summary>
		/// Why a raising is standing still or crawling, said once (STANDARDS 7b). Null when the
		/// crew is whole, which is the caller's signal to unsay whatever it said last.
		/// </summary>
		/// <param name="DisplayName">What is being raised.</param>
		/// <param name="FreeHands">Hands left over for it this pass.</param>
		public static string RaisingShortfallLine(string DisplayName, int FreeHands)
		{
			string name = string.IsNullOrEmpty(DisplayName) ? "structure" : DisplayName;
			if (FreeHands <= 0)
			{
				return "The " + name + " stands half-raised. There is nobody free to work on it, and a frame does not lift itself.";
			}
			if (FreeHands >= RaisingHandsWanted)
			{
				return null;
			}
			return "The " + name + " rises slowly: " + FreeHands + " pair of hands on it where " + RaisingHandsWanted + " are wanted.";
		}

		// --- Deadlines the founder was not there to see come due ---------------------------
		//
		// Three systems had their own copy of the same three lines: a due tick has passed, the
		// founder has only now walked in, so push a fresh full window out from the moment of
		// witnessing rather than firing something nobody saw. The manifest turned its load back
		// and re-stamped a new window; the raid re-warned with a fresh lead; the arrival loop
		// burned whatever overshoot it could not seat this pass. Each of the three keeps its own
		// prose - a turned-back load, a re-warned faction and a queued settler are not the same
		// news - and none of them keeps its own arithmetic any more.
		//
		// This is Addendum 8 clause 3 in its cheapest form: the deadline is real and elapsed
		// while nobody watched, and what waits for awareness is the CONSEQUENCE. Nothing is
		// forgiven (the raid still comes, the load still turns back) and nothing is banked (an
		// overshoot buys no extra arrivals) - only the moment it lands moves.

		/// <summary>
		/// Where a repeating or one-shot deadline stands the moment a founder walks in on it.
		/// <para>
		/// Returns <paramref name="DeadlineTick"/> unchanged when it has not been overrun, or
		/// when the overrun is still inside the witness band - the founder is close enough to
		/// the moment to count as having been there, so the caller fires it as it stands.
		/// Otherwise the deadline moves to a fresh full window measured from now.
		/// </para>
		/// <para>
		/// A band of zero days means the deadline is spent the instant it comes due, which is
		/// what an arrival slot and a caravan's window both are. A band of one day means a
		/// founder who walks in within the day of it coming due still meets it, which is the
		/// grace the raid warning has always kept: raiders who came a day early wait for the
		/// fight, and raiders who came a season early do not resolve it in the dark.
		/// </para>
		/// </summary>
		/// <param name="DeadlineTick">The due tick as it stands.</param>
		/// <param name="NowTick">The witnessing moment.</param>
		/// <param name="LeadTicks">The fresh window's full length. Zero or less puts the
		/// deadline at now, which is a caller asking for it to be due immediately.</param>
		/// <param name="WitnessGraceDays">Whole days past the deadline in which the founder
		/// still counts as a witness. Zero, the common case, is no band at all.</param>
		/// <returns>The deadline the caller should now carry.</returns>
		public static long RestampDeadline(long DeadlineTick, long NowTick, long LeadTicks, int WitnessGraceDays)
		{
			if (NowTick < DeadlineTick)
			{
				return DeadlineTick;
			}
			if (WitnessGraceDays > 0 && NowTick - DeadlineTick <= (long)WitnessGraceDays * TicksPerDay)
			{
				return DeadlineTick;
			}
			if (LeadTicks <= 0L)
			{
				return NowTick;
			}
			// Fails closed at the far end rather than wrapping into the past: a deadline that
			// overflowed into a negative would read as long overdue and fire on the spot, which
			// is the one outcome this whole helper exists to prevent.
			return (NowTick > long.MaxValue - LeadTicks) ? long.MaxValue : (NowTick + LeadTicks);
		}

		/// <summary>
		/// What a repeating visitor's clock did while nobody was watching it: how many turns of
		/// it came and went, and whether the most recent one is still standing there.
		/// </summary>
		public readonly struct Passages
		{
			/// <summary>Turns of the clock that came due AND ran out of patience unwitnessed.
			/// These are the ones that leave a dated trace and nothing else.</summary>
			public readonly int Departed;

			/// <summary>The tick the one still standing arrived on, or zero when nobody is.
			/// At most one, because an existing visitor blocks the next.</summary>
			public readonly long StandingSince;

			/// <summary>When the next turn falls due, given everything above already happened.
			/// </summary>
			public readonly long NextDueTick;

			/// <summary>The tick the most recent DEPARTED turn arrived on, for a caller dating
			/// the news. Zero when none departed.</summary>
			public readonly long LastDepartedTick;

			public Passages(int Departed, long StandingSince, long NextDueTick, long LastDepartedTick)
			{
				this.Departed = Departed;
				this.StandingSince = StandingSince;
				this.NextDueTick = NextDueTick;
				this.LastDepartedTick = (Departed > 0) ? LastDepartedTick : 0L;
			}
		}

		/// <summary>
		/// Runs a repeating arrival clock forward over however long nobody was looking at it.
		/// <para>
		/// Addendum 8 clause 1 for visitors: travellers walk the road whether the founder is
		/// home or not, so a season away is a season of people arriving, waiting out their
		/// patience at a gate nobody answered, and going on again. What awareness gets is the
		/// dated news of it (clause 3), never a queue of strangers who have been standing in the
		/// square since spring.
		/// </para>
		/// <para>
		/// At most one visitor is left standing, and only when the LAST turn of the clock fell
		/// inside its own patience of now - which is the same rule the shipped code kept by
		/// accident, because an existing visitor blocks the next one. Everything before that
		/// counts as departed.
		/// </para>
		/// </summary>
		/// <param name="DueTick">When the next one was due. Zero or less means the clock has
		/// never been planted, and nothing has happened yet.</param>
		/// <param name="NowTick">Now.</param>
		/// <param name="IntervalTicks">Ticks between one arrival and the next. Zero or less
		/// answers nothing rather than dividing by it.</param>
		/// <param name="PatienceTicks">How long one visitor waits before giving up.</param>
		public static Passages PassagesThrough(long DueTick, long NowTick, long IntervalTicks, long PatienceTicks)
		{
			if (DueTick <= 0L || IntervalTicks <= 0L || NowTick < DueTick)
			{
				return new Passages(0, 0L, DueTick, 0L);
			}
			long overshoot = NowTick - DueTick;
			long arrivals = overshoot / IntervalTicks + 1L;
			long last = DueTick + (arrivals - 1L) * IntervalTicks;
			// The last one is still at the gate only if its own patience has not run out. With
			// every shipped interval longer than its patience that is the newest arrival or
			// nobody, which is why one is all this ever reports.
			if (PatienceTicks > 0L && NowTick - last < PatienceTicks)
			{
				return new Passages(Whole(arrivals - 1L), last, RestampDeadline(last, last, IntervalTicks, 0), last - IntervalTicks);
			}
			return new Passages(Whole(arrivals), 0L, RestampDeadline(last, NowTick, IntervalTicks, 0), last);
		}

		/// <summary>Narrows a non-negative long count into an int without wrapping. A count past
		/// <c>int.MaxValue</c> is a clock nobody has looked at since the world was made, and
		/// saturating is the honest answer.</summary>
		private static int Whole(long Count)
		{
			if (Count <= 0L)
			{
				return 0;
			}
			return (Count > int.MaxValue) ? int.MaxValue : (int)Count;
		}

	}
}
