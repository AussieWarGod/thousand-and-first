using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>Real correspondence and body actions; synthetic founding/water and menu input.</summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomGuestActionsNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string Setup = "guest-actions-setup", Check = "guest-actions-check", Quickstart = "guest-actions-quickstart";
		private const string Receipt = "r_TAF_ScenarioGuestActions_v1";
		private static readonly string[] QuickstartScript = { "quickstart-lifecycle marsh yes", Quickstart, "advance 6000", Check, "stagedigest" };
		private static readonly string[] Script = { "stagedigest", Setup, "advance 6000", Check, "stagedigest" };
		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;
		public IEnumerable<string> ScenarioVerbs => new[] { Setup, Quickstart, Check };

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(string.IsNullOrEmpty(Argument) && (Verb == Setup || Verb == Check || Verb == Quickstart), "unexpected guest action verb");
				Require(KingdomScenarioScript.TryRead(out IList<string> script, out _)
					&& script.Count == Script.Length, "exact guest action script absent");
				bool quickstart = script[0] == QuickstartScript[0];
				string[] expected = quickstart ? QuickstartScript : Script;
				for (int i = 0; i < expected.Length; i++) Require(script[i] == expected[i], "guest action script differs");
				XRLGame game = The.Game;
				Zone zone = The.Player?.CurrentZone;
				Require(game != null && zone != null && !KingdomSurvey.HasBoundPass
					&& !KingdomScenarioAdvance.Pending && KingdomMaster.ConfiguredEnabled
					&& KingdomGrowth.Enabled, "guest action execution context absent");
				if (Verb == Setup || Verb == Quickstart)
				{
					Require(KingdomGuestActionsNativeChecks.Vacant
						&& (quickstart || !(game.GetSystem<KingdomSystem>()?.Founded ?? false))
						&& (quickstart || !KingdomNativeRegressionContext.HasQuickstartState(game))
						&& !KingdomNativeRegressionContext.HasAnyState(game, Receipt), "requires fresh unstamped guest state");
					game.SetStringGameState(Receipt, "intent");
				}
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "guest action owner intent differs");
				string result = Verb == Setup ? KingdomGuestActionsNativeChecks.Start(game, zone)
					: Verb == Quickstart ? KingdomGuestActionsNativeChecks.StartQuickstart(game, zone)
					: KingdomGuestActionsNativeChecks.Check(game, zone);
				if (Verb == Check)
				{
					game.SetStringGameState(Receipt, result);
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, result), "guest action result readback failed");
				}
				Ok = true;
				return result;
			}
			catch (Exception error)
			{
				return "native-guest-actions cases=1 passed=0 failed=1; "
					+ KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message)
					+ KingdomGuestActionsNativeChecks.Evidence;
			}
		}

		internal static void Require(bool Value, string Failure)
		{
			if (!Value) throw new InvalidOperationException(Failure);
		}
	}
}
