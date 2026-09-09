using System;
using System.Collections.Generic;
using HarmonyLib;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Unattended replacement for the attended first-guest read. The setup founds a camp with
	/// water and no roof; the two checks read the correspondence the production cadence opened on
	/// real turns, once each side of a further due pass.
	/// </summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomFirstGuestNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string SetupVerb = "first-guest-setup";
		internal const string CheckVerb = "first-guest-check";
		internal const string Receipt = "r_TAF_ScenarioFirstGuestNative_v1";
		private static readonly string[] Script = { "stagedigest", SetupVerb, "advance 6000",
			CheckVerb, "advance 6000", CheckVerb, "stagedigest" };

		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }

		public IEnumerable<string> ScenarioVerbs { get { return new[] { SetupVerb, CheckVerb }; } }

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(string.IsNullOrEmpty(Argument) && (Verb == SetupVerb || Verb == CheckVerb),
					"first-guest verbs take no arguments");
				IList<string> script;
				Require(KingdomScenarioScript.TryRead(out script, out _)
					&& script.Count == Script.Length, "the exact sealed first-guest script is absent");
				for (int i = 0; i < Script.Length; i++)
					Require(script[i] == Script[i], "the sealed first-guest script differs");
				XRLGame game = The.Game;
				Zone zone = The.Player?.CurrentZone;
				if (Verb == SetupVerb)
				{
					Require(Eligible(game, zone), "requires a fresh stamped marsh camp with the "
						+ "master, growth, civic story and native messages enabled");
					game.SetStringGameState(Receipt, "intent");
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
						"the first-guest intent failed its exact readback");
				}
				Require(game != null && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
					"the first-guest owner intent is absent or torn");
				bool complete;
				string result = KingdomFirstGuestNativeChecks.Run(Verb, game, zone, out complete);
				if (complete)
				{
					Require(ReferenceEquals(The.Game, game), "the first-guest report owner changed");
					game.SetStringGameState(Receipt, result);
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, result),
						"the first-guest report failed its exact readback");
				}
				Ok = true;
				return result;
			}
			catch (Exception error) { return KingdomFirstGuestNativeChecks.Fail(error); }
		}

		private static bool Eligible(XRLGame Game, Zone Zone)
		{
			KingdomScenarioPlan plan;
			KingdomQuickstartProfile profile;
			return Game != null && Zone != null
				&& ReferenceEquals(The.ZoneManager?.ActiveZone, Zone) && MessageQueue.Enabled
				&& KingdomMaster.ConfiguredEnabled && KingdomGrowth.Enabled
				&& XRL.UI.Options.GetOption(KingdomExperienceOptions.StoryOptionId, "Yes") != "No"
				&& !KingdomSurvey.HasBoundPass && !KingdomScenarioAdvance.Pending
				&& !(Game.GetSystem<KingdomSystem>()?.Founded ?? false)
				&& KingdomFirstGuestNativeChecks.Vacant
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
				Failure ?? "native first-guest evidence refused");
		}
	}

	/// <summary>
	/// Void observation at the real message write. The prefix only counts; it never alters the
	/// line, suppresses it, or changes whether the engine writes it.
	/// </summary>
	[HarmonyPatch(typeof(MessageQueue), "Add", new Type[] { typeof(string) })]
	internal static class KingdomFirstGuestMessageObserver
	{
		[HarmonyPrefix]
		internal static void Prefix(string Message)
		{
			KingdomFirstGuestNativeChecks.Observe(Message);
		}
	}
}
