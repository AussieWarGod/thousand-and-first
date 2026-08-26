using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterialRules
	{
		// --- Infrastructure gates construction ------------------------------------------------

		/// <summary>
		/// One yard as the gate needs to see it. Standing is the building; staffed is whether
		/// anybody is in it this pass; headed is whether the office layer has seated a notable over
		/// it. All three are read, never assigned.
		/// </summary>
		public struct KingdomYardStanding
		{
			public KingdomYard Yard;

			/// <summary>A finished building of this kind stands on the settlement's ground.
			/// </summary>
			public bool Standing;

			/// <summary>Somebody is working it this pass.</summary>
			public bool Staffed;

			/// <summary>A named notable heads it (Addendum 6's office rule).</summary>
			public bool Headed;

			public KingdomYardStanding(KingdomYard Yard, bool Standing, bool Staffed, bool Headed)
			{
				this.Yard = Yard;
				this.Standing = Standing;
				this.Staffed = Staffed;
				this.Headed = Headed;
			}
		}

		/// <summary>Whether a design of this size needs a yard standing behind it at all. Small and
		/// middling works are raised by whoever is free; the big ones are not.</summary>
		public static bool RequiresYard(KingdomPlotRules.PlotSize Size)
		{
			return Size == KingdomPlotRules.PlotSize.Large || Size == KingdomPlotRules.PlotSize.Huge;
		}

		/// <summary>Whether a design of this size needs its yard HEADED as well as staffed. Only
		/// the grand ones: a great work is led by somebody with a name (Addendum 6).</summary>
		public static bool RequiresHeadedYard(KingdomPlotRules.PlotSize Size)
		{
			return Size == KingdomPlotRules.PlotSize.Huge;
		}

		/// <summary>
		/// Which yards a design's material cost implies, in yard order.
		/// <para>
		/// Every refined material a design names says its own yard outright: shaped stone is a
		/// mason's yard, and there is no other way to have any. A design that names none is judged
		/// by what it is mostly made OF &mdash; a temple of forty stone wants a mason's yard behind
		/// it whether or not the stone was dressed &mdash; which is what makes the gate reach
		/// designs written before yards existed without a single attribute being added to them.
		/// A design made of mud and brush alone implies no yard at all, and is raised by hands.
		/// </para>
		/// </summary>
		/// <param name="Size">The design's plot tier. Anything under Large implies nothing.</param>
		/// <param name="Cost">The design's material cost. Null and empty imply nothing.</param>
		public static List<KingdomYard> YardsFor(KingdomPlotRules.PlotSize Size, KingdomMaterialTally Cost)
		{
			List<KingdomYard> yards = new List<KingdomYard>();
			if (!RequiresYard(Size) || Cost == null || Cost.IsEmpty())
			{
				return yards;
			}
			for (int i = 0; i < YardCount; i++)
			{
				if (Cost.Get(YardMakes[i]) > 0)
				{
					yards.Add((KingdomYard)i);
				}
			}
			if (yards.Count == 0 && TryDominantYard(Cost, out var dominant))
			{
				yards.Add(dominant);
			}
			return yards;
		}

		/// <summary>
		/// The yard whose stock a cost is mostly made of: timber to the sawyer, stone and marble to
		/// the mason, scrap to the smelter, with each yard's own refined output counted alongside
		/// the raw it came from. Ties go to the earlier yard in
		/// <see cref="KingdomYard"/> order, which is a rule rather than an accident so the same
		/// cost always names the same yard.
		/// </summary>
		/// <returns>False for a cost of nothing but mud and brush, which no yard touches.</returns>
		public static bool TryDominantYard(KingdomMaterialTally Cost, out KingdomYard Yard)
		{
			Yard = KingdomYard.Sawyer;
			if (Cost == null)
			{
				return false;
			}
			int best = 0;
			bool found = false;
			for (int i = 0; i < YardCount; i++)
			{
				int units = Cost.Get(YardMakes[i]);
				KingdomMaterial[] eats = YardEats[i];
				for (int j = 0; j < eats.Length; j++)
				{
					units += Cost.Get(eats[j]);
				}
				if (units > best)
				{
					best = units;
					Yard = (KingdomYard)i;
					found = true;
				}
			}
			return found;
		}

		/// <summary>What one yard's standing is in a list of them, or a yard that stands nowhere.
		/// </summary>
		public static KingdomYardStanding StandingOf(IList<KingdomYardStanding> Yards, KingdomYard Yard)
		{
			if (Yards != null)
			{
				for (int i = 0; i < Yards.Count; i++)
				{
					if (Yards[i].Yard == Yard)
					{
						return Yards[i];
					}
				}
			}
			return new KingdomYardStanding(Yard, Standing: false, Staffed: false, Headed: false);
		}

		/// <summary>
		/// Whether the settlement's infrastructure will carry this design, and what is missing when
		/// it will not.
		/// <para>
		/// The law of Addendum 7 in one method: a large work wants the relevant yard standing and
		/// staffed, and a grand one wants it headed as well. Every refusal names the yard and the
		/// state it is in, once, where the founder is standing (STANDARDS 7b) &mdash; "there is no
		/// mason's yard" and "the mason's yard stands idle" are different problems with different
		/// answers, and a founder told only "you cannot build this" has been told nothing.
		/// </para>
		/// </summary>
		/// <param name="Size">The design's plot tier.</param>
		/// <param name="Cost">The design's material cost.</param>
		/// <param name="Yards">What the settlement's yards are doing. Null reads as none standing.
		/// </param>
		/// <param name="DesignName">What the founder calls the design, for the sentence. Null is
		/// accepted and reads as "the work".</param>
		/// <param name="Refusal">Null when this returns true, else the founder-facing reason.
		/// </param>
		public static bool AllowsBuild(KingdomPlotRules.PlotSize Size, KingdomMaterialTally Cost, IList<KingdomYardStanding> Yards, string DesignName, out string Refusal)
		{
			Refusal = null;
			List<KingdomYard> wanted = YardsFor(Size, Cost);
			if (wanted.Count == 0)
			{
				return true;
			}
			string name = string.IsNullOrEmpty(DesignName) ? "the work" : ("the " + DesignName);
			bool headed = RequiresHeadedYard(Size);
			for (int i = 0; i < wanted.Count; i++)
			{
				KingdomYardStanding standing = StandingOf(Yards, wanted[i]);
				string yard = YardName(wanted[i]);
				if (!standing.Standing)
				{
					Refusal = "A work of this size is not raised by willing hands alone. " + Capitalise(name)
						+ " wants {{C|a " + yard + "}} standing in the settlement, and there is none. Raise one first.";
					return false;
				}
				if (!standing.Staffed)
				{
					Refusal = Capitalise(name) + " wants the {{C|" + yard
						+ "}}, and it stands idle. Stand a settler down off the water or another work and it will be worked again.";
					return false;
				}
				if (headed && !standing.Headed)
				{
					Refusal = Capitalise(name) + " is a great work, and a great work is led. The {{C|" + yard
						+ "}} wants somebody named over it before the settlement will attempt this.";
					return false;
				}
			}
			return true;
		}

		/// <summary>One line for the founder about what a design's size will ask of the yards,
		/// before they order it. Null when the design asks nothing.</summary>
		public static string YardRequirementLine(KingdomPlotRules.PlotSize Size, KingdomMaterialTally Cost)
		{
			List<KingdomYard> wanted = YardsFor(Size, Cost);
			if (wanted.Count == 0)
			{
				return null;
			}
			List<string> names = new List<string>();
			for (int i = 0; i < wanted.Count; i++)
			{
				names.Add("a " + YardName(wanted[i]));
			}
			string list = JoinPhrases(names);
			return RequiresHeadedYard(Size)
				? ("A work this size wants " + list + ", worked and headed.")
				: ("A work this size wants " + list + ", worked.");
		}

	}
}
