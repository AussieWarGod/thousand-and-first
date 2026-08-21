using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// One brink as it stands right now: whether there is one at all, when it was reached, how
	/// much of its window has been spent, and what the founder would have to act on. Immutable,
	/// because a half-read brink two callers can disagree about is a settler who leaves twice.
	/// </summary>
	public readonly struct BrinkRecord
	{
		/// <summary>Whether a brink is recorded at all. Everything else is meaningless when this
		/// is false.</summary>
		public readonly bool Stands;

		/// <summary>The tick the irreversible line was actually crossed &mdash; not the pass the
		/// founder noticed it. Zero for a brink recorded before it could be dated.</summary>
		public readonly long ReachedTick;

		/// <summary>Attended passes of the window already spent, or
		/// <see cref="KingdomBrinkRules.Unannounced"/> when nobody has been told yet.</summary>
		public readonly int PassesSpent;

		/// <summary>What the founder would act on: the creed pulling at them, the other city.
		/// Null when the kind carries no cause of its own.</summary>
		public readonly string Cause;

		/// <summary>The <see cref="ConversionChannel"/> a creed brink was reached through, so the
		/// conversion that fires at the end of the window picks the same words it would have
		/// picked on the day. Zero for the kinds that have no channel.</summary>
		public readonly int Channel;

		public BrinkRecord(bool Stands, long ReachedTick, int PassesSpent, string Cause, int Channel)
		{
			this.Stands = Stands;
			this.ReachedTick = Stands ? ReachedTick : 0L;
			this.PassesSpent = Stands ? PassesSpent : 0;
			this.Cause = Stands ? (string.IsNullOrEmpty(Cause) ? null : Cause) : null;
			this.Channel = Stands ? Channel : 0;
		}

		/// <summary>No brink. What every settler and every realm carries nearly always.</summary>
		public static BrinkRecord None
		{
			get { return new BrinkRecord(Stands: false, 0L, 0, null, 0); }
		}
	}

	/// <summary>
	/// The engine-coupled shell for <see cref="KingdomBrinkRules"/>: where brinks are kept, how
	/// they are announced, and how they are unsaid.
	/// <para>
	/// <b>Where the record lives.</b> A settler's brink lives on the settler, in the same
	/// serialized property bag <c>KingdomShrinePull</c>, <c>KingdomSharedDays</c> and
	/// <c>KingdomLodgingUnhousedAnnounced</c> already use. That is not a shortcut: whose roof
	/// failed and whose creed turned are facts about ONE PERSON, and a person carries their own
	/// facts through a seat swap, a secession and a save without any per-city map having to
	/// remember to carry them (<c>CLOCK-REWORK-CHANGE-MAP.md</c> &sect;4.3, the seat-swap trap).
	/// The realm's own brink &mdash; the one about a city leaving &mdash; is realm state and must
	/// stay off <c>KingdomSettlement</c>, so it lives in the game's generic already-serialized
	/// counter store, exactly as <c>KingdomPlanMarker.PlanOrderCounterKey</c> and
	/// <c>KingdomReach</c>'s per-zone character do.
	/// </para>
	/// <para>
	/// <b>Announce once, never nag, unsay on arrest.</b> The record IS the announce flag: a brink
	/// whose window is <see cref="KingdomBrinkRules.Unannounced"/> speaks on the next attended
	/// pass and never again, and a brink whose cause lifted is removed, which both unsays it and
	/// re-arms it should the cause return.
	/// </para>
	/// </summary>
	public static class KingdomBrink
	{
		// Stored window = spent + StoredWindowOffset, so that the property bag's own "absent reads
		// as zero" means NO BRINK rather than "announced, and no pass has run since". One is the
		// unannounced sentinel, two is announced-and-nothing-spent, and so on up. Without the
		// offset a settler who had just been announced about and a settler nothing had ever
		// happened to would be the same integer.
		private const int StoredWindowOffset = 2;

		/// <summary>Tick a settler's roof brink was reached at.</summary>
		public const string RoofTickProperty = "KingdomBrinkRoofTick";

		/// <summary>A settler's roof-brink window, offset (see the note on the offset).</summary>
		public const string RoofWindowProperty = "KingdomBrinkRoofWindow";

		/// <summary>Tick a settler's creed brink was reached at.</summary>
		public const string CreedTickProperty = "KingdomBrinkCreedTick";

		/// <summary>A settler's creed-brink window, offset.</summary>
		public const string CreedWindowProperty = "KingdomBrinkCreedWindow";

		/// <summary>The creed a settler's creed brink is toward.</summary>
		public const string CreedTowardProperty = "KingdomBrinkCreedToward";

		/// <summary>The <see cref="ConversionChannel"/> a settler's creed brink was reached
		/// through.</summary>
		public const string CreedChannelProperty = "KingdomBrinkCreedChannel";

		/// <summary>
		/// Key under which the realm's secession window lives in <c>XRLGame.IntGameState</c>. A
		/// generic, already-serialized slot rather than a new field on <c>KingdomSystem</c>, for
		/// the reason <c>KingdomPlanMarker</c> gives at its own: realm state that must not be
		/// carried by a city has no business on the seat's reflected field layout.
		/// </summary>
		public const string CityWindowStateKey = "r_TAF_CityBrinkWindow";

		// --- A settler's brink -------------------------------------------------------------

		/// <summary>What is standing over this settler, of this kind. Never throws; a null
		/// settler and one nothing has ever happened to both read as no brink.</summary>
		public static BrinkRecord Of(GameObject Subject, BrinkKind Kind)
		{
			if (Subject == null)
			{
				return BrinkRecord.None;
			}
			int stored = Subject.GetIntProperty(WindowPropertyFor(Kind));
			if (stored == 0)
			{
				return BrinkRecord.None;
			}
			return new BrinkRecord(
				Stands: true,
				Subject.GetLongProperty(TickPropertyFor(Kind)),
				stored - StoredWindowOffset,
				(Kind == BrinkKind.Creed) ? Subject.GetStringProperty(CreedTowardProperty) : null,
				(Kind == BrinkKind.Creed) ? Subject.GetIntProperty(CreedChannelProperty) : 0);
		}

		/// <summary>Whether anything of this kind is standing over this settler.</summary>
		public static bool Stands(GameObject Subject, BrinkKind Kind)
		{
			return Subject != null && Subject.GetIntProperty(WindowPropertyFor(Kind)) != 0;
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
			Subject.SetLongProperty(TickPropertyFor(Kind), (ReachedTick > 0L) ? ReachedTick : 0L);
			Subject.SetIntProperty(WindowPropertyFor(Kind), KingdomBrinkRules.Unannounced + StoredWindowOffset);
			if (Kind == BrinkKind.Creed)
			{
				Subject.SetStringProperty(CreedTowardProperty, string.IsNullOrEmpty(Cause) ? null : Cause);
				Subject.SetIntProperty(CreedChannelProperty, Channel);
			}
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
			Subject.SetIntProperty(WindowPropertyFor(Kind), 0);
			Subject.SetLongProperty(TickPropertyFor(Kind), 0L);
			if (Kind == BrinkKind.Creed)
			{
				Subject.SetStringProperty(CreedTowardProperty, null);
				Subject.SetIntProperty(CreedChannelProperty, 0);
			}
			return true;
		}

		/// <summary>
		/// Spends one attended pass of this settler's window and reports where it now stands.
		/// Called from the owning consumer's attended pass and from nowhere else, which is the
		/// whole of "absence never spends a window".
		/// </summary>
		/// <returns>The record after the pass. <c>Stands</c> is false when there was no brink to
		/// spend, and the caller should do nothing.</returns>
		public static BrinkRecord SpendPass(GameObject Subject, BrinkKind Kind)
		{
			BrinkRecord before = Of(Subject, Kind);
			if (!before.Stands)
			{
				return BrinkRecord.None;
			}
			int spent = KingdomBrinkRules.AfterAttendedPass(before.PassesSpent);
			Subject.SetIntProperty(WindowPropertyFor(Kind), spent + StoredWindowOffset);
			return new BrinkRecord(Stands: true, before.ReachedTick, spent, before.Cause, before.Channel);
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
			if (System == null || The.Game == null)
			{
				return BrinkRecord.None;
			}
			int stored = The.Game.GetIntGameState(CityWindowStateKey);
			if (stored == 0)
			{
				return BrinkRecord.None;
			}
			return new BrinkRecord(Stands: true, System.LastDissentTick, stored - StoredWindowOffset, null, 0);
		}

		/// <summary>Whether the realm stands at the breaking point with its window still
		/// running.</summary>
		public static bool CityStands()
		{
			return The.Game != null && The.Game.GetIntGameState(CityWindowStateKey) != 0;
		}

		/// <summary>
		/// Records that the realm has reached the breaking point, freezing
		/// <c>KingdomSystem.LastDissentTick</c> at the day the crossing actually happened so the
		/// announcement can quote it. Idempotent, for the reason the settler form is.
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
			The.Game.SetIntGameState(CityWindowStateKey, KingdomBrinkRules.Unannounced + StoredWindowOffset);
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
			The.Game.SetIntGameState(CityWindowStateKey, 0);
			if (System != null)
			{
				System.LastDissentTick = NowTick;
			}
			return true;
		}

		/// <summary>Spends one attended pass of the realm's window.</summary>
		public static BrinkRecord SpendCityPass(KingdomSystem System)
		{
			BrinkRecord before = OfCity(System);
			if (!before.Stands)
			{
				return BrinkRecord.None;
			}
			int spent = KingdomBrinkRules.AfterAttendedPass(before.PassesSpent);
			The.Game.SetIntGameState(CityWindowStateKey, spent + StoredWindowOffset);
			return new BrinkRecord(Stands: true, before.ReachedTick, spent, before.Cause, before.Channel);
		}

		// --- Saying it, and unsaying it ----------------------------------------------------

		/// <summary>
		/// The announcement, said once and in both places the founder looks: the ledger, where it
		/// is waiting when they come home, and the chronicle, which dates it. Rule 3.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Kind">Which brink.</param>
		/// <param name="Subject">The settler by name, or the city by name.</param>
		/// <param name="Cause">What the founder would act on.</param>
		/// <param name="Record">The brink as it now stands, for its reached tick and window.</param>
		/// <param name="NowTick">Now, for the honest elapsed.</param>
		public static void Announce(KingdomSystem System, BrinkKind Kind, string Subject, string Cause, BrinkRecord Record, long NowTick)
		{
			if (System == null)
			{
				return;
			}
			int days = KingdomBrinkRules.DaysStood(Record.ReachedTick, NowTick);
			System.Ledger.NoteBrink(KingdomBrinkRules.AnnounceNote(Kind, Subject, Cause, days, KingdomBrinkRules.PassesLeft(Kind, Record.PassesSpent)));
			KingdomChronicle.Record(System, KingdomBrinkRules.AnnounceTelling(Kind, Subject, Cause, days));
			KingdomLog.Log("brink: " + Kind + " " + (Subject ?? "-") + " cause=" + (Cause ?? "-") + " days=" + days);
		}

		/// <summary>
		/// The unsaying, when the cause went before the window did. Ledger only: the chronicle
		/// records what happened, and a thing that stopped happening is news for the homecoming
		/// report rather than an entry in the book.
		/// </summary>
		public static void Unsay(KingdomSystem System, BrinkKind Kind, string Subject)
		{
			if (System == null)
			{
				return;
			}
			System.Ledger.NoteBrinkLifted(KingdomBrinkRules.LiftedNote(Kind, Subject));
		}

		// --- Which property ----------------------------------------------------------------

		private static string TickPropertyFor(BrinkKind Kind)
		{
			return (Kind == BrinkKind.Creed) ? CreedTickProperty : RoofTickProperty;
		}

		private static string WindowPropertyFor(BrinkKind Kind)
		{
			return (Kind == BrinkKind.Creed) ? CreedWindowProperty : RoofWindowProperty;
		}
	}
}
