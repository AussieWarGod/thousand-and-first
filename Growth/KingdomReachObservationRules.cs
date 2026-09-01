using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Pure fixed-shape payload and source-evidence rules for durable Reach memory.</summary>
	public static class KingdomReachObservationRules
	{
		public const string Purpose = "taf.reach";
		public const string SourceRevision = "taf.reach.zone/v2";
		public const string LegacySourceRevision = "taf.reach.zone/v1";
		public const int KindCount = 6;
		public const int LegacyKindCount = 5;
		public const int MaxAuthorityRows = 512;
		public const int MaxAuthorityRowChars = 131072;
		private const string PayloadPrefix = "rp2";
		private const string LegacyPayloadPrefix = "rp1";
		private static readonly string[] Kinds =
			new string[] { "craft", "spirit", "learning", "order", "luxury", "wealth" };

		public static string KindAt(int Index)
		{
			return Index >= 0 && Index < Kinds.Length ? Kinds[Index] : null;
		}

		public static bool SameKindOrder(IList<string> Values)
		{
			if (Values == null || Values.Count != KindCount) return false;
			for (int i = 0; i < KindCount; i++)
				if (!string.Equals(Values[i], Kinds[i], StringComparison.Ordinal)) return false;
			return true;
		}

		public static bool TryAuthorityDigest(IList<string> Rows, out string Digest)
		{
			Digest = null;
			if (Rows == null || Rows.Count > MaxAuthorityRows) return false;
			List<string> sorted = new List<string>(Rows.Count);
			for (int i = 0; i < Rows.Count; i++)
			{
				string row = Rows[i];
				if (string.IsNullOrEmpty(row) || row.Length > MaxAuthorityRowChars) return false;
				sorted.Add(row);
			}
			sorted.Sort(StringComparer.Ordinal);
			for (int i = 1; i < sorted.Count; i++)
				if (string.Equals(sorted[i - 1], sorted[i], StringComparison.Ordinal)) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream,
					new UTF8Encoding(false, true)))
				{
					KingdomZoneObservationRules.Write(writer, "taf.reach.authority/v1");
					writer.Write(sorted.Count);
					for (int i = 0; i < sorted.Count; i++)
						KingdomZoneObservationRules.Write(writer, sorted[i]);
					writer.Flush(); Digest = KingdomZoneObservationRules.Digest(
						stream.ToArray(), stream.Length); return Digest != null;
				}
			}
			catch { Digest = null; return false; }
		}

		public static bool TryEncodePayload(int[] City, int[] Realm,
			string AuthorityDigest, out string Payload)
		{
			Payload = null;
			if (!Amounts(City) || !Amounts(Realm)
				|| !KingdomZoneObservationRules.LowerHexDigest(AuthorityDigest)) return false;
			Payload = PayloadPrefix + "|" + AuthorityDigest + "|" + Join(City) + "|" + Join(Realm);
			return true;
		}

		public static bool TryDecodePayload(string Payload, out int[] City,
			out int[] Realm, out string AuthorityDigest)
		{
			return TryDecodePayload(Payload, out City, out Realm, out AuthorityDigest,
				out bool _);
		}

		/// <summary>Decodes current six-kind payloads and exact five-kind prerelease payloads.
		/// Legacy values keep their original five positions and migrate with zero wealth; they are
		/// never reinterpreted as a six-kind row.</summary>
		public static bool TryDecodePayload(string Payload, out int[] City,
			out int[] Realm, out string AuthorityDigest, out bool Legacy)
		{
			City = null; Realm = null; AuthorityDigest = null; Legacy = false;
			if (string.IsNullOrEmpty(Payload) || Payload.Length > 512) return false;
			string[] fields = Payload.Split('|');
			if (fields.Length != 4
				|| !KingdomZoneObservationRules.LowerHexDigest(fields[1])) return false;
			int count;
			if (fields[0] == PayloadPrefix) count = KindCount;
			else if (fields[0] == LegacyPayloadPrefix)
			{
				count = LegacyKindCount; Legacy = true;
			}
			else return false;
			if (!TryAmounts(fields[2], count, out int[] city)
				|| !TryAmounts(fields[3], count, out int[] realm)
				|| !string.Equals(fields[0] + "|" + fields[1] + "|" + Join(city)
					+ "|" + Join(realm), Payload, StringComparison.Ordinal))
			{
				Legacy = false; return false;
			}
			AuthorityDigest = fields[1];
			if (!Legacy)
			{
				City = city; Realm = realm; return true;
			}
			City = ExpandLegacy(city); Realm = ExpandLegacy(realm); return true;
		}

		/// <summary>Decodes only when receipt source revision and payload generation agree.</summary>
		public static bool TryDecodeVersionedPayload(string Revision, string Payload,
			out int[] City, out int[] Realm, out string AuthorityDigest)
		{
			if (!TryDecodePayload(Payload, out City, out Realm, out AuthorityDigest,
				out bool legacy)) return false;
			bool exact = string.Equals(Revision,
				legacy ? LegacySourceRevision : SourceRevision, StringComparison.Ordinal);
			if (exact) return true;
			City = null; Realm = null; AuthorityDigest = null; return false;
		}

		public static int Amount(string Payload, string Kind, bool RealmBand)
		{
			if (!TryDecodePayload(Payload, out int[] city, out int[] realm, out string _)) return 0;
			int index = IndexOf(Kind);
			return index < 0 ? 0 : (RealmBand ? realm[index] : city[index]);
		}

		private static int IndexOf(string Kind)
		{
			for (int i = 0; i < KindCount; i++)
				if (string.Equals(Kind, Kinds[i], StringComparison.Ordinal)) return i;
			return -1;
		}

		private static bool Amounts(int[] Values)
		{
			if (Values == null || Values.Length != KindCount) return false;
			for (int i = 0; i < Values.Length; i++) if (Values[i] < 0) return false;
			return true;
		}

		private static bool TryAmounts(string Text, int Count, out int[] Values)
		{
			Values = null; string[] fields = (Text ?? "").Split(',');
			if (fields.Length != Count) return false;
			int[] parsed = new int[Count];
			for (int i = 0; i < fields.Length; i++)
			{
				if (!int.TryParse(fields[i], NumberStyles.None, CultureInfo.InvariantCulture,
					out parsed[i]) || parsed[i] < 0
					|| parsed[i].ToString(CultureInfo.InvariantCulture) != fields[i]) return false;
			}
			Values = parsed; return true;
		}

		private static int[] ExpandLegacy(int[] Values)
		{
			int[] expanded = new int[KindCount];
			Array.Copy(Values, expanded, LegacyKindCount);
			return expanded;
		}

		private static string Join(int[] Values)
		{
			StringBuilder result = new StringBuilder();
			for (int i = 0; i < Values.Length; i++)
			{
				if (i > 0) result.Append(',');
				result.Append(Values[i].ToString(CultureInfo.InvariantCulture));
			}
			return result.ToString();
		}
	}
}
