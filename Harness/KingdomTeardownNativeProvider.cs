using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Behavioural coverage: building teardown. Founds a fresh camp, dedicates water and a
	/// materials stockpile directly (SYNTHETIC SETUP, DISCLOSED -- the same convention
	/// <see cref="KingdomDepositOverflowNativeChecks"/> and <see cref="KingdomFirstGuestNativeChecks"/>
	/// use: real founding, real dedication API, harness-assigned raw counts), commissions one
	/// real plot building through <see cref="KingdomCommission.Commission"/>, orders it struck
	/// through the real <see cref="KingdomMaterials.OrderStrike"/> once it is functionally
	/// built, and proves removal plus material return on real turns. One negative path: a
	/// second strike order against the now-absent building must refuse by name, never silently
	/// pass.
	/// </summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomTeardownNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string SetupVerb = "teardown-setup";
		internal const string CheckVerb = "teardown-check";
		internal const string Receipt = "r_TAF_ScenarioTeardownNative_v1";
		// Cumulative ticks 2000/4800/7600/10800: fire+larder serialize one active labour root
		// per settlement pass (Growth/KingdomConstructionPresence.cs:180-186 -- non-selected
		// roots stay at 0 effectiveness) and each strike leg burns its own checkpoint pass
		// before banking effort (Growth/KingdomMaterials.11.StrikeWorkAndRecoveryEntry.cs:31-36),
		// so four checks are needed for both cases' full commission->build->strike->salvage
		// progression to land inside one finite budget (see review-teardown-reachability-
		// findings.md section 4).
		private static readonly string[] Script = { "stagedigest", SetupVerb, "advance 2000",
			CheckVerb, "advance 2800", CheckVerb, "advance 2800", CheckVerb, "advance 3200",
			CheckVerb, "stagedigest" };

		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }

		public IEnumerable<string> ScenarioVerbs { get { return new[] { SetupVerb, CheckVerb }; } }

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(string.IsNullOrEmpty(Argument) && (Verb == SetupVerb || Verb == CheckVerb),
					"teardown verbs take no arguments");
				IList<string> script;
				Require(KingdomScenarioScript.TryRead(out script, out _)
					&& script.Count == Script.Length, "the exact sealed teardown script is absent");
				for (int i = 0; i < Script.Length; i++)
					Require(script[i] == Script[i], "the sealed teardown script differs");
				XRLGame game = The.Game;
				Zone zone = The.Player?.CurrentZone;
				if (Verb == SetupVerb)
				{
					Require(Eligible(game, zone), "requires a fresh stamped marsh camp with the "
						+ "master and growth cadence enabled");
					game.SetStringGameState(Receipt, "intent");
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
						"the teardown intent failed its exact readback");
				}
				Require(game != null && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
					"the teardown owner intent is absent or torn");
				bool complete;
				string result = KingdomTeardownNativeChecks.Run(Verb, game, zone, out complete);
				if (complete)
				{
					Require(ReferenceEquals(The.Game, game), "the teardown report owner changed");
					game.SetStringGameState(Receipt, result);
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, result),
						"the teardown report failed its exact readback");
				}
				Ok = true;
				return result;
			}
			catch (Exception error) { return KingdomTeardownNativeChecks.Fail(error); }
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
				&& KingdomTeardownNativeChecks.Vacant
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
				Failure ?? "native teardown evidence refused");
		}
	}
}
