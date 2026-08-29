using System;

namespace ThousandAndFirst
{
	public static partial class KingdomWaterRiteRules
	{

		// ==================================================================================
		// Asked once, and not again until something is different. The rule that keeps this from
		// becoming a button the founder presses every visit until the dice fall right -- and
		// there are no dice here, so pressing it twice against an unchanged settlement would
		// produce the identical refusal at the identical price, which is the definition of a nag.
		// ==================================================================================

		/// <summary>Records what a refusal turned on, so <see cref="SomethingChanged"/> can tell a
		/// second question apart from the same question.</summary>
		/// <param name="Facts">The facts the answer was given against.</param>
		/// <param name="Answer">The answer given.</param>
		public static WaterRiteStamp StampFor(WaterRiteFacts Facts, WaterRiteAnswer Answer)
		{
			return new WaterRiteStamp(
				Answer,
				Clamp(Facts.Hostility, 0, 100),
				Facts.RivalShrine,
				Answer == WaterRiteAnswer.Steadfast,
				NeededDays(Distance(Facts)),
				Facts.RealmCreed);
		}

		/// <summary>
		/// Whether anything has changed since they answered that could honestly change the answer.
		/// False is the ordinary result, and means the Charter shows the row shut with the reason
		/// they gave rather than letting the founder buy the same refusal twice.
		/// <para>
		/// Four doors and no others. The realm believing something else is always a different
		/// question. A quarrel that has eased is a real change. A rival shrine that is gone is a
		/// real change. And a shared life grown long enough to cover the distance the refusal
		/// turned on is a real change &mdash; the only door that opens by itself, and it opens on
		/// attended passes, so it never opens while the founder is away.
		/// </para>
		/// </summary>
		/// <param name="Then">The stamp their refusal left.</param>
		/// <param name="Now">The facts as they stand tonight.</param>
		public static bool SomethingChanged(WaterRiteStamp Then, WaterRiteFacts Now)
		{
			if (!SameCreed(Then.RealmCreed, Now.RealmCreed))
			{
				return true;
			}
			if (Then.Absolute)
			{
				return false;
			}
			if (Clamp(Now.Hostility, 0, 100) < Then.Hostility)
			{
				return true;
			}
			if (Then.RivalShrine && !Now.RivalShrine)
			{
				return true;
			}
			return Then.NeededDays > 0 && Now.SharedDays >= Then.NeededDays;
		}

		/// <summary>Whether two creed faction names are the same belief. Null and empty are one
		/// another and both mean "holds nothing in particular", so a realm that recanted and a
		/// realm that never declared read alike.</summary>
		public static bool SameCreed(string A, string B)
		{
			bool aEmpty = string.IsNullOrEmpty(A);
			bool bEmpty = string.IsNullOrEmpty(B);
			if (aEmpty || bEmpty)
			{
				return aEmpty && bEmpty;
			}
			return string.Equals(A, B, StringComparison.Ordinal);
		}

		/// <summary>
		/// Preserves a rite's independently-derived eligibility across the civic office's
		/// title-only projection. The title grants no service, capability, ritual authority, or
		/// obligation, so holding it cannot open or close the basin.
		/// </summary>
		/// <param name="Baseline">Eligibility derived from creed, consent, road, cadence, and
		/// stores.</param>
		/// <param name="HoldsTitleOnlyOffice">Whether this resident carries the civic title. This
		/// fact is deliberately observational only.</param>
		public static WaterRiteBar PreserveEligibilityAcrossCivicTitle(WaterRiteBar Baseline,
			bool HoldsTitleOnlyOffice)
		{
			return Baseline;
		}

		// ==================================================================================
		// Shared living with the settlement, counted in the days somebody has actually lived
		// here. Deliberately not a date: the roll records the day somebody arrived, and that day
		// is a fact about the calendar rather than about anything shared -- what this counts is
		// how much of this settlement's own life they have been part of.
		// ==================================================================================

