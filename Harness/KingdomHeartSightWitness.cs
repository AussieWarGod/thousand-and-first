using System;
using XRL;
using XRL.World;
using XRL.World.ZoneParts;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomHeartSightWitness
	{
		private const string Key = "r_TAF_ScenarioHeartSight_v1";
		private static string InitialReceipt, Geometry, PreactivationFailure;
		private static XRLGame Preactivated;
		private static int Preactivations;
		private static long OpenedTick;
		internal static string Open()
		{
			Require(InitialReceipt == null && The.Game != null, "heart opening repeated or game absent");
			InitialReceipt = The.ActiveZone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null);
			Require(KingdomFoundingHeartRules.TryDecode(InitialReceipt, out _), "initial founding receipt absent");
			OpenedTick = The.Game.TimeTicks;
			int canvas = 0;
			foreach (GameObject item in The.ActiveZone.GetObjects())
				if (GameObject.Validate(item) && item.Blueprint == "r_KingdomStructureCanvasWall") canvas++;
			return "initial-canvas=" + canvas + "; tick=" + OpenedTick + "; synthetic-completion=false";
		}
		internal static string Inspect()
		{
			Require(InitialReceipt != null && Geometry == null && The.Game.TimeTicks > OpenedTick
				&& The.ActiveZone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null) == InitialReceipt,
				"heart did not retain its founding receipt through ordinary time");
			Geometry = KingdomHeartSightGeometry.Capture(out string detail);
			StartSight(); return detail;
		}
		internal static void StartSight()
		{
			Zone zone = The.ActiveZone;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			var part = zone?.GetPart<KingdomClaimedGroundLight>();
			Require(zone != null && ReferenceEquals(zone, The.Player?.CurrentZone) && system?.Founded == true
				&& system.ClaimedZones.Contains(zone.ZoneID) && KingdomClaimedGround.Enabled
				&& KingdomClaimedGroundLight.CitySightEnabled && part != null
				&& part.SettlementId == system.SettlementIdForOwnedZone(zone.ZoneID), "claimed-zone sight authority absent");
			KingdomQuickstartSightWitness.Start(zone, part);
		}
		internal static string Record(XRLGame game)
		{
			Require(Geometry != null && KingdomHeartSightGeometry.Capture(out _) == Geometry
				&& !KingdomNativeRegressionContext.HasAnyState(game, Key), "heart changed during source frames or witness exists");
			string sight = Sight();
			game.SetStringGameState(Key, Geometry);
			Require(KingdomScenarioDurableState.ProvesExactText(Key, Geometry), "heart witness did not persist exactly");
			return sight + "; witness=recorded; receipt-sha256=" + KingdomScenarioSaveFiles.HashText(Geometry);
		}
		internal static string Sight() => KingdomQuickstartSightWitness.Check(The.ActiveZone,
			The.ActiveZone.GetPart<KingdomClaimedGroundLight>());
		internal static void BeforeActivation(XRLGame game)
		{
			try
			{
				Require(++Preactivations == 1 && Preactivated == null
					&& KingdomScenarioLoadReaderWitness.Releases == 1 && !KingdomScenarioLoadReaderWitness.HadErrors,
					"heart preactivation or reader repeated or failed");
				string stamp = Compare(game);
				Preactivated = game;
				Require(KingdomScenarioJournal.Append("heart-load-preactivation", true, stamp
					+ "; before-AfterGameLoaded=true") == null, "heart preactivation journal unavailable");
			}
			catch (Exception error)
			{
				PreactivationFailure = error.Message;
				KingdomScenarioJournal.Append("heart-load-preactivation", false, error.Message);
			}
		}
		internal static void VerifyLoaded(XRLGame game)
		{
			Require(Preactivations == 1 && PreactivationFailure == null && ReferenceEquals(Preactivated, game),
				"heart preactivation absent or failed: " + PreactivationFailure);
			Require(KingdomScenarioJournal.Append("heart-load-verified", true, Compare(game)) == null,
				"heart loaded journal unavailable");
		}
		internal static string Compare(XRLGame game)
		{
			KingdomHeartSightNativeProvider.RequireScript();
			Require(game != null && ReferenceEquals(game, The.Game)
				&& game.GetSystem<KingdomScenarioAutoRunner>()?.HasConsideredScript == true,
				"loaded game or saved script-considered marker differs");
			string wire = game.GetStringGameState(Key);
			Require(wire != null && wire.Length <= 100000 && KingdomScenarioDurableState.ProvesExactText(Key, wire),
				"saved heart witness absent or malformed");
			Require(KingdomHeartSightGeometry.Capture(out string detail) == wire, "loaded physical heart differs from saved witness");
			return detail + "; same-physical-heart=true; receipt-sha256=" + KingdomScenarioSaveFiles.HashText(wire);
		}
		private static void Require(bool value, string failure) => KingdomHeartSightNativeProvider.Require(value, failure);
	}
}
