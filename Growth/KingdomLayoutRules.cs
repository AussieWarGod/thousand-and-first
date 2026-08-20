using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free grammar for where a settlement raises its next work. The whole of it is a
	/// function of what is ALREADY standing: casks gather by the casks, houses gather by the
	/// houses and stand back from the wall, the civic ground thickens where the settlement
	/// already lives, fields lie out past the last roof, and a wall extends the wall. Nothing
	/// here knows about a zone, a cell, or a game object &mdash; the caller passes coordinates
	/// in and gets a coordinate back, which is what makes the whole layout provable without a
	/// running game. The engine-coupled half (reading the zone, walking cells, checking what a
	/// cell will bear) is <c>KingdomLayout</c>, in the same folder.
	/// </summary>
	/// <remarks>
	/// Distances are Chebyshev, because Qud moves and reads distance in eight directions.
	/// Scores are points where higher is better and most terms are penalties, so a cell the
	/// plan is perfectly happy with scores zero.
	/// <para>
	/// The plan is deliberately allowed to have NO opinion, and says so
	/// (<see cref="LayoutOutcome.Defer"/>) rather than inventing one. A first building on
	/// empty ground has no neighbours to reason from: there is no settlement shape yet to
	/// answer to, so the founder's own ground wins and becomes the seed every later building
	/// is read against. The city's grammar starts from where the founder chose to stand.
	/// </para>
	/// </remarks>
	public static class KingdomLayoutRules
	{
		/// <summary>
		/// What a work is FOR, which is the only thing the plan needs to know about it. Derived
		/// from a building's <c>Category</c> so third-party designs are sited by the same
		/// grammar with no code (see <see cref="PurposeOf"/>).
		/// </summary>
		public enum LayoutPurpose
		{
			/// <summary>A category the plan does not recognise &mdash; including any a third-party
			/// mod invents. The plan defers rather than guessing at someone else's intent.</summary>
			Unknown = 0,
			/// <summary>Walls and towers. They belong on the frontier and extend each other.</summary>
			Defence = 1,
			/// <summary>Water and food stores. They gather where the settlement's water already is.</summary>
			Storage = 2,
			/// <summary>Where settlers sleep. Clusters, and stands back from the wall.</summary>
			Housing = 3,
			/// <summary>Craft, faith, knowledge, and the gathering places: the settled heart.</summary>
			Civic = 4,
			/// <summary>Worked ground. Lies in a ring out past the built-up centre.</summary>
			Field = 5,
			/// <summary>The dead. Quiet ground further out still, and rows lie together.</summary>
			Memorial = 6,
			/// <summary>A work sited by the ground itself &mdash; a wheel wants moving water, a
			/// sailvane wants wind. The plan cannot see either, so it never overrules the founder
			/// who walked to the spot.</summary>
			Sited = 7
		}

		/// <summary>What the plan did with a request.</summary>
		public enum LayoutOutcome
		{
			/// <summary>The plan has no opinion; the caller should place this the way it always
			/// did (beside the founder, else anywhere clear).</summary>
			Defer = 0,
			/// <summary>The plan chose the cell.</summary>
			Grammar = 1,
			/// <summary>The plan had an opinion and the founder's own ground satisfied it, so
			/// intent won. Still a real choice, not a fallback.</summary>
			Founder = 2,
			/// <summary>Nothing could be sited: no candidate ground was offered at all.</summary>
			None = 3
		}

		/// <summary>A position the plan may put something at.</summary>
		public struct LayoutPoint
		{
			public int X;

			public int Y;

			public LayoutPoint(int X, int Y)
			{
				this.X = X;
				this.Y = Y;
			}
		}

		/// <summary>Something the settlement already has standing, and what it is for. The plan
		/// reasons only from these, which is what makes it grow with the city.</summary>
		public struct LayoutMark
		{
			public int X;

			public int Y;

			public LayoutPurpose Purpose;

			public LayoutMark(int X, int Y, LayoutPurpose Purpose)
			{
				this.X = X;
				this.Y = Y;
				this.Purpose = Purpose;
			}
		}

		/// <summary>Penalty per cell of distance from the nearest work of the same kind. The
		/// strongest pull there is: like gathers to like before anything else.</summary>
		public const int AnchorWeight = 4;

		/// <summary>Penalty per cell of distance from the settled heart.</summary>
		public const int HeartWeight = 2;

		/// <summary>Penalty per cell a ring-sited work misses its ring by.</summary>
		public const int RingWeight = 3;

		/// <summary>Flat penalty for sitting hard against something already built, so a quarter
		/// keeps lanes through it instead of fusing into one block. Not applied to walls (a wall
		/// is a line and must touch), nor to fields and graves (rows abut).</summary>
		public const int CrowdPenalty = 10;

		/// <summary>How close counts as hard against something. One cell: the plan wants a
		/// walkable gap, not a courtyard.</summary>
		public const int CrowdRadius = 1;

		/// <summary>Flat penalty for putting anything but a defensive work in the frontier band.
		/// Large enough to beat any pull across a zone this size, but deliberately a penalty and
		/// not a ban: a settlement whose whole interior is rock must still be able to house its
		/// people somewhere.</summary>
		public const int FrontierPenalty = 60;

		/// <summary>Bonus per adjacent defensive work, for extending a line rather than starting
		/// a new one somewhere else along the same edge.</summary>
		public const int WallContinuityBonus = 20;

		/// <summary>Adjacent walls that still earn <see cref="WallContinuityBonus"/>. Two is a
		/// cell that closes a gap in the line, which is the best thing a new segment can be; a
		/// third neighbour and beyond is the line getting thicker instead of longer.</summary>
		public const int WallContinuityCap = 2;

		/// <summary>Penalty per adjacent wall past <see cref="WallContinuityCap"/>, so a
		/// settlement finishes its line before it doubles it. Small: a thick wall is wasteful,
		/// not wrong, and it still beats starting a fresh stub somewhere else.</summary>
		public const int WallThickenPenalty = 6;

		/// <summary>How far apart two defensive works still read as one line.</summary>
		public const int WallReachCells = 1;

		/// <summary>Chebyshev distance from the heart that worked ground wants.</summary>
		public const int FieldRingCells = 6;

		/// <summary>Chebyshev distance from the heart that the dead are given.</summary>
		public const int MemorialRingCells = 9;

		/// <summary>How near the founder a cell must be to count as their own ground. They stand
		/// on it, so the work goes up beside them.</summary>
		public const int FounderReachCells = 1;

		/// <summary>How much worse than the plan's best the founder's own ground may score and
		/// still win. Four cells of drift at <see cref="AnchorWeight"/>: the plan picks the
		/// quarter, the founder picks the spot inside it. Anything the plan feels strongly about
		/// &mdash; the frontier band, above all &mdash; costs far more than this and overrules
		/// them.</summary>
		public const int FounderTolerance = 16;

		/// <summary>Defensive works that stand in for a heart when nothing else has been raised
		/// yet. Three segments describe an enclosure; one describes a post in a field.</summary>
		public const int WallsFormHeartMinimum = 3;

		/// <summary>
		/// The purpose a building's <c>Category</c> names. Case-insensitive, and both spellings
		/// of defence are accepted because the catalog ships the American one and English
		/// authors will type the other. An unrecognised category &mdash; anything a third-party
		/// registry invents &mdash; is <see cref="LayoutPurpose.Unknown"/>, which makes the plan
		/// defer instead of filing someone else's building into a quarter it guessed at.
		/// </summary>
		/// <param name="Category">A <c>BuildEntry.Category</c>. Null or empty reads as Unknown.</param>
		public static LayoutPurpose PurposeOf(string Category)
		{
			if (string.IsNullOrEmpty(Category))
			{
				return LayoutPurpose.Unknown;
			}
			switch (Category.Trim().ToLowerInvariant())
			{
				case "defense":
				case "defence":
					return LayoutPurpose.Defence;
				case "storage":
					return LayoutPurpose.Storage;
				case "housing":
					return LayoutPurpose.Housing;
				case "craft":
				case "civic":
				case "faith":
				case "knowledge":
					return LayoutPurpose.Civic;
				case "food":
					return LayoutPurpose.Field;
				case "memorial":
					return LayoutPurpose.Memorial;
				case "power":
					return LayoutPurpose.Sited;
				default:
					return LayoutPurpose.Unknown;
			}
		}

		/// <summary>
		/// The clause a commission message ends with, naming the ground the plan chose in the
		/// settlement's own terms. Null when there is nothing to say &mdash; the plan deferred,
		/// nothing was sited, or the purpose has no quarter of its own &mdash; and the caller
		/// keeps its plain sentence.
		/// </summary>
		public static string PlacementClause(LayoutPurpose Purpose, LayoutOutcome Outcome)
		{
			if (Outcome == LayoutOutcome.Founder)
			{
				return "where you stand";
			}
			if (Outcome != LayoutOutcome.Grammar)
			{
				return null;
			}
			switch (Purpose)
			{
				case LayoutPurpose.Defence:
					return "on the line";
				case LayoutPurpose.Storage:
					return "beside the stores";
				case LayoutPurpose.Housing:
					return "among the homes";
				case LayoutPurpose.Civic:
					return "on the settled ground";
				case LayoutPurpose.Field:
					return "out past the last roof";
				case LayoutPurpose.Memorial:
					return "on the quiet ground";
				default:
					return null;
			}
		}

		/// <summary>Chebyshev distance, the one Qud walks in.</summary>
		public static int Chebyshev(int AX, int AY, int BX, int BY)
		{
			int dx = (AX > BX) ? (AX - BX) : (BX - AX);
			int dy = (AY > BY) ? (AY - BY) : (BY - AY);
			return (dx > dy) ? dx : dy;
		}

		/// <summary>
		/// The settled heart: the mean position of everything raised that is not a wall, because
		/// a wall is by definition at the edge and would drag the centre out to it. Falls back to
		/// the mean of the defensive works when there are at least
		/// <see cref="WallsFormHeartMinimum"/> of them and nothing else &mdash; the inside of a
		/// line you have already drawn is a meaningful centre, and one post is not.
		/// </summary>
		/// <returns>False when the settlement has no shape yet, in which case
		/// <paramref name="X"/> and <paramref name="Y"/> are zero and mean nothing.</returns>
		public static bool TryHeart(IList<LayoutMark> Marks, out int X, out int Y)
		{
			X = 0;
			Y = 0;
			if (Marks == null)
			{
				return false;
			}
			int sumX = 0;
			int sumY = 0;
			int count = 0;
			int wallX = 0;
			int wallY = 0;
			int walls = 0;
			for (int i = 0; i < Marks.Count; i++)
			{
				if (Marks[i].Purpose == LayoutPurpose.Defence)
				{
					wallX += Marks[i].X;
					wallY += Marks[i].Y;
					walls++;
					continue;
				}
				sumX += Marks[i].X;
				sumY += Marks[i].Y;
				count++;
			}
			if (count > 0)
			{
				X = (sumX + count / 2) / count;
				Y = (sumY + count / 2) / count;
				return true;
			}
			if (walls >= WallsFormHeartMinimum)
			{
				X = (wallX + walls / 2) / walls;
				Y = (wallY + walls / 2) / walls;
				return true;
			}
			return false;
		}

		/// <summary>Distance to the nearest work of one purpose.</summary>
		/// <returns>False when nothing of that purpose stands yet.</returns>
		public static bool TryNearest(IList<LayoutMark> Marks, LayoutPurpose Purpose, int X, int Y, out int Distance)
		{
			Distance = 0;
			if (Marks == null)
			{
				return false;
			}
			bool found = false;
			for (int i = 0; i < Marks.Count; i++)
			{
				if (Marks[i].Purpose != Purpose)
				{
					continue;
				}
				int distance = Chebyshev(X, Y, Marks[i].X, Marks[i].Y);
				if (!found || distance < Distance)
				{
					Distance = distance;
					found = true;
				}
			}
			return found;
		}

		/// <summary>Distance to the nearest work of any purpose.</summary>
		/// <returns>False when nothing stands yet.</returns>
		public static bool TryNearestAny(IList<LayoutMark> Marks, int X, int Y, out int Distance)
		{
			Distance = 0;
			if (Marks == null)
			{
				return false;
			}
			bool found = false;
			for (int i = 0; i < Marks.Count; i++)
			{
				int distance = Chebyshev(X, Y, Marks[i].X, Marks[i].Y);
				if (!found || distance < Distance)
				{
					Distance = distance;
					found = true;
				}
			}
			return found;
		}

		/// <summary>How many works of one purpose the settlement has standing here.</summary>
		public static int CountOf(IList<LayoutMark> Marks, LayoutPurpose Purpose)
		{
			if (Marks == null)
			{
				return 0;
			}
			int count = 0;
			for (int i = 0; i < Marks.Count; i++)
			{
				if (Marks[i].Purpose == Purpose)
				{
					count++;
				}
			}
			return count;
		}

		/// <summary>How many works of one purpose stand within <paramref name="Radius"/>.</summary>
		public static int CountWithin(IList<LayoutMark> Marks, LayoutPurpose Purpose, int X, int Y, int Radius)
		{
			if (Marks == null)
			{
				return 0;
			}
			int count = 0;
			for (int i = 0; i < Marks.Count; i++)
			{
				if (Marks[i].Purpose == Purpose && Chebyshev(X, Y, Marks[i].X, Marks[i].Y) <= Radius)
				{
					count++;
				}
			}
			return count;
		}

		/// <summary>
		/// Whether the plan has anything to say about siting this purpose here. False means the
		/// founder decides, and is the honest answer in three cases: nothing is built yet, the
		/// purpose is one the ground decides (<see cref="LayoutPurpose.Sited"/>) or one the plan
		/// does not recognise, or a wall is wanted where there is neither a frontier to put it on
		/// nor a line to extend.
		/// </summary>
		public static bool HasOpinion(LayoutPurpose Purpose, IList<LayoutMark> Marks, KingdomRules.Frontier Edges)
		{
			if (Marks == null || Marks.Count == 0)
			{
				return false;
			}
			if (Purpose == LayoutPurpose.Defence)
			{
				return Edges != KingdomRules.Frontier.None && CountOf(Marks, LayoutPurpose.Defence) > 0;
			}
			if (Purpose == LayoutPurpose.Unknown || Purpose == LayoutPurpose.Sited)
			{
				return false;
			}
			if (TryHeart(Marks, out _, out _))
			{
				return true;
			}
			return CountOf(Marks, Purpose) > 0;
		}

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
