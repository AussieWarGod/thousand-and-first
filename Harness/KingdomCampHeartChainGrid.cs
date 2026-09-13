using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomCampHeartChainGrid
	{
		internal static IEnumerable<KingdomPlotRules.PlotRect> Candidates()
		{
			for (int side = 0; side < 2; side++)
				for (int y = 2; y <= 14; y += 6)
					for (int column = 0; column < (side == 0 ? 4 : 3); column++)
					{
						int x = (side == 0 ? 2 : 52) + column * 7;
						yield return new KingdomPlotRules.PlotRect(x, y, x + 5, y + 3);
					}
		}
	}
}
