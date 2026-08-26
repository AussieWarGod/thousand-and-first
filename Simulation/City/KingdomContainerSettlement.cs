namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Applies one exact physical-container touch. <paramref name="applied"/> is authoritative:
	/// a callback may report failure after a measured delta, and that delta still leaves the debt.
	/// </summary>
	internal delegate bool KingdomContainerSettlement(
		int sourceIndex,
		KingdomStockKind kind,
		KingdomUnitDirection direction,
		int offered,
		out int applied);
}
