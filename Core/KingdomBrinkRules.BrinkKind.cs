namespace ThousandAndFirst
{
	/// <summary>
	/// Which irreversible thing is standing one window away from happening. The value is stable
	/// forever: it is written into settler properties and into the realm's own state slot, so a
	/// renumbering would read one city's brink as another's.
	/// </summary>
	public enum BrinkKind
	{
		/// <summary>A settler with nowhere in the settlement they would live. Ends in
		/// <c>KingdomGrowth.Emigrate</c> under <c>KingdomLodgingRules.DepartureCause</c>.</summary>
		Roof = 1,

		/// <summary>A settler the road has already turned &mdash; osmosis, the shared table or a
		/// shrine &mdash; standing one window short of holding somebody else's creed. Ends in
		/// <c>KingdomConversion.Convert</c>.</summary>
		Creed = 2,

		/// <summary>A realm whose two cities have quarrelled all the way to the breaking point.
		/// Ends in <c>KingdomCreed.Secede</c>.</summary>
		City = 3
	}

}
