namespace ThousandAndFirst
{
	public static partial class KingdomGatehouseRules
	{
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
			case "moonstair":
			case "gyre": // pre-v1 save/mod alias; keep its exact frozen form key
				Form = NewForm(Key, "Black Marble",
					"r_KingdomFixtureChairMarble", "+", "&c", "&W", "Y",
					"176", "&y^K", "&K", "y",
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

	}
}
