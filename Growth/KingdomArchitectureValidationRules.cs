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
				if (!Cells[CellKey(x, y, Width)].Claim) return true;
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
