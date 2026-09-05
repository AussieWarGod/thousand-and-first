using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomRaidOutboxNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "raid-outbox-check";
		internal const string Receipt = "r_TAF_ScenarioRaidOutboxChecks_v1";
		internal const int ExpectedCases = 6;
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { Verb }; } }

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			if (Verb != KingdomRaidOutboxNativeProvider.Verb || !string.IsNullOrEmpty(Argument))
				return "raid-outbox-check takes no arguments";
			XRLGame game = The.Game;
			Zone zone = The.Player?.CurrentZone;
			string failure;
			if (!Eligible(game, zone, out failure)) return "native raid outbox refused: " + failure;
			game.SetStringGameState(Receipt, "intent");
			if (!KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"))
				return "native raid outbox refused: test intent did not persist exactly";
			KingdomRaidOutboxNativeContext context = new KingdomRaidOutboxNativeContext(game, zone);
			KingdomRaids.NativeOutboxChecks(context);
			Ok = context.Count == ExpectedCases && context.Passed == ExpectedCases && context.Failed == 0;
			string report = context.Report();
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

		private static bool Eligible(XRLGame Game, Zone Zone, out string Failure)
		{
			Failure = "requires a fresh unfounded marsh profile with native messages enabled";
			if (Game == null || Zone == null || The.ZoneManager == null
				|| !ReferenceEquals(The.ZoneManager.ActiveZone, Zone) || !MessageQueue.Enabled
				|| (Game.GetSystem<KingdomSystem>()?.Founded ?? false)
				|| KingdomNativeRegressionContext.HasQuickstartState(Game)
				|| KingdomNativeRegressionContext.HasAnyState(Game, Receipt)
				|| KingdomNativeRegressionContext.HasAnyState(Game, KingdomRaidOutboxNativeContext.RegistryKey)
				|| KingdomNativeRegressionContext.HasAnyState(Game, KingdomRaidOutboxNativeContext.FaultKey))
				return false;
			KingdomScenarioPlan plan;
			KingdomScenarioProvenance stamp;
			if (!KingdomScenarioRealizer.TryBindStampedPlan(out plan, out stamp, out Failure)) return false;
			IList<string> script;
			if (plan.Key != "founding-first-city"
				|| !KingdomScenarioScript.TryRead(out script, out Failure) || script.Count != 3
				|| script[0] != "stagedigest" || script[1] != Verb || script[2] != "stagedigest")
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
