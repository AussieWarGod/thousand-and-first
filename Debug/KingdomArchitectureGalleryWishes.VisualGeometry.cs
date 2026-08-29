using System;
using System.Collections.Generic;
using System.Globalization;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomArchitectureGalleryWishes
	{
		private sealed class VisualCreated
		{
			public GameObject Item;
			public string Role;
		}

		private static bool TryFindVisualCanvas(Zone Zone, VisualCase Case,
			out KingdomPlotRules.PlotRect Rect, out string Failure)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			Failure = null;
			if (Zone == null || Case == null || Case.Width < 1 || Case.Height < 1
				|| Case.Width + 2 > Zone.Width || Case.Height + 2 > Zone.Height)
				return Fail("The visual case cannot fit in this zone with a review margin.", out Failure);
			HashSet<int> connections = ConnectionCells(Zone);
			Cell player = The.Player?.CurrentCell;
			int best = int.MaxValue;
			for (int y = 1; y + Case.Height < Zone.Height; y++)
				for (int x = 1; x + Case.Width < Zone.Width; x++)
				{
					KingdomPlotRules.PlotRect candidate = new KingdomPlotRules.PlotRect(
						x, y, x + Case.Width - 1, y + Case.Height - 1);
					if (!SafeVisualCanvas(Zone, candidate, connections, player)) continue;
					int distance = player == null ? y * Zone.Width + x
						: Math.Abs(candidate.CenterX - player.X) + Math.Abs(candidate.CenterY - player.Y);
					if (distance >= best) continue;
					Rect = candidate;
					best = distance;
				}
			if (best == int.MaxValue)
				return Fail("No untouched passable canvas fits. Use an isolated, empty test zone; "
					+ "the gallery never clears live terrain.", out Failure);
			return true;
		}

		private static bool SafeVisualCanvas(Zone Zone, KingdomPlotRules.PlotRect Rect,
			HashSet<int> Connections, Cell Player)
		{
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			for (int y = Rect.Y1 - 1; y <= Rect.Y2 + 1; y++)
				for (int x = Rect.X1 - 1; x <= Rect.X2 + 1; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null || (Player != null && cell == Player)
						|| Connections.Contains(y * Zone.Width + x) || cell.HasStairs()
						|| cell.HasObjectWithPart("StairsUp") || cell.HasObjectWithPart("StairsDown")
						|| cell.HasOpenLiquidVolume() || !cell.IsPassable()) return false;
					if (!Rect.Contains(x, y)) continue;
					string blocker;
					if (KingdomPlots.ReadGround(cell, out blocker) != KingdomPlotRules.GroundKind.Bare
						|| KingdomRoads.FindOurFloor(cell, out _) != KingdomPhysicalLookupState.Absent
						|| (system != null && KingdomConstruction.HasActiveAt(system, Zone, cell))) return false;
					List<GameObject> objects = cell.GetObjects();
					for (int i = 0; i < objects.Count; i++)
						if (GameObject.Validate(objects[i]) && (objects[i].IsCreature
							|| objects[i].IsPlayer()
							|| objects[i].GetIntProperty(GallerySchemaProperty) == GallerySchema
							|| objects[i].GetIntProperty(VisualSchemaProperty) == VisualGallerySchema))
							return false;
				}
			return true;
		}

		private static bool TryVisualRect(VisualCase Case, out KingdomPlotRules.PlotRect Rect,
			out string Failure)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			Failure = null;
			if (!VisualGalleryActive() || Case == null)
				return Fail("No exact non-plot/road visual gallery is active.", out Failure);
			int x = The.Player.GetIntProperty(VisualXProperty);
			int y = The.Player.GetIntProperty(VisualYProperty);
			int width = The.Player.GetIntProperty(VisualWidthProperty);
			int height = The.Player.GetIntProperty(VisualHeightProperty);
			if (width != Case.Width || height != Case.Height || x < 0 || y < 0)
				return Fail("The active visual canvas receipt is malformed.", out Failure);
			Rect = new KingdomPlotRules.PlotRect(x, y, x + width - 1, y + height - 1);
			return true;
		}

		private static bool TryVisualItems(Zone Zone, VisualCase Case, string Receipt,
			out List<VisualCreated> Items, out string Failure)
		{
			Items = new List<VisualCreated>();
			Failure = null;
			HashSet<string> roles = new HashSet<string>(StringComparer.Ordinal);
			foreach (GameObject item in Zone.GetObjects())
			{
				if (!GameObject.Validate(item) || item.IsPlayer()
					|| item.GetIntProperty(VisualSchemaProperty) != VisualGallerySchema) continue;
				if (item.GetStringProperty(VisualReceiptProperty) != Receipt
					|| item.GetStringProperty(VisualCaseProperty) != Case.Key)
					return Fail("This zone contains a foreign or orphaned visual-gallery object.", out Failure);
				string role = item.GetStringProperty(VisualRoleProperty);
				if (string.IsNullOrEmpty(role) || !roles.Add(role))
					return Fail("The visual-gallery roles are absent or duplicated.", out Failure);
				Items.Add(new VisualCreated { Item = item, Role = role });
			}
			if (Items.Count != Case.ExpectedObjects)
				return Fail("The exact visual-gallery object set is absent or duplicated.", out Failure);
			Items.Sort(delegate(VisualCreated a, VisualCreated b)
			{
				return string.CompareOrdinal(a.Role, b.Role);
			});
			return true;
		}

		private static string VisualDigest(VisualCase Case, Zone Zone,
			KingdomPlotRules.PlotRect Rect, List<VisualCreated> Items, string Extra)
		{
			List<string> rows = new List<string>();
			rows.Add(Case.Key + "|" + Zone.ZoneID + "|" + Rect.X1 + "," + Rect.Y1 + ","
				+ Rect.X2 + "," + Rect.Y2 + "|" + (Extra ?? ""));
			for (int i = 0; i < Items.Count; i++)
			{
				GameObject item = Items[i].Item;
				Cell cell = item?.CurrentCell;
				Render render = item?.GetPart<Render>();
				rows.Add(Items[i].Role + "|" + (item?.Blueprint ?? "<null>") + "|"
					+ (cell == null ? "<null>" : cell.X + "," + cell.Y) + "|"
					+ LiquidDeclaration(item) + "|"
					+ (render?.RenderString ?? "") + "|" + (render?.ColorString ?? "") + "|"
					+ (render?.DetailColor ?? "") + "|" + (render?.Tile ?? "") + "|"
					+ item.GetIntProperty(KingdomRoads.PathStateProperty).ToString(
						CultureInfo.InvariantCulture));
			}
			rows.Sort(StringComparer.Ordinal);
			return Hash(string.Join("\n", rows.ToArray()));
		}

		private static string LiquidDeclaration(GameObject Item)
		{
			if (!GameObject.Validate(Item)) return "";
			r_KingdomLiquidConduit conduit = Item.GetPart<r_KingdomLiquidConduit>();
			if (conduit != null) return "main:" + (conduit.Liquid ?? "") + ":" + (conduit.Joins ?? "<null>");
			r_KingdomLiquidTap tap = Item.GetPart<r_KingdomLiquidTap>();
			if (tap != null) return "tap:" + (tap.Liquid ?? "") + ":" + (tap.Joins ?? "<null>");
			r_KingdomLiquidCrossover crossing = Item.GetPart<r_KingdomLiquidCrossover>();
			return crossing == null ? "" : "crossing:" + (crossing.Pairs ?? "<null>");
		}
	}
}
