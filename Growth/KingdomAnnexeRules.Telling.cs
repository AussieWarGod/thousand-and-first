using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomAnnexeRules
	{

		/// <summary>The two-answer consent prompt. There is no third answer here and there should
		/// not be: the lab's "never offer this again" belongs to a list of procedures a founder
		/// scrolls past, and this is one act at one building they walked to on purpose.</summary>
		public static readonly string[] ConsentOptions = new string[2]
		{
			"Enter them on the rolls.",
			"Not today."
		};

		// --- What is said afterward ----------------------------------------------------------------

		/// <summary>What the founder is told the moment the register closes.</summary>
		public static string DoneLine(string Who, string CityName)
		{
			return "{{G|" + Named(Who, "They") + " is on the rolls of " + Named(CityName, "this city")
				+ ". The machines will not ask again.}}";
		}

		/// <summary>The same moment, for the chronicle.</summary>
		public static string DoneTelling(string Who, string CityName)
		{
			return Named(CityName, "the city") + " wrote " + Named(Who, "one of its own")
				+ " into its own rolls, and decided for itself who may be counted";
		}

		/// <summary>
		/// What is said when a roll lapses because the city that kept it is no longer the realm's.
		/// <para>
		/// STANDARDS 7b's applicable-but-blocked case and the single most important sentence this
		/// system can say: a founder whose nook silently stops opening has been handed a bug. The
		/// second half is not decoration &mdash; it is the &sect;1.5 promise being kept out loud.
		/// </para>
		/// </summary>
		public static string LapseLine(string CityName)
		{
			return "{{r|The rolls of " + Named(CityName, "your city")
				+ " are not yours to be on any more, and the nooks have gone back to asking.}} What was fitted to you stays fitted. Nothing was taken out.";
		}

		/// <summary>The same moment, for the chronicle.</summary>
		public static string LapseTelling(string CityName)
		{
			return "the book at " + Named(CityName, "the city") + " left with the city that kept it, and the old machines began asking again";
		}

		// --- The register screen -------------------------------------------------------------------

		/// <summary>The register's own heading.</summary>
		public static string RegisterTitle(string CityName)
		{
			return "the rolls of " + Named(CityName, "this city");
		}

		/// <summary>
		/// The two lines above the list: who keeps the book, and how many names are in it. Both
		/// are facts a founder would otherwise have to go and count.
		/// </summary>
		/// <param name="Keeper">Whoever is lodged at the register, or null when nobody is.</param>
		/// <param name="Count">Names on this city's rolls.</param>
		public static string RegisterIntro(string Keeper, int Count)
		{
			StringBuilder text = new StringBuilder();
			if (string.IsNullOrEmpty(Keeper))
			{
				// 7b: an annexe with nobody in it will write nothing, ever, and that is the single
				// most important thing on this screen.
				text.Append("{{r|Nobody is at the register. The annexe writes no names until somebody who has had it done to themselves lives in this city.}}");
			}
			else
			{
				text.Append("at the register: {{W|").Append(Keeper).Append("}}");
			}
			text.Append("\nnames in the book: ");
			text.Append((Count > 0) ? ("{{C|" + Count + "}}") : "{{K|none}}");
			text.Append("\n{{K|").Append(Charter).Append("}}");
			return text.ToString();
		}

		/// <summary>One row of the register: a person, and whether the book still holds them.</summary>
		/// <param name="Who">The person, as the founder reads them.</param>
		/// <param name="Held">Whether the realm still keeps their roll.</param>
		public static string RegisterRow(string Who, bool Held)
		{
			return Named(Who, "somebody") + "  "
				+ (Held ? "{{green|[þ]}} {{K|on the rolls}}" : "{{red|[X]}} {{K|the book that held them is gone}}");
		}

		/// <summary>The line a city's own book carries about its rolls. Rendered rather than
		/// stored, so nothing anywhere has to keep it in step.</summary>
		public static string RollsLine(int Count)
		{
			if (Count <= 0)
			{
				return "{{K|This city keeps no rolls.}}";
			}
			return "{{W|This city keeps its own rolls, and there " + ((Count == 1) ? "is {{C|1}} name" : ("are {{C|" + Count + "}} names"))
				+ " in them.}}";
		}

		// --- Creed friction (F4: the debt, END-STATE §2.4) ------------------------------------------
		//
		// The trigger arithmetic is KingdomLabRules.SpeaksAgainstHall, consumed rather than copied:
		// a tenth of the city, a minority rather than a majority, and once is the whole of it. What
		// is different here is WHO speaks. The lab's petitioner is offended BY the act; the annexe's
		// is of the creed the act belongs to and minds the manner of it -- the Mechanimists hold
		// chrome as a debt owed to Shekhinah (B/Books.xml:165,170,171), not as a purchase, and a
		// city handing chrome out on its own authority has not settled anything with anybody.

		/// <summary>
		/// The creed that holds the debt, and therefore the creed the petitioner speaks for.
		/// <para>
		/// The Mechanimists, who are "mainly comprised of mutant humanoids" and whose own liturgy
		/// is a liturgy of chrome as an obligation: <i>"Unburden yourself from the weight of your
		/// chrome guilt"</i>, <i>"Repay that debt, lightseeker! Offer your chrome to Shekhinah!"</i>
		/// (<c>B/Books.xml:165,170</c>). They are the one people in Qud for whom the annexe is
		/// neither transgression nor novelty &mdash; it is an unsettled account.
		/// </para>
		/// </summary>
		public const string Creditors = "Mechanimists";

		/// <summary>What the petitioner is waiting to speak about.</summary>
		public static string SpokenAboutSubject()
		{
			return "the debt on the chrome";
		}

		/// <summary>
		/// What they actually say, and there is no correct answer to it. The founder's call,
		/// exactly as DIVERSITY &sect;3.6 asks: friction is named people and placement, never a
		/// meter.
		/// </summary>
		/// <param name="Creed">The creed the speaker holds, as the founder reads it.</param>
		public static string SpokenAboutSpeech(string Creed)
		{
			return "\"I am not here to argue that it should not be done. I have chrome in me and I am glad of it. But chrome is borrowed, and "
				+ Named(Creed, "my people")
				+ " teach that what is borrowed is repaid — down the well, at the Heart, in front of somebody. Your annexe writes a name in a book and calls the matter closed. "
				+ "I would like you to say, out loud, who you think the city owes for what it is handing out.\"";
		}

		/// <summary>The deed, for the chronicle, when the founder answers.</summary>
		public static string SpokenAboutDeed(string Name)
		{
			return "the debt on the chrome of " + Named(Name, "the realm") + " was named out loud, in front of the people who believe it is owed";
		}

		// --- Shared -------------------------------------------------------------------------------

		/// <summary>A name as a founder would say it, or an honest word when nothing named one.
		/// The lab's <c>Named</c> one lane over, with a caller-chosen fallback because a person
		/// and a procedure do not degrade to the same word.</summary>
		public static string Named(string Text, string Fallback)
		{
			return string.IsNullOrEmpty(Text) ? Fallback : Text.Trim();
		}
	}
}
