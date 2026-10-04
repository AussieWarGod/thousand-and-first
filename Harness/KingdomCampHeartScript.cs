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
			if (!RequireSave && KingdomCampHeartChainScript.SealedTargetRung(Script) > 0) return true;
			bool save = Script.Count == Steps.Length + 1 && Script[Steps.Length] == SaveVerb;
			if (!save && (RequireSave || Script.Count != Steps.Length)) return false;
			for (int i = 0; i < Steps.Length; i++) if (Script[i] != Steps[i]) return false;
			return true;
		}
	}
}
