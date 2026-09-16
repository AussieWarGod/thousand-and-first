using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ThousandAndFirst.Simulation.City;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomHomeMapAbsentWitness
	{
		private const string Key = "r_TAF_ScenarioHomeMapAbsent_v1";
		private const string ContextKey = "r_TAF_ScenarioHomeMapAbsentContext_v1";
		private static XRLGame Preactivated;
		private static int Attempts;
		private static string Failure;
		private sealed class Visit
		{
			internal string Body, HomeZone, AwayZone, Plot;
			internal int Resident, HomeWork, Occupied, X, Y;
		}

		internal static string Record(XRLGame game)
		{
			Require(!KingdomScenarioSaveFiles.LoadPresent() && !KingdomNativeRegressionContext.HasAnyState(game, Key)
				&& !KingdomNativeRegressionContext.HasAnyState(game, ContextKey), "absent witness already exists or load is active");
			Visit visit = Depart(game);
			string context = Encode(new[] { visit.Body, visit.HomeZone, visit.AwayZone, visit.Plot,
				Number(visit.Resident), Number(visit.HomeWork), Number(visit.Occupied), Number(visit.X), Number(visit.Y) });
			game.SetStringGameState(ContextKey, context);
			Require(KingdomScenarioDurableState.ProvesExactText(ContextKey, context), "absent context did not persist");
			string wire = Capture(game, visit);
			game.SetStringGameState(Key, wire);
			Require(KingdomScenarioDurableState.ProvesExactText(Key, wire), "absent authority witness did not persist");
			Capacity(game, visit);
			return Stamp(game, visit) + "; witness=recorded; saved-away=true; synthetic-transfer=true; world-repair=false";
		}

		internal static void BeforeActivation(XRLGame game)
		{
			try
			{
				Require(++Attempts == 1 && Preactivated == null && KingdomScenarioLoadReaderWitness.Releases == 1
					&& !KingdomScenarioLoadReaderWitness.HadErrors, "absent preactivation or deserializer repeated or failed");
				Visit visit = Compare(game); Preactivated = game;
				Journal("home-map-absent-preactivation", Stamp(game, visit)
					+ "; before-AfterGameLoaded=true; before-remote-lookup=true; world-repair=false");
			}
			catch (Exception error)
			{
				Failure = error.Message;
				KingdomScenarioJournal.Append("home-map-absent-preactivation", false, Failure);
			}
		}

		internal static void VerifyLoaded(XRLGame game)
		{
			Require(Attempts == 1 && Failure == null && ReferenceEquals(Preactivated, game),
				"absent preactivation absent or failed: " + Failure);
			Visit visit = Compare(game);
			Capacity(game, visit);
			Journal("home-map-absent-loaded", Stamp(game, visit)
				+ "; before-remote-lookup=true; capacity-retained=true; world-repair=false");
			Return(game, visit);
			Journal("home-map-absent-return", Stamp(game, visit, true)
				+ "; exact-body=true; return-settlement-pass=true; capacity-retained=true; world-repair=false");
		}

		private static Visit Compare(XRLGame game)
		{
			KingdomHomeMapAbsentProvider.RequireScript();
			Require(KingdomScenarioLoadEntry.Armed && KingdomScenarioLoadEntry.LifecycleSnapshot != null,
				"absent witness does not own a sealed load");
			string context = game.GetStringGameState(ContextKey);
			Require(context != null && context.Length <= 8192 && KingdomScenarioDurableState.ProvesExactText(ContextKey, context),
				"absent context is not exact");
			string[] fields = context.Split('\n');
			Require(fields.Length == 9, "absent context field count differs");
			for (int i = 0; i < fields.Length; i++)
			{
				int colon = fields[i].IndexOf(':');
				Require(colon > 0 && colon <= 5 && int.TryParse(fields[i].Substring(0, colon), NumberStyles.None,
					CultureInfo.InvariantCulture, out int length) && length == fields[i].Length - colon - 1,
					"absent context length prefix differs");
				fields[i] = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(fields[i].Substring(colon + 1)));
			}
			Require(Encode(fields) == context, "absent context is not canonical");
			var visit = new Visit { Body = fields[0], HomeZone = fields[1], AwayZone = fields[2], Plot = fields[3],
				Resident = int.Parse(fields[4], CultureInfo.InvariantCulture), HomeWork = int.Parse(fields[5], CultureInfo.InvariantCulture),
				Occupied = int.Parse(fields[6], CultureInfo.InvariantCulture), X = int.Parse(fields[7], CultureInfo.InvariantCulture),
				Y = int.Parse(fields[8], CultureInfo.InvariantCulture) };
			string wire = game.GetStringGameState(Key);
			Require(wire != null && wire.Length <= 32768 && KingdomScenarioDurableState.ProvesExactText(Key, wire)
				&& Capture(game, visit) == wire, "absent saved homes, profiles or bindings differ");
			return visit;
		}

		private static string Capture(XRLGame game, Visit visit)
		{
			KingdomSystem system = game?.GetSystem<KingdomSystem>();
			Zone home = The.Player?.CurrentZone;
			Require(ReferenceEquals(game, The.Game) && system?.Founded == true && home != null
				&& home.ZoneID == visit.HomeZone && system.ClaimedZones.Count == 2
				&& system.ClaimedZones.Contains(visit.HomeZone) && system.ClaimedZones.Contains(visit.AwayZone)
				&& visit.HomeZone != visit.AwayZone && visit.Resident > 0 && visit.HomeWork > 0 && visit.Occupied > 0
				&& system.City.HasValidSubsidenceStorage(), "absent home ground/authority differs");
			Require(system.City.TryReadExact(out KingdomCityState state, out _) && state.ResidentCount == 4
				&& system.Bindings.TryReadExact(out _, out _), "absent roster/bindings differ");
			system.Bindings.TryReadExact(out KingdomBindingTable bindings, out _);
			Require(KingdomQuickstartRules.TryDecode(game.GetStringGameState(KingdomQuickstartRules.ReceiptState),
				out var receipt) && receipt.FounderObjectIds.Length == 4 && receipt.FounderObjectIds[0] == visit.Body,
				"original absent founder differs");
			var fields = new List<string> { game.GameID, system.RealmId, system.CurrentSettlementId,
				game.GetStringGameState(ContextKey) };
			var ids = new HashSet<int>();
			foreach (string bodyId in receipt.FounderObjectIds)
			{
				bool absent = bodyId == visit.Body;
				KingdomResidentRow row = default;
				int matches = 0;
				for (int i = 0; i < state.ResidentCount; i++)
					if (state.TryResident(i, out var candidate)
						&& bindings.TryGet(candidate.ResidentId, KingdomBindingKind.Resident, out var binding)
						&& binding.ObjectId == bodyId)
					{
						Require(binding.ZoneId == (absent ? visit.AwayZone : visit.HomeZone), "actual body binding differs");
						row = candidate; matches++;
					}
				Require(matches == 1 && ids.Add(row.ResidentId) && KingdomResidentRules.OnTheRoll(row)
					&& row.BoundZoneId == (absent ? visit.AwayZone : visit.HomeZone)
					&& KingdomResidenceRules.TryDecode(row.Residence, out var residence)
					&& residence.ZoneId == visit.HomeZone && row.HomeWorkId > 0, "canonical founder home differs");
				GameObject body = home.FindObjectByID(bodyId);
				if (absent)
					Require(body == null && row.ResidentId == visit.Resident && row.HomeWorkId == visit.HomeWork
						&& KingdomResidenceRules.TryDecode(row.Residence, out var absentHome)
						&& KingdomResidenceRules.SameHome(absentHome, visit.HomeZone, visit.Plot), "absent founder returned or lost home");
				else Require(GameObject.Validate(body) && body.IsAlive && body.CurrentZone == home
					&& KingdomResidents.IdOf(body) == row.ResidentId && KingdomCitizenship.BelongsTo(system, body)
					&& KingdomResidenceRules.TryDecode(row.Residence, out var localHome)
					&& KingdomResidenceRules.SameHome(localHome, home.ZoneID,
						body.GetStringProperty(KingdomLodging.HomePlotIdProperty)), "local founder body/home differs");
				fields.Add(bodyId); fields.Add(Number(row.ResidentId)); fields.Add(Number(row.HomeWorkId));
				fields.Add(row.BoundZoneId); fields.Add(row.Residence);
			}
			return Encode(fields);
		}

		private static string Stamp(XRLGame game, Visit visit, bool returned = false) => "residents=4; local="
			+ (returned ? "4; away=0" : "3; away=1") + "; maps=2; body="
			+ visit.Body + "; resident=" + visit.Resident + "; home-zone=" + visit.HomeZone + "; away-zone=" + visit.AwayZone
			+ "; original-home=" + visit.HomeWork + "; plot=" + visit.Plot + "; occupied=" + visit.Occupied
			+ "; receipt-sha256=" + KingdomScenarioSaveFiles.HashText(game.GetStringGameState(Key));
		private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
		private static string Encode(IEnumerable<string> fields)
		{
			var encoded = new List<string>();
			foreach (string field in fields)
			{
				Require(field != null && field.Length <= 8192, "absent witness field exceeds bound or is null");
				string value = Convert.ToBase64String(new UTF8Encoding(false, true).GetBytes(field));
				encoded.Add(Number(value.Length) + ":" + value);
			}
			string wire = string.Join("\n", encoded);
			Require(wire.Length <= 32768, "absent witness exceeds bound");
			return wire;
		}
		private static void Journal(string row, string detail) => Require(KingdomScenarioJournal.Append(row, true, detail) == null,
			"absent-home journal unavailable");
		private static void Require(bool value, string reason) => KingdomScenarioSaveFiles.Require(value, reason);
	}
}
