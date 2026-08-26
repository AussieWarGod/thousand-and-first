using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritanceSpatial
	{
		private static void SelectBoundaryComponent(bool[,] Roads, out int EntrySide,
			out int EntryX, out int EntryY, out List<int> StreetX, out List<int> StreetY)
		{
			EntrySide = KingdomInheritanceSpatialRules.NoEntry;
			EntryX = 0;
			EntryY = 0;
			StreetX = new List<int>();
			StreetY = new List<int>();
			for (int y = 0; y < KingdomInheritanceSpatialRules.Height; y++)
			{
				for (int x = 0; x < KingdomInheritanceSpatialRules.Width; x++)
				{
					int side = KingdomInheritanceSpatialRules.SideOfBoundary(x, y);
					if (side == KingdomInheritanceSpatialRules.NoEntry || !Roads[x, y]) continue;
					EntrySide = side;
					EntryX = x;
					EntryY = y;
					y = KingdomInheritanceSpatialRules.Height;
					break;
				}
			}
			if (EntrySide == KingdomInheritanceSpatialRules.NoEntry) return;
			bool[,] reached = new bool[KingdomInheritanceSpatialRules.Width,
				KingdomInheritanceSpatialRules.Height];
			Queue<int> queue = new Queue<int>();
			reached[EntryX, EntryY] = true;
			queue.Enqueue(EntryY * KingdomInheritanceSpatialRules.Width + EntryX);
			int[] dx = new int[4] { 0, 1, 0, -1 };
			int[] dy = new int[4] { -1, 0, 1, 0 };
			while (queue.Count > 0)
			{
				int packed = queue.Dequeue();
				int x = packed % KingdomInheritanceSpatialRules.Width;
				int y = packed / KingdomInheritanceSpatialRules.Width;
				for (int d = 0; d < 4; d++)
				{
					int nx = x + dx[d];
					int ny = y + dy[d];
					if (nx < 0 || ny < 0 || nx >= KingdomInheritanceSpatialRules.Width
						|| ny >= KingdomInheritanceSpatialRules.Height || reached[nx, ny]
						|| !Roads[nx, ny]) continue;
					reached[nx, ny] = true;
					queue.Enqueue(ny * KingdomInheritanceSpatialRules.Width + nx);
				}
			}
			for (int y = 0; y < KingdomInheritanceSpatialRules.Height; y++)
				for (int x = 0; x < KingdomInheritanceSpatialRules.Width; x++)
					if (reached[x, y])
					{
						StreetX.Add(x);
						StreetY.Add(y);
					}
		}

		private static KingdomInheritanceSpatialCaptureResult Malformed(string Detail,
			out string Failure)
		{
			Failure = string.IsNullOrEmpty(Detail) ? "spatial inheritance evidence is malformed"
				: Detail;
			return KingdomInheritanceSpatialCaptureResult.Malformed;
		}
	}
}
