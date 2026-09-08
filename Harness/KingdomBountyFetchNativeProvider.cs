using System;
using System.Collections.Generic;
using HarmonyLib;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomBountyFetchNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string SetupVerb = "bounty-fetch-setup", CheckVerb = "bounty-fetch-check";
		internal const string RevisitVerb = "bounty-fetch-revisit";
		internal const string Receipt = "r_TAF_ScenarioBountyFetchNative_v1";
		private static readonly string[] Script = { "stagedigest", SetupVerb, "advance 2400",
			CheckVerb, "advance 1200", RevisitVerb, "stagedigest" };
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs
		{ get { return new[] { SetupVerb, CheckVerb, RevisitVerb }; } }
		public string RunScenarioVerb(string verb, string argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(string.IsNullOrEmpty(argument)
					&& (verb == SetupVerb || verb == CheckVerb || verb == RevisitVerb),
					"bounty fetch verbs take no arguments");
				Require(KingdomScenarioScript.TryRead(out var script, out _)
					&& script.Count == Script.Length, "exact sealed bounty fetch script absent");
				for (int i = 0; i < Script.Length; i++)
					Require(script[i] == Script[i], "bounty fetch script differs");
				XRLGame game = The.Game; Zone zone = The.Player?.CurrentZone;
				if (verb == SetupVerb)
				{
					Require(Eligible(game, zone), "requires fresh stamped marsh, enabled bounty and materials, and no haul hook");
					game.SetStringGameState(Receipt, "intent");
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
						"bounty fetch intent failed exact readback");
				}
				Require(game != null && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
					"bounty fetch owner intent absent or torn");
				string result = KingdomBountyFetchNativeChecks.Run(verb, game, zone, out bool complete);
				if (complete)
				{
					Require(ReferenceEquals(The.Game, game)
						&& KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
						"bounty fetch report owner changed");
					game.SetStringGameState(Receipt, result);
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, result),
						"bounty fetch report failed exact readback");
				}
				Ok = true; return result;
			}
			catch (Exception error) { return KingdomBountyFetchNativeChecks.Fail(error); }
		}

		// The shipped carry-sign seam must be absent: a hook that took the haul over would move
		// the load through a different route entirely, and this fixture would prove nothing about
		// the ordinary fetch transfer it exists to witness.
		private static bool Eligible(XRLGame game, Zone zone)
		{
			return game != null && zone != null && ReferenceEquals(The.ZoneManager?.ActiveZone, zone)
				&& MessageQueue.Enabled && KingdomMaster.ConfiguredEnabled && KingdomBounty.Enabled
				&& KingdomMaterials.Enabled && KingdomBounty.HaulHook == null
				&& !KingdomSurvey.HasBoundPass && !KingdomScenarioAdvance.Pending
				&& !(game.GetSystem<KingdomSystem>()?.Founded ?? false)
				&& KingdomBountyFetchNativeChecks.Vacant
				&& !KingdomNativeRegressionContext.HasQuickstartState(game)
				&& !KingdomNativeRegressionContext.HasAnyState(game, Receipt)
				&& KingdomScenarioRealizer.TryBindStampedPlan(out var plan, out _, out _)
				&& plan.Key == "founding-first-city"
				&& plan.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority
				&& KingdomScenarioTransactionMarker.Observe(out _) == KingdomScenarioTransactionShape.None
				&& KingdomQuickstartRules.TryProfile("marsh", out var profile)
				&& zone.ZoneID == profile.ZoneId;
		}

		internal static void Require(bool value, string failure)
		{ if (!value) throw new InvalidOperationException(failure ?? "native bounty fetch evidence refused"); }
	}

	// Void observations only. The real settlement pass is called by the game, never by this
	// fixture; these two record that it ran and what the notice looked like on each side.
	[HarmonyPatch(typeof(KingdomBounty), "OnSettlementPass")]
	internal static class KingdomBountyFetchPassObserver
	{
		[HarmonyPrefix] internal static void Prefix(KingdomSystem System, Zone Z)
		{ KingdomBountyFetchNativeChecks.ObservePass(true, System, Z); }
		[HarmonyPostfix] internal static void Postfix(KingdomSystem System, Zone Z)
		{ KingdomBountyFetchNativeChecks.ObservePass(false, System, Z); }
	}
}
