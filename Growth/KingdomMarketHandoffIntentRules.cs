namespace ThousandAndFirst
{
	public enum KingdomMarketHandoffIntentState
	{
		None = 0,
		FirstOnly = 1,
		SecondOnly = 2,
		Paired = 3,
		Divergent = 4
	}

	/// <summary>Pure crash-cut law for two independently persisted handoff fields.</summary>
	public static class KingdomMarketHandoffIntentRules
	{
		public static KingdomMarketHandoffIntentState Classify(string First,
			string ExpectedFirst, string Second, string ExpectedSecond)
		{
			bool firstEmpty = string.IsNullOrEmpty(First);
			bool secondEmpty = string.IsNullOrEmpty(Second);
			if ((!firstEmpty && First != ExpectedFirst)
				|| (!secondEmpty && Second != ExpectedSecond)
				|| string.IsNullOrEmpty(ExpectedFirst) || string.IsNullOrEmpty(ExpectedSecond))
				return KingdomMarketHandoffIntentState.Divergent;
			if (firstEmpty && secondEmpty) return KingdomMarketHandoffIntentState.None;
			if (!firstEmpty && secondEmpty) return KingdomMarketHandoffIntentState.FirstOnly;
			if (firstEmpty) return KingdomMarketHandoffIntentState.SecondOnly;
			return KingdomMarketHandoffIntentState.Paired;
		}

		public static bool ExactOrRecoverable(string First, string ExpectedFirst,
			string Second, string ExpectedSecond)
		{
			return Classify(First, ExpectedFirst, Second, ExpectedSecond)
				!= KingdomMarketHandoffIntentState.Divergent;
		}
	}
}
