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
		// Cumulative ticks 2400/6000/9600/13200 (deltas 2400/3600/3600/3600) -- widened per
		// review-bba51c4-teardown-findings.md nit 1: the prior 2000/4800/7600/10800 schedule had
		// ZERO slack (fire's 600 labour ticks / larder's 1200 vs one 1200-tick settlement pass
		// per checkpoint), and persona EXPECT hard-required fire fully built by tick 2000 --
		// exactly what native run 15 never achieved. The first interval is now TWO passes
		// (2400 ticks): production's own first attended pass after founding prices the
		// newly-selected raising at 0 (selection itself happens on that pass, Growth/
		// KingdomConstructionPresence.cs:95-118 Assign, before any labour is priced against it),
		// so a single 1200-tick interval can price nothing even on the successful path; the
		// second pass inside this same interval is what actually prices fire's 600 ticks. Every
		// later interval keeps a full spare pass (3600 = 3 passes for legs needing at most 1200
		// ticks: fire's removal/salvage, larder's own raise+strike, larder's removal).
		private static readonly string[] Script = { "stagedigest", SetupVerb, "advance 2400",
			CheckVerb, "advance 3600", CheckVerb, "advance 3600", CheckVerb, "advance 3600",
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
