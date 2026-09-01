using System;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouseRules
	{
		public const string BuildKey = "gatehouse";
		public const string RootBlueprint = "r_KingdomGatehouse";
		public const string StoneBlueprint = "r_KingdomStructureSandstone";
		public const string WatchBlueprint = "r_KingdomFixtureBenchTimber";
		public const int SatelliteCount = KingdomGatehouseTopology.SatelliteCount;
		public const int PassageCount = 3;
		public const int FootprintCells = 9;
		public const int MaxReceiptChars = 512;
		private const int V1ReceiptChars = 96;
		private const int MaxFormKeyChars = 16;
		private const int MaxBlueprintChars = 96;
		private const int MaxPaletteChars = 16;
		private const int MaxTileChars = 96;
		private const int MaxClaimChars = 320;

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
				ReceiptVersion = 1,
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

		/// <summary>New work freezes its bounded form before funding; unknown styles use common.</summary>
		public static bool TryPlan(int Width, int Height, KingdomRules.Frontier Edges,
			int HeartX, int HeartY, string Style, out KingdomGatehousePlan Plan)
		{
			Plan = null;
			if (!TryPlan(Width, Height, Edges, HeartX, HeartY,
				out KingdomGatehousePlan geometry)
				|| !TryResolveForm(Style, out KingdomGatehouseForm form)) return false;
			geometry.ReceiptVersion = 2;
			CopyForm(geometry, form);
			if (!Valid(geometry)) return false;
			Plan = geometry;
			return true;
		}

		/// <summary>Resolve only immutable built-in v2 form law. Catalogue mutation is irrelevant.</summary>
		public static bool TryResolveForm(string Style, out KingdomGatehouseForm Form)
		{
			string key = string.IsNullOrWhiteSpace(Style)
				? "common" : Style.Trim().ToLowerInvariant();
			if (!KnownForm(key, out Form)) return KnownForm("common", out Form);
			return true;
		}

		public static bool TryMaterialCost(KingdomGatehousePlan Plan,
			out KingdomMaterialDebitCost Cost)
		{
			Cost = null;
			return Plan != null && Plan.ReceiptVersion == 2 && Valid(Plan)
				&& KingdomMaterialDebitCost.TryParseClaim(Plan.MaterialClaim, out Cost)
				&& Cost.ToClaimString() == Plan.MaterialClaim;
		}

		/// <summary>v1 had no form claim; require canonical paid truth but never invent a claim.</summary>
		public static bool MaterialClaimMatches(KingdomGatehousePlan Plan, string Claim)
		{
			if (Plan == null || !Valid(Plan)
				|| !KingdomMaterialDebitCost.TryParseClaim(Claim,
					out KingdomMaterialDebitCost parsed)
				|| parsed.ToClaimString() != Claim) return false;
			return Plan.ReceiptVersion != 2 || Plan.MaterialClaim == Claim;
		}

		public static bool TryRootPalette(KingdomGatehousePlan Plan,
			out string ColorString, out string DetailColor)
		{
			ColorString = null;
			DetailColor = null;
			if (!Valid(Plan) || Plan.ReceiptVersion != 2) return false;
			ColorString = Plan.RootColorString;
			DetailColor = Plan.RootDetailColor;
			return true;
		}

		public static bool TryRootRender(KingdomGatehousePlan Plan,
			out string RenderString, out string ColorString, out string TileColor,
			out string DetailColor, out string ClosedTile, out string OpenTile)
		{
			RenderString = null; ColorString = null; TileColor = null;
			DetailColor = null; ClosedTile = null; OpenTile = null;
			if (!Valid(Plan) || Plan.ReceiptVersion != 2) return false;
			RenderString = Plan.RootRenderString;
			ColorString = Plan.RootColorString;
			TileColor = Plan.RootTileColor;
			DetailColor = Plan.RootDetailColor;
			ClosedTile = Plan.RootClosedTile;
			OpenTile = Plan.RootOpenTile;
			return true;
		}

		public static bool TrySatellitePalette(KingdomGatehousePlan Plan, int Index,
			out string ColorString, out string DetailColor)
		{
			ColorString = null;
			DetailColor = null;
			if (!Valid(Plan) || Plan.ReceiptVersion != 2
				|| Index < 0 || Index >= SatelliteCount) return false;
			bool wall = Index < 4;
			ColorString = wall ? Plan.WallColorString : Plan.WatchColorString;
			DetailColor = wall ? Plan.WallDetailColor : Plan.WatchDetailColor;
			return true;
		}

		public static bool TrySatelliteRender(KingdomGatehousePlan Plan, int Index,
			out string RenderString, out string ColorString, out string TileColor,
			out string DetailColor, out string Tile)
		{
			RenderString = null; ColorString = null; TileColor = null;
			DetailColor = null; Tile = null;
			if (!Valid(Plan) || Plan.ReceiptVersion != 2
				|| Index < 0 || Index >= SatelliteCount) return false;
			bool wall = Index < 4;
			RenderString = wall ? Plan.WallRenderString : Plan.WatchRenderString;
			ColorString = wall ? Plan.WallColorString : Plan.WatchColorString;
			TileColor = wall ? Plan.WallTileColor : Plan.WatchTileColor;
			DetailColor = wall ? Plan.WallDetailColor : Plan.WatchDetailColor;
			Tile = wall ? null : Plan.WatchTile;
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

		/// <summary>Six owned outputs: four frozen-form guard walls and two frozen-form watches.</summary>
		public static bool TrySatellite(KingdomGatehousePlan Plan, int Index,
			out KingdomGatehouseCell Cell)
		{
			Cell = default(KingdomGatehouseCell);
			if (!Valid(Plan) || Index < 0 || Index >= SatelliteCount) return false;
			int depth = (Index < 2) ? 0 : ((Index < 4) ? 1 : 2);
			int lateral = (Index % 2 == 0) ? -1 : 1;
			string material = Plan.ReceiptVersion == 2
				? (Index < 4 ? Plan.WallBlueprint : Plan.WatchBlueprint)
				: (Index < 4 ? StoneBlueprint : WatchBlueprint);
			string slot = (Index < 4
				? (Plan.ReceiptVersion == 2 ? "wall-" : "stone-") : "watch-")
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

	}
}
