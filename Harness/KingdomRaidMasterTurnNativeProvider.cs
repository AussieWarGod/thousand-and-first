using System;
using System.Collections.Generic;
using HarmonyLib;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomRaidMasterTurnNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string SetupVerb = "raid-recovery-pause-setup", ResumeVerb = "raid-recovery-pause-resume", CheckVerb = "raid-recovery-pause-check";
		internal const string Receipt = "r_TAF_ScenarioRaidMasterTurnNative_v1";
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { SetupVerb, ResumeVerb, CheckVerb }; } }
		public string RunScenarioVerb(string verb, string argument, out bool Ok)
		{
			Ok = false;
			if ((verb != SetupVerb && verb != ResumeVerb && verb != CheckVerb) || !string.IsNullOrEmpty(argument)) return "master turn verbs take no arguments";
			try
			{
				Require(KingdomScenarioScript.TryRead(out var script, out _) && script.Count == 7
					&& script[0] == "stagedigest" && script[1] == SetupVerb && script[2] == "advance 1"
					&& script[3] == ResumeVerb && script[4] == "advance 2" && script[5] == CheckVerb && script[6] == "stagedigest", "exact sealed seven-verb script absent");
				XRLGame game = The.Game; Zone zone = The.Player?.CurrentZone;
				if (verb == SetupVerb)
				{
					Require(Eligible(game, zone), "requires fresh stamped unfounded marsh and vacant probes");
					game.SetStringGameState(Receipt, "intent"); Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "master turn intent did not persist");
				}
				Require(game != null && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "retained master turn intent absent");
				string report = KingdomRaidMasterTurnNativeChecks.Run(verb, game, zone, out Ok);
				Require(ReferenceEquals(The.Game, game) && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "master turn owner/intent changed");
				if (verb == CheckVerb || !Ok)
				{ game.SetStringGameState(Receipt, report); Require(KingdomScenarioDurableState.ProvesExactText(Receipt, report), "master turn report did not persist"); }
				return report;
			}
			catch (Exception error)
			{ Ok = false; return "native master turn refused; evidence retained: " + KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message); }
		}
		private static bool Eligible(XRLGame game, Zone zone)
		{
			if (game == null || zone == null || !ReferenceEquals(The.ZoneManager?.ActiveZone, zone)
				|| !MessageQueue.Enabled || !KingdomRaids.Enabled || !KingdomMaster.ConfiguredEnabled
				|| (game.GetSystem<KingdomSystem>()?.Founded ?? false) || KingdomSurvey.HasBoundPass || KingdomScenarioAdvance.Pending
				|| KingdomNativeRegressionContext.HasQuickstartState(game) || KingdomNativeRegressionContext.HasAnyState(game, Receipt)
				|| KingdomRaidLaunchNativeFixture.LastAttempt != null || !r_TAF_RaidMintProbe.Vacant || r_TAF_RaidMintProbe.Armed || r_TAF_RaidMintProbe.Book != null) return false;
			foreach (string key in new[] { KingdomRaidLaunchNativeProvider.ReceiptA, KingdomRaidLaunchNativeProvider.ReceiptB1,
				KingdomRaidContactNativeProvider.Receipt, KingdomRaidDeathNativeProvider.Receipt, "r_TAF_ScenarioRaidDeathVetoNative_v1",
				KingdomRaidRecoveryDeathNativeProvider.Receipt, KingdomRaidRecoveryReturnNativeProvider.Receipt,
				"r_TAF_ScenarioRaidRecoveryGuardsNative_v1", "r_TAF_ScenarioRaidRecoveryTurnNative_v1" })
				if (KingdomNativeRegressionContext.HasAnyState(game, key)) return false;
			return KingdomScenarioRealizer.TryBindStampedPlan(out var plan, out _, out _) && plan.Key == "founding-first-city"
				&& plan.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority
				&& KingdomScenarioTransactionMarker.Observe(out _) == KingdomScenarioTransactionShape.None
				&& KingdomQuickstartRules.TryProfile("marsh", out var profile) && zone.ZoneID == profile.ZoneId;
		}
		internal static void Require(bool value, string failure)
		{ if (!value) throw new InvalidOperationException(failure ?? "native master turn evidence refused"); }
	}

	// Void observation only: no argument, result, event, or production control flow is replaced.
	[HarmonyPatch(typeof(KingdomSystem), "HandleEvent", new Type[] { typeof(EndTurnEvent) })]
	internal static class KingdomRaidMasterTurnEndObserver
	{
		[HarmonyPrefix] internal static void Prefix(KingdomSystem __instance)
		{ KingdomRaidMasterTurnNativeChecks.Observe(0, __instance, The.Game?.TimeTicks ?? -1, The.ZoneManager?.ActiveZone, false); }
		[HarmonyPostfix] internal static void Postfix(KingdomSystem __instance)
		{ KingdomRaidMasterTurnNativeChecks.Observe(5, __instance, The.Game?.TimeTicks ?? -1, The.ZoneManager?.ActiveZone, false); }
	}
	[HarmonyPatch(typeof(KingdomMaster), "ObserveAutomaticWake", new Type[] { typeof(KingdomSystem), typeof(long) })]
	internal static class KingdomRaidMasterTurnLatchObserver
	{
		[HarmonyPrefix] internal static void Prefix(KingdomSystem system, long now)
		{ KingdomRaidMasterTurnNativeChecks.Observe(1, system, now, The.ZoneManager?.ActiveZone, false); }
		[HarmonyPostfix] internal static void Postfix(KingdomSystem system, long now, bool __result)
		{ KingdomRaidMasterTurnNativeChecks.Observe(2, system, now, The.ZoneManager?.ActiveZone, __result); }
	}
	[HarmonyPatch(typeof(KingdomRaids), "OnWorldWake", new Type[] { typeof(KingdomSystem), typeof(long), typeof(Zone) })]
	internal static class KingdomRaidMasterTurnRaidObserver
	{
		[HarmonyPrefix] internal static void Prefix(KingdomSystem system, long now, Zone currentZone)
		{ KingdomRaidMasterTurnNativeChecks.Observe(3, system, now, currentZone, false); }
		[HarmonyPostfix] internal static void Postfix(KingdomSystem system, long now, Zone currentZone)
		{ KingdomRaidMasterTurnNativeChecks.Observe(4, system, now, currentZone, false); }
	}

	internal sealed class KingdomRaidMasterTurnWitness
	{
		private readonly XRLGame Game;
		private readonly KingdomSystem System;
		private readonly Zone Zone;
		private readonly Action<int> Capture;
		private readonly long Token, Applied, InitialLatchTick;
		private bool EndOpen, MasterOpen, RaidOpen, Setting, LastAllowed;
		private int Ends, Masters, DispatchMasters, DispatchRaids;
		private long LastTick, LastTurns, StartTick, StartTurns;
		private KingdomMasterDecision Decision;
		internal bool Armed = true, Resuming;
		internal int Dispatches, Raids, ResumeApplications;
		internal long DisabledTick = -1, ResumeTick = -1;
		internal string Fault;
		internal KingdomRaidMasterTurnWitness(XRLGame game, KingdomSystem system, Zone zone, Action<int> capture)
		{
			Game = game; System = system; Zone = zone; Capture = capture;
			Token = system.MasterResumeToken; Applied = system.MasterAppliedResumeToken; InitialLatchTick = system.MasterOptionTick;
			StartTick = game.TimeTicks; StartTurns = game.Turns;
			Require(Token == Applied && Token < long.MaxValue && system.MasterOption == KingdomMasterLatchValue.Enabled, "initial master tokens unavailable");
		}
		internal void SetOption(bool enabled)
		{
			Require(!Setting && !EndOpen && !MasterOpen && !RaidOpen && Fault == null, "option transition observer is busy or faulted");
			long turns = Game.Turns, tick = Game.TimeTicks, actions = Game.ActionTicks, playerActions = Game.PlayerActionTicks;
			Resuming = enabled; Setting = true;
			try { Options.SetOption(KingdomMaster.OptionId, enabled ? "Yes" : "No"); }
			finally { Setting = false; }
			Require(ReferenceEquals(The.Game, Game) && ReferenceEquals(Game.GetSystem<KingdomSystem>(), System)
				&& KingdomMaster.ConfiguredEnabled == enabled && Options.GetOption(KingdomMaster.OptionId) == (enabled ? "Yes" : "No")
				&& Game.Turns == turns && Game.TimeTicks == tick && Game.ActionTicks == actions && Game.PlayerActionTicks == playerActions
				&& Fault == null, "option callback changed owner or setting");
			Capture(6);
		}
		internal void BeginResume()
		{
			Verify(1); Require(!Resuming && DisabledTick >= 0 && Raids == 0, "pause did not retain a disabled no-raid interval");
			Dispatches = Ends = Masters = Raids = 0; StartTick = Game.TimeTicks; StartTurns = Game.Turns;
			SetOption(true);
		}
		internal void Observe(int stage, KingdomSystem system, long tick, Zone zone, bool result)
		{
			if (!Armed || Fault != null) return;
			try
			{
				Require(ReferenceEquals(The.Game, Game) && ReferenceEquals(system, System) && ReferenceEquals(zone, Zone)
					&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), System) && tick == Game.TimeTicks
					&& KingdomMaster.ConfiguredEnabled == Resuming, "master observation owner/tick/option differs");
				if (stage == 0)
				{
					Require(!Setting && !EndOpen && !MasterOpen && !RaidOpen && Dispatches == Ends && Dispatches < 32 && KingdomScenarioAdvance.Pending,
						"EndTurn is unowned, reentrant, or exceeds 32 dispatches");
					Require(tick == checked(StartTick + Dispatches) && Game.Turns == checked(StartTurns + Dispatches), "dispatch clocks are not contiguous");
					LastTick = tick; LastTurns = Game.Turns; Dispatches++; DispatchMasters = DispatchRaids = 0; EndOpen = true; Capture(stage); return;
				}
				Require((Setting && (stage == 1 || stage == 2)) || (EndOpen && tick == LastTick && Game.Turns == LastTurns), "observation outside an owned option/EndTurn boundary");
				if (stage == 1)
				{
					Require(!MasterOpen && Masters < 512 && (Setting || DispatchMasters < 16), "master observation reentrant or oversized");
					MasterOpen = true; Masters++; DispatchMasters++;
					Decision = KingdomMasterRules.Observe(System.MasterOption, System.MasterOptionTick, System.MasterResumeToken, System.MasterAppliedResumeToken, Resuming, tick);
					Require(Decision.Valid, "actual master prior state invalid"); Capture(stage);
				}
				else if (stage == 2)
				{
					Require(MasterOpen, "master result has no captured entry"); var expected = Decision;
					if (expected.Transition == KingdomMasterTransition.ResumeRequired)
					{ Require(ResumeApplications == 0, "resume token applied more than once"); expected = KingdomMasterRules.ApplyResume(expected); ResumeApplications++; ResumeTick = tick; }
					if (!Resuming && expected.State == KingdomMasterLatchValue.Disabled) DisabledTick = expected.ChangedAtTick;
					Require(System.MasterOption == expected.State && System.MasterOptionTick == expected.ChangedAtTick
						&& System.MasterResumeToken == expected.ResumeToken && System.MasterAppliedResumeToken == expected.AppliedResumeToken
						&& result == (Decision.Transition == KingdomMasterTransition.None && expected.AutomaticWorkAllowed && expected.ChangedAtTick != tick), "actual master result/latch differs");
					LastAllowed = result; Capture(stage); MasterOpen = false;
				}
				else if (stage == 3)
				{
					Require(EndOpen && !MasterOpen && !RaidOpen && Resuming && ResumeTick >= 0 && tick > ResumeTick && LastAllowed && DispatchRaids < 8,
						"raid wake occurred while paused, during transition, or outside actual allowed heartbeat");
					RaidOpen = true; Raids++; DispatchRaids++; Capture(stage);
				}
				else if (stage == 4) { Require(RaidOpen, "unpaired raid wake exit"); Capture(stage); RaidOpen = false; }
				else if (stage == 5)
				{
					Require(!MasterOpen && !RaidOpen && DispatchMasters > 0, "EndTurn lacks completed actual master observation");
					Require((!Resuming || tick == ResumeTick) ? DispatchRaids == 0 && !LastAllowed : ResumeTick >= 0 && DispatchRaids > 0 && LastAllowed,
						"automatic work did not respect the paused/transition/later-turn boundary");
					Capture(stage); Ends++; EndOpen = false;
				}
				else Require(false, "unknown master observation stage");
			}
			catch (Exception error) { Fault = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message); }
		}
		internal void CurrentLatch()
		{
			long expected = ResumeApplications == 0 ? Token : checked(Token + 1);
			Require(KingdomMaster.ConfiguredEnabled == Resuming && System.MasterResumeToken == expected && System.MasterAppliedResumeToken == (ResumeApplications == 0 ? Applied : expected)
				&& ResumeApplications <= 1 && (ResumeTick < 0 || (System.MasterOption == KingdomMasterLatchValue.Enabled && System.MasterOptionTick == ResumeTick)), "retained master token/epoch changed");
			if (ResumeTick < 0) Require(System.MasterOption == (DisabledTick < 0 ? KingdomMasterLatchValue.Enabled : KingdomMasterLatchValue.Disabled)
				&& System.MasterOptionTick == (DisabledTick < 0 ? InitialLatchTick : DisabledTick), "pre-resume latch changed");
			Require(KingdomMaster.AutomaticWorkAllowed(System) == (Resuming && ResumeTick >= 0 && Game.TimeTicks != ResumeTick), "automatic gate disagrees with observed transition epoch");
		}
		internal void Verify(int minimum)
		{
			Require(Armed && Fault == null && !Setting && !EndOpen && !MasterOpen && !RaidOpen && Dispatches >= minimum && Dispatches == Ends
				&& !KingdomScenarioAdvance.Pending && Game.Turns == checked(StartTurns + Dispatches) && Game.TimeTicks == checked(StartTick + Dispatches)
				&& Game.Turns == checked(LastTurns + 1) && Game.TimeTicks == checked(LastTick + 1), "actual advance clocks/observations incomplete: " + Fault);
			CurrentLatch();
		}
		private static void Require(bool value, string failure) { KingdomRaidMasterTurnNativeProvider.Require(value, failure); }
	}
}
