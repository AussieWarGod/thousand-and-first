using System;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Canonical frozen plot-works truth carried inside a founding-heart receipt.</summary>
	public static class KingdomFoundingHeartStakeRules
	{
		private const int MaximumText = 512;

		public static bool TryCreate(string BuildKey, string DisplayName, string Blueprint,
			int FootprintX1, int FootprintY1, int FootprintX2, int FootprintY2, int Roof,
			bool Open, bool Carved, string WallBlueprint, string Contents, int Staff,
			bool ThresholdManning, int Defence, bool HasDoor, int DoorX, int DoorY,
			bool PurposeLegacy, out KingdomFoundingHeartStakeTruth Truth)
		{
			Truth = new KingdomFoundingHeartStakeTruth
			{
				BuildKey = BuildKey,
				DisplayName = DisplayName,
				Blueprint = Blueprint,
				FootprintX1 = FootprintX1,
				FootprintY1 = FootprintY1,
				FootprintX2 = FootprintX2,
				FootprintY2 = FootprintY2,
				Roof = Roof,
				Open = Open,
				Carved = Carved,
				WallBlueprint = WallBlueprint,
				Contents = Contents,
				Staff = Staff,
				ThresholdManning = ThresholdManning,
				Defence = Defence,
				HasDoor = HasDoor,
				DoorX = DoorX,
				DoorY = DoorY,
				PurposeLegacy = PurposeLegacy
			};
			if (Valid(Truth)) return true;
			Truth = null;
			return false;
		}

		public static bool Valid(KingdomFoundingHeartStakeTruth Truth)
		{
			return Truth != null && Truth.BuildKey == "heartbasin"
				&& Text(Truth.DisplayName) && Text(Truth.Blueprint)
				&& Optional(Truth.WallBlueprint) && Optional(Truth.Contents)
				&& Truth.FootprintX1 >= 0 && Truth.FootprintY1 >= 0
				&& Truth.FootprintX1 <= Truth.FootprintX2
				&& Truth.FootprintY1 <= Truth.FootprintY2
				&& Truth.Roof >= 0 && Truth.Roof <= 3
				&& (!Truth.Carved || Truth.Roof == 0 || Truth.Roof == 3)
				&& (Truth.Carved || Truth.Roof != 3)
				&& (Truth.Roof == 2) == !string.IsNullOrEmpty(Truth.WallBlueprint)
				&& Truth.Staff >= 0 && Truth.Defence >= 0
				&& (!Truth.HasDoor || (Truth.Roof == 2 || Truth.Roof == 3)
					&& DoorOnBorder(Truth));
		}

		public static string Encode(KingdomFoundingHeartStakeTruth Truth)
		{
			if (!Valid(Truth)) return null;
			return "s1|" + B64(Truth.BuildKey) + "|" + B64(Truth.DisplayName) + "|"
				+ B64(Truth.Blueprint) + "|" + N(Truth.FootprintX1) + "|"
				+ N(Truth.FootprintY1) + "|" + N(Truth.FootprintX2) + "|"
				+ N(Truth.FootprintY2) + "|" + N(Truth.Roof) + "|" + B(Truth.Open) + "|"
				+ B(Truth.Carved) + "|" + B64(Truth.WallBlueprint) + "|" + B64(Truth.Contents)
				+ "|" + N(Truth.Staff) + "|" + B(Truth.ThresholdManning) + "|"
				+ N(Truth.Defence) + "|" + B(Truth.HasDoor) + "|" + N(Truth.DoorX) + "|"
				+ N(Truth.DoorY) + "|" + B(Truth.PurposeLegacy);
		}

		public static bool TryDecode(string Encoded, out KingdomFoundingHeartStakeTruth Truth)
		{
			Truth = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaximumText * 8) return false;
			string[] parts = Encoded.Split('|');
			if (parts.Length != 20 || parts[0] != "s1") return false;
			try
			{
				Truth = new KingdomFoundingHeartStakeTruth
				{
					BuildKey = Required(parts[1]), DisplayName = Required(parts[2]),
					Blueprint = Required(parts[3]), FootprintX1 = Number(parts[4]),
					FootprintY1 = Number(parts[5]), FootprintX2 = Number(parts[6]),
					FootprintY2 = Number(parts[7]), Roof = Number(parts[8]),
					Open = Boolean(parts[9]), Carved = Boolean(parts[10]),
					WallBlueprint = OptionalText(parts[11]), Contents = OptionalText(parts[12]),
					Staff = Number(parts[13]), ThresholdManning = Boolean(parts[14]),
					Defence = Number(parts[15]), HasDoor = Boolean(parts[16]),
					DoorX = Number(parts[17]), DoorY = Number(parts[18]),
					PurposeLegacy = Boolean(parts[19])
				};
				if (!Valid(Truth) || Encode(Truth) != Encoded) Truth = null;
			}
			catch { Truth = null; }
			return Truth != null;
		}

		private static bool Text(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= MaximumText;
		}

		private static bool Optional(string Value)
		{
			return Value == null || Text(Value);
		}

		private static bool DoorOnBorder(KingdomFoundingHeartStakeTruth Truth)
		{
			bool border = Truth.DoorX == Truth.FootprintX1 || Truth.DoorX == Truth.FootprintX2
				|| Truth.DoorY == Truth.FootprintY1 || Truth.DoorY == Truth.FootprintY2;
			bool corner = (Truth.DoorX == Truth.FootprintX1
				|| Truth.DoorX == Truth.FootprintX2)
				&& (Truth.DoorY == Truth.FootprintY1 || Truth.DoorY == Truth.FootprintY2);
			return border && !corner && Truth.DoorX >= Truth.FootprintX1
				&& Truth.DoorX <= Truth.FootprintX2 && Truth.DoorY >= Truth.FootprintY1
				&& Truth.DoorY <= Truth.FootprintY2;
		}

		private static string B64(string Value)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Value ?? ""));
		}

		private static string Required(string Value)
		{
			string result = Encoding.UTF8.GetString(Convert.FromBase64String(Value));
			return string.IsNullOrEmpty(result) ? null : result;
		}

		private static string OptionalText(string Value)
		{
			return Required(Value);
		}

		private static int Number(string Value)
		{
			return int.Parse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture);
		}

		private static bool Boolean(string Value)
		{
			if (Value == "0") return false;
			if (Value == "1") return true;
			throw new FormatException("boolean is not canonical");
		}

		private static string N(int Value)
		{
			return Value.ToString(CultureInfo.InvariantCulture);
		}

		private static string B(bool Value)
		{
			return Value ? "1" : "0";
		}
	}
}
