using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestRules
	{
		/// <summary>Guestbook entries kept per city before the oldest is trimmed. Smaller than
		/// <c>KingdomChronicle.MaxEntries</c> because the guestbook is a side reading, not the
		/// settlement's primary record &mdash; every guestbook event is also written into the
		/// chronicle proper, which keeps the full 200.</summary>
		public const int GuestbookMaxEntries = 30;

		/// <summary>
		/// Classifies a plot rect's own dimensions back into <see cref="KingdomPlotRules.PlotSize"/>
		/// by area, since <c>KingdomPlotRules.PlotRect</c> carries no tier of its own &mdash; only
		/// the four corners <c>KingdomPlots.TryReadRect</c> reads off a real object's stamp. Every
		/// rect this mod stamps matches one of <c>KingdomPlotRules.TryDimensions</c>'s four exact
		/// bands, so area thresholds at each band's own size classify it exactly; scoped here
		/// rather than added to <c>KingdomPlotRules</c> because the guestbook is the only caller
		/// that needs a rect's tier back out of its dimensions instead of the other way around.
		/// </summary>
		public static KingdomPlotRules.PlotSize ClassifyRectTier(int Width, int Height)
		{
			int area = Width * Height;
			if (area <= 0)
			{
				return KingdomPlotRules.PlotSize.None;
			}
			if (area <= KingdomPlotRules.SmallWidth * KingdomPlotRules.SmallHeight)
			{
				return KingdomPlotRules.PlotSize.Small;
			}
			if (area <= KingdomPlotRules.MediumWidth * KingdomPlotRules.MediumHeight)
			{
				return KingdomPlotRules.PlotSize.Medium;
			}
			if (area <= KingdomPlotRules.LargeWidth * KingdomPlotRules.LargeHeight)
			{
				return KingdomPlotRules.PlotSize.Large;
			}
			return KingdomPlotRules.PlotSize.Huge;
		}

		/// <summary>Measures one normalized exact designation and refuses sparse geometry instead
		/// of letting an irregular room borrow the tier of its bounding rectangle.</summary>
		internal static bool TryExactPlotBounds(IReadOnlyList<KingdomBenefitCell> Cells,
			out int Width, out int Height)
		{
			Width = 0;
			Height = 0;
			if (Cells == null) return false;
			int minX = int.MaxValue, minY = int.MaxValue;
			int maxX = int.MinValue, maxY = int.MinValue, occupied = 0;
			for (int i = 0; i < Cells.Count; i++)
			{
				KingdomBenefitCell cell = Cells[i];
				if ((cell.Use & KingdomBenefitCellUse.Plot) == 0) continue;
				if (cell.X < minX) minX = cell.X;
				if (cell.X > maxX) maxX = cell.X;
				if (cell.Y < minY) minY = cell.Y;
				if (cell.Y > maxY) maxY = cell.Y;
				occupied++;
			}
			if (occupied == 0) return false;
			Width = maxX - minX + 1;
			Height = maxY - minY + 1;
			return (long)Width * Height == occupied;
		}

	}
}
