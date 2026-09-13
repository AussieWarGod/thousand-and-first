using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>Exact developer-only paid heart chain; fixture support is supplied once, then
	/// ordinary settlement turns own every improvement and its payment.</summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomCampHeartChainProvider : IKingdomScenarioVerbProvider
	{
		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;
		public IEnumerable<string> ScenarioVerbs => new[] { KingdomCampHeartChainScript.Setup,
			KingdomCampHeartChainScript.Check, KingdomCampHeartChainScript.Supply };
		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				KingdomCampHeartNativeProvider.Require(string.IsNullOrEmpty(Argument)
					&& KingdomScenarioScript.TryRead(out var script, out _)
					&& KingdomCampHeartChainScript.Matches(script), "exact sealed paid heart chain absent");
				string report = KingdomCampHeartNativeChecks.Chain(Verb);
				Ok = true;
				return report;
			}
			catch (Exception error)
			{
				return "paid-heart-chain refused; evidence retained: "
					+ KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
			}
		}
	}
}
