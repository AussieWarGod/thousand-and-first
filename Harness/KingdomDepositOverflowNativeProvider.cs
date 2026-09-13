using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Native-check seam for the raw delivery overflow refusal (issue #111). Every case runs the
	/// REAL adapters &mdash; <c>KingdomMaterials.StockpileDepositHost</c> and
	/// <c>KingdomMaterials.GroundSpillHost</c> &mdash; through the real
	/// <c>KingdomDepositEngine.Fill</c>, over real <c>GameObject</c> bodies in a real zone. The
	/// engine-free suites can only drive the seam's CONTRACT through a fake host; this seam is the
	/// only place the production censuses themselves (<c>TryRawStockHeldIn</c>,
	/// <c>TryCountBlueprint</c>) are actually executed against engine stack counts.
	/// <para>
	/// SYNTHETIC SETUP, DISCLOSED. The camp is founded by the harness; the large stacks are
	/// fixture bodies whose <c>Stacker.StackCount</c> the harness assigns directly
	/// (1,200,000,000 each, and one at exactly <c>int.MaxValue</c>) and which carry
	/// <c>NeverStack</c> so the engine cannot merge them. Ordinary play has never been observed to
	/// produce such a stack; the engine merely permits one (<c>Stacker._StackCount</c> is a plain
	/// <c>int</c> field with no ceiling, serialized through <c>Reader.ReadInt32</c> and merged by
	/// unchecked <c>int</c> addition). No claim is made about reachability through play, about a
	/// rendered Charter, about save/load, or about general camp acceptance.
	/// </para>
	/// </summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomDepositOverflowNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string SetupVerb = "deposit-overflow-setup";
		internal const string CheckVerb = "deposit-overflow-check";
		internal const string Receipt = "r_TAF_ScenarioDepositOverflowNative_v1";
		private static readonly string[] Script = { "stagedigest", SetupVerb, CheckVerb, CheckVerb,
			CheckVerb, CheckVerb, "stagedigest" };

		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }

		public IEnumerable<string> ScenarioVerbs { get { return new[] { SetupVerb, CheckVerb }; } }

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(string.IsNullOrEmpty(Argument) && (Verb == SetupVerb || Verb == CheckVerb),
					"deposit-overflow verbs take no arguments");
				IList<string> script;
				Require(KingdomScenarioScript.TryRead(out script, out _)
					&& script.Count == Script.Length,
					"the exact sealed deposit-overflow script is absent");
				for (int i = 0; i < Script.Length; i++)
					Require(script[i] == Script[i], "the sealed deposit-overflow script differs");
				XRLGame game = The.Game;
				Zone zone = The.Player?.CurrentZone;
				if (Verb == SetupVerb)
				{
					Require(Eligible(game, zone), "requires a fresh stamped camp with the master "
						+ "and growth enabled and no deposit-overflow state already standing");
					game.SetStringGameState(Receipt, "intent");
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
						"the deposit-overflow intent failed its exact readback");
				}
				Require(game != null
					&& KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
					"the deposit-overflow owner intent is absent or torn");
				bool complete;
				string result = KingdomDepositOverflowNativeChecks.Run(Verb, game, zone,
					out complete);
				if (complete)
				{
					Require(ReferenceEquals(The.Game, game),
						"the deposit-overflow report owner changed");
					game.SetStringGameState(Receipt, result);
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, result),
						"the deposit-overflow report failed its exact readback");
				}
				Ok = true;
				return result;
			}
			catch (Exception error) { return KingdomDepositOverflowNativeChecks.Fail(error); }
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
				&& KingdomDepositOverflowNativeChecks.Vacant
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
				Failure ?? "native deposit-overflow evidence refused");
		}
	}
}
