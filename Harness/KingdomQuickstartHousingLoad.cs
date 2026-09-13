using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Exact survivor/departure witness for the dedicated housing-crisis save lane.
	/// Writes only harness evidence before saving; cold-load observations never repair the world.</summary>
	internal static class KingdomQuickstartHousingLoad
	{
		private const string Key = "r_TAF_ScenarioHousingSave_v1";
		internal const string Row = "quickstart-housing-load";
		private const string Header = "taf-housing-save-v1";
		private static readonly string[] Script = {
			"quickstart-lifecycle marsh yes", "stagedigest", "lifecycle-open", "housing-delay", "advance 10000",
			"housing-retained", "advance 2400", "housing-retained", "housing-unblock",
			"advance 4800", "housing-recovered", "housing-witness", "lifecycle-save", "stagedigest" };

		internal static bool ClaimsScript()
		{
			return KingdomScenarioScript.TryRead(out IList<string> script, out _)
				&& script.Contains("housing-witness");
		}

		private static void ExactScript()
		{
			Require(KingdomScenarioScript.TryRead(out IList<string> script, out _)
				&& script.Count == Script.Length, "housing save script is absent or changed");
			for (int i = 0; i < Script.Length; i++)
				Require(script[i] == Script[i], "housing save script differs at " + i);
		}

		internal static void Record(XRLGame Game, Zone Zone, KingdomSystem System,
			HashSet<string> Retained, HashSet<string> Departed)
		{
			ExactScript();
			Require(KingdomScenarioStateShape.Classify(KingdomScenarioDurableState.Observe(Key), out _)
				== KingdomDurableKeyShape.Absent, "housing witness already exists");
			Require(Retained.Count == 2 && Departed.Count == 2 && !Retained.Overlaps(Departed),
				"housing witness requires two distinct survivors and two distinct departures");
			var retained = new List<string>(Retained); retained.Sort(StringComparer.Ordinal);
			var departed = new List<string>(Departed); departed.Sort(StringComparer.Ordinal);
			var fields = new List<string> { Header, Game.GameID, System.RealmId, Zone.ZoneID,
				KingdomScenarioSaveFiles.HashText(Game.GetStringGameState(KingdomQuickstartRules.ReceiptState)) };
			fields.AddRange(retained); fields.AddRange(departed);
			foreach (string field in fields)
				Require(!string.IsNullOrEmpty(field) && field.Length <= 512
					&& field.IndexOfAny(new[] { '\n', '\r' }) < 0, "housing witness field is malformed");
			string wire = string.Join("\n", fields);
			Game.SetStringGameState(Key, wire);
			Require(KingdomScenarioDurableState.ProvesExactText(Key, wire), "housing witness did not publish exactly");
		}

		internal static bool Observe(XRLGame Game, Zone Zone, KingdomSystem System, out string Failure)
		{
			Failure = null;
			int citizens = 0, housed = 0, absent = 0, beds = 0;
			try
			{
				ExactScript();
				Require(KingdomScenarioLoadEntry.Armed && KingdomScenarioLoadEntry.LifecycleSnapshot != null,
					"housing load witness requires the real sealed lifecycle load");
				Require(KingdomMaster.ConfiguredEnabled && KingdomGrowth.Enabled && KingdomLodging.Enabled,
					"housing load requires normal settlement, growth and lodging options");
				string raw = Game.GetStringGameState(Key);
				Require(raw != null && raw.Length <= 5000 && KingdomScenarioDurableState.ProvesExactText(Key, raw),
					"housing save witness absent or malformed");
				string[] fields = raw.Split('\n');
				Require(fields.Length == 9 && fields[0] == Header && fields[1] == Game.GameID
					&& fields[2] == System.RealmId && fields[3] == Zone.ZoneID, "housing witness owner differs");
				string receiptWire = Game.GetStringGameState(KingdomQuickstartRules.ReceiptState);
				Require(KingdomQuickstartRules.TryDecode(receiptWire, out var receipt), "Quickstart receipt cannot be decoded");
				Require(KingdomScenarioSaveFiles.HashText(receiptWire) == fields[4]
					&& receipt.FoundersDisposition == KingdomQuickstartFoundersDisposition.Seeded
					&& KingdomQuickstartRules.IsTerminal(receipt) && receipt.ZoneId == Zone.ZoneID,
					"original four-founder receipt changed across housing recovery load");
				var ids = new HashSet<string>(StringComparer.Ordinal);
				for (int i = 5; i < 9; i++) Require(!string.IsNullOrEmpty(fields[i]) && ids.Add(fields[i]), "housing identities repeat");
				Require(ids.SetEquals(receipt.FounderObjectIds), "housing identities differ from original cohort");
				for (int i = 5; i < 9; i++)
				{
					GameObject body = null;
					foreach (GameObject item in Zone.GetObjects())
					{
						if (!GameObject.Validate(item) || item.IDIfAssigned != fields[i]) continue;
						Require(body == null, "duplicate original founder after load"); body = item;
					}
					if (i >= 7) { Require(body == null, "a departed founder was reminted on load"); absent++; continue; }
					Require(body != null && body.CurrentZone == Zone && body.IsCreature && !body.IsPlayer()
						&& KingdomCitizenship.BelongsTo(System, body), "retained founder lost citizenship on load");
					Require(KingdomResidents.TryResident(System.City, KingdomResidents.IdOf(body), out var row)
						&& KingdomResidentRules.OnTheRoll(row), "retained founder lost living roll membership");
					citizens++;
					Require(KingdomLodging.HomeDesignKeyOf(Zone, body) == KingdomQuickstartRules.ShelterBuildKey,
						"retained founder lost usable starter home on load"); housed++;
				}
				Require(KingdomResidents.OnRollCount(System) == 2, "load changed retained population");
				Require(KingdomGrowth.TryCountBeds(Zone, out beds, out string roofFailure) && beds >= 2,
					"loaded housing is unusable: " + roofFailure);
			}
			catch (Exception error) { Failure = error.GetType().Name + ": " + error.Message; }
			KingdomScenarioJournal.Append(Row, Failure == null, "citizens=" + citizens + "; housed=" + housed
				+ "; departed-still-absent=" + absent + "; beds=" + beds + "; turns=" + Game.Turns
				+ "; world-repair=false; synthetic-residents=false" + (Failure == null ? "" : "; failure=" + Failure));
			return Failure == null;
		}

		private static void Require(bool Value, string Failure)
		{ KingdomQuickstartHousingRecovery.Require(Value, Failure); }
	}
}
