using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ThousandAndFirst.Simulation.City;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomHomeMapSaveWitness
	{
		private const string Key = "r_TAF_ScenarioHomeMapSave_v1";
		private static XRLGame Preactivated;
		private static int Attempts;
		private static string Failure;

		internal static string Record(XRLGame Game)
		{
			Require(!KingdomScenarioSaveFiles.LoadPresent() && !KingdomNativeRegressionContext.HasAnyState(Game, Key),
				"home witness exists or is recording on load");
			string wire = Capture(Game);
			Game.SetStringGameState(Key, wire);
			Require(KingdomScenarioDurableState.ProvesExactText(Key, wire), "home witness did not persist exactly");
			return Stamp(Game) + "; witness=recorded; world-repair=false";
		}

		internal static void BeforeActivation(XRLGame Game)
		{
			try
			{
				Require(++Attempts == 1 && Preactivated == null && KingdomScenarioLoadReaderWitness.Releases == 1
					&& !KingdomScenarioLoadReaderWitness.HadErrors, "home preactivation or reader repeated or failed");
				Compare(Game); Preactivated = Game;
				Require(KingdomScenarioJournal.Append("home-map-load-preactivation", true,
					Stamp(Game) + "; before-AfterGameLoaded=true; world-repair=false") == null, "home journal unavailable");
			}
			catch (Exception error)
			{
				Failure = error.Message;
				KingdomScenarioJournal.Append("home-map-load-preactivation", false, Failure);
			}
		}

		internal static void VerifyLoaded(XRLGame Game)
		{
			Require(Attempts == 1 && Failure == null && ReferenceEquals(Preactivated, Game),
				"home preactivation absent or failed: " + Failure);
			Compare(Game);
			Require(KingdomScenarioJournal.Append("home-map-load-verified", true,
				Stamp(Game) + "; world-repair=false") == null, "home loaded journal unavailable");
			string visit = KingdomHomeMapNativeProvider.RepeatLoadedVisit();
			Compare(Game);
			Require(KingdomScenarioJournal.Append("home-map-load-travel", true, visit) == null,
				"home return journal unavailable");
		}

		private static void Compare(XRLGame Game)
		{
			Require(KingdomScenarioLoadEntry.Armed && KingdomScenarioLoadEntry.LifecycleSnapshot != null,
				"home witness does not own a sealed lifecycle load");
			string wire = Game.GetStringGameState(Key);
			Require(wire != null && wire.Length <= 32768 && KingdomScenarioDurableState.ProvesExactText(Key, wire)
				&& Capture(Game) == wire, "saved home identities, profiles, bodies or claim maps differ");
		}

		private static string Stamp(XRLGame Game) => "residents=4; maps=2; exact-homes=true; receipt-sha256="
			+ KingdomScenarioSaveFiles.HashText(Game.GetStringGameState(Key));

		private static string Capture(XRLGame Game)
		{
			KingdomHomeMapSaveProvider.RequireScript();
			KingdomSystem system = Game?.GetSystem<KingdomSystem>();
			Zone zone = The.Player?.CurrentZone;
			Require(Game != null && ReferenceEquals(Game, The.Game) && zone != null
				&& system?.Founded == true && system.ClaimedZones.Count == 2
				&& system.City.HasValidSubsidenceStorage(), "saved home authority is unavailable");
			Require(system.City.TryReadExact(out KingdomCityState state, out _) && state.ResidentCount == 4,
				"saved home roster differs");
			Require(system.Bindings != null && system.Bindings.TryReadExact(out _, out _),
				"saved resident binding authority unavailable");
			system.Bindings.TryReadExact(out KingdomBindingTable bindings, out _);
			Require(KingdomQuickstartRules.TryDecode(Game.GetStringGameState(KingdomQuickstartRules.ReceiptState),
				out var receipt) && receipt.FounderObjectIds.Length == 4, "original four founder identities absent");
			var maps = new List<string>(system.ClaimedZones); maps.Sort(StringComparer.Ordinal);
			var fields = new List<string> { "taf-home-save-v1", Game.GameID, system.RealmId,
				system.CurrentSettlementId, zone.ZoneID, maps[0], maps[1] };
			var ids = new HashSet<int>();
			foreach (string bodyId in receipt.FounderObjectIds)
			{
				GameObject body = zone.FindObjectByID(bodyId);
				Require(GameObject.Validate(body) && body.IsAlive && body.CurrentZone == zone
					&& KingdomCitizenship.BelongsTo(system, body), "original founder body/citizenship differs");
				int id = KingdomResidents.IdOf(body);
				Require(bindings.TryGet(id, KingdomBindingKind.Resident, out KingdomBinding binding)
					&& binding.ObjectId == bodyId && binding.ZoneId == zone.ZoneID,
					"saved exact body binding differs");
				Require(ids.Add(id) && state.TryResidentIndex(id, out int index)
					&& state.TryResident(index, out KingdomResidentRow row)
					&& KingdomResidentRules.OnTheRoll(row) && row.BoundZoneId == zone.ZoneID
					&& KingdomResidenceRules.TryDecode(row.Residence, out KingdomResidence home)
					&& KingdomResidenceRules.SameHome(home, zone.ZoneID,
						body.GetStringProperty(KingdomLodging.HomePlotIdProperty)), "original founder home record differs");
				state.TryResidentIndex(id, out int at); state.TryResident(at, out KingdomResidentRow resident);
				fields.Add(bodyId); fields.Add(id.ToString(CultureInfo.InvariantCulture));
				fields.Add(resident.HomeWorkId.ToString(CultureInfo.InvariantCulture));
				fields.Add(resident.BoundZoneId); fields.Add(resident.Residence);
			}
			var encoded = new List<string>();
			foreach (string field in fields)
				encoded.Add(field == null ? "-" : Convert.ToBase64String(new UTF8Encoding(false, true).GetBytes(field)));
			string wire = string.Join("\n", encoded);
			Require(wire.Length <= 32768, "home witness exceeds bound");
			return wire;
		}
		private static void Require(bool value, string reason) => KingdomScenarioSaveFiles.Require(value, reason);
	}
}
