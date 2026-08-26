using XRL;
using XRL.World;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
	{
	/// <summary>
	/// The engine-coupled shell for <see cref="KingdomBrinkRules"/>: where brinks are kept, how
	/// the word about them goes out, and how they are unsaid.
	/// <para>
	/// <b>Where the record lives, and why it moved.</b> A settler's brink is a fact about ONE
	/// PERSON, and it used to live on the person &mdash; in the same serialized property bag
	/// <c>KingdomShrinePull</c> and <c>KingdomLodgingUnhousedAnnounced</c> still use. That was
	/// right while a settler WAS a <c>GameObject</c>. It stopped being right the moment the design
	/// asked sixty people to keep losing roofs and turning creed while their zone is on disk: a
	/// frozen object's properties are unreachable, so a window that lived there could not run, and
	/// LIVING-CITY-ARCHITECTURE &sect;1.2(d) moved the window into the settler's <b>resident
	/// row</b>. It is still a fact about one person, kept under their own stable
	/// <c>KingdomResidentId</c>, and it still cannot be carried to the wrong city by a seat swap
	/// &mdash; the row travels with the book of the city whose roll they are on.
	/// <b>Nothing above this line changed.</b> Every rule, every window, every announcement is
	/// where it was; what swapped is the storage under six accessors.
	/// </para>
	/// <para>
	/// The realm's own brink &mdash; the one about a city leaving &mdash; is realm state and must
	/// stay off <c>KingdomSettlement</c>, so it stays in the game's generic already-serialized
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
	public static partial class KingdomBrink
	{
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
		/// settler, one nothing has ever happened to, and one no city has a row for all read as no
		/// brink.</summary>
		public static BrinkRecord Of(GameObject Subject, BrinkKind Kind)
		{
			KingdomCityBook book;
			int id;
			if (!KingdomResidents.TryLocate(Realm(), Subject, out book, out id))
			{
				return BrinkRecord.None;
			}
			bool stands;
			long reached;
			long warned;
			string toward;
			int channel;
			if (!book.TryReadBrink(id, Kind, out stands, out reached, out warned, out toward, out channel) || !stands)
			{
				return BrinkRecord.None;
			}
			return new BrinkRecord(Stands: true, reached, warned, toward, channel);
		}

		/// <summary>Whether anything of this kind is standing over this settler.</summary>
		public static bool Stands(GameObject Subject, BrinkKind Kind)
		{
			return Of(Subject, Kind).Stands;
		}

		/// <summary>
		/// Records that this settler has reached an irreversible line, at the tick they actually
		/// reached it. Idempotent: a settler already at this brink keeps the record they have, so
		/// a second caller in the same pass cannot restart their window or redate their loss.
		/// <para>
		/// Enrols the settler if the roll has not reached them yet. That is the one thing this
		/// does which reading does not: a settler who arrived during the growth step can be housed,
		/// refused and warned several steps before the next check-in would have written their row,
		/// and a warning with nowhere to live is a warning that never fires.
		/// </para>
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
			KingdomCityBook book;
			int id;
			if (!KingdomResidents.TryEnsureRow(Realm(), Subject, out book, out id) || Stands(Subject, Kind))
			{
				return false;
			}
			return book.TryWriteBrink(id, Kind, stands: true, (ReachedTick > 0L) ? ReachedTick : 0L,
				KingdomBrinkRules.Unwarned, (Kind == BrinkKind.Creed) ? Cause : null,
				(Kind == BrinkKind.Creed) ? Channel : 0);
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
			BrinkRecord brink = Of(Subject, Kind);
			if (!brink.Stands || brink.Warned)
			{
				return false;
			}
			KingdomCityBook book;
			int id;
			if (!KingdomResidents.TryLocate(Realm(), Subject, out book, out id))
			{
				return false;
			}
			return book.TryWriteBrink(id, Kind, stands: true, brink.ReachedTick,
				(NowTick > 0L) ? NowTick : 1L, brink.Cause, brink.Channel);
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
			KingdomCityBook book;
			int id;
			if (!KingdomResidents.TryLocate(Realm(), Subject, out book, out id))
			{
				return false;
			}
			return book.TryWriteBrink(id, Kind, stands: false, 0L, KingdomBrinkRules.Unwarned, null, 0);
		}

		/// <summary>Whether this settler's window has run out with the cause still standing, at
		/// the world's clock rather than at anybody's attendance. False for a brink the founder
		/// was never warned of.</summary>
		public static bool WindowSpent(GameObject Subject, BrinkKind Kind, long NowTick)
		{
			BrinkRecord brink = Of(Subject, Kind);
			return brink.Stands && KingdomBrinkRules.WindowSpent(Kind, brink.WarnedTick, NowTick);
		}

	}
}
