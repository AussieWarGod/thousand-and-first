using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPlotRules
	{
		/// <summary>Cells two rects share; zero when they do not meet.</summary>
		public static int OverlapArea(PlotRect A, PlotRect B)
		{
			int x1 = (A.X1 > B.X1) ? A.X1 : B.X1;
			int y1 = (A.Y1 > B.Y1) ? A.Y1 : B.Y1;
			int x2 = (A.X2 < B.X2) ? A.X2 : B.X2;
			int y2 = (A.Y2 < B.Y2) ? A.Y2 : B.Y2;
			if (x1 > x2 || y1 > y2)
			{
				return 0;
			}
			return (x2 - x1 + 1) * (y2 - y1 + 1);
		}

		/// <summary>
		/// What the plan takes off a rect for standing in ground the heart is surveyed for. A
		/// PREFERENCE and never a refusal: the settlement will not volunteer to build there while
		/// clear ground is going, and the founder's own stake still beats the grammar anywhere,
		/// which is why this sits below <c>KingdomLayoutRules.FounderTolerance</c> (16) rather
		/// than above it &mdash; ground the founder is standing on still wins outright.
		/// </summary>
		public const int SurveyRepulsion = 12;

		/// <summary>
		/// The repulsion term itself, scaled by how much of the rect actually stands in surveyed
		/// ground, so a plot clipping one corner of the survey pays almost nothing and a plot
		/// squarely inside it pays the whole of <see cref="SurveyRepulsion"/>. Independent of
		/// tier on purpose: a hut in the heart's ground is as much in the way as a hall is.
		/// </summary>
		public static int SurveyPenalty(PlotRect Rect, PlotRect Survey)
		{
			int area = Rect.Area;
			if (area <= 0)
			{
				return 0;
			}
			int overlap = OverlapArea(Rect, Survey);
			if (overlap <= 0)
			{
				return 0;
			}
			return SurveyRepulsion * overlap / area;
		}

		/// <summary>
		/// The sentence a plot staked in surveyed heart ground carries from the moment it is
		/// staked, and forever after in its own description. Consent before cost, told up front:
		/// the ground is legal to build on, and the mark is the promise being made about it.
		/// </summary>
		public static string YieldingLine(string Name)
		{
			return "The " + Name + " is staked in the ground the heart was surveyed for, and is marked to yield: when the great work is called for this ground, this is what moves. Nothing is taken from it, and nothing is refused you for it.";
		}

		/// <summary>
		/// The same promise, read off the thing itself rather than heard once. Carried by
		/// <c>r_KingdomYielding</c> into the plot's own description, so consent given at placement
		/// is still legible a hundred days later.
		/// </summary>
		public const string YieldingMark = "Staked in the ground the heart was surveyed for. Marked to yield: when the great work is called for this ground, this is what moves.";

		/// <summary>
		/// The heart's next rung wants ground that something already laid is standing in. Named
		/// rather than quietly worked around, and named as a thing the founder can act on: the
		/// mark said this day would come.
		/// </summary>
		public static string RefuseHeartGround(string SuccessorName, string What)
		{
			return "The " + SuccessorName + " wants the surveyed ground, and the {{C|" + What
				+ "}} is standing in it. Nothing the settlement raised comes down on its own: clear it, and the heart can climb.";
		}

		/// <summary>
		/// What the founder is told at the rite, once, when the ground is paced out. Says the
		/// three things the mark is: how much ground, that it costs nothing, and that building
		/// inside it is allowed and marked.
		/// </summary>
		public static string SurveyLine(PlotRect Survey)
		{
			return "You pace out the ground while the water soaks in: {{C|" + Survey.Width + " by " + Survey.Height
				+ "}} cells around the basin, stakes at the corners. Nothing is claimed and nothing is spent: the settlement will simply build elsewhere while it can, and anything staked inside is marked to yield.";
		}

		/// <summary>
		/// A founder asking the settlement to raise one of the heart's own rungs somewhere else.
		/// There is one heart, standing where the water was poured, and it climbs by improvement
		/// rather than by being ordered a second time.
		/// </summary>
		public static string RefuseSecondHeart(string Name)
		{
			return "There is one heart at " + (string.IsNullOrEmpty(Name) ? "this settlement" : Name)
				+ ", and it stands on the ground the first water was poured on. It is not raised twice; it grows where it is.";
		}

		/// <summary>
		/// The blocker is a plot the founder was warned about at the moment they staked it. Said
		/// differently from the general case on purpose: the mark promised this day, and the
		/// founder is owed the promise being kept out loud and a route to the complete ring-call
		/// plan before any labour begins.
		/// </summary>
		public static string RefuseHeartYielding(string SuccessorName, string What)
		{
			return "The {{C|" + What + "}} was marked to yield when it was staked, and the day it was marked for is here: the "
				+ SuccessorName + " wants that ground. The heart's ring can carry the same whole lot to lawful ground, one at a time, for labour and no stores; nothing moves until the founder reviews and consents to the complete plan.";
		}

		/// <summary>The heart's next rung has no room inside the ground surveyed for it &mdash;
		/// a zone too small, or a rite poured against the edge of one.</summary>
		public static string RefuseHeartRoom(string SuccessorName)
		{
			return "The " + SuccessorName + " will not fit the ground surveyed at the rite. There is no room here for the heart to grow into.";
		}

		/// <summary>
		/// The settled heart, seeded at the rite ground and drifting toward the built centre.
		/// <para>
		/// With nothing built, the heart IS the rite ground &mdash; which is what gives the very
		/// first plot something to be sited against. Each work raised pulls the mean one work's
		/// worth toward itself, so the heart drifts rather than jumping: pour the rite in a corner
		/// and build across the zone, and the heart walks after the city over a dozen buildings.
		/// Walls are left out of the mean for the same reason
		/// <c>KingdomLayoutRules.TryHeart</c> leaves them out: a wall is by definition at the edge
		/// and would drag the centre out to it.
		/// </para>
		/// </summary>
		/// <param name="Marks">Everything the settlement has standing here.</param>
		/// <param name="HasRite">Whether the rite ground in this zone is known. A settlement
		/// founded before the rite ground was recorded simply has none, and the heart falls back
		/// to <c>KingdomLayoutRules.TryHeart</c> unchanged.</param>
		/// <param name="RiteX">Rite ground x; ignored when HasRite is false.</param>
		/// <param name="RiteY">Rite ground y; ignored when HasRite is false.</param>
		/// <param name="RiteWeight">How many votes the rite ground gets, from
		/// <see cref="HeartWeightForRung"/> once the heart's own great work stands on it. Clamped
		/// up to <see cref="RiteHeartWeight"/>, so no caller can vote the rite ground away.</param>
		/// <returns>False when there is neither a rite ground nor any shape to read, in which case
		/// both outputs are zero and mean nothing.</returns>
		public static bool TryHeart(IList<KingdomLayoutRules.LayoutMark> Marks, bool HasRite, int RiteX, int RiteY, out int X, out int Y, int RiteWeight = RiteHeartWeight)
		{
			X = 0;
			Y = 0;
			if (!HasRite)
			{
				return KingdomLayoutRules.TryHeart(Marks, out X, out Y);
			}
			int weight = (RiteWeight < RiteHeartWeight) ? RiteHeartWeight : RiteWeight;
			int sumX = RiteX * weight;
			int sumY = RiteY * weight;
			int count = weight;
			if (Marks != null)
			{
				for (int i = 0; i < Marks.Count; i++)
				{
					if (Marks[i].Purpose == KingdomLayoutRules.LayoutPurpose.Defence)
					{
						continue;
					}
					sumX += Marks[i].X;
					sumY += Marks[i].Y;
					count++;
				}
			}
			X = (sumX + count / 2) / count;
			Y = (sumY + count / 2) / count;
			return true;
		}

		/// <summary>
		/// Extra penalty per cell of distance from the heart, by tier. A hut may stand anywhere its
		/// own quarter allows; a great plot wants the heart, which is why a heart full of early
		/// huts eventually has to be struck to make room for one. Small and middling plots add
		/// nothing here and are sited purely by the layout grammar's own pulls.
		/// </summary>
		public static int HeartPull(PlotSize Size)
		{
			switch (Size)
			{
				case PlotSize.Large:
					return 1;
				case PlotSize.Huge:
					return 3;
				default:
					return 0;
			}
		}
	}
}
