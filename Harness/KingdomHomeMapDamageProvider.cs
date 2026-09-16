using System;
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomHomeMapDamageProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "home-map-damage-native";
		private static readonly string[] Script = { "quickstart-lifecycle marsh yes", "lifecycle-open",
			"advance 8400", "home-map-native", "lifecycle-grown", Verb, "stagedigest" };
		private static bool Attempted;
		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;
		public IEnumerable<string> ScenarioVerbs => new[] { Verb };
		internal static bool IsExact(IList<string> script)
		{
			if (script == null || script.Count != Script.Length) return false;
			for (int i = 0; i < Script.Length; i++) if (script[i] != Script[i]) return false;
			return true;
		}
		public string RunScenarioVerb(string name, string argument, out bool ok)
		{
			ok = false;
			try
			{
				KingdomScenarioSaveFiles.Require(name == Verb && string.IsNullOrEmpty(argument) && !Attempted
					&& KingdomScenarioScript.TryRead(out IList<string> script, out _) && IsExact(script)
					&& The.Game != null && !KingdomSurvey.HasBoundPass && !KingdomScenarioAdvance.Pending,
					"home damage probe context differs");
				Attempted = true;
				string evidence = KingdomHomeMapAbsentWitness.DamageProbe(The.Game);
				ok = true; return "native-home-damage cases=1 passed=1 failed=0; " + evidence;
			}
			catch (Exception error)
			{
				KingdomLog.Log("home damage retained failure: " + error);
				return "native-home-damage cases=1 passed=0 failed=1; " + KingdomScenarioRules.Bounded(error.Message);
			}
		}
	}
}
