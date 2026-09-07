using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomRaidRecoveryReturnNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "raid-recovery-return-native-check";
		internal const string Receipt = "r_TAF_ScenarioRaidRecoveryReturnNative_v1";
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { Verb }; } }
		public string RunScenarioVerb(string verb, string argument, out bool Ok)
		{
			Ok = false;
			if (verb != Verb || !string.IsNullOrEmpty(argument)) return Verb + " takes no arguments";
			XRLGame game = The.Game; Zone zone = The.Player?.CurrentZone;
			try
			{
				if (!Eligible(game, zone, out string failure)) return "native recovery return refused: " + failure;
				game.SetStringGameState(Receipt, "intent");
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "return intent did not persist exactly");
				string report = KingdomRaidRecoveryReturnNativeChecks.Run(game, zone, out Ok);
				Require(ReferenceEquals(The.Game, game) && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"),
					"return intent or game changed");
				game.SetStringGameState(Receipt, report);
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, report), "return report did not persist exactly");
				return report;
			}
			catch (Exception error)
			{
				Ok = false;
				return "native recovery return refused; evidence retained: "
					+ KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
			}
		}
		private static bool Eligible(XRLGame game, Zone zone, out string failure)
		{
			failure = "requires fresh active unfounded marsh ground, enabled raids and native messages";
			if (game == null || zone == null || !ReferenceEquals(The.ZoneManager?.ActiveZone, zone)
				|| !MessageQueue.Enabled || !KingdomRaids.Enabled || !KingdomMaster.ConfiguredEnabled
				|| (game.GetSystem<KingdomSystem>()?.Founded ?? false)
				|| KingdomNativeRegressionContext.HasQuickstartState(game)
				|| KingdomNativeRegressionContext.HasAnyState(game, Receipt)
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidRecoveryDeathNativeProvider.Receipt)
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidDeathNativeProvider.Receipt)
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidContactNativeProvider.Receipt)
				|| KingdomNativeRegressionContext.HasAnyState(game, "r_TAF_ScenarioRaidDeathVetoNative_v1")
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidLaunchNativeProvider.ReceiptA)
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidLaunchNativeProvider.ReceiptB1)
				|| KingdomRaidLaunchNativeFixture.LastAttempt != null || !r_TAF_RaidMintProbe.Vacant
				|| r_TAF_RaidMintProbe.Armed || r_TAF_RaidMintProbe.Book != null) return false;
			if (!KingdomScenarioRealizer.TryBindStampedPlan(out var plan, out _, out failure)) return false;
			if (plan.Key != "founding-first-city" || plan.AuthorityClass != KingdomScenarioFoundingStep.FoundingAuthority
				|| !KingdomScenarioScript.TryRead(out var script, out failure) || script.Count != 3
				|| script[0] != "stagedigest" || script[1] != Verb || script[2] != "stagedigest")
			{ failure = "requires exact sealed recovery-return script and stamped founding plan"; return false; }
			failure = "requires unspent transaction and exact marsh zone";
			if (KingdomScenarioTransactionMarker.Observe(out _) != KingdomScenarioTransactionShape.None
				|| !KingdomQuickstartRules.TryProfile("marsh", out var profile) || zone.ZoneID != profile.ZoneId) return false;
			failure = null; return true;
		}
		internal static void Require(bool value, string failure)
		{ if (!value) throw new InvalidOperationException(failure ?? "native recovery return evidence refused"); }
	}
}
