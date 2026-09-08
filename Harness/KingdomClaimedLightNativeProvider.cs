using System;
using System.Collections.Generic;
using HarmonyLib;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.ZoneParts;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Unattended replacement for the attended claimed-ground light walk. Two verbs: the setup
	/// founds and activates, the check reads the light the real render dispatch left behind.
	/// </summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomClaimedLightNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string SetupVerb = "claimed-light-setup";
		internal const string CheckVerb = "claimed-light-check";
		internal const string Receipt = "r_TAF_ScenarioClaimedLightNative_v1";
		private static readonly string[] Script = { "stagedigest", SetupVerb, "advance 2400",
			CheckVerb, "stagedigest" };

		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }

		public IEnumerable<string> ScenarioVerbs { get { return new[] { SetupVerb, CheckVerb }; } }

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(string.IsNullOrEmpty(Argument) && (Verb == SetupVerb || Verb == CheckVerb),
					"claimed-light verbs take no arguments");
				IList<string> script;
				Require(KingdomScenarioScript.TryRead(out script, out _)
					&& script.Count == Script.Length, "the exact sealed claimed-light script is absent");
				for (int i = 0; i < Script.Length; i++)
					Require(script[i] == Script[i], "the sealed claimed-light script differs");
				XRLGame game = The.Game;
				Zone zone = The.Player?.CurrentZone;
				if (Verb == SetupVerb)
				{
					Require(Eligible(game, zone),
						"requires a fresh stamped marsh camp with the master and the light option on");
					game.SetStringGameState(Receipt, "intent");
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
						"the claimed-light intent failed its exact readback");
				}
				Require(game != null && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
					"the claimed-light owner intent is absent or torn");
				bool complete;
				string result = KingdomClaimedLightNativeChecks.Run(Verb, game, zone, out complete);
				if (complete)
				{
					Require(ReferenceEquals(The.Game, game), "the claimed-light report owner changed");
					game.SetStringGameState(Receipt, result);
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, result),
						"the claimed-light report failed its exact readback");
				}
				Ok = true;
				return result;
			}
			catch (Exception error) { return KingdomClaimedLightNativeChecks.Fail(error); }
		}

		private static bool Eligible(XRLGame Game, Zone Zone)
		{
			KingdomScenarioPlan plan;
			KingdomQuickstartProfile profile;
			return Game != null && Zone != null
				&& ReferenceEquals(The.ZoneManager?.ActiveZone, Zone) && MessageQueue.Enabled
				&& KingdomMaster.ConfiguredEnabled && KingdomClaimedGround.Enabled
				&& !KingdomSurvey.HasBoundPass && !KingdomScenarioAdvance.Pending
				&& !(Game.GetSystem<KingdomSystem>()?.Founded ?? false)
				&& KingdomClaimedLightNativeChecks.Vacant
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
				Failure ?? "native claimed-light evidence refused");
		}
	}

	/// <summary>
	/// Void bracket around the production part's own render dispatch. No argument, result, event
	/// or callback is replaced; the prefix and postfix only read the zone's light map.
	/// </summary>
	[HarmonyPatch(typeof(KingdomClaimedGroundLight), "HandleEvent",
		new Type[] { typeof(BeforeRenderEvent) })]
	internal static class KingdomClaimedLightRenderObserver
	{
		[HarmonyPrefix]
		internal static void Prefix(KingdomClaimedGroundLight __instance)
		{
			KingdomClaimedLightNativeChecks.Observe(true, __instance);
		}

		[HarmonyPostfix]
		internal static void Postfix(KingdomClaimedGroundLight __instance)
		{
			KingdomClaimedLightNativeChecks.Observe(false, __instance);
		}
	}
}
