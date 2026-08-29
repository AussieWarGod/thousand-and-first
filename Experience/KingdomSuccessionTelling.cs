using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomSuccessionRules
	{
		// ==================================================================================
		// The telling
		// ==================================================================================

		/// <summary>The line the chronicle keeps for a founder's death. No trailing period, the
		/// register the rest of the chronicle is written in.</summary>
		public static string FallenChronicle(string FounderName, string SeatName, string CauseClause)
		{
			string who = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			string where = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			string how = string.IsNullOrEmpty(CauseClause) ? "was lost, and no one living can say how" : CauseClause;
			return who + ", who founded " + where + ", " + how;
		}

		/// <summary>What the outsiders say instead, which is never quite what happened. The
		/// disputed half of C12's rumour register.</summary>
		public static string FallenRumour(string FounderName, string SeatName)
		{
			string who = string.IsNullOrEmpty(FounderName) ? "the one who founded it" : FounderName;
			string where = string.IsNullOrEmpty(SeatName) ? "that settlement out east" : SeatName;
			return "word going about is that " + who + " will not be coming back to " + where + ", and that nobody there has said why";
		}

		/// <summary>What a founder's cairn is cut with. Deliberately not
		/// <c>KingdomOfficeRules.Epitaph</c>: that grammar says who CAME to the settlement, and the
		/// founder is the one person of whom it was never true.</summary>
		public static string FounderEpitaph(string FounderName, string SeatName, string Region, string CauseClause)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("Here is remembered ").Append(string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName);
			builder.Append(", who poured out the first water at ").Append(string.IsNullOrEmpty(SeatName) ? "this place" : SeatName);
			if (!string.IsNullOrEmpty(Region))
			{
				builder.Append(" in ").Append(Region);
			}
			builder.Append(" and ").Append(string.IsNullOrEmpty(CauseClause) ? "was lost, and no one living can say how" : CauseClause);
			builder.Append(". The water was shared, and is shared still.");
			return builder.ToString();
		}

		/// <summary>How the word came, said in the rite's own voice. Named roads only, never a
		/// number of days on screen.</summary>
		public static string RoadClause(NewsRoad Road, int Days)
		{
			switch (Road)
			{
			case NewsRoad.Arch:
				return "the word crossed the arch with the light, the same hour";
			case NewsRoad.Seat:
				return "there was no one to send: it happened here";
			case NewsRoad.Rumour:
				return "no road went there, so the word came the long way, hand to hand, and took " + Plural(Days, "day");
			default:
				return (Days <= 0)
					? "the word was ridden in before the day was out"
					: ("the word was ridden in, and was " + Plural(Days, "day") + " on the road");
			}
		}

		/// <summary>The chronicle's telling of the mourning rite and the accession together, because
		/// they are one occasion: a realm does not crown anybody on a day it is not burying somebody.</summary>
		public static string RiteChronicle(string SeatName, string FounderName, string HeirName, NewsRoad Road, int Days)
		{
			string where = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			string heir = string.IsNullOrEmpty(HeirName) ? "one of its own" : HeirName;
			return where + " learned that " + founder + " was dead — " + RoadClause(Road, Days)
				+ " — and held the mourning rite, and put the charter into the hands of " + heir;
		}

		/// <summary>The one semantic chronicle row for a successful succession: exact death,
		/// priced news, physical rite, in-run shrine, and accession.</summary>
		public static string SuccessionChronicle(string SeatName, string FounderName,
			string CauseClause, string HeirName, NewsRoad Road, int Days, string FixtureName)
		{
			string where = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			string heir = string.IsNullOrEmpty(HeirName) ? "one of its own" : HeirName;
			string cause = string.IsNullOrEmpty(CauseClause)
				? "died, and no one living can say how" : CauseClause;
			string fixture = string.IsNullOrEmpty(FixtureName) ? "the rite ground" : FixtureName;
			return founder + ", who founded " + where + ", " + cause + "; "
				+ RoadClause(Road, Days) + ", so its named residents present walked to " + fixture
				+ ", held the mourning rite, raised the founder's shrine-marker, and put the charter into the hands of "
				+ heir;
		}

		/// <summary>The modal the heir reads when the rite is held with them standing in it.</summary>
		public static string RiteAttendedPopup(string SeatName, string FounderName, string HeirName, NewsRoad Road, int Days)
		{
			string where = string.IsNullOrEmpty(SeatName) ? "the settlement" : "{{C|" + SeatName + "}}";
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : "{{C|" + FounderName + "}}";
			string heir = string.IsNullOrEmpty(HeirName) ? "you" : "{{C|" + HeirName + "}}";
			return where + " has heard: " + RoadClause(Road, Days) + ".\n\n"
				+ "They lay out water for " + founder + ", and drink none of it, and stand in the dust until the sun is off the roofs.\n\n"
				+ "Then they turn round and look at " + heir + ", because there is nobody else left to look at.";
		}

		/// <summary>What the founder's remains offer, once, to whoever kneels at them.</summary>
		public static string CorpseReadPrompt(string FounderName)
		{
			return "Read what " + (string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName) + " knew?";
		}

		/// <summary>What reading them is like. The psychal gland's own register
		/// (<c>D/XRL/World/Parts/SecretsOnEat.cs:22-35</c>), which is Qud's established grammar for
		/// coming into another mind's knowledge.</summary>
		public static string CorpseReadLine(int Entries, int QuestMarks)
		{
			if (Entries <= 0 && QuestMarks <= 0)
			{
				return "{{K|There is nothing left in there that you did not already know.}}";
			}
			StringBuilder builder = new StringBuilder();
			builder.Append("{{W|Someone else's memories seep into your own.}}");
			if (Entries > 0)
			{
				builder.Append(" You remember ").Append(Plural(Entries, "thing")).Append(" you never learned.");
			}
			if (QuestMarks > 0)
			{
				builder.Append(" And you know where ").Append(Plural(QuestMarks, "undertaking")).Append(" began.");
			}
			return builder.ToString();
		}

		/// <summary>The map note left at a quest-giver's ground by the corpse-read. The concrete
		/// half of C5's "quest updates": no quest state is touched, and the heir is simply told
		/// where the founder took the errand on.</summary>
		public static string QuestMarkNote(string QuestName, string GiverName)
		{
			string quest = BoundQuestLabel(WithoutInheritedSuffix(QuestName), "an undertaking");
			if (string.IsNullOrEmpty(GiverName))
			{
				return "the founder's journal marks where " + quest + " began";
			}
			return "the founder's journal marks where " + quest + " began, and names "
				+ BoundQuestLabel(GiverName, "the quest-giver");
		}

		/// <summary>What the founder is told when the line ends with them. The honest ending, in
		/// the mod's own words, before Qud's own door closes.</summary>
		public static string DynastyEndPopup(string SeatName, SuccessionVerdict Verdict)
		{
			string where = string.IsNullOrEmpty(SeatName) ? "your settlement" : "{{C|" + SeatName + "}}";
			if (Verdict == SuccessionVerdict.HeirUnreachable)
			{
				return "There is a name on the roll at " + where + ", and nobody standing under it.\n\nThe line ends here.";
			}
			return "There is nobody left at " + where + " to take the charter up.\n\nThe line ends here.";
		}

		/// <summary>The chronicle's last line, which the chronicle keeps regardless: a state that
		/// erases the chronicle is a defect (DECISIONS.md).</summary>
		public static string DynastyEndChronicle(string SeatName, string FounderName)
		{
			string where = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			return founder + " died with no one on the roll to follow, and " + where + " kept no charter after that day";
		}

		/// <summary>Counted noun, Qud-plain: "one day", "three days".</summary>
		public static string Plural(int Count, string Noun)
		{
			if (Count == 1)
			{
				return "one " + Noun;
			}
			return Count.ToString() + " " + Noun + "s";
		}
	}
}
