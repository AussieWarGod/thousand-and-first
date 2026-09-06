using System;

namespace ThousandAndFirst
{
	/// <summary>Selects the settlement-wire basis version a persisted authority hash was cut
	/// under. It computes nothing itself: the caller supplies a hash delegate, so this file needs
	/// no engine, no SDK and no file system. Pinned provenance is checked exactly once and never
	/// falls back to search; an unresolved slot enumerates the whole accepted set newest-first
	/// with no early exit and selects only on a unique match. The outcome is the entire authority
	/// granted here: nothing is persisted, no receipt is mutated and no hash is ever rewritten.</summary>
	internal static class KingdomRealmHashBasisRules
	{
		/// <summary>Computes the persisted-form authority hash of the archive projected to
		/// <paramref name="Version"/>. False means that version cannot represent the graph, and
		/// leaves <paramref name="Hash"/> unusable; true must yield a canonical hash.</summary>
		internal delegate bool AuthorityHasher(int Version, out string Hash);

		/// <summary>Bounded result of one selection. PinnedMismatch is separate from NoMatch
		/// because a stored basis that fails to reproduce its hash is tamper or corruption and
		/// must never re-enter matching, while NoMatch is the honest outcome of a search that
		/// did run over every accepted version; collapsing them would let a caller retry a
		/// tampered receipt as if it had no provenance at all.</summary>
		internal enum BasisOutcome
		{
			Malformed = 0,
			Selected = 1,
			NoMatch = 2,
			PinnedMismatch = 3,
			MultipleMatches = 4,
			ComputationFailure = 5
		}

		/// <summary>Stored provenance of 0 means no basis was ever recorded for that slot.</summary>
		internal const int Unresolved = 0;

		/// <summary>Bounds of the accepted archive decode set, taken from the codec constants
		/// (KingdomArchivedSettlementCodec.LegacyVersion and .CurrentVersion) so the search can
		/// never drift from the versions the reader actually accepts.</summary>
		internal const int MinVersion = KingdomArchivedSettlementCodec.LegacyVersion;
		internal const int MaxVersion = KingdomArchivedSettlementCodec.CurrentVersion;

		/// <summary>Length of the canonical hash text: SHA-256 rendered as lowercase hex.</summary>
		internal const int CanonicalHashLength = 64;

		/// <summary>The exact order an unresolved selection asks its hasher, newest accepted
		/// version first. A fresh array per call; there is no shared mutable state here.</summary>
		internal static int[] SearchOrder()
		{
			int[] order = new int[MaxVersion - MinVersion + 1];
			for (int index = 0; index < order.Length; index++) order[index] = MaxVersion - index;
			return order;
		}

		/// <summary>Resolves the basis for one persisted hash. <paramref name="Basis"/> is left at
		/// Unresolved on every outcome except Selected.</summary>
		internal static BasisOutcome Select(string ExpectedHash, int StoredBasis,
			AuthorityHasher Hasher, out int Basis, out string Reason)
		{
			Basis = Unresolved;
			Reason = null;
			if (Hasher == null)
			{
				Reason = "no authority hasher was supplied";
				return BasisOutcome.ComputationFailure;
			}
			if (!CanonicalHash(ExpectedHash))
			{
				Reason = "persisted authority hash is not canonical";
				return BasisOutcome.Malformed;
			}
			if (StoredBasis < Unresolved || StoredBasis > MaxVersion)
			{
				Reason = "stored basis is outside the accepted archive versions";
				return BasisOutcome.Malformed;
			}
			if (StoredBasis != Unresolved)
				return Pinned(ExpectedHash, StoredBasis, Hasher, out Basis, out Reason);
			return Search(ExpectedHash, Hasher, out Basis, out Reason);
		}

		/// <summary>One check at the recorded version. An unrepresentable, uncomputable or
		/// disagreeing pinned basis refuses outright; it never widens into a search.</summary>
		private static BasisOutcome Pinned(string ExpectedHash, int Version,
			AuthorityHasher Hasher, out int Basis, out string Reason)
		{
			Basis = Unresolved;
			Reason = null;
			string hash;
			bool represented;
			try
			{
				represented = Hasher(Version, out hash);
			}
			catch (Exception thrown)
			{
				Reason = "pinned basis hash threw " + thrown.GetType().Name;
				return BasisOutcome.ComputationFailure;
			}
			if (!represented)
			{
				Reason = "pinned basis cannot represent this archive";
				return BasisOutcome.ComputationFailure;
			}
			if (!CanonicalHash(hash))
			{
				Reason = "pinned basis produced a hash that is not canonical";
				return BasisOutcome.ComputationFailure;
			}
			if (!string.Equals(hash, ExpectedHash, StringComparison.Ordinal))
			{
				Reason = "pinned basis does not reproduce the persisted hash";
				return BasisOutcome.PinnedMismatch;
			}
			Basis = Version;
			return BasisOutcome.Selected;
		}

		/// <summary>Full descending enumeration of the accepted set. Uniqueness, not the first
		/// hit, is the criterion, so the loop never returns early on a match; an unrepresentable
		/// version is skipped, while a throwing or non-canonical hasher stops it at once rather
		/// than letting the remaining versions guess a basis.</summary>
		private static BasisOutcome Search(string ExpectedHash, AuthorityHasher Hasher,
			out int Basis, out string Reason)
		{
			Basis = Unresolved;
			Reason = null;
			string computationFailure = null;
			int matched = Unresolved;
			int matches = 0;
			for (int version = MaxVersion; version >= MinVersion; version--)
			{
				string hash;
				bool represented;
				try
				{
					represented = Hasher(version, out hash);
				}
				catch (Exception thrown)
				{
					computationFailure = "candidate basis hash threw " + thrown.GetType().Name;
					break;
				}
				if (!represented) continue;
				if (!CanonicalHash(hash))
				{
					computationFailure = "candidate basis produced a hash that is not canonical";
					break;
				}
				if (!string.Equals(hash, ExpectedHash, StringComparison.Ordinal)) continue;
				matches++;
				matched = version;
			}
			if (computationFailure != null)
			{
				Reason = computationFailure;
				return BasisOutcome.ComputationFailure;
			}
			if (matches == 0)
			{
				Reason = "no accepted archive version reproduces the persisted hash";
				return BasisOutcome.NoMatch;
			}
			if (matches > 1)
			{
				Reason = "several accepted archive versions reproduce the persisted hash";
				return BasisOutcome.MultipleMatches;
			}
			Basis = matched;
			return BasisOutcome.Selected;
		}

		/// <summary>Canonical persisted form: exactly 64 lowercase hexadecimal digits, matching
		/// the digest text every authority hash writes.</summary>
		private static bool CanonicalHash(string Value)
		{
			if (Value == null || Value.Length != CanonicalHashLength) return false;
			for (int index = 0; index < Value.Length; index++)
			{
				char digit = Value[index];
				if ((digit < '0' || digit > '9') && (digit < 'a' || digit > 'f')) return false;
			}
			return true;
		}
	}
}
