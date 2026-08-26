using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomLayoutRules
	{

		/// <summary>
		/// What the plan thinks of one cell for one purpose. Higher is better; a cell the plan
		/// has no complaint about scores zero, and a purpose the plan has no opinion on scores
		/// zero everywhere so nothing can be read into the ranking.
		/// </summary>
		/// <param name="Purpose">What is being raised.</param>
		/// <param name="X">Cell x.</param>
		/// <param name="Y">Cell y.</param>
		/// <param name="Width">Zone width in cells.</param>
		/// <param name="Height">Zone height in cells.</param>
		/// <param name="Edges">Edges of this zone facing unclaimed ground.</param>
		/// <param name="Marks">Everything the settlement already has standing here.</param>
		public static int ScoreCell(LayoutPurpose Purpose, int X, int Y, int Width, int Height, KingdomRules.Frontier Edges, IList<LayoutMark> Marks)
		{
			if (!HasOpinion(Purpose, Marks, Edges))
			{
				return 0;
			}
			if (Purpose == LayoutPurpose.Defence)
			{
				int touching = CountWithin(Marks, LayoutPurpose.Defence, X, Y, WallReachCells);
				int counted = (touching > WallContinuityCap) ? WallContinuityCap : touching;
				return counted * WallContinuityBonus - (touching - counted) * WallThickenPenalty;
			}
			int score = 0;
			if (KingdomRules.IsOnFrontier(X, Y, Width, Height, Edges))
			{
				score -= FrontierPenalty;
			}
			if (KeepsLanes(Purpose) && TryNearestAny(Marks, X, Y, out var crowd) && crowd <= CrowdRadius)
			{
				score -= CrowdPenalty;
			}
			bool hasHeart = TryHeart(Marks, out var heartX, out var heartY);
			int fromHeart = hasHeart ? Chebyshev(X, Y, heartX, heartY) : 0;
			bool hasKin = TryNearest(Marks, Purpose, X, Y, out var kin);
			switch (Purpose)
			{
				case LayoutPurpose.Storage:
				case LayoutPurpose.Housing:
					if (hasKin)
					{
						score -= kin * AnchorWeight;
					}
					else if (hasHeart)
					{
						score -= fromHeart * HeartWeight;
					}
					break;
				case LayoutPurpose.Civic:
					if (hasHeart)
					{
						score -= fromHeart * HeartWeight;
					}
					break;
				case LayoutPurpose.Field:
				case LayoutPurpose.Memorial:
					if (hasHeart)
					{
						int ring = (Purpose == LayoutPurpose.Field) ? FieldRingCells : MemorialRingCells;
						int missed = (fromHeart > ring) ? (fromHeart - ring) : (ring - fromHeart);
						score -= missed * RingWeight;
					}
					if (hasKin)
					{
						score -= kin * AnchorWeight;
					}
					break;
			}
			return score;
		}

		/// <summary>Whether this purpose wants a walkable gap around it. Walls must touch to be
		/// a wall; fields and graves lie in rows; everything else is entered and used, so it
		/// keeps its lane.</summary>
		public static bool KeepsLanes(LayoutPurpose Purpose)
		{
			return Purpose == LayoutPurpose.Storage || Purpose == LayoutPurpose.Housing || Purpose == LayoutPurpose.Civic;
		}

		/// <summary>
		/// Choose the ground for one work out of the cells the caller says are clear.
		/// <para>
		/// The founder is not fought. Their own ground is scored by the same grammar as
		/// everything else, and it wins whenever it comes within <see cref="FounderTolerance"/>
		/// of the plan's best &mdash; so the plan chooses the quarter and the founder chooses the
		/// spot inside it. The plan only overrules them where it feels strongly: a house on the
		/// wall, a cask half a zone from the water. Where the plan has no opinion at all it says
		/// <see cref="LayoutOutcome.Defer"/> and the caller places the work the way it always
		/// did.
		/// </para>
		/// </summary>
		/// <param name="Purpose">What is being raised.</param>
		/// <param name="Width">Zone width in cells.</param>
		/// <param name="Height">Zone height in cells.</param>
		/// <param name="Edges">Edges of this zone facing unclaimed ground.</param>
		/// <param name="Marks">Everything the settlement already has standing here.</param>
		/// <param name="Candidates">Cells the caller will accept, in any order. Ties are broken
		/// toward the founder and then by position, never by the order of this list, so the
		/// answer does not depend on how the caller walked the zone.</param>
		/// <param name="HasFounder">Whether the founder is standing in this zone.</param>
		/// <param name="FounderX">Founder cell x; ignored when HasFounder is false.</param>
		/// <param name="FounderY">Founder cell y; ignored when HasFounder is false.</param>
		/// <param name="Index">Index into <paramref name="Candidates"/> of the chosen cell, or
		/// -1 when the result is <see cref="LayoutOutcome.Defer"/> or
		/// <see cref="LayoutOutcome.None"/>.</param>
		public static LayoutOutcome Choose(LayoutPurpose Purpose, int Width, int Height, KingdomRules.Frontier Edges, IList<LayoutMark> Marks, IList<LayoutPoint> Candidates, bool HasFounder, int FounderX, int FounderY, out int Index)
		{
			Index = -1;
			if (Candidates == null || Candidates.Count == 0)
			{
				return LayoutOutcome.None;
			}
			if (!HasOpinion(Purpose, Marks, Edges))
			{
				return LayoutOutcome.Defer;
			}
			int best = -1;
			int bestScore = 0;
			int bestReach = 0;
			int near = -1;
			int nearScore = 0;
			int nearReach = 0;
			for (int i = 0; i < Candidates.Count; i++)
			{
				LayoutPoint point = Candidates[i];
				int score = ScoreCell(Purpose, point.X, point.Y, Width, Height, Edges, Marks);
				int reach = HasFounder ? Chebyshev(point.X, point.Y, FounderX, FounderY) : 0;
				if (best < 0 || Beats(score, reach, point, bestScore, bestReach, Candidates[best]))
				{
					best = i;
					bestScore = score;
					bestReach = reach;
				}
				if (HasFounder && reach <= FounderReachCells && (near < 0 || Beats(score, reach, point, nearScore, nearReach, Candidates[near])))
				{
					near = i;
					nearScore = score;
					nearReach = reach;
				}
			}
			if (near >= 0 && nearScore >= bestScore - FounderTolerance)
			{
				Index = near;
				return LayoutOutcome.Founder;
			}
			Index = best;
			return LayoutOutcome.Grammar;
		}

		/// <summary>
		/// Whether one candidate should be preferred to another: the plan's opinion first, then
		/// the founder's own feet, then position, so a run always returns the same ground for
		/// the same settlement.
		/// </summary>
		public static bool Beats(int ScoreA, int ReachA, LayoutPoint A, int ScoreB, int ReachB, LayoutPoint B)
		{
			if (ScoreA != ScoreB)
			{
				return ScoreA > ScoreB;
			}
			if (ReachA != ReachB)
			{
				return ReachA < ReachB;
			}
			if (A.Y != B.Y)
			{
				return A.Y < B.Y;
			}
			return A.X < B.X;
		}
	}
}
