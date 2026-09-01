using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRules
	{
		/// <summary>
		/// Refuses an undeclared hole from a walled building cell into authored exterior ground.
		/// Open, soft, and natural cells make no enclosure promise. A structure or an explicit
		/// entrance/exit/door/threshold anchor is an authored boundary opening, never a bare leak.
		/// </summary>
		public static bool TryValidateEnclosure(ArchitectureLayoutSnapshot Snapshot,
			out string Failure)
		{
			Failure = null;
			if (Snapshot == null || Snapshot.Width <= 0 || Snapshot.Height <= 0
				|| (long)Snapshot.Width * Snapshot.Height > MaxMapArea
				|| Snapshot.Cells == null
				|| Snapshot.Cells.Count != Snapshot.Width * Snapshot.Height
				|| Snapshot.Placements == null || Snapshot.Anchors == null)
				return Fail("enclosure snapshot is absent, malformed, or incomplete", out Failure);
			Dictionary<int, ArchitectureCellState> cells = new Dictionary<int, ArchitectureCellState>();
			HashSet<int> structures = new HashSet<int>();
			HashSet<int> openings = new HashSet<int>();
			for (int i = 0; i < Snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = Snapshot.Cells[i];
				int key = cell == null ? -1 : CellKey(cell.X, cell.Y, Snapshot.Width);
				if (cell == null || cell.X < 0 || cell.X >= Snapshot.Width
					|| cell.Y < 0 || cell.Y >= Snapshot.Height
					|| !KnownPassability(cell.Passability) || !KnownCover(cell.Cover)
					|| cells.ContainsKey(key))
					return Fail("enclosure snapshot has a malformed or duplicate cell", out Failure);
				cells.Add(key, cell);
			}
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (placement == null || placement.X < 0 || placement.X >= Snapshot.Width
					|| placement.Y < 0 || placement.Y >= Snapshot.Height)
					return Fail("enclosure snapshot has a malformed placement", out Failure);
				if (placement.Layer == ArchitectureLayer.Structure)
					structures.Add(CellKey(placement.X, placement.Y, Snapshot.Width));
			}
			for (int i = 0; i < Snapshot.Anchors.Count; i++)
			{
				ArchitectureAnchor anchor = Snapshot.Anchors[i];
				if (anchor == null || anchor.X < 0 || anchor.X >= Snapshot.Width
					|| anchor.Y < 0 || anchor.Y >= Snapshot.Height || !ValidKey(anchor.Key))
					return Fail("enclosure snapshot has a malformed anchor", out Failure);
				if (DeclaredOpening(AnchorRole(anchor.Key)))
					openings.Add(CellKey(anchor.X, anchor.Y, Snapshot.Width));
			}
			int[] dx = new int[4] { 0, 1, 0, -1 };
			int[] dy = new int[4] { -1, 0, 1, 0 };
			foreach (KeyValuePair<int, ArchitectureCellState> pair in cells)
			{
				ArchitectureCellState cell = pair.Value;
				if (cell.Claim != ArchitectureClaim.Building
					|| cell.Cover != ArchitectureCover.Walled
					|| structures.Contains(pair.Key) || openings.Contains(pair.Key)) continue;
				for (int direction = 0; direction < 4; direction++)
				{
					int x = cell.X + dx[direction];
					int y = cell.Y + dy[direction];
					if (x < 0 || x >= Snapshot.Width || y < 0 || y >= Snapshot.Height)
						return BareLeak(cell.X, cell.Y, x, y, out Failure);
					int neighborKey = CellKey(x, y, Snapshot.Width);
					ArchitectureCellState neighbor = cells[neighborKey];
					bool exterior = neighbor.Claim != ArchitectureClaim.Building
						|| (neighbor.Cover != ArchitectureCover.Walled
						&& !structures.Contains(neighborKey) && !openings.Contains(neighborKey));
					if (exterior)
						return BareLeak(cell.X, cell.Y, x, y, out Failure);
				}
			}
			return true;
		}

		private static bool BareLeak(int X, int Y, int TowardX, int TowardY,
			out string Failure)
		{
			return Fail("walled building perimeter has a bare leak at " + X + "," + Y
				+ " toward " + TowardX + "," + TowardY, out Failure);
		}

		private static bool DeclaredOpening(string Role)
		{
			if (string.IsNullOrEmpty(Role)) return false;
			int separator = Role.IndexOf(':');
			string family = separator < 0 ? Role : Role.Substring(0, separator);
			return family == "door" || family == "entrance" || family == "exit"
				|| family == "threshold";
		}
	}
}
