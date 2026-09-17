using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The exact sealed script the ordinary tier-upgrade seam answers to, kept engine-free so both
	/// test projects can compare it against the persona without a running game. Behaviour-coverage
	/// row 8 (ordinary, non-heart building tier replacement: tent -&gt; tentrow).
	/// </summary>
	internal static class KingdomTierUpgradeScript
	{
		internal const string Setup = "tier-upgrade-setup";
		internal const string Check = "tier-upgrade-check";
		internal const string Short = "tier-upgrade-short";
		private static readonly string[] Steps = {
			"stagedigest", Setup, "advance 1200", Check, Short, "advance 1200", Check,
			"advance 1200", Check, "stagedigest" };

		internal static bool Matches(IList<string> Script)
		{
			if (Script == null || Script.Count != Steps.Length) return false;
			for (int i = 0; i < Steps.Length; i++) if (Script[i] != Steps[i]) return false;
			return true;
		}
	}
}
