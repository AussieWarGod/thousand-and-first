using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// A standing source of conversion pressure on a settlement's people &mdash; a shrine
	/// consecrated over a quarter, or anything a later wave builds that works the same way.
	/// <para>
	/// A <c>Protocol</c>-shaped contract rather than a call the source makes once, and
	/// deliberately: pressure is a FACT re-derived on every resolve, not an event that
	/// happened. A source that fired once and left a counter running would keep pushing a settler
	/// toward the road after the founder had already deconsecrated the shrine, and the founder
	/// would have no way to tell. Asked fresh every pass, taking the pressure off takes it off.
	/// </para>
	/// <para>
	/// Register with <see cref="KingdomConversion.AddPressureSource"/>. Implementations are
	/// untrusted (STANDARDS.md &sect;9): one that throws is logged and skipped, and never takes
	/// the settlement pass down with it.
	/// </para>
	/// </summary>
	public interface IConversionPressure
	{
		/// <summary>
		/// The creed this source is imposing on <paramref name="Settler"/> where they stand, or
		/// null when it is imposing nothing on them.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The claimed zone the settler is standing in.</param>
		/// <param name="Settler">The settler being asked about.</param>
		/// <returns>A faction name, or null. Whether the settler resents it is not this source's
		/// question &mdash; <see cref="KingdomConversionRules.Resents"/> decides that, from the
		/// engine's own faction feelings.</returns>
		string PressingCreed(KingdomSystem System, Zone Z, GameObject Settler);
	}

	/// <summary>
	/// Conversion, and the exit from it. The engine-coupled shell for Addendum 5's two passive
	/// channels and for the guard every channel shares.
	/// <para>
	/// <b>Osmosis.</b> Each housed settler whose household holds a creed by strict majority is
	/// pulled a little toward it for every DAY they actually live under that roof, scaled by the
	/// closeness ladder: nothing at all in one open room (the people in it already agree by
	/// construction), fastest in a hut, slowest in quarters of one's own, and nothing across a
	/// feeling the quarters would refuse. The stone house is the only architecture that holds an
	/// ambient grudge under one roof, so the stone house is where a real difference actually gets
	/// crossed &mdash; which is the whole of the healing arc the fault-line ceiling (Addendum 4d)
	/// needs. Partition, then build better, then the quarters dissolve.
	/// </para>
	/// <para>
	/// <b>Culture.</b> Each witnessed shared meal nudges its attendees toward the table's own
	/// majority: small, and capped at <see cref="KingdomConversionRules.MealCeilingPercent"/> of
	/// the road so a settlement can never eat its way to a conversion. A free rider on a ceremony
	/// the founder was already holding for other reasons.
	/// </para>
	/// <para>
	/// <b>The exit.</b> A settler may always emigrate rather than convert. Living beside somebody
	/// generates no pressure &mdash; that is the arc working &mdash; but a creed IMPOSED on a
	/// settler who resents it (a realm declaration against theirs, a rival shrine consecrated in
	/// their quarter) starts them toward the road instead: warned once wherever the founder is,
	/// given <see cref="KingdomConversionRules.ResentedWindowDays"/> of world time for them to take
	/// it off, then gone through the settlement's ordinary emigration, chronicled by name and
	/// cause in both registers. <see cref="NotePressure"/> and
	/// <see cref="AddPressureSource"/> are the surface every other channel uses; nothing may build
	/// its own.
	/// </para>
	/// <para>
	/// <b>Counted in cohabitation time, warned at the crossing, spent by the world.</b> People go
	/// on living together whether or not anyone is watching (Addendum 8 clause 1, which names
	/// osmosis), so shared living accrues for every day two settlers actually spent under one roof
	/// and the founder's presence has nothing to do with it. The road ends in a brink rather than
	/// in a conversion, and under Addendum 10(a) the founder's presence does not govern the window
	/// either: the word is pushed to them the moment the road ends, and
	/// <see cref="KingdomBrinkRules.CreedBrinkWindowDays"/> of world time later the creed changes
	/// hands whether they came back or not. What their presence still governs is everything that
	/// can STOP it &mdash; and nothing changes hands unwarned.
	/// </para>
	/// <para>
	/// <b>What the absence is allowed to assume.</b> Who sleeps where is written only by
	/// <c>KingdomLodging.OnSettlementPass</c>, which is attended, so the household standing at the
	/// last pass is the household that stood through the whole stretch &mdash; nobody moved house
	/// while nobody was there. The cohabitation clock is therefore honest by construction, and it
	/// is restarted (<see cref="ForgetCohabitation"/>) the moment lodging does move somebody, so a
	/// settler never inherits days spent under a roof they have left.
	/// </para>
	/// </summary>
	public static partial class KingdomConversion
	{
		/// <summary>
		/// Whether the passive channels run at all. Conversion is creed machinery working through
		/// the lodging assignment, so it is off whenever either of those is off rather than
		/// carrying a toggle of its own for a thing that cannot happen without both.
		/// </summary>
		public static bool Enabled
		{
			get { return KingdomCreed.Enabled && KingdomLodging.Enabled; }
		}

		// Standing pressure sources, asked fresh on every resolve. A list rather than a
		// single hook because two shrines in two quarters are two sources, and the first one that
		// names a creed this settler resents is the one they leave over -- a second grievance does
		// not make anybody leave twice.
		private static readonly List<IConversionPressure> Sources = new List<IConversionPressure>();

		/// <summary>
		/// Tick this settler's cohabitation was last credited at. Stamped on the settler rather
		/// than kept in a map, because how long somebody has lived under their roof is a fact
		/// about them: it survives a seat swap, a secession and a save without any per-city map
		/// having to remember to carry it.
		/// </summary>
		public const string CohabitTickProperty = "KingdomCohabitTick";

		/// <summary>
		/// Roads of shared living this settler has walked all the way to the end, converted or
		/// refused. The draw's ordinal (<c>KingdomConversionRules.RoadEnd</c>): progress holds at
		/// the road's end and can no longer be divided to find out which road they are on, so it
		/// is counted instead.
		/// </summary>
		public const string RoadsWalkedProperty = "KingdomConversionRoads";

		/// <summary>
		/// Registers a standing source of conversion pressure. Idempotent: registering the same
		/// instance twice registers it once, so a loader that re-runs cannot make one shrine press
		/// twice as hard.
		/// </summary>
		/// <param name="Source">The source. Null is ignored.</param>
		public static void AddPressureSource(IConversionPressure Source)
		{
			if (Source == null || Sources.Contains(Source))
			{
				return;
			}
			Sources.Add(Source);
		}

		/// <summary>Forgets every registered pressure source. For a registry loader re-reading its
		/// streams, and for tests; a save carries none of this.</summary>
		public static void ClearPressureSources()
		{
			Sources.Clear();
		}

		/// <summary>
		/// The kingdom's one attended pass over what its people believe: credits every household's
		/// minority the days they actually spent under its roof, records a brink for anyone who
		/// has reached the end of that road, spends one pass of every standing window, and turns
		/// anyone whose window has run out and whose draw agrees.
		/// <para>
		/// Preconditions: called from the settlement pass, on claimed ground, AFTER
		/// <c>KingdomLodging.OnSettlementPass</c> &mdash; who sleeps where is the input, so a pass
		/// that ran first would read yesterday's households. Side effects: shared living accrues,
		/// a conversion may be recorded in both registers, a settler may leave through
		/// <c>KingdomGrowth.Emigrate</c>. Failure mode: returns having done nothing.
		/// </para>
		/// </summary>
		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || Survey == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			List<GameObject> residents = new List<GameObject>(Survey.CitizenBodies);
			if (residents.Count == 0)
			{
				return;
			}
			long now = (The.Game != null) ? The.Game.TimeTicks : 0L;
			Dictionary<string, List<GameObject>> households = Households(residents);
			// The window first, and osmosis after it. The order no longer decides anything -- the
			// window is anchored at the warning tick, so a brink recorded and warned in this
			// resolve has zero days spent whichever loop looks at it next -- but it is kept,
			// because reading the standing brinks before creating new ones is the order the
			// re-derive-every-pass contract is easiest to check in.
			for (int i = 0; i < residents.Count; i++)
			{
				CreedWindow(System, Z, residents[i], now);
			}
			for (int i = 0; i < residents.Count; i++)
			{
				Osmosis(System, Z, residents[i], households, now);
			}
			for (int i = 0; i < residents.Count; i++)
			{
				Pressure(System, Z, residents[i], now);
			}
			ForgetDeparted(System);
		}

		/// <summary>
		/// Addendum 5's culture channel: the meal the founder just held nudges everyone who sat
		/// down at it toward the table's own majority creed.
		/// <para>
		/// Preconditions: called from <c>KingdomLarder.HoldSharedMeal</c> once the meal has
		/// actually been spent and recorded, so a meal that failed for want of food nudges
		/// nobody. Side effects: shared living accrues for the attendees, and a conversion may be
		/// recorded. Failure mode: returns having done nothing.
		/// </para>
		/// <para>
		/// The table's majority is read with <c>KingdomCreedRules.DominantCreed</c> over the
		/// people actually standing there &mdash; the same rule that decides what a city believes,
		/// asked of one evening &mdash; so a small or evenly split table nudges nobody at all, and
		/// the meal never invents a majority the settlement does not have.
		/// </para>
		/// </summary>
	}
}
