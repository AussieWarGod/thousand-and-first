using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomCampHeartChainGrid
	{
		// KingdomPlotRules.FounderReachCells (Growth/KingdomPlotSitingRules.cs), which this
		// engine-free shard cannot reference from the portable suite. The main suite pins both
		// spellings together.
		internal const int FounderReach = 2;

		/// <summary>The paid Medium source tent the chain commission must quote before it pays
		/// (#282): the founder's own ground below the north court footprint and its reserved lane
		/// clear of the final heart. Every home and court below is proved against this rect.
		/// </summary>
		internal static KingdomPlotRules.PlotRect SourceTent => new KingdomPlotRules.PlotRect(22, 6, 29, 11);

		internal static KingdomPlotRules.PlotRect NorthCourt => new KingdomPlotRules.PlotRect(23, 0, 30, 5);

		/// <summary>Founder column for the commission: one tent width, its road margin and the
		/// founder's reach west of the final heart, so the easternmost tent this founder reaches
		/// keeps its reserved lane off that heart.</summary>
		internal static int CommissionX(KingdomPlotRules.PlotRect FinalHeart, int TentWidth)
		{
			return FinalHeart.X1 - TentWidth - KingdomPlotRules.RoadMargin - FounderReach;
		}

		/// <summary>Founder row for the commission. Production prefers the founder's own ground
		/// within <see cref="FounderReach"/> cells and breaks score ties toward the smaller Y1, so
		/// the northernmost tent this founder reaches starts on the first row below the north
		/// court footprint.</summary>
		internal static int CommissionY(int TentHeight)
		{
			return NorthCourt.Y2 + TentHeight + FounderReach;
		}

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
			yield return NorthCourt;
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

		/// <summary>The accepted grid first, then three transposed 4x6 slots on the retained S
		/// canvas binding. The source tent's approach removes grid lots (16,2) and (16,8), so the
		/// first two slots restore eighteen homes and the third is a spare the ordinary refusal
		/// path may reach. Against the old Small tent the grid alone supplies eighteen.</summary>
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
			yield return new KingdomPlotRules.PlotRect(16, 3, 19, 8);
			yield return new KingdomPlotRules.PlotRect(74, 2, 77, 7);
			yield return new KingdomPlotRules.PlotRect(74, 9, 77, 14);
		}
	}
}
