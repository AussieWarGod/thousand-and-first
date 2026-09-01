using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRules
	{
		private static string FoldType(string Value)
		{
			if (string.IsNullOrWhiteSpace(Value)) return null;
			string folded = Value.Trim().ToLowerInvariant();
			return ValidKey(folded) ? folded : null;
		}

		private static bool ValidKey(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= MaxKeyChars
				&& Value == Value.Trim() && !HasControl(Value);
		}

		private static bool ValidOptionalKey(string Value)
		{
			return string.IsNullOrEmpty(Value) || ValidKey(Value);
		}

		public static bool TryParseTech(string Value, out int Tech)
		{
			Tech = -1;
			if (string.IsNullOrEmpty(Value) || Value != Value.Trim()) return false;
			for (int i = 0; i < KingdomZoningRules.TechLevelNames.Length; i++)
			{
				if (Value == KingdomZoningRules.TechLevelNames[i])
				{
					Tech = i;
					return true;
				}
			}
			return false;
		}

		private static bool ValidBlueprint(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= MaxBlueprintChars
				&& Value == Value.Trim() && !HasControl(Value);
		}

		private static bool HasControl(string Value)
		{
			for (int i = 0; i < Value.Length; i++) if (char.IsControl(Value[i])) return true;
			return false;
		}

		private static bool KnownLotSize(ArchitectureLotSize Value)
		{
			return Value >= ArchitectureLotSize.Small && Value <= ArchitectureLotSize.Huge;
		}

		private static bool KnownFacing(ArchitectureFacing Value)
		{
			return Value >= ArchitectureFacing.North && Value <= ArchitectureFacing.West;
		}

		private static bool KnownFrontage(ArchitectureFrontage Value)
		{
			return Value == ArchitectureFrontage.Heart || Value == ArchitectureFrontage.Road;
		}

		private static bool KnownClaim(ArchitectureClaim Value)
		{
			return Value >= ArchitectureClaim.Unclaimed
				&& Value <= ArchitectureClaim.LegacyClaimed;
		}

		private static bool CurrentClaim(ArchitectureClaim Value)
		{
			return Value == ArchitectureClaim.Unclaimed || Value == ArchitectureClaim.Yard
				|| Value == ArchitectureClaim.Building;
		}

		/// <summary>True for either authored claim or legacy one-bit claimed truth.</summary>
		public static bool IsClaimed(ArchitectureClaim Value)
		{
			return Value != ArchitectureClaim.Unclaimed && KnownClaim(Value);
		}

		/// <summary>Queries an already-frozen canonical footprint. Malformed rectangles fail closed.</summary>
		public static bool ContainsFootprintCell(ArchitectureLayoutSnapshot Snapshot, int X, int Y)
		{
			return Snapshot != null && ValidFootprint(Snapshot.Width, Snapshot.Height,
				Snapshot.FootprintX, Snapshot.FootprintY, Snapshot.FootprintWidth,
				Snapshot.FootprintHeight) && X >= Snapshot.FootprintX && Y >= Snapshot.FootprintY
				&& X < Snapshot.FootprintX + Snapshot.FootprintWidth
				&& Y < Snapshot.FootprintY + Snapshot.FootprintHeight;
		}

		private static bool ValidFootprint(int MapWidth, int MapHeight, int X, int Y,
			int Width, int Height)
		{
			return MapWidth > 0 && MapHeight > 0 && X >= 0 && Y >= 0
				&& Width > 0 && Height > 0 && (long)X + Width <= MapWidth
				&& (long)Y + Height <= MapHeight;
		}

		private static bool TryResolveFootprint(ArchitectureMapDraft Map, int CatalogueWidth,
			int CatalogueHeight, out int X, out int Y, out int Width, out int Height,
			out string Failure)
		{
			X = 0; Y = 0; Width = 0; Height = 0; Failure = null;
			if (Map == null || CatalogueWidth < 0 || CatalogueHeight < 0
				|| (CatalogueWidth == 0) != (CatalogueHeight == 0))
				return Fail("catalogue footprint dimensions are malformed", out Failure);
			if (Map.HasFootprint && !ValidFootprint(Map.Width, Map.Height, Map.FootprintX,
				Map.FootprintY, Map.FootprintWidth, Map.FootprintHeight))
				return Fail("map footprint is outside its canonical bounds", out Failure);
			if (CatalogueWidth > 0)
			{
				if (!Map.HasFootprint || Map.FootprintWidth != CatalogueWidth
					|| Map.FootprintHeight != CatalogueHeight)
					return Fail("map footprint must explicitly match catalogue dimensions", out Failure);
				X = Map.FootprintX; Y = Map.FootprintY;
				Width = Map.FootprintWidth; Height = Map.FootprintHeight;
				return true;
			}
			if (Map.HasFootprint && (Map.FootprintX != 0 || Map.FootprintY != 0
				|| Map.FootprintWidth != Map.Width || Map.FootprintHeight != Map.Height))
				return Fail("a fill-plot catalogue tier may only declare the exact full map footprint",
					out Failure);
			Width = Map.Width; Height = Map.Height;
			return ValidFootprint(Map.Width, Map.Height, X, Y, Width, Height)
				|| Fail("resolved footprint is malformed", out Failure);
		}

		private static bool TryValidateCurrentFootprint(ArchitectureLayoutSnapshot Snapshot,
			out string Failure)
		{
			Failure = null;
			if (Snapshot == null || Snapshot.Cells == null || !KnownRoof(Snapshot.BaseRoof)
				|| !ValidFootprint(Snapshot.Width, Snapshot.Height,
				Snapshot.FootprintX, Snapshot.FootprintY, Snapshot.FootprintWidth,
				Snapshot.FootprintHeight))
				return Fail("snapshot footprint or catalogue roof is malformed", out Failure);
			if (!ContainsFootprintCell(Snapshot, Snapshot.MainX, Snapshot.MainY))
				return Fail("$building and main must lie inside the frozen footprint", out Failure);
			if (!TryValidateCurrentRoof(Snapshot, out Failure)) return false;
			for (int i = 0; i < Snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = Snapshot.Cells[i];
				if (cell == null || !CurrentClaim(cell.Claim))
					return Fail("current snapshot contains legacy or unknown claim truth", out Failure);
				if (cell.Claim == ArchitectureClaim.Building
					&& !ContainsFootprintCell(Snapshot, cell.X, cell.Y))
					return Fail("building claim or cover lies outside the frozen footprint", out Failure);
			}
			return true;
		}

		private static bool KnownLayer(ArchitectureLayer Value)
		{
			return Value >= ArchitectureLayer.Ground && Value <= ArchitectureLayer.Object;
		}

		private static bool KnownPassability(ArchitecturePassability Value)
		{
			return Value >= ArchitecturePassability.Walkable && Value <= ArchitecturePassability.Adjacent;
		}

		private static bool KnownCover(ArchitectureCover Value)
		{
			return Value >= ArchitectureCover.Open && Value <= ArchitectureCover.Natural;
		}

		private static bool KnownRoof(KingdomPlotRules.RoofState Value)
		{
			return Value >= KingdomPlotRules.RoofState.Open
				&& Value <= KingdomPlotRules.RoofState.Carved;
		}

		private static bool KnownAccess(ArchitectureAnchorAccess Value)
		{
			return Value == ArchitectureAnchorAccess.OnCell || Value == ArchitectureAnchorAccess.Adjacent;
		}

		private static string SlotFor(ArchitectureLayer Layer, int X, int Y)
		{
			char prefix = Layer == ArchitectureLayer.Ground ? 'g'
				: (Layer == ArchitectureLayer.Structure ? 's' : 'o');
			return prefix + ":" + X.ToString("D2", CultureInfo.InvariantCulture)
				+ ":" + Y.ToString("D2", CultureInfo.InvariantCulture);
		}

		private static int CellKey(int X, int Y, int Width)
		{
			return Y * Width + X;
		}

		private static Dictionary<int, ArchitectureCellState> CellDictionary(
			IList<ArchitectureCellState> Cells, int Width)
		{
			Dictionary<int, ArchitectureCellState> result = new Dictionary<int, ArchitectureCellState>();
			for (int i = 0; i < Cells.Count; i++)
				result[CellKey(Cells[i].X, Cells[i].Y, Width)] = Cells[i];
			return result;
		}

		private static bool ClaimBoundary(Dictionary<int, ArchitectureCellState> Cells,
			int Width, int Height, int X, int Y)
		{
			int[] dx = new int[4] { 0, 1, 0, -1 };
			int[] dy = new int[4] { -1, 0, 1, 0 };
			for (int i = 0; i < 4; i++)
			{
				int x = X + dx[i];
				int y = Y + dy[i];
				if (x < 0 || x >= Width || y < 0 || y >= Height) return true;
				if (!IsClaimed(Cells[CellKey(x, y, Width)].Claim)) return true;
			}
			return false;
		}

		private static bool AdjacentReached(int X, int Y, int Width, int Height,
			HashSet<int> Reached)
		{
			int[] dx = new int[4] { 0, 1, 0, -1 };
			int[] dy = new int[4] { -1, 0, 1, 0 };
			for (int i = 0; i < 4; i++)
			{
				int x = X + dx[i];
				int y = Y + dy[i];
				if (x >= 0 && x < Width && y >= 0 && y < Height
					&& Reached.Contains(CellKey(x, y, Width))) return true;
			}
			return false;
		}

		private static bool AnchorMatchesRole(string Key, string Role)
		{
			string keyRole = AnchorRole(Key);
			if (Role.IndexOf(':') >= 0) return keyRole == Role;
			int separator = keyRole.IndexOf(':');
			return separator < 0 ? keyRole == Role : keyRole.Substring(0, separator) == Role;
		}

		private static string AnchorRole(string Key)
		{
			int identity = Key == null ? -1 : Key.LastIndexOf('@');
			return identity < 0 ? Key : Key.Substring(0, identity);
		}

		private static string StableAnchorKey(string Role, int X, int Y)
		{
			return Role + "@" + X.ToString(CultureInfo.InvariantCulture) + ","
				+ Y.ToString(CultureInfo.InvariantCulture);
		}

		private static bool ContainsAnchor(IList<ArchitectureAnchor> Anchors, string Key)
		{
			for (int i = 0; i < Anchors.Count; i++) if (Anchors[i].Key == Key) return true;
			return false;
		}

		private static void SortSnapshot(ArchitectureLayoutSnapshot Snapshot)
		{
			Snapshot.Cells.Sort(CompareCells);
			Snapshot.Placements.Sort(ComparePlacements);
			Snapshot.Anchors.Sort(delegate(ArchitectureAnchor A, ArchitectureAnchor B)
			{
				return string.CompareOrdinal(A.Key, B.Key);
			});
		}

		private static int CompareCells(ArchitectureCellState A, ArchitectureCellState B)
		{
			int compare = A.Y.CompareTo(B.Y);
			return compare != 0 ? compare : A.X.CompareTo(B.X);
		}

		private static int ComparePlacements(ArchitecturePlacement A, ArchitecturePlacement B)
		{
			int compare = ((int)A.Layer).CompareTo((int)B.Layer);
			if (compare != 0) return compare;
			compare = A.Y.CompareTo(B.Y);
			return compare != 0 ? compare : A.X.CompareTo(B.X);
		}

		private static int ComparePlacementsReverse(ArchitecturePlacement A, ArchitecturePlacement B)
		{
			return -ComparePlacements(A, B);
		}

	}
}
