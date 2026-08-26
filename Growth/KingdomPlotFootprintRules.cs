using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPlotRules
	{
		// --- Footprints: the building's own ground, inside the plot -----------------------

		/// <summary>
		/// The invariant the two layers owe each other, and the only one: a tier's footprint fits
		/// inside its plot. Checked when the catalogue loads and again, by name, at the moment an
		/// improvement would grow past it.
		/// </summary>
		public static bool FootprintFits(PlotSize Plot, int Width, int Height)
		{
			if (Width < 1 || Height < 1)
			{
				return false;
			}
			return TryDimensions(Plot, out var plotWidth, out var plotHeight) && Width <= plotWidth && Height <= plotHeight;
		}

		/// <summary>The cells a design's tier takes, which is the whole plot for a tier that
		/// declares no footprint of its own.</summary>
		/// <returns>False for a null spec and for one that is not a plot at all.</returns>
		public static bool TryFootprint(PlotSpec Spec, out int Width, out int Height)
		{
			Width = 0;
			Height = 0;
			if (Spec == null)
			{
				return false;
			}
			if (Spec.FillsPlot)
			{
				return TryDimensions(Spec.Size, out Width, out Height);
			}
			Width = Spec.FootprintWidth;
			Height = Spec.FootprintHeight;
			return true;
		}

		/// <summary>The smallest tier of plot that holds a footprint, or
		/// <see cref="PlotSize.None"/> when nothing a settlement lays does.</summary>
		public static PlotSize SmallestPlotFor(int Width, int Height)
		{
			PlotSize[] order = new PlotSize[4] { PlotSize.Small, PlotSize.Medium, PlotSize.Large, PlotSize.Huge };
			for (int i = 0; i < order.Length; i++)
			{
				if (FootprintFits(order[i], Width, Height))
				{
					return order[i];
				}
			}
			return PlotSize.None;
		}

		/// <summary>
		/// Where the building sits inside its plot: the position whose centre lies nearest the
		/// heart, so a building fronts the settlement and its yard lies behind it, and so a tier
		/// that grows eats the yard from the street inwards rather than jumping across the plot.
		/// Ties break north-then-west, exactly as <see cref="TryDoor"/> does, which is what makes
		/// the same plot lay out the same way every time it is read.
		/// </summary>
		/// <returns>False when the footprint is larger than the plot on either span, in which
		/// case <paramref name="Footprint"/> is a zero rect and means nothing.</returns>
		public static bool TryFootprintWithin(PlotRect Plot, int Width, int Height, int HeartX, int HeartY, out PlotRect Footprint)
		{
			Footprint = default(PlotRect);
			if (Width < 1 || Height < 1 || Width > Plot.Width || Height > Plot.Height)
			{
				return false;
			}
			bool found = false;
			int bestDistance = 0;
			for (int y = Plot.Y1; y + Height - 1 <= Plot.Y2; y++)
			{
				for (int x = Plot.X1; x + Width - 1 <= Plot.X2; x++)
				{
					PlotRect rect = new PlotRect(x, y, x + Width - 1, y + Height - 1);
					int distance = KingdomLayoutRules.Chebyshev(rect.CenterX, rect.CenterY, HeartX, HeartY);
					if (!found || distance < bestDistance)
					{
						found = true;
						bestDistance = distance;
						Footprint = rect;
					}
				}
			}
			return found;
		}

		/// <summary>Whether a footprint lies wholly inside a plot.</summary>
		public static bool Within(PlotRect Plot, PlotRect Footprint)
		{
			return Footprint.X1 >= Plot.X1 && Footprint.Y1 >= Plot.Y1
				&& Footprint.X2 <= Plot.X2 && Footprint.Y2 <= Plot.Y2;
		}

		// --- The yard: plot minus footprint, recomputed per tier ---------------------------

		/// <summary>
		/// The yard, as up to four rectangles: everything inside the plot the building does not
		/// stand on, north band and south band full width, then the two side bands beside the
		/// footprint. Recomputed from the CURRENT tier, so a building that grows takes its yard
		/// back a band at a time rather than the yard being a thing stored anywhere.
		/// </summary>
		/// <returns>An empty list for a footprint that fills its plot, and for one that is not
		/// inside the plot at all, which has no yard anybody can name.</returns>
		public static List<PlotRect> YardBands(PlotRect Plot, PlotRect Footprint)
		{
			List<PlotRect> bands = new List<PlotRect>();
			if (!Within(Plot, Footprint))
			{
				return bands;
			}
			if (Footprint.Y1 > Plot.Y1)
			{
				bands.Add(new PlotRect(Plot.X1, Plot.Y1, Plot.X2, Footprint.Y1 - 1));
			}
			if (Footprint.Y2 < Plot.Y2)
			{
				bands.Add(new PlotRect(Plot.X1, Footprint.Y2 + 1, Plot.X2, Plot.Y2));
			}
			if (Footprint.X1 > Plot.X1)
			{
				bands.Add(new PlotRect(Plot.X1, Footprint.Y1, Footprint.X1 - 1, Footprint.Y2));
			}
			if (Footprint.X2 < Plot.X2)
			{
				bands.Add(new PlotRect(Footprint.X2 + 1, Footprint.Y1, Plot.X2, Footprint.Y2));
			}
			return bands;
		}

		/// <summary>Cells of yard a tier leaves. Zero for a tier that fills its plot.</summary>
		public static int YardArea(PlotRect Plot, PlotRect Footprint)
		{
			List<PlotRect> bands = YardBands(Plot, Footprint);
			int area = 0;
			for (int i = 0; i < bands.Count; i++)
			{
				area += bands[i].Area;
			}
			return area;
		}

		/// <summary>Whether a cell is yard: inside the plot, and not under the building.</summary>
		public static bool InYard(PlotRect Plot, PlotRect Footprint, int X, int Y)
		{
			return Plot.Contains(X, Y) && !Footprint.Contains(X, Y);
		}

		/// <summary>
		/// Whether growing from one tier to the next takes ground the old one did not stand on.
		/// False for a tier that grows into the same rect, which stamps nothing and can never be
		/// refused for want of room.
		/// </summary>
		public static bool TakesNewGround(PlotRect Old, PlotRect Grown)
		{
			for (int y = Grown.Y1; y <= Grown.Y2; y++)
			{
				for (int x = Grown.X1; x <= Grown.X2; x++)
				{
					if (!Old.Contains(x, y))
					{
						return true;
					}
				}
			}
			return false;
		}
	}
}
