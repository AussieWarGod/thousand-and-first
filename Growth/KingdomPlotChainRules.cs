using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPlotRules
	{
		// --- Staking foresight: the whole chain's ground, before the stake goes in ---------

		/// <summary>
		/// One tier of a design's improvement chain, reduced to what the founder needs to see
		/// before choosing how much ground to stake: what it is called, and how much of the plot
		/// it will stand on when the settlement gets that far.
		/// </summary>
		public readonly struct ChainStep
		{
			public readonly string Key;

			public readonly string Name;

			public readonly int Width;

			public readonly int Height;

			public readonly RoofState Roof;

			public ChainStep(string Key, string Name, int Width, int Height, RoofState Roof)
			{
				this.Key = Key;
				this.Name = Name;
				this.Width = Width;
				this.Height = Height;
				this.Roof = Roof;
			}

			public int Area => Width * Height;
		}

		/// <summary>Whether every tier of a chain stands on one plot of this tier.</summary>
		/// <param name="FirstUnfit">Index of the first tier that wants more ground than the plot
		/// holds, or -1 when they all fit. An empty chain fits everything.</param>
		public static bool ChainFits(PlotSize Plot, IList<ChainStep> Chain, out int FirstUnfit)
		{
			FirstUnfit = -1;
			if (Chain == null)
			{
				return true;
			}
			for (int i = 0; i < Chain.Count; i++)
			{
				if (!FootprintFits(Plot, Chain[i].Width, Chain[i].Height))
				{
					FirstUnfit = i;
					return false;
				}
			}
			return true;
		}

		/// <summary>The smallest plot that holds a whole chain, or <see cref="PlotSize.None"/>
		/// when no tier a settlement lays does.</summary>
		public static PlotSize SmallestPlotForChain(IList<ChainStep> Chain)
		{
			if (Chain == null || Chain.Count == 0)
			{
				return PlotSize.None;
			}
			int width = 0;
			int height = 0;
			for (int i = 0; i < Chain.Count; i++)
			{
				if (Chain[i].Width > width)
				{
					width = Chain[i].Width;
				}
				if (Chain[i].Height > height)
				{
					height = Chain[i].Height;
				}
			}
			return SmallestPlotFor(width, height);
		}

		/// <summary>
		/// The plot tiers a founder may actually choose to stake for a design: never smaller than
		/// the design's own declared plot or than its first tier needs, never larger than the
		/// settlement has grown into. The choice is the ceiling: stake big for room to grow, or
		/// tight for the yard trade sooner and take what that costs later.
		/// </summary>
		/// <returns>An empty list when the design is not a plot at all, when its own first tier
		/// fits no plot, or when the settlement is not yet a settlement enough to lay one.</returns>
		public static List<PlotSize> StakeableSizes(PlotSize Declared, GrowthStage Stage, IList<ChainStep> Chain)
		{
			List<PlotSize> sizes = new List<PlotSize>();
			PlotSize floor = Declared;
			if (Chain != null && Chain.Count > 0)
			{
				PlotSize needed = SmallestPlotFor(Chain[0].Width, Chain[0].Height);
				if (needed == PlotSize.None)
				{
					return sizes;
				}
				if (needed > floor)
				{
					floor = needed;
				}
			}
			if (floor == PlotSize.None)
			{
				return sizes;
			}
			PlotSize ceiling = MaxSizeForStage(Stage);
			for (int size = (int)floor; size <= (int)ceiling; size++)
			{
				sizes.Add((PlotSize)size);
			}
			return sizes;
		}

		/// <summary>How the mod says a rectangle's size out loud.</summary>
		public static string SpanWord(int Width, int Height)
		{
			return Width + " by " + Height;
		}

		/// <summary>The chain's ground, tier by tier, in the order the settlement will build
		/// it.</summary>
		/// <returns>Null for an empty chain, which has nothing to foresee.</returns>
		public static string ChainFootprintLine(IList<ChainStep> Chain)
		{
			if (Chain == null || Chain.Count == 0)
			{
				return null;
			}
			string line = null;
			for (int i = 0; i < Chain.Count; i++)
			{
				string piece = Chain[i].Name + " " + SpanWord(Chain[i].Width, Chain[i].Height);
				line = (line == null) ? piece : (line + ", then " + piece);
			}
			return line;
		}

		/// <summary>One line for one stakeable tier, as the founder reads it in the list: how big
		/// the ground is, how far up the chain it carries, and what is left over for a yard
		/// meanwhile.</summary>
		public static string StakeOptionLine(PlotSize Plot, IList<ChainStep> Chain)
		{
			if (!TryDimensions(Plot, out var width, out var height))
			{
				return null;
			}
			string ground = SizeName(Plot) + " ground, " + SpanWord(width, height);
			if (Chain == null || Chain.Count == 0)
			{
				return ground;
			}
			bool fits = ChainFits(Plot, Chain, out var unfit);
			if (unfit == 0)
			{
				return ground + ": too little ground for the work itself";
			}
			int yard = width * height - Chain[0].Area;
			return ground + (fits ? ": holds every tier" : (": holds as far as the " + Chain[unfit - 1].Name))
				+ ", " + yard + ((yard == 1) ? " cell" : " cells") + " of yard to begin with";
		}

		/// <summary>
		/// What the founder is told before the stake goes in: the ground they are about to claim,
		/// every tier that will ever stand on it, and where the ceiling falls if they stake tight.
		/// Foresight rather than a warning: staking tight is a real choice, not a mistake, and the
		/// sentence says so.
		/// </summary>
		public static string ForesightLine(PlotSize Plot, IList<ChainStep> Chain)
		{
			if (!TryDimensions(Plot, out var width, out var height) || Chain == null || Chain.Count == 0)
			{
				return null;
			}
			string line = "A " + SizeName(Plot) + " plot is " + SpanWord(width, height) + ". " + ChainFootprintLine(Chain) + ".";
			if (Chain.Count == 1)
			{
				return line + " It never grows: what it takes now is what it takes.";
			}
			if (ChainFits(Plot, Chain, out var unfit))
			{
				return line + " Every tier it grows into stands on this ground.";
			}
			PlotSize whole = SmallestPlotForChain(Chain);
			string ceiling = " The " + Chain[unfit].Name + " wants " + SpanWord(Chain[unfit].Width, Chain[unfit].Height)
				+ ", which this plot does not hold. "
				+ ((whole == PlotSize.None)
					? "No plot this settlement lays holds the whole chain."
					: ("A " + SizeName(whole) + " plot is the smallest that holds all of it."));
			return line + ceiling + " Stake larger ground for room to grow, or stake here and take"
				+ " the ceiling: what outgrows this plot waits until the ground is struck and staked again.";
		}
	}
}
