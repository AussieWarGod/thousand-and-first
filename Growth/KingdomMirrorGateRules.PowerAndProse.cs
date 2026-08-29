using System;

namespace ThousandAndFirst
{
	internal static partial class KingdomMirrorGateRules
	{
		internal const string KeyedRemovalFailureLine =
			"Unkey this mirror-gate before striking or converting it. Its crossing must be released while the arch still stands.";

		internal const string RemovalProofFailureLine =
			"The mirror-gate register cannot prove this arch unkeyed. Repair the register before taking the arch down.";

		/// <summary>Removal is safe only when the exact physical-ground key has no register row.</summary>
		internal static bool MayRemove(KingdomGateRow[] Rows, string Key)
		{
			return !string.IsNullOrEmpty(Key) && IndexOfKey(Rows, Key) < 0;
		}

		/// <summary>
		/// What an open arch owes for a span of days.
		/// <para>
		/// The full elapsed, uncapped (Addendum 8 clause 1, STANDARDS &sect;8): a crossing left open
		/// across a season costs a season, and there is no forgiveness anywhere in it. What bounds
		/// it is not a ceiling but the city's own salt &mdash; nothing can pay a hundred days of
		/// this out of a store built to hold one, so an arch left open across a long absence is
		/// found dark, and one day's works light it again.
		/// </para>
		/// </summary>
		/// <param name="days">Whole world-day boundaries, from
		/// <c>KingdomProductionRules.TryDaysBetween</c>. Zero or fewer owes nothing.</param>
		/// <returns>Charge owed, saturating rather than wrapping.</returns>
		internal static int DrawForDays(long days)
		{
			if (days <= 0L)
			{
				return 0;
			}
			long owed = days * OpenChargePerDay;
			return (owed > int.MaxValue || owed < 0L) ? int.MaxValue : (int)owed;
		}

		/// <summary>
		/// Whether the works paid what the arch owed.
		/// <para>
		/// A span that crossed no day boundary decides nothing &mdash; an arch is not closed by
		/// being looked at between days &mdash; which is why this has three answers and not two.
		/// </para>
		/// </summary>
		/// <param name="owedCharge">From <see cref="DrawForDays"/>.</param>
		/// <param name="availableCharge">What the arch can actually draw on, measured off the real
		/// vessels rather than assumed (STANDARDS &sect;1).</param>
		internal static KingdomGateHold JudgeHold(int owedCharge, int availableCharge)
		{
			if (owedCharge <= 0)
			{
				return KingdomGateHold.Unchanged;
			}
			return (availableCharge >= owedCharge) ? KingdomGateHold.Held : KingdomGateHold.Lost;
		}

		/// <summary>
		/// The sentence a refused keying is told with. Every one names the fix, and none of them
		/// says "that failed" (STANDARDS 7b).
		/// <para>
		/// Empty for the three verdicts that are not refusals: nothing has gone wrong, and 7b
		/// forbids telling somebody about the absence of a problem.
		/// </para>
		/// </summary>
		/// <param name="verdict">What <see cref="TryDedicate"/>, <see cref="TryPair"/> or
		/// <see cref="TryRelease"/> answered.</param>
		/// <param name="city">The city already keeping an arch, where the verdict names one.</param>
		internal static string RefusalLine(KingdomGateVerdict verdict, string city)
		{
			switch (verdict)
			{
			case KingdomGateVerdict.RefusedCityKeyed:
				return "The arch at " + Named(city) + " already answers for that city. Release it, and this one can take its place.";
			case KingdomGateVerdict.RefusedAlreadyKeyed:
				return "This arch is already keyed.";
			case KingdomGateVerdict.RefusedUnkeyed:
				return "This arch was never keyed, so there is nothing to give up.";
			case KingdomGateVerdict.RefusedFull:
				return "The realm's register will carry no more arches. Release one you no longer cross.";
			case KingdomGateVerdict.RefusedNamed:
				return "This ground cannot be written down, so the crossing cannot be kept honestly. Raise the arch somewhere the realm can name.";
			default:
				return "";
			}
		}

