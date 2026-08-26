using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomReachRules
	{
		// --- What the ground is called ----------------------------------------------------------

		/// <summary>What a lift is called in a sentence. A kind this build does not know is said
		/// as itself rather than dressed up in prose that would be a guess.</summary>
		public static string CharacterWord(string Kind)
		{
			switch (Fold(Kind))
			{
			case "spirit":
				return "faith";
			case "learning":
				return "learning";
			case "craft":
				return "craft";
			case "order":
				return "order";
			case "luxury":
				return "comfort";
			default:
				return Fold(Kind) ?? "nothing";
			}
		}

		/// <summary>
		/// What the settlement calls ground of this character. Names the quarter the way the
		/// people living there would &mdash; never a district, never a type, just the phrase a
		/// founder reads and recognises.
		/// </summary>
		public static string QuarterName(string Kind)
		{
			switch (Fold(Kind))
			{
			case "spirit":
				return "the temple quarter";
			case "learning":
				return "the scribes' quarter";
			case "craft":
				return "the workers' quarter";
			case "order":
				return "the watch's quarter";
			case "luxury":
				return "the fine quarter";
			case null:
				return "ordinary ground";
			default:
				return "a quarter of its own";
			}
		}

		/// <summary>
		/// One line naming what shades the ground the founder is standing on, for the status
		/// report. Ground nothing reaches says exactly that rather than nothing at all, so the
		/// surface is readable before the first shrine as well as after it.
		/// </summary>
		public static string QuarterLine(GroundCharacter Character)
		{
			if (Character == null || Character.Lifts.Count == 0)
			{
				return "This ground: ordinary ground, shaded by nothing standing near it.";
			}
			string list = "";
			for (int i = 0; i < Character.Lifts.Count; i++)
			{
				list += ((i == 0) ? "" : ", ") + CharacterWord(Character.Lifts[i].Kind) + " " + Character.Lifts[i].Amount;
			}
			return "This ground: " + QuarterName(Character.Dominant) + " — " + list + ".";
		}

		/// <summary>The clause naming how far a design carries, for the catalogue's own
		/// description of it.</summary>
		public static string ReachClause(ReachBand Band)
		{
			switch (Band)
			{
			case ReachBand.Quarter:
				return "shades its own quarter";
			case ReachBand.Zone:
				return "shades everything built around it";
			case ReachBand.City:
				return "shades the whole city, while somebody heads it";
			case ReachBand.Realm:
				return "shades the whole realm, while somebody heads it";
			default:
				return "shades the ground it stands on";
			}
		}

	}
}
