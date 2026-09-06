using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomSubsidenceRungNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "subsidence-rung-check";
		internal const string DeathVerb = "subsidence-rung-death-check";
		internal static bool ExactScript(string script) { return script == Verb || script == DeathVerb; }
		internal const string Receipt = "r_TAF_ScenarioSubsidenceRungChecks_v1";
		internal const int ExpectedCases = 6;
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { Verb, DeathVerb }; } }

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			if ((Verb != KingdomSubsidenceRungNativeProvider.Verb && Verb != DeathVerb) || !string.IsNullOrEmpty(Argument))
				return "subsidence rung checks take no arguments";
			XRLGame game = The.Game;
			Zone zone = The.Player?.CurrentZone;
			string failure;
			if (!Eligible(game, zone, Verb, out failure)) return "native subsidence refused: " + failure;
			game.SetStringGameState(Receipt, "intent");
			if (!KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"))
				return "native subsidence refused: test intent did not persist exactly";
			string report = KingdomSubsidenceRungNativeChecks.Run(game, zone, out Ok, Verb == DeathVerb);
			if (!KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"))
			{
				Ok = false;
				return report + "\nNative intent receipt changed; existing evidence retained.";
			}
			game.SetStringGameState(Receipt, report);
			if (!KingdomScenarioDurableState.ProvesExactText(Receipt, report))
			{
				Ok = false;
				return report + "\nNative result receipt did not persist exactly.";
			}
			return report;
		}

		private static bool Eligible(XRLGame Game, Zone Zone, string RequestedVerb, out string Failure)
		{
			Failure = "requires a fresh unfounded marsh profile with subsidence and native messages enabled";
			if (Game == null || Zone == null || The.ZoneManager == null
				|| !ReferenceEquals(The.ZoneManager.ActiveZone, Zone) || !MessageQueue.Enabled
				|| (RequestedVerb == DeathVerb && !KingdomOffices.Enabled)
				|| !KingdomSubsidence.Enabled || !KingdomLodging.Enabled || !KingdomMaster.ConfiguredEnabled
				|| Game.TimeTicks <= 3L * KingdomSubsidenceStepRules.StepTicks
				|| (Game.GetSystem<KingdomSystem>()?.Founded ?? false)
				|| KingdomNativeRegressionContext.HasQuickstartState(Game)
				|| KingdomNativeRegressionContext.HasAnyState(Game, Receipt)) return false;
			KingdomScenarioPlan plan;
			KingdomScenarioProvenance stamp;
			if (!KingdomScenarioRealizer.TryBindStampedPlan(out plan, out stamp, out Failure)) return false;
			IList<string> script;
			if (plan.Key != "founding-first-city"
				|| !KingdomScenarioScript.TryRead(out script, out Failure) || script.Count != 3
				|| script[0] != "stagedigest" || script[1] != RequestedVerb || script[2] != "stagedigest")
			{
				Failure = "requires the exact fresh-profile script and stamped plan";
				return false;
			}
			string transaction;
			if (KingdomScenarioTransactionMarker.Observe(out transaction) != KingdomScenarioTransactionShape.None)
			{
				Failure = "requires an unspent scenario transaction";
				return false;
			}
			KingdomQuickstartProfile profile;
			Failure = "requires the active stamped marsh zone";
			if (!KingdomQuickstartRules.TryProfile("marsh", out profile) || Zone.ZoneID != profile.ZoneId)
				return false;
			Failure = null;
			return true;
		}
	}
}
