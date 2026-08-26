using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPlotRules
	{
		// --- Zone interior and the road budget -------------------------------------------

		/// <summary>
		/// Cells kept clear of plots at every edge of a zone, so the settlement's own ground never
		/// starts hard against the zone seam. On Qud's 80x25 surface zone this leaves the 76x21
		/// interior the plot budget is reckoned against.
		/// </summary>
		public const int ZoneMargin = 2;

		/// <summary>
		/// The lane a plot reserves on every side. Roads are never drawn in this mod &mdash; the
		/// gap between two plots' reserved rects IS the road, and it is what settler routes wear
		/// into a path later. One cell: a settlement wants to walk between its buildings, not to
		/// hold a parade.
		/// </summary>
		public const int RoadMargin = 1;

		/// <summary>
		/// The share of a zone's interior that stays lane, yard, and open ground rather than plot.
		/// Reckoned from the brief's own mature budget on an 80x25 zone &mdash; one XL, two or
		/// three L, four or five M, six to eight S is about six parts plot to four parts
		/// everything else &mdash; and it is what stops a settlement tiling itself solid.
		/// </summary>
		public const int RoadBudgetPercent = 40;

		/// <summary>The rect plots may be laid in, inset from every edge by
		/// <see cref="ZoneMargin"/>.</summary>
		/// <returns>False for a zone too small to have an interior at all.</returns>
		public static bool TryInterior(int Width, int Height, out PlotRect Interior)
		{
			Interior = default(PlotRect);
			if (Width <= ZoneMargin * 2 || Height <= ZoneMargin * 2)
			{
				return false;
			}
			Interior = new PlotRect(ZoneMargin, ZoneMargin, Width - 1 - ZoneMargin, Height - 1 - ZoneMargin);
			return true;
		}

		/// <summary>Whether <paramref name="Rect"/> lies wholly inside <paramref name="Bounds"/>.</summary>
		public static bool Fits(PlotRect Rect, PlotRect Bounds)
		{
			return Rect.X1 >= Bounds.X1 && Rect.Y1 >= Bounds.Y1 && Rect.X2 <= Bounds.X2 && Rect.Y2 <= Bounds.Y2;
		}

		/// <summary>The rect plus its reserved lane. Two plots may not overlap each other's
		/// reserved rects, which is the whole of the road rule.</summary>
		public static PlotRect Reserved(PlotRect Rect)
		{
			return new PlotRect(Rect.X1 - RoadMargin, Rect.Y1 - RoadMargin, Rect.X2 + RoadMargin, Rect.Y2 + RoadMargin);
		}

		/// <summary>Whether two rects share any cell.</summary>
		public static bool Overlaps(PlotRect A, PlotRect B)
		{
			return A.X1 <= B.X2 && B.X1 <= A.X2 && A.Y1 <= B.Y2 && B.Y1 <= A.Y2;
		}

		/// <summary>
		/// Whether a proposed plot would crowd any plot already laid: its own rect against every
		/// existing plot's RESERVED rect, so the lane between them survives whichever plot was
		/// laid first.
		/// </summary>
		public static bool CrowdsExisting(PlotRect Rect, IList<PlotRect> Existing)
		{
			if (Existing == null)
			{
				return false;
			}
			for (int i = 0; i < Existing.Count; i++)
			{
				if (Overlaps(Rect, Reserved(Existing[i])))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Plot cells a zone's interior may hold before the lanes are eaten.</summary>
		public static int PlotAreaAllowance(int Width, int Height)
		{
			if (!TryInterior(Width, Height, out var interior))
			{
				return 0;
			}
			return interior.Area * (100 - RoadBudgetPercent) / 100;
		}

		/// <summary>How much plot a zone already carries, for the budget check.</summary>
		public static int LaidArea(IList<PlotRect> Existing)
		{
			if (Existing == null)
			{
				return 0;
			}
			int area = 0;
			for (int i = 0; i < Existing.Count; i++)
			{
				area += Existing[i].Area;
			}
			return area;
		}

		/// <summary>
		/// Whether laying one more plot of this tier would spend more of the zone than
		/// <see cref="RoadBudgetPercent"/> leaves for plots. Checked before the ground is walked,
		/// so a founder is told the ground is full rather than watching a search fail silently.
		/// </summary>
		public static bool WouldExceedBudget(IList<PlotRect> Existing, PlotSize Size, int Width, int Height)
		{
			if (!TryDimensions(Size, out var plotWidth, out var plotHeight))
			{
				return false;
			}
			return LaidArea(Existing) + plotWidth * plotHeight > PlotAreaAllowance(Width, Height);
		}
	}
}
