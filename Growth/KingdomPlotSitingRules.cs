using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPlotRules
	{
		// --- Siting ----------------------------------------------------------------------

		/// <summary>Whether any cell of a rect lies in the zone's frontier band.</summary>
		public static bool TouchesFrontier(PlotRect Rect, int Width, int Height, KingdomRules.Frontier Edges)
		{
			if (Edges == KingdomRules.Frontier.None)
			{
				return false;
			}
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
			{
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					if (KingdomRules.IsOnFrontier(x, y, Width, Height, Edges))
					{
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// What the plan thinks of one rect. The layout grammar scores the rect's centre exactly
		/// as it scores a single cell &mdash; so a plot gathers with its own kind, thickens the
		/// civic ground, and rings out past the last roof for the same reasons a lone cask does
		/// &mdash; with two terms a rect needs and a cell does not:
		/// <list type="bullet">
		/// <item>a plot whose CORNER lands in the frontier band pays the frontier penalty even
		/// when its centre is clear of it, because a house on the wall is a house on the wall
		/// whichever cell you measure;</item>
		/// <item>a big plot is pulled toward the heart by <see cref="HeartPull"/>, because the
		/// great works want the settled centre and the huts do not;</item>
		/// <item>a rect standing in ground the heart was surveyed for at the rite pays
		/// <see cref="SurveyPenalty"/> &mdash; a preference away from the great work's ground, and
		/// never a refusal of it.</item>
		/// </list>
		/// Higher is better, and a rect the plan has no complaint about scores zero.
		/// <para>
		/// The grammar's own terms are left exactly as they are for a single cell &mdash; this
		/// never reaches into <c>ScoreCell</c> to change what a cask or a hut wants. Only the
		/// heart-pull term added here knows about the rite ground, because only the great works
		/// care where the settlement started.
		/// </para>
		/// </summary>
		public static int ScoreRect(KingdomLayoutRules.LayoutPurpose Purpose, PlotSize Size, PlotRect Rect, int Width, int Height, KingdomRules.Frontier Edges, IList<KingdomLayoutRules.LayoutMark> Marks, bool HasRite, int RiteX, int RiteY, bool HasSurvey = false, PlotRect Survey = default(PlotRect), int RiteWeight = RiteHeartWeight)
		{
			int centerX = Rect.CenterX;
			int centerY = Rect.CenterY;
			int score = KingdomLayoutRules.ScoreCell(Purpose, centerX, centerY, Width, Height, Edges, Marks);
			if (Purpose != KingdomLayoutRules.LayoutPurpose.Defence
				&& !KingdomRules.IsOnFrontier(centerX, centerY, Width, Height, Edges)
				&& TouchesFrontier(Rect, Width, Height, Edges))
			{
				score -= KingdomLayoutRules.FrontierPenalty;
			}
			int pull = HeartPull(Size);
			if (pull > 0 && TryHeart(Marks, HasRite, RiteX, RiteY, out var heartX, out var heartY, RiteWeight))
			{
				score -= KingdomLayoutRules.Chebyshev(centerX, centerY, heartX, heartY) * pull;
			}
			if (HasSurvey)
			{
				// The founder's ambition, paced out at the rite and standing in the ground as
				// stakes. The settlement reads it as a preference and nothing more: it will not
				// VOLUNTEER to build in the heart's ground while there is clear ground going, and
				// it never refuses to.
				score -= SurveyPenalty(Rect, Survey);
			}
			return score;
		}

		/// <summary>Chebyshev distance from a cell to the nearest cell of a rect; zero inside
		/// it. This is how near the founder is to a plot they are standing at the edge of.</summary>
		public static int Reach(PlotRect Rect, int X, int Y)
		{
			int dx = (X < Rect.X1) ? (Rect.X1 - X) : ((X > Rect.X2) ? (X - Rect.X2) : 0);
			int dy = (Y < Rect.Y1) ? (Rect.Y1 - Y) : ((Y > Rect.Y2) ? (Y - Rect.Y2) : 0);
			return (dx > dy) ? dx : dy;
		}

		/// <summary>
		/// Whether one candidate rect should be preferred to another: the plan's opinion first,
		/// then the founder's own feet, then position, so a run always returns the same ground for
		/// the same settlement. Mirrors <c>KingdomLayoutRules.Beats</c> exactly, on rects.
		/// </summary>
		public static bool Beats(int ScoreA, int ReachA, PlotRect A, int ScoreB, int ReachB, PlotRect B)
		{
			if (ScoreA != ScoreB)
			{
				return ScoreA > ScoreB;
			}
			if (ReachA != ReachB)
			{
				return ReachA < ReachB;
			}
			if (A.Y1 != B.Y1)
			{
				return A.Y1 < B.Y1;
			}
			return A.X1 < B.X1;
		}

		/// <summary>
		/// How near the founder a rect must come to count as their own ground. Two cells rather
		/// than the layout grammar's one: a founder cannot stand inside a plot that does not exist
		/// yet, so "where you stand" for a rect means "the plot you are standing at the edge of".
		/// </summary>
		public const int FounderReachCells = 2;

		/// <summary>
		/// Choose the ground for one plot out of the rects the caller says are clear.
		/// <para>
		/// The same bargain the cell grammar strikes, on rects: the founder's own ground is scored
		/// by the same rules as everything else and wins whenever it comes within
		/// <c>KingdomLayoutRules.FounderTolerance</c> of the plan's best, so the plan picks the
		/// quarter and the founder picks the spot. Where the plan has no opinion at all it says
		/// <see cref="KingdomLayoutRules.LayoutOutcome.Defer"/> and the caller sites the plot its
		/// own way &mdash; which, on empty ground, is where the founder is standing.
		/// </para>
		/// </summary>
		/// <param name="Purpose">What is being raised.</param>
		/// <param name="Size">Which tier, for the heart pull.</param>
		/// <param name="Width">Zone width in cells.</param>
		/// <param name="Height">Zone height in cells.</param>
		/// <param name="Edges">Edges of this zone facing unclaimed ground.</param>
		/// <param name="Marks">Everything the settlement already has standing here.</param>
		/// <param name="Candidates">Rects the caller will accept, in any order. Ties break toward
		/// the founder and then by position, never by the order of this list.</param>
		/// <param name="HasFounder">Whether the founder is standing in this zone.</param>
		/// <param name="FounderX">Founder cell x; ignored when HasFounder is false.</param>
		/// <param name="FounderY">Founder cell y; ignored when HasFounder is false.</param>
		/// <param name="HasRite">Whether the rite ground is known here.</param>
		/// <param name="RiteX">Rite ground x; ignored when HasRite is false.</param>
		/// <param name="RiteY">Rite ground y; ignored when HasRite is false.</param>
		/// <param name="Index">Index into <paramref name="Candidates"/> of the chosen rect, or -1
		/// when the result is <c>Defer</c> or <c>None</c>.</param>
		public static KingdomLayoutRules.LayoutOutcome ChooseRect(KingdomLayoutRules.LayoutPurpose Purpose, PlotSize Size, int Width, int Height, KingdomRules.Frontier Edges, IList<KingdomLayoutRules.LayoutMark> Marks, IList<PlotRect> Candidates, bool HasFounder, int FounderX, int FounderY, bool HasRite, int RiteX, int RiteY, out int Index, bool HasSurvey = false, PlotRect Survey = default(PlotRect), int RiteWeight = RiteHeartWeight)
		{
			Index = -1;
			if (Candidates == null || Candidates.Count == 0)
			{
				return KingdomLayoutRules.LayoutOutcome.None;
			}
			if (!KingdomLayoutRules.HasOpinion(Purpose, Marks, Edges))
			{
				return KingdomLayoutRules.LayoutOutcome.Defer;
			}
			int best = -1;
			int bestScore = 0;
			int bestReach = 0;
			int near = -1;
			int nearScore = 0;
			int nearReach = 0;
			for (int i = 0; i < Candidates.Count; i++)
			{
				PlotRect rect = Candidates[i];
				int score = ScoreRect(Purpose, Size, rect, Width, Height, Edges, Marks, HasRite, RiteX, RiteY, HasSurvey, Survey, RiteWeight);
				int reach = HasFounder ? Reach(rect, FounderX, FounderY) : 0;
				if (best < 0 || Beats(score, reach, rect, bestScore, bestReach, Candidates[best]))
				{
					best = i;
					bestScore = score;
					bestReach = reach;
				}
				if (HasFounder && reach <= FounderReachCells && (near < 0 || Beats(score, reach, rect, nearScore, nearReach, Candidates[near])))
				{
					near = i;
					nearScore = score;
					nearReach = reach;
				}
			}
			if (near >= 0 && nearScore >= bestScore - KingdomLayoutRules.FounderTolerance)
			{
				Index = near;
				return KingdomLayoutRules.LayoutOutcome.Founder;
			}
			Index = best;
			return KingdomLayoutRules.LayoutOutcome.Grammar;
		}

		/// <summary>
		/// Where the door is cut: the border cell nearest the heart that is not a corner, so a
		/// house faces the settlement it belongs to rather than the empty ground behind it. Ties
		/// break north-then-west, so the same plot always opens the same way.
		/// </summary>
		/// <returns>False for a rect too small to have a non-corner border cell (anything under
		/// three cells on both spans), in which case both outputs are zero.</returns>
		public static bool TryDoor(PlotRect Rect, int HeartX, int HeartY, out int X, out int Y)
		{
			X = 0;
			Y = 0;
			bool found = false;
			int bestDistance = 0;
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
			{
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					if (!Rect.IsBorder(x, y) || Rect.IsCorner(x, y))
					{
						continue;
					}
					int distance = KingdomLayoutRules.Chebyshev(x, y, HeartX, HeartY);
					if (!found || distance < bestDistance)
					{
						found = true;
						bestDistance = distance;
						X = x;
						Y = y;
					}
				}
			}
			return found;
		}
	}
}
