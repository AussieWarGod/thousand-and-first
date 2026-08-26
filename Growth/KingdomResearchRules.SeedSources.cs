using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomResearchRules
	{
		// --- Seeds (Addendum 18; verdict 9) ----------------------------------------------------

		/// <summary>What one seed is worth: a quarter of the walk, and only ever the first quarter.</summary>
		public const int SeedPercent = 25;

		/// <summary>What every seed a node will ever take is worth together. A founder who has
		/// shared water with all of Qud still walks half of every road.</summary>
		public const int MaxSeedPercent = 50;

		/// <summary>Bound on durable per-city source receipts. The shipped tree uses fewer than
		/// a dozen; the larger ceiling leaves extensions room while malformed XML cannot grow one
		/// game-state string without limit.</summary>
		internal const int MaxSeedReceiptRows = 256;

		internal const int MaxSeedReceiptNodeLength = 256;

		internal const int MaxSeedReceiptSourceLength = 512;

		internal const int MaxSeedReceiptRowLength =
			MaxSeedReceiptNodeLength + MaxSeedReceiptSourceLength + 32;

		internal const int MaxSeedReceiptEncodedLength = MaxSeedReceiptRows *
			MaxSeedReceiptRowLength;

		internal const int MaxSeedReceiptEncodedUtf8Bytes = MaxSeedReceiptEncodedLength * 4;

		/// <summary>Distinct sources beyond this cannot raise a node's seed floor and therefore do
		/// not need durable receipt rows.</summary>
		internal const int MaxSeedSourcesPerNode = MaxSeedPercent / SeedPercent;

		/// <summary>Bound on the founder's permanent water-ritual ledger. Vanilla currently has far
		/// fewer ritual factions; the headroom is for extensions, not unbounded game state.</summary>
		internal const int MaxFounderRites = 256;

		internal const int MaxFounderRiteNameLength = 256;

		internal const int MaxFounderRiteEncodedLength = MaxFounderRites *
			(MaxFounderRiteNameLength + 6);

		internal const string SeedReceiptKind = "researchseed";

		/// <summary>
		/// Whether a vanilla water-ritual start is the covenant moment that may put a rite source
		/// on the founder's permanent ledger. Vanilla has no completion event: its start event's
		/// <c>Initial</c> bit is the one first-share fact it publishes. A later visit to the same
		/// ritual must not become a second source, and a malformed faction name must not poison the
		/// ledger it will be stored in.
		/// </summary>
		/// <param name="Initial">Vanilla's first-ever-share bit.</param>
		/// <param name="Faction">The ritual record's faction.</param>
		internal static bool MayRememberRite(bool Initial, string Faction)
		{
			if (!Initial || Faction == null || Faction.Length > MaxFounderRiteNameLength)
			{
				return false;
			}
			return IsCanonicalFounderRite(KingdomZoningRules.ComposeKey(KindRite, Faction));
		}

		internal static bool IsCanonicalFounderRite(string Key)
		{
			string kind = KingdomZoningRules.KindOf(Key);
			string name = KingdomZoningRules.NameOf(Key);
			string canonical = KingdomZoningRules.ComposeKey(KindRite, name);
			return kind == KindRite && name != null && name.Length <= MaxFounderRiteNameLength &&
				name.IndexOf(KingdomZoningRules.ListSeparator) < 0 &&
				canonical != null && string.Equals(canonical, Fold(Key), StringComparison.Ordinal);
		}

		internal static List<string> CanonicalFounderRites(string Encoded)
		{
			List<string> result = new List<string>();
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaxFounderRiteEncodedLength ||
				Encoding.UTF8.GetByteCount(Encoded) > MaxFounderRiteEncodedLength * 4)
			{
				return result;
			}
			int rows = 1;
			for (int i = 0; i < Encoded.Length; i++)
			{
				if (Encoded[i] == KingdomZoningRules.RosterSeparator && ++rows > MaxFounderRites)
				{
					return new List<string>();
				}
			}
			HashSet<string> seen = new HashSet<string>();
			string[] rowsRead = Encoded.Split(KingdomZoningRules.RosterSeparator);
			for (int i = 0; i < rowsRead.Length; i++)
			{
				string key = Fold(rowsRead[i]);
				if (key != null && seen.Add(key) && IsCanonicalFounderRite(key)
					&& result.Count < MaxFounderRites)
				{
					result.Add(key);
				}
			}
			return result;
		}

		/// <summary>Writes the founder-wide rite ledger under its own receipt bounds. This is not a
		/// keeper roster: sharing its delimiter does not make the roster's smaller heap ceiling its
		/// authority.</summary>
		internal static bool TryEncodeFounderRites(IEnumerable<string> Rites, out string Encoded)
		{
			Encoded = null;
			List<string> canonical = new List<string>();
			HashSet<string> seen = new HashSet<string>();
			if (Rites != null)
			{
				foreach (string raw in Rites)
				{
					string key = Fold(raw);
					if (!IsCanonicalFounderRite(key)) return false;
					if (seen.Add(key))
					{
						if (canonical.Count >= MaxFounderRites) return false;
						canonical.Add(key);
					}
				}
			}
			string value = string.Join(KingdomZoningRules.RosterSeparator.ToString(),
				canonical.ToArray());
			if (value.Length > MaxFounderRiteEncodedLength
				|| Encoding.UTF8.GetByteCount(value) > MaxFounderRiteEncodedLength * 4) return false;
			Encoded = value;
			return true;
		}

	}
}
