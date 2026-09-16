namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Engine-free decision behind KingdomScenarioAutoRunner.Death.cs, in the repo's own
	/// value-testable shape (KingdomQuickstartLifecycleOpenWait), so the one case a source pin
	/// cannot catch is proved by value: SUCCESSION RE-BODIES THE PLAYER INSIDE AfterDieEvent
	/// (Experience/KingdomSuccession.DeathExecution.cs -> SetPlayerBodyAndRebindAll ->
	/// KingdomSuccessionBodyAndState.TrySetBodyAndRebindPlayerSystems, which re-registers every
	/// player system on the heir), so by the time the runner's handler sees the founder's death
	/// the founder is no longer IsPlayer() and no longer the latest registered body. The death
	/// of ANY body this run ever registered still counts.
	/// </summary>
	internal static class KingdomScenarioDeathRules
	{
		/// <summary>True when a scripted run must stop on this death: a script is running and the
		/// dying body is the player now OR was a body this runner registered at any point.</summary>
		internal static bool ShouldRecordDeath(bool DyingIsPlayer, bool DyingWasRegistered,
			bool ScriptRunning)
		{
			if (!ScriptRunning) return false;
			return DyingIsPlayer || DyingWasRegistered;
		}
	}
}
