using System;
using System.Collections.Generic;
using HarmonyLib;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomRaidRecoveryTurnNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string SetupVerb = "raid-recovery-turn-setup", CheckVerb = "raid-recovery-turn-check";
		internal const string Receipt = "r_TAF_ScenarioRaidRecoveryTurnNative_v1";
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { SetupVerb, CheckVerb }; } }
		public string RunScenarioVerb(string verb, string argument, out bool Ok)
		{
			Ok = false;
			if ((verb != SetupVerb && verb != CheckVerb) || !string.IsNullOrEmpty(argument)) return "recovery turn verbs take no arguments";
			XRLGame game = The.Game; Zone zone = The.Player?.CurrentZone;
			try
			{
				Require(ExactScript(), "requires exact sealed five-verb recovery-turn script");
				if (verb == SetupVerb)
				{
					Require(Eligible(game, zone), "requires fresh stamped unfounded marsh and vacant native probes");
					game.SetStringGameState(Receipt, "intent");
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "turn intent did not persist exactly");
					return KingdomRaidRecoveryTurnNativeChecks.Setup(game, zone, out Ok);
				}
				Require(game != null && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "exact retained turn intent absent");
				string report = KingdomRaidRecoveryTurnNativeChecks.Check(game, zone, out Ok);
				Require(ReferenceEquals(The.Game, game) && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "turn game/intent changed");
				game.SetStringGameState(Receipt, report);
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, report), "turn report did not persist exactly");
				return report;
			}
			catch (Exception error)
			{
				Ok = false;
				return "native recovery turn refused; evidence retained: " + KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
			}
		}
		internal static bool ExactScript()
		{
			return KingdomScenarioScript.TryRead(out var script, out _) && script.Count == 5
				&& script[0] == "stagedigest" && script[1] == SetupVerb && script[2] == "advance 1"
				&& script[3] == CheckVerb && script[4] == "stagedigest";
		}
		private static bool Eligible(XRLGame game, Zone zone)
		{
			if (game == null || zone == null || !ReferenceEquals(The.ZoneManager?.ActiveZone, zone)
				|| !MessageQueue.Enabled || !KingdomRaids.Enabled || !KingdomMaster.ConfiguredEnabled
				|| (game.GetSystem<KingdomSystem>()?.Founded ?? false) || KingdomSurvey.HasBoundPass || KingdomScenarioAdvance.Pending
				|| KingdomNativeRegressionContext.HasQuickstartState(game) || KingdomNativeRegressionContext.HasAnyState(game, Receipt)
				|| KingdomRaidLaunchNativeFixture.LastAttempt != null || !r_TAF_RaidMintProbe.Vacant
				|| r_TAF_RaidMintProbe.Armed || r_TAF_RaidMintProbe.Book != null) return false;
			foreach (string key in new[] { KingdomRaidLaunchNativeProvider.ReceiptA, KingdomRaidLaunchNativeProvider.ReceiptB1,
				KingdomRaidContactNativeProvider.Receipt, KingdomRaidDeathNativeProvider.Receipt,
				"r_TAF_ScenarioRaidDeathVetoNative_v1", KingdomRaidRecoveryDeathNativeProvider.Receipt,
				KingdomRaidRecoveryReturnNativeProvider.Receipt, "r_TAF_ScenarioRaidRecoveryGuardsNative_v1" })
				if (KingdomNativeRegressionContext.HasAnyState(game, key)) return false;
			return KingdomScenarioRealizer.TryBindStampedPlan(out var plan, out _, out _)
				&& plan.Key == "founding-first-city" && plan.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority
				&& KingdomScenarioTransactionMarker.Observe(out _) == KingdomScenarioTransactionShape.None
				&& KingdomQuickstartRules.TryProfile("marsh", out var profile) && zone.ZoneID == profile.ZoneId;
		}
		internal static void Require(bool value, string failure)
		{ if (!value) throw new InvalidOperationException(failure ?? "native recovery-turn evidence refused"); }
	}

	// These void observers leave the original event handlers, arguments and results untouched.
	[HarmonyPatch(typeof(KingdomSystem), "HandleEvent", new Type[] { typeof(EndTurnEvent) })]
	internal static class KingdomRaidRecoveryTurnEndObserver
	{
		[HarmonyPrefix] internal static void Prefix(KingdomSystem __instance)
		{ KingdomRaidRecoveryTurnNativeChecks.Observe(0, __instance, The.Game?.TimeTicks ?? -1, The.ZoneManager?.ActiveZone); }
		[HarmonyPostfix] internal static void Postfix(KingdomSystem __instance)
		{ KingdomRaidRecoveryTurnNativeChecks.Observe(3, __instance, The.Game?.TimeTicks ?? -1, The.ZoneManager?.ActiveZone); }
	}
	[HarmonyPatch(typeof(KingdomRaids), "OnWorldWake", new Type[] { typeof(KingdomSystem), typeof(long), typeof(Zone) })]
	internal static class KingdomRaidRecoveryTurnWakeObserver
	{
		[HarmonyPrefix] internal static void Prefix(KingdomSystem system, long now, Zone currentZone)
		{ KingdomRaidRecoveryTurnNativeChecks.Observe(1, system, now, currentZone); }
		[HarmonyPostfix] internal static void Postfix(KingdomSystem system, long now, Zone currentZone)
		{ KingdomRaidRecoveryTurnNativeChecks.Observe(2, system, now, currentZone); }
	}

	internal sealed class KingdomRaidRecoveryTurnWitness
	{
		private readonly XRLGame Game;
		private readonly KingdomSystem System;
		private readonly Zone Zone;
		private readonly Action<int> Capture;
		internal bool Armed = true;
		internal int Dispatches { get; private set; }
		internal int Wakes { get; private set; }
		private int Ends, DispatchWakes;
		private bool EndOpen, WakeOpen;
		private readonly long[] DispatchTicks = new long[32], DispatchTurns = new long[32];
		internal string Fault { get; private set; }
		internal long ReadyTick { get; private set; }
		internal long LastDispatchTick { get; private set; }
		internal long LastDispatchTurns { get; private set; }
		internal KingdomRaidRecoveryTurnWitness(XRLGame game, KingdomSystem system, Zone zone, Action<int> capture)
		{ Game = game; System = system; Zone = zone; Capture = capture; }
		internal void Observe(int stage, KingdomSystem system, long tick, Zone zone)
		{
			if (!Armed || Fault != null) return;
			try
			{
				KingdomRaidRecoveryTurnNativeProvider.Require(stage >= 0 && stage < 4
					&& ReferenceEquals(The.Game, Game) && ReferenceEquals(system, System) && ReferenceEquals(zone, Zone)
					&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), System) && tick == Game.TimeTicks
					&& KingdomScenarioAdvance.Pending, "unexpected event order, owner, tick, or non-advance wake");
				if (stage == 0)
				{
					KingdomRaidRecoveryTurnNativeProvider.Require(!EndOpen && !WakeOpen && Dispatches == Ends && Dispatches < 32,
						"EndTurn is reentrant, unpaired, or exceeds 32-dispatch bound");
					if (Dispatches > 0) KingdomRaidRecoveryTurnNativeProvider.Require(tick == checked(LastDispatchTick + 1)
						&& Game.Turns == checked(LastDispatchTurns + 1), "observed EndTurn clocks are not contiguous");
					LastDispatchTick = tick; LastDispatchTurns = Game.Turns;
					DispatchTicks[Dispatches] = tick; DispatchTurns[Dispatches] = Game.Turns;
					Dispatches++; DispatchWakes = 0; EndOpen = true; Capture(stage); return;
				}
				KingdomRaidRecoveryTurnNativeProvider.Require(EndOpen && tick == LastDispatchTick && Game.Turns == LastDispatchTurns,
					"event outside EndTurn or clock changed within dispatch");
				if (stage == 1)
				{
					KingdomRaidRecoveryTurnNativeProvider.Require(!WakeOpen && DispatchWakes < 8, "reentrant wake or per-dispatch wake bound exceeded");
					WakeOpen = true; DispatchWakes++; Wakes++;
					if (Wakes == 1) ReadyTick = tick;
					Capture(stage);
				}
				else if (stage == 2)
				{
					KingdomRaidRecoveryTurnNativeProvider.Require(WakeOpen, "unpaired raid wake exit");
					Capture(stage); WakeOpen = false;
				}
				else
				{
					KingdomRaidRecoveryTurnNativeProvider.Require(!WakeOpen && DispatchWakes > 0, "EndTurn did not enclose a completed raid wake");
					Capture(stage); Ends++; EndOpen = false;
				}
			}
			catch (Exception error) { Fault = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message); }
		}
		internal void Verify()
		{ KingdomRaidRecoveryTurnNativeProvider.Require(Armed && Dispatches > 0 && Dispatches == Ends && !EndOpen && !WakeOpen && Wakes > 0 && Fault == null,
			"real turn witness incomplete: dispatches=" + Dispatches + " exits=" + Ends + " wakes=" + Wakes + " fault=" + Fault); }
	}
}
