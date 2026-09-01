using System;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouseRules
	{
		private static bool TryRawApproach(KingdomGatehousePlan Plan, int Depth,
			out KingdomGatehouseCell Cell)
		{
			World(Plan, 0, Depth, out int x, out int y);
			Cell = new KingdomGatehouseCell(x, y, null, null);
			return true;
		}

		private static void SetBounds(KingdomGatehousePlan Plan)
		{
			World(Plan, -1, 0, out int ax, out int ay);
			World(Plan, 1, 2, out int bx, out int by);
			Plan.X1 = Math.Min(ax, bx);
			Plan.Y1 = Math.Min(ay, by);
			Plan.X2 = Math.Max(ax, bx);
			Plan.Y2 = Math.Max(ay, by);
		}

		private static void World(KingdomGatehousePlan Plan, int Lateral, int Depth,
			out int X, out int Y)
		{
			int inwardX = 0;
			int inwardY = 0;
			int lateralX = 0;
			int lateralY = 0;
			switch (Plan.Orientation)
			{
				case KingdomGatehouseOrientation.North:
					inwardY = 1; lateralX = 1; break;
				case KingdomGatehouseOrientation.East:
					inwardX = -1; lateralY = 1; break;
				case KingdomGatehouseOrientation.South:
					inwardY = -1; lateralX = -1; break;
				case KingdomGatehouseOrientation.West:
					inwardX = 1; lateralY = -1; break;
			}
			X = Plan.GateX + inwardX * Depth + lateralX * Lateral;
			Y = Plan.GateY + inwardY * Depth + lateralY * Lateral;
		}

		private static bool InBounds(int X, int Y, int Width, int Height)
		{
			return X >= 0 && Y >= 0 && X < Width && Y < Height;
		}

		private static bool TryInt(string Text, int Minimum, int Maximum, out int Value)
		{
			return int.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value)
				&& Value >= Minimum && Value <= Maximum
				&& Value.ToString(CultureInfo.InvariantCulture) == Text;
		}
	}
}
