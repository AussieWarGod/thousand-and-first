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
	/// <b>Addendum 10(a) moved the doctrine.</b> It used to be <em>consequences wait for
	/// awareness</em>: the brink stood still until the founder came home, and its window was spent
	/// in attended passes, so a settlement left alone could never actually lose anybody. The
	/// author's ruling replaced that with <em>awareness is PUSHED</em> &mdash; "with enough
	/// warning, coaching, and fair time to resolve something, it would be fair if things happened
	/// while they are away". The five rules below are what that ruling costs and buys.
	/// </para>
	/// <para>
	/// <b>Rule 1 &mdash; reaching the threshold does not fire it.</b> A process whose accrual
	/// crosses an irreversible line records a brink &mdash; who, what caused it, and the tick it
	/// was reached &mdash; and then <b>stops accruing</b>. A thousand-day absence and a ten-day
	/// absence arrive at the same place, because there is nowhere past the brink to arrive at.
	/// This survives the change of doctrine unaltered: it is what keeps an absence from minting a
	/// debt no founder chose.
	/// </para>
	/// <para>
	/// <b>Rule 2 &mdash; the pressure is a fact, re-derived every pass.</b> A brink whose cause
	/// has lifted is removed silently and its accrual restarts from nothing. That is what makes
	/// the window arrestable by <em>acting</em> and never by waiting: the founder who rehouses the
	/// settler, separates the household, deconsecrates the shrine or pours the rite has ended it,
	/// and the founder who stands still has not.
	/// </para>
	/// <para>
	/// <b>Rule 3 &mdash; word is pushed at the crossing, once, dated, and it COACHES.</b> The
	/// warning reaches the founder wherever they stand (<c>KingdomWord</c>), names the subject and
	/// the cause, says how long the brink has actually stood, and &mdash; the part that matters
	/// &mdash; names the ARREST (<see cref="ArrestNote"/>). A line that only reports the doom is a
	/// line the founder cannot act on, and a consequence that may fire in absence has no business
	/// being announced by one.
	/// </para>
	/// <para>
	/// <b>Rule 4 &mdash; the window runs in WORLD-DAYS from the warning's delivery</b>, at
	/// <see cref="RoofBrinkWindowDays"/>, <see cref="CreedBrinkWindowDays"/> and
	/// <see cref="CityBrinkWindowDays"/>. Not in attended passes: the window used to be the
	/// founder's and to exist only in their presence, which meant a warned settler could stand at
	/// the edge forever. Every one of the three is its old attended-pass rope multiplied by
	/// <see cref="CohabitationDaysPerAttendedPass"/>, so a founder who comes home at the cadence
	/// the design always assumed walks the same road they always walked, and only one who leaves
	/// sees the difference.
	/// </para>
	/// <para>
	/// <b>Rule 5 &mdash; the window spent with the cause standing fires the consequence, attended
	/// or not.</b> No new outcomes live here; only a new gate in front of the old ones, and every
	/// consequence keeps its own prose. The passes run on zone activation, so "fires in absence"
	/// means concretely: on the founder's return the consequence is found to have HAPPENED at
	/// <see cref="ExpiryTick"/>, and its aftermath is dated to that tick rather than to the
	/// homecoming (<see cref="FiredClause"/>). Nothing irreversible ever fires UNWARNED &mdash;
	/// <see cref="WindowSpent"/> is false for a brink nobody has been told about, whatever the
	/// clock says.
	/// </para>
	/// <para>
	/// Engine-free, so the whole of it is tabled. <see cref="KingdomBrink"/> is the shell that
	/// holds the records against real settlers and the real realm, and <c>KingdomWord</c> is the
	/// one channel every warning is pushed through.
	/// </para>
	/// </summary>
	public static class KingdomBrinkRules
	{
		// ==================================================================================
		// The exchange rate, and the three windows derived through it. The old attended-pass
		// ropes are kept as the INPUT to the derivation rather than deleted, so each window
		// shows its own working and a design that wants a longer rope for one of them still
		// moves exactly one number.
		// ==================================================================================

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
		/// shrine's pull, the water rite's shared living, and now all three brink windows &mdash;
		/// derives its new threshold from its old one through <see cref="InCohabitationDays"/>, so
		/// an attentive founder walks exactly the same road they walked before.
		/// </para>
		/// </summary>
		public const int CohabitationDaysPerAttendedPass = 3;

		/// <summary>
		/// The roof window as it was denominated before Addendum 10(a): two attended passes. Kept
		/// as the INPUT to <see cref="RoofBrinkWindowDays"/> rather than deleted, so the number
		/// shows its working. Two: long enough for a founder standing there to raise a bunk or
		/// stake a plan, short enough that the answer to "why is nobody moving out" is never
		/// "wait longer". Addendum 4b's own number.
		/// </summary>
		public const int RoofBrinkWindowPasses = 2;

		/// <summary>
		/// The creed window as it was denominated before Addendum 10(a): six attended passes,
		/// three times <see cref="RoofBrinkWindowPasses"/>, because a roof is tonight's problem
		/// and a creed is a life's, and the founder's answer here is a household to break up or a
		/// shrine to deconsecrate rather than a bunk they can raise on the spot.
		/// </summary>
		public const int CreedBrinkWindowPasses = 6;

		/// <summary>
		/// The city window as it was denominated before Addendum 10(a): three attended passes.
		/// This is the window the four-tier warning ladder never had &mdash; secession used to
		/// fire on the same pass dissent reached its threshold. One rung under the seven attended
		/// days the Rupture-to-Breaking span is tested at, so the loudest warning still stands for
		/// longer than the window that follows it.
		/// </summary>
		public const int CityBrinkWindowPasses = 3;

		/// <summary>
		/// World-days a settler with nowhere to live has from the moment the word reaches the
		/// founder. Six: <see cref="RoofBrinkWindowPasses"/> through the exchange rate.
		/// </summary>
		public const int RoofBrinkWindowDays = RoofBrinkWindowPasses * CohabitationDaysPerAttendedPass;

		/// <summary>
		/// World-days a settler at the end of a creed's road has from the moment the word reaches
		/// the founder. Eighteen: <see cref="CreedBrinkWindowPasses"/> through the exchange rate.
		/// </summary>
		public const int CreedBrinkWindowDays = CreedBrinkWindowPasses * CohabitationDaysPerAttendedPass;

		/// <summary>
		/// World-days a realm at the breaking point has from the moment the word reaches the
		/// founder. Nine: <see cref="CityBrinkWindowPasses"/> through the exchange rate.
		/// </summary>
		public const int CityBrinkWindowDays = CityBrinkWindowPasses * CohabitationDaysPerAttendedPass;

		/// <summary>The tick of a brink nobody has been told about yet. Zero, and it is the ONLY
		/// unwarned marker: <see cref="WindowSpent"/> refuses to fire on it, which is the whole of
		/// "nothing irreversible ever fires unwarned".</summary>
		public const long Unwarned = 0L;

		/// <summary>World-days the window of one kind of brink runs for, from the warning.</summary>
		public static int WindowDays(BrinkKind Kind)
		{
			switch (Kind)
			{
			case BrinkKind.Roof:
				return RoofBrinkWindowDays;
			case BrinkKind.Creed:
				return CreedBrinkWindowDays;
			case BrinkKind.City:
				return CityBrinkWindowDays;
			default:
				return RoofBrinkWindowDays;
			}
		}

		/// <summary>The attended-pass rope the same window was cut from, so the derivation is
		/// pinnable end to end rather than restated in two places.</summary>
		public static int WindowPasses(BrinkKind Kind)
		{
			switch (Kind)
			{
			case BrinkKind.Roof:
				return RoofBrinkWindowPasses;
			case BrinkKind.Creed:
				return CreedBrinkWindowPasses;
			case BrinkKind.City:
				return CityBrinkWindowPasses;
			default:
				return RoofBrinkWindowPasses;
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

		// ==================================================================================
		// Prose. One announce, one coaching clause and one unsaying per kind, so the ledger
		// cannot drift from the chronicle and a test can pin all three. Each consumer supplies
		// the subject and the cause; none of them writes its own sentence.
		// ==================================================================================

		/// <summary>
		/// What the founder would have to DO, named. Rule 3's coaching clause, pulled out of
		/// <see cref="AnnounceNote"/> so it is a surface a test can hold every kind to: a warning
		/// that says only what will be lost is a warning the founder cannot act on, and under
		/// Addendum 10(a) &mdash; where the loss lands whether they are watching or not &mdash;
		/// that is the difference between a fair consequence and an ambush.
		/// </summary>
		/// <param name="Kind">Which irreversible thing is one window away.</param>
		/// <param name="Cause">The creed pulling at them, or the other city. Blank is tolerated.</param>
		public static string ArrestNote(BrinkKind Kind, string Cause)
		{
			switch (Kind)
			{
			case BrinkKind.Creed:
				return "Break the household up or take the shrine out of their quarter and they hold what they held.";
			case BrinkKind.City:
				return "Pour the rite, or settle what the two of them believe, and it holds.";
			default:
				return "Raise something they would take and they stay.";
			}
		}

		/// <summary>
		/// The founder-facing warning, said once, pushed to wherever they are standing
		/// (<c>KingdomWord</c>): who, what is doing it, how long it has really been going on, what
		/// would stop it (<see cref="ArrestNote"/>), and how many days of world time are left.
		/// </summary>
		/// <param name="Kind">Which irreversible thing is one window away.</param>
		/// <param name="Subject">The settler by name, or the city by name.</param>
		/// <param name="Cause">The creed pulling at them, or the other city &mdash; whatever the
		/// founder would have to act on. Blank is tolerated and named vaguely.</param>
		/// <param name="Days">Whole days the brink has stood, from <see cref="DaysStood"/>.</param>
		/// <param name="DaysLeft">World-days left, from <see cref="DaysLeft"/>.</param>
		public static string AnnounceNote(BrinkKind Kind, string Subject, string Cause, int Days, int DaysLeft)
		{
			string who = string.IsNullOrEmpty(Subject) ? "A settler" : Subject;
			string elapsed = ElapsedPhrase(Days);
			string window = WindowPhrase(DaysLeft);
			string arrest = ArrestNote(Kind, Cause);
			switch (Kind)
			{
			case BrinkKind.Creed:
			{
				string creed = string.IsNullOrEmpty(Cause) ? "the creed of the house they sleep in" : Cause;
				return who + " has come to the end of the road toward " + creed + ", " + elapsed
					+ ". " + arrest + " " + window;
			}
			case BrinkKind.City:
			{
				string here = string.IsNullOrEmpty(Subject) ? "the other city" : Subject;
				string kept = string.IsNullOrEmpty(Cause) ? "this one" : Cause;
				return here + " has been at the breaking point with " + kept + " " + elapsed
					+ ". " + arrest + " " + window;
			}
			default:
				return who + " has had no roof in this settlement they would live under, " + elapsed
					+ ". " + arrest + " " + window;
			}
		}

		/// <summary>
		/// The same day as the founder's own book records it: lower-case clause, no trailing
		/// period, because <c>KingdomChronicle.Record</c> dates it and closes it. Written on the
		/// day the word goes out rather than on the day it fires, so the book holds the warning as
		/// well as the loss.
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
		/// warning was, because a warning that is never withdrawn is a warning the founder stops
		/// believing.
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

		/// <summary>
		/// The push framing: how word out of a settlement the founder is not standing in reads
		/// when it catches up with them. Qud-honest &mdash; somebody walked, or somebody talked,
		/// and the news found them wherever they were.
		/// <para>
		/// Only the FRAMING is conditional. The warning itself is pushed either way, because a
		/// consequence that fires in absence cannot be announced by a note that is only read at
		/// the seat. Standing in the settlement the founder gets the plain line and nothing else,
		/// so nobody is ever told the same thing twice in two voices.
		/// </para>
		/// </summary>
		public static string WordFrom(string CityName, string Line)
		{
			if (string.IsNullOrEmpty(Line))
			{
				return "";
			}
			string from = string.IsNullOrEmpty(CityName) ? "your settlement" : ("{{C|" + CityName + "}}");
			return "Word from " + from + " finds you: " + Line;
		}
	}
}
