using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Canonical proof naming one physically paired delve.</summary>
	public sealed class KingdomDelveLinkReceipt
	{
		public string HeadZoneId { get; internal set; }
		public string FootZoneId { get; internal set; }
		public int X { get; internal set; }
		public int Y { get; internal set; }
		public string RootId { get; internal set; }
		public string LotId { get; internal set; }
		public string SnapshotHash { get; internal set; }
		public string DownSlot { get; internal set; }
		public string HeadEndpointId { get; internal set; }
		public string FootEndpointId { get; internal set; }
		public string Token { get; internal set; }
	}

	/// <summary>
	/// Engine-free receipt and identity rules. State is evidence only when this canonical record
	/// names both endpoint objects; the engine-coupled layer separately proves those objects and
	/// their two vanilla zone connections still exist.
	/// </summary>
	public static class KingdomDelveLinkRules
	{
		public const string Prefix = "dl1";
		public const int MaxReceiptChars = 4096;
		public const int MaxZoneChars = 256;
		public const int MaxIdChars = 256;
		public const int MaxKeyChars = 128;
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static bool TryToken(string HeadZoneId, string FootZoneId, int X, int Y,
			string RootId, string LotId, string SnapshotHash, string DownSlot,
			out string Token, out string Failure)
		{
			Token = null;
			Failure = null;
			if (!ValidZonePair(HeadZoneId, FootZoneId))
				return Fail("delve link zones are not one canonical shaft pair", out Failure);
			if (X < 0 || X > 511 || Y < 0 || Y > 511)
				return Fail("delve link coordinate is outside the bounded zone envelope", out Failure);
			if (!Bounded(RootId, MaxIdChars) || !Bounded(LotId, MaxIdChars)
				|| !Bounded(DownSlot, MaxKeyChars))
				return Fail("delve link identity is absent, overlong, or contains controls", out Failure);
			if (!CanonicalHash(SnapshotHash))
				return Fail("delve link architecture hash is not canonical", out Failure);
			string core = "dlt1|" + Frame(HeadZoneId) + Frame(FootZoneId)
				+ X.ToString(CultureInfo.InvariantCulture) + ":"
				+ Y.ToString(CultureInfo.InvariantCulture) + ":"
				+ Frame(RootId) + Frame(LotId) + SnapshotHash + ":" + Frame(DownSlot);
			Token = Hash(core);
			return true;
		}

		public static bool TryCreate(string HeadZoneId, string FootZoneId, int X, int Y,
			string RootId, string LotId, string SnapshotHash, string DownSlot,
			string HeadEndpointId, string FootEndpointId,
			out KingdomDelveLinkReceipt Receipt, out string Failure)
		{
			Receipt = null;
			Failure = null;
			string token;
			if (!TryToken(HeadZoneId, FootZoneId, X, Y, RootId, LotId, SnapshotHash,
				DownSlot, out token, out Failure)) return false;
			if (!Bounded(HeadEndpointId, MaxIdChars) || !Bounded(FootEndpointId, MaxIdChars)
				|| HeadEndpointId == FootEndpointId)
				return Fail("delve link endpoint identities are absent, equal, or invalid", out Failure);
			Receipt = new KingdomDelveLinkReceipt
			{
				HeadZoneId = HeadZoneId,
				FootZoneId = FootZoneId,
				X = X,
				Y = Y,
				RootId = RootId,
				LotId = LotId,
				SnapshotHash = SnapshotHash,
				DownSlot = DownSlot,
				HeadEndpointId = HeadEndpointId,
				FootEndpointId = FootEndpointId,
				Token = token
			};
			return true;
		}

		public static bool TryEncode(KingdomDelveLinkReceipt Receipt,
			out string Encoded, out string Failure)
		{
			Encoded = null;
			Failure = null;
			KingdomDelveLinkReceipt checkedReceipt;
			if (Receipt == null || !TryCreate(Receipt.HeadZoneId, Receipt.FootZoneId,
				Receipt.X, Receipt.Y, Receipt.RootId, Receipt.LotId, Receipt.SnapshotHash,
				Receipt.DownSlot, Receipt.HeadEndpointId, Receipt.FootEndpointId,
				out checkedReceipt, out Failure)) return false;
			if (Receipt.Token != checkedReceipt.Token)
				return Fail("delve link token disagrees with its scalar identity", out Failure);
			string body = Prefix + "|" + B64(Receipt.HeadZoneId) + "|" + B64(Receipt.FootZoneId)
				+ "|" + Receipt.X.ToString(CultureInfo.InvariantCulture)
				+ "|" + Receipt.Y.ToString(CultureInfo.InvariantCulture)
				+ "|" + B64(Receipt.RootId) + "|" + B64(Receipt.LotId)
				+ "|" + Receipt.SnapshotHash + "|" + B64(Receipt.DownSlot)
				+ "|" + B64(Receipt.HeadEndpointId) + "|" + B64(Receipt.FootEndpointId)
				+ "|" + Receipt.Token;
			string encoded = body + "|" + Hash(body);
			if (encoded.Length > MaxReceiptChars)
				return Fail("delve link receipt exceeds its storage bound", out Failure);
			Encoded = encoded;
			return true;
		}

		public static bool TryDecode(string Encoded, out KingdomDelveLinkReceipt Receipt,
			out string Failure)
		{
			Receipt = null;
			Failure = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaxReceiptChars || HasControl(Encoded))
				return Fail("delve link receipt is absent, overlong, or contains controls", out Failure);
			string[] fields = Encoded.Split('|');
			if (fields.Length != 13 || fields[0] != Prefix)
				return Fail("delve link receipt shape or schema is unknown", out Failure);
			string body = string.Join("|", fields, 0, 12);
			if (!CanonicalHash(fields[12]) || Hash(body) != fields[12])
				return Fail("delve link receipt digest does not verify", out Failure);
			string head;
			string foot;
			string root;
			string lot;
			string slot;
			string headEndpoint;
			string footEndpoint;
			int x;
			int y;
			if (!TryB64(fields[1], out head) || !TryB64(fields[2], out foot)
				|| !CanonicalInt(fields[3], out x) || !CanonicalInt(fields[4], out y)
				|| !TryB64(fields[5], out root) || !TryB64(fields[6], out lot)
				|| !TryB64(fields[8], out slot) || !TryB64(fields[9], out headEndpoint)
				|| !TryB64(fields[10], out footEndpoint))
				return Fail("delve link receipt field encoding is malformed", out Failure);
			KingdomDelveLinkReceipt receipt;
			if (!TryCreate(head, foot, x, y, root, lot, fields[7], slot,
				headEndpoint, footEndpoint, out receipt, out Failure)) return false;
			if (receipt.Token != fields[11])
				return Fail("delve link receipt token does not verify", out Failure);
			string canonical;
			if (!TryEncode(receipt, out canonical, out Failure) || canonical != Encoded)
				return Fail("delve link receipt is not canonical", out Failure);
			Receipt = receipt;
			return true;
		}

		private static bool ValidZonePair(string Head, string Foot)
		{
			if (!Bounded(Head, MaxZoneChars) || !Bounded(Foot, MaxZoneChars)) return false;
			string derived;
			return KingdomDelveRules.TryFootZoneId(Head, out derived) && derived == Foot;
		}

		private static bool Bounded(string Value, int Max)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= Max && !HasControl(Value);
		}

		private static bool HasControl(string Value)
		{
			if (Value == null) return false;
			for (int i = 0; i < Value.Length; i++)
			{
				if (char.IsControl(Value[i])) return true;
			}
			return false;
		}

		private static bool CanonicalHash(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
			}
			return true;
		}

		private static bool CanonicalInt(string Value, out int Parsed)
		{
			Parsed = 0;
			return int.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture,
				out Parsed) && Parsed.ToString(CultureInfo.InvariantCulture) == Value;
		}

		private static string Frame(string Value)
		{
			return Value.Length.ToString(CultureInfo.InvariantCulture) + ":" + Value;
		}

		private static string B64(string Value)
		{
			return Convert.ToBase64String(StrictUtf8.GetBytes(Value));
		}

		private static bool TryB64(string Encoded, out string Value)
		{
			Value = null;
			try
			{
				byte[] bytes = Convert.FromBase64String(Encoded);
				string decoded = StrictUtf8.GetString(bytes);
				if (B64(decoded) != Encoded) return false;
				Value = decoded;
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static string Hash(string Value)
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] bytes = sha.ComputeHash(StrictUtf8.GetBytes(Value));
				StringBuilder result = new StringBuilder(64);
				for (int i = 0; i < bytes.Length; i++)
				{
					result.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
				}
				return result.ToString();
			}
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