		/// <summary>What the founder is told when this end is keyed and nothing answers it yet.</summary>
		internal static string OfferedLine(string city)
		{
			return "The arch is keyed to " + Named(city) + ". Raise its twin in another of your cities and key that one, and the two become one place.";
		}

		/// <summary>What the founder is told the moment a crossing exists.</summary>
		internal static string JoinedLine(string here, string there)
		{
			return Named(here) + " and " + Named(there) + " stand one step apart.";
		}

		/// <summary>The same moment, dated, for the chronicle.</summary>
		internal static string JoinedTelling(string here, string there)
		{
			return "the arch at " + Named(here) + " found its twin at " + Named(there) + ", and the road between them stopped being a road";
		}

		/// <summary>What the founder is told when a crossing is given up. Said plainly: the arch is
		/// not touched, and a founder who has just spent a hundred and twenty drams on one needs to
		/// hear that in the same breath.</summary>
		internal static string ReleasedLine(string city)
		{
			return "The arch at " + Named(city) + " is unkeyed. It stands exactly where it stands; the crossing simply no longer answers.";
		}

		/// <summary>What the city at the other end is left with. Told once, because a founder who
		/// unkeys one end has silently unkeyed two.</summary>
		internal static string OrphanedLine(string city)
		{
			return "The arch at " + Named(city) + " is left waiting. Nothing answers it now.";
		}

		/// <summary>
		/// STANDARDS 7b, for the one stall this building can have: the works could not hold it
		/// open. Names the arrest rather than only the doom.
		/// </summary>
		internal static string WentDarkLine(string city)
		{
			return "The arch at " + Named(city) + " has gone dark. It draws " + OpenChargePerDay
				+ " charge a day to stand open and the works could not pay it; another wheel, or a deeper bed of salt, and it lights again.";
		}

		/// <summary>The same, dated, for the chronicle.</summary>
		internal static string WentDarkTelling(string city, string realm)
		{
			return "the arch at " + Named(city) + " went dark, and " + Named(realm) + " was two journeys wide again";
		}

		/// <summary>The line the arch carries in its own description, so the state of the crossing
		/// is legible where a founder actually looks for it.</summary>
		/// <param name="keyed">Whether this arch is in the register at all.</param>
		/// <param name="answering">The city at the other end, or null when nothing answers.</param>
		/// <param name="dark">Whether the works failed to hold it open.</param>
		internal static string DescriptionLine(bool keyed, string answering, bool dark)
		{
			if (!keyed)
			{
				return "\n{{rules|Unkeyed. It draws nothing and goes nowhere.}}";
			}
			if (string.IsNullOrEmpty(answering))
			{
				return "\n{{rules|Keyed, and waiting for a twin in another city. It draws nothing until one answers.}}";
			}
			if (dark)
			{
				return "\n{{r|Dark. It answers " + Named(answering) + " when the works can pay the " + OpenChargePerDay + " charge a day it wants.}}";
			}
			return "\n{{G|Open onto " + Named(answering) + ", and drawing " + OpenChargePerDay + " charge a day to stay that way.}}";
		}

		/// <summary>
		/// What the founder is asked before a keying, and it is the whole of the cost, disclosed
		/// before anything is committed &mdash; the shape the Charter's own dedications keep.
		/// </summary>
		internal static string DedicationPrompt(string city)
		{
			return "Key this arch for " + Named(city) + "?\n\nWhile it answers another city it draws {{C|" + OpenChargePerDay
				+ "}} charge a day from this city's works, whether anyone crosses or not. Crossing costs nothing beyond that. When the works cannot pay it the arch goes dark, and lights again the day they can.";
		}

		/// <summary>A city as a founder would say it, or an honest word when the realm never named
		/// one.</summary>
		internal static string Named(string city)
		{
			return string.IsNullOrEmpty(city) ? "the city" : city.Trim();
		}
	}
}
