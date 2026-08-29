namespace ThousandAndFirst
{
	public enum KingdomLoadReconciliationMode : byte
	{
		None = 0,
		CommittedCapacityOnly = 1,
		Full = 2
	}

	/// <summary>Pure load routing. Master-off and realm-retirement may reconcile exact
	/// committed capacity, but never observe options or create Foundation/Polity work.</summary>
	public static class KingdomLoadReconciliationRules
	{
		public static KingdomLoadReconciliationMode Select(bool Founded,
			bool CentralNewWorkAllowed)
		{
			if (!Founded) return KingdomLoadReconciliationMode.None;
			return CentralNewWorkAllowed ? KingdomLoadReconciliationMode.Full
				: KingdomLoadReconciliationMode.CommittedCapacityOnly;
		}
	}
}
