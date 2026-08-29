using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Pure codec and identity rules for callback-safe founding-heart projection.</summary>
	public static class KingdomFoundingHeartRules
	{
		public const int SlotCount = 6;
		public const int RelicSlot = 0;
		public const int NorthWestStakeSlot = 1;
		public const int NorthEastStakeSlot = 2;
		public const int SouthWestStakeSlot = 3;
		public const int SouthEastStakeSlot = 4;
		public const int WorksSlot = 5;
		private const int MaximumText = 262144;

		public static bool TryCreate(string TransactionId, string ZoneId, int RiteX, int RiteY,
			int SurveyX1, int SurveyY1, int SurveyX2, int SurveyY2,
			int RectX1, int RectY1, int RectX2, int RectY2, long StartedTick,
			long TotalTicks, string Payload, string StakeTruth, out KingdomFoundingHeartPlan Plan)
		{
			Plan = new KingdomFoundingHeartPlan
			{
				TransactionId = TransactionId,
				ZoneId = ZoneId,
				RiteX = RiteX,
				RiteY = RiteY,
				SurveyX1 = SurveyX1,
				SurveyY1 = SurveyY1,
				SurveyX2 = SurveyX2,
				SurveyY2 = SurveyY2,
				RectX1 = RectX1,
				RectY1 = RectY1,
				RectX2 = RectX2,
				RectY2 = RectY2,
				StartedTick = StartedTick,
				TotalTicks = TotalTicks,
					PlotId = StableId(TransactionId, ZoneId, "plot"),
					Payload = Payload,
					StakeTruth = StakeTruth,
					States = new int[SlotCount]
			};
			if (!Valid(Plan))
			{
				Plan = null;
				return false;
			}
			return true;
		}

		public static string StableId(string TransactionId, string ZoneId, string Role)
		{
			if (!LowerHex32(TransactionId) || string.IsNullOrEmpty(ZoneId)
				|| ZoneId.Length > 512 || string.IsNullOrEmpty(Role) || Role.Length > 32)
				return null;
			return "taf-heart-v1-" + Digest(TransactionId + "\n" + ZoneId + "\n" + Role);
		}

		public static string SlotId(KingdomFoundingHeartPlan Plan, int Slot)
		{
			return Plan == null || Slot < 0 || Slot >= SlotCount
				? null : StableId(Plan.TransactionId, Plan.ZoneId, "slot-" + Slot);
		}

		public static bool TryAdvance(KingdomFoundingHeartPlan Plan, int Slot,
			int Expected, int Next)
		{
			if (!Valid(Plan) || Slot < 0 || Slot >= SlotCount
				|| Plan.States[Slot] != Expected || Next != Expected + 1 || Next > 2)
				return false;
			Plan.States[Slot] = Next;
			if (Valid(Plan)) return true;
			Plan.States[Slot] = Expected;
			return false;
		}

		public static bool Complete(KingdomFoundingHeartPlan Plan)
		{
			if (!Valid(Plan)) return false;
			for (int i = 0; i < SlotCount; i++)
				if (Plan.States[i] != 2) return false;
			return true;
		}

		public static string CompletionSeal(KingdomFoundingHeartPlan Plan)
		{
			string encoded = Complete(Plan) ? Encode(Plan) : null;
			return encoded == null ? null : "hs1-" + Digest("founding-heart-complete\n" + encoded);
		}

		public static bool Valid(KingdomFoundingHeartPlan Plan)
		{
			KingdomFoundingHeartStakeTruth stake;
			if (Plan == null || !LowerHex32(Plan.TransactionId)
				|| string.IsNullOrEmpty(Plan.ZoneId) || Plan.ZoneId.Length > 512
				|| string.IsNullOrEmpty(Plan.Payload) || Plan.Payload.Length > MaximumText
				|| !KingdomFoundingHeartStakeRules.TryDecode(Plan.StakeTruth, out stake)
				|| Plan.StartedTick < 0L || Plan.TotalTicks < 1L
				|| Plan.SurveyX1 < 0 || Plan.SurveyY1 < 0
				|| Plan.SurveyX1 > Plan.SurveyX2 || Plan.SurveyY1 > Plan.SurveyY2
				|| Plan.RectX1 > Plan.RectX2 || Plan.RectY1 > Plan.RectY2
				|| Plan.RiteX < Plan.SurveyX1 || Plan.RiteX > Plan.SurveyX2
				|| Plan.RiteY < Plan.SurveyY1 || Plan.RiteY > Plan.SurveyY2
				|| Plan.RectX1 < Plan.SurveyX1 || Plan.RectX2 > Plan.SurveyX2
				|| Plan.RectY1 < Plan.SurveyY1 || Plan.RectY2 > Plan.SurveyY2
				|| stake.FootprintX1 < Plan.RectX1 || stake.FootprintX2 > Plan.RectX2
				|| stake.FootprintY1 < Plan.RectY1 || stake.FootprintY2 > Plan.RectY2
				|| stake.HasDoor && (stake.DoorX < stake.FootprintX1
					|| stake.DoorX > stake.FootprintX2 || stake.DoorY < stake.FootprintY1
					|| stake.DoorY > stake.FootprintY2)
				|| Plan.PlotId != StableId(Plan.TransactionId, Plan.ZoneId, "plot")
				|| Plan.States == null || Plan.States.Length != SlotCount) return false;
			bool open = false;
			for (int i = 0; i < SlotCount; i++)
			{
				int state = Plan.States[i];
				if (state < 0 || state > 2 || open && state != 0) return false;
				if (state < 2) open = true;
			}
			return true;
		}

		public static string Encode(KingdomFoundingHeartPlan Plan)
		{
			if (!Valid(Plan)) return null;
			string frozen = Frozen(Plan);
			string states = StateText(Plan);
			string envelope = "h2|" + frozen + "|" + states;
			return envelope + "|" + Digest(envelope);
		}

		public static bool TryDecode(string Encoded, out KingdomFoundingHeartPlan Plan)
		{
			Plan = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaximumText * 2) return false;
			string[] parts = Encoded.Split('|');
			if (parts.Length != 20 || parts[0] != "h2") return false;
			string frozen = string.Join("|", parts, 1, 17);
			string envelope = "h2|" + frozen + "|" + parts[18];
			if (parts[19] != Digest(envelope)) return false;
			try
			{
				string[] states = parts[18].Split('.');
				if (states.Length != SlotCount) return false;
				Plan = new KingdomFoundingHeartPlan
				{
					TransactionId = Text(parts[1]), ZoneId = Text(parts[2]),
					RiteX = Number(parts[3]), RiteY = Number(parts[4]),
					SurveyX1 = Number(parts[5]), SurveyY1 = Number(parts[6]),
					SurveyX2 = Number(parts[7]), SurveyY2 = Number(parts[8]),
					RectX1 = Number(parts[9]), RectY1 = Number(parts[10]),
					RectX2 = Number(parts[11]), RectY2 = Number(parts[12]),
					StartedTick = Long(parts[13]), TotalTicks = Long(parts[14]),
					PlotId = Text(parts[15]), Payload = Text(parts[16]),
					StakeTruth = Text(parts[17]),
					States = new int[SlotCount]
				};
				for (int i = 0; i < SlotCount; i++) Plan.States[i] = Number(states[i]);
				if (!Valid(Plan) || Encode(Plan) != Encoded) Plan = null;
			}
			catch { Plan = null; }
			return Plan != null;
		}

		private static string Frozen(KingdomFoundingHeartPlan Plan)
		{
			return B64(Plan.TransactionId) + "|" + B64(Plan.ZoneId) + "|" + N(Plan.RiteX) + "|"
				+ N(Plan.RiteY) + "|" + N(Plan.SurveyX1) + "|" + N(Plan.SurveyY1) + "|"
				+ N(Plan.SurveyX2) + "|" + N(Plan.SurveyY2) + "|" + N(Plan.RectX1) + "|"
				+ N(Plan.RectY1) + "|" + N(Plan.RectX2) + "|" + N(Plan.RectY2) + "|"
				+ N(Plan.StartedTick) + "|" + N(Plan.TotalTicks) + "|"
				+ B64(Plan.PlotId) + "|" + B64(Plan.Payload) + "|" + B64(Plan.StakeTruth);
		}

		private static string StateText(KingdomFoundingHeartPlan Plan)
		{
			StringBuilder states = new StringBuilder();
			for (int i = 0; i < SlotCount; i++)
			{
				if (i > 0) states.Append('.');
				states.Append(Plan.States[i]);
			}
			return states.ToString();
		}

		private static string N(long Value)
		{
			return Value.ToString(CultureInfo.InvariantCulture);
		}

		private static bool LowerHex32(string Value)
		{
			if (Value == null || Value.Length != 32) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9')
					|| (Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}

		private static string B64(string Value)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Value ?? ""));
		}

		private static string Text(string Value)
		{
			return Encoding.UTF8.GetString(Convert.FromBase64String(Value));
		}

		private static int Number(string Value)
		{
			return int.Parse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture);
		}

		private static long Long(string Value)
		{
			return long.Parse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture);
		}

		private static string Digest(string Value)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(Value ?? "");
			byte[] digest;
			using (SHA256 hash = SHA256.Create()) digest = hash.ComputeHash(bytes);
			StringBuilder text = new StringBuilder(64);
			for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2",
				CultureInfo.InvariantCulture));
			return text.ToString();
		}
	}
}
