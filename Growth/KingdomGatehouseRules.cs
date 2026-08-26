using System;
using System.Globalization;

namespace ThousandAndFirst
{
	public static class KingdomGatehouseRules
	{
		public const string BuildKey = "gatehouse";
		public const string StoneBlueprint = "r_KingdomStructureSandstone";
		public const string WatchBlueprint = "r_KingdomFixtureBenchTimber";
		public const int SatelliteCount = 6;
		public const int PassageCount = 3;
		public const int FootprintCells = 9;

		public static bool IsGatehouse(string Key)
		{
			return !string.IsNullOrEmpty(Key)
				&& Key.Trim().ToLowerInvariant() == BuildKey;
		}

		/// <summary>Freeze the road endpoint and its inward-facing 3x3 guard topology.</summary>
		public static bool TryPlan(int Width, int Height, KingdomRules.Frontier Edges,
			int HeartX, int HeartY, out KingdomGatehousePlan Plan)
		{
			Plan = null;
			int gateX;
			int gateY;
			if (!KingdomRoadRules.TryGate(Width, Height, Edges, HeartX, HeartY,
				out gateX, out gateY)) return false;
			KingdomGatehouseOrientation orientation;
			if (!TryOrientation(Width, Height, Edges, gateX, gateY, out orientation))
				return false;
			KingdomGatehousePlan candidate = new KingdomGatehousePlan
			{
				Orientation = orientation,
				GateX = gateX,
				GateY = gateY
			};
			SetBounds(candidate);
			// One visible approach cell on either side proves that the door crosses a route
			// rather than terminating at the map edge or in its own guard room.
			KingdomGatehouseCell outside;
			KingdomGatehouseCell inside;
			if (!TryApproach(candidate, 0, out outside)
				|| !TryApproach(candidate, 1, out inside)
				|| !InBounds(candidate.X1, candidate.Y1, Width, Height)
				|| !InBounds(candidate.X2, candidate.Y2, Width, Height)
				|| !InBounds(outside.X, outside.Y, Width, Height)
				|| !InBounds(inside.X, inside.Y, Width, Height)) return false;
			Plan = candidate;
			return true;
		}

		/// <summary>Fixed N/E/S/W choice when a two-cell frontier band meets at a corner.</summary>
		public static bool TryOrientation(int Width, int Height, KingdomRules.Frontier Edges,
			int GateX, int GateY, out KingdomGatehouseOrientation Orientation)
		{
			Orientation = 0;
			if (Width <= 0 || Height <= 0 || GateX < 0 || GateY < 0
				|| GateX >= Width || GateY >= Height) return false;
			if ((Edges & KingdomRules.Frontier.North) != 0
				&& GateY < KingdomRules.FrontierBandCells)
				Orientation = KingdomGatehouseOrientation.North;
			else if ((Edges & KingdomRules.Frontier.East) != 0
				&& GateX >= Width - KingdomRules.FrontierBandCells)
				Orientation = KingdomGatehouseOrientation.East;
			else if ((Edges & KingdomRules.Frontier.South) != 0
				&& GateY >= Height - KingdomRules.FrontierBandCells)
				Orientation = KingdomGatehouseOrientation.South;
			else if ((Edges & KingdomRules.Frontier.West) != 0
				&& GateX < KingdomRules.FrontierBandCells)
				Orientation = KingdomGatehouseOrientation.West;
			return Orientation != 0;
		}

		/// <summary>Six owned outputs: four stone jamb/guard walls and two timber watches.</summary>
		public static bool TrySatellite(KingdomGatehousePlan Plan, int Index,
			out KingdomGatehouseCell Cell)
		{
			Cell = default(KingdomGatehouseCell);
			if (!Valid(Plan) || Index < 0 || Index >= SatelliteCount) return false;
			int depth = (Index < 2) ? 0 : ((Index < 4) ? 1 : 2);
			int lateral = (Index % 2 == 0) ? -1 : 1;
			string material = Index < 4 ? StoneBlueprint : WatchBlueprint;
			string slot = (Index < 4 ? "stone-" : "watch-")
				+ depth.ToString(CultureInfo.InvariantCulture)
				+ (lateral < 0 ? "-left" : "-right");
			World(Plan, lateral, depth, out int x, out int y);
			Cell = new KingdomGatehouseCell(x, y, slot, material);
			return true;
		}

		/// <summary>The three-cell open centerline, with the vanilla Door root at index zero.</summary>
		public static bool TryPassage(KingdomGatehousePlan Plan, int Index,
			out KingdomGatehouseCell Cell)
		{
			Cell = default(KingdomGatehouseCell);
			if (!Valid(Plan) || Index < 0 || Index >= PassageCount) return false;
			World(Plan, 0, Index, out int x, out int y);
			Cell = new KingdomGatehouseCell(x, y,
				Index == 0 ? "door" : "passage-" + Index.ToString(CultureInfo.InvariantCulture),
				null);
			return true;
		}

