using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomQolRules
	{
		[System.Obsolete("Retired before public release; use KingdomLodgingRules.Conflicts with an explicit closeness rung.", true)]
		public static QolVerdict JudgeCohabitation(QolProfile Profile, string[] TheirHousehold, int CreedHostility, out string Tag)
		{
			Tag = "";
			if (CreedHostility >= 100)
			{
				return QolVerdict.Refused;
			}
			QolVerdict verdict = Judge(TheirHousehold, Profile, out Tag);
			// A neighbour is not a landlord: their household cannot supply what this person needs,
			// so an unmet need says nothing at all about whether the two can live together.
			if (verdict == QolVerdict.NeedUnmet)
			{
				Tag = "";
				return QolVerdict.Match;
			}
			return verdict;
		}

		// --- Saying so (STANDARDS 7b) -----------------------------------------------------------

		/// <summary>
		/// One tag as a sentence can hold it. Our own six read as plain English; anything else
		/// &mdash; another mod's, or one of ours a later wave adds &mdash; is quoted verbatim,
		/// which is both honest and the only thing that could be right about a word this file has
		/// never heard.
		/// </summary>
		public static string TagPhrase(string Tag)
		{
			switch (Fold(Tag))
			{
			case TagCharge:
				return "place to draw charge";
			case TagOpenWater:
				return "open water at the door";
			case TagDamp:
				return "damp";
			case TagDark:
				return "shade from the sun";
			case TagSky:
				return "sky overhead";
			case TagQuiet:
				return "quiet";
			default:
				return string.IsNullOrEmpty(Fold(Tag)) ? "what is wanted" : ("\"" + Fold(Tag) + "\"");
			}
		}

		/// <summary>The same tag from the other side, for a thing somebody will not live beside.
		/// </summary>
		public static string TagObjection(string Tag)
		{
			switch (Fold(Tag))
			{
			case TagCharge:
				return "the hum of the cradles";
			case TagOpenWater:
				return "the standing water";
			case TagDamp:
				return "the damp";
			case TagDark:
				return "the dark";
			case TagSky:
				return "the open sky";
			case TagQuiet:
				return "the quiet";
			default:
				return string.IsNullOrEmpty(Fold(Tag)) ? "what is there" : ("\"" + Fold(Tag) + "\"");
			}
		}

		/// <summary>
		/// The author's own sentence for a refusal, and the shortest true thing this system can
		/// say: <c>Vashti will not sleep beside the fungal cellar.</c> Names the person and the
		/// place, and nothing else.
		/// </summary>
		/// <param name="Who">The resident's name. Blank becomes "the newcomer".</param>
		/// <param name="Where">What they will not sleep beside. Blank becomes "what stands
		/// there".</param>
		public static string WillNotSleepBeside(string Who, string Where)
		{
			return Person(Who) + " will not sleep beside " + Place(Where) + ".";
		}

		/// <summary>
		/// The one sentence a refused match owes the founder. Every line names the person, the
		/// place, and the tag that decided it, and a line about an unmet need also names what would
		/// lift it &mdash; because a settler who quietly never moves in, for a reason nobody can
		/// see, is the exact failure STANDARDS 7b exists to prevent.
		/// </summary>
		/// <param name="Verdict">From <see cref="Judge"/>.</param>
		/// <param name="Who">The resident's name.</param>
		/// <param name="Where">The building, quarters, or household.</param>
		/// <param name="Tag">The tag <see cref="Judge"/> named. Empty is accepted and reads as a
		/// creed clash.</param>
		/// <returns>A player-facing line, or null for a match, which correctly says nothing.
		/// </returns>
		public static string RefusalLine(QolVerdict Verdict, string Who, string Where, string Tag)
		{
			switch (Verdict)
			{
			case QolVerdict.NeedUnmet:
				return Person(Who) + " will not live in " + Place(Where) + ": it has no "
					+ TagPhrase(Tag) + ". Give it that, or offer other quarters.";
			case QolVerdict.Refused:
				if (string.IsNullOrEmpty(Fold(Tag)))
				{
					return Person(Who) + " will not share a roof with the people already in "
						+ Place(Where) + ": what each of them believes leaves no room for the other.";
				}
				return WillNotSleepBeside(Who, Where) + " It is " + TagObjection(Tag) + ".";
			default:
				return null;
			}
		}

		/// <summary>
		/// The line for a resident the whole settlement has nowhere to put, which is the one a
		/// founder actually needs: a single refused building is ordinary, and a person who can live
		/// in none of them is a thing to go and fix. Said once, by a caller holding an announced
		/// flag, exactly like every other 7b line in the mod.
		/// </summary>
		/// <param name="Who">The resident's name.</param>
		/// <param name="Settlement">The settlement's display name. Blank reads as "this
		/// settlement".</param>
		/// <param name="Tag">The need nothing there meets.</param>
		public static string NowhereLine(string Who, string Settlement, string Tag)
		{
			string where = string.IsNullOrWhiteSpace(Settlement) ? "this settlement" : Settlement.Trim();
			return "Nothing standing in " + where + " has " + TagPhrase(Tag) + ", so "
				+ Person(Who) + " has nowhere in it to live. Build somewhere that does.";
		}

		private static string Person(string Name)
		{
			return string.IsNullOrWhiteSpace(Name) ? "the newcomer" : Name.Trim();
		}

		private static string Place(string Name)
		{
			return string.IsNullOrWhiteSpace(Name) ? "what stands there" : ("the " + Name.Trim());
		}
	}
}
