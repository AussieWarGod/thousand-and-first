using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using XRL;
using XRL.World;
using XRL.UI;

namespace ThousandAndFirst.Harness
{
	// External saved-world witness only; never publishes scenario state or allocates an ID.
	internal static class KingdomQuickstartSaveState
	{
		internal static KingdomQuickstartSaveSnapshot Capture(XRLGame Game, string Seed,
			KingdomQuickstartBootRequest Request)
		{
			Check(Game != null && ReferenceEquals(The.Game, Game) && Request?.Save == true,
				"save witness does not own the genuine Quickstart request");
			KingdomQuickstartSaveSnapshot snapshot = Read(Game, Seed, Request);
			Verify(Game, snapshot);
			return snapshot;
		}

		internal static void Verify(XRLGame Game, KingdomQuickstartSaveSnapshot Snapshot)
		{
			Check(KingdomQuickstartSaveSnapshotCodec.TryEncode(Snapshot, out string expected),
				"saved Quickstart witness cannot encode");
			Check(KingdomQuickstartBootRequest.TryParse(new[] { KingdomQuickstartBootRequest.SaveVerb
				+ " " + Snapshot.ProfileKey + " " + (Snapshot.Advisor ? "yes" : "no") }, out var request)
				&& KingdomQuickstartRules.TryProfile(Snapshot.ProfileKey, out _), "saved profile is not canonical");
			Check(KingdomQuickstartSaveSnapshotCodec.TryEncode(Read(Game, Snapshot.Seed, request), out string before)
				&& before == expected, "saved-world fields differ before physical verification");
			KingdomQuickstartRules.TryProfile(Snapshot.ProfileKey, out var profile);
			int waterDrams = InitialWater(Snapshot, expected);
			Check(KingdomQuickstartBootstrap.NativeVerifyFreshBoot(Game, The.Player, The.ZoneManager?.ActiveZone,
				profile, Snapshot.Advisor, waterDrams, out string failure), failure);
			Check(KingdomQuickstartSaveSnapshotCodec.TryEncode(Read(Game, Snapshot.Seed, request), out string after)
				&& after == expected, "saved-world fields changed during physical verification");
		}

		private static int InitialWater(KingdomQuickstartSaveSnapshot Snapshot, string Wire)
		{
			string path = Path.Combine(KingdomScenarioSaveFiles.Root(), "Local", KingdomQuickstartHistoricalGrant.FileName);
			if (!File.Exists(path) && !Directory.Exists(path)) return KingdomQuickstartRules.StarterWaterDrams;
			Check(KingdomScenarioLoadEntry.Armed && ReferenceEquals(KingdomScenarioLoadEntry.QuickstartSnapshot, Snapshot)
				&& KingdomScenarioLoadEntry.SnapshotWire == Wire,
				"historical grant witness does not own this sealed load");
			Check(KingdomQuickstartHistoricalGrant.TryRead(
				KingdomScenarioSaveFiles.ReadText(path, KingdomQuickstartHistoricalGrant.MaxBytes),
				Snapshot.GameId, KingdomScenarioSaveFiles.HashText(Wire), out int drams),
				"historical grant witness differs from the exact public source and saved snapshot");
			return drams;
		}

