using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouseRules
	{
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

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

	}
}
