using System.Collections.Generic;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public partial class KingdomArchitectureGalleryWishes
	{
		private enum VisualCaseKind : byte
		{
			Objects = 0,
			Gatehouse = 1,
			RoadWorn = 2,
			RoadTrodden = 3,
			RoadPath = 4,
			RoadPaved = 5
		}

		private sealed class VisualPlacement
		{
			public string Role;
			public string Blueprint;
			public int X;
			public int Y;
			public string Declaration;
		}

		private sealed class VisualCase
		{
			public int Number;
			public string Key;
			public string CatalogueKey;
			public VisualCaseKind Kind;
			public int Width;
			public int Height;
			public List<VisualPlacement> Placements = new List<VisualPlacement>();

			public int ExpectedObjects
			{
				get
				{
					if (Kind == VisualCaseKind.Gatehouse) return 7;
					if (Kind == VisualCaseKind.RoadWorn) return 0;
					if (Kind == VisualCaseKind.RoadTrodden || Kind == VisualCaseKind.RoadPath
						|| Kind == VisualCaseKind.RoadPaved) return Width;
					return Placements.Count;
				}
			}
		}

		private static List<VisualCase> VisualCases()
		{
			List<VisualCase> result = new List<VisualCase>();
			AddWallTopologyCase(result, "palisade", "r_KingdomPalisade",
				"r_KingdomFixtureGateBrinestalk");
			AddWallTopologyCase(result, "rampart", "r_KingdomRampart",
				"r_KingdomFixtureGateBrinestalk");
			AddObjectCase(result, "watchtower", "r_KingdomWatchtower");
			result.Add(new VisualCase { Key = "gatehouse", CatalogueKey = "gatehouse",
				Kind = VisualCaseKind.Gatehouse, Width = 3, Height = 3 });
			AddLineCase(result, "watermain", "r_KingdomWaterMain");
			AddLineCase(result, "brinemain", "r_KingdomBrineMain");
			AddLiquidCrossingCase(result);
			AddTapCase(result, "watertap", "r_KingdomWaterTap", "r_KingdomWaterMain");
			AddTapCase(result, "brinetap", "r_KingdomBrineTap", "r_KingdomBrineMain");
			AddWallTopologyCase(result, "rubblewall", "r_KingdomRubbleWall",
				"r_KingdomFixtureGateBrinestalk");
			result.Add(Road("road-worn", VisualCaseKind.RoadWorn, 3));
			result.Add(Road("road-trodden", VisualCaseKind.RoadTrodden, 5));
			result.Add(Road("road-path", VisualCaseKind.RoadPath, 5));
			result.Add(Road("road-paved", VisualCaseKind.RoadPaved, 5));
			for (int i = 0; i < result.Count; i++) result[i].Number = i + 1;
			return result;
		}

		private static void AddObjectCase(List<VisualCase> Into, string Key, string Blueprint)
		{
			VisualCase item = new VisualCase { Key = Key, CatalogueKey = Key,
				Kind = VisualCaseKind.Objects, Width = 1, Height = 1 };
			item.Placements.Add(At("root", Blueprint, 0, 0));
			Into.Add(item);
		}

		private static void AddWallTopologyCase(List<VisualCase> Into, string Key,
			string Wall, string Gate)
		{
			// Each group has a full eight-neighbour gap. PaintedWall may read diagonals,
			// so one review silhouette must never change another group's bitmask.
			VisualCase item = new VisualCase { Key = Key, CatalogueKey = Key,
				Kind = VisualCaseKind.Objects, Width = 13, Height = 9 };
			item.Placements.Add(At("single", Wall, 0, 0));
			item.Placements.Add(At("horizontal-west", Wall, 3, 0));
			item.Placements.Add(At("horizontal-centre", Wall, 4, 0));
			item.Placements.Add(At("horizontal-east", Wall, 5, 0));
			item.Placements.Add(At("vertical-north", Wall, 8, 0));
			item.Placements.Add(At("vertical-centre", Wall, 8, 1));
			item.Placements.Add(At("vertical-south", Wall, 8, 2));
			item.Placements.Add(At("corner-turn", Wall, 0, 4));
			item.Placements.Add(At("corner-east", Wall, 1, 4));
			item.Placements.Add(At("corner-south", Wall, 0, 5));
			item.Placements.Add(At("tee-north", Wall, 4, 3));
			item.Placements.Add(At("tee-west", Wall, 3, 4));
			item.Placements.Add(At("tee-centre", Wall, 4, 4));
			item.Placements.Add(At("tee-east", Wall, 5, 4));
			item.Placements.Add(At("cross-north", Wall, 10, 3));
			item.Placements.Add(At("cross-west", Wall, 9, 4));
			item.Placements.Add(At("cross-centre", Wall, 10, 4));
			item.Placements.Add(At("cross-east", Wall, 11, 4));
			item.Placements.Add(At("cross-south", Wall, 10, 5));
			item.Placements.Add(At("gate-far-west", Wall, 8, 8));
			item.Placements.Add(At("gate-adjacent-west", Wall, 9, 8));
			item.Placements.Add(At("gate", Gate, 10, 8));
			item.Placements.Add(At("gate-adjacent-east", Wall, 11, 8));
			item.Placements.Add(At("gate-far-east", Wall, 12, 8));
			Into.Add(item);
		}

		private static void AddLineCase(List<VisualCase> Into, string Key, string Blueprint)
		{
			VisualCase item = new VisualCase { Key = Key, CatalogueKey = Key,
				Kind = VisualCaseKind.Objects, Width = 7, Height = 7 };
			for (int mask = 0; mask < 16; mask++)
			{
				string joins;
				KingdomLiquidVisualRules.TryCanonicalJoins(mask, out joins);
				item.Placements.Add(At("mask-" + mask.ToString("D2"), Blueprint,
					(mask % 4) * 2, (mask / 4) * 2, joins));
			}
			Into.Add(item);
		}

		private static void AddTapCase(List<VisualCase> Into, string Key, string Tap, string Main)
		{
			VisualCase item = new VisualCase { Key = Key, CatalogueKey = Key,
				Kind = VisualCaseKind.Objects, Width = 7, Height = 7 };
			for (int mask = 0; mask < 16; mask++)
			{
				string joins;
				KingdomLiquidVisualRules.TryCanonicalJoins(mask, out joins);
				item.Placements.Add(At("mask-" + mask.ToString("D2"), Tap,
					(mask % 4) * 2, (mask / 4) * 2, joins));
			}
			Into.Add(item);
		}

		private static void AddLiquidCrossingCase(List<VisualCase> Into)
		{
			VisualCase item = new VisualCase { Key = "liquidcrossing",
				CatalogueKey = "liquidcrossing", Kind = VisualCaseKind.Objects,
				Width = 9, Height = 3 };
			// Full eight-neighbour gap between the two crosses. Each surrounding end declares
			// back toward its crossing, so the screenshot proves both visual orientation and law.
			item.Placements.Add(At("fresh-vertical-crossing", "r_KingdomLiquidCrossing",
				1, 1, "NSEW"));
			item.Placements.Add(At("fresh-vertical-water-n", "r_KingdomWaterMain", 1, 0, "S"));
			item.Placements.Add(At("fresh-vertical-water-s", "r_KingdomWaterMain", 1, 2, "N"));
			item.Placements.Add(At("fresh-vertical-brine-w", "r_KingdomBrineMain", 0, 1, "E"));
			item.Placements.Add(At("fresh-vertical-brine-e", "r_KingdomBrineMain", 2, 1, "W"));
			item.Placements.Add(At("fresh-horizontal-crossing", "r_KingdomLiquidCrossing",
				7, 1, "EWNS"));
			item.Placements.Add(At("fresh-horizontal-brine-n", "r_KingdomBrineMain", 7, 0, "S"));
			item.Placements.Add(At("fresh-horizontal-brine-s", "r_KingdomBrineMain", 7, 2, "N"));
			item.Placements.Add(At("fresh-horizontal-water-w", "r_KingdomWaterMain", 6, 1, "E"));
			item.Placements.Add(At("fresh-horizontal-water-e", "r_KingdomWaterMain", 8, 1, "W"));
			Into.Add(item);
		}

		private static VisualPlacement At(string Role, string Blueprint, int X, int Y)
		{
			return new VisualPlacement { Role = Role, Blueprint = Blueprint, X = X, Y = Y };
		}

		private static VisualPlacement At(string Role, string Blueprint, int X, int Y,
			string Declaration)
		{
			return new VisualPlacement { Role = Role, Blueprint = Blueprint, X = X, Y = Y,
				Declaration = Declaration };
		}

		private static VisualCase Road(string Key, VisualCaseKind Kind, int Width)
		{
			return new VisualCase { Key = Key, CatalogueKey = null, Kind = Kind,
				Width = Width, Height = 1 };
		}
	}
}
