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
			KingdomCampHeartChainScript.Check, KingdomCampHeartChainScript.Supply,
			KingdomCampHeartChainScript.Capital };
		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				bool read = KingdomScenarioScript.TryRead(out var script, out _);
				KingdomCampHeartNativeProvider.Require(string.IsNullOrEmpty(Argument) && read
					&& KingdomCampHeartChainScript.SealedTargetRung(script) > 0,
					"exact sealed paid heart chain absent");
				KingdomCampHeartNativeProvider.Require(
					Verb != KingdomCampHeartChainScript.Capital
						|| KingdomCampHeartChainScript.MatchesRung5(script),
					"the capital seed belongs to the sealed arcology script only");
				string report = KingdomCampHeartNativeChecks.Chain(Verb, The.Game);
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
