using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomFoundingHeartNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "founding-heart-check";
		internal const string Receipt = "r_TAF_ScenarioFoundingHeartChecks_v1";
		internal const int ExpectedCases = 7;
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { Verb }; } }

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			if (Verb != KingdomFoundingHeartNativeProvider.Verb || !string.IsNullOrEmpty(Argument))
				return "founding-heart-check takes no arguments";
			XRLGame game = The.Game;
			Zone zone = The.Player?.CurrentZone;
			if (!Eligible(game, zone, out string failure)) return "native founding heart refused: " + failure;
			game.StringGameState.Add(Receipt, "intent");
			if (!KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"))
				return "native founding heart refused: test intent did not persist exactly";
			string report = KingdomFoundingHeartNativeChecks.Run(game, zone, out Ok);
			if (!ReferenceEquals(The.Game, game) || !KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"))
			{
				Ok = false;
				return report + "\nNative intent receipt changed; evidence retained.";
			}
			game.SetStringGameState(Receipt, report);
			if (!KingdomScenarioDurableState.ProvesExactText(Receipt, report))
			{
				Ok = false;
				return report + "\nNative result receipt did not persist exactly.";
			}
			return report;
		}

		private static bool Eligible(XRLGame game, Zone zone, out string failure)
		{
			failure = "requires a fresh unfounded marsh profile with subsidence and native messages enabled";
			if (game == null || zone == null || The.ZoneManager == null
				|| !ReferenceEquals(The.ZoneManager.ActiveZone, zone) || !MessageQueue.Enabled
				|| !KingdomSubsidence.Enabled || !KingdomLodging.Enabled || !KingdomMaster.ConfiguredEnabled
				|| (game.GetSystem<KingdomSystem>()?.Founded ?? false)
				|| KingdomNativeRegressionContext.HasQuickstartState(game)
				|| KingdomNativeRegressionContext.HasAnyState(game, Receipt)) return false;
			KingdomScenarioPlan plan;
			KingdomScenarioProvenance stamp;
			if (!KingdomScenarioRealizer.TryBindStampedPlan(out plan, out stamp, out failure)) return false;
			IList<string> script;
			if (plan.Key != "founding-first-city"
				|| !KingdomScenarioScript.TryRead(out script, out failure) || script.Count != 3
				|| script[0] != "stagedigest" || script[1] != Verb || script[2] != "stagedigest")
			{
				failure = "requires the exact fresh-profile script and stamped plan";
				return false;
			}
			string transaction;
			if (KingdomScenarioTransactionMarker.Observe(out transaction) != KingdomScenarioTransactionShape.None)
			{
				failure = "requires an unspent scenario transaction";
				return false;
			}
			KingdomQuickstartProfile profile;
			failure = "requires the active stamped marsh zone";
			if (!KingdomQuickstartRules.TryProfile("marsh", out profile) || zone.ZoneID != profile.ZoneId) return false;
			failure = null;
			return true;
		}
	}
}
