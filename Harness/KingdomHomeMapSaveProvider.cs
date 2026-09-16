using System;
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomHomeMapSaveProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "home-map-save-witness";
		private static readonly string[] Script = { "quickstart-lifecycle marsh yes", "lifecycle-open",
			"advance 8400", "home-map-native", "lifecycle-grown", Verb, "lifecycle-save", "stagedigest" };
		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;
		public IEnumerable<string> ScenarioVerbs => new[] { Verb };
		internal static bool ClaimsScript() => KingdomScenarioScript.TryRead(out IList<string> script, out _)
			&& script.Contains(Verb);
		internal static bool IsExact(IList<string> script)
		{
			if (script == null || script.Count != Script.Length) return false;
			for (int i = 0; i < Script.Length; i++) if (script[i] != Script[i]) return false;
			return true;
		}
		internal static void RequireScript()
		{
			KingdomScenarioSaveFiles.Require(KingdomScenarioScript.TryRead(out IList<string> script, out _)
				&& IsExact(script), "home-map save script differs");
		}
		public string RunScenarioVerb(string name, string argument, out bool ok)
		{
			ok = false;
			try
			{
				KingdomScenarioSaveFiles.Require(name == Verb && string.IsNullOrEmpty(argument)
					&& The.Game != null && !KingdomSurvey.HasBoundPass && !KingdomScenarioAdvance.Pending,
					"home-map save context differs");
				RequireScript();
				string result = KingdomHomeMapSaveWitness.Record(The.Game);
				ok = true; return result;
			}
			catch (Exception error) { return "native-home-save failed; " + KingdomScenarioRules.Bounded(error.Message); }
		}
	}
}
