using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		private static bool HeartGroundLawful(Zone Zone, KingdomPlotRules.PlotRect Target,
			string HeartLot, IList<GameObject> Blockers, out string Failure)
		{
			Failure = null; HashSet<string> yielding = new HashSet<string>();
			for (int i = 0; i < Blockers.Count; i++) yielding.Add(
				Blockers[i].GetStringProperty(KingdomPlots.PlotIdProperty));
			for (int y = Target.Y1; y <= Target.Y2; y++)
				for (int x = Target.X1; x <= Target.X2; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null)
					{
						Failure = "The next heart rung crosses the zone boundary."; return false;
					}
					foreach (GameObject item in cell.GetObjects())
					{
						if (!GameObject.Validate(item) || item.IsCreature || item.IsPlayer()) continue;
						string lot = item.GetStringProperty(KingdomPlots.PlotIdProperty);
						if (lot == HeartLot || yielding.Contains(lot)
							|| !KingdomPlotRules.Refuses(KingdomPlots.ReadObject(item))) continue;
						Failure = "The next heart rung is protected by "
							+ KingdomDesign.ReferenceFor(item, item.ShortDisplayNameStripped)
							+ " at " + x + "," + y + ".";
						return false;
					}
				}
			return true;
		}

		private static List<KingdomPlotRules.PlotRect> FixedRects(KingdomSurvey Survey,
			string HeartLot, IList<GameObject> Blockers, KingdomPlotRules.PlotRect Target)
		{
			HashSet<GameObject> moving = new HashSet<GameObject>(Blockers);
			List<KingdomPlotRules.PlotRect> result = new List<KingdomPlotRules.PlotRect> { Target };
			for (int i = 0; i < Blockers.Count; i++)
				if (KingdomPlots.TryReadRect(Blockers[i], out KingdomPlotRules.PlotRect movingRect))
					result.Add(movingRect);
			for (int i = 0; i < Survey.PlotRoots.Count; i++)
			{
				GameObject item = Survey.PlotRoots[i];
				if (!GameObject.Validate(item) || moving.Contains(item)
					|| item.GetStringProperty(KingdomPlots.PlotIdProperty) == HeartLot) continue;
				if (KingdomPlots.TryReadRect(item, out KingdomPlotRules.PlotRect rect)) result.Add(rect);
			}
			return result;
		}

		private static List<KingdomLayoutRules.LayoutMark> FixedMarks(KingdomSurvey Survey,
			string HeartLot, IList<GameObject> Blockers)
		{
			HashSet<string> moving = new HashSet<string>();
			for (int i = 0; i < Blockers.Count; i++) moving.Add(
				Blockers[i].GetStringProperty(KingdomPlots.PlotIdProperty));
			List<KingdomLayoutRules.LayoutMark> result = new List<KingdomLayoutRules.LayoutMark>();
			for (int i = 0; i < Survey.LayoutRoots.Count; i++)
			{
				GameObject item = Survey.LayoutRoots[i];
				string lot = item.GetStringProperty(KingdomPlots.PlotIdProperty);
				if (lot != HeartLot && moving.Contains(lot)) continue;
				if (KingdomLayout.TryReadMark(item, out KingdomLayoutRules.LayoutMark mark))
					result.Add(mark);
			}
			return result;
		}
	}
}
