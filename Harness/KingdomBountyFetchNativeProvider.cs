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
		// Exact phase transitions, not a report store. Each verb requires the phase its
		// predecessor wrote and then writes its own, so the mandatory revisit step still has an
		// owner phase it can prove; the report itself travels back as the verb's result.
		internal const string IntentPhase = "intent", CarriedPhase = "carried";
		internal const string RevisitedPhase = "revisited";
		private static readonly string[] Script = { "stagedigest", SetupVerb, "advance 7200",
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
					game.SetStringGameState(Receipt, IntentPhase);
				}
				string expected = Required(verb);
				Require(game != null && KingdomScenarioDurableState.ProvesExactText(Receipt, expected),
					"bounty fetch owner phase absent, out of order or torn");
				string result = KingdomBountyFetchNativeChecks.Run(verb, game, zone, out bool complete);
				if (complete)
				{
					string written = Written(verb);
					Require(ReferenceEquals(The.Game, game)
						&& KingdomScenarioDurableState.ProvesExactText(Receipt, expected),
						"bounty fetch report owner changed");
					game.SetStringGameState(Receipt, written);
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, written),
						"bounty fetch phase failed exact readback");
				}
				Ok = true; return result;
			}
			catch (Exception error) { return KingdomBountyFetchNativeChecks.Fail(error); }
		}

		/// <summary>The exact phase this verb's predecessor must have left on the receipt.</summary>
		private static string Required(string verb)
		{ return (verb == RevisitVerb) ? CarriedPhase : IntentPhase; }

		/// <summary>The exact phase this verb writes once its own report is complete.</summary>
		private static string Written(string verb)
		{ return (verb == CheckVerb) ? CarriedPhase : RevisitedPhase; }

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
	// fixture; these two record that it ran on the fixture's own realm and ground.
	[HarmonyPatch(typeof(KingdomBounty), "OnSettlementPass")]
	internal static class KingdomBountyFetchPassObserver
	{
		[HarmonyPrefix] internal static void Prefix(KingdomSystem System, Zone Z)
		{ KingdomBountyFetchNativeChecks.ObservePass(true, System, Z); }
		[HarmonyPostfix] internal static void Postfix(KingdomSystem System, Zone Z)
		{ KingdomBountyFetchNativeChecks.ObservePass(false, System, Z); }
	}
}
