using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterialRules
	{
		// --- Wear, and what mending it costs ---------------------------------------------------

		/// <summary>
		/// The most wear a work ever carries. Damage runs a work down and never stops it: a
		/// settlement that comes home to a burnt mill finds it turning slowly, not gone. Nothing
		/// here is ever reached by the calendar &mdash; wear comes from events (a raid, hard
		/// running, temperamental certified tech) and from nothing else. Time is labour, never
		/// decay.
		/// </summary>
		public const int MaxWearPercent = 60;

		/// <summary>Wear a work carries after an event adds to what it already had, clamped both
		/// ways. Nothing ever wears past <see cref="MaxWearPercent"/>.</summary>
		public static int AddWear(int Wear, int Added)
		{
			int total = ((Wear > 0) ? Wear : 0) + ((Added > 0) ? Added : 0);
			return (total > MaxWearPercent) ? MaxWearPercent : total;
		}

		/// <summary>How well a worn work runs, as a percentage of what it does whole. Never zero:
		/// the floor is <c>100 - </c><see cref="MaxWearPercent"/>.</summary>
		public static int ConditionPercent(int Wear)
		{
			int wear = (Wear > 0) ? Wear : 0;
			if (wear > MaxWearPercent)
			{
				wear = MaxWearPercent;
			}
			return 100 - wear;
		}

		/// <summary>Wear at which a work stops being merely knocked about and starts being badly
		/// used. Named because three things read the same ladder &mdash; the word, the adjective
		/// the work wears in its own name, and the sentence its description carries &mdash; and
		/// they must not be able to disagree about where a stage begins.</summary>
		public const int BadlyUsedWearPercent = 20;

		/// <summary>Wear at which a work is half-wrecked: the deepest stage there is, and the
		/// same line <c>KingdomLodgingRules.CondemnedWearPercent</c> stops calling a house a roof
		/// at. A work here reads as a ruin, in its name and in its description
		/// (<see cref="ConditionAdjective"/>, <see cref="ConditionLook"/>).</summary>
		public const int HalfWreckedWearPercent = 40;

		/// <summary>One word for the state of a work, for the line the founder reads. Never null.
		/// </summary>
		public static string ConditionWord(int Wear)
		{
			if (Wear <= 0)
			{
				return "sound";
			}
			if (Wear < BadlyUsedWearPercent)
			{
				return "knocked about";
			}
			return (Wear < HalfWreckedWearPercent) ? "badly used" : "half-wrecked";
		}

		/// <summary>
		/// The adjective a worn work wears in its own NAME, so a settlement that fell reads as a
		/// field of ruins rather than as pristine buildings with quiet arithmetic against them
		/// (Addendum 10(c): "a collapsed settlement's former building plots read as RUINS, not as
		/// pristine-but-nerfed works").
		/// <para>
		/// One ladder, three stages, on exactly the thresholds <see cref="ConditionWord"/> uses,
		/// so the name on the plot and the word in the report can never describe different
		/// buildings. Null for a sound work &mdash; which is the whole of how a mending walks the
		/// name back: the stage is a function of the wear and of nothing else, so putting the
		/// wear back down the ladder puts the name back down it, and a work mended to nothing
		/// carries no adjective at all.
		/// </para>
		/// </summary>
		/// <param name="Wear">The work's own wear, 0 to <see cref="MaxWearPercent"/>.</param>
		/// <returns>Null for a sound work, so a caller adds nothing rather than adding
		/// "sound".</returns>
		public static string ConditionAdjective(int Wear)
		{
			if (Wear <= 0)
			{
				return null;
			}
			if (Wear < BadlyUsedWearPercent)
			{
				return "battered";
			}
			return (Wear < HalfWreckedWearPercent) ? "half-ruined" : "ruined";
		}

		/// <summary>
		/// What a worn work LOOKS like, for the description somebody reads when they stop and
		/// look at it. The other half of Addendum 10(c)'s presentation: the name says which stage
		/// of ruin it is in, and this says what that stage looks like standing there.
		/// <para>
		/// Same three stages, same thresholds. Null for a sound work, which needs no sentence
		/// about its condition at all &mdash; and which is again what makes mending walk it back:
		/// nothing here remembers that a work was ever worse than it is.
		/// </para>
		/// </summary>
		/// <param name="Wear">The work's own wear, 0 to <see cref="MaxWearPercent"/>.</param>
		/// <returns>Null for a sound work.</returns>
		public static string ConditionLook(int Wear)
		{
			if (Wear <= 0)
			{
				return null;
			}
			if (Wear < BadlyUsedWearPercent)
			{
				return "Boards have sprung and the weather is getting into it.";
			}
			return (Wear < HalfWreckedWearPercent)
				? "Half of it is propped and the other half leans; it is still doing its work, badly."
				: "It is more ruin than building now - a shell with its work still going on somewhere inside it.";
		}

		/// <summary>
		/// What mending a work costs in material: the share of what it was built from that the wear
		/// stands for, and never the whole building again. A design built for nothing is mended for
		/// nothing, which is honest &mdash; there is nothing in a mud wall to replace.
		/// </summary>
		/// <param name="BuildCost">What the design cost to raise.</param>
		/// <param name="Wear">How worn it is, as a percentage.</param>
		public static KingdomMaterialTally RepairCost(KingdomMaterialTally BuildCost, int Wear)
		{
			if (BuildCost == null || Wear <= 0)
			{
				return new KingdomMaterialTally();
			}
			int wear = (Wear > MaxWearPercent) ? MaxWearPercent : Wear;
			return BuildCost.Scaled(wear);
		}

		/// <summary>
		/// What mending a work costs in bits: the same share of what its design was priced in.
		/// This is the certified-tech half of Addendum 7 &mdash; a temperamental machine is mended
		/// with the same stock it was built from, and a settlement that has no bits has a machine
		/// running at reduced effect and a reason it can read.
		/// </summary>
		public static KingdomBitTally RepairBits(KingdomBitTally BuildBits, int Wear)
		{
			if (BuildBits == null || Wear <= 0)
			{
				return new KingdomBitTally();
			}
			int wear = (Wear > MaxWearPercent) ? MaxWearPercent : Wear;
			return BuildBits.Scaled(wear);
		}

		/// <summary>Effort mending a work costs, from what has to be put back into it. Always at
		/// least one for any wear at all: nothing is mended for free.</summary>
		public static int RepairEffort(int MaterialUnits, int Wear)
		{
			if (Wear <= 0)
			{
				return 0;
			}
			int units = (MaterialUnits > 0) ? MaterialUnits : 0;
			int effort = StrikeBaseEffort / 2 + units * StrikeEffortPerUnit;
			return (effort < 1) ? 1 : effort;
		}

		/// <summary>
		/// The one line a damaged work gets, said once when the damage happens and not again
		/// (STANDARDS 7b). Null for a work that is sound, so a caller never announces nothing.
		/// </summary>
		public static string DamageLine(string Name, int Wear)
		{
			if (Wear <= 0)
			{
				return null;
			}
			string name = string.IsNullOrEmpty(Name) ? "a work" : ("the " + Name);
			return "{{r|" + Capitalise(name) + " is " + ConditionWord(Wear) + ", and runs at " + ConditionPercent(Wear)
				+ " parts in a hundred until somebody mends it. It will not fail, and it will not mend itself.}}";
		}

		// --- Small shared helpers --------------------------------------------------------------

		/// <summary>
		/// Joins phrases the way a person would: "a", "a and b", "a, b and c". Null for an empty
		/// list, so every caller has one thing to test rather than two, and so a tally with nothing
		/// in it never produces a sentence about nothing.
		/// </summary>
		public static string JoinPhrases(List<string> Parts)
		{
			if (Parts == null || Parts.Count == 0)
			{
				return null;
			}
			StringBuilder text = new StringBuilder();
			for (int i = 0; i < Parts.Count; i++)
			{
				if (i > 0)
				{
					text.Append((i == Parts.Count - 1) ? " and " : ", ");
				}
				text.Append(Parts[i]);
			}
			return text.ToString();
		}

		/// <summary>The same sentence with its first letter raised. Left alone when it already is,
		/// and when there is nothing to raise.</summary>
		private static string Capitalise(string Text)
		{
			if (string.IsNullOrEmpty(Text))
			{
				return Text;
			}
			return char.ToUpperInvariant(Text[0]) + Text.Substring(1);
		}
	}
}
