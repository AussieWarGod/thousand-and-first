using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartRules
	{
		public static string Encode(KingdomQuickstartReceipt Receipt)
		{
			if (!Valid(Receipt)) return null;
			string body = "q1|" + B64(Receipt.ProfileKey) + "|" + B64(Receipt.ZoneId)
				+ "|" + ((int)Receipt.Phase).ToString(CultureInfo.InvariantCulture)
				+ "|" + B64(Receipt.FoodBlueprint) + "|" + B64(Receipt.WaterObjectId)
				+ "|" + B64(Receipt.LarderObjectId) + "|" + B64(Receipt.StockpileObjectId)
				+ "|" + ((int)Receipt.AdvisorDisposition).ToString(
					CultureInfo.InvariantCulture) + "|" + B64(Receipt.AdvisorObjectId);
			return body + "|" + Digest(body);
		}

		public static bool TryDecode(string Wire, out KingdomQuickstartReceipt Receipt)
		{
			Receipt = null;
			if (string.IsNullOrEmpty(Wire) || Wire.Length > MaximumWireLength) return false;
			string[] fields = Wire.Split('|');
			if (fields.Length != 11 || fields[0] != "q1") return false;
			string body = string.Join("|", fields, 0, 10);
			if (!string.Equals(Digest(body), fields[10], StringComparison.Ordinal)) return false;
			try
			{
				Receipt = new KingdomQuickstartReceipt
				{
					ProfileKey = Text(fields[1]),
					ZoneId = Text(fields[2]),
					Phase = (KingdomQuickstartPhase)int.Parse(fields[3],
						NumberStyles.Integer, CultureInfo.InvariantCulture),
					FoodBlueprint = Text(fields[4]),
					WaterObjectId = Text(fields[5]),
					LarderObjectId = Text(fields[6]),
					StockpileObjectId = Text(fields[7]),
					AdvisorDisposition = (KingdomQuickstartAdvisorDisposition)int.Parse(
						fields[8], NumberStyles.Integer, CultureInfo.InvariantCulture),
					AdvisorObjectId = Text(fields[9])
				};
			}
			catch
			{
				Receipt = null;
				return false;
			}
			if (!Valid(Receipt) || !string.Equals(Encode(Receipt), Wire,
				StringComparison.Ordinal))
			{
				Receipt = null;
				return false;
			}
			return true;
		}

		public static string WorldReservation(KingdomQuickstartProfile Profile)
		{
			if (Profile == null || !TryProfile(Profile.Key,
				out KingdomQuickstartProfile canonical) || !ReferenceEquals(Profile, canonical))
				return null;
			string body = "qr1|" + B64(Profile.Key) + "|" + B64(Profile.ZoneId);
			return body + "|" + Digest(body);
		}

		public static bool WorldReservationMatches(string Wire,
			KingdomQuickstartProfile Profile)
		{
			string expected = WorldReservation(Profile);
			return expected != null && string.Equals(Wire, expected, StringComparison.Ordinal);
		}

		/// <summary>
		/// Stable, checksummed ownership mark for a physical grant. It excludes receipt phase and
		/// object identity so the same mark proves an object on both sides of receipt publication.
		/// </summary>
		public static string GrantMarker(KingdomQuickstartReceipt Receipt,
			KingdomQuickstartPhase Target)
		{
			if (!Valid(Receipt) || Receipt.Phase < KingdomQuickstartPhase.Founded
				|| Target < KingdomQuickstartPhase.WaterStocked
				|| Target > KingdomQuickstartPhase.AdvisorResolved) return null;
			string body = "qg1|" + B64(Receipt.ProfileKey) + "|" + B64(Receipt.ZoneId)
				+ "|" + ((int)Target).ToString(CultureInfo.InvariantCulture)
				+ "|" + B64(Receipt.FoodBlueprint);
			return body + "|" + Digest(body);
		}

		private static string B64(string Value)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Value ?? ""));
		}

		private static string Text(string Value)
		{
			return Encoding.UTF8.GetString(Convert.FromBase64String(Value));
		}

		private static string Digest(string Value)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(Value ?? "");
			byte[] digest;
			using (SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(bytes);
			StringBuilder text = new StringBuilder(64);
			for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2",
				CultureInfo.InvariantCulture));
			return text.ToString();
		}
	}
}
