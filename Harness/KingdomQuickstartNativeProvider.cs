using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>Dev-only native seam tests; never claims full embark or ordinary-play acceptance.</summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomQuickstartNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "quickstart-check";
		internal const string Receipt = "r_TAF_ScenarioQuickstartChecks_v1";
		internal const int ExpectedCases = 16;
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { Verb }; } }

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			if (!string.Equals(Verb, KingdomQuickstartNativeProvider.Verb, StringComparison.Ordinal)
				|| !string.IsNullOrEmpty(Argument)) return "quickstart-check takes no arguments";
			XRLGame game = The.Game;
			Zone zone = The.Player?.CurrentZone;
			string reason;
			if (!Eligible(game, zone, out reason)) return "native quickstart refused: " + reason;
			game.SetStringGameState(Receipt, "intent");
			if (!KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"))
				return "native quickstart refused: test intent did not persist exactly";
			KingdomNativeRegressionContext context = new KingdomNativeRegressionContext(game, zone,
				KingdomQuickstartBootstrap.NativeQuickstartCleanup);
			KingdomQuickstartBootstrap.NativeQuickstartChecks(context);
			Ok = context.Count == ExpectedCases && context.Passed == ExpectedCases
				&& context.Failed == 0;
			string report = context.Report();
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
			if (Game == null || Zone == null
				|| The.ZoneManager == null || !ReferenceEquals(The.ZoneManager.ActiveZone, Zone)
				|| (Game.GetSystem<KingdomSystem>()?.Founded ?? false)
				|| KingdomNativeRegressionContext.HasQuickstartState(Game)
				|| KingdomNativeRegressionContext.HasAnyState(Game, Receipt)) return false;
			KingdomScenarioPlan plan;
			KingdomScenarioProvenance stamp;
			if (!KingdomScenarioRealizer.TryBindStampedPlan(out plan, out stamp, out Failure)) return false;
			// The plan describes an unexecuted production transaction. Its Synthetic flag
			// does not classify this separate provider's injected fixture evidence.
			IList<string> script;
			if (!string.Equals(plan.Key, "founding-first-city", StringComparison.Ordinal)
				|| !KingdomScenarioScript.TryRead(out script, out Failure) || script.Count != 3
				|| script[0] != "stagedigest" || script[1] != Verb || script[2] != "stagedigest")
			{
				Failure = "native fixture checks require their exact fresh-profile script and stamped plan";
				return false;
			}
			string transaction;
			if (KingdomScenarioTransactionMarker.Observe(out transaction)
				!= KingdomScenarioTransactionShape.None)
			{
				Failure = "native fixture checks require an unspent scenario transaction";
				return false;
			}
			Failure = "requires clear marsh fixture cells";
			KingdomQuickstartProfile profile;
			if (!KingdomQuickstartRules.TryProfile("marsh", out profile)
				|| !string.Equals(Zone.ZoneID, profile.ZoneId, StringComparison.Ordinal)) return false;
			for (int x = 28; x <= 30; x++)
				for (int y = 10; y <= 16; y++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null || !cell.IsPassable() || cell.HasOpenLiquidVolume()) return false;
					foreach (GameObject item in cell.GetObjects())
						if (GameObject.Validate(item) && (item.IsCreature
							|| KingdomPlots.ReadObject(item) != KingdomPlotRules.GroundKind.Bare))
							return false;
				}
			Failure = null;
			return true;
		}
	}
}
