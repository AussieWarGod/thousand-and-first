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
	public static partial class KingdomLayoutRules
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

	}
}
