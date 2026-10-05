using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomCampHeartScript
	{
		internal const string SaveVerb = "camp-heart-save";
		private static readonly string[] Steps = { "stagedigest", "camp-heart-setup", "advance 1200",
			"camp-heart-check", "advance 3600", "camp-heart-check", "advance 1200",
			"camp-heart-check", "stagedigest" };

		internal static bool Matches(IList<string> Script, bool RequireSave = false)
		{
			if (Script == null) return false;
			if (!RequireSave && KingdomCampHeartChainScript.Matches(Script)) return true;
			bool save = Script.Count == Steps.Length + 1 && Script[Steps.Length] == SaveVerb;
			if (!save && (RequireSave || Script.Count != Steps.Length)) return false;
			for (int i = 0; i < Steps.Length; i++) if (Script[i] != Steps[i]) return false;
			return true;
		}

		/// <summary>Every sealed camp-heart form that continues past the last camp check: the
		/// save variant and every paid-chain form. Each depends on the real paid source tent
		/// (#282); the plain 1 -> 2 regression and the rung-3 run still record whatever the real
		/// commission answers, verbatim.</summary>
		internal static bool PaysTent(IList<string> Script)
		{
			return Matches(Script) && Script.Count > Steps.Length;
		}
	}
}
