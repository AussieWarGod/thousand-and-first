using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCityRules
	{
		/// <summary>
		/// Where a container sorts in the drain.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.9 wants the order to be a STORED FACT, so the number is
		/// the ordinal stamped on the container the first pass the city counted it. A container
		/// carrying no ordinal has not been counted yet, and it sorts <b>last</b> rather than
		/// first: an unstamped vessel has no claim to being the oldest, and sorting it first would
		/// let a container the city has never seen jump the whole queue.
		/// </para>
		/// </summary>
		internal static int DrainOrdinal(int stamped)
		{
			return (stamped > 0) ? stamped : int.MaxValue;
		}

		/// <summary>The stable code for a district key, or <see cref="NoDistrict"/>. The registry is
		/// data-driven under the extensibility law, so the row carries a code and the name stays in
		/// one place.</summary>
		internal static int DistrictCode(string district)
		{
			if (string.IsNullOrEmpty(district))
			{
				return NoDistrict;
			}
			for (int i = 0; i < KingdomRules.Districts.Length; i++)
			{
				if (string.Equals(KingdomRules.Districts[i], district, StringComparison.Ordinal))
				{
					return i + 1;
				}
			}
			return NoDistrict;
		}

		/// <summary>The district key a code names, or null. The inverse of
		/// <see cref="DistrictCode"/> over every representable input.</summary>
		internal static string DistrictKey(int code)
		{
			int index = code - 1;
			if (index < 0 || index >= KingdomRules.Districts.Length)
			{
				return null;
			}
			return KingdomRules.Districts[index];
		}

		private static ulong Mint(ulong basis, int worldSeed, string realmName, long foundedTick)
		{
			ulong hash = basis;
			hash = Fold(hash, (ulong)(uint)worldSeed);
			for (int i = 0; i < realmName.Length; i++)
			{
				hash = Fold(hash, realmName[i]);
			}
			hash = Fold(hash, (ulong)foundedTick);
			return hash;
		}

		private static ulong Fold(ulong hash, ulong value)
		{
			for (int shift = 0; shift < 64; shift += 8)
			{
				hash ^= (value >> shift) & 0xFFUL;
				hash *= 0x100000001B3UL;
			}
			return hash;
		}

		private static int Min(int left, int right)
		{
			return (left < right) ? left : right;
		}
	}
}