		/// <summary>
		/// Shared living after a stretch of days lived here. Stops at
		/// <see cref="MaxCountedDays"/>, which is exactly where <see cref="Reach"/> stops rising,
		/// so the number never grows past the point of meaning anything.
		/// <para>
		/// The clock used to FORBID here and never cause: a settler could be counted at most once
		/// a day, but only an attended pass could count them at all. Addendum 8 clause 1 makes the
		/// days themselves the unit &mdash; a settler goes on living in the settlement whether or
		/// not the founder is standing in it &mdash; and the one-a-day gate survives as arithmetic
		/// rather than as a guard, because a stretch of elapsed time cannot yield more whole days
		/// than it contains.
		/// </para>
		/// <para>
		/// Nothing irreversible hangs off this counter, so it needs no brink of its own. It buys
		/// REACH, which only makes an invitation the founder must still extend and the settler
		/// must still accept more likely to be accepted; the rite's exit is its refusal counter,
		/// and its pressure surface is <c>KingdomConversion</c>'s. One exit, many feeders.
		/// </para>
		/// </summary>
		/// <param name="Held">Days so far. Negative reads as none.</param>
		/// <param name="Days">Whole days lived here since the last count, from
		/// <c>KingdomRules.ElapsedDays</c>. Non-positive changes nothing.</param>
		public static int SharedDaysAfter(int Held, int Days)
		{
			int held = (Held < 0) ? 0 : Held;
			if (Days <= 0)
			{
				return held;
			}
			long total = (long)held + Days;
			return (total >= MaxCountedDays) ? MaxCountedDays : (int)total;
		}

		// ==================================================================================
		// The exit, which is not optional: a settler may always emigrate rather than convert.
		//
		// One invitation is not pressure -- KingdomConversionRules.IsImposed says so, and says
		// it about this channel by name -- so a settler who is asked and says no is simply a
		// settler who said no. Being asked over and over IS pressure, and at the count below the
		// asking stops being a question the founder is allowed to keep putting. From that night
		// the rite is shut to them for as long as the realm holds what it holds, and the shell
		// hands them to KingdomConversion's own pressure surface, which is where every channel's
		// resented departure is named, graced and chronicled. There is one exit in this mod and
		// this file does not build a second one.
		// ==================================================================================

		/// <summary>
		/// Refusals after which the asking closes. Three: enough that a founder who was told "not
		/// yet" is not punished for asking again once something changed, few enough that
		/// persistence has a cost the person paying it can see coming.
		/// </summary>
		public const int RefusalsBeforeAskingCloses = 3;

		/// <summary>Whether a further asking would be one asking too many.</summary>
		public static bool AskedTooOften(int Refusals)
		{
			return Refusals >= RefusalsBeforeAskingCloses;
		}

		/// <summary>Refusals after one more. Clamped at the threshold, because past it the count
		/// stops meaning anything &mdash; the next asking closes the matter either way.</summary>
		public static int RefusalsAfter(int Refusals)
		{
			if (Refusals < 0)
			{
				return 1;
			}
			return (Refusals >= RefusalsBeforeAskingCloses) ? RefusalsBeforeAskingCloses : (Refusals + 1);
		}

		// ==================================================================================
		// The quarter. Addendum 4d's quarters emerge from the layout grammar with no code
		// knowing the word, so this is the only reading of "their quarter" the code can
		// honestly make: the ground within sight of their own door.
		//
		// Narrower than the shrine channel's own scope on purpose. KingdomFaith asks whether a
		// consecrated shrine stands in the SETTLEMENT, because that is a question about a
		// settlement. The rite asks whether one stands where THIS PERSON lives, because that is
		// a question about one person's evening, and a shrine across town is not in their ears
		// when the basin goes down.
		// ==================================================================================

		/// <summary>
		/// Cells from a settler's own door within which a consecrated building is in their quarter.
		/// Twelve: half a zone's twenty-five rows and a sixth of its eighty columns, which is a few
		/// streets rather than a city.
		/// </summary>
		public const int QuarterRadiusCells = 12;

		/// <summary>Whether a cell offset from a settler's door falls inside their quarter.
		/// Chebyshev, matching how the engine measures a neighbourhood on a grid.</summary>
		public static bool WithinQuarter(int DX, int DY)
		{
			int x = (DX < 0) ? -DX : DX;
			int y = (DY < 0) ? -DY : DY;
			return ((x > y) ? x : y) <= QuarterRadiusCells;
		}
	}
}
