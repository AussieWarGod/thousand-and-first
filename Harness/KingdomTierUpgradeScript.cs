using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The exact sealed script the ordinary tier-upgrade seam answers to, kept engine-free so both
	/// test projects can compare it against the persona without a running game. Behaviour-coverage
	/// row 8 (ordinary, non-heart building tier replacement: tent -&gt; tentrow).
	/// <para>
	/// THE LEGS ARE COUNTED IN SETTLEMENT PASSES. Receipt-bearing work advances only inside the
	/// daily attended pass (<c>Simulation/City/KingdomSemanticClockRules.cs</c>,
	/// <c>CadenceTicks = KingdomRules.TicksPerDay</c> = 1200), so every 1200-turn leg holds
	/// exactly one pass. A freshly staked or projected raising carries no crew witness until
	/// <c>KingdomConstructionPresence.Assign</c> stamps it inside a pass, so its first interval is
	/// priced at zero (<c>Growth/KingdomPlot2.26b.LabourWindow.cs</c>,
	/// <c>Growth/KingdomScaffold.LabourWindow.cs</c>; observed natively in
	/// teardown-native-check and camp-heart-native-checks). Leg one is three passes: the zero
	/// interval, the priced 900-tick tent, and a spare. Leg two is two passes: the begin and the
	/// improvement's own zero-priced first interval, so the paid job is still Working when it is
	/// read. Leg three is three passes: the priced 900-tick improvement and two spares.
	/// </para>
	/// </summary>
	internal static class KingdomTierUpgradeScript
	{
		internal const string Setup = "tier-upgrade-setup";
		internal const string Check = "tier-upgrade-check";
		internal const string Short = "tier-upgrade-short";
		private static readonly string[] Steps = {
			"stagedigest", Setup, "advance 3600", Check, Short, "advance 2400", Check,
			"advance 3600", Check, "stagedigest" };

		internal static bool Matches(IList<string> Script)
		{
			if (Script == null || Script.Count != Steps.Length) return false;
			for (int i = 0; i < Steps.Length; i++) if (Script[i] != Steps[i]) return false;
			return true;
		}
	}
}
