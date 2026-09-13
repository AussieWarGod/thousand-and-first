using System;
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst.Harness
{
	/// <summary>Ordinary completed construction, real rendered frames, then save/cold load.</summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomHeartSightNativeProvider : IKingdomScenarioVerbProvider
	{
		private static readonly string[] Script = {
			"quickstart-lifecycle marsh yes", "lifecycle-open", "heart-site-open", "advance 8400",
			"lifecycle-grown", "heart-sight-inspect", "yield-frames 3", "heart-sight-check",
			"lifecycle-save", "stagedigest" };
		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;
		public IEnumerable<string> ScenarioVerbs => new[] { "heart-site-open", "heart-sight-inspect", "heart-sight-check" };
		internal static bool ClaimsScript() => KingdomScenarioScript.TryRead(out IList<string> script, out _)
			&& script.Contains("heart-sight-inspect");
		internal static void RequireScript()
		{
			Require(KingdomScenarioScript.TryRead(out IList<string> script, out _) && script.Count == Script.Length,
				"exact completed-heart script absent");
			for (int i = 0; i < Script.Length; i++) Require(script[i] == Script[i], "completed-heart script differs");
		}
		public string RunScenarioVerb(string verb, string argument, out bool ok)
		{
			ok = false;
			try
			{
				RequireScript();
				Require(string.IsNullOrEmpty(argument) && !KingdomScenarioSaveFiles.LoadPresent()
					&& !KingdomScenarioAdvance.Pending && !KingdomSurvey.HasBoundPass, "heart source context differs");
				string result;
				if (verb == "heart-site-open") result = KingdomHeartSightWitness.Open();
				else if (verb == "heart-sight-inspect") result = KingdomHeartSightWitness.Inspect();
				else
				{
					Require(verb == "heart-sight-check", "unknown completed-heart verb");
					result = KingdomHeartSightWitness.Record(The.Game);
				}
				ok = true; return result;
			}
			catch (Exception error) { return "completed-heart refused: " + KingdomScenarioRules.Bounded(error.Message); }
		}
		internal static void Require(bool value, string failure)
		{
			if (!value) throw new InvalidOperationException(failure);
		}
	}
}
