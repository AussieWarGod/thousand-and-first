using System;
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst.Harness
{
	/// <summary>Exact developer-only paid heart chain; fixture support is supplied once, then
	/// ordinary settlement turns own every improvement and its payment.</summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomCampHeartChainProvider : IKingdomScenarioVerbProvider
	{
		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;
		public IEnumerable<string> ScenarioVerbs => new[] { KingdomCampHeartChainScript.Setup,
			KingdomCampHeartChainScript.Check, KingdomCampHeartChainScript.Supply, KingdomCampHeartChainScript.Save };
		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				KingdomCampHeartNativeProvider.Require(string.IsNullOrEmpty(Argument)
					&& KingdomScenarioScript.TryRead(out var script, out _)
					&& KingdomCampHeartChainScript.Matches(script, Verb == KingdomCampHeartChainScript.Save),
					"exact sealed paid heart chain absent");
				string report = Verb == KingdomCampHeartChainScript.Save
					? KingdomCampHeartNativeChecks.SaveChain(The.Game) : KingdomCampHeartNativeChecks.Chain(Verb, The.Game);
				Ok = true;
				return report;
			}
			catch (Exception error)
			{
				KingdomScenarioJournal.Append("camp-heart-chain-diagnostic", false,
					error.GetType().Name + ": " + error.Message);
				return "paid-heart-chain refused; evidence retained: "
					+ KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
			}
		}
	}
}
