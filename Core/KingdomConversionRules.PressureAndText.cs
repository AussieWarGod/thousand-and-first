using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomConversionRules
	{
		// ==================================================================================
		// The exit: a settler may always emigrate rather than convert.
		//
		// Shaped exactly like Addendum 4b's housing brink, and for the same reason -- warned once
		// by name and PUSHED to wherever the founder is, counted in WORLD-DAYS from that warning
		// and in nothing else, ended by the founder acting or by the settler walking out through
		// the emigration the settlement already has. Absence spends this window like any other
		// (Addendum 10(a)); what absence cannot do is start one, because an entry that carries no
		// warning day has no deadline.
		//
		// What generates pressure is deliberately narrow. Osmosis and the shared table are
		// chosen proximity: Addendum 5 wants the Roomed household across an ambient grudge to
		// blend, and a household that could push somebody out for living in it would make the
		// healing arc into the thing it was written against. An IMPOSED creed is different --
		// the founder declared it, or consecrated it in somebody's quarter -- and that is the
		// one a settler is owed an exit from.
		// ==================================================================================

		/// <summary>
		/// Whether a channel imposes its creed rather than offering it, which is the whole of
		/// which channels a settler may resent.
		/// </summary>
		/// <param name="Channel">The channel.</param>
		/// <returns>True for <see cref="ConversionChannel.Shrine"/>, which is consecrated over a
		/// quarter whether or not the quarter asked. False for
		/// <see cref="ConversionChannel.Osmosis"/> and <see cref="ConversionChannel.Culture"/>,
		/// which are people living and eating together, and false for
		/// <see cref="ConversionChannel.Diplomacy"/>, which is invited and consented to one at a
		/// time.</returns>
		public static bool IsImposed(ConversionChannel Channel)
		{
			return Channel == ConversionChannel.Shrine;
		}

		/// <summary>
		/// Hostility at which a settler resents a creed being imposed on them: fifty, the ambient
		/// grudge fifty-three faction pairs hold toward everyone they have not troubled to name,
		/// and the same feeling a hut refuses to sleep on
		/// (<c>KingdomLodgingRules.CloseRefusalHostility</c>). Below it they merely differ, and
		/// people who merely differ do not leave over a shrine.
		/// </summary>
		public const int ResentmentHostility = 50;

		/// <summary>
		/// World-days a settler under a creed they resent is given before they go, counted from
		/// the day the word reached the founder. Eighteen: three times
		/// <c>KingdomLodgingRules.GraceDays</c>, because a roof is tonight's problem and a creed is
		/// a life's, and the founder's answer here is a thing they must unsay or deconsecrate
		/// rather than a bunk they can raise on the spot. Derived from
		/// <see cref="KingdomBrinkRules.CreedBrinkWindowDays"/>, which is where that ruling now
		/// lives and which the end-of-the-road brink shares, so the two ways a settler can be one
		/// window from losing their creed cannot drift apart.
		/// </summary>
		public const int ResentedWindowDays = KingdomBrinkRules.CreedBrinkWindowDays;

		/// <summary>
		/// The stored day of a settler nobody has warned the founder about yet. Zero, which is
		/// what an absent map entry already reads as, so "not being pressed" and "pressed and
		/// never warned" cannot be told apart by accident &mdash; the entry's PRESENCE is the
		/// pressure and its VALUE is the warning.
		/// </summary>
		public const int NotWarned = 0;

		/// <summary>Whether an imposed creed is one this settler resents.</summary>
		/// <param name="Hostility">0-100 between the settler's own creed and the creed being
		/// imposed, from <c>KingdomCreed.HostilityBetween</c>. A creedless settler and a settler
		/// who already holds the imposed creed both read zero and resent nothing.</param>
		public static bool Resents(int Hostility)
		{
			return Hostility >= ResentmentHostility;
		}

		/// <summary>
		/// Whether a settler's window under a resented creed is spent and they leave now: a whole
		/// <see cref="ResentedWindowDays"/> of world time since the day the founder was warned.
		/// <para>
		/// Days rather than passes, and false for anybody at <see cref="NotWarned"/> however long
		/// the pressure has stood. That refusal is the load-bearing half: the founder's clock
		/// starts when they are TOLD, so a settler pressed all through an absence is warned on the
		/// pass that finds them and still gets the whole window from there.
		/// </para>
		/// </summary>
		/// <param name="WarnedDay">The world day the word went out, from
		/// <c>KingdomBrinkRules.DayNumber</c>. <see cref="NotWarned"/> means it has not.</param>
		/// <param name="NowDay">Today, from the same helper.</param>
		public static bool ResentmentRunOut(int WarnedDay, int NowDay)
		{
			return WarnedDay > NotWarned && NowDay - WarnedDay >= ResentedWindowDays;
		}

		/// <summary>World-days the founder has left to take a resented creed off somebody. The
		/// whole window for a settler nobody has been warned about, because their window has not
		/// started.</summary>
		public static int ResentmentDaysLeft(int WarnedDay, int NowDay)
		{
			if (WarnedDay <= NotWarned)
			{
				return ResentedWindowDays;
			}
			int left = ResentedWindowDays - (NowDay - WarnedDay);
			return (left > 0) ? left : 0;
		}

		/// <summary>The cause a conversion-pressure departure is chronicled and noted under, in
		/// both registers. Named here rather than written at the call site so the chronicle and
		/// the ledger cannot drift apart, and so a test can pin it.</summary>
		public const string DepartureCause = "rather than take a creed they never chose";

		// ==================================================================================
		// Prose. Two registers that disagree where the day is contested, per the pillar: the
		// founder's book says somebody took the water, and the roads say somebody was bought.
		// ==================================================================================

		/// <summary>
		/// Hostility between the creed left and the creed taken at or above which the world argues
		/// about the day: fifty. The same number as <see cref="ResentmentHostility"/> and a
		/// different question &mdash; that one asks whether a person will stand for it, this one
		/// asks whether anybody else will believe it was freely done.
		/// </summary>
		public const int ContestedHostility = 50;

		/// <summary>Whether the two registers should tell this conversion differently.</summary>
		/// <param name="Hostility">0-100 between the creed left behind and the creed taken.
		/// A settler who held nothing before reads zero: nobody contests a conversion from
		/// nothing.</param>
		public static bool Contested(int Hostility)
		{
			return Hostility >= ContestedHostility;
		}

		/// <summary>
		/// The day as the founder's own book records it: lower-case clause, no trailing period,
		/// because the chronicle dates it and closes it.
		/// </summary>
		/// <param name="Channel">Which channel turned them, which picks the words.</param>
		/// <param name="ResidentName">The person, by name.</param>
		/// <param name="CreedDisplayName">The creed taken, as the founder reads it
		/// (<c>KingdomCreed.CreedName</c>).</param>
		public static string ConversionTelling(ConversionChannel Channel, string ResidentName, string CreedDisplayName)
		{
			string who = string.IsNullOrEmpty(ResidentName) ? "a settler" : ResidentName;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "the creed of the house they slept in" : CreedDisplayName;
			switch (Channel)
			{
			case ConversionChannel.Culture:
				return who + " came to hold with " + creed + ", having eaten at their table often enough to be counted at it";
			case ConversionChannel.Shrine:
				return who + " took the water at the shrine and holds with " + creed;
			case ConversionChannel.Diplomacy:
				return who + " took the water rite and holds with " + creed;
			default:
				return who + " came to hold with " + creed + ", after a long season of sleeping under their roof";
			}
		}

		/// <summary>
		/// The same day as the roads tell it. Third person already, because the rumour register is
		/// not a translation of the founder's account but a rival to it. Handed to
		/// <c>KingdomChronicle.RecordDisputed</c> only when <see cref="Contested"/>; an uncontested
		/// conversion is derived the ordinary way, because the world has no reason to argue about
		/// somebody who held nothing changing their mind.
		/// </summary>
		/// <param name="Channel">Which channel turned them.</param>
		/// <param name="ResidentName">The person, by name.</param>
		/// <param name="CreedDisplayName">The creed taken.</param>
		public static string ConversionRumour(ConversionChannel Channel, string ResidentName, string CreedDisplayName)
		{
			string who = string.IsNullOrEmpty(ResidentName) ? "a settler" : ResidentName;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "whoever was paying" : CreedDisplayName;
			switch (Channel)
			{
			case ConversionChannel.Culture:
				return who + " was bought with a good supper, and " + creed + " did not have to ask twice";
			case ConversionChannel.Shrine:
				return who + " was bought, and the shrine of " + creed + " did the buying";
			case ConversionChannel.Diplomacy:
				return who + " was bought, and the founder poured the water themselves";
			default:
				return who + " was talked around by the people they had been made to sleep beside";
			}
		}

		/// <summary>The founder-facing ledger note for a conversion: one line, said plainly,
		/// because this is news and not an alarm.</summary>
		/// <param name="ResidentName">The person, by name.</param>
		/// <param name="CreedDisplayName">The creed taken.</param>
		public static string ConversionNote(string ResidentName, string CreedDisplayName)
		{
			string who = string.IsNullOrEmpty(ResidentName) ? "A settler" : ResidentName;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "the creed of the house they sleep in" : CreedDisplayName;
			return who + " holds with " + creed + " now.";
		}

		/// <summary>
		/// The day a settler decided they would rather go, as the founder's book records it:
		/// lower-case clause, no trailing period. Written on the pass the pressure is first
		/// noticed and not on the pass they leave, so the founder hears about it while there is
		/// still something to do (STANDARDS 7b).
		/// </summary>
		/// <param name="ResidentName">The person, by name.</param>
		/// <param name="CreedDisplayName">The creed being imposed on them.</param>
		public static string PressureTelling(string ResidentName, string CreedDisplayName)
		{
			string who = string.IsNullOrEmpty(ResidentName) ? "a settler" : ResidentName;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "the creed the realm had taken up" : CreedDisplayName;
			return who + " would not be made to hold with " + creed + ", and began asking after the roads";
		}

		/// <summary>
		/// The same, as the founder can act on it. Says what would stop it, because a line that
		/// only reports is a line that stalls in silence.
		/// </summary>
		/// <param name="ResidentName">The person, by name.</param>
		/// <param name="CreedDisplayName">The creed being imposed on them.</param>
		public static string PressureNote(string ResidentName, string CreedDisplayName)
		{
			string who = string.IsNullOrEmpty(ResidentName) ? "A settler" : ResidentName;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "the creed the realm has taken up" : CreedDisplayName;
			return who + " will not be made to hold with " + creed + ". Take it back out of their quarter and they stay; leave it standing and they take the road.";
		}

		/// <summary>
		/// The one sentence the founder is owed when the grace has run out and they are going
		/// (STANDARDS 7b). Names the person and the cause and nothing else; the departure itself
		/// is chronicled by the emigration machinery under <see cref="DepartureCause"/>.
		/// </summary>
		public static string LeavingLine(string ResidentName)
		{
			string who = string.IsNullOrEmpty(ResidentName) ? "A settler" : ResidentName;
			return who + " waited out the grace under a creed they never chose, and is leaving.";
		}
	}
}
