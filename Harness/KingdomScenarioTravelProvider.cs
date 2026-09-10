using System;
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst.Harness
{
	/// <summary>Sealed first-city presence pair; no generic teleport or scenario-state rewrite.</summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomScenarioTravelProvider : IKingdomScenarioVerbProvider
	{
		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;
		public IEnumerable<string> ScenarioVerbs => new[] { "beta-away", "beta-present", "beta-return", "beta-check" };

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				KingdomScenarioTravel.Require(The.Game != null, "no live scenario game");
				KingdomScenarioTravel.Require(string.IsNullOrEmpty(Argument), "travel verbs take no arguments");
				if (Verb == "beta-away" || Verb == "beta-present")
				{
					KingdomScenarioTravel.Require(KingdomScenarioRealizer.TryBindStampedPlan(out var plan, out var stamp, out string why)
						&& plan.Key == "founding-first-city", why ?? "requires founding-first-city plan");
					KingdomScenarioTravel.Seed = plan.Seed;
					KingdomScenarioTravel.Require(KingdomScenarioScript.TryRead(out IList<string> script, out why), why);
					string[] exact = { "stagedigest", "realize", "advance 1", Verb, "advance 1200",
						"beta-return", "advance 39", "yield-frames 1", "beta-check", "status" };
					if (!KingdomScenarioPauseController.Recipe(script, Verb))
					{
						KingdomScenarioTravel.Require(script.Count == exact.Length, "requires exact travel persona");
						for (int i = 0; i < exact.Length; i++) KingdomScenarioTravel.Require(script[i] == exact[i], "travel script differs");
					}
					else KingdomScenarioTravel.Require(KingdomScenarioPauseController.Active, "pause recipe has no live witness");
					KingdomScenarioPauseController.BeforeTravel();
				}
				string result;
				switch (Verb)
				{
					case "beta-away": result = KingdomScenarioTravel.Begin(true); break;
					case "beta-present": result = KingdomScenarioTravel.Begin(false); break;
					case "beta-return": result = KingdomScenarioTravel.Return(); break;
					case "beta-check": result = KingdomScenarioTravel.Check(); break;
					default: return "taf-travel-refused: unknown travel verb";
				}
				Ok = true; return result;
			}
			catch (Exception error)
			{
				KingdomScenarioTravel.Fault = error.Message;
				KingdomScenarioTravel.State = KingdomScenarioTravel.Phase.Failed;
				return "taf-travel-refused: " + error.Message;
			}
		}
	}
}
