using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Unattended replacement for the attended five-topic traversal. Reads the graph the
	/// production advisor creator wrote; never opens a conversation window, never founds, and
	/// leaves the fresh profile unfounded.
	/// </summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomGuideTopicsNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "guide-topics-check";
		internal const string Receipt = "r_TAF_ScenarioGuideTopicsChecks_v1";
		internal const int ExpectedCases = 3;

		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }

		public IEnumerable<string> ScenarioVerbs { get { return new[] { Verb }; } }

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			if (Verb != KingdomGuideTopicsNativeProvider.Verb || !string.IsNullOrEmpty(Argument))
				return "guide-topics-check takes no arguments";
			XRLGame game = The.Game;
			Zone zone = The.Player?.CurrentZone;
			string failure;
			if (!Eligible(game, zone, out failure)) return "native guide topics refused: " + failure;
			game.SetStringGameState(Receipt, "intent");
			if (!KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"))
				return "native guide topics refused: test intent did not persist exactly";
			KingdomNativeRegressionContext context = new KingdomNativeRegressionContext(game, zone,
				KingdomQuickstartBootstrap.NativeQuickstartCleanup);
			KingdomQuickstartBootstrap.NativeGuideTopicChecks(context);
			Ok = context.Count == ExpectedCases && context.Passed == ExpectedCases
				&& context.Failed == 0;
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
			Failure = "requires a fresh, unfounded marsh scenario profile";
			if (Game == null || Zone == null || The.ZoneManager == null
				|| !ReferenceEquals(The.ZoneManager.ActiveZone, Zone)
				|| (Game.GetSystem<KingdomSystem>()?.Founded ?? false)
				|| KingdomNativeRegressionContext.HasQuickstartState(Game)
				|| KingdomNativeRegressionContext.HasAnyState(Game, Receipt)) return false;
			KingdomScenarioPlan plan;
			KingdomScenarioProvenance stamp;
			if (!KingdomScenarioRealizer.TryBindStampedPlan(out plan, out stamp, out Failure))
				return false;
			IList<string> script;
			if (plan.Key != "founding-first-city"
				|| !KingdomScenarioScript.TryRead(out script, out Failure) || script.Count != 3
				|| script[0] != "stagedigest" || script[1] != Verb || script[2] != "stagedigest")
			{
				Failure = "requires the exact fresh-profile script and stamped plan";
				return false;
			}
			string transaction;
			if (KingdomScenarioTransactionMarker.Observe(out transaction)
				!= KingdomScenarioTransactionShape.None)
			{
				Failure = "requires an unspent scenario transaction";
				return false;
			}
			KingdomQuickstartProfile profile;
			Failure = "requires the active stamped marsh zone";
			if (!KingdomQuickstartRules.TryProfile("marsh", out profile)
				|| Zone.ZoneID != profile.ZoneId) return false;
			Failure = null;
			return true;
		}
	}
}
