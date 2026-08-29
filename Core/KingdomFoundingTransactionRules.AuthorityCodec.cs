using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransactionRules
	{
		public static string FormatAuthority(KingdomFoundingAuthority Authority)
		{
			if (!AuthorityValid(Authority))
			{
				return null;
			}
			return string.Join("|", new string[]
			{
				AuthorityVersion,
				((int)Authority.Kind).ToString(),
				((int)Authority.OwnerKind).ToString(),
				EncodeField(Authority.TransactionID),
				EncodeField(Authority.OwnerNonce),
				EncodeField(Authority.RealmFaction),
				EncodeField(Authority.ZoneID),
				Authority.RiteX.ToString(),
				Authority.RiteY.ToString(),
				Authority.PayloadDigest
			});
		}

		public static bool TryParseAuthority(string Encoded,
			out KingdomFoundingAuthority Authority)
		{
			Authority = default(KingdomFoundingAuthority);
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaximumAuthorityLength)
			{
				return false;
			}
			string[] fields = Encoded.Split('|');
			if (fields.Length != 10 || fields[0] != AuthorityVersion ||
				!int.TryParse(fields[1], out var rawKind) ||
				!TryParseKind(rawKind, out Authority.Kind) ||
				!int.TryParse(fields[2], out var rawOwner))
			{
				return false;
			}
			Authority.OwnerKind = (KingdomFoundingOwnerKind)rawOwner;
			if (!TryDecodeField(fields[3], 64, out Authority.TransactionID) ||
				!TryDecodeField(fields[4], 64, out Authority.OwnerNonce) ||
				!TryDecodeField(fields[5], 256, out Authority.RealmFaction) ||
				!TryDecodeField(fields[6], 512, out Authority.ZoneID) ||
				!int.TryParse(fields[7], out Authority.RiteX) ||
				!int.TryParse(fields[8], out Authority.RiteY))
			{
				return false;
			}
			Authority.PayloadDigest = fields[9];
			return AuthorityValid(Authority) && FormatAuthority(Authority) == Encoded;
		}

		public static bool AuthorityMatches(string Encoded,
			KingdomFoundingAuthority Expected)
		{
			return TryParseAuthority(Encoded, out var parsed) &&
				FormatAuthority(parsed) == FormatAuthority(Expected);
		}

		public static bool AuthorityValid(KingdomFoundingAuthority Authority)
		{
			return IsKnownKind(Authority.Kind) && Authority.Kind != KingdomFoundingKind.None &&
				IsKnownOwnerKind(Authority.OwnerKind) &&
				Authority.OwnerKind != KingdomFoundingOwnerKind.None &&
				IsNonce(Authority.TransactionID) && IsNonce(Authority.OwnerNonce) &&
				Bounded(Authority.RealmFaction, 256) && Bounded(Authority.ZoneID, 512) &&
				Authority.RiteX >= 0 && Authority.RiteX <= 255 &&
				Authority.RiteY >= 0 && Authority.RiteY <= 255 &&
				IsLowerHex(Authority.PayloadDigest, 64);
		}

		public static string PayloadDigest(KingdomFoundingKind Kind, string Name,
			string Vocation, string VillageFaction, string VillageDisplay,
			int OriginalVolume, int OriginalMax, int CommittedVolume, int CommittedMax,
			string OriginalComponents, string CommittedComponents)
		{
			if (!IsKnownKind(Kind) || Kind == KingdomFoundingKind.None)
			{
				return null;
			}
			StringBuilder payload = new StringBuilder();
			AppendDigestField(payload, ((int)Kind).ToString());
			AppendDigestField(payload, Name);
			AppendDigestField(payload, Vocation);
			AppendDigestField(payload, VillageFaction);
			AppendDigestField(payload, VillageDisplay);
			AppendDigestField(payload, OriginalVolume.ToString());
			AppendDigestField(payload, OriginalMax.ToString());
			AppendDigestField(payload, CommittedVolume.ToString());
			AppendDigestField(payload, CommittedMax.ToString());
			AppendDigestField(payload, OriginalComponents);
			AppendDigestField(payload, CommittedComponents);
			try
			{
				using (SHA256 sha = SHA256.Create())
				{
					byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString()));
					StringBuilder hex = new StringBuilder(64);
					for (int i = 0; i < digest.Length; i++)
					{
						hex.Append(digest[i].ToString("x2"));
					}
					return hex.ToString();
				}
			}
			catch
			{
				return null;
			}
		}

		public static bool TryDecodeComponents(string Encoded,
			out Dictionary<string, int> Components)
		{
			Components = new Dictionary<string, int>(StringComparer.Ordinal);
			if (Encoded == null || Encoded.Length > MaximumComponentEncodingLength)
			{
				return false;
			}
			if (Encoded.Length == 0)
			{
				return true;
			}
			string[] rows = Encoded.Split(';');
			if (rows.Length > MaximumComponentCount)
			{
				return false;
			}
			string previous = null;
			foreach (string row in rows)
			{
				int split = row.LastIndexOf(':');
				string amountText = split >= 0 && split < row.Length - 1
					? row.Substring(split + 1) : null;
				if (split <= 0 || split == row.Length - 1 ||
					!int.TryParse(amountText, out var amount) ||
					amount.ToString() != amountText ||
					amount <= 0 || amount > 1000 ||
					!TryDecodeField(row.Substring(0, split), MaximumComponentNameLength,
						out var key) || string.IsNullOrEmpty(key) ||
					Components.ContainsKey(key) ||
					(previous != null && string.CompareOrdinal(previous, key) >= 0))
				{
					Components.Clear();
					return false;
				}
				Components.Add(key, amount);
				previous = key;
			}
			return true;
		}

		public static bool ComponentsDescribePureWater(Dictionary<string, int> Components,
			int Volume)
		{
			if (Components == null || Volume < 0)
			{
				return false;
			}
			if (Volume == 0)
			{
				return Components.Count == 0;
			}
			return Components.Count == 1 && Components.TryGetValue("water", out var water) &&
				water == 1000;
		}

		public static bool IsNonce(string Value)
		{
			return IsLowerHex(Value, 32);
		}

		public static bool IsLowerHex(string Value, int Length)
		{
			if (Value == null || Value.Length != Length)
			{
				return false;
			}
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Builds one write-ahead village-standing effect receipt. It binds the paid
		/// transaction and authority to the exact faction, display, site, and canonical before/after
		/// scaled pair; it is not inferred from today's standing.</summary>
		public static string VillageStandingEffectDigest(string TransactionId,
			string Authority, string VillageFaction, string VillageDisplay, string ZoneId,
			int Before, int BeforeCarry, int After, int AfterCarry)
		{
			if (!IsNonce(TransactionId) ||
				!TryParseAuthority(Authority, out KingdomFoundingAuthority parsed) ||
				parsed.Kind != KingdomFoundingKind.VillageCharter ||
				parsed.TransactionID != TransactionId || parsed.ZoneID != ZoneId ||
				!Bounded(VillageFaction, 256) || !Bounded(VillageDisplay, 256) ||
				!KingdomStandingRules.CanonicalPair(Before, BeforeCarry) ||
				!KingdomStandingRules.CanonicalPair(After, AfterCarry) ||
				After != KingdomRules.VillageCharterSealedStanding || AfterCarry != 0 ||
				(long)Before * KingdomStandingRules.FractionScale + BeforeCarry >=
					(long)After * KingdomStandingRules.FractionScale + AfterCarry) return null;
			StringBuilder payload = new StringBuilder();
			AppendDigestField(payload, "taf-village-standing-effect-v1");
			AppendDigestField(payload, TransactionId);
			AppendDigestField(payload, Authority);
			AppendDigestField(payload, VillageFaction);
			AppendDigestField(payload, VillageDisplay);
			AppendDigestField(payload, ZoneId);
			AppendDigestField(payload, Before.ToString());
			AppendDigestField(payload, BeforeCarry.ToString());
			AppendDigestField(payload, After.ToString());
			AppendDigestField(payload, AfterCarry.ToString());
			try
			{
				using (SHA256 sha = SHA256.Create())
				{
					byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString()));
					StringBuilder hex = new StringBuilder(64);
					for (int i = 0; i < digest.Length; i++) hex.Append(digest[i].ToString("x2"));
					return hex.ToString();
				}
			}
			catch { return null; }
		}

		private static bool Bounded(string Value, int Maximum)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= Maximum;
		}

		private static string EncodeField(string Value)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Value ?? ""));
		}

		private static bool TryDecodeField(string Encoded, int Maximum,
			out string Value)
		{
			Value = null;
			if (Encoded == null || Encoded.Length > Maximum * 4 + 8)
			{
				return false;
			}
			try
			{
				byte[] bytes = Convert.FromBase64String(Encoded);
				Value = new UTF8Encoding(false, true).GetString(bytes);
				return Value.Length <= Maximum && EncodeField(Value) == Encoded;
			}
			catch
			{
				Value = null;
				return false;
			}
		}

		private static void AppendDigestField(StringBuilder Builder, string Value)
		{
			string value = Value ?? "";
			byte[] bytes = Encoding.UTF8.GetBytes(value);
			Builder.Append(bytes.Length).Append(':').Append(value).Append(';');
		}

	}
}
