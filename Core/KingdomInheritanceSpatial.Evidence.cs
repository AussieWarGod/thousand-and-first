using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritanceSpatial
	{
		private static bool TrySourceRows(Simulation.City.KingdomCityBook Book,
			KingdomSealRecord Record, out List<SourceWork> Rows, out string Failure)
		{
			Rows = new List<SourceWork>();
			Failure = "";
			for (int i = 0; i < Book.WorkIds.Count && Rows.Count < KingdomSealRecord.MaxWorks; i++)
			{
				if (i >= Book.WorkZoneIds.Count || Book.WorkZoneIds[i] != Record.GroundZoneId)
					continue;
				if (i >= Book.WorkDesignKeys.Count || i >= Book.WorkAnchorsX.Count
					|| i >= Book.WorkAnchorsY.Count || i >= Book.WorkConditions.Count) continue;
				string key;
				string design = Book.WorkDesignKeys[i];
				if (!KingdomInheritRules.TrySemanticKeyForBlueprint(design, out key))
				{
					key = KingdomSealRules.SanitizeToken(design, KingdomSealRecord.MaxIdChars);
					if (!KingdomInheritRules.IsStableSemanticKey(key)) continue;
				}
				int x = Book.WorkAnchorsX[i];
				int y = Book.WorkAnchorsY[i];
				if (x < 0 || x > 255 || y < 0 || y > 255) continue;
				int at = Rows.Count;
				if (at >= Record.WorkKeys.Count || Record.WorkKeys[at] != key
					|| Record.WorkX[at] != x || Record.WorkY[at] != y)
				{
					Failure = "the city book changed while its spatial seal was witnessed";
					Rows = null;
					return false;
				}
				Rows.Add(new SourceWork
				{
					WorkId = Book.WorkIds[i], Blueprint = design, X = x, Y = y
				});
			}
			if (Rows.Count != Record.WorkKeys.Count)
			{
				Failure = "the city book's spatial work rows are incomplete";
				Rows = null;
				return false;
			}
			return true;
		}

		private static bool TryExactRoot(Zone Zone, SourceWork Row, out GameObject Root,
			out string Failure)
		{
			Root = null;
			Failure = "";
			Cell cell = Zone.GetCell(Row.X, Row.Y);
			if (cell == null)
			{
				Failure = "a sealed work anchor is outside its witnessed zone";
				return false;
			}
			int count = 0;
			for (int i = 0; i < cell.Objects.Count; i++)
			{
				GameObject item = cell.Objects[i];
				if (!GameObject.Validate(item) || item.Blueprint != Row.Blueprint
					|| Simulation.City.KingdomCityRules.StableId(item.ID) != Row.WorkId) continue;
				Root = item;
				count++;
			}
			if (count != 1)
			{
				Root = null;
				Failure = "a sealed work root is absent, duplicated, moved, or changed";
				return false;
			}
			return true;
		}

		private static bool HasArchitectureEvidence(GameObject Root)
		{
			return Root.HasIntProperty(KingdomArchitectureRuntime.SchemaProperty)
				|| Root.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty)
				|| Root.HasStringProperty(KingdomArchitectureRuntime.SnapshotProperty)
				|| Root.HasStringProperty(KingdomArchitectureRuntime.HashProperty)
				|| Root.HasStringProperty(KingdomArchitectureRuntime.PlanKeyProperty)
				|| Root.HasStringProperty(KingdomArchitectureRuntime.BindingKeyProperty);
		}

		private static bool TryRoadEvidence(Zone Zone,
			IList<KingdomInheritanceSpatialRules.Rect> Rects, out bool[,] Roads,
			out string Failure)
		{
			Roads = new bool[KingdomInheritanceSpatialRules.Width,
				KingdomInheritanceSpatialRules.Height];
			Failure = "";
			for (int y = 0; y < Zone.Height; y++)
			{
				for (int x = 0; x < Zone.Width; x++)
				{
					GameObject floor;
					KingdomPhysicalLookupState state = KingdomRoads.FindOurFloor(
						Zone.GetCell(x, y), out floor);
					if (state == KingdomPhysicalLookupState.Ambiguous)
					{
						Failure = "road evidence is physically ambiguous at " + x + "," + y;
						return false;
					}
					if (state == KingdomPhysicalLookupState.Exact) Roads[x, y] = true;
				}
			}
			List<KingdomRoadRules.WornCell> tally;
			string error;
			if (!KingdomRoadRules.TryDecode(Zone.GetZoneProperty(KingdomRoads.TallyProperty,
				null), out tally, out error))
			{
				Failure = error ?? "the road tally is malformed";
				return false;
			}
			for (int i = 0; i < tally.Count; i++)
			{
				KingdomRoadRules.WornCell cell = tally[i];
				if (cell.X >= 0 && cell.Y >= 0 && cell.X < Zone.Width && cell.Y < Zone.Height
					&& KingdomRoadRules.WearAt(cell.Traffic) > KingdomRoadRules.WearState.Untouched)
					Roads[cell.X, cell.Y] = true;
			}
			for (int y = 0; y < Zone.Height; y++)
				for (int x = 0; x < Zone.Width; x++)
					for (int i = 0; Roads[x, y] && i < Rects.Count; i++)
						if (Rects[i].Contains(x, y)) Roads[x, y] = false;
			return true;
		}

	}
}
