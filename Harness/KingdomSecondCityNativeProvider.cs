using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Behavioural coverage row 12 "Multiple cities". City one is founded by the built-in
	/// realize verb - the production first-city transaction, never a harness copy. This provider
	/// then resolves a second site on a non-adjacent surface parasang, relocates the founder,
	/// drives the production second-city transaction through KingdomFounding.FoundSecond, proves
	/// the held-ground refusal spends nothing, and proves the seat comes back by production's own
	/// ZoneActivatedEvent handler when the founder returns.
	/// <para>
	/// SYNTHETIC SETUP, DISCLOSED: zero-energy SystemMoveTo plus SetActiveZone instead of walking
	/// the world map; FoundSecond is the waterless route, so the basin's dram cost and its three
	/// Popup prompts (name, vocation, refusal) are NOT exercised - a sealed script cannot answer
	/// a popup and KingdomScenarioVerbProvider forbids one outright. Force is never passed, so
	/// GroundIsTooClose is observed on the bordering parasang rather than bypassed. No turns are
	/// spent; city two has no population, buildings, stockpile or economy. Save and cold load are
	/// out of scope for this persona.
	/// </para>
	/// NOT YET NATIVELY RUN: written and compiled only; the sealed script below has not executed.
	/// </summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomSecondCityNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string SetupVerb = KingdomSecondCityScript.SetupVerb;
		internal const string CheckVerb = KingdomSecondCityScript.CheckVerb;
		internal const string Receipt = "r_TAF_ScenarioSecondCityNative_v1";

		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }

		public IEnumerable<string> ScenarioVerbs { get { return new[] { SetupVerb, CheckVerb }; } }

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(string.IsNullOrEmpty(Argument) && (Verb == SetupVerb || Verb == CheckVerb),
					"second-city verbs take no arguments");
				IList<string> script;
				Require(KingdomScenarioScript.TryRead(out script, out _)
					&& KingdomSecondCityScript.Matches(script),
					"the exact sealed second-city script is absent");
				XRLGame game = The.Game;
				Zone zone = The.Player == null ? null : The.Player.CurrentZone;
				if (Verb == SetupVerb)
				{
					Require(Eligible(game, zone), "requires the realized single-city marsh camp "
						+ "with the master and growth cadence enabled");
					game.SetStringGameState(Receipt, "intent");
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
						"the second-city intent failed its exact readback");
				}
				Require(game != null
					&& KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
					"the second-city owner intent is absent or torn");
				bool complete;
				string result = KingdomSecondCityNativeChecks.Run(Verb, game, zone, out complete);
				if (complete)
				{
					Require(ReferenceEquals(The.Game, game),
						"the second-city report owner changed");
					game.SetStringGameState(Receipt, result);
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, result),
						"the second-city report failed its exact readback");
				}
				// A per-case failure is caught and counted rather than thrown, so the verb must
				// refuse on its own account here instead of reporting green.
				Ok = KingdomSecondCityNativeChecks.Ok;
				return result;
			}
			catch (Exception error) { return KingdomSecondCityNativeChecks.Fail(error); }
		}

		/// <summary>
		/// The setup verb runs AFTER realize, so unlike the pre-founding fixtures this wall
		/// requires a founded realm - and exactly one city in it.
		/// </summary>
		private static bool Eligible(XRLGame Game, Zone Zone)
		{
			KingdomScenarioPlan plan;
			KingdomQuickstartProfile profile;
			KingdomSystem system = Game == null ? null : Game.GetSystem<KingdomSystem>();
			return Game != null && Zone != null && system != null && system.Founded
				&& system.SettlementCount == 1 && system.NonSeatSettlementCount == 0
				&& ReferenceEquals(The.ZoneManager == null ? null : The.ZoneManager.ActiveZone,
					Zone) && MessageQueue.Enabled
				&& KingdomMaster.ConfiguredEnabled && KingdomGrowth.Enabled
				&& !KingdomSurvey.HasBoundPass && !KingdomScenarioAdvance.Pending
				&& KingdomSecondCityNativeChecks.Vacant
				&& !KingdomNativeRegressionContext.HasQuickstartState(Game)
				&& !KingdomNativeRegressionContext.HasAnyState(Game, Receipt)
				&& KingdomScenarioRealizer.TryBindStampedPlan(out plan, out _, out _)
				&& plan.Key == "founding-first-city"
				&& plan.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority
				&& KingdomQuickstartRules.TryProfile("marsh", out profile)
				&& Zone.ZoneID == profile.ZoneId;
		}

		internal static void Require(bool Value, string Failure)
		{
			if (!Value) throw new InvalidOperationException(
				Failure ?? "native second-city evidence refused");
		}
	}
}
