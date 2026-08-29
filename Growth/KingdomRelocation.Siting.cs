using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		private static bool TryChooseDestination(KingdomSystem System, Zone Zone,
			GameObject Root, KingdomPlotRules.PlotRect Source,
			IList<KingdomPlotRules.PlotRect> Fixed,
			IList<KingdomLayoutRules.LayoutMark> Marks,
			IDictionary<string, KingdomPlotRules.PlotRect> Overrides,
			out KingdomPlotRules.PlotRect Destination, out string Failure)
		{
			Destination = default(KingdomPlotRules.PlotRect); Failure = null;
			if (!KingdomPlotRules.TryInterior(Zone.Width, Zone.Height,
				out KingdomPlotRules.PlotRect interior))
			{
				Failure = "This zone has no lawful relocation interior."; return false;
			}
			List<KingdomPlotRules.PlotRect> candidates = new List<KingdomPlotRules.PlotRect>();
			for (int y = interior.Y1; y + Source.Height - 1 <= interior.Y2; y++)
				for (int x = interior.X1; x + Source.Width - 1 <= interior.X2; x++)
				{
					KingdomPlotRules.PlotRect rect = new KingdomPlotRules.PlotRect(x, y,
						x + Source.Width - 1, y + Source.Height - 1);
					if (KingdomPlotRules.CrowdsExisting(rect, Fixed)
						|| !GroundCanReceive(Zone, rect)) continue;
					candidates.Add(rect);
				}
			if (candidates.Count == 0)
			{
				Failure = "No lawful clear ground can receive the whole yielding plot."; return false;
			}
			string plot = Root.GetStringProperty(KingdomPlots.PlotIdProperty);
			if (Overrides != null && Overrides.TryGetValue(plot,
				out KingdomPlotRules.PlotRect chosen))
			{
				for (int i = 0; i < candidates.Count; i++)
					if (Same(candidates[i], chosen)
						&& TryArchitectureDestination(System, Zone, Root, chosen, out Failure))
					{
						Destination = chosen; return true;
					}
				Failure = Failure ?? "The founder's chosen ground is no longer lawful.";
				return false;
			}
			KingdomLayoutRules.LayoutPurpose purpose = KingdomLayoutRules.LayoutPurpose.Unknown;
			if (KingdomData.TryGetBuilding(KingdomUpgrade.DesignKeyOf(Root),
				out KingdomRules.BuildEntry entry)) purpose = KingdomLayout.PurposeOfEntry(entry);
			KingdomPlotRules.PlotSize size = KingdomPlotRules.SmallestPlotFor(
				Source.Width, Source.Height);
			KingdomRules.Frontier edges = KingdomRules.FrontierEdges(Zone.ZoneID,
				System.ClaimedZones);
			bool hasRite = KingdomPlots.TryRiteGround(Zone, out int riteX, out int riteY);
			bool hasSurvey = KingdomPlots.TrySurveyedHeart(Zone,
				out KingdomPlotRules.PlotRect survey);
			Cell founder = The.Player?.CurrentCell;
			bool hasFounder = founder != null && founder.ParentZone == Zone;
			while (candidates.Count > 0)
			{
				KingdomPlotRules.ChooseRect(purpose, size, Zone.Width, Zone.Height, edges,
					Marks, candidates, hasFounder, hasFounder ? founder.X : 0,
					hasFounder ? founder.Y : 0, hasRite, riteX, riteY, out int index,
					hasSurvey, survey, KingdomPlots.RiteWeight(Zone));
				if (index < 0) index = KingdomPlots.NearestIndex(candidates, hasFounder,
					hasFounder ? founder.X : 0, hasFounder ? founder.Y : 0);
				if (index < 0) break;
				KingdomPlotRules.PlotRect candidate = candidates[index];
				if (TryArchitectureDestination(System, Zone, Root, candidate, out Failure))
				{
					Destination = candidate; return true;
				}
				candidates.RemoveAt(index);
			}
			Failure = Failure ?? "No lawful ground carries the plot's frozen architecture.";
			return false;
		}

		private static bool GroundCanReceive(Zone Zone, KingdomPlotRules.PlotRect Rect)
		{
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null) return false;
					foreach (GameObject item in cell.GetObjects())
						if (GameObject.Validate(item) && !item.IsCreature && !item.IsPlayer()
							&& KingdomPlotRules.Refuses(KingdomPlots.ReadObject(item))) return false;
				}
			return true;
		}

		private static bool Same(KingdomPlotRules.PlotRect A, KingdomPlotRules.PlotRect B)
		{
			return A.X1 == B.X1 && A.Y1 == B.Y1 && A.X2 == B.X2 && A.Y2 == B.Y2;
		}

		private static void RemoveRect(List<KingdomPlotRules.PlotRect> Rects,
			KingdomPlotRules.PlotRect Source, KingdomPlotRules.PlotRect NeverRemove)
		{
			for (int i = Rects.Count - 1; i >= 0; i--)
				if (Same(Rects[i], Source) && !Same(Rects[i], NeverRemove))
				{ Rects.RemoveAt(i); return; }
		}
	}
}
