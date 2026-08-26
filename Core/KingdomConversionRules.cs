using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// Conversion: the long road back from the fault-line ceiling (Addendum 4d) that partitioned
	/// the city into quarters, and the guard that keeps that road from ever becoming a corridor.
	/// <para>
	/// <b>Two passive channels live here.</b> <i>Osmosis</i> is the household majority pulling at
	/// the minority, counted in SHARED LIVING &mdash; cohabitation days actually spent under one
	/// roof &mdash; and scaled by the closeness ladder, because how much of a difference a
	/// household can cross is a property of its architecture and of nothing else. <i>Culture</i>
	/// is the witnessed shared meal nudging its attendees toward the table's majority: small,
	/// capped at <see cref="MealCeilingPercent"/> of the road, and a free rider on a ceremony the
	/// founder was already holding. Neither is a meter and neither is ever shown as a percentage.
	/// </para>
	/// <para>
	/// <b>People go on living together whether or not anyone is watching (Addendum 8 clause 1).</b>
	/// Osmosis is named in the doctrine by name. Shared living is therefore denominated in
	/// cohabitation TIME &mdash; days two people actually spent under one roof &mdash; and not in
	/// visits, and this file's figures were recalibrated through
	/// <see cref="KingdomBrinkRules.CohabitationDaysPerAttendedPass"/> so that a founder who comes
	/// home at the cadence the design always assumed walks EXACTLY the road they walked before.
	/// Only an absent founder sees any difference, and what they come home to is a brink rather
	/// than a fait accompli.
	/// </para>
	/// <para>
	/// <b>Rates are time x architecture, never time alone (clause 2).</b> The closeness ladder is
	/// the multiplier: a bunk row buys nothing however many days pass in it, and a fine house buys
	/// a third of what a hut does. An unhoused settler cohabits with nobody and accrues nothing at
	/// all, which is the same rule stated from the other side.
	/// </para>
	/// <para>
	/// <b>A settler may always emigrate rather than convert.</b> Osmosis and the shared table are
	/// chosen proximity and generate no pressure at all: living beside someone is not being made
	/// to agree with them, and Addendum 5 wants exactly the Roomed household across an ambient
	/// grudge to blend. What generates pressure is an IMPOSED creed &mdash; a realm declaration
	/// against theirs, a rival shrine consecrated in their quarter &mdash; and a settler who
	/// resents one takes the road instead of the creed: warned once and pushed to the founder
	/// wherever they are (STANDARDS 7b, Addendum 10(a)), given <see cref="ResentedWindowDays"/> of
	/// WORLD TIME for the founder to take the pressure off, and then gone through the settlement's
	/// ordinary emigration, chronicled by name and cause in both registers. The covenant is drunk,
	/// never administered.
	/// </para>
	/// <para>
	/// <b>The end of the road is a BRINK, not a conversion.</b> Reaching
	/// <see cref="SharedLivingForConversion"/> records a creed brink
	/// (<see cref="KingdomBrinkRules"/>) and shared living STOPS ACCRUING there, so a thousand-day
	/// absence and a two-hundred-day one arrive at the same place. The founder is warned once with
	/// the honest elapsed and gets <see cref="KingdomBrinkRules.CreedBrinkWindowDays"/> of world
	/// time to break the household up, rehouse them, or bring another creed's pull to bear &mdash;
	/// and only when that window is spent with the household still pulling is the draw asked at
	/// all. Spent, now, whether or not the founder came back to watch it spend.
	/// </para>
	/// <para>
	/// <b>Rare, and never re-rolled.</b> The draw is <see cref="ConversionChancePercent"/>, taken
	/// through <see cref="CounterRandom"/> on a key that names the settlement, the channel, the
	/// person and which ROAD this is (<see cref="RoadEnd"/>). The same road always answers the
	/// same way, in any process, forever &mdash; a reload never re-rolls a soul, and a founder
	/// cannot save-scum a conversion. A road that answered no is walked again from nothing, and
	/// the next one is a genuinely new question.
	/// </para>
	/// <para>
	/// Engine-free, so the whole of it is tabled. Which people share which roof, and which of them
	/// hold which creed, is <c>KingdomConversion</c>'s to gather.
	/// </para>
	/// </summary>
	public static partial class KingdomConversionRules
	{
		// ==================================================================================
		// Addendum 5, the osmosis channel: shared living, counted in COHABITATION DAYS.
		//
		// The unit used to be a pass -- one arrival of the founder on the settlement's own ground
		// -- and this block used to say, in as many words, that no code path here turned elapsed
		// time into progress. Addendum 8 clause 1 names osmosis as a thing that happens as time
		// passes, so the unit is now a day two people actually spent under one roof, and the
		// founder's presence has nothing to do with it.
		//
		// THE RECALIBRATION, so the change of unit is not a change of pace. Every figure that
		// moved was multiplied by KingdomBrinkRules.CohabitationDaysPerAttendedPass, which is the
		// design's own model of how often a present founder comes home (three days; see that
		// constant for where the number comes from and why it is not a guess). The per-rung rates
		// below did not move at all -- they were per-pass and are now per-day -- and the ROAD
		// moved by exactly the same factor, so every rung lands on the identical wall-clock
		// distance it always had:
		//
		//     hut          24 passes x 3 days = 72 days   ==   216 / 3 per day  = 72 days
		//     stone house  36 passes x 3 days = 108 days  ==   216 / 2 per day  = 108 days
		//     fine house   72 passes x 3 days = 216 days  ==   216 / 1 per day  = 216 days
		//
		// An attentive founder therefore notices nothing. An absent one finds the road walked
		// while they were gone, stopped at its end, waiting to be told about.
		// ==================================================================================

		/// <summary>
		/// The road as it was denominated before the clock rework: seventy-two points of shared
		/// living, bought at up to three a pass. Kept as the INPUT to the recalibration rather
		/// than deleted, so <see cref="SharedLivingForConversion"/> shows its own working and a
		/// playtest that wants a shorter road can move the figure it was actually calibrated in.
		/// </summary>
		public const int SharedLivingInPasses = 72;

		/// <summary>
		/// Shared living one settler must accumulate before the road ends and the brink is
		/// recorded. Two hundred and sixteen: <see cref="SharedLivingInPasses"/> restated in
		/// cohabitation days, which at the ladder below is seventy-two days under a hut's roof, a
		/// hundred and eight under a stone house's, and two hundred and sixteen in quarters of
		/// one's own &mdash; a season of shared living, never a week of it, and that is before the
		/// draw is asked for at all.
		/// </summary>
		public const int SharedLivingForConversion = SharedLivingInPasses * KingdomBrinkRules.CohabitationDaysPerAttendedPass;

		/// <summary>
		/// Shared living a <see cref="KingdomLodgingRules.Closeness.Packed"/> household buys in a
		/// day: nothing, and by author's ruling rather than by arithmetic. One open room refuses
		/// every filed feeling (<c>KingdomLodgingRules.PackedRefusalHostility</c> is one), so the
		/// people in it already agree by construction. There is nothing there to cross, and a
		/// bunk row must never become a cheap conversion engine the founder builds on purpose.
		/// Zero is also the one rung a change of unit cannot move: no number of days multiplied by
		/// nothing is anything.
		/// </summary>
		public const int PackedSharedPerDay = 0;

		/// <summary>
		/// Shared living a <see cref="KingdomLodgingRules.Closeness.Close"/> household buys in a
		/// day: three, the fastest rung there is. A hut is one household's worth of walls and a
		/// door that shuts, and it holds only mild differences (it refuses the ambient grudge), so
		/// what it converts it converts quickly &mdash; seventy-two days of it, which is what
		/// twenty-four visits always bought.
		/// </summary>
		public const int CloseSharedPerDay = 3;

		/// <summary>
		/// Shared living a <see cref="KingdomLodgingRules.Closeness.Roomed"/> household buys in a
		/// day: two. This is the rung Addendum 5 is about &mdash; the stone house is the only
		/// architecture that will hold an ambient grudge under one roof at all, so it is the only
		/// place a real difference gets crossed. Slower than the hut because the walls that make
		/// it possible are also walls: a hundred and eight days.
		/// </summary>
		public const int RoomedSharedPerDay = 2;

		/// <summary>
		/// Shared living a <see cref="KingdomLodgingRules.Closeness.Private"/> household buys in a
		/// day: one. A household that meets at dinner because it chose to converts slowest of
		/// all &mdash; the whole road, two hundred and sixteen days &mdash; which is the honest
		/// reading of quarters of one's own and is also the point: the fine house's value is
		/// quality and notables, never a tool for processing people.
		/// </summary>
		public const int PrivateSharedPerDay = 1;

		/// <summary>
		/// Shared living one witnessed meal bought each attendee before the clock rework: four,
		/// against a road of seventy-two. Kept as the input to the recalibration, so the fact that
		/// nine meals fill the ceiling can be read off the arithmetic instead of trusted.
		/// </summary>
		public const int MealSharedInPasses = 4;

		/// <summary>
		/// Shared living one witnessed meal buys each attendee, before the ceiling: twelve. Small
		/// on purpose &mdash; a meal is a good evening, not a policy, exactly as
		/// <c>KingdomCreedRules.MealEase</c> is smaller than a rite.
		/// <para>
		/// A meal is an EVENT and not a stretch of time, so it did not need recalibrating for its
		/// own sake. It is scaled anyway, by exactly the factor the road was, because the thing
		/// worth holding still is what a meal is WORTH relative to the road: nine meals reached
		/// the ceiling before and nine meals reach it now. Leaving this at four would have
		/// tripled the number of suppers culture costs without anybody deciding to.
		/// </para>
		/// </summary>
		public const int MealShared = MealSharedInPasses * KingdomBrinkRules.CohabitationDaysPerAttendedPass;

		/// <summary>
		/// Share of the road meals may ever carry a settler along: half. Culture nudges;
		/// architecture converts. A settler who never shares a roof with the creed pulling at them
		/// &mdash; unhoused, or in a Packed household &mdash; can be fed to the ceiling and will
		/// stop there for good, which is Addendum 5's ordering ("architecture manages difference;
		/// practice converts it") made unforgeable rather than merely intended.
		/// </summary>
		public const int MealCeilingPercent = 50;

		/// <summary>
		/// Chance, in percent, that reaching a milestone of shared living actually turns somebody.
		/// Forty: rare enough that a conversion is a chronicle entry rather than a schedule, and
		/// short of certain so that the founder who builds the stone house is buying a real
		/// possibility rather than commissioning a result.
		/// </summary>
		public const int ConversionChancePercent = 40;

		/// <summary>
		/// Shared living meals may ever supply, from <see cref="MealCeilingPercent"/> of
		/// <see cref="SharedLivingForConversion"/>.
		/// </summary>
		public static int MealCeiling
		{
			get { return SharedLivingForConversion * MealCeilingPercent / 100; }
		}

		/// <summary>
		/// Shared living one day under one roof buys, against the closeness ladder and the creed
		/// feeling between the two people. The whole of Addendum 8 clause 2 for this channel: the
		/// time term is the day, and the architecture is the multiplier.
		/// <para>
		/// <b>No conversion across a refusal.</b> You do not convert somebody you will not live
		/// beside: a hostility the quarters would refuse buys nothing at all, at every rung. The
		/// lodging pass already refuses to seat such a pair, so this is the same law stated where
		/// it can be tested rather than inferred from an assignment that happens to be correct.
		/// </para>
		/// </summary>
		/// <param name="Quarters">The home's rung, from <c>KingdomLodging.QuartersOf</c>.</param>
		/// <param name="Hostility">0-100 between the settler's creed and the creed pulling at
		/// them, from <c>KingdomCreed.HostilityBetween</c>.</param>
		/// <returns>Points of shared living per cohabited day; zero for Packed quarters and for
		/// any pair the quarters would refuse.</returns>
		public static int SharedLivingPerDay(KingdomLodgingRules.Closeness Quarters, int Hostility)
		{
			if (Hostility >= KingdomLodgingRules.RefusalHostility(Quarters))
			{
				return 0;
			}
			switch (Quarters)
			{
			case KingdomLodgingRules.Closeness.Close:
				return CloseSharedPerDay;
			case KingdomLodgingRules.Closeness.Roomed:
				return RoomedSharedPerDay;
			case KingdomLodgingRules.Closeness.Private:
				return PrivateSharedPerDay;
			default:
				return PackedSharedPerDay;
			}
		}

		/// <summary>
		/// Shared living one witnessed meal buys a settler who is already this far along, clamped
		/// so meals can carry somebody to exactly <see cref="MealCeiling"/> and never one point
		/// past it.
		/// </summary>
		/// <param name="Shared">Shared living already accumulated toward the table's creed.
		/// Negative reads as none.</param>
		/// <returns>Zero once the ceiling is reached, so a settlement that eats every night still
		/// cannot eat its way to a conversion.</returns>
		public static int MealSharedFor(int Shared)
		{
			int held = (Shared < 0) ? 0 : Shared;
			int room = MealCeiling - held;
			if (room <= 0)
			{
				return 0;
			}
			return (room < MealShared) ? room : MealShared;
		}

		/// <summary>
		/// The creed a household holds by majority, which is the creed it pulls its minority
		/// toward.
		/// <para>
		/// A STRICT majority of everyone under the roof, believers and ordinary settlers alike:
		/// two of three, three of five. Not a plurality and not
		/// <c>KingdomCreedRules.DominantSharePercent</c>'s third &mdash; a city is known by what a
		/// third of it holds, but a household only pulls when the people in it are actually
		/// outnumbering somebody. A tie has no winner, which is also the only answer that does not
		/// depend on the order a dictionary enumerates in.
		/// </para>
		/// </summary>
		/// <param name="Counts">Creed name to occupants holding it, over one household. Null,
		/// empty and non-positive entries all read as nobody.</param>
		/// <param name="Household">Everybody under the roof, including the creedless and including
		/// the settler being asked about.</param>
		/// <returns>The majority creed's name, or null when no creed holds one.</returns>
		public static string HouseholdMajority(IDictionary<string, int> Counts, int Household)
		{
			if (Counts == null || Household <= 0)
			{
				return null;
			}
			string best = null;
			foreach (KeyValuePair<string, int> entry in Counts)
			{
				if (string.IsNullOrEmpty(entry.Key) || entry.Value <= 0)
				{
					continue;
				}
				if (entry.Value * 2 > Household)
				{
					best = entry.Key;
				}
			}
			return best;
		}
	}
}
