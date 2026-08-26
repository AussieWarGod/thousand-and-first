namespace ThousandAndFirst
{
	public static partial class KingdomBrinkRules
	{
		// ==================================================================================
		// Prose. One announce, one coaching clause and one unsaying per kind, so the ledger
		// cannot drift from the chronicle and a test can pin all three. Each consumer supplies
		// the subject and the cause; none of them writes its own sentence.
		// ==================================================================================

		/// <summary>
		/// What the founder would have to DO, named. Rule 3's coaching clause, pulled out of
		/// <see cref="AnnounceNote"/> so it is a surface a test can hold every kind to: a warning
		/// that says only what will be lost is a warning the founder cannot act on, and under
		/// Addendum 10(a) &mdash; where the loss lands whether they are watching or not &mdash;
		/// that is the difference between a fair consequence and an ambush.
		/// </summary>
		/// <param name="Kind">Which irreversible thing is one window away.</param>
		/// <param name="Cause">The creed pulling at them, or the other city. Blank is tolerated.</param>
		public static string ArrestNote(BrinkKind Kind, string Cause)
		{
			switch (Kind)
			{
			case BrinkKind.Creed:
				return "Break the household up or take the shrine out of their quarter and they hold what they held.";
			case BrinkKind.City:
				return "Pour the rite, or settle what the two of them believe, and it holds.";
			default:
				return "Raise something they would take and they stay.";
			}
		}

		/// <summary>
		/// The founder-facing warning, said once, pushed to wherever they are standing
		/// (<c>KingdomWord</c>): who, what is doing it, how long it has really been going on, what
		/// would stop it (<see cref="ArrestNote"/>), and how many days of world time are left.
		/// </summary>
		/// <param name="Kind">Which irreversible thing is one window away.</param>
		/// <param name="Subject">The settler by name, or the city by name.</param>
		/// <param name="Cause">The creed pulling at them, or the other city &mdash; whatever the
		/// founder would have to act on. Blank is tolerated and named vaguely.</param>
		/// <param name="Days">Whole days the brink has stood, from <see cref="DaysStood"/>.</param>
		/// <param name="DaysLeft">World-days left, from <see cref="DaysLeft"/>.</param>
		public static string AnnounceNote(BrinkKind Kind, string Subject, string Cause, int Days, int DaysLeft)
		{
			string who = string.IsNullOrEmpty(Subject) ? "A settler" : Subject;
			string elapsed = ElapsedPhrase(Days);
			string window = WindowPhrase(DaysLeft);
			string arrest = ArrestNote(Kind, Cause);
			switch (Kind)
			{
			case BrinkKind.Creed:
			{
				string creed = string.IsNullOrEmpty(Cause) ? "the creed of the house they sleep in" : Cause;
				return who + " has come to the end of the road toward " + creed + ", " + elapsed
					+ ". " + arrest + " " + window;
			}
			case BrinkKind.City:
			{
				string here = string.IsNullOrEmpty(Subject) ? "the other city" : Subject;
				string kept = string.IsNullOrEmpty(Cause) ? "this one" : Cause;
				return here + " has been at the breaking point with " + kept + " " + elapsed
					+ ". " + arrest + " " + window;
			}
			default:
				return who + " has had no roof in this settlement they would live under, " + elapsed
					+ ". " + arrest + " " + window;
			}
		}

		/// <summary>
		/// The same day as the founder's own book records it: lower-case clause, no trailing
		/// period, because <c>KingdomChronicle.Record</c> dates it and closes it. Written on the
		/// day the word goes out rather than on the day it fires, so the book holds the warning as
		/// well as the loss.
		/// </summary>
		public static string AnnounceTelling(BrinkKind Kind, string Subject, string Cause, int Days)
		{
			string who = string.IsNullOrEmpty(Subject) ? "a settler" : Subject;
			string elapsed = ElapsedPhrase(Days);
			switch (Kind)
			{
			case BrinkKind.Creed:
			{
				string creed = string.IsNullOrEmpty(Cause) ? "the creed of the house they slept in" : Cause;
				return who + " had all but taken up " + creed + ", having been on that road " + elapsed;
			}
			case BrinkKind.City:
			{
				string here = string.IsNullOrEmpty(Subject) ? "the other city" : Subject;
				string kept = string.IsNullOrEmpty(Cause) ? "the seat" : Cause;
				return here + " stood at the breaking point with " + kept + ", and had stood there " + elapsed;
			}
			default:
				return who + " had been sleeping in the open " + elapsed + ", with nowhere here they would live";
			}
		}

		/// <summary>
		/// The unsaying, when the cause is gone before the window is. Said in the same place the
		/// warning was, because a warning that is never withdrawn is a warning the founder stops
		/// believing.
		/// </summary>
		public static string LiftedNote(BrinkKind Kind, string Subject)
		{
			string who = string.IsNullOrEmpty(Subject) ? "A settler" : Subject;
			switch (Kind)
			{
			case BrinkKind.Creed:
				return who + " holds what they held. Whatever was pulling at them is not pulling now.";
			case BrinkKind.City:
			{
				string here = string.IsNullOrEmpty(Subject) ? "The other city" : Subject;
				return here + " has stepped back from the breaking point. Nobody is leaving the realm tonight.";
			}
			default:
				return who + " has a roof again, and is staying.";
			}
		}

		/// <summary>
		/// The push framing: how word out of a settlement the founder is not standing in reads
		/// when it catches up with them. Qud-honest &mdash; somebody walked, or somebody talked,
		/// and the news found them wherever they were.
		/// <para>
		/// Only the FRAMING is conditional. The warning itself is pushed either way, because a
		/// consequence that fires in absence cannot be announced by a note that is only read at
		/// the seat. Standing in the settlement the founder gets the plain line and nothing else,
		/// so nobody is ever told the same thing twice in two voices.
		/// </para>
		/// </summary>
		public static string WordFrom(string CityName, string Line)
		{
			if (string.IsNullOrEmpty(Line))
			{
				return "";
			}
			string from = string.IsNullOrEmpty(CityName) ? "your settlement" : ("{{C|" + CityName + "}}");
			return "Word from " + from + " finds you: " + Line;
		}
	}
}
