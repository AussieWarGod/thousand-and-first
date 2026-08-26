namespace ThousandAndFirst
{
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
	public static partial class KingdomBrinkRules
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

	}
}