		/// <summary>Outside road approach (0), then the first inward road cell beyond the work (1).</summary>
		public static bool TryApproach(KingdomGatehousePlan Plan, int Index,
			out KingdomGatehouseCell Cell)
		{
			Cell = default(KingdomGatehouseCell);
			if (!Valid(Plan) || (Index != 0 && Index != 1)) return false;
			int depth = Index == 0 ? -1 : 3;
			World(Plan, 0, depth, out int x, out int y);
			Cell = new KingdomGatehouseCell(x, y,
				Index == 0 ? "road-outside" : "road-inside", null);
			return true;
		}

		public static bool TryEncode(KingdomGatehousePlan Plan, out string Receipt)
		{
			Receipt = null;
			if (!Valid(Plan)) return false;
			string orientation = ((int)Plan.Orientation).ToString(CultureInfo.InvariantCulture);
			Receipt = "v1," + orientation + ","
				+ Plan.GateX.ToString(CultureInfo.InvariantCulture) + ","
				+ Plan.GateY.ToString(CultureInfo.InvariantCulture) + ","
				+ Plan.X1.ToString(CultureInfo.InvariantCulture) + ","
				+ Plan.Y1.ToString(CultureInfo.InvariantCulture) + ","
				+ Plan.X2.ToString(CultureInfo.InvariantCulture) + ","
				+ Plan.Y2.ToString(CultureInfo.InvariantCulture);
			return true;
		}

		public static bool TryDecode(string Receipt, out KingdomGatehousePlan Plan)
		{
			Plan = null;
			if (string.IsNullOrEmpty(Receipt) || Receipt.Length > 96) return false;
			string[] f = Receipt.Split(',');
			int orientation;
			int gateX;
			int gateY;
			int x1;
			int y1;
			int x2;
			int y2;
			if (f.Length != 8 || f[0] != "v1"
				|| !TryInt(f[1], 1, 4, out orientation)
				|| !TryInt(f[2], 0, 1023, out gateX)
				|| !TryInt(f[3], 0, 1023, out gateY)
				|| !TryInt(f[4], 0, 1023, out x1)
				|| !TryInt(f[5], 0, 1023, out y1)
				|| !TryInt(f[6], 0, 1023, out x2)
				|| !TryInt(f[7], 0, 1023, out y2)) return false;
			KingdomGatehousePlan parsed = new KingdomGatehousePlan
			{
				Orientation = (KingdomGatehouseOrientation)orientation,
				GateX = gateX,
				GateY = gateY,
				X1 = x1,
				Y1 = y1,
				X2 = x2,
				Y2 = y2
			};
			string canonical;
			if (!TryEncode(parsed, out canonical) || canonical != Receipt) return false;
			Plan = parsed;
			return true;
		}

		/// <summary>
		/// The v2 strike wire reuses its bounded rectangle/owner fields for this one typed
		/// non-plot network. BuildKey is the type marker; HasPlot remains false, so removal can
		/// never mint a cleared-plot successor.
		/// </summary>
		public static bool IsNetworkStrike(string Key, bool HasPlot, int X1, int Y1,
			int X2, int Y2, string OwnerId, int TargetCount)
		{
			return IsGatehouse(Key) && !HasPlot && !string.IsNullOrEmpty(OwnerId)
				&& X1 >= 0 && Y1 >= 0 && X2 - X1 == 2 && Y2 - Y1 == 2
				&& X2 <= 1023 && Y2 <= 1023 && TargetCount == SatelliteCount;
		}

		private static bool Valid(KingdomGatehousePlan Plan)
		{
			if (Plan == null || (int)Plan.Orientation < 1 || (int)Plan.Orientation > 4
				|| Plan.GateX < 0 || Plan.GateY < 0 || Plan.GateX > 1023 || Plan.GateY > 1023)
				return false;
			KingdomGatehousePlan expected = new KingdomGatehousePlan
			{
				Orientation = Plan.Orientation,
				GateX = Plan.GateX,
				GateY = Plan.GateY
			};
			SetBounds(expected);
			if (expected.X1 < 0 || expected.Y1 < 0 || expected.X2 > 1023 || expected.Y2 > 1023
				|| expected.X1 != Plan.X1 || expected.Y1 != Plan.Y1
				|| expected.X2 != Plan.X2 || expected.Y2 != Plan.Y2) return false;
			KingdomGatehouseCell outside;
			KingdomGatehouseCell inside;
			return TryRawApproach(expected, -1, out outside)
				&& TryRawApproach(expected, 3, out inside)
				&& outside.X >= 0 && outside.Y >= 0 && outside.X <= 1023 && outside.Y <= 1023
				&& inside.X >= 0 && inside.Y >= 0 && inside.X <= 1023 && inside.Y <= 1023;
		}

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
