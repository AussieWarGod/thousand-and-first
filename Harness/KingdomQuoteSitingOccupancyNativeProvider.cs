using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Native coverage for the #034 quote-siting occupancy fix (Growth/KingdomPlot2.08.
	/// Siting.cs, Growth/KingdomPlotSelectionRules.cs): three behaviours the source-only tests
	/// in DevTests cannot reach (no Zone/GameObject there). SYNTHETIC SETUP, DISCLOSED: real
	/// founding/dedication, one synthetic NPC body per phase, harness-assigned raw timber.
	/// Nothing forces a rect, a snapshot, or a receipt directly -- every assertion reads
	/// TryQuoteCommission's/Commission's own real output. NOT YET NATIVELY RUN: written and
	/// gate.sh-compiled only; the sealed script below has not been executed.
	/// </summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomQuoteSitingOccupancyNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string SetupVerb = "quote-occupancy-setup";
		internal const string CheckVerb = "quote-occupancy-check";
		internal const string Receipt = "r_TAF_ScenarioQuoteOccupancyNative_v1";
		private static readonly string[] Script = { "stagedigest", SetupVerb, CheckVerb, CheckVerb,
			CheckVerb, CheckVerb, CheckVerb, "stagedigest" };

		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }

		public IEnumerable<string> ScenarioVerbs { get { return new[] { SetupVerb, CheckVerb }; } }

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(string.IsNullOrEmpty(Argument) && (Verb == SetupVerb || Verb == CheckVerb),
					"quote-occupancy verbs take no arguments");
				IList<string> script;
				Require(KingdomScenarioScript.TryRead(out script, out _)
					&& script.Count == Script.Length, "the exact sealed quote-occupancy script is absent");
				for (int i = 0; i < Script.Length; i++)
					Require(script[i] == Script[i], "the sealed quote-occupancy script differs");
				XRLGame game = The.Game;
				Zone zone = The.Player?.CurrentZone;
				if (Verb == SetupVerb)
				{
					Require(Eligible(game, zone), "requires a fresh stamped marsh camp with the "
						+ "master and growth cadence enabled");
					game.SetStringGameState(Receipt, "intent");
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
						"the quote-occupancy intent failed its exact readback");
				}
				Require(game != null && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
					"the quote-occupancy owner intent is absent or torn");
				bool complete;
				string result = KingdomQuoteSitingOccupancyNativeChecks.Run(Verb, game, zone, out complete);
				if (complete)
				{
					Require(ReferenceEquals(The.Game, game), "the quote-occupancy report owner changed");
					game.SetStringGameState(Receipt, result);
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, result),
						"the quote-occupancy report failed its exact readback");
				}
				Ok = true;
				return result;
			}
			catch (Exception error) { return KingdomQuoteSitingOccupancyNativeChecks.Fail(error); }
		}

		private static bool Eligible(XRLGame Game, Zone Zone)
		{
			KingdomScenarioPlan plan;
			KingdomQuickstartProfile profile;
			return Game != null && Zone != null
				&& ReferenceEquals(The.ZoneManager?.ActiveZone, Zone) && MessageQueue.Enabled
				&& KingdomMaster.ConfiguredEnabled && KingdomGrowth.Enabled
				&& !KingdomSurvey.HasBoundPass && !KingdomScenarioAdvance.Pending
				&& !(Game.GetSystem<KingdomSystem>()?.Founded ?? false)
				&& KingdomQuoteSitingOccupancyNativeChecks.Vacant
				&& !KingdomNativeRegressionContext.HasQuickstartState(Game)
				&& !KingdomNativeRegressionContext.HasAnyState(Game, Receipt)
				&& KingdomScenarioRealizer.TryBindStampedPlan(out plan, out _, out _)
				&& plan.Key == "founding-first-city"
				&& plan.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority
				&& KingdomScenarioTransactionMarker.Observe(out _)
					== KingdomScenarioTransactionShape.None
				&& KingdomQuickstartRules.TryProfile("marsh", out profile)
				&& Zone.ZoneID == profile.ZoneId;
		}

		internal static void Require(bool Value, string Failure)
		{
			if (!Value) throw new InvalidOperationException(
				Failure ?? "native quote-occupancy evidence refused");
		}
	}
}
