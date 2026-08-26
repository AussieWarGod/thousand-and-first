namespace ThousandAndFirst
{
	public static partial class KingdomBrinkRules
	{
		// ==================================================================================
		// When it was reached, and how long ago that was. The honest elapsed is the whole point
		// of dating the crossing rather than the noticing: the founder is told what really
		// happened and when, not a number the clock was capped down to.
		// ==================================================================================

		/// <summary>
		/// The tick a steady per-day accrual crossed its threshold, somewhere inside a stretch the
		/// founder was not watching.
		/// <para>
		/// Whole days, because that is the unit the accrual is charged in: the crossing lands on
		/// the day boundary the threshold was actually met at, never on the pass the founder
		/// happened to notice it. Clamped to <paramref name="NowTick"/>, so a brink can never be
		/// dated in the future by a rate that overshot.
		/// </para>
		/// </summary>
		/// <param name="StartTick">Tick the stretch was charged from.</param>
		/// <param name="NowTick">Tick the stretch was resolved at.</param>
		/// <param name="Standing">What the accrual stood at when the stretch began.</param>
		/// <param name="Threshold">The line it crossed.</param>
		/// <param name="PerDay">What one day of the stretch added.</param>
		/// <returns>The crossing tick, or zero when nothing crossed &mdash; a non-positive rate, a
		/// threshold at or below nothing, or a stretch that ran backwards.</returns>
		public static long CrossingTick(long StartTick, long NowTick, int Standing, int Threshold, int PerDay)
		{
			if (StartTick <= 0L || NowTick < StartTick || Threshold <= 0)
			{
				return 0L;
			}
			int held = (Standing < 0) ? 0 : Standing;
			if (held >= Threshold)
			{
				return StartTick;
			}
			if (PerDay <= 0)
			{
				return 0L;
			}
			int wanted = Threshold - held;
			long days = (wanted + PerDay - 1) / PerDay;
			long crossed = StartTick + days * KingdomRules.TicksPerDay;
			return (crossed > NowTick) ? NowTick : crossed;
		}

		/// <summary>
		/// Whole days a brink has stood. Uncapped, for the reason the whole rework exists: the
		/// founder is owed the real number.
		/// </summary>
		/// <param name="ReachedTick">Tick the brink was recorded at. Zero or less &mdash; a brink
		/// whose record predates its dating &mdash; reads as today rather than as the age of the
		/// world.</param>
		/// <param name="NowTick">Now.</param>
		public static int DaysStood(long ReachedTick, long NowTick)
		{
			if (ReachedTick <= 0L || NowTick <= ReachedTick)
			{
				return 0;
			}
			return KingdomRules.ElapsedDays(NowTick - ReachedTick);
		}

		/// <summary>
		/// How long it has stood, said the way a person would say it. Plain and short, because it
		/// is a clause inside a longer sentence and the sentence is already carrying the news.
		/// </summary>
		public static string ElapsedPhrase(int Days)
		{
			if (Days <= 0)
			{
				return "since tonight";
			}
			if (Days == 1)
			{
				return "since yesterday";
			}
			return "for " + Days + " days now";
		}

		/// <summary>World-days left, said the way a person would say it. Days rather than visits:
		/// the window is the world's now, and it runs whether or not the founder comes back to
		/// watch it.</summary>
		public static string WindowPhrase(int DaysLeft)
		{
			if (DaysLeft <= 0)
			{
				return "There is no more time in it.";
			}
			if (DaysLeft == 1)
			{
				return "You have one day.";
			}
			return "You have " + DaysLeft + " days.";
		}

		/// <summary>
		/// How long ago the consequence actually happened, said plainly. The window is spent by
		/// the world's clock, so the founder who was elsewhere is told a date rather than being
		/// left to assume it happened as they walked in.
		/// </summary>
		public static string FiredPhrase(int DaysAgo)
		{
			if (DaysAgo <= 0)
			{
				return "today";
			}
			if (DaysAgo == 1)
			{
				return "yesterday";
			}
			return DaysAgo + " days ago";
		}

		/// <summary>
		/// The dating clause every consequence that may fire in absence appends to its own prose.
		/// Empty for something that happened today, because a line in the present tense is already
		/// dated correctly and a redundant "today" reads as an apology.
		/// </summary>
		/// <param name="DaysAgo">Whole days between <see cref="ExpiryTick"/> and the pass that
		/// found it, from <see cref="DaysStood"/>.</param>
		public static string FiredClause(int DaysAgo)
		{
			if (DaysAgo <= 0)
			{
				return "";
			}
			return " It happened " + FiredPhrase(DaysAgo) + ", when the window you were warned of ran out.";
		}

		/// <summary>
		/// The standalone dating line for a consequence whose own prose is already written
		/// elsewhere &mdash; the conversion the chronicle recorded in two registers, the secession
		/// that announced itself. Empty when it happened today, because the consequence's own
		/// sentence is then correctly in the present tense and a second line would be a second
		/// telling of one thing.
		/// </summary>
		/// <param name="Kind">Which brink ran out.</param>
		/// <param name="Subject">The settler, or the city that walked.</param>
		/// <param name="DaysAgo">Whole days between <see cref="ExpiryTick"/> and the resolve that
		/// found it.</param>
		public static string FiredNote(BrinkKind Kind, string Subject, int DaysAgo)
		{
			if (DaysAgo <= 0)
			{
				return "";
			}
			string when = FiredPhrase(DaysAgo) + ", when the window you were warned of ran out.";
			switch (Kind)
			{
			case BrinkKind.Creed:
				return (string.IsNullOrEmpty(Subject) ? "A settler" : Subject) + " took the creed " + when;
			case BrinkKind.City:
				return (string.IsNullOrEmpty(Subject) ? "The other city" : Subject) + " drew up its own charter " + when;
			default:
				return (string.IsNullOrEmpty(Subject) ? "A settler" : Subject) + " left " + when;
			}
		}

	}
}
