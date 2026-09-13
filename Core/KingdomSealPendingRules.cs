namespace ThousandAndFirst
{
	internal static class KingdomSealPendingRules
	{
		internal static bool RoadlessEntrance(bool PublicEntranceFault, bool NoEntry, int StreetCount)
			=> PublicEntranceFault && NoEntry && StreetCount == 0;

		/// <summary>
		/// A sealed work root that is absent because its own improvement has not finished.
		///
		/// <para>The handover destroys the predecessor before its receipt completes, so between
		/// those two points -- and for as long as a refused step leaves the receipt quarantined or
		/// inspection-required -- the row names an identity nothing carries. That is a settlement
		/// under inspection, which the settlement's own records already say, not a malformed seal;
		/// reporting it as malformed would raise the same fault every day for as long as the
		/// inspection stood. It stays a REFUSAL either way: nothing is sealed while it holds.
		/// </para>
		/// </summary>
		internal static bool ClimbUnderInspection(bool RootAbsent, bool ImprovementUnfinished)
			=> RootAbsent && ImprovementUnfinished;
	}
}
