using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomReachRules
	{
		// --- Seats: the great work is an office ---------------------------------------------

		/// <summary>
		/// Whether a band only works while a named notable heads it. The great works, and only
		/// them: an S plot is any hands, forever, and that is the point of it.
		/// </summary>
		public static bool RequiresSeat(ReachBand Band)
		{
			return Band >= ReachBand.City;
		}

		/// <summary>
		/// What a great work reaches while no one heads it. Never nothing: the temple with no
		/// keeper of rites is still a temple to the people who live beside it, so it drops to the
		/// zone it stands in and says so once (STANDARDS 7b). Nothing here ever closes a work,
		/// and nothing here decays &mdash; naming a keeper restores the band the same pass.
		/// </summary>
		public static ReachBand Unheaded(ReachBand Band)
		{
			return RequiresSeat(Band) ? ReachBand.Zone : Band;
		}

		/// <summary>
		/// What the settlement calls whoever heads a work of this purpose. A name, never a rank
		/// &mdash; the same posture <c>KingdomOfficeRules.OfficeTitles</c> takes with the
		/// settlement's own office. A category this build does not know gets the plain title
		/// rather than a guess at somebody else's vocabulary.
		/// </summary>
		/// <param name="Category">A <c>BuildEntry.Category</c>. Null or unknown reads as the
		/// plain keeper.</param>
		public static string SeatTitle(string Category)
		{
			switch (Fold(Category))
			{
			case "faith":
				return "keeper of rites";
			case "knowledge":
				return "archivist";
			case "craft":
				return "master of the yard";
			case "food":
				return "reeve of the fields";
			case "storage":
				return "warden of the stores";
			case "defense":
			case "defence":
				return "captain of the watch";
			case "housing":
				return "steward of the house";
			case "civic":
				return "steward";
			case "memorial":
				return "keeper of the names";
			default:
				return "keeper";
			}
		}

		/// <summary>
		/// How well one settler would head a work of this purpose, read off who they already are.
		/// Derive-first: nothing is assigned, nothing is trained, and the founder chooses nobody
		/// &mdash; a settler another mod shipped is scored by the same attributes the game gives
		/// every creature.
		/// <para>
		/// The governing attribute doubles and a second one counts once, so a candidate is
		/// plainly better at the thing the work does rather than plainly better in general.
		/// </para>
		/// </summary>
		/// <returns>Never negative. Zero for a candidate with nothing to bring.</returns>
		public static int SeatFitness(string Category, int Strength, int Agility, int Toughness, int Intelligence, int Willpower, int Ego)
		{
			int primary;
			int secondary;
			switch (Fold(Category))
			{
			case "faith":
				primary = Willpower;
				secondary = Ego;
				break;
			case "knowledge":
				primary = Intelligence;
				secondary = Willpower;
				break;
			case "craft":
				// Addendum 7's own reading of a crew: strength is what stonework and haulage
				// actually ask for, and the hand comes after the arm.
				primary = Strength;
				secondary = Agility;
				break;
			case "food":
				primary = Toughness;
				secondary = Strength;
				break;
			case "storage":
				primary = Toughness;
				secondary = Intelligence;
				break;
			case "defense":
			case "defence":
				primary = Strength;
				secondary = Toughness;
				break;
			default:
				// Including every category a third party invents: who the settlement listens to,
				// which is the honest answer when nobody has said what the work is.
				primary = Ego;
				secondary = Willpower;
				break;
			}
			int score = (2 * Clamp(primary)) + Clamp(secondary);
			return (score < 0) ? 0 : score;
		}

		/// <summary>
		/// How much better a challenger must be before a seated notable is replaced. Without it
		/// the seat would change hands whenever two settlers' attributes happened to swap order,
		/// and the chronicle would fill with an office nobody actually lost.
		/// </summary>
		public const int SeatUnseatMargin = 3;

		/// <summary>Whether a challenger takes a seated notable's place. An empty seat is taken
		/// by anybody, which is <c>IncumbentScore</c> below zero.</summary>
		public static bool ShouldUnseat(int IncumbentScore, int ChallengerScore)
		{
			if (IncumbentScore < 0)
			{
				return ChallengerScore >= 0;
			}
			return ChallengerScore >= IncumbentScore + SeatUnseatMargin;
		}

		/// <summary>
		/// The line a great work with nobody at its head gives the founder, once (STANDARDS 7b).
		/// Names what would lift it, because a founder who cannot see why the city stopped
		/// gaining anything from its own cathedral is the exact failure the rule exists for.
		/// </summary>
		/// <param name="WorkName">What the founder calls the work.</param>
		/// <param name="Title">From <see cref="SeatTitle"/>.</param>
		public static string UnheadedLine(string WorkName, string Title)
		{
			string name = string.IsNullOrEmpty(WorkName) ? "the great work" : WorkName;
			string title = string.IsNullOrEmpty(Title) ? "keeper" : Title;
			return "{{W|" + name + " stands, and no " + title + " has been named. It keeps its own ground until one is.}}";
		}

		/// <summary>
		/// The chronicle's telling of a work's seat changing hands, or empty for
		/// <c>OfficeTransition.None</c>, which is never announced. Deliberately classified by
		/// <c>KingdomOfficeRules.ClassifyTransition</c> rather than by a second rule of its own:
		/// a great work IS an office, and there is one grammar for an office changing hands.
		/// </summary>
		public static string SeatChronicle(KingdomOfficeRules.OfficeTransition Transition, string Title, string Holder, string WorkName)
		{
			string name = string.IsNullOrEmpty(WorkName) ? "the great work" : WorkName;
			switch (Transition)
			{
			case KingdomOfficeRules.OfficeTransition.FirstHolder:
				return Holder + " is named " + Title + " of " + name;
			case KingdomOfficeRules.OfficeTransition.Passed:
				return "the office of " + Title + " of " + name + " passes to " + Holder;
			case KingdomOfficeRules.OfficeTransition.Vacant:
				return name + " has no " + Title + " left to head it";
			default:
				return "";
			}
		}

		/// <summary>The line spoken live when a seat changes hands, or empty when the chronicle
		/// has nothing to say either.</summary>
		public static string SeatMessage(KingdomOfficeRules.OfficeTransition Transition, string Title, string Holder, string WorkName)
		{
			string chronicle = SeatChronicle(Transition, Title, Holder, WorkName);
			if (chronicle.Length == 0)
			{
				return "";
			}
			string colour = (Transition == KingdomOfficeRules.OfficeTransition.Vacant) ? "r" : "W";
			return "{{" + colour + "|" + char.ToUpperInvariant(chronicle[0]) + chronicle.Substring(1) + ".}}";
		}

		// --- Small shared helpers -------------------------------------------------------------

		private static int Clamp(int Value)
		{
			return (Value < 0) ? 0 : Value;
		}

		private static int IndexIn(string[] Set, string Value)
		{
			for (int i = 0; i < Set.Length; i++)
			{
				if (Set[i] == Value)
				{
					return i;
				}
			}
			return -1;
		}

		private static string Join(string[] Values)
		{
			string joined = "";
			for (int i = 0; i < Values.Length; i++)
			{
				if (i > 0)
				{
					joined += (i == Values.Length - 1) ? " and " : ", ";
				}
				joined += Values[i];
			}
			return joined;
		}

		/// <summary>Trims and lower-cases one token. Null for anything that was only space, so
		/// every caller has one thing to test rather than two.</summary>
		private static string Fold(string Value)
		{
			if (string.IsNullOrEmpty(Value))
			{
				return null;
			}
			string trimmed = Value.Trim().ToLowerInvariant();
			return (trimmed.Length == 0) ? null : trimmed;
		}
	}
}
