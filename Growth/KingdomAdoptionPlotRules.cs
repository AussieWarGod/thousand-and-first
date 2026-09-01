using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Engine-free exact geometry for a player-designated open plot.</summary>
	public static class KingdomAdoptionPlotRules
	{
		public static bool TryCenteredCells(int CenterX, int CenterY,
			KingdomPlotRules.PlotSize Size, int ZoneWidth, int ZoneHeight,
			out KingdomPlotRules.PlotRect Rect, out List<ArchitecturePoint> Cells,
			out string Failure)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			Cells = new List<ArchitecturePoint>(); Failure = null;
			if (!KingdomPlotRules.TryDimensions(Size, out int width, out int height))
				return Fail("open adoption has no catalogue plot tier", out Failure);
			int x1 = CenterX - (width - 1) / 2;
			int y1 = CenterY - (height - 1) / 2;
			Rect = new KingdomPlotRules.PlotRect(x1, y1,
				x1 + width - 1, y1 + height - 1);
			if (!KingdomPlotRules.ValidZoneRect(Rect, ZoneWidth, ZoneHeight))
				return Fail("the projected open plot crosses the edge of this ground", out Failure);
			long area = (long)width * height;
			if (area < 1 || area > KingdomDesignationRules.MaxCellsPerDesignation)
				return Fail("the projected open plot exceeds the designation bound", out Failure);
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
				for (int x = Rect.X1; x <= Rect.X2; x++)
					Cells.Add(new ArchitecturePoint(x, y));
			return true;
		}

		public static bool Contains(KingdomPlotRules.PlotRect Rect, int X, int Y)
		{
			return X >= Rect.X1 && X <= Rect.X2 && Y >= Rect.Y1 && Y <= Rect.Y2;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
