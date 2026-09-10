using System;
using System.Collections.Generic;
using HarmonyLib;
using XRL;
using XRL.CharacterBuilds;
using XRL.CharacterBuilds.Qud;
using XRL.UI;
using XRL.World;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst.Harness
{
	// Observes genuine production boot; never realizes a scenario or grants replacement state.
	internal static class KingdomQuickstartBootTest
	{
		private static EmbarkBuilder Builder;
		private static EmbarkInfo Info;
		private static KingdomQuickstartBootRequest Request;
		private static KingdomQuickstartProfile Profile;
		private static XRLGame Game;
		private static GameObject Founder;
		private static Zone Zone, PreparedZone;
		private static string Seed, Failure, ObservedReceipt;
		private static int WorldCalls, CampCalls, RunCalls, Observations;
		private static bool Begun, Ended, OwnSuppression, RunSucceeded, Verified;
		private static bool? StartReachableBefore;

		internal static string SelectMode(EmbarkBuilder Selected)
		{
			if (!KingdomScenarioScript.TryRead(out IList<string> script, out _)
				|| script.Count == 0 || !script[0].StartsWith("quickstart-",
					StringComparison.Ordinal)) return KingdomScenarioFastEmbarkModule.ModeId;
			if (Builder != null || Selected?.info == null
				|| !KingdomQuickstartBootRequest.TryParse(script, out var request)
				|| !KingdomQuickstartRules.TryProfile(request.ProfileKey, out var profile))
			{
				KingdomScenarioJournal.Append("QUICKSTART-BOOT-REFUSED", false, "invalid or repeated boot request");
				return null;
			}
			Builder = Selected; Info = Selected.info; Request = request; Profile = profile;
			return KingdomQuickstartRules.ModeId;
		}

		internal static bool Selected(EmbarkBuilder Candidate)
		{
			return Builder != null && ReferenceEquals(Builder, Candidate)
				&& ReferenceEquals(Candidate.info, Info) && Request != null && !Begun && !Ended;
		}

		internal static string StartingLocation(EmbarkBuilder Candidate)
		{
			return Selected(Candidate) ? Profile.LocationId : KingdomScenarioFastEmbarkModule.StartingLocationId;
		}

		internal static bool Prepare(EmbarkBuilder Candidate, string FrozenSeed)
		{
			if (!Selected(Candidate) || string.IsNullOrEmpty(FrozenSeed) || !ExactScript()
				|| Options.GetOption(KingdomQuickstartRules.AdvisorOption) != (Request.Advisor ? "Yes" : "No"))
				return false;
			Seed = FrozenSeed;
			return true;
		}

		internal static void Begin(EmbarkInfo Candidate, XRLGame Current)
		{
			if (!ReferenceEquals(Candidate, Info) || Request == null) return;
			if (Begun || Ended) { Fail("boot repeated"); return; }
			Begun = true; Game = Current;
			if (Game == null || !ReferenceEquals(The.Game, Game) || string.IsNullOrEmpty(Seed)
				|| Candidate.GameSeed != Seed || !ExactScript()
				|| Builder.GetModule<QudGamemodeModule>()?.GetMode() != KingdomQuickstartRules.ModeId)
				Fail("boot owner, mode, seed or script disagreed");
			OwnSuppression = !Popup.Suppress;
			if (OwnSuppression) Popup.Suppress = true;
			KingdomScenarioJournal.Append("QUICKSTART-BOOT-BEGIN", Failure == null,
				Request.Command + "; seed=" + Seed + "; genuine-production-boot=true");
		}

		private static bool Active { get { return Begun && !Ended && ReferenceEquals(The.Game, Game); } }
		internal static void WorldBuilt()
		{
			if (Active) WorldCalls++;
		}
		internal static void CampEntering(Zone Built)
		{
			if (!Active || Built?.ZoneID != Profile.ZoneId) return;
			if (StartReachableBefore.HasValue) Fail("camp preparation entry repeated");
			else StartReachableBefore = Built.IsReachable(
				KingdomQuickstartRules.StartCellX, KingdomQuickstartRules.StartCellY);
		}
		internal static void CampBuilt(Zone Built, bool Success)
		{
			if (!Active || Built?.ZoneID != Profile.ZoneId) return;
			CampCalls++;
			if (!Success) Fail("production camp builder refused");
			if (CampCalls != 1 || !Built.Built || The.Player != null)
				Fail("camp did not run once after zone generation and before founder placement");
			PreparedZone = Built;
		}

		internal static void BeforeRun(XRLGame Current)
		{
			KingdomQuickstartSaveTest.BootstrapCalled(Current);
			KingdomQuickstartLoadTest.BootstrapCalled(Current);
			if (!Active || !ReferenceEquals(Current, Game)) return;
			RunCalls++;
			Founder = The.Player; Zone = The.ZoneManager?.ActiveZone;
			if (CampCalls != 1 || !ReferenceEquals(PreparedZone, Zone))
				Fail("bootstrap lacks its exact completed camp; world=" + WorldCalls + "; camp=" + CampCalls);
			bool reachable = Zone?.IsReachable(KingdomQuickstartRules.StartCellX,
				KingdomQuickstartRules.StartCellY) == true;
			if (!StartReachableBefore.HasValue || !reachable) Fail("prepared founder cell is not engine-reachable");
			MetricsManager.LogInfo("[TAF] quickstart camp reachability: before=" + StartReachableBefore
				+ "; after=" + reachable);
			if (RunCalls != 1 || !GameObject.Validate(Founder) || Founder.CurrentCell == null
				|| !ReferenceEquals(Founder.CurrentZone, Zone) || Zone?.ZoneID != Profile.ZoneId
				|| Founder.CurrentCell.X != KingdomQuickstartRules.StartCellX
				|| Founder.CurrentCell.Y != KingdomQuickstartRules.StartCellY
				|| Game.GetSystem<KingdomSystem>()?.Founded == true
				|| KingdomNativeRegressionContext.HasAnyState(Game, KingdomQuickstartRules.ReceiptState)
				|| KingdomNativeRegressionContext.HasAnyState(Game, KingdomScenarioNewGameGate.RequestState))
				Fail("bootstrap did not enter with the actual placed founder and an unseeded receipt");
		}

		internal static void AfterRun(XRLGame Current, bool Success)
		{
			if (!Active || !ReferenceEquals(Current, Game)) return;
			RunSucceeded = Success;
			if (!Success) Fail("production bootstrap refused; world=" + WorldCalls + "; camp=" + CampCalls);
		}

		internal static void Observe(EmbarkInfo Candidate, string Id, XRLGame Current)
		{
			if (!Active || !ReferenceEquals(Candidate, Info) || !ReferenceEquals(Current, Game)
				|| Id != QudGameBootModule.BOOTEVENT_GAMESTARTING) return;
			Observations++;
			try
			{
				if (Observations != 1 || WorldCalls != 1 || CampCalls != 1 || RunCalls != 1
					|| !RunSucceeded || !ExactScript() || Game.GetStringGameState("OriginalWorldSeed", null) != Seed
					|| Game.GetSystem<KingdomScenarioAutoRunner>() != null)
					Fail("production hook counts, seed or runner exclusion disagreed");
				if (!KingdomQuickstartBootstrap.NativeVerifyFreshBoot(Game, Founder, Zone,
					Profile, Request.Advisor, out string failure)) Fail(failure);
				Verified = Failure == null;
				if (Verified) ObservedReceipt = Game.GetStringGameState(KingdomQuickstartRules.ReceiptState, null);
			}
			catch (Exception error) { Fail("observer exception: " + error.GetType().Name); }
			KingdomScenarioJournal.Append("QUICKSTART-BOOT-OBSERVED", Verified,
				Failure ?? "actual founded heart, finite single grants and advisor verified after GAMESTARTING");
		}

		internal static void End(EmbarkInfo Candidate, Exception Error)
		{
			if (!ReferenceEquals(Candidate, Info) || !Begun || Ended) return;
			try
			{
				if (Error != null) Fail("boot exception: " + Error.GetType().Name);
				if (!ExactCompletion(Candidate)) Fail("boot completion was not proved");
				if (Failure == null && !KingdomQuickstartBootstrap.NativeVerifyFreshBoot(Game, Founder, Zone,
					Profile, Request.Advisor, out string failure)) Fail(failure);
				if (!ExactCompletion(Candidate)) Fail("boot completion authority changed during final verification");
				KingdomScenarioJournal.Append("QUICKSTART-BOOT-COMPLETE", Failure == null,
					(Failure ?? Request.Command) + "; boot-only=true; save-load=false; ordinary-acceptance=false");
				// The build phase never starts before this row: boot-only=true above always
				// observes a COMPLETED boot first, unmodified, whether or not a build follows.
				if (Failure == null && Request.Build) KingdomQuickstartBuildTest.Run(Game, Zone, ObservedReceipt, Request.Command);
			}
			catch (Exception error)
			{
				Fail("boot completion observer exception: " + error.GetType().Name);
				KingdomScenarioJournal.Append("QUICKSTART-BOOT-COMPLETE", false, Failure);
			}
			finally { Ended = true; if (OwnSuppression) Popup.Suppress = false; }
		}

		private static bool ExactCompletion(EmbarkInfo Candidate)
		{
			return Active && Verified && ReferenceEquals(Candidate, Info) && Observations == 1
				&& WorldCalls == 1 && CampCalls == 1 && RunCalls == 1 && ExactScript()
				&& Candidate.GameSeed == Seed && Game.GetStringGameState("OriginalWorldSeed", null) == Seed
				&& Game.GetSystem<KingdomScenarioAutoRunner>() == null
				&& !KingdomNativeRegressionContext.HasAnyState(Game, KingdomScenarioNewGameGate.RequestState)
				&& Options.GetOption(KingdomQuickstartRules.AdvisorOption) == (Request.Advisor ? "Yes" : "No")
				&& Game.GetStringGameState(KingdomQuickstartRules.ReceiptState, null) == ObservedReceipt;
		}

		private static bool ExactScript()
		{
			return Request != null && KingdomScenarioScript.TryRead(out IList<string> script, out _)
				&& KingdomQuickstartBootRequest.TryParse(script, out var observed)
				&& observed.Command == Request.Command;
		}

		internal static bool ClaimsSave(XRLGame Current)
		{
			return Request?.Save == true && Begun && Ended && Game != null
				&& ReferenceEquals(Game, Current) && ReferenceEquals(The.Game, Game);
		}

		internal static void VerifyForSave(XRLGame Current, out string FrozenSeed,
			out KingdomQuickstartBootRequest SelectedRequest)
		{
			FrozenSeed = Seed; SelectedRequest = Request;
			KingdomScenarioSaveFiles.Require(ClaimsSave(Current) && Failure == null && Verified
				&& Observations == 1 && WorldCalls == 1 && CampCalls == 1 && RunCalls == 1
				&& RunSucceeded && ExactScript() && ReferenceEquals(Info, Builder?.info)
				&& Info.GameSeed == Seed && Game.GetStringGameState("OriginalWorldSeed", null) == Seed
				&& Game.GetStringGameState(KingdomQuickstartRules.ReceiptState, null) == ObservedReceipt
				&& ReferenceEquals(The.Player, Founder) && ReferenceEquals(The.ZoneManager?.ActiveZone, Zone),
				"save lacks an exact successful genuine boot witness");
		}
		private static void Fail(string Reason) { if (Failure == null) Failure = Reason ?? "unspecified refusal"; }
	}

	[HarmonyPatch(typeof(EmbarkInfo), "bootGame", new Type[] { typeof(XRLGame) })]
	internal static class KingdomQuickstartBootBoundaryPatch
	{
		[HarmonyPrefix]
		internal static void Prefix(EmbarkInfo __instance, XRLGame game) { KingdomQuickstartBootTest.Begin(__instance, game); }
		[HarmonyFinalizer]
		internal static Exception Finalizer(EmbarkInfo __instance, Exception __exception)
		{
			KingdomQuickstartBootTest.End(__instance, __exception);
			return __exception;
		}
	}

	[HarmonyPatch(typeof(EmbarkInfo), "fireBootEvent", new Type[] { typeof(string), typeof(XRLGame) })]
	internal static class KingdomQuickstartBootObservationPatch
	{
		[HarmonyPostfix]
		internal static void Postfix(EmbarkInfo __instance, string id, XRLGame game)
		{ KingdomQuickstartBootTest.Observe(__instance, id, game); }
	}

	[HarmonyPatch(typeof(KingdomQuickstartWorldExtension), "OnAfterMutableInit")]
	internal static class KingdomQuickstartWorldObservationPatch
	{
		[HarmonyPostfix]
		internal static void Postfix() { KingdomQuickstartBootTest.WorldBuilt(); }
	}

	[HarmonyPatch(typeof(KingdomQuickstartCampBuilder), "BuildZone")]
	internal static class KingdomQuickstartCampObservationPatch
	{
		[HarmonyPrefix]
		internal static void Prefix(Zone Z) { KingdomQuickstartBootTest.CampEntering(Z); }
		[HarmonyPostfix]
		internal static void Postfix(Zone Z, bool __result) { KingdomQuickstartBootTest.CampBuilt(Z, __result); }
	}

	[HarmonyPatch(typeof(KingdomQuickstartBootstrap), "Run")]
	internal static class KingdomQuickstartRunObservationPatch
	{
		[HarmonyPrefix]
		internal static void Prefix(XRLGame Game) { KingdomQuickstartBootTest.BeforeRun(Game); }
		[HarmonyPostfix]
		internal static void Postfix(XRLGame Game, bool __result) { KingdomQuickstartBootTest.AfterRun(Game, __result); }
	}
}
