using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		private static void RaiseFrame(r_KingdomPlotWorks Works, Zone Z,
			KingdomPlotRules.PlotRect Rect, KingdomPlotRules.RoofState Roof)
		{
			if (!KingdomPlotRules.RaisesWalls(Roof)) return;
			PlaceMarked(Works, Z.GetCell(Rect.X1, Rect.Y1), FrameBlueprint);
			PlaceMarked(Works, Z.GetCell(Rect.X2, Rect.Y1), FrameBlueprint);
			PlaceMarked(Works, Z.GetCell(Rect.X1, Rect.Y2), FrameBlueprint);
			PlaceMarked(Works, Z.GetCell(Rect.X2, Rect.Y2), FrameBlueprint);
		}

		private static void RaiseWalls(r_KingdomPlotWorks Works, Zone Z,
			KingdomPlotRules.PlotRect Rect, KingdomPlotRules.RoofState Roof)
		{
			if (!KingdomPlotRules.Encloses(Roof)) return;
			TakeDownFrame(Works, Z, Rect);
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null) continue;
					bool border = Rect.IsBorder(x, y);
					if (border && Works.HasDoor && x == Works.DoorX && y == Works.DoorY)
					{
						PlaceMarked(Works, cell, DoorBlueprint);
						continue;
					}
					if (border)
					{
						if (KingdomPlotRules.RaisesWalls(Roof)
							&& !string.IsNullOrEmpty(Works.WallBlueprint))
							PlaceMarked(Works, cell, Works.WallBlueprint);
						continue;
					}
					PlaceMarked(Works, cell, FloorBlueprint);
				}
		}

		private static void TakeDownFrame(r_KingdomPlotWorks Works, Zone Z,
			KingdomPlotRules.PlotRect Rect)
		{
			string id = Works.ParentObject?.GetStringProperty(PlotIdProperty);
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null) continue;
					List<GameObject> standing = new List<GameObject>(cell.GetObjects());
					for (int i = 0; i < standing.Count; i++)
						if (standing[i] != null && standing[i].Blueprint == FrameBlueprint
							&& standing[i].GetStringProperty(PlotIdProperty) == id)
						{
							bool removed;
							try { removed = standing[i].Destroy(null, Silent: true); }
							finally
							{
								KingdomSurvey.ObserveCurrentTopologyInActive(Z, standing[i]);
							}
							if (removed && !GameObject.Validate(standing[i]))
								KingdomSurvey.ObserveRemovedFromActive(Z, standing[i]);
						}
				}
		}

		private static GameObject PlaceMarked(r_KingdomPlotWorks Works, Cell C, string Blueprint)
		{
			return PlaceForPlot(C, Blueprint,
				Works.ParentObject?.GetStringProperty(PlotIdProperty));
		}
	}
}
