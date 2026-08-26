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

	}
}
