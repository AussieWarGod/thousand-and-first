using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// One brink as it stands right now: whether there is one at all, when it was reached, when
	/// the founder was warned of it, and what they would have to act on. Immutable, because a
	/// half-read brink two callers can disagree about is a settler who leaves twice.
	/// </summary>
	public readonly struct BrinkRecord
	{
		/// <summary>Whether a brink is recorded at all. Everything else is meaningless when this
		/// is false.</summary>
		public readonly bool Stands;

		/// <summary>The tick the irreversible line was actually crossed &mdash; not the pass the
		/// founder noticed it. Zero for a brink recorded before it could be dated.</summary>
		public readonly long ReachedTick;

		/// <summary>
		/// The tick the word went out. <see cref="KingdomBrinkRules.Unwarned"/> until it has, and
		/// the anchor of the whole window once it has: the founder's time runs from being told,
		/// never from the crossing, so a brink reached deep inside an absence still hands them the
		/// entire window on the day they hear about it.
		/// </summary>
		public readonly long WarnedTick;

		/// <summary>What the founder would act on: the creed pulling at them, the other city.
		/// Null when the kind carries no cause of its own.</summary>
		public readonly string Cause;

		/// <summary>The <see cref="ConversionChannel"/> a creed brink was reached through, so the
		/// conversion that fires at the end of the window picks the same words it would have
		/// picked on the day. Zero for the kinds that have no channel.</summary>
		public readonly int Channel;

		public BrinkRecord(bool Stands, long ReachedTick, long WarnedTick, string Cause, int Channel)
		{
			this.Stands = Stands;
			this.ReachedTick = Stands ? ReachedTick : 0L;
			this.WarnedTick = Stands ? WarnedTick : 0L;
			this.Cause = Stands ? (string.IsNullOrEmpty(Cause) ? null : Cause) : null;
			this.Channel = Stands ? Channel : 0;
		}

		/// <summary>Whether the founder has been told. A brink nobody has been told about can
		/// never fire, however old it is.</summary>
		public bool Warned
		{
			get { return Stands && KingdomBrinkRules.Warned(WarnedTick); }
		}

		/// <summary>No brink. What every settler and every realm carries nearly always.</summary>
		public static BrinkRecord None
		{
			get { return new BrinkRecord(Stands: false, 0L, 0L, null, 0); }
		}
	}

	/// <summary>
	/// The engine-coupled shell for <see cref="KingdomBrinkRules"/>: where brinks are kept, how
	/// the word about them goes out, and how they are unsaid.
	/// <para>
	/// <b>Where the record lives.</b> A settler's brink lives on the settler, in the same
	/// serialized property bag <c>KingdomShrinePull</c>, <c>KingdomSharedDays</c> and
	/// <c>KingdomLodgingUnhousedAnnounced</c> already use. That is not a shortcut: whose roof
	/// failed and whose creed turned are facts about ONE PERSON, and a person carries their own
	/// facts through a seat swap, a secession and a save without any per-city map having to
	/// remember to carry them (<c>CLOCK-REWORK-CHANGE-MAP.md</c> &sect;4.3, the seat-swap trap).
	/// The realm's own brink &mdash; the one about a city leaving &mdash; is realm state and must
	/// stay off <c>KingdomSettlement</c>, so it lives in the game's generic already-serialized
	/// state store, exactly as <c>KingdomPlanMarker.PlanOrderCounterKey</c> and
	/// <c>KingdomReach</c>'s per-zone character do.
	/// </para>
	/// <para>
	/// <b>Warn once, never nag, unsay on arrest.</b> The warned tick IS the announce flag: a brink
	/// at <see cref="KingdomBrinkRules.Unwarned"/> speaks on the next resolve of its owning pass
	/// and never again, and a brink whose cause lifted is removed, which both unsays it and
	/// re-arms it should the cause return.
	/// </para>
	/// <para>
	/// <b>And then it runs on the world's clock.</b> Nothing here spends a pass. The window is
	/// <c>WarnedTick</c> plus <c>KingdomBrinkRules.WindowDays</c> of world time, so it spends
	/// whether the founder comes back to watch it or not &mdash; Addendum 10(a). What absence
	/// cannot do is start one: an unwarned brink has no deadline, and every consumer checks
	/// <see cref="KingdomBrinkRules.WindowSpent"/>, which refuses to fire on one.
	/// </para>
	/// </summary>
	public static class KingdomBrink
	{
		/// <summary>Tick a settler's roof brink was reached at.</summary>
		public const string RoofTickProperty = "KingdomBrinkRoofTick";

		/// <summary>Tick the founder was warned of a settler's roof brink, and the anchor of its
		/// window. Zero until the word goes out.</summary>
		public const string RoofWarnedProperty = "KingdomBrinkRoofWarned";

		/// <summary>One when a roof brink stands over this settler at all. Kept apart from the
		/// warned tick so that "recorded, and the word has not gone out yet" and "no brink" are
		/// different states rather than the same zero.</summary>
		public const string RoofStandingProperty = "KingdomBrinkRoofStanding";

		/// <summary>Tick a settler's creed brink was reached at.</summary>
		public const string CreedTickProperty = "KingdomBrinkCreedTick";

		/// <summary>Tick the founder was warned of a settler's creed brink.</summary>
		public const string CreedWarnedProperty = "KingdomBrinkCreedWarned";

		/// <summary>One when a creed brink stands over this settler at all.</summary>
		public const string CreedStandingProperty = "KingdomBrinkCreedStanding";

		/// <summary>The creed a settler's creed brink is toward.</summary>
		public const string CreedTowardProperty = "KingdomBrinkCreedToward";

		/// <summary>The <see cref="ConversionChannel"/> a settler's creed brink was reached
		/// through.</summary>
		public const string CreedChannelProperty = "KingdomBrinkCreedChannel";

		/// <summary>
		/// Key under which the fact that the realm stands at the breaking point lives in
		/// <c>XRLGame.IntGameState</c>. A generic, already-serialized slot rather than a new field
		/// on <c>KingdomSystem</c>, for the reason <c>KingdomPlanMarker</c> gives at its own: realm
		/// state that must not be carried by a city has no business on the seat's reflected field
		/// layout.
		/// </summary>
		public const string CityStandingStateKey = "r_TAF_CityBrinkStanding";

		/// <summary>Key under which the tick the founder was warned of the secession lives, as a
		/// string because a tick is a <c>long</c> and the int store is not.</summary>
		public const string CityWarnedStateKey = "r_TAF_CityBrinkWarned";

		// --- A settler's brink -------------------------------------------------------------

		/// <summary>What is standing over this settler, of this kind. Never throws; a null
		/// settler and one nothing has ever happened to both read as no brink.</summary>
		public static BrinkRecord Of(GameObject Subject, BrinkKind Kind)
		{
			if (Subject == null || Subject.GetIntProperty(StandingPropertyFor(Kind)) == 0)
			{
				return BrinkRecord.None;
			}
			return new BrinkRecord(
				Stands: true,
				Subject.GetLongProperty(TickPropertyFor(Kind)),
				Subject.GetLongProperty(WarnedPropertyFor(Kind)),
				(Kind == BrinkKind.Creed) ? Subject.GetStringProperty(CreedTowardProperty) : null,
				(Kind == BrinkKind.Creed) ? Subject.GetIntProperty(CreedChannelProperty) : 0);
		}

		/// <summary>Whether anything of this kind is standing over this settler.</summary>
		public static bool Stands(GameObject Subject, BrinkKind Kind)
		{
			return Subject != null && Subject.GetIntProperty(StandingPropertyFor(Kind)) != 0;
		}

		/// <summary>
		/// Records that this settler has reached an irreversible line, at the tick they actually
		/// reached it. Idempotent: a settler already at this brink keeps the record they have, so
		/// a second caller in the same pass cannot restart their window or redate their loss.
		/// </summary>
		/// <param name="Subject">The settler.</param>
		/// <param name="Kind">Which line.</param>
		/// <param name="ReachedTick">When it was crossed, from
		/// <see cref="KingdomBrinkRules.CrossingTick"/> or from the pass that found it.</param>
		/// <param name="Cause">The creed pulling at them, for a creed brink; null otherwise.</param>
		/// <param name="Channel">The channel that turned them, for a creed brink; zero otherwise.</param>
		/// <returns>True when this call is the one that recorded it.</returns>
		public static bool Record(GameObject Subject, BrinkKind Kind, long ReachedTick, string Cause, int Channel)
		{
			if (Subject == null || Stands(Subject, Kind))
			{
				return false;
			}
			Subject.SetIntProperty(StandingPropertyFor(Kind), 1);
			Subject.SetLongProperty(TickPropertyFor(Kind), (ReachedTick > 0L) ? ReachedTick : 0L);
			Subject.SetLongProperty(WarnedPropertyFor(Kind), KingdomBrinkRules.Unwarned);
			if (Kind == BrinkKind.Creed)
			{
				Subject.SetStringProperty(CreedTowardProperty, string.IsNullOrEmpty(Cause) ? null : Cause);
				Subject.SetIntProperty(CreedChannelProperty, Channel);
			}
			return true;
		}

		/// <summary>
		/// Stamps the tick the word went out, which starts the window. Idempotent: a brink already
		/// warned keeps its original anchor, so a second pass cannot buy the founder more time by
		/// re-warning them and cannot take any away either.
		/// </summary>
		/// <returns>True when this call is the one that warned, which is the caller's signal to
		/// actually say it.</returns>
		public static bool MarkWarned(GameObject Subject, BrinkKind Kind, long NowTick)
		{
			if (!Stands(Subject, Kind) || KingdomBrinkRules.Warned(Subject.GetLongProperty(WarnedPropertyFor(Kind))))
			{
				return false;
			}
			Subject.SetLongProperty(WarnedPropertyFor(Kind), (NowTick > 0L) ? NowTick : 1L);
			return true;
		}

		/// <summary>
		/// Forgets a brink, because its cause is gone. Rule 2: the pressure is a fact re-derived
		/// every pass, so the window is not banked, halved or remembered &mdash; if the cause
		/// returns the founder gets the whole window again, because they are being asked to act on
		/// THIS one.
		/// </summary>
		/// <returns>True when there was something to forget, which is the caller's signal to
		/// unsay it.</returns>
		public static bool Lift(GameObject Subject, BrinkKind Kind)
		{
			if (!Stands(Subject, Kind))
			{
				return false;
			}
			Subject.SetIntProperty(StandingPropertyFor(Kind), 0);
			Subject.SetLongProperty(TickPropertyFor(Kind), 0L);
			Subject.SetLongProperty(WarnedPropertyFor(Kind), KingdomBrinkRules.Unwarned);
			if (Kind == BrinkKind.Creed)
			{
				Subject.SetStringProperty(CreedTowardProperty, null);
				Subject.SetIntProperty(CreedChannelProperty, 0);
			}
			return true;
		}

		/// <summary>Whether this settler's window has run out with the cause still standing, at
		/// the world's clock rather than at anybody's attendance. False for a brink the founder
		/// was never warned of.</summary>
		public static bool WindowSpent(GameObject Subject, BrinkKind Kind, long NowTick)
		{
			BrinkRecord brink = Of(Subject, Kind);
			return brink.Stands && KingdomBrinkRules.WindowSpent(Kind, brink.WarnedTick, NowTick);
		}

		// --- The realm's brink -------------------------------------------------------------

		/// <summary>
		/// What is standing over the realm. The reached tick is <c>KingdomSystem.LastDissentTick</c>
		/// itself, and honestly so: dissent stops accruing at the breaking point, so the last tick
		/// dissent moved IS the tick the realm reached the brink, and freezing it there costs no
		/// field and tells no lie.
		/// </summary>
		public static BrinkRecord OfCity(KingdomSystem System)
		{
			if (System == null || The.Game == null || The.Game.GetIntGameState(CityStandingStateKey) == 0)
			{
				return BrinkRecord.None;
			}
			return new BrinkRecord(Stands: true, System.LastDissentTick, CityWarnedTick(), null, 0);
		}

		/// <summary>Whether the realm stands at the breaking point with its window still
		/// running.</summary>
		public static bool CityStands()
		{
			return The.Game != null && The.Game.GetIntGameState(CityStandingStateKey) != 0;
		}

		/// <summary>
		/// Records that the realm has reached the breaking point, freezing
		/// <c>KingdomSystem.LastDissentTick</c> at the day the crossing actually happened so the
		/// warning can quote it. Idempotent, for the reason the settler form is.
		/// </summary>
		public static bool RecordCity(KingdomSystem System, long ReachedTick)
		{
			if (System == null || The.Game == null || CityStands())
			{
				return false;
			}
			if (ReachedTick > 0L)
			{
				System.LastDissentTick = ReachedTick;
			}
			The.Game.SetIntGameState(CityStandingStateKey, 1);
			The.Game.SetStringGameState(CityWarnedStateKey, "");
			return true;
		}

		/// <summary>Stamps the tick the realm's warning went out. Idempotent, for the reason
		/// <see cref="MarkWarned"/> is.</summary>
		public static bool MarkCityWarned(long NowTick)
		{
			if (!CityStands() || KingdomBrinkRules.Warned(CityWarnedTick()))
			{
				return false;
			}
			The.Game.SetStringGameState(CityWarnedStateKey, ((NowTick > 0L) ? NowTick : 1L).ToString());
			return true;
		}

		/// <summary>
		/// Forgets the realm's brink and restarts its clock at <paramref name="NowTick"/>, because
		/// the quarrel has been eased and the days the realm spent standing at the edge must not
		/// be billed again the moment it steps back.
		/// </summary>
		/// <returns>True when there was something to forget.</returns>
		public static bool LiftCity(KingdomSystem System, long NowTick)
		{
			if (!CityStands())
			{
				return false;
			}
			The.Game.SetIntGameState(CityStandingStateKey, 0);
			The.Game.SetStringGameState(CityWarnedStateKey, "");
			if (System != null)
			{
				System.LastDissentTick = NowTick;
			}
			return true;
		}

		/// <summary>Whether the realm's window has run out with the quarrel still standing.</summary>
		public static bool CityWindowSpent(long NowTick)
		{
			return CityStands() && KingdomBrinkRules.WindowSpent(BrinkKind.City, CityWarnedTick(), NowTick);
		}

		// --- Saying it, and unsaying it ----------------------------------------------------

		/// <summary>
		/// The warning, pushed once through <see cref="KingdomWord"/>: to the founder wherever
		/// they stand, into the ledger's brink lane for the report they read at the seat, and into
		/// the chronicle which dates it. Rule 3, coaching clause and all.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Kind">Which brink.</param>
		/// <param name="Subject">The settler by name, or the city by name.</param>
		/// <param name="Cause">What the founder would act on.</param>
		/// <param name="Record">The brink as it now stands, for its reached and warned ticks.</param>
		/// <param name="NowTick">Now, for the honest elapsed.</param>
		/// <param name="Here">Whether the founder is standing in the ground this is about.</param>
		/// <param name="From">The city the word comes out of, when they are not.</param>
		/// <param name="Spoken">A consumer's own louder wording for the pushed line, or null to
		/// push the ledger note itself.</param>
		public static void Announce(KingdomSystem System, BrinkKind Kind, string Subject, string Cause, BrinkRecord Record, long NowTick, bool Here, string From, string Spoken)
		{
			if (System == null)
			{
				return;
			}
			int days = KingdomBrinkRules.DaysStood(Record.ReachedTick, NowTick);
			int left = KingdomBrinkRules.DaysLeft(Kind, Record.WarnedTick, NowTick);
			KingdomWord.Warn(System, From, Here,
				KingdomBrinkRules.AnnounceNote(Kind, Subject, Cause, days, left),
				KingdomBrinkRules.AnnounceTelling(Kind, Subject, Cause, days),
				Spoken);
			KingdomLog.Log("brink: " + Kind + " " + (Subject ?? "-") + " cause=" + (Cause ?? "-")
				+ " days=" + days + " left=" + left);
		}

		/// <summary>
		/// The unsaying, when the cause went before the window did. Pushed the same way the
		/// warning was, and out of the chronicle: the book records what happened, and a thing that
		/// stopped happening is news for the report rather than an entry in it.
		/// </summary>
		public static void Unsay(KingdomSystem System, BrinkKind Kind, string Subject, bool Here, string From)
		{
			KingdomWord.Unsay(System, From, Here, KingdomBrinkRules.LiftedNote(Kind, Subject));
		}

		// --- Which property ----------------------------------------------------------------

		private static long CityWarnedTick()
		{
			string stored = (The.Game == null) ? null : The.Game.GetStringGameState(CityWarnedStateKey, "");
			long tick;
			return (!string.IsNullOrEmpty(stored) && long.TryParse(stored, out tick)) ? tick : KingdomBrinkRules.Unwarned;
		}

		private static string TickPropertyFor(BrinkKind Kind)
		{
			return (Kind == BrinkKind.Creed) ? CreedTickProperty : RoofTickProperty;
		}

		private static string WarnedPropertyFor(BrinkKind Kind)
		{
			return (Kind == BrinkKind.Creed) ? CreedWarnedProperty : RoofWarnedProperty;
		}

		private static string StandingPropertyFor(BrinkKind Kind)
		{
			return (Kind == BrinkKind.Creed) ? CreedStandingProperty : RoofStandingProperty;
		}
	}
}
