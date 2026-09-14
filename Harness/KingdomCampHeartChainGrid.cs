using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomCampHeartChainGrid
	{
		internal static bool ClearsPaidApproach(KingdomPlotRules.PlotRect Candidate,
			KingdomPlotRules.PlotRect PaidPlot)
		{
			int margin = KingdomPlotRules.RoadMargin + 1;
			var approach = new KingdomPlotRules.PlotRect(PaidPlot.X1 - margin,
				PaidPlot.Y1 - margin, PaidPlot.X2 + margin, PaidPlot.Y2 + margin);
			return !KingdomPlotRules.Overlaps(Candidate, approach);
		}

		// Complete persisted legacy footprints, not the single-cell producer roots.
		internal static IEnumerable<KingdomPlotRules.PlotRect> WaterCourts()
		{
			yield return new KingdomPlotRules.PlotRect(23, 0, 30, 5);
			yield return new KingdomPlotRules.PlotRect(23, 12, 30, 17);
			foreach (int x in new[] { 0, 9, 18, 53, 62, 71 })
				yield return new KingdomPlotRules.PlotRect(x, 19, x + 7, 24);
		}

		internal static bool ClearsWaterFootprints(KingdomPlotRules.PlotRect Candidate)
		{
			foreach (var court in WaterCourts())
				if (KingdomPlotRules.Overlaps(Candidate, court)) return false;
			return true;
		}

		internal static IEnumerable<KingdomPlotRules.PlotRect> Candidates()
		{
			for (int side = 0; side < 2; side++)
				for (int y = 2; y <= 14; y += 6)
					for (int column = 0; column < (side == 0 ? 4 : 3); column++)
					{
						int x = (side == 0 ? 2 : 53) + column * 7;
						var rect = new KingdomPlotRules.PlotRect(x, y, x + 5, y + 3);
						if (ClearsWaterFootprints(rect)) yield return rect;
					}
		}
	}
}
