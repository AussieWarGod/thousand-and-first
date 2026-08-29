using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static class KingdomGatehouseRules
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
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

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

		public static bool TryEncode(KingdomGatehousePlan Plan, out string Receipt)
		{
			Receipt = null;
			if (!Valid(Plan)) return false;
			if (Plan.ReceiptVersion == 2) return TryEncodeV2(Plan, out Receipt);
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
			if (string.IsNullOrEmpty(Receipt) || Receipt.Length > MaxReceiptChars) return false;
			if (Receipt.StartsWith("v2,", StringComparison.Ordinal))
				return TryDecodeV2(Receipt, out Plan);
			if (Receipt.Length > V1ReceiptChars) return false;
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
				ReceiptVersion = 1,
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

		private static bool TryEncodeV2(KingdomGatehousePlan Plan, out string Receipt)
		{
			Receipt = null;
			if (Plan == null || Plan.ReceiptVersion != 2 || !ValidV2Form(Plan)) return false;
			string[] fields = new string[]
			{
				"v2",
				((int)Plan.Orientation).ToString(CultureInfo.InvariantCulture),
				Plan.GateX.ToString(CultureInfo.InvariantCulture),
				Plan.GateY.ToString(CultureInfo.InvariantCulture),
				Plan.X1.ToString(CultureInfo.InvariantCulture),
				Plan.Y1.ToString(CultureInfo.InvariantCulture),
				Plan.X2.ToString(CultureInfo.InvariantCulture),
				Plan.Y2.ToString(CultureInfo.InvariantCulture),
				EncodeText(Plan.FormKey),
				EncodeText(Plan.WallBlueprint),
				EncodeText(Plan.WatchBlueprint),
				EncodeText(Plan.RootRenderString),
				EncodeText(Plan.RootColorString),
				EncodeText(Plan.RootTileColor),
				EncodeText(Plan.RootDetailColor),
				EncodeText(Plan.RootClosedTile),
				EncodeText(Plan.RootOpenTile),
				EncodeText(Plan.WallRenderString),
				EncodeText(Plan.WallColorString),
				EncodeText(Plan.WallTileColor),
				EncodeText(Plan.WallDetailColor),
				EncodeText(Plan.WatchRenderString),
				EncodeText(Plan.WatchColorString),
				EncodeText(Plan.WatchTileColor),
				EncodeText(Plan.WatchDetailColor),
				EncodeText(Plan.WatchTile),
				EncodeText(Plan.MaterialClaim)
			};
			string body = string.Join(",", fields);
			Receipt = body + "," + Digest(body);
			if (Receipt.Length > MaxReceiptChars)
			{
				Receipt = null;
				return false;
			}
			return true;
		}

		private static bool TryDecodeV2(string Receipt, out KingdomGatehousePlan Plan)
		{
			Plan = null;
			string[] f = Receipt.Split(',');
			if (f.Length != 28 || f[0] != "v2") return false;
			int digestAt = Receipt.LastIndexOf(',');
			if (digestAt <= 0 || f[27].Length != 64
				|| Digest(Receipt.Substring(0, digestAt)) != f[27]) return false;
			if (!TryInt(f[1], 1, 4, out int orientation)
				|| !TryInt(f[2], 0, 1023, out int gateX)
				|| !TryInt(f[3], 0, 1023, out int gateY)
				|| !TryInt(f[4], 0, 1023, out int x1)
				|| !TryInt(f[5], 0, 1023, out int y1)
				|| !TryInt(f[6], 0, 1023, out int x2)
				|| !TryInt(f[7], 0, 1023, out int y2)
				|| !TryDecodeText(f[8], MaxFormKeyChars, out string formKey)
				|| !TryDecodeText(f[9], MaxBlueprintChars, out string wallBlueprint)
				|| !TryDecodeText(f[10], MaxBlueprintChars, out string watchBlueprint)
				|| !TryDecodeText(f[11], MaxPaletteChars, out string rootRender)
				|| !TryDecodeText(f[12], MaxPaletteChars, out string rootColor)
				|| !TryDecodeText(f[13], MaxPaletteChars, out string rootTileColor)
				|| !TryDecodeText(f[14], MaxPaletteChars, out string rootDetail)
				|| !TryDecodeText(f[15], MaxTileChars, out string rootClosedTile)
				|| !TryDecodeText(f[16], MaxTileChars, out string rootOpenTile)
				|| !TryDecodeText(f[17], MaxPaletteChars, out string wallRender)
				|| !TryDecodeText(f[18], MaxPaletteChars, out string wallColor)
				|| !TryDecodeText(f[19], MaxPaletteChars, out string wallTileColor)
				|| !TryDecodeText(f[20], MaxPaletteChars, out string wallDetail)
				|| !TryDecodeText(f[21], MaxPaletteChars, out string watchRender)
				|| !TryDecodeText(f[22], MaxPaletteChars, out string watchColor)
				|| !TryDecodeText(f[23], MaxPaletteChars, out string watchTileColor)
				|| !TryDecodeText(f[24], MaxPaletteChars, out string watchDetail)
				|| !TryDecodeText(f[25], MaxTileChars, out string watchTile)
				|| !TryDecodeText(f[26], MaxClaimChars, out string materialClaim)) return false;
			KingdomGatehousePlan parsed = new KingdomGatehousePlan
			{
				ReceiptVersion = 2,
				Orientation = (KingdomGatehouseOrientation)orientation,
				GateX = gateX,
				GateY = gateY,
				X1 = x1,
				Y1 = y1,
				X2 = x2,
				Y2 = y2,
				FormKey = formKey,
				WallBlueprint = wallBlueprint,
				WatchBlueprint = watchBlueprint,
				RootRenderString = rootRender,
				RootColorString = rootColor,
				RootTileColor = rootTileColor,
				RootDetailColor = rootDetail,
				RootClosedTile = rootClosedTile,
				RootOpenTile = rootOpenTile,
				WallRenderString = wallRender,
				WallColorString = wallColor,
				WallTileColor = wallTileColor,
				WallDetailColor = wallDetail,
				WatchRenderString = watchRender,
				WatchColorString = watchColor,
				WatchTileColor = watchTileColor,
				WatchDetailColor = watchDetail,
				WatchTile = watchTile,
				MaterialClaim = materialClaim
			};
			if (!TryEncode(parsed, out string canonical) || canonical != Receipt) return false;
			Plan = parsed;
			return true;
		}

		/// <summary>
		/// The versioned strike wire reuses its bounded rectangle/owner fields for this one typed
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
			if (Plan == null
				|| (Plan.ReceiptVersion != 1 && Plan.ReceiptVersion != 2)
				|| (int)Plan.Orientation < 1 || (int)Plan.Orientation > 4
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
				&& inside.X >= 0 && inside.Y >= 0 && inside.X <= 1023 && inside.Y <= 1023
				&& (Plan.ReceiptVersion != 2 || ValidV2Form(Plan));
		}

		private static bool ValidV2Form(KingdomGatehousePlan Plan)
		{
			if (Plan == null || Plan.ReceiptVersion != 2
				|| !KnownForm(Plan.FormKey, out KingdomGatehouseForm expected)) return false;
			return Plan.FormKey == expected.FormKey
				&& Plan.WallBlueprint == expected.WallBlueprint
				&& Plan.WatchBlueprint == expected.WatchBlueprint
				&& Plan.RootRenderString == expected.RootRenderString
				&& Plan.RootColorString == expected.RootColorString
				&& Plan.RootTileColor == expected.RootTileColor
				&& Plan.RootDetailColor == expected.RootDetailColor
				&& Plan.RootClosedTile == expected.RootClosedTile
				&& Plan.RootOpenTile == expected.RootOpenTile
				&& Plan.WallRenderString == expected.WallRenderString
				&& Plan.WallColorString == expected.WallColorString
				&& Plan.WallTileColor == expected.WallTileColor
				&& Plan.WallDetailColor == expected.WallDetailColor
				&& Plan.WatchRenderString == expected.WatchRenderString
				&& Plan.WatchColorString == expected.WatchColorString
				&& Plan.WatchTileColor == expected.WatchTileColor
				&& Plan.WatchDetailColor == expected.WatchDetailColor
				&& Plan.WatchTile == expected.WatchTile
				&& Plan.MaterialClaim == expected.MaterialClaim
				&& Plan.FormKey.Length <= MaxFormKeyChars
				&& Plan.WallBlueprint.Length <= MaxBlueprintChars
				&& Plan.WatchBlueprint.Length <= MaxBlueprintChars
				&& Plan.RootRenderString.Length <= MaxPaletteChars
				&& Plan.RootColorString.Length <= MaxPaletteChars
				&& Plan.RootTileColor.Length <= MaxPaletteChars
				&& Plan.RootDetailColor.Length <= MaxPaletteChars
				&& Plan.RootClosedTile.Length <= MaxTileChars
				&& Plan.RootOpenTile.Length <= MaxTileChars
				&& Plan.WallRenderString.Length <= MaxPaletteChars
				&& Plan.WallColorString.Length <= MaxPaletteChars
				&& Plan.WallTileColor.Length <= MaxPaletteChars
				&& Plan.WallDetailColor.Length <= MaxPaletteChars
				&& Plan.WatchRenderString.Length <= MaxPaletteChars
				&& Plan.WatchColorString.Length <= MaxPaletteChars
				&& Plan.WatchTileColor.Length <= MaxPaletteChars
				&& Plan.WatchDetailColor.Length <= MaxPaletteChars
				&& Plan.WatchTile.Length <= MaxTileChars
				&& Plan.MaterialClaim.Length <= MaxClaimChars
				&& KingdomMaterialDebitCost.TryParseClaim(Plan.MaterialClaim,
					out KingdomMaterialDebitCost parsed)
				&& parsed.ToClaimString() == Plan.MaterialClaim;
		}

		private static void CopyForm(KingdomGatehousePlan Plan, KingdomGatehouseForm Form)
		{
			Plan.FormKey = Form.FormKey;
			Plan.WallBlueprint = Form.WallBlueprint;
			Plan.WatchBlueprint = Form.WatchBlueprint;
			Plan.RootRenderString = Form.RootRenderString;
			Plan.RootColorString = Form.RootColorString;
			Plan.RootTileColor = Form.RootTileColor;
			Plan.RootDetailColor = Form.RootDetailColor;
			Plan.RootClosedTile = Form.RootClosedTile;
			Plan.RootOpenTile = Form.RootOpenTile;
			Plan.WallRenderString = Form.WallRenderString;
			Plan.WallColorString = Form.WallColorString;
			Plan.WallTileColor = Form.WallTileColor;
			Plan.WallDetailColor = Form.WallDetailColor;
			Plan.WatchRenderString = Form.WatchRenderString;
			Plan.WatchColorString = Form.WatchColorString;
			Plan.WatchTileColor = Form.WatchTileColor;
			Plan.WatchDetailColor = Form.WatchDetailColor;
			Plan.WatchTile = Form.WatchTile;
			Plan.MaterialClaim = Form.MaterialClaim;
		}

		private static bool KnownForm(string Key, out KingdomGatehouseForm Form)
		{
			Form = null;
			switch (Key)
			{
			case "common":
				Form = NewForm("common", StoneBlueprint, "r_KingdomFixtureChairStone",
					"+", "&W", "&y", "y",
					"177", "&r^W", "&r", "W",
					"190", "&K", "&w", "W", "Items/sw_bench.bmp",
					0, 0, 0, 44, 0, 6);
				break;
			case "verdant":
				Form = NewForm("verdant", "r_KingdomStructureBrinestalkWall",
					WatchBlueprint, "+", "&g", "&g", "G",
					"215", "&w^y", "&w", "y",
					"190", "&g", "&g", "G", "Items/sw_bench.bmp",
					0, 10, 34, 0, 0, 6);
				break;
			case "fungal":
				Form = NewForm("fungal", "r_KingdomStructureMushroomWall",
					"r_KingdomFixtureCushionCanvas", "+", "&m", "&m", "M",
					"007", "&y^Y", "&y", "Y",
					"009", "&m", "&m", "M", "Items/sw_cushion1.bmp",
					24, 4, 16, 0, 0, 6);
				break;
			case "gyre":
				Form = NewForm("gyre", "r_KingdomStructureLimestone",
					"r_KingdomFixtureChairMarble", "+", "&c", "&W", "Y",
					"177", "&W^c", "&W", "Y",
					"190", "&W", "&W", "Y", "Items/sw_bench.bmp",
					0, 0, 0, 28, 16, 6);
				break;
			case "eater":
				Form = NewForm("eater", "r_KingdomRubbleWall",
					"r_KingdomFixtureChairStone", "+", "&r", "&y", "C",
					"178", "&r", "&w", "w",
					"190", "&r", "&w", "C", "Items/sw_bench.bmp",
					0, 0, 0, 34, 0, 16);
				break;
			default:
				return false;
			}
			return true;
		}

		private static KingdomGatehouseForm NewForm(string Key, string Wall, string Watch,
			string RootRender, string RootColor, string RootTileColor, string RootDetail,
			string WallRender, string WallColor, string WallTileColor, string WallDetail,
			string WatchRender, string WatchColor, string WatchTileColor, string WatchDetail,
			string WatchTile, int Mud, int Brush, int Timber,
			int Stone, int Marble, int Scrap)
		{
			KingdomMaterialTally materials = new KingdomMaterialTally();
			materials.Set(KingdomMaterial.Mud, Mud);
			materials.Set(KingdomMaterial.Brush, Brush);
			materials.Set(KingdomMaterial.Timber, Timber);
			materials.Set(KingdomMaterial.Stone, Stone);
			materials.Set(KingdomMaterial.Marble, Marble);
			materials.Set(KingdomMaterial.Scrap, Scrap);
			return new KingdomGatehouseForm
			{
				FormKey = Key,
				WallBlueprint = Wall,
				WatchBlueprint = Watch,
				RootRenderString = RootRender,
				RootColorString = RootColor,
				RootTileColor = RootTileColor,
				RootDetailColor = RootDetail,
				RootClosedTile = "Items/sw_fence_gates_2_open.bmp",
				RootOpenTile = "Items/sw_fence_gates_closed.bmp",
				WallRenderString = WallRender,
				WallColorString = WallColor,
				WallTileColor = WallTileColor,
				WallDetailColor = WallDetail,
				WatchRenderString = WatchRender,
				WatchColorString = WatchColor,
				WatchTileColor = WatchTileColor,
				WatchDetailColor = WatchDetail,
				WatchTile = WatchTile,
				MaterialClaim = new KingdomMaterialDebitCost(materials).ToClaimString()
			};
		}

		private static string EncodeText(string Text)
		{
			return Convert.ToBase64String(StrictUtf8.GetBytes(Text));
		}

		private static bool TryDecodeText(string Encoded, int MaxChars, out string Text)
		{
			Text = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaxChars * 4 + 8)
				return false;
			try
			{
				byte[] bytes = Convert.FromBase64String(Encoded);
				Text = StrictUtf8.GetString(bytes);
			}
			catch (Exception)
			{
				Text = null;
				return false;
			}
			return !string.IsNullOrEmpty(Text) && Text.Length <= MaxChars
				&& EncodeText(Text) == Encoded;
		}

		private static string Digest(string Text)
		{
			byte[] hash;
			using (SHA256 sha = SHA256.Create())
				hash = sha.ComputeHash(StrictUtf8.GetBytes(Text));
			StringBuilder encoded = new StringBuilder(64);
			for (int i = 0; i < hash.Length; i++) encoded.Append(hash[i].ToString("x2"));
			return encoded.ToString();
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
