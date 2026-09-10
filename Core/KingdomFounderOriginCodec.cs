using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// The strict, bounded, versioned wire for one founder's accounting obligation, and the only
	/// way one is ever read or written.
	/// <para>
	/// Nothing about it is positional on an engine type: it is one namespaced string property, so
	/// no shipped serialized part gains a field. A wire this codec did not mint &#8212; a wrong
	/// tag, a wrong field count, an out-of-range number, a bad digest, or text this settlement
	/// never wrote &#8212; does not decode, and the caller treats an undecodable reading exactly as
	/// it treats a foreign one.
	/// </para>
	/// </summary>
	internal static class KingdomFounderOriginCodec
	{
		/// <summary>
		/// The one object property this accounting owns. Namespaced and versioned, so an older or
		/// newer shape can never be mistaken for this one.
		/// </summary>
		internal const string ReceiptProperty = "r_TAF_FounderOriginAccount_v1";

		private const string Tag = "fo1";
		private const int FieldCount = 7;
		private const int MaximumWireLength = 2048;
		private const int MaximumFieldLength = 512;

		internal static string Encode(KingdomFounderOriginReceipt Receipt)
		{
			if (!Valid(Receipt)) return null;
			try { return Framed(Receipt); }
			catch (EncoderFallbackException) { return null; }
			catch (ArgumentException) { return null; }
		}

		private static string Framed(KingdomFounderOriginReceipt Receipt)
		{
			string body = Tag + "|" + B64(Receipt.BodyId) + "|" + B64(Receipt.Profile)
				+ "|" + B64(Receipt.CityId)
				+ "|" + Receipt.Before.ToString(CultureInfo.InvariantCulture)
				+ "|" + ((int)Receipt.State).ToString(CultureInfo.InvariantCulture);
			string wire = body + "|" + Digest(body);
			return wire.Length > MaximumWireLength ? null : wire;
		}

		internal static bool TryDecode(string Wire, out KingdomFounderOriginReceipt Receipt)
		{
			Receipt = null;
			if (string.IsNullOrEmpty(Wire) || Wire.Length > MaximumWireLength) return false;
			string[] fields = Wire.Split('|');
			if (fields.Length != FieldCount
				|| !string.Equals(fields[0], Tag, StringComparison.Ordinal)) return false;
			string body = string.Join("|", fields, 0, FieldCount - 1);
			KingdomFounderOriginReceipt decoded;
			try
			{
				// The digest is taken INSIDE the refusal boundary. Hashing is itself an encoding
				// step, and a raw wire is arbitrary text: a lone surrogate anywhere in the body
				// makes the strict encoder throw, and a decoder that throws on a malformed reading
				// is a decoder that cannot be asked about one. Every reading refuses; none raises.
				if (!string.Equals(Digest(body), fields[FieldCount - 1],
					StringComparison.Ordinal)) return false;
				decoded = new KingdomFounderOriginReceipt(Text(fields[1]), Text(fields[2]),
					Text(fields[3]),
					int.Parse(fields[4], NumberStyles.AllowLeadingSign,
						CultureInfo.InvariantCulture),
					(KingdomFounderOriginState)int.Parse(fields[5], NumberStyles.Integer,
						CultureInfo.InvariantCulture));
			}
			catch
			{
				return false;
			}
			// Round-trip guard: a wire whose own encoder disagrees with it is not this codec's.
			if (!Valid(decoded) || !string.Equals(Encode(decoded), Wire, StringComparison.Ordinal))
				return false;
			Receipt = decoded;
			return true;
		}

		internal static bool Valid(KingdomFounderOriginReceipt Receipt)
		{
			return Receipt != null && Identity(Receipt.BodyId) && Identity(Receipt.Profile)
				&& Identity(Receipt.CityId) && Receipt.Before >= 0
				&& (Receipt.State == KingdomFounderOriginState.Prepared
					|| Receipt.State == KingdomFounderOriginState.Completed
					|| Receipt.State == KingdomFounderOriginState.Quarantined);
		}

		/// <summary>
		/// A bounded, unambiguously encodable identity. Two separate rules, for two separate
		/// reasons, and it is worth keeping them apart.
		/// <para>
		/// An unpaired surrogate is not encodable at all. UTF-8 has no representation for one, so
		/// the replacement-fallback encoder substitutes U+FFFD and two DIFFERENT malformed
		/// identities become the SAME bytes and the same digest. An identity that cannot be told
		/// apart from another one is not an identity, so it is refused before it is written down.
		/// </para>
		/// <para>
		/// A control character is a different matter: it round-trips through UTF-8 perfectly well
		/// and collides with nothing. It is refused by POLICY, not by encoding &#8212; a tab, a
		/// newline or a NUL inside a field of a pipe-delimited, line-oriented wire is a value no
		/// caller should be able to smuggle past a reader, and an object id that carries one is not
		/// an id this settlement minted.
		/// </para>
		/// </summary>
		private static bool Identity(string Value)
		{
			if (string.IsNullOrWhiteSpace(Value) || Value.Length > MaximumFieldLength
				|| Value.IndexOf('|') >= 0) return false;
			for (int i = 0; i < Value.Length; i++)
			{
				char letter = Value[i];
				if (char.IsControl(letter)) return false;
				if (char.IsHighSurrogate(letter))
				{
					if (i + 1 >= Value.Length || !char.IsLowSurrogate(Value[i + 1])) return false;
					i++;
					continue;
				}
				if (char.IsLowSurrogate(letter)) return false;
			}
			return true;
		}

		/// <summary>Throws rather than substituting U+FFFD, in both directions.</summary>
		private static readonly UTF8Encoding Strict = new UTF8Encoding(false, true);

		private static string B64(string Value)
		{
			return Convert.ToBase64String(Strict.GetBytes(Value ?? ""));
		}

		private static string Text(string Value)
		{
			return Strict.GetString(Convert.FromBase64String(Value));
		}

		private static string Digest(string Value)
		{
			byte[] digest;
			using (SHA256 sha = SHA256.Create())
				digest = sha.ComputeHash(Strict.GetBytes(Value ?? ""));
			StringBuilder text = new StringBuilder(64);
			for (int i = 0; i < digest.Length; i++)
				text.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
			return text.ToString();
		}
	}
}
