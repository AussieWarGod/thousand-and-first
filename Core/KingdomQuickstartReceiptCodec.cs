using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartRules
	{
		/// <summary>
		/// Wire tag of a receipt this version mints. <c>q1</c> stays exactly what it was: the same
		/// ten fields under the same digest, written by versions whose world build never bared the
		/// shelter lots. Only the tag distinguishes them, so an old wire keeps its exact bytes and
		/// its old obligations, and neither tag can be read as the other.
		/// </summary>
		private const string ShelterWireTag = "q2";

		private const string LegacyWireTag = "q1";

		/// <summary>
		/// Wire tags of a receipt that carries a founding cohort. They are new tags rather than a
		/// widened <c>q1</c>/<c>q2</c> because the founders form has five more fields: a wire
		/// written before founders existed keeps its exact eleven fields and its exact bytes, and
		/// no tag can be read as another. <c>q4</c> is <c>q3</c> plus the shelter obligation, the
		/// same way <c>q2</c> is <c>q1</c> plus it.
		/// </summary>
		private const string FoundersWireTag = "q3";

		private const string FoundersShelterWireTag = "q4";

		private const int LegacyFieldCount = 11;

		private const int FoundersFieldCount = 16;

		/// <summary>
		/// Emits the OLD eleven-field form if and only if the receipt carries no cohort, so every
		/// receipt written before this version, and every world founded with the founders option
		/// off, re-encodes byte for byte as it was read.
		/// </summary>
		public static string Encode(KingdomQuickstartReceipt Receipt)
		{
			if (!Valid(Receipt)) return null;
			bool founders = Receipt.FoundersDisposition
				!= KingdomQuickstartFoundersDisposition.Omitted;
			string tag = founders
				? (Receipt.ShelterObligation ? FoundersShelterWireTag : FoundersWireTag)
				: (Receipt.ShelterObligation ? ShelterWireTag : LegacyWireTag);
			string body = tag
				+ "|" + B64(Receipt.ProfileKey) + "|" + B64(Receipt.ZoneId)
				+ "|" + ((int)Receipt.Phase).ToString(CultureInfo.InvariantCulture)
				+ "|" + B64(Receipt.FoodBlueprint) + "|" + B64(Receipt.WaterObjectId)
				+ "|" + B64(Receipt.LarderObjectId) + "|" + B64(Receipt.StockpileObjectId)
				+ "|" + ((int)Receipt.AdvisorDisposition).ToString(
					CultureInfo.InvariantCulture) + "|" + B64(Receipt.AdvisorObjectId);
			if (founders)
			{
				body += "|" + ((int)Receipt.FoundersDisposition).ToString(
					CultureInfo.InvariantCulture);
				for (int i = 0; i < FounderCount; i++)
					body += "|" + B64(Receipt.FounderObjectIds[i]);
			}
			return body + "|" + Digest(body);
		}

		public static bool TryDecode(string Wire, out KingdomQuickstartReceipt Receipt)
		{
			Receipt = null;
			if (string.IsNullOrEmpty(Wire) || Wire.Length > MaximumWireLength) return false;
			string[] fields = Wire.Split('|');
			bool founders = fields[0] == FoundersWireTag
				|| fields[0] == FoundersShelterWireTag;
			bool legacy = fields[0] == LegacyWireTag || fields[0] == ShelterWireTag;
			if (!founders && !legacy) return false;
			int count = founders ? FoundersFieldCount : LegacyFieldCount;
			if (fields.Length != count) return false;
			string body = string.Join("|", fields, 0, count - 1);
			if (!string.Equals(Digest(body), fields[count - 1],
				StringComparison.Ordinal)) return false;
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
					AdvisorObjectId = Text(fields[9]),
					ShelterObligation = string.Equals(fields[0], ShelterWireTag,
						StringComparison.Ordinal)
						|| string.Equals(fields[0], FoundersShelterWireTag,
							StringComparison.Ordinal),
					FoundersDisposition = founders
						? (KingdomQuickstartFoundersDisposition)int.Parse(fields[10],
							NumberStyles.Integer, CultureInfo.InvariantCulture)
						: KingdomQuickstartFoundersDisposition.Omitted
				};
				if (founders)
					for (int i = 0; i < FounderCount; i++)
						Receipt.FounderObjectIds[i] = Text(fields[11 + i]);
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
