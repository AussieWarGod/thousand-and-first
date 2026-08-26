using System;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomHappeningRules
	{
		// ==================================================================================
		// The prose
		// ==================================================================================

		/// <summary>Qud's own name for the day a feast is anchored to, as
		/// <c>Calendar.GetMonth</c> and <c>Calendar.GetDay</c> spell them.</summary>
		internal static string AnchorName(KingdomFestivalAnchor anchor)
		{
			switch (anchor)
			{
			case KingdomFestivalAnchor.UtYaraUx:
				return "the festival of Ut yara Ux";
			case KingdomFestivalAnchor.Ides:
				return "the Ides";
			default:
				return "";
			}
		}

		/// <summary>
		/// The chronicle's telling of a feast: what day it was, and what the settlement put on the
		/// table. The dish is the realm's own &mdash; <c>Faction.WaterRitualRecipeText</c> as
		/// <c>KingdomDish</c> stamped it &mdash; so the feast serves what the creed already eats
		/// rather than a menu invented for the occasion.
		/// </summary>
		internal static string FestivalTelling(KingdomFestivalAnchor anchor, string settlementName, string dishName, int mouths)
		{
			string day = AnchorName(anchor);
			string what = string.IsNullOrEmpty(dishName) ? "what the larders held" : dishName;
			string who = (mouths > 0)
				? (mouths == 1 ? "one of them ate" : (mouths.ToString() + " of them ate"))
				: "the tables were bare";
			return settlementName + " kept " + day + ", and " + who + " " + what;
		}

		/// <summary>The line the founder is handed when the feast happens somewhere they are
		/// not.</summary>
		internal static string FestivalNotice(KingdomFestivalAnchor anchor, string settlementName, string dishName)
		{
			string what = string.IsNullOrEmpty(dishName) ? "what the larders held" : dishName;
			return settlementName + " kept " + AnchorName(anchor) + ", and set out " + what + ".";
		}

		/// <summary>
		/// The chronicle's telling of a wedding. Named for the roof rather than for a ceremony
		/// this mod does not simulate: what the model knows is that these two share a home, and
		/// the prose says exactly that much and no more.
		/// </summary>
		internal static string WeddingTelling(string oneName, string otherName, string settlementName)
		{
			return oneName + " and " + otherName + " were married under the roof they already shared, and " + settlementName + " drank to it";
		}

		/// <summary>The wedding as the founder hears it, wherever they are standing.</summary>
		internal static string WeddingNotice(string oneName, string otherName)
		{
			return oneName + " and " + otherName + " were married, and the water was shared.";
		}

		/// <summary>
		/// The rite clause the city's one telling of a death carries: where they were laid, and
		/// who spoke.
		/// <para>
		/// <b>A clause, not a sentence, and that is the point.</b> It is appended to the line
		/// <c>KingdomOfficeRules.MourningChronicle</c> already composes, so the death is told once
		/// and the funeral is part of that telling rather than a second one following it.
		/// </para>
		/// </summary>
		/// <param name="officeTitle">The settlement's office, from
		/// <c>KingdomOfficeRules.ChooseTitle</c>, or empty when nobody holds it.</param>
		/// <param name="officeHolder">Who holds it, or empty.</param>
		internal static string FuneralClause(string officeTitle, string officeHolder)
		{
			if (string.IsNullOrEmpty(officeHolder) || string.IsNullOrEmpty(officeTitle))
			{
				// A settlement of one, or one that has just lost the only person who could have
				// spoken. Said plainly rather than dressed up: nobody spoke.
				return ", and there was no one left to speak the water over them";
			}
			return ", and " + officeHolder + ", " + officeTitle + ", spoke the water over them";
		}

		/// <summary>
		/// The chronicle's telling of a work that stopped, named rather than logged: which work,
		/// where, and what condition it was in when it went.
		/// </summary>
		internal static string BreakdownTelling(string workName, string settlementName, int conditionPercent)
		{
			return "the " + Named(workName) + " at " + settlementName + " went still at " + conditionPercent + " parts in a hundred, and the hands stood about";
		}

		/// <summary>The breakdown as the founder hears it. One line, once, and it names the thing
		/// that stopped (STANDARDS 7b).</summary>
		internal static string BreakdownNotice(string workName, int conditionPercent)
		{
			return "The " + Named(workName) + " has stopped. " + conditionPercent + " parts in a hundred are left of it.";
		}

		/// <summary>
		/// The unsaying: the work turns again. <c>KingdomWord.Unsay</c>'s own lane, because a
		/// founder told from a distance that their mill had stopped is owed the withdrawal from
		/// the same distance.
		/// </summary>
		internal static string MendedNotice(string workName, int conditionPercent)
		{
			return "The " + Named(workName) + " turns again, at " + conditionPercent + " parts in a hundred.";
		}

		/// <summary>
		/// The homecoming report's one line for a happening the founder was not there for. The
		/// told-log ring is what the report reads, so this is the only place a stored line becomes
		/// prose.
		/// </summary>
		internal static string ToldLine(KingdomToldKind kind, int count)
		{
			if (count <= 0)
			{
				return "";
			}
			bool one = count == 1;
			switch (kind)
			{
			case KingdomToldKind.Wedding:
				return one ? "There was a wedding." : (count + " couples were married.");
			case KingdomToldKind.Funeral:
				return one ? "One of yours was buried." : (count + " of yours were buried.");
			case KingdomToldKind.Festival:
				return one ? "A feast was kept." : (count + " feasts were kept.");
			case KingdomToldKind.Breakdown:
				return one ? "Something stopped working." : (count + " works stopped.");
			default:
				return "";
			}
		}

		/// <summary>A work's display name, or the plainest honest noun for one nobody named.</summary>
		private static string Named(string workName)
		{
			return string.IsNullOrEmpty(workName) ? "works" : workName;
		}
	}
}
