using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using XRL;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomArchitectureGalleryWishes
	{
		private static bool TryFindCanvas(Zone Zone, int Width, int Height,
			out KingdomPlotRules.PlotRect Rect, out string Failure)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			Failure = null;
			if (Zone == null || Width < 1 || Height < 1 || Width + 2 > Zone.Width
				|| Height + 2 > Zone.Height)
				return Fail("The selected pose cannot fit inside this zone with a review margin.", out Failure);
			HashSet<int> connections = ConnectionCells(Zone);
			Cell player = The.Player?.CurrentCell;
			int best = int.MaxValue;
			for (int y = 1; y + Height < Zone.Height; y++)
				for (int x = 1; x + Width < Zone.Width; x++)
				{
					KingdomPlotRules.PlotRect candidate = new KingdomPlotRules.PlotRect(
						x, y, x + Width - 1, y + Height - 1);
					if (!SafeCanvas(Zone, candidate, connections, player)) continue;
					int distance = player == null ? y * Zone.Width + x
						: Math.Abs(candidate.CenterX - player.X) + Math.Abs(candidate.CenterY - player.Y);
					if (distance >= best) continue;
					Rect = candidate;
					best = distance;
				}
			if (best == int.MaxValue)
				return Fail("No untouched passable rectangle with a one-cell review margin fits here. "
					+ "Move to an empty test zone; the gallery will not clear live terrain or objects.",
					out Failure);
			return true;
		}

		private static bool SafeCanvas(Zone Zone, KingdomPlotRules.PlotRect Rect,
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
					if (Rect.Contains(x, y))
					{
						string blocker;
						if (KingdomPlots.ReadGround(cell, out blocker)
							!= KingdomPlotRules.GroundKind.Bare
							|| (system != null && KingdomConstruction.HasActiveAt(system, Zone, cell)))
							return false;
						List<GameObject> objects = cell.GetObjects();
						for (int i = 0; i < objects.Count; i++)
							if (GameObject.Validate(objects[i])
								&& (objects[i].IsCreature || objects[i].IsPlayer()
									|| objects[i].GetIntProperty(GallerySchemaProperty) == GallerySchema))
								return false;
					}
				}
			return true;
		}

		private static bool TryCreateSyntheticAuthority(Zone Zone,
			ArchitectureLayoutSnapshot Snapshot, KingdomArchitectureIntent Intent, string Receipt,
			out GameObject Synthetic, out string Failure)
		{
			Synthetic = null;
			Failure = null;
			ArchitecturePlacement existing = null;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
				if (Snapshot.Placements[i].ExistingAuthority)
				{
					if (existing != null)
						return Fail("The gallery case declares more than one existing authority.", out Failure);
					existing = Snapshot.Placements[i];
				}
			if (existing == null) return true;
			foreach (GameObject item in Zone.GetObjects())
				if (GameObject.Validate(item)
					&& item.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1)
					return Fail("A real first basin already stands in this zone. Heart-plan gallery cases "
						+ "require an isolated test zone and never borrow or alter that relic.", out Failure);
			int x;
			int y;
			if (existing.Blueprint != KingdomPlots.HeartRelicBlueprint
				|| !KingdomArchitectureRuntime.TryWorldPlacement(Snapshot, Intent.Rect, existing,
					out x, out y, out Failure)) return false;
			Synthetic = GameObject.Create(existing.Blueprint);
			if (!GameObject.Validate(Synthetic))
				return Fail("The synthetic gallery basin blueprint created no object.", out Failure);
			Synthetic.SetIntProperty(KingdomPlots.HeartRelicProperty, 1);
			Synthetic.SetIntProperty(GallerySyntheticProperty, 1);
			StampGallery(Synthetic, Receipt, "synthetic:first-basin");
			Cell cell = Zone.GetCell(x, y);
			GameObject accepted = cell.AddObject(Synthetic, NoStack: true, Silent: true);
			return (ReferenceEquals(accepted, Synthetic)
				&& ReferenceEquals(Synthetic.CurrentCell, cell) && Synthetic.InInventory == null)
				|| Fail("The engine refused, replaced, or displaced the exact synthetic gallery basin.",
					out Failure);
		}
	}
}
