using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		private static bool TryDecodeLegacyPlotPayload(string Payload,
			out KingdomPlotRules.PlotRect Rect, out string SkinKey)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			SkinKey = null;
			string[] fields = Payload.Split('|');
			int x1;
			int y1;
			int x2;
			int y2;
			if (fields.Length != 6 || fields[0] != "v1" || !TryPlotCoordinate(fields[1], out x1)
				|| !TryPlotCoordinate(fields[2], out y1) || !TryPlotCoordinate(fields[3], out x2)
				|| !TryPlotCoordinate(fields[4], out y2) || x2 < x1 || y2 < y1) return false;
			try
			{
				string skin = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(fields[5]));
				SkinKey = skin.Length == 0 ? null : skin;
			}
			catch { return false; }
			Rect = new KingdomPlotRules.PlotRect(x1, y1, x2, y2);
			return true;
		}

		private static bool TryIntentFromSnapshot(KingdomPlotRules.PlotRect Rect, string Encoded,
			out KingdomArchitectureIntent Intent, out string Failure)
		{
			Intent = null;
			if (!KingdomArchitectureRules.TryDecodeSnapshot(Encoded,
				out ArchitectureLayoutSnapshot snapshot, out Failure)) return false;
			string hash;
			if (!KingdomArchitectureRules.TryEncodedSnapshotHash(Encoded, out hash, out Failure)) return false;
			int mainX;
			int mainY;
			if (!KingdomArchitectureRules.TryToWorld(Rect.X1, Rect.Y1, snapshot.Width,
				snapshot.Height, snapshot.Facing, snapshot.MainX, snapshot.MainY,
				out mainX, out mainY) || !Rect.Contains(mainX, mainY))
			{
				Failure = "The authored plot main anchor does not fit its exact rectangle.";
				return false;
			}
			KingdomArchitectureIntent rebuilt = KingdomArchitectureIntent.CreateRaw(
				KingdomArchitectureRuntime.ReceiptSchema, snapshot.BuildKey, snapshot.PlanKey,
				snapshot.BindingKey, snapshot.TierKey, snapshot.VariantKey, snapshot.PaletteKey,
				snapshot.LotType, snapshot.LotSize, snapshot.Facing, Encoded, hash, Rect, mainX, mainY);
			if (!KingdomArchitectureRuntime.TryValidate(rebuilt, out Failure)) return false;
			Intent = rebuilt;
			return true;
		}

		private static bool TryEncodePlotSkin(string SkinKey, out string Encoded)
		{
			Encoded = null;
			string skin = SkinKey ?? "";
			if (skin.Length > MaxPlotSkinChars || HasPlotControl(skin)) return false;
			try { Encoded = Convert.ToBase64String(StrictPlotUtf8.GetBytes(skin)); }
			catch { return false; }
			return true;
		}

		private static bool TryDecodePlotSkin(string Encoded, out string SkinKey)
		{
			SkinKey = null;
			try
			{
				byte[] bytes = Convert.FromBase64String(Encoded);
				if (Convert.ToBase64String(bytes) != Encoded) return false;
				string skin = StrictPlotUtf8.GetString(bytes);
				if (skin.Length > MaxPlotSkinChars || HasPlotControl(skin)) return false;
				SkinKey = skin.Length == 0 ? null : skin;
				return true;
			}
			catch { return false; }
		}

		private static bool HasPlotControl(string Value)
		{
			for (int i = 0; i < Value.Length; i++) if (char.IsControl(Value[i])) return true;
			return false;
		}

		private static bool TryPlotRect(KingdomPlotRules.PlotRect Rect)
		{
			return Rect.X2 >= Rect.X1 && Rect.Y2 >= Rect.Y1
				&& TryPlotCoordinate(PlotCoordinate(Rect.X1), out _)
				&& TryPlotCoordinate(PlotCoordinate(Rect.Y1), out _)
				&& TryPlotCoordinate(PlotCoordinate(Rect.X2), out _)
				&& TryPlotCoordinate(PlotCoordinate(Rect.Y2), out _);
		}

		private static string PlotCoordinate(int Value)
		{
			return Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
		}

		private static string PlotPayloadHash(string Text)
		{
			try
			{
				byte[] bytes = StrictPlotUtf8.GetBytes(Text);
				byte[] digest;
				using (System.Security.Cryptography.SHA256 sha =
					System.Security.Cryptography.SHA256.Create()) digest = sha.ComputeHash(bytes);
				System.Text.StringBuilder result = new System.Text.StringBuilder(64);
				for (int i = 0; i < digest.Length; i++) result.Append(digest[i].ToString("x2",
					global::System.Globalization.CultureInfo.InvariantCulture));
				return result.ToString();
			}
			catch { return null; }
		}

		private static bool CanonicalPlotHash(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if ((Value[i] < '0' || Value[i] > '9')
					&& (Value[i] < 'a' || Value[i] > 'f')) return false;
			return true;
		}

		private static bool SameRect(KingdomPlotRules.PlotRect A,
			KingdomPlotRules.PlotRect B)
		{
			return A.X1 == B.X1 && A.Y1 == B.Y1 && A.X2 == B.X2 && A.Y2 == B.Y2;
		}

		private static bool SameIntent(KingdomArchitectureIntent A, KingdomArchitectureIntent B)
		{
			return A != null && B != null && A.EncodedSnapshot == B.EncodedSnapshot
				&& A.SnapshotHash == B.SnapshotHash && SameRect(A.Rect, B.Rect)
				&& A.MainWorldX == B.MainWorldX && A.MainWorldY == B.MainWorldY;
		}

		private static bool SamePlotSkin(string A, string B)
		{
			return (string.IsNullOrEmpty(A) ? null : A)
				== (string.IsNullOrEmpty(B) ? null : B);
		}

		private static bool TryPlotCoordinate(string Text, out int Value)
		{
			return int.TryParse(Text, global::System.Globalization.NumberStyles.None,
				global::System.Globalization.CultureInfo.InvariantCulture, out Value)
				&& Value >= 0 && Value <= 1023
				&& Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture) == Text;
		}

	}
}
