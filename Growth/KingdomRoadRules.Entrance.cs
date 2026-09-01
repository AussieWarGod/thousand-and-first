using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomRoadRules
	{
		// Canonical N/E/S/W is both the breadth-first tie law and the corner-exit tie law.
		// Keeping it canonical means the chosen route rotates with the authored building.
		private static readonly int[] EntranceStepX = new int[4] { 0, 1, 0, -1 };
		private static readonly int[] EntranceStepY = new int[4] { -1, 0, 1, 0 };

		/// <summary>
		/// Finds the exact authored way from a public or service entrance to the lot exterior. Claimed cells
		/// other than the entrance are never crossed: the way may use only unclaimed walk cells,
		/// then leave the first map edge reached by the fixed N/E/S/W breadth-first tie law.
		/// </summary>
		/// <param name="Snapshot">A complete frozen architecture snapshot.</param>
		/// <param name="Entrance">An exact road-entrance member of that snapshot.</param>
		/// <param name="Route">Receives canonical cells after the entrance through the map edge.</param>
		/// <param name="ExitX">Canonical outward x step at the chosen edge.</param>
		/// <param name="ExitY">Canonical outward y step at the chosen edge.</param>
		public static bool TryCanonicalEntranceEgress(ArchitectureLayoutSnapshot Snapshot,
			ArchitectureAnchor Entrance, IList<ArchitecturePoint> Route,
			out int ExitX, out int ExitY)
		{
			ExitX = 0;
			ExitY = 0;
			if (Route == null) return false;
			Route.Clear();
			if (Snapshot == null || Entrance == null || Snapshot.Cells == null
				|| Snapshot.Anchors == null || Snapshot.Width <= 0 || Snapshot.Height <= 0
				|| (long)Snapshot.Width * Snapshot.Height > KingdomArchitectureRules.MaxMapArea
				|| Snapshot.Cells.Count != Snapshot.Width * Snapshot.Height
				|| Entrance.X < 0 || Entrance.X >= Snapshot.Width
				|| Entrance.Y < 0 || Entrance.Y >= Snapshot.Height
				|| !IsExactRoadEntrance(Snapshot, Entrance)) return false;

			ArchitectureCellState[] cells = new ArchitectureCellState[Snapshot.Cells.Count];
			for (int i = 0; i < Snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = Snapshot.Cells[i];
				if (cell == null || cell.X < 0 || cell.X >= Snapshot.Width
					|| cell.Y < 0 || cell.Y >= Snapshot.Height) return false;
				int key = cell.Y * Snapshot.Width + cell.X;
				if (cells[key] != null) return false;
				cells[key] = cell;
			}
			int entranceKey = Entrance.Y * Snapshot.Width + Entrance.X;
			ArchitectureCellState entranceCell = cells[entranceKey];
			if (entranceCell == null || !KingdomArchitectureRules.IsClaimed(entranceCell.Claim)
				|| entranceCell.Passability != ArchitecturePassability.Walkable) return false;
			if (TryOutwardStep(Entrance.X, Entrance.Y, Snapshot.Width, Snapshot.Height,
				out ExitX, out ExitY)) return true;

			int[] parent = new int[cells.Length];
			for (int i = 0; i < parent.Length; i++) parent[i] = -1;
			int[] queue = new int[cells.Length];
			int head = 0;
			int tail = 0;
			parent[entranceKey] = entranceKey;
			queue[tail++] = entranceKey;
			int boundary = -1;
			while (head < tail && boundary < 0)
			{
				int current = queue[head++];
				int x = current % Snapshot.Width;
				int y = current / Snapshot.Width;
				for (int d = 0; d < EntranceStepX.Length; d++)
				{
					int nx = x + EntranceStepX[d];
					int ny = y + EntranceStepY[d];
					if (nx < 0 || nx >= Snapshot.Width || ny < 0 || ny >= Snapshot.Height) continue;
					int next = ny * Snapshot.Width + nx;
					ArchitectureCellState cell = cells[next];
					if (parent[next] >= 0 || cell == null
						|| KingdomArchitectureRules.IsClaimed(cell.Claim)
						|| cell.Passability != ArchitecturePassability.Walkable) continue;
					parent[next] = current;
					queue[tail++] = next;
					if (TryOutwardStep(nx, ny, Snapshot.Width, Snapshot.Height,
						out ExitX, out ExitY))
					{
						boundary = next;
						break;
					}
				}
			}
			if (boundary < 0) return false;
			List<ArchitecturePoint> reversed = new List<ArchitecturePoint>();
			for (int step = boundary; step != entranceKey; step = parent[step])
			{
				if (step < 0 || reversed.Count >= MaxRouteCells - KingdomPlotRules.RoadMargin)
					return false;
				reversed.Add(new ArchitecturePoint(step % Snapshot.Width, step / Snapshot.Width));
			}
			for (int i = reversed.Count - 1; i >= 0; i--) Route.Add(reversed[i]);
			return true;
		}

		/// <summary>
		/// Transforms one canonical egress into the exact world DoorToLane route. The returned
		/// intermediates include unclaimed lot ground and every reserved exterior-margin cell;
		/// the lane endpoint is one cell beyond that margin, matching <see cref="TryLane"/>.
		/// </summary>
		public static bool TryAuthoredLane(ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, ArchitectureAnchor Entrance,
			IList<ArchitecturePoint> Route, out int DoorX, out int DoorY,
			out int LaneX, out int LaneY)
		{
			DoorX = DoorY = LaneX = LaneY = 0;
			if (Route == null) return false;
			Route.Clear();
			List<ArchitecturePoint> canonical = new List<ArchitecturePoint>();
			if (!TryCanonicalEntranceEgress(Snapshot, Entrance, canonical,
				out int exitX, out int exitY)) return false;
			if (!KingdomArchitectureRules.TryWorldDimensions(Snapshot.Width, Snapshot.Height,
				Snapshot.Facing, out int width, out int height)
				|| Rect.Width != width || Rect.Height != height
				|| !KingdomArchitectureRules.TryToWorld(Rect.X1, Rect.Y1, Snapshot.Width,
					Snapshot.Height, Snapshot.Facing, Entrance.X, Entrance.Y, out DoorX, out DoorY))
				return false;
			List<ArchitecturePoint> world = new List<ArchitecturePoint>();
			for (int i = 0; i < canonical.Count; i++)
			{
				ArchitecturePoint point = canonical[i];
				if (!KingdomArchitectureRules.TryToWorld(Rect.X1, Rect.Y1, Snapshot.Width,
					Snapshot.Height, Snapshot.Facing, point.X, point.Y, out int x, out int y))
				{
					Route.Clear();
					return false;
				}
				world.Add(new ArchitecturePoint(x, y));
			}
			ArchitecturePoint edge = canonical.Count == 0
				? new ArchitecturePoint(Entrance.X, Entrance.Y) : canonical[canonical.Count - 1];
			if (!KingdomArchitectureRules.TryToWorld(Rect.X1, Rect.Y1, Snapshot.Width,
				Snapshot.Height, Snapshot.Facing, edge.X, edge.Y, out int edgeX, out int edgeY))
				return false;
			RotateStep(Snapshot.Facing, exitX, exitY, out int worldStepX, out int worldStepY);
			for (int distance = 1; distance <= KingdomPlotRules.RoadMargin; distance++)
			{
				if (!TryOffset(edgeX, edgeY, worldStepX, worldStepY, distance,
					out int x, out int y)) return false;
				world.Add(new ArchitecturePoint(x, y));
			}
			if (world.Count > MaxRouteCells || !TryOffset(edgeX, edgeY, worldStepX, worldStepY,
				KingdomPlotRules.RoadMargin + 1, out LaneX, out LaneY)
				|| KingdomPlotRules.Reserved(Rect).Contains(LaneX, LaneY))
			{
				Route.Clear();
				return false;
			}
			for (int i = 0; i < world.Count; i++) Route.Add(world[i]);
			return true;
		}

		private static bool IsExactRoadEntrance(ArchitectureLayoutSnapshot Snapshot,
			ArchitectureAnchor Entrance)
		{
			if (!(Entrance.Key == "entrance:public" || Entrance.Key == "entrance:service"
				|| (Entrance.Key != null && (Entrance.Key.StartsWith("entrance:public@",
					System.StringComparison.Ordinal) || Entrance.Key.StartsWith("entrance:service@",
					System.StringComparison.Ordinal))))) return false;
			for (int i = 0; i < Snapshot.Anchors.Count; i++)
			{
				ArchitectureAnchor item = Snapshot.Anchors[i];
				if (item != null && item.Key == Entrance.Key && item.X == Entrance.X
					&& item.Y == Entrance.Y && item.Access == Entrance.Access) return true;
			}
			return false;
		}

		private static bool TryOutwardStep(int X, int Y, int Width, int Height,
			out int StepX, out int StepY)
		{
			StepX = 0;
			StepY = 0;
			if (Y == 0) StepY = -1;
			else if (X == Width - 1) StepX = 1;
			else if (Y == Height - 1) StepY = 1;
			else if (X == 0) StepX = -1;
			else return false;
			return true;
		}

		private static void RotateStep(ArchitectureFacing Facing, int X, int Y,
			out int WorldX, out int WorldY)
		{
			if (Facing == ArchitectureFacing.East) { WorldX = -Y; WorldY = X; }
			else if (Facing == ArchitectureFacing.South) { WorldX = -X; WorldY = -Y; }
			else if (Facing == ArchitectureFacing.West) { WorldX = Y; WorldY = -X; }
			else { WorldX = X; WorldY = Y; }
		}

		private static bool TryOffset(int X, int Y, int StepX, int StepY, int Distance,
			out int ResultX, out int ResultY)
		{
			long x = (long)X + (long)StepX * Distance;
			long y = (long)Y + (long)StepY * Distance;
			ResultX = ResultY = 0;
			if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
				return false;
			ResultX = (int)x;
			ResultY = (int)y;
			return true;
		}
	}
}
