using System;

namespace ThousandAndFirst
{
	/// <summary>Engine-free frozen-snapshot law for a size-growing ordinary architecture edge.</summary>
	public static class KingdomArchitectureExpansionRules
	{
		/// <summary>
		/// Size bindings are intentionally allowed to differ: one binding owns one exact size.
		/// Plan and typed-lot identity carry lineage across those bindings; target binding identity
		/// is then frozen in the successor snapshot.
		/// </summary>
		public static bool SameFrozenLineage(ArchitectureLayoutSnapshot Before,
			ArchitectureLayoutSnapshot After)
		{
			return Before != null && After != null
				&& string.Equals(Before.PlanKey, After.PlanKey, StringComparison.Ordinal)
				&& SameType(Before.LotType, After.LotType)
				&& (int)After.LotSize == (int)Before.LotSize + 1
				&& Before.Facing == After.Facing
				&& KingdomArchitectureTransitionRules.AllowsLotExpansion(
					After.IncomingTransitionMode);
		}

		private static bool SameType(string Left, string Right)
		{
			return !string.IsNullOrWhiteSpace(Left) && !string.IsNullOrWhiteSpace(Right)
				&& string.Equals(Left.Trim(), Right.Trim(), StringComparison.OrdinalIgnoreCase);
		}
	}
}
