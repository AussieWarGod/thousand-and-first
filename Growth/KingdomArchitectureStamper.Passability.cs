using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		private static bool TryBlueprintPassAudit(ArchitectureLayoutSnapshot Snapshot,
			out string Failure)
		{
			Failure = null;
			for (int c = 0; c < Snapshot.Cells.Count; c++)
			{
				ArchitectureCellState cell = Snapshot.Cells[c];
				if (!cell.Claim) continue;
				bool solid = false;
				bool door = false;
				for (int p = 0; p < Snapshot.Placements.Count; p++)
				{
					ArchitecturePlacement placement = Snapshot.Placements[p];
					if (placement.X != cell.X || placement.Y != cell.Y) continue;
					GameObjectBlueprint blueprint = GameObjectFactory.Factory.GetBlueprintIfExists(
						placement.Blueprint);
					if (blueprint == null)
						return Fail("pass audit names missing blueprint " + placement.Blueprint,
							out Failure);
					bool isDoor = blueprint.HasPart("Door");
					door |= isDoor;
					if (!isDoor && blueprint.HasPart("Physics")
						&& blueprint.GetPartParameter("Physics", "Solid", false)) solid = true;
					if (cell.Passability == ArchitecturePassability.Walkable && !isDoor
						&& blueprint.HasPart("Physics")
						&& blueprint.GetPartParameter("Physics", "Solid", false))
						return Fail("walkable authored cell " + Coordinate(cell.X, cell.Y)
							+ " contains solid blueprint " + placement.Blueprint, out Failure);
				}
				if (cell.Passability == ArchitecturePassability.Blocked && (!solid || door))
					return Fail("blocked authored cell " + Coordinate(cell.X, cell.Y)
						+ " lacks one solid non-door concrete blueprint", out Failure);
				if (cell.Passability == ArchitecturePassability.Adjacent
					&& !HasCardinalWalkCell(Snapshot, cell.X, cell.Y))
					return Fail("adjacent-use authored cell " + Coordinate(cell.X, cell.Y)
						+ " has no cardinal walk/door use cell", out Failure);
			}
			return true;
		}

		private static bool TryVerifyPassabilityThrough(Zone Z,
			KingdomArchitectureIntent Intent, ArchitectureLayoutSnapshot Snapshot, string Lot,
			ArchitectureLayer Through, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = Snapshot.Cells[i];
				if (!cell.Claim || HighestLayerAt(Snapshot, cell.X, cell.Y) > (int)Through) continue;
				if (!TryVerifyPassabilityCell(Z, Intent, Snapshot, Lot, cell, out Failure))
					return false;
			}
			return true;
		}

		private static bool TryVerifyPassability(Zone Z, KingdomArchitectureIntent Intent,
			ArchitectureLayoutSnapshot Snapshot, string Lot, out string Failure)
		{
			return TryVerifyPassabilityThrough(Z, Intent, Snapshot, Lot,
				ArchitectureLayer.Object, out Failure);
		}

		private static bool TryVerifyPassabilityCell(Zone Z, KingdomArchitectureIntent Intent,
			ArchitectureLayoutSnapshot Snapshot, string Lot, ArchitectureCellState CellState,
			out string Failure)
		{
			Failure = null;
			int x;
			int y;
			if (!KingdomArchitectureRuntime.TryWorldCell(Snapshot, Intent.Rect, CellState,
				out x, out y, out Failure)) return false;
			Cell cell = Z.GetCell(x, y);
			if (cell == null) return Fail("authored pass cell left its exact zone", out Failure);
			bool authoredDoor = HasAuthoredDoor(cell, Lot, Intent.SnapshotHash);
			bool walk = cell.IsPassable() || authoredDoor;
			if (CellState.Passability == ArchitecturePassability.Walkable && !walk)
				return Fail("concrete authored walk cell is blocked at " + Coordinate(x, y), out Failure);
			if (CellState.Passability == ArchitecturePassability.Blocked
				&& (cell.IsPassable() || authoredDoor))
				return Fail("concrete authored blocked cell is passable or a door at "
					+ Coordinate(x, y), out Failure);
			if (CellState.Passability == ArchitecturePassability.Adjacent)
			{
				int[] dx = new int[4] { 0, 1, 0, -1 };
				int[] dy = new int[4] { -1, 0, 1, 0 };
				bool reached = false;
				for (int d = 0; d < 4 && !reached; d++)
				{
					ArchitectureCellState neighbour = FindCell(Snapshot,
						CellState.X + dx[d], CellState.Y + dy[d]);
					if (neighbour == null
						|| neighbour.Passability != ArchitecturePassability.Walkable) continue;
					int nx;
					int ny;
					if (!KingdomArchitectureRuntime.TryWorldCell(Snapshot, Intent.Rect, neighbour,
						out nx, out ny, out Failure)) return false;
					Cell use = Z.GetCell(nx, ny);
					reached = use != null && (use.IsPassable()
						|| HasAuthoredDoor(use, Lot, Intent.SnapshotHash));
				}
				if (!reached)
					return Fail("adjacent-use authored cell has no concrete cardinal use cell at "
						+ Coordinate(x, y), out Failure);
			}
			return true;
		}

		private static int HighestLayerAt(ArchitectureLayoutSnapshot Snapshot, int X, int Y)
		{
			int layer = -1;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (placement.X == X && placement.Y == Y && (int)placement.Layer > layer)
					layer = (int)placement.Layer;
			}
			return layer;
		}

		private static ArchitectureCellState FindCell(ArchitectureLayoutSnapshot Snapshot,
			int X, int Y)
		{
			if (X < 0 || X >= Snapshot.Width || Y < 0 || Y >= Snapshot.Height) return null;
			for (int i = 0; i < Snapshot.Cells.Count; i++)
				if (Snapshot.Cells[i].X == X && Snapshot.Cells[i].Y == Y) return Snapshot.Cells[i];
			return null;
		}

		private static bool HasCardinalWalkCell(ArchitectureLayoutSnapshot Snapshot, int X, int Y)
		{
			int[] dx = new int[4] { 0, 1, 0, -1 };
			int[] dy = new int[4] { -1, 0, 1, 0 };
			for (int i = 0; i < 4; i++)
			{
				ArchitectureCellState cell = FindCell(Snapshot, X + dx[i], Y + dy[i]);
				if (cell != null && cell.Claim
					&& cell.Passability == ArchitecturePassability.Walkable) return true;
			}
			return false;
		}

		private static bool HasAuthoredDoor(Cell Cell, string Lot, string Hash)
		{
			if (Cell == null) return false;
			List<GameObject> objects = Cell.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (GameObject.Validate(item) && item.IsDoor()
					&& item.GetIntProperty(ComponentSchemaProperty) == ComponentSchema
					&& item.GetStringProperty(KingdomPlots.PlotIdProperty) == Lot
					&& item.GetStringProperty(ComponentHashProperty) == Hash) return true;
			}
			return false;
		}

	}
}
