using System;

namespace ThousandAndFirst.Simulation.Kernel
{
	internal static class KingdomRecruitmentRules
	{
		internal const ulong MaxBaseWeight = 1000000UL;
		internal const string NoEligibleFailure = "no eligible settlers are on peaceful terms with both founder and settlement";

		/// <summary>Native hostility is supplied by the adapter. Each non-hostile channel
		/// contributes a bounded 51..300 factor; zero regard contributes 100.</summary>
		internal static bool TryWeight(ulong BaseWeight, int Personal, int Civic,
			bool Hostile, out ulong Weight)
		{
			Weight = 0UL;
			if (BaseWeight == 0UL || BaseWeight > MaxBaseWeight) return false;
			if (Hostile) return true;
			Weight = BaseWeight * Factor(Personal) * Factor(Civic);
			return true;
		}

		private static ulong Factor(int Regard)
		{
			return (ulong)(100 + Math.Max(-249, Math.Min(1000, Regard)) / 5);
		}
	}
}
