using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Native-check seam for the ORDINARY (non-heart) building tier upgrade: a real settlement
	/// raises its own paid tent into a staked tent-row. Behaviour-coverage row 8.
	/// <para>
	/// THE UPGRADE IS DRIVEN BY THE REAL SETTLEMENT PASS. This seam never calls
	/// <c>KingdomUpgrade.Begin</c>, <c>BeginPrepared</c>, <c>BeginPreparedPlanChange</c>,
	/// <c>TryApplyUpgrade</c>, <c>KingdomPlots.Advance</c> or
	/// <c>KingdomConstruction.TryFundNew</c>. Ordinary turn advance runs
	/// <c>KingdomSystem.AttendSeatedSemantics</c>'s "improvement" step
	/// (<c>Core/KingdomSystem.z21.SemanticPass.cs:112-115</c>), which is what assesses, begins,
	/// builds, hands over and retires the predecessor. The verbs only observe, with the single
	/// disclosed exception of the setup verb's world seeding.
	/// </para>
	/// <para>
	/// SYNTHETIC SETUP, DISCLOSED. The camp is founded by the harness through its own founding
	/// step; the water store, the residents (and their born provenance) and the raw material
	/// units are fixture inputs; the once-per-game modal first notice
	/// (<c>Growth/KingdomUpgrade.09.RegistryAndIdentity.cs:111-120</c>) is pre-marked as given so
	/// a sealed script is not stalled by a popup. The TENT ITSELF IS NOT SYNTHETIC: it is
	/// commissioned through <c>KingdomPlots.Commission</c> and built by production, because the
	/// authored upgrade lane needs frozen lot receipts no fixture may invent. No claim is made
	/// about reachability through ordinary play, about a rendered Charter, or about save/load.
	/// </para>
	/// </summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomTierUpgradeProvider : IKingdomScenarioVerbProvider
	{
		internal const string SetupVerb = "tier-upgrade-setup";
		internal const string CheckVerb = "tier-upgrade-check";
		internal const string ShortVerb = "tier-upgrade-short";
		internal const string Receipt = "r_TAF_ScenarioTierUpgradeNative_v1";

		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }

		public IEnumerable<string> ScenarioVerbs
		{
			get { return new[] { SetupVerb, CheckVerb, ShortVerb }; }
		}

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(string.IsNullOrEmpty(Argument) && (Verb == SetupVerb || Verb == CheckVerb
					|| Verb == ShortVerb), "tier-upgrade verbs take no arguments");
				IList<string> script;
				Require(KingdomScenarioScript.TryRead(out script, out _)
					&& KingdomTierUpgradeScript.Matches(script),
					"the exact sealed tier-upgrade script is absent or differs");
				XRLGame game = The.Game;
				Zone zone = The.Player?.CurrentZone;
				if (Verb == SetupVerb)
				{
					Require(Eligible(game, zone), "requires a fresh stamped founding-first-city "
						+ "camp with the master and growth enabled and no tier-upgrade state "
						+ "already standing");
					game.SetStringGameState(Receipt, "intent");
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
						"the tier-upgrade intent failed its exact readback");
				}
				Require(game != null
					&& KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
					"the tier-upgrade owner intent is absent or torn");
				bool complete;
				string result = KingdomTierUpgradeChecks.Run(Verb, game, zone, out complete);
				if (complete)
				{
					Require(ReferenceEquals(The.Game, game),
						"the tier-upgrade report owner changed");
					game.SetStringGameState(Receipt, result);
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, result),
						"the tier-upgrade report failed its exact readback");
				}
				Ok = true;
				return result;
			}
			catch (Exception error) { return KingdomTierUpgradeChecks.Fail(error); }
		}

		private static bool Eligible(XRLGame Game, Zone Zone)
		{
			KingdomScenarioPlan plan;
			return Game != null && Zone != null
				&& ReferenceEquals(The.ZoneManager?.ActiveZone, Zone)
				&& XRL.Messages.MessageQueue.Enabled
				&& KingdomMaster.ConfiguredEnabled && KingdomGrowth.Enabled
				&& !KingdomSurvey.HasBoundPass && !KingdomScenarioAdvance.Pending
				&& !(Game.GetSystem<KingdomSystem>()?.Founded ?? false)
				&& KingdomTierUpgradeChecks.Vacant
				&& !KingdomNativeRegressionContext.HasQuickstartState(Game)
				&& !KingdomNativeRegressionContext.HasAnyState(Game, Receipt)
				&& KingdomScenarioRealizer.TryBindStampedPlan(out plan, out _, out _)
				&& plan.Key == "founding-first-city"
				&& plan.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority
				&& KingdomScenarioTransactionMarker.Observe(out _)
					== KingdomScenarioTransactionShape.None;
		}

		internal static void Require(bool Value, string Failure)
		{
			if (!Value) throw new InvalidOperationException(
				Failure ?? "native tier-upgrade evidence refused");
		}
	}
}
