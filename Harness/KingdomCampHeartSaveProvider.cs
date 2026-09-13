using System;
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomCampHeartSaveProvider : IKingdomScenarioVerbProvider
	{
		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;
		public IEnumerable<string> ScenarioVerbs => new[] { KingdomCampHeartScript.SaveVerb };
		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				KingdomCampHeartNativeProvider.Require(Verb == KingdomCampHeartScript.SaveVerb
					&& string.IsNullOrEmpty(Argument) && KingdomScenarioScript.TryRead(out var script, out _)
					&& KingdomCampHeartScript.Matches(script, true), "the paid camp save script differs");
				string report = KingdomCampHeartNativeChecks.Save(The.Game);
				Ok = true;
				return report;
			}
			catch (Exception error)
			{
				return "paid camp save refused: " + KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
			}
		}
	}
}
