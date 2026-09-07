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
	public sealed class KingdomRaidRecoveryGuardsNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "raid-recovery-guards-native-check";
		internal const string Receipt = "r_TAF_ScenarioRaidRecoveryGuardsNative_v1";
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { Verb }; } }
		public string RunScenarioVerb(string verb, string argument, out bool Ok)
		{
			Ok = false;
			if (verb != Verb || !string.IsNullOrEmpty(argument)) return Verb + " takes no arguments";
			XRLGame game = The.Game; Zone zone = The.Player?.CurrentZone;
			try
			{
				if (!Eligible(game, zone, out string failure)) return "native recovery guards refused: " + failure;
				game.SetStringGameState(Receipt, "intent");
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "guard intent did not persist exactly");
				string report = KingdomRaidRecoveryGuardsNativeChecks.Run(game, zone, out Ok);
				Require(ReferenceEquals(The.Game, game) && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "guard game/intent changed");
				game.SetStringGameState(Receipt, report);
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, report), "guard report did not persist exactly");
				return report;
			}
			catch (Exception error)
			{
				Ok = false;
				return "native recovery guards refused; evidence retained: " + KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
			}
		}
		private static bool Eligible(XRLGame game, Zone zone, out string failure)
		{
			failure = "requires fresh active unfounded marsh ground, enabled raids and native messages";
			if (game == null || zone == null || !ReferenceEquals(The.ZoneManager?.ActiveZone, zone)
				|| !MessageQueue.Enabled || !KingdomRaids.Enabled || !KingdomMaster.ConfiguredEnabled
				|| (game.GetSystem<KingdomSystem>()?.Founded ?? false) || KingdomSurvey.HasBoundPass
				|| KingdomNativeRegressionContext.HasQuickstartState(game)
				|| KingdomNativeRegressionContext.HasAnyState(game, Receipt)
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidRecoveryDeathNativeProvider.Receipt)
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidRecoveryReturnNativeProvider.Receipt)
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidDeathNativeProvider.Receipt)
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidContactNativeProvider.Receipt)
				|| KingdomNativeRegressionContext.HasAnyState(game, "r_TAF_ScenarioRaidDeathVetoNative_v1")
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidLaunchNativeProvider.ReceiptA)
				|| KingdomNativeRegressionContext.HasAnyState(game, KingdomRaidLaunchNativeProvider.ReceiptB1)
				|| KingdomRaidLaunchNativeFixture.LastAttempt != null || !r_TAF_RaidMintProbe.Vacant
				|| r_TAF_RaidMintProbe.Armed || r_TAF_RaidMintProbe.Book != null || !KingdomRaidRecoveryGuardsScan.Vacant) return false;
			if (!KingdomScenarioRealizer.TryBindStampedPlan(out var plan, out _, out failure)) return false;
			if (plan.Key != "founding-first-city" || plan.AuthorityClass != KingdomScenarioFoundingStep.FoundingAuthority
				|| !KingdomScenarioScript.TryRead(out var script, out failure) || script.Count != 3
				|| script[0] != "stagedigest" || script[1] != Verb || script[2] != "stagedigest")
			{ failure = "requires exact sealed recovery-guards script and stamped founding plan"; return false; }
			failure = "requires unspent transaction and exact marsh zone";
			if (KingdomScenarioTransactionMarker.Observe(out _) != KingdomScenarioTransactionShape.None
				|| !KingdomQuickstartRules.TryProfile("marsh", out var profile) || zone.ZoneID != profile.ZoneId) return false;
			failure = null; return true;
		}
		internal static void Require(bool value, string failure)
		{ if (!value) throw new InvalidOperationException(failure ?? "native recovery guards evidence refused"); }
	}

	// Counts actual method entries; never suppresses a method, changes arguments, or supplies a result.
	[HarmonyPatch(typeof(KingdomSurvey), "TakeCustodyOnly", new Type[] { typeof(Zone) })]
	internal static class KingdomRaidRecoveryGuardsCustodyObserver
	{
		[HarmonyPrefix]
		internal static void Prefix(Zone zone) { KingdomRaidRecoveryGuardsScan.Observe(true, zone); }
	}
	[HarmonyPatch(typeof(Zone), "GetObjects", new Type[] { })]
	internal static class KingdomRaidRecoveryGuardsObjectsObserver
	{
		[HarmonyPrefix]
		internal static void Prefix(Zone __instance) { KingdomRaidRecoveryGuardsScan.Observe(false, __instance); }
	}

	internal sealed class KingdomRaidRecoveryGuardsScan : IDisposable
	{
		private static readonly object Sync = new object();
		private static KingdomRaidRecoveryGuardsScan Current;
		private readonly XRLGame Game;
		private readonly Zone Zone;
		private int Custody, Objects;
		private bool Closed;
		private string Fault;
		internal static bool Vacant { get { lock (Sync) return Current == null; } }
		private KingdomRaidRecoveryGuardsScan(XRLGame game, Zone zone) { Game = game; Zone = zone; }
		internal static KingdomRaidRecoveryGuardsScan Begin(XRLGame game, Zone zone)
		{
			lock (Sync)
			{
				Require(Current == null && game != null && ReferenceEquals(The.Game, game) && zone != null, "scan window owner refused");
				Current = new KingdomRaidRecoveryGuardsScan(game, zone); return Current;
			}
		}
		internal static void Observe(bool custody, Zone zone)
		{
			lock (Sync)
			{
				var current = Current; if (current == null) return;
				try
				{
					if (!ReferenceEquals(The.Game, current.Game) || !ReferenceEquals(zone, current.Zone)) current.Latch("foreign scan owner");
					if (custody) { if (current.Custody < 64) current.Custody++; else current.Latch("custody entry bound exceeded"); }
					else { if (current.Objects < 64) current.Objects++; else current.Latch("objects entry bound exceeded"); }
				}
				catch (Exception error) { current.Latch(error.GetType().Name); }
			}
		}
		private void Latch(string failure) { if (Fault == null) Fault = failure; }
		internal string Verify(int minimum, int maximum)
		{
			lock (Sync)
			{
				Require(Closed && Fault == null && minimum >= 0 && maximum >= minimum && maximum <= 64
					&& Custody >= minimum && Custody <= maximum && Objects == Custody,
					"actual scan counts refused: custody=" + Custody + " objects=" + Objects + " fault=" + Fault);
				return "custody-entries=" + Custody + " objects-entries=" + Objects;
			}
		}
		public void Dispose()
		{
			lock (Sync)
			{
				if (Closed) return;
				Require(ReferenceEquals(Current, this), "scan observer owner was replaced"); Current = null; Closed = true;
			}
		}
		private static void Require(bool value, string failure) { KingdomRaidRecoveryGuardsNativeProvider.Require(value, failure); }
	}
}
