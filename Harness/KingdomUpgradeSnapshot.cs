namespace ThousandAndFirst.Harness
{
	// Observation only. These bytes never become game-state authority.
	internal sealed class KingdomUpgradeSnapshot
	{
		internal readonly string GameId, Case, OldPin;
		internal readonly long Turns, TimeTicks, ActionTicks, PlayerActionTicks;
		internal readonly string Inheritance, Transition, Legacy;

		internal KingdomUpgradeSnapshot(string gameId, string scenario, string oldPin,
			long turns, long timeTicks, long actionTicks, long playerActionTicks,
			string inheritance, string transition, string legacy)
		{
			GameId = gameId; Case = scenario; OldPin = oldPin;
			Turns = turns; TimeTicks = timeTicks; ActionTicks = actionTicks; PlayerActionTicks = playerActionTicks;
			Inheritance = inheritance; Transition = transition; Legacy = legacy;
		}
	}
}
