using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal enum KingdomInheritanceSpatialFault
	{
		None = 0,
		NullInput = 1,
		WrongDimensions = 2,
		RaggedWorks = 3,
		TooManyWorks = 4,
		MalformedSnapshot = 5,
		SnapshotHash = 6,
		Footprint = 7,
		Overlap = 8,
		RaggedStreets = 9,
		TooManyStreets = 10,
		StreetCoordinate = 11,
		StreetOrder = 12,
		StreetOverlap = 13,
		Entry = 14,
		DisconnectedStreet = 15,
		PublicEntrance = 16
	}

	/// <summary>
	/// Pure validation and geometry for the spatial half of a promoted seal. Coordinates are
	/// normalized to the old seat zone's low corner, never to a mutable catalogue footprint.
	/// </summary>
	internal static class KingdomInheritanceSpatialRules
	{
		internal const int SpatialVersion = 1;
		internal const int Width = 80;
		internal const int Height = 25;
		internal const int MaxStreetCells = 1024;
		internal const int MaxSnapshotChars = KingdomArchitectureRules.MaxSnapshotChars;
		internal const int North = 0;
		internal const int East = 1;
		internal const int South = 2;
		internal const int West = 3;
		internal const int NoEntry = -1;

		internal struct Rect
		{
			internal int X1;
			internal int Y1;
			internal int X2;
			internal int Y2;

			internal bool Contains(int X, int Y)
			{
				return X >= X1 && X <= X2 && Y >= Y1 && Y <= Y2;
			}
		}

		internal static bool TryValidate(IList<string> WorkKeys, IList<int> WorkX,
			IList<int> WorkY, IList<int> WorkConditions, IList<string> WorkSnapshots,
			IList<string> WorkSnapshotHashes, int SpatialWidth, int SpatialHeight,
			int EntrySide, int EntryX, int EntryY, IList<int> StreetX, IList<int> StreetY,
			out KingdomInheritanceSpatialFault Fault)
		{
			Fault = KingdomInheritanceSpatialFault.None;
			try
			{
				if (WorkKeys == null || WorkX == null || WorkY == null || WorkConditions == null
					|| WorkSnapshots == null || WorkSnapshotHashes == null
					|| StreetX == null || StreetY == null)
					return Fail(KingdomInheritanceSpatialFault.NullInput, out Fault);
				if (SpatialWidth != Width || SpatialHeight != Height)
					return Fail(KingdomInheritanceSpatialFault.WrongDimensions, out Fault);
				int works = WorkKeys.Count;
				if (works > KingdomSealRecord.MaxWorks)
					return Fail(KingdomInheritanceSpatialFault.TooManyWorks, out Fault);
				if (WorkX.Count != works || WorkY.Count != works || WorkConditions.Count != works
					|| WorkSnapshots.Count != works || WorkSnapshotHashes.Count != works)
					return Fail(KingdomInheritanceSpatialFault.RaggedWorks, out Fault);

				Rect[] rects = new Rect[works];
				ArchitectureLayoutSnapshot[] snapshots = new ArchitectureLayoutSnapshot[works];
				for (int i = 0; i < works; i++)
				{
					string encoded = WorkSnapshots[i] ?? "";
					string hash = WorkSnapshotHashes[i] ?? "";
					if (encoded.Length == 0)
					{
						if (hash.Length != 0 || !TryLegacyRect(WorkKeys[i], WorkX[i], WorkY[i], out rects[i]))
							return Fail(KingdomInheritanceSpatialFault.MalformedSnapshot, out Fault);
					}
					else
					{
						if (encoded.Length > MaxSnapshotChars
							|| !KingdomArchitectureRules.IsCurrentSnapshotEncoding(encoded)
							|| !KingdomArchitectureRules.TryDecodeSnapshot(encoded, out snapshots[i], out _))
							return Fail(KingdomInheritanceSpatialFault.MalformedSnapshot, out Fault);
						string actualHash;
						if (hash.Length != 64 || !KingdomArchitectureRules.TryEncodedSnapshotHash(
							encoded, out actualHash, out _) || actualHash != hash)
							return Fail(KingdomInheritanceSpatialFault.SnapshotHash, out Fault);
						if (!TrySnapshotRect(snapshots[i], WorkX[i], WorkY[i], out rects[i]))
							return Fail(KingdomInheritanceSpatialFault.Footprint, out Fault);
					}
					if (!Inside(rects[i]))
						return Fail(KingdomInheritanceSpatialFault.Footprint, out Fault);
					for (int j = 0; j < i; j++)
						if (Overlaps(rects[i], rects[j]))
							return Fail(KingdomInheritanceSpatialFault.Overlap, out Fault);
				}

				if (StreetX.Count != StreetY.Count)
					return Fail(KingdomInheritanceSpatialFault.RaggedStreets, out Fault);
				if (StreetX.Count > MaxStreetCells)
					return Fail(KingdomInheritanceSpatialFault.TooManyStreets, out Fault);
				bool[,] streets = new bool[Width, Height];
				for (int i = 0; i < StreetX.Count; i++)
				{
					int x = StreetX[i];
					int y = StreetY[i];
					if (x < 0 || y < 0 || x >= Width || y >= Height)
						return Fail(KingdomInheritanceSpatialFault.StreetCoordinate, out Fault);
					if (i > 0 && (y < StreetY[i - 1]
						|| (y == StreetY[i - 1] && x <= StreetX[i - 1])))
						return Fail(KingdomInheritanceSpatialFault.StreetOrder, out Fault);
					for (int w = 0; w < rects.Length; w++)
						if (rects[w].Contains(x, y))
							return Fail(KingdomInheritanceSpatialFault.StreetOverlap, out Fault);
					streets[x, y] = true;
				}

				if (StreetX.Count == 0)
				{
					if (EntrySide != NoEntry || EntryX != 0 || EntryY != 0)
						return Fail(KingdomInheritanceSpatialFault.Entry, out Fault);
					for (int i = 0; i < snapshots.Length; i++)
						if (snapshots[i] != null && HasPublicEntrance(snapshots[i]))
							return Fail(KingdomInheritanceSpatialFault.PublicEntrance, out Fault);
					return true;
				}

				if (!EntryMatches(EntrySide, EntryX, EntryY) || !streets[EntryX, EntryY])
					return Fail(KingdomInheritanceSpatialFault.Entry, out Fault);
				bool[,] reached = Reach(streets, EntryX, EntryY);
				for (int i = 0; i < StreetX.Count; i++)
					if (!reached[StreetX[i], StreetY[i]])
						return Fail(KingdomInheritanceSpatialFault.DisconnectedStreet, out Fault);
				for (int i = 0; i < snapshots.Length; i++)
				{
					if (snapshots[i] == null) continue;
					for (int a = 0; a < snapshots[i].Anchors.Count; a++)
					{
						ArchitectureAnchor anchor = snapshots[i].Anchors[a];
						if (!IsPublicEntrance(anchor)) continue;
						int x;
						int y;
						if (!TryWorldPoint(snapshots[i], rects[i], anchor.X, anchor.Y, out x, out y)
							|| !Adjacent(reached, x, y))
							return Fail(KingdomInheritanceSpatialFault.PublicEntrance, out Fault);
					}
				}
				return true;
			}
			catch
			{
				return Fail(KingdomInheritanceSpatialFault.MalformedSnapshot, out Fault);
			}
		}

		internal static bool TrySnapshotRect(ArchitectureLayoutSnapshot Snapshot, int MainX,
			int MainY, out Rect Rect)
		{
			Rect = new Rect();
			if (Snapshot == null) return false;
			int relativeX;
			int relativeY;
			int worldWidth;
			int worldHeight;
			if (!KingdomArchitectureRules.TryToWorld(0, 0, Snapshot.Width, Snapshot.Height,
				Snapshot.Facing, Snapshot.MainX, Snapshot.MainY, out relativeX, out relativeY)
				|| !KingdomArchitectureRules.TryWorldDimensions(Snapshot.Width, Snapshot.Height,
					Snapshot.Facing, out worldWidth, out worldHeight)) return false;
			Rect.X1 = MainX - relativeX;
			Rect.Y1 = MainY - relativeY;
			Rect.X2 = Rect.X1 + worldWidth - 1;
			Rect.Y2 = Rect.Y1 + worldHeight - 1;
			return true;
		}

		internal static bool HasExistingAuthority(ArchitectureLayoutSnapshot Snapshot)
		{
			if (Snapshot == null) return false;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
				if (Snapshot.Placements[i].ExistingAuthority) return true;
			return false;
		}

		internal static int SideOfBoundary(int X, int Y)
		{
			if (Y == 0) return North;
			if (X == Width - 1) return East;
			if (Y == Height - 1) return South;
			if (X == 0) return West;
			return NoEntry;
		}

		internal static bool TryLegacyRect(string Key, int X, int Y, out Rect Rect)
		{
			Rect = new Rect();
			int width;
			int height;
			if (!KingdomInheritRules.TryFootprint(Key, out width, out height))
			{
				width = 1;
				height = 1;
			}
			Rect.X1 = X - (width - 1) / 2;
			Rect.Y1 = Y - (height - 1) / 2;
			Rect.X2 = Rect.X1 + width - 1;
			Rect.Y2 = Rect.Y1 + height - 1;
			return true;
		}

		private static bool TryWorldPoint(ArchitectureLayoutSnapshot Snapshot, Rect Rect,
			int U, int V, out int X, out int Y)
		{
			return KingdomArchitectureRules.TryToWorld(Rect.X1, Rect.Y1, Snapshot.Width,
				Snapshot.Height, Snapshot.Facing, U, V, out X, out Y);
		}

		private static bool HasPublicEntrance(ArchitectureLayoutSnapshot Snapshot)
		{
			for (int i = 0; i < Snapshot.Anchors.Count; i++)
				if (IsPublicEntrance(Snapshot.Anchors[i])) return true;
			return false;
		}

		private static bool IsPublicEntrance(ArchitectureAnchor Anchor)
		{
			return Anchor != null && (Anchor.Key == "entrance:public"
				|| Anchor.Key.StartsWith("entrance:public@", StringComparison.Ordinal));
		}

		private static bool EntryMatches(int Side, int X, int Y)
		{
			return X >= 0 && X < Width && Y >= 0 && Y < Height
				&& ((Side == North && Y == 0) || (Side == East && X == Width - 1)
					|| (Side == South && Y == Height - 1) || (Side == West && X == 0));
		}

		private static bool Adjacent(bool[,] Reached, int X, int Y)
		{
			return (Y > 0 && Reached[X, Y - 1]) || (X + 1 < Width && Reached[X + 1, Y])
				|| (Y + 1 < Height && Reached[X, Y + 1]) || (X > 0 && Reached[X - 1, Y]);
		}

		private static bool[,] Reach(bool[,] Streets, int StartX, int StartY)
		{
			bool[,] reached = new bool[Width, Height];
			Queue<int> queue = new Queue<int>();
			reached[StartX, StartY] = true;
			queue.Enqueue(StartY * Width + StartX);
			int[] dx = new int[4] { 0, 1, 0, -1 };
			int[] dy = new int[4] { -1, 0, 1, 0 };
			while (queue.Count > 0)
			{
				int packed = queue.Dequeue();
				int x = packed % Width;
				int y = packed / Width;
				for (int d = 0; d < 4; d++)
				{
					int nx = x + dx[d];
					int ny = y + dy[d];
					if (nx < 0 || ny < 0 || nx >= Width || ny >= Height
						|| reached[nx, ny] || !Streets[nx, ny]) continue;
					reached[nx, ny] = true;
					queue.Enqueue(ny * Width + nx);
				}
			}
			return reached;
		}

		private static bool Inside(Rect Rect)
		{
			return Rect.X1 >= 0 && Rect.Y1 >= 0 && Rect.X2 < Width && Rect.Y2 < Height;
		}

		private static bool Overlaps(Rect A, Rect B)
		{
			return A.X1 <= B.X2 && A.X2 >= B.X1 && A.Y1 <= B.Y2 && A.Y2 >= B.Y1;
		}

		private static bool Fail(KingdomInheritanceSpatialFault Value,
			out KingdomInheritanceSpatialFault Fault)
		{
			Fault = Value;
			return false;
		}
	}
}
