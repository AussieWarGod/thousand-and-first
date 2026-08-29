using System;
using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputRules
	{
		private const string EnvelopePrefix = "kci1|";

		public static bool TryEncode(KingdomConstructionInputReceipt Receipt,
			out string Encoded, out KingdomConstructionInputFault Fault)
		{
			Encoded = null;
			if (!TryValidate(Receipt, out Fault)) return false;
			try
			{
				byte[] payload = WritePayload(Receipt);
				if (payload.Length > MaxPayloadBytes)
					return Refuse(KingdomConstructionInputFault.Bounds, out Fault);
				string body = Convert.ToBase64String(payload);
				Encoded = EnvelopePrefix + body + "|" + HashBytes(payload);
				if (Encoded.Length > MaxEncodedChars)
				{ Encoded = null; return Refuse(KingdomConstructionInputFault.Bounds, out Fault); }
				Fault = KingdomConstructionInputFault.None;
				return true;
			}
			catch
			{
				Encoded = null;
				return Refuse(KingdomConstructionInputFault.Codec, out Fault);
			}
		}

		public static bool TryDecode(string Encoded, out KingdomConstructionInputReceipt Receipt,
			out KingdomConstructionInputFault Fault)
		{
			Receipt = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaxEncodedChars
				|| !Encoded.StartsWith(EnvelopePrefix, StringComparison.Ordinal))
				return Refuse(KingdomConstructionInputFault.Codec, out Fault);
			int divider = Encoded.IndexOf('|', EnvelopePrefix.Length);
			if (divider < 0 || Encoded.IndexOf('|', divider + 1) >= 0)
				return Refuse(KingdomConstructionInputFault.Codec, out Fault);
			string body = Encoded.Substring(EnvelopePrefix.Length,
				divider - EnvelopePrefix.Length);
			string digest = Encoded.Substring(divider + 1);
			if (!ValidDigest(digest)) return Refuse(KingdomConstructionInputFault.Digest, out Fault);
			try
			{
				byte[] payload = Convert.FromBase64String(body);
				if (payload.Length > MaxPayloadBytes || !FixedEquals(HashBytes(payload), digest))
					return Refuse(KingdomConstructionInputFault.Digest, out Fault);
				if (!TryReadPayload(payload, out Receipt, out Fault)) return false;
				string canonical;
				KingdomConstructionInputFault ignored;
				if (!TryEncode(Receipt, out canonical, out ignored)
					|| !FixedEquals(canonical, Encoded))
				{ Receipt = null; return Refuse(KingdomConstructionInputFault.Codec, out Fault); }
				return true;
			}
			catch
			{
				Receipt = null;
				return Refuse(KingdomConstructionInputFault.Codec, out Fault);
			}
		}

		public static bool TryReceiptDigest(KingdomConstructionInputReceipt Receipt,
			out string Digest, out KingdomConstructionInputFault Fault)
		{
			Digest = null;
			if (!TryValidate(Receipt, out Fault)) return false;
			try { Digest = HashBytes(WritePayload(Receipt)); return true; }
			catch { Digest = null; return Refuse(KingdomConstructionInputFault.Codec, out Fault); }
		}
	}
}
