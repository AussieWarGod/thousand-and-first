namespace ThousandAndFirst
{
	/// <summary>
	/// Which irreversible thing is standing one window away from happening. The value is stable
	/// forever: it is written into settler properties and into the realm's own state slot, so a
	/// renumbering would read one city's brink as another's.
	/// </summary>
	public enum BrinkKind
	{
		/// <summary>A settler with nowhere in the settlement they would live. Ends in
		/// <c>KingdomGrowth.Emigrate</c> under <c>KingdomLodgingRules.DepartureCause</c>.</summary>
		Roof = 1,

		/// <summary>A settler the road has already turned &mdash; osmosis, the shared table or a
		/// shrine &mdash; standing one window short of holding somebody else's creed. Ends in
		/// <c>KingdomConversion.Convert</c>.</summary>
		Creed = 2,

		/// <summary>A realm whose two cities have quarrelled all the way to the breaking point.
		/// Ends in <c>KingdomCreed.Secede</c>.</summary>
		City = 3
	}

	/// <summary>
	/// The brink: one shape for every irreversible consequence in the mod, and the arithmetic of
	/// the last arrestable window in front of it.
	/// <para>
	/// <b>Rule 1 &mdash; reaching the threshold does not fire it.</b> A process whose accrual
	/// crosses an irreversible line records a brink &mdash; who, what caused it, and the tick it
	/// was reached &mdash; and then <b>stops accruing</b>. That halt is the whole of Addendum 8
	/// clause 3: a thousand-day absence and a ten-day absence arrive at the same place, because
	/// there is nowhere past the brink to arrive at.
	/// </para>
	/// <para>
	/// <b>Rule 2 &mdash; the pressure is a fact, re-derived every pass.</b> A brink whose cause
	/// has lifted is removed silently and its accrual restarts from nothing. That is what makes
	/// the window arrestable by <em>acting</em> and never by waiting: the founder who rehouses the
	/// settler, separates the household, deconsecrates the shrine or pours the rite has ended it,
	/// and the founder who stands still has not.
	/// </para>
	/// <para>
	/// <b>Rule 3 &mdash; it announces once, at awareness, with the honest elapsed.</b> The line
	/// names the subject and the cause and says how long the brink has actually stood, however
	/// long that is. Once, per spell: the brink record IS the announce flag (STANDARDS 7b's
	/// idiom), so a settler already at the brink is never told about twice and one whose cause
	/// lifted and returned is told afresh.
	/// </para>
	/// <para>
	/// <b>Rule 4 &mdash; the window is spent in ATTENDED PASSES only</b>, at the length the owning
	/// design names: <see cref="RoofBrinkWindow"/> for a roof, <see cref="CreedBrinkWindow"/> for
	/// a creed, <see cref="CityBrinkWindow"/> for a city. Absence never spends one. The window is
	/// the founder's, and it exists only in their presence.
	/// </para>
	/// <para>
	/// <b>Rule 5 &mdash; if the window runs out, the consequence fires exactly as it did before.</b>
	/// No new outcomes live here; only a new gate in front of the old ones, and every consequence
	/// keeps its own prose.
	/// </para>
	/// <para>
	/// Engine-free, so the whole of it is tabled. <see cref="KingdomBrink"/> is the shell that
	/// holds the records against real settlers and the real realm.
	/// </para>
	/// </summary>
	public static class KingdomBrinkRules
	{
		// ==================================================================================
		// The three windows. Named here and derived at every consumer, so a design that wants a
		// longer rope for one of them moves one constant and the consumer's own tests move with
		// it. Every one of them is counted in ATTENDED PASSES.
		// ==================================================================================

		/// <summary>
		/// Attended passes a settler with nowhere to live is given before they go. Two: long
		/// enough for a founder standing there to raise a bunk or stake a plan, short enough that
		/// the answer to "why is nobody moving out" is never "wait longer". Addendum 4b's own
		/// number, moved here rather than restated.
		/// </summary>
		public const int RoofBrinkWindow = 2;

		/// <summary>
		/// Attended passes a settler at the end of a creed's road is given before they take it.
		/// Six: three times <see cref="RoofBrinkWindow"/>, because a roof is tonight's problem and
		/// a creed is a life's, and the founder's answer here is a household to break up or a
		/// shrine to deconsecrate rather than a bunk they can raise on the spot.
		/// </summary>
		public const int CreedBrinkWindow = 6;

		/// <summary>
		/// Attended passes a realm at the breaking point is given before the unhappier city walks.
		/// Three, and this is the window the four-tier warning ladder never had: secession used to
		/// fire on the same pass dissent reached its threshold, so uncapping accrual without this
		/// would have made an absence lose a city faster than presence does &mdash; Addendum 8
		/// clause 3 exactly inverted. One rung under the seven attended days the Rupture-to-
		/// Breaking span is tested at, so the loudest warning still stands for longer than the
		/// window that follows it.
		/// </summary>
		public const int CityBrinkWindow = 3;

		/// <summary>
		/// The window of a brink nobody has been told about yet. Negative so it can never be
		/// confused with "announced, and no pass has run since", which is zero. The value the
		/// retired <c>KingdomLodgingRules.NoGrace</c> and <c>KingdomConversionRules.NoResentment</c>
		/// both carried, kept because both maps still store it.
		/// </summary>
		public const int Unannounced = -1;

		/// <summary>
		/// Days of world time one attended pass stood for under the old counters, and therefore
		/// the exchange rate every pass-denominated social clock is recalibrated through.
		/// <para>
		/// Three. Not chosen here: the retired <c>MaxUpkeepDaysCharged</c> was three because the
		/// design's model of a present founder was one who comes home about every third day, and
		/// <c>KingdomCreedRules.RiteCooldownDays</c> says so in as many words &mdash; "matching the
		/// absence cap, so the cadence a present founder can hold is the cadence an absent one is
		/// charged". A counter that used to buy N per attended pass therefore buys the same thing
		/// per three cohabited days, and a threshold denominated in passes is the same wall-clock
		/// distance when multiplied by this.
		/// </para>
		/// <para>
		/// It exists so the migration from passes to time is a MULTIPLICATION with an argument
		/// rather than a re-guess. Every consumer that moved &mdash; osmosis, the shared meal, the
		/// shrine's pull, the water rite's shared living &mdash; derives its new threshold from its
		/// old one through <see cref="InCohabitationDays"/>, so an attentive founder walks exactly
		/// the same road they walked before and only an absent one sees any difference.
		/// </para>
		/// </summary>
		public const int CohabitationDaysPerAttendedPass = 3;

		/// <summary>Attended passes the window of one kind of brink runs for.</summary>
		public static int WindowFor(BrinkKind Kind)
		{
			switch (Kind)
			{
			case BrinkKind.Roof:
				return RoofBrinkWindow;
			case BrinkKind.Creed:
				return CreedBrinkWindow;
			case BrinkKind.City:
				return CityBrinkWindow;
			default:
				return RoofBrinkWindow;
			}
		}

		/// <summary>
		/// A pass-denominated figure restated in cohabitation days, at
		/// <see cref="CohabitationDaysPerAttendedPass"/>.
		/// <para>
		/// The one conversion every migrated counter goes through. Non-positive reads as nothing,
		/// because a threshold of nothing is a threshold already met and no clock should be able
		/// to mint one.
		/// </para>
		/// </summary>
		public static int InCohabitationDays(int AttendedPasses)
		{
			return (AttendedPasses <= 0) ? 0 : (AttendedPasses * CohabitationDaysPerAttendedPass);
		}

		// ==================================================================================
		// The window: advanced by attended passes and by nothing else.
		// ==================================================================================

		/// <summary>
		/// The window after one more attended pass has found the cause still standing. A brink at
		/// <see cref="Unannounced"/> becomes zero, which is the pass it is announced on; every
		/// later attended pass adds one.
		/// <para>
		/// This is the ONLY thing that advances a window, and every consumer calls it from its own
		/// attended pass. An absent founder therefore cannot spend a single pass of anybody's
		/// window: no clock is read here, and nothing elapses on its own.
		/// </para>
		/// </summary>
		public static int AfterAttendedPass(int Spent)
		{
			return (Spent < 0) ? 0 : (Spent + 1);
		}

		/// <summary>Whether this is the pass the brink speaks on: true exactly once, for the
		/// window as it stood BEFORE <see cref="AfterAttendedPass"/> was applied.</summary>
		public static bool ShouldAnnounce(int SpentBefore)
		{
			return SpentBefore < 0;
		}

		/// <summary>Whether the window is spent and the consequence fires now: exactly
		/// <see cref="WindowFor"/> attended passes after the one it was announced on.</summary>
		public static bool WindowSpent(BrinkKind Kind, int Spent)
		{
			return Spent >= WindowFor(Kind);
		}

		/// <summary>Attended passes the founder has left. Zero once the window is spent; never
		/// negative.</summary>
		public static int PassesLeft(BrinkKind Kind, int Spent)
		{
			int left = WindowFor(Kind) - ((Spent < 0) ? 0 : Spent);
			return (left > 0) ? left : 0;
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

		// ==================================================================================
		// When it was reached, and how long ago that was. The honest elapsed is the whole point
		// of announcing at awareness rather than at the moment: the founder is told what really
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

		/// <summary>Attended passes left, said the way a person would say it.</summary>
		public static string WindowPhrase(int PassesLeft)
		{
			if (PassesLeft <= 0)
			{
				return "There is no more time in it.";
			}
			if (PassesLeft == 1)
			{
				return "You have one more visit.";
			}
			return "You have " + PassesLeft + " more visits.";
		}

		// ==================================================================================
		// Prose. One announce and one unsaying per kind, so the ledger cannot drift from the
		// chronicle and a test can pin both. Each consumer supplies the subject and the cause;
		// none of them writes its own sentence.
		// ==================================================================================

		/// <summary>
		/// The founder-facing announcement, said once, where the founder will see it (STANDARDS
		/// 7b): who, what is doing it, how long it has really been going on, and what is left of
		/// the window. Says what would stop it, because a line that only reports is a line that
		/// stalls in silence.
		/// </summary>
		/// <param name="Kind">Which irreversible thing is one window away.</param>
		/// <param name="Subject">The settler by name, or the city by name.</param>
		/// <param name="Cause">The creed pulling at them, or the other city &mdash; whatever the
		/// founder would have to act on. Blank is tolerated and named vaguely.</param>
		/// <param name="Days">Whole days the brink has stood, from <see cref="DaysStood"/>.</param>
		/// <param name="PassesLeft">Attended passes left, from <see cref="PassesLeft"/>.</param>
		public static string AnnounceNote(BrinkKind Kind, string Subject, string Cause, int Days, int PassesLeft)
		{
			string who = string.IsNullOrEmpty(Subject) ? "A settler" : Subject;
			string elapsed = ElapsedPhrase(Days);
			string window = WindowPhrase(PassesLeft);
			switch (Kind)
			{
			case BrinkKind.Creed:
			{
				string creed = string.IsNullOrEmpty(Cause) ? "the creed of the house they sleep in" : Cause;
				return who + " has come to the end of the road toward " + creed + ", " + elapsed
					+ ". Break the household up or take the shrine out of their quarter and they hold what they held. "
					+ window;
			}
			case BrinkKind.City:
			{
				string here = string.IsNullOrEmpty(Subject) ? "the other city" : Subject;
				string kept = string.IsNullOrEmpty(Cause) ? "this one" : Cause;
				return here + " has been at the breaking point with " + kept + " " + elapsed
					+ ". Pour the rite, or settle what the two of them believe, and it holds. " + window;
			}
			default:
				return who + " has had no roof in this settlement they would live under, " + elapsed
					+ ". Raise something they would take and they stay. " + window;
			}
		}

		/// <summary>
		/// The same day as the founder's own book records it: lower-case clause, no trailing
		/// period, because <c>KingdomChronicle.Record</c> dates it and closes it. Written on the
		/// pass the brink is first noticed rather than on the pass it fires, so the book holds the
		/// warning as well as the loss.
		/// </summary>
		public static string AnnounceTelling(BrinkKind Kind, string Subject, string Cause, int Days)
		{
			string who = string.IsNullOrEmpty(Subject) ? "a settler" : Subject;
			string elapsed = ElapsedPhrase(Days);
			switch (Kind)
			{
			case BrinkKind.Creed:
			{
				string creed = string.IsNullOrEmpty(Cause) ? "the creed of the house they slept in" : Cause;
				return who + " had all but taken up " + creed + ", having been on that road " + elapsed;
			}
			case BrinkKind.City:
			{
				string here = string.IsNullOrEmpty(Subject) ? "the other city" : Subject;
				string kept = string.IsNullOrEmpty(Cause) ? "the seat" : Cause;
				return here + " stood at the breaking point with " + kept + ", and had stood there " + elapsed;
			}
			default:
				return who + " had been sleeping in the open " + elapsed + ", with nowhere here they would live";
			}
		}

		/// <summary>
		/// The unsaying, when the cause is gone before the window is. Said in the same place the
		/// announcement was, because a warning that is never withdrawn is a warning the founder
		/// stops believing.
		/// </summary>
		public static string LiftedNote(BrinkKind Kind, string Subject)
		{
			string who = string.IsNullOrEmpty(Subject) ? "A settler" : Subject;
			switch (Kind)
			{
			case BrinkKind.Creed:
				return who + " holds what they held. Whatever was pulling at them is not pulling now.";
			case BrinkKind.City:
			{
				string here = string.IsNullOrEmpty(Subject) ? "The other city" : Subject;
				return here + " has stepped back from the breaking point. Nobody is leaving the realm tonight.";
			}
			default:
				return who + " has a roof again, and is staying.";
			}
		}
	}
}
