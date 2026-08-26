using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPlotRules
	{
		// --- Refusals (STANDARDS 7b: nothing stalls in silence) ---------------------------

		/// <summary>Names the thing standing in the way and where it stands. The one refusal the
		/// protection law makes unavoidable: the settlement will not take that ground, ever, so
		/// the founder has to be told which ground and why.</summary>
		public static string RefuseObstruction(string What, int X, int Y)
		{
			return "{{C|" + What + "}} stands at " + X + ", " + Y + ". The plot would have to take that ground, and nothing standing there is the settlement's to take. Clear it yourself, or stake the work elsewhere.";
		}

		/// <summary>Water refuses the plot and is never filled. The river is why the site was
		/// chosen.</summary>
		public static string RefuseLiquid(int X, int Y)
		{
			return "There is open water at " + X + ", " + Y + ". A plot is never laid over water, and the water is never filled in.";
		}

		/// <summary>Names the stage that would lift a tier gate.</summary>
		public static string RefuseStage(PlotSize Size, string SeatName, GrowthStage Stage)
		{
			return "A " + SizeName(Size) + " plot is the work of a " + StageForSize(Size).ToString().ToLowerInvariant()
				+ ". " + SeatName + " is a " + Stage.ToString().ToLowerInvariant() + " yet.";
		}

		/// <summary>Names a weather-dependent design refused underground.</summary>
		public static string RefuseSky(string Name)
		{
			return "The " + Name + " wants weather, and there is none under the rock. Raise it under open sky.";
		}

		/// <summary>No rect of this tier fits any clear ground here.</summary>
		public static string RefuseRoom(PlotSize Size)
		{
			return "There is no clear ground here wide enough for a " + SizeName(Size) + " plot and the lanes a settlement keeps around one.";
		}

		/// <summary>The zone is laid out to its budget: more plot would leave no road.</summary>
		public static string RefuseBudget(string SeatName)
		{
			return "This ground is laid out. What already stands at " + SeatName + ", and the lanes between, leave no room for another plot until something is struck.";
		}

		/// <summary>
		/// The improvement wants more ground than the plot it stands on holds. Refused BY NAME
		/// rather than by silently siting the larger tier somewhere else or quietly shrinking it:
		/// the ceiling was a choice the founder made when they staked this ground, and this is the
		/// sentence that tells them the choice has arrived.
		/// </summary>
		/// <param name="Name">What would be raised.</param>
		/// <param name="Width">Cells across it wants.</param>
		/// <param name="Height">Cells down it wants.</param>
		/// <param name="Plot">The tier of plot it stands on.</param>
		public static string RefuseFootprint(string Name, int Width, int Height, PlotSize Plot)
		{
			string ground = TryDimensions(Plot, out var plotWidth, out var plotHeight)
				? ("a " + SizeName(Plot) + " plot is " + SpanWord(plotWidth, plotHeight))
				: "this ground is less than that";
			return "The {{C|" + Name + "}} wants more ground than this plot holds: it stands "
				+ SpanWord(Width, Height) + ", and " + ground
				+ ". Strike what is here and stake larger ground, or leave it as it is.";
		}

		/// <summary>A design that needs weather, refused a tier that has declared itself
		/// closed.</summary>
		public static string RefuseRoofSky(string Name, RoofState Roof)
		{
			return "The " + Name + " wants weather, and this tier of it is " + RoofWord(Roof)
				+ ". Raise it under something that lets the sky in.";
		}

		/// <summary>
		/// The grown building would stand on the cell a yard trade is worked in. Never taken down
		/// on its own: the founder is told which trade is in the way and chooses, because a
		/// household's sideline is theirs and the settlement does not tidy it away to make room.
		/// </summary>
		public static string RefuseYardWork(string Name, string SuccessorName, string WorkName)
		{
			return "The " + Name + " could be raised into " + KingdomUpgradeRules.Article(SuccessorName)
				+ ", but the {{C|" + WorkName + "}} in its yard stands on ground the larger building needs."
				+ " Let the trade go first, and the work can begin. Nothing in the yard comes down on its own.";
		}

		/// <summary>A design people are meant to sleep in, on a tier with nothing over it.</summary>
		public static string RefuseBedRoof(string Name)
		{
			return "Nobody sleeps in the open. The " + Name + " is " + RoofWord(RoofState.Open)
				+ ", and a bed wants canvas over it at the very least.";
		}
	}
}
