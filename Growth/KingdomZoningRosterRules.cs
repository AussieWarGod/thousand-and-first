using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomZoningRules
	{
		/// <summary>
		/// Builds a roster key. Returns null for anything that could not survive a round trip
		/// through the store &mdash; a blank name, or one carrying the
		/// <see cref="RosterSeparator"/> &mdash; so a hostile blueprint name disables one key
		/// rather than corrupting the whole roster (STANDARDS 9).
		/// </summary>
		/// <param name="Kind">One of <see cref="KindDisk"/>, <see cref="KindMachine"/>,
		/// <see cref="KindOrigin"/>, or any kind a third party invents. A kind this file does not
		/// weigh is worth no craft points but gates perfectly well.</param>
		/// <param name="Name">Blueprint name, origin, or trade. Case is folded away.</param>
		public static string ComposeKey(string Kind, string Name)
		{
			if ((Kind != null && Kind.Length > MaxRosterKeyChars)
				|| (Name != null && Name.Length > MaxRosterKeyChars)) return null;
			string kind = Fold(Kind);
			string name = Fold(Name);
			if (kind == null || name == null)
			{
				return null;
			}
			if (kind.IndexOf(RosterSeparator) >= 0 || name.IndexOf(RosterSeparator) >= 0 || kind.IndexOf(KindSeparator) >= 0)
			{
				return null;
			}
			string key = kind + KindSeparator + name;
			return ValidRosterKey(key) ? key : null;
		}

		/// <summary>The kind half of a roster key, or null when the key carries no kind.</summary>
		public static string KindOf(string Key)
		{
			string key = Fold(Key);
			if (key == null)
			{
				return null;
			}
			int at = key.IndexOf(KindSeparator);
			return (at <= 0) ? null : key.Substring(0, at);
		}

		/// <summary>The name half of a roster key; the whole key when it carries no kind.</summary>
		public static string NameOf(string Key)
		{
			string key = Fold(Key);
			if (key == null)
			{
				return null;
			}
			int at = key.IndexOf(KindSeparator);
			return (at < 0 || at >= key.Length - 1) ? key : key.Substring(at + 1);
		}

		/// <summary>
		/// Reads the settlement's stored roster. Order is preserved (oldest learning first, which
		/// is how the keepers' screen reads), duplicates and blank rows are dropped, and a store
		/// that is null, empty, malformed, or outside any hard bound yields an empty roster rather
		/// than throwing or returning a misleading partial prefix &mdash; an unreadable roster must
		/// never be able to cost a founder a building.
		/// </summary>
		public static List<string> DecodeRoster(string Encoded)
		{
			List<string> roster;
			if (!TryDecodeRoster(Encoded, out roster)) roster = new List<string>();
			return roster;
		}

		/// <summary>Total bounded decoder. False means the aggregate is outside the permanent
		/// knowledge contract; no partial prefix is returned as if it were the city.</summary>
		public static bool TryDecodeRoster(string Encoded, out List<string> Roster)
		{
			Roster = new List<string>();
			if (string.IsNullOrEmpty(Encoded)) return true;
			if (Encoded.Length > MaxRosterEncodedChars
				|| Encoding.UTF8.GetByteCount(Encoded) > MaxRosterEncodedUtf8Bytes) return false;
			int rows = 1;
			for (int i = 0; i < Encoded.Length; i++)
				if (Encoded[i] == RosterSeparator && ++rows > MaxRosterRows) return false;
			string[] parts = Encoded.Split(RosterSeparator);
			HashSet<string> seen = new HashSet<string>();
			for (int i = 0; i < parts.Length; i++)
			{
				if (parts[i] != null && parts[i].Length > MaxRosterKeyChars) return false;
				string key = Fold(parts[i]);
				if (key == null) continue;
				if (!ValidRosterKey(key)) return false;
				if (seen.Add(key)) Roster.Add(key);
			}
			return Roster.Count <= MaxRosterRows;
		}

		/// <summary>Writes a roster back to its stored form. Round-trips
		/// <see cref="DecodeRoster"/> exactly, including the de-duplication.</summary>
		public static string EncodeRoster(IEnumerable<string> Roster)
		{
			string encoded;
			return TryEncodeRoster(Roster, out encoded) ? encoded : null;
		}

		/// <summary>Atomic bounded encoder. It never truncates knowledge to make it fit.</summary>
		public static bool TryEncodeRoster(IEnumerable<string> Roster, out string Encoded)
		{
			Encoded = null;
			List<string> keys = new List<string>();
			HashSet<string> seen = new HashSet<string>();
			int chars = 0;
			int utf8 = 0;
			if (Roster != null)
			{
				foreach (string entry in Roster)
				{
					if (entry != null && entry.Length > MaxRosterKeyChars) return false;
					string key = Fold(entry);
					if (key == null) continue;
					if (!ValidRosterKey(key)) return false;
					if (seen.Add(key))
					{
						if (keys.Count >= MaxRosterRows) return false;
						int separator = keys.Count == 0 ? 0 : 1;
						chars += separator + key.Length;
						utf8 += separator + Encoding.UTF8.GetByteCount(key);
						if (chars > MaxRosterEncodedChars
							|| utf8 > MaxRosterEncodedUtf8Bytes) return false;
						keys.Add(key);
					}
				}
			}
			Encoded = string.Join(RosterSeparator.ToString(), keys.ToArray());
			return true;
		}

		/// <summary>Validates and canonicalizes a persisted aggregate in one bounded pass.</summary>
		public static bool TryCanonicalRoster(string Stored, out string Canonical)
		{
			Canonical = null;
			List<string> rows;
			return TryDecodeRoster(Stored, out rows) && TryEncodeRoster(rows, out Canonical);
		}

		private static bool ValidRosterKey(string Key)
		{
			return !string.IsNullOrEmpty(Key) && Key.Length <= MaxRosterKeyChars
				&& Key.IndexOf(RosterSeparator) < 0
				&& Encoding.UTF8.GetByteCount(Key) <= MaxRosterKeyUtf8Bytes;
		}

		/// <summary>
		/// Whether the roster satisfies one requirement. A requirement carrying a
		/// <see cref="KindSeparator"/> must match a key exactly; one without matches any key of
		/// any kind whose name half is the same, so an author can write
		/// <c>Knowledge="solar condenser"</c> and be satisfied by a disk, a certification, or a
		/// settler who already knew.
		/// </summary>
		public static bool Knows(IEnumerable<string> Roster, string Requirement)
		{
			return Fold(Requirement) == null || SatisfyingKey(Roster, Requirement) != null;
		}

		/// <summary>The canonical concrete roster key that satisfies one authored requirement.
		/// A bare requirement may match several kinds; the oldest stored key wins, exactly as the
		/// roster is read. Receipt code must use this concrete key rather than the authored alias,
		/// or <c>name</c> and <c>rite:name</c> could charge the same knowledge twice.</summary>
		internal static string SatisfyingKey(IEnumerable<string> Roster, string Requirement)
		{
			string required = Fold(Requirement);
			if (required == null || Roster == null)
			{
				return null;
			}
			string[] arms = required.Split(RosterSeparator);
			for (int i = 0; i < arms.Length; i++)
			{
				string concrete = SatisfyingLiteralKey(Roster, arms[i]);
				if (concrete != null)
				{
					return concrete;
				}
			}
			return null;
		}

		private static string SatisfyingLiteralKey(IEnumerable<string> Roster, string Requirement)
		{
			string required = Fold(Requirement);
			if (required == null)
			{
				return null;
			}
			bool qualified = required.IndexOf(KindSeparator) >= 0;
			foreach (string entry in Roster)
			{
				string key = Fold(entry);
				if (key != null && (qualified ? key == required : NameOf(key) == required))
				{
					return key;
				}
			}
			return null;
		}

		/// <summary>Every distinct concrete roster key satisfying a comma-list of authored
		/// alternatives. Aliases that resolve to the same stored key appear once; genuinely distinct
		/// sources retain author order.</summary>
		internal static List<string> SatisfyingKeys(IEnumerable<string> Roster, string Requirements)
		{
			List<string> result = new List<string>();
			foreach (string requirement in Tokens(Requirements))
			{
				string[] arms = requirement.Split(RosterSeparator);
				for (int i = 0; i < arms.Length; i++)
				{
					string concrete = SatisfyingLiteralKey(Roster, arms[i]);
					if (concrete != null && !result.Contains(concrete))
					{
						result.Add(concrete);
					}
				}
			}
			return result;
		}

		/// <summary>Every requirement in a <c>Knowledge</c> list the roster does not satisfy, in
		/// the order the author wrote them. Empty when the settlement knows all of it.</summary>
		public static List<string> MissingKnowledge(IEnumerable<string> Roster, string Required)
		{
			List<string> missing = new List<string>();
			if (!Gated(Required))
			{
				return missing;
			}
			foreach (string token in Tokens(Required))
			{
				if (!Knows(Roster, token) && !missing.Contains(token))
				{
					missing.Add(token);
				}
			}
			return missing;
		}

	}
}
