using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Pure arithmetic and identity rules for the two directional civic ledgers.</summary>
	public static class KingdomStandingRules
	{
		public const int MaxRelationships = 512;
		public const int FractionScale = 100;
		public const int MaxFactionNameChars = 512;
		public const int MaxFactionNameBytes = 2048;
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static bool EligibleForeignFaction(string factionName, string realmFactionName)
		{
			if (string.IsNullOrWhiteSpace(factionName) || factionName.IndexOf('\0') >= 0 ||
				factionName.Length > MaxFactionNameChars ||
				string.Equals(factionName, "Player", StringComparison.Ordinal) ||
				string.Equals(factionName, realmFactionName, StringComparison.Ordinal) ||
				string.Equals(factionName, "*", StringComparison.Ordinal)) return false;
			try
			{
				return StrictUtf8.GetByteCount(factionName) <= MaxFactionNameBytes;
			}
			catch (EncoderFallbackException)
			{
				return false;
			}
		}

		public static bool ValidRemainder(int remainder)
		{
			return remainder > -FractionScale && remainder < FractionScale;
		}

		/// <summary>Whether one persisted standing/carry pair is the unique quotient and
		/// remainder representation of its scaled total. Carry has the total's sign, zero carry is
		/// omitted from maps, and a saturated endpoint cannot retain outward debt.</summary>
		public static bool CanonicalPair(int standing, int remainder)
		{
			if (!ValidRemainder(remainder) ||
				(standing > 0 && remainder < 0) ||
				(standing < 0 && remainder > 0) ||
				(standing == int.MaxValue && remainder > 0) ||
				(standing == int.MinValue && remainder < 0)) return false;
			long scaled = (long)standing * FractionScale + remainder;
			return scaled / FractionScale == standing &&
				scaled % FractionScale == remainder;
		}

		/// <summary>Validates a whole persisted pair ledger. A missing standing row is zero; a
		/// missing carry row is zero. Explicit zero carry rows are noncanonical.</summary>
		public static bool CanonicalPairs(Dictionary<string, int> standings,
			Dictionary<string, int> remainders)
		{
			if (standings == null || remainders == null) return false;
			foreach (KeyValuePair<string, int> row in standings)
			{
				remainders.TryGetValue(row.Key, out int carry);
				if (!CanonicalPair(row.Value, carry)) return false;
			}
			foreach (KeyValuePair<string, int> row in remainders)
			{
				if (row.Value == 0) return false;
				standings.TryGetValue(row.Key, out int standing);
				if (!CanonicalPair(standing, row.Value)) return false;
			}
			return true;
		}

		/// <summary>Adds whole standing points to one canonical scaled pair. The carry is
		/// retained through every non-clipping history; clipping consumes all outward carry.</summary>
		public static bool TryAdjustPair(int standing, int remainder, int delta,
			out int nextStanding, out int nextRemainder)
		{
			nextStanding = standing;
			nextRemainder = remainder;
			if (!CanonicalPair(standing, remainder)) return false;
			long scaled = (long)standing * FractionScale + remainder +
				(long)delta * FractionScale;
			return TryFromScaledTotal(scaled, out nextStanding, out nextRemainder);
		}

		/// <summary>Folds one personal-reputation event into faction-to-realm regard. The signed
		/// remainder is measured in hundredths of one standing point. Each update canonicalizes
		/// the complete scaled standing, so partitioning or reordering equal weighted deltas cannot
		/// change either persisted component while no intermediate update clips. Clipping consumes
		/// outward debt and deliberately ends that history-level guarantee.</summary>
		public static bool TrySpillover(int standing, int remainder, int reputationBefore,
			int reputationAfter, GrowthStage stage, out int nextStanding,
			out int nextRemainder)
		{
			nextStanding = standing;
			nextRemainder = remainder;
			if (!Enum.IsDefined(typeof(GrowthStage), stage) ||
				!CanonicalPair(standing, remainder))
				return false;
			long reputationDelta = (long)reputationAfter - reputationBefore;
			long scaled = checked((long)standing * FractionScale + remainder
				+ reputationDelta * KingdomRules.SpilloverPercent(stage));
			return TryFromScaledTotal(scaled, out nextStanding, out nextRemainder);
		}

		private static bool TryFromScaledTotal(long scaled, out int standing,
			out int remainder)
		{
			long maximum = (long)int.MaxValue * FractionScale;
			long minimum = (long)int.MinValue * FractionScale;
			if (scaled >= maximum)
			{
				standing = int.MaxValue;
				remainder = 0;
				return true;
			}
			if (scaled <= minimum)
			{
				standing = int.MinValue;
				remainder = 0;
				return true;
			}
			standing = (int)(scaled / FractionScale);
			remainder = (int)(scaled % FractionScale);
			return CanonicalPair(standing, remainder);
		}

		public static int SaturatingAdd(int value, int delta)
		{
			long candidate = (long)value + delta;
			if (candidate > int.MaxValue) return int.MaxValue;
			if (candidate < int.MinValue) return int.MinValue;
			return (int)candidate;
		}

		/// <summary>Maps only the five feeling values written by the pre-directional TAF
		/// founding path back to representative vanilla reputation thresholds. Any other value is
		/// ambiguous engine/mod residue and must not become owned policy.</summary>
		public static bool TryLegacyFeelingPolicy(int feeling, out int policy)
		{
			switch (feeling)
			{
			case -100: policy = -600; return true;
			case -50: policy = -250; return true;
			case 0: policy = 0; return true;
			case 50: policy = 250; return true;
			case 100: policy = 600; return true;
			default: policy = 0; return false;
			}
		}
	}
}
