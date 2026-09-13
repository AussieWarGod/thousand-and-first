using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomCampHeartChainScript
	{
		internal const string Setup = "camp-heart-chain-setup";
		internal const string Check = "camp-heart-chain-check";
		internal const string Supply = "camp-heart-chain-supply";
		private static readonly string[] Steps = {
			"stagedigest", "camp-heart-setup", "advance 1200", "camp-heart-check",
			"advance 3600", "camp-heart-check", "advance 1200", "camp-heart-check", "stagedigest",
			Setup, "advance 1200", Check, "advance 7200", Check,
			Supply, "advance 1200", Check, "advance 6600", "advance 6600", Check, "advance 1200", Check };

		internal static bool Matches(IList<string> Script)
		{
			if (Script == null || Script.Count != Steps.Length) return false;
			for (int i = 0; i < Steps.Length; i++) if (Script[i] != Steps[i]) return false;
			return true;
		}
	}
}