		private static KingdomQuickstartSaveSnapshot Read(XRLGame Game, string Seed,
			KingdomQuickstartBootRequest Request)
		{
			Check(Game != null && ReferenceEquals(The.Game, Game) && Request != null,
				"saved-world game ownership changed");
			GameObject founder = The.Player;
			Zone zone = The.ZoneManager?.ActiveZone;
			Check(GameObject.Validate(founder) && founder._BaseID > 0 && zone != null
				&& ReferenceEquals(Game.ZoneManager, The.ZoneManager) && ReferenceEquals(founder.CurrentZone, zone)
				&& KingdomScenarioDurableState.ProvesExactText("OriginalWorldSeed", Seed)
				&& KingdomScenarioScript.TryRead(out IList<string> script, out _)
				&& KingdomQuickstartBootRequest.TryParse(script, out var selected)
				&& selected.Save && selected.Command == Request.Command
				&& Options.GetOption(KingdomQuickstartRules.AdvisorOption) == (Request.Advisor ? "Yes" : "No")
				&& Game.GetSystem<KingdomScenarioAutoRunner>() == null
				&& !KingdomNativeRegressionContext.HasAnyState(Game, KingdomScenarioNewGameGate.RequestState)
				&& !KingdomNativeRegressionContext.HasAnyState(Game, KingdomScenarioSaveFiles.SnapshotKey),
				"saved-world founder, seed, advisor or scenario exclusion differs");
			string founderId = null;
			Check(founder.IntProperty?.ContainsKey("id") != true, "founder ID has foreign typed authority");
			if (founder.Property != null && founder.Property.TryGetValue("id", out founderId))
				Check(founderId != null, "founder ID has a stored null value");
			string heart = ZoneText(Game.ZoneManager, zone, KingdomPlots.FoundingHeartReceiptProperty, false);
			string seal = ZoneText(Game.ZoneManager, zone, KingdomPlots.FoundingHeartSealProperty, false);
			string terminal = ZoneText(Game.ZoneManager, zone, KingdomPlots.FoundingHeartTerminalProperty, true);
			Check(KingdomFoundingHeartRules.TryDecode(heart, out var plan)
				&& KingdomFoundingHeartRules.Complete(plan) && plan.ZoneId == zone.ZoneID,
				"saved-world heart is not the exact complete founding plan");
			string receipt = Game.GetStringGameState(KingdomQuickstartRules.ReceiptState, null);
			Check(KingdomScenarioDurableState.ProvesExactText(KingdomQuickstartRules.ReceiptState, receipt),
				"saved-world Quickstart receipt has foreign typed authority");
			return new KingdomQuickstartSaveSnapshot(Game.GameID, Seed, Request.ProfileKey, Request.Advisor,
				founder._BaseID, founderId, Game.Turns, Game.TimeTicks, Game.ActionTicks, Game.PlayerActionTicks,
				receipt, heart, seal, terminal, Reservations(plan));
		}

		private static string ZoneText(ZoneManager Manager, Zone Zone, string Key, bool Optional)
		{
			Dictionary<string, object> rows = null;
			Check(Manager.ZoneProperties != null && Manager.ZoneProperties.TryGetValue(Zone.ZoneID, out rows)
				&& rows != null && (ReferenceEquals(rows.Comparer, StringComparer.Ordinal)
					|| ReferenceEquals(rows.Comparer, EqualityComparer<string>.Default)), "zone property table differs");
			if (!rows.TryGetValue(Key, out object value))
			{
				Check(Optional, "required founding zone property is absent");
				return null;
			}
			Check(value is string, "founding zone property has null or foreign type");
			return (string)value;
		}

		private static string Reservations(KingdomFoundingHeartPlan Plan)
		{
			StringBuilder wire = new StringBuilder("qhr1:");
			for (int i = 0; i <= KingdomFoundingHeartRules.SlotCount; i++)
			{
				string role = i == KingdomFoundingHeartRules.SlotCount ? "final" : "slot-" + i;
				string id = KingdomFoundingHeartRules.StableId(Plan.TransactionId, Plan.ZoneId, role);
				string key = KingdomFoundingHeartReservationRules.Prefix + id;
				string expected = KingdomFoundingHeartReservationRules.Encode(Plan, id, role);
				Check(expected != null && KingdomScenarioDurableState.ProvesExactText(key, expected),
					"founding reservation does not match its saved heart authority");
				wire.Append(key.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(key);
				wire.Append(expected.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(expected);
			}
			return wire.ToString();
		}

		private static void Check(bool Condition, string Failure)
		{ KingdomScenarioSaveFiles.Require(Condition, Failure ?? "Quickstart save state refused"); }
	}
}
