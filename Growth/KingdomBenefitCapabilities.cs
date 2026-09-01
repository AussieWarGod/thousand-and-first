using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Open semantic capabilities carried by live physical providers. Catalogue
	/// <c>Provides</c> values only accept these tags; they never manufacture a capability.</summary>
	public static class KingdomBenefitCapabilities
	{
		public const string Cooking = "taf:cooking";
		public const string Shrine = "taf:shrine";
		public const string Education = "taf:education";
		public const string Inquiry = "taf:inquiry";
		public const string Market = "taf:market";

		public static readonly string[] BuiltIn = new string[5]
		{
			Cooking, Shrine, Education, Inquiry, Market
		};

		/// <summary>Whether one immutable designation reading credits this exact capability.
		/// Unknown namespaced capabilities remain usable by extension consumers.</summary>
		public static bool Has(KingdomBenefitReading Reading, string Capability)
		{
			return Contains(Reading?.Provides, Capability);
		}

		/// <summary>Whether the designation contract permits a capability. This is eligibility,
		/// never evidence that any physical provider currently supplies it.</summary>
		public static bool Accepts(KingdomBenefitReading Reading, string Capability)
		{
			return Contains(Reading?.Designation?.AcceptedTags, Capability);
		}

		/// <summary>Counts capable designations, not furniture pieces. Caps and allocation have
		/// already folded every provider in one designation into one effective tag.</summary>
		public static int Count(IReadOnlyList<KingdomBenefitReading> Readings,
			string Capability)
		{
			int count = 0;
			for (int i = 0; Readings != null && i < Readings.Count; i++)
				if (Has(Readings[i], Capability) && count < int.MaxValue) count++;
			return count;
		}

		private static string Fold(string Value)
		{
			return (Value ?? "").Trim().ToLowerInvariant();
		}

		private static bool Contains(IReadOnlyList<string> Values, string Value)
		{
			string sought = Fold(Value);
			if (sought.Length == 0 || Values == null) return false;
			for (int i = 0; i < Values.Count; i++)
				if (string.Equals(Fold(Values[i]), sought,
					StringComparison.Ordinal)) return true;
			return false;
		}
	}
}
