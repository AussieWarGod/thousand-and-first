using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomCrownRules
	{
		// --- What the founder is told -------------------------------------------------------------
		//
		// Every refusal names the act that lifts it and none of them says "that failed" (STANDARDS
		// 7b). The two events name what they cost before they are committed, which is the Charter's
		// own dedication grammar: disclose the whole price, ask, then act.

		/// <summary>The sentence a refused taking-up is told with. Empty for the three verdicts that
		/// are not refusals, because 7b forbids telling somebody about the absence of a problem.</summary>
		public static string RefusalLine(KingdomCrownVerdict Verdict)
		{
			switch (Verdict)
			{
			case KingdomCrownVerdict.RefusedUnfounded:
				return "You rule nothing yet, and a crown over nothing is a hat.";
			case KingdomCrownVerdict.RefusedNotOurGround:
				return "The crown is set down on the kingdom's own ground, not in other people's houses.";
			case KingdomCrownVerdict.RefusedNotOurWork:
				return "The settlement never raised this hall. Adopt it first, and then the crown can come to it.";
			case KingdomCrownVerdict.RefusedNamed:
				return "This city cannot be written into the realm's own record, so the crown could not honestly be said to be here. Name the city something the record can carry.";
			default:
				return "";
			}
		}

		/// <summary>What the founder is told when the crown is already in this city. Not a refusal:
		/// there is nothing wrong, and the sentence says so rather than opening a question whose
		/// only answer is yes.</summary>
		public static string AlreadyHereLine(string City)
		{
			return Named(City) + " is already the capital. The crown is set down in this hall and there is nothing here to decide.";
		}

		/// <summary>
		/// The whole cost of a first crowning, disclosed before anything is committed. It is
		/// deliberately short, because a first crowning costs nothing that was not already spent
		/// raising the hall.
		/// </summary>
		public static string CrownPrompt(string City)
		{
			return "Set the crown down at " + Named(City) + "?\n\n"
				+ "This city becomes the capital of the realm and stays the capital for as long as this hall stands and the crown is in it. "
				+ "Structures only a capital may raise become buildable here. If your cities keep arches, they will be re-keyed to answer this one.\n\n"
				+ "Nothing is spent. The hall is the price, and it is already paid.";
		}

		/// <summary>
		/// The whole cost of a MOVE, and it is the one prompt in this file that has to be honest
		/// about a loss. The second raising is already sunk by the time this is read; what is not
		/// yet spent is the crossing topology, and a founder who has built a two-city crossing is
		/// owed that sentence before they answer, not after.
		/// </summary>
		public static string MovePrompt(string From, string To)
		{
			return "Move the crown from " + Named(From) + " to " + Named(To) + "?\n\n"
				+ "{{W|" + Named(To) + "}} becomes the capital. {{y|" + Named(From) + "}} stops being one: its hall stands exactly where it stands and nothing is taken down, "
				+ "but the structures only a capital may raise can no longer be begun there, and any that stand there stand as what a former capital keeps.\n\n"
				+ "{{r|Every arch the realm keeps is re-keyed to answer " + Named(To) + ".}} A crossing you built between two other cities will land at the new capital instead. "
				+ "No arch is taken down and no arch goes dark; they simply answer somewhere else from the moment you say yes.";
		}

		/// <summary>What the founder is told the moment a realm gets its first capital.</summary>
		public static string CrownedLine(string City)
		{
			return "{{W|The crown is set down at " + Named(City) + ". The realm has a capital.}}";
		}

		/// <summary>The same moment, dated, for the chronicle.</summary>
		public static string CrownedTelling(string City, string Realm)
		{
			return "the crown was set down at " + Named(City) + ", and " + Named(Realm) + " had a capital for the first time";
		}

		/// <summary>What the founder is told the moment a capital moves.</summary>
		public static string MovedLine(string From, string To)
		{
			return "{{W|The crown is taken up out of " + Named(From) + " and set down at " + Named(To) + ".}}";
		}

		/// <summary>The same moment, dated, for the chronicle.</summary>
		public static string MovedTelling(string From, string To)
		{
			return "the crown left " + Named(From) + " for " + Named(To) + ", and the hall it came out of kept its stone and lost its name";
		}

		/// <summary>
		/// STANDARDS 7b and the protection law in one sentence: the old hall is not destroyed, not
		/// moved, and not taken back &mdash; it is DESIGNATED, and the founder is told what it is
		/// now so they never go looking for the thing it used to do.
		/// </summary>
		public static string FormerCrownLine(string City)
		{
			return "{{y|The hall at " + Named(City) + " is a former crown hall. It stands whole and it is nobody's to pull down; it simply holds nothing now.}}";
		}

		/// <summary>
		/// What the founder is told when the realm's record named a capital whose hall is no longer
		/// standing. Said once, at the moment the realm notices, because there is no other moment at
		/// which they would find out.
		/// </summary>
		public static string StruckLine(string City)
		{
			return "{{r|" + Named(City) + " keeps no crown hall any more, so the realm has no capital. Raise a crown hall and set the crown down in it; the arches go on answering wherever they last answered.}}";
		}

		/// <summary>The same, for a realm that found the crown standing somewhere its own record did
		/// not expect. A repair, told plainly rather than performed in silence.</summary>
		public static string RepairedLine(string City)
		{
			return "{{y|The realm's record of its crown was out of step with its halls. The crown stands at " + Named(City) + ", and that is what the record says now.}}";
		}

		/// <summary>The line a crown hall carries in its own description, so which hall holds the
		/// crown is legible where a founder actually looks for it.</summary>
		/// <param name="Holds">Whether the crown is set down in THIS hall.</param>
		/// <param name="Capital">The city that keeps the crown, or null when none does.</param>
		public static string DescriptionLine(bool Holds, string Capital)
		{
			if (Holds)
			{
				return "\n{{W|The crown is set down here. This city is the capital of the realm.}}";
			}
			if (string.IsNullOrEmpty(Capital))
			{
				return "\n{{rules|Empty. Set the crown down here and this city becomes the capital.}}";
			}
			return "\n{{K|A former crown hall. The crown is at " + Named(Capital) + ".}}";
		}

		/// <summary>What the action reads as in the list. The state is in the label, so a founder
		/// never presses a thing that cannot work and then reads why.</summary>
		public static string TakeUpLabel(bool Holds, string Capital)
		{
			if (Holds)
			{
				return "{{K|set the crown down here}} [already here]";
			}
			return string.IsNullOrEmpty(Capital) ? "set the crown down here" : "move the crown here";
		}

		/// <summary>The line a city's own book carries about the crown. Rendered rather than stored,
		/// so nothing anywhere has to keep it in step.</summary>
		public static string CapitalLine(bool Here, string Capital)
		{
			if (Here)
			{
				return "{{W|The crown is here. This is the capital.}}";
			}
			return string.IsNullOrEmpty(Capital)
				? "{{K|The realm has no capital.}}"
				: ("{{K|The capital is " + Named(Capital) + ".}}");
		}

		/// <summary>A city as a founder would say it, or an honest word when the realm never named
		/// one.</summary>
		public static string Named(string City)
		{
			return string.IsNullOrEmpty(City) ? "the city" : City.Trim();
		}
	}
}
