using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomHomeMapNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "home-map-native";
		private static bool Attempted;
		private static readonly StringBuilder Evidence = new StringBuilder();
		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;
		public IEnumerable<string> ScenarioVerbs => new[] { Verb };

		public string RunScenarioVerb(string Name, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(Name == Verb && string.IsNullOrEmpty(Argument) && !Attempted, "home-map probe already attempted or wrong verb");
				Require(KingdomScenarioScript.TryRead(out IList<string> script, out _)
					&& (KingdomHomeMapSaveProvider.IsExact(script) || KingdomHomeMapAbsentProvider.IsExact(script)
						|| KingdomHomeMapDamageProvider.IsExact(script) || script.Count == 5
					&& script[0] == "quickstart-lifecycle marsh yes" && script[1] == "lifecycle-open"
					&& script[2] == "advance 8400" && script[3] == Verb && script[4] == "stagedigest"), "exact home-map script absent");
				Attempted = true;
				Run();
				Ok = true;
				return "native-home-map cases=1 passed=1 failed=0" + Evidence;
			}
			catch (Exception error)
			{
				KingdomLog.Log("native-home-map retained failure: " + error + Evidence);
				return "native-home-map cases=1 passed=0 failed=1; failure="
					+ KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message) + Evidence;
			}
		}

		internal static string RepeatLoadedVisit()
		{
			Require(!Attempted && KingdomScenarioLoadEntry.Armed && KingdomHomeMapSaveProvider.ClaimsScript(),
				"cold home visit is not owned or already ran");
			Attempted = true;
			Run(true);
			return "native-home-map cases=1 passed=1 failed=0" + Evidence;
		}

		private static void Run(bool ExistingAway = false)
		{
			XRLGame game = The.Game;
			Zone homeZone = The.Player?.CurrentZone;
			KingdomSystem system = game?.GetSystem<KingdomSystem>();
			Require(game != null && homeZone != null && system?.Founded == true
				&& KingdomLodging.Enabled && !KingdomSurvey.HasBoundPass && !KingdomScenarioAdvance.Pending,
				"requires an idle genuine Quickstart with lodging enabled");
			Require(KingdomQuickstartSettlementChecks.Observe(game, homeZone, system,
				ExistingAway ? "loaded" : "grown", out string failure), failure);
			Require(KingdomQuickstartRules.TryDecode(game.GetStringGameState(KingdomQuickstartRules.ReceiptState),
				out var receipt), "original founder receipt absent");
			GameObject resident = homeZone.FindObjectByID(receipt.FounderObjectIds[0]);
			Require(GameObject.Validate(resident) && resident.IsAlive && KingdomCitizenship.BelongsTo(system, resident),
				"original founder is not a living citizen");
			int residentId = KingdomResidents.IdOf(resident);
			Require(KingdomResidents.TryResident(system.City, residentId, out var original)
				&& original.HomeWorkId > 0 && original.BoundZoneId == homeZone.ZoneID, "original resident row lacks its real home");
			string plot = resident.GetStringProperty(KingdomLodging.HomePlotIdProperty);
			Require(!string.IsNullOrEmpty(plot), "original home plot absent");
			Require(KingdomResidenceRules.TryDecode(original.Residence, out var originalResidence)
				&& KingdomResidenceRules.SameHome(originalResidence, homeZone.ZoneID, plot),
				"original map-qualified home is absent");
			int occupiedBefore = Occupants(system, homeZone, plot, residentId);
			Require(occupiedBefore > 0, "home has no projected occupants before departure");
			string awayId = homeZone.GetZoneIDFromDirection("E");
			Require(!string.IsNullOrEmpty(awayId) && awayId != homeZone.ZoneID
				&& system.ClaimedZones.Contains(awayId) == ExistingAway, "adjacent claim state differs");
			Zone away = The.ZoneManager.GetZone(awayId);
			Require(away != null && KingdomFounding.ZonesAdjacent(homeZone.ZoneID, awayId), "adjacent local map unavailable");
			Cell destination = null;
			for (int y = 1; y < away.Height - 1 && destination == null; y++)
				for (int x = 1; x < away.Width - 1 && destination == null; x++)
				{
					Cell cell = away.GetCell(x, y);
					if (cell.Objects.Count == 0) destination = cell;
				}
			Require(destination != null, "no empty native landing for controlled resident transfer");
			// Diagnostic setup uses the internal claim operation and a zero-energy body transfer.
			// It does not prove public stage eligibility, pedestrian travel or a second paid district.
			Require((ExistingAway || KingdomFounding.ClaimZone(away, Force: false))
				&& system.ClaimedZones.Contains(homeZone.ZoneID) && system.ClaimedZones.Contains(awayId),
				"same city's internal adjacent claim refused");
			Cell departure = resident.CurrentCell;
			GameObject player = The.Player;
			Cell playerDeparture = player.CurrentCell;
			long turns = game.Turns, tick = game.TimeTicks;
			string bodyId = resident.IDIfAssigned;
			Evidence.Append("; home-zone=").Append(homeZone.ZoneID).Append(" away-zone=").Append(awayId)
				.Append(" resident=").Append(residentId).Append(" body=").Append(bodyId)
				.Append(" original-home=").Append(original.HomeWorkId).Append(" plot=").Append(plot)
				.Append("; synthetic-claim-entry=true synthetic-transfer=true synthetic-housing=false");
			Evidence.Append("; claim-reused=").Append(ExistingAway ? "true" : "false");
			bool reserved, rowKept, plotKept;
			try
			{
				Require(resident.SystemMoveTo(destination, energyCost: 0, forced: false,
					ignoreCombat: true, ignoreGravity: false, noStack: true)
					&& resident.CurrentZone == away && resident.IDIfAssigned == bodyId, "exact resident transfer refused");
				int occupiedAway = Occupants(system, homeZone, plot, residentId);
				reserved = occupiedAway == occupiedBefore;
				Evidence.Append("; occupied-before=").Append(occupiedBefore).Append(" occupied-away=").Append(occupiedAway);
				Cell playerLanding = null;
				for (int y = 1; y < away.Height - 1 && playerLanding == null; y++)
					for (int x = 1; x < away.Width - 1 && playerLanding == null; x++)
						if (away.GetCell(x, y).Objects.Count == 0) playerLanding = away.GetCell(x, y);
				Require(playerLanding != null && player.SystemMoveTo(playerLanding, energyCost: 0,
					forced: false, ignoreCombat: true, ignoreGravity: false, noStack: true), "founder transfer refused");
				The.ZoneManager.SetActiveZone(away);
				Require(player.CurrentZone == away && ReferenceEquals(The.ZoneManager.ActiveZone, away),
					"visiting map is not the founder's active ground");
				Evidence.Append("; active-away=true synthetic-founder-transfer=true");
				Require(KingdomSurvey.TryBindLocalOperation(away, system, out var scope, out failure), failure);
				using (scope)
				{
					KingdomSurvey survey = KingdomSurvey.ActiveFor(away);
					KingdomCity.CheckIn(system, away, survey, game.TimeTicks);
					Require(KingdomResidents.TryResident(system.City, residentId, out var visited), "visited resident row missing");
					rowKept = visited.HomeWorkId == original.HomeWorkId
						&& KingdomResidenceRules.TryDecode(visited.Residence, out var visitedResidence)
						&& KingdomResidenceRules.SameHome(visitedResidence, homeZone.ZoneID, plot)
						&& visitedResidence.BedId == originalResidence.BedId;
					Evidence.Append("; visited-home=").Append(visited.HomeWorkId).Append(" visited-bound=").Append(visited.BoundZoneId);
					Require(visited.BoundZoneId == awayId, "visit did not bind the same living resident to the actual map");
					KingdomLodging.OnSettlementPass(system, away, survey);
					plotKept = resident.GetStringProperty(KingdomLodging.HomePlotIdProperty) == plot;
					Evidence.Append("; visited-plot=").Append(resident.GetStringProperty(KingdomLodging.HomePlotIdProperty) ?? "<null>");
				}
				Require(!KingdomSurvey.HasBoundPass, "visit leaked local survey scope");
			}
			finally
			{
				// Restore only the controlled physical transfers. Never repair a lost home or row.
				Require(GameObject.Validate(resident) && resident.IsAlive && resident.IDIfAssigned == bodyId
					&& resident.SystemMoveTo(departure, energyCost: 0, forced: false,
						ignoreCombat: true, ignoreGravity: false, noStack: true)
					&& resident.CurrentCell == departure, "exact original resident could not return to its departure cell");
				Require(ReferenceEquals(The.Player, player) && player.SystemMoveTo(playerDeparture, energyCost: 0,
					forced: false, ignoreCombat: true, ignoreGravity: false, noStack: true), "founder could not return");
				The.ZoneManager.SetActiveZone(homeZone);
				Require(player.CurrentCell == playerDeparture && ReferenceEquals(The.ZoneManager.ActiveZone, homeZone),
					"original active ground was not restored");
				Evidence.Append("; physical-return=true home-repair=false");
			}
			Require(game.Turns == turns && game.TimeTicks == tick, "controlled transfers advanced world time");
			Require(reserved && rowKept && plotKept, "visit lost bed reservation, resident home identity or home plot");
			Require(KingdomSurvey.TryBindLocalOperation(homeZone, system, out var returnedScope, out failure), failure);
			using (returnedScope)
			{
				KingdomSurvey survey = KingdomSurvey.ActiveFor(homeZone);
				KingdomCity.CheckIn(system, homeZone, survey, game.TimeTicks);
				KingdomLodging.OnSettlementPass(system, homeZone, survey);
				Require(KingdomResidents.TryResident(system.City, residentId, out var returned)
					&& returned.BoundZoneId == homeZone.ZoneID && returned.HomeWorkId == original.HomeWorkId
					&& KingdomResidenceRules.TryDecode(returned.Residence, out var returnedHome)
					&& KingdomResidenceRules.SameHome(returnedHome, homeZone.ZoneID, plot)
					&& returnedHome.BedId == originalResidence.BedId
					&& resident.GetStringProperty(KingdomLodging.HomePlotIdProperty) == plot,
					"ordinary settlement pass did not preserve the returned resident home");
			}
			Require(Occupants(system, homeZone, plot, residentId) == occupiedBefore, "return changed reserved capacity");
			Evidence.Append("; return-settlement-pass=true reservation-owner-exact=true");
		}

		internal static int Occupants(KingdomSystem System, Zone Zone, string Plot, int ResidentId)
		{
			Require(KingdomSurvey.TryBindLocalOperation(Zone, System, out var scope, out string failure), failure);
			using (scope)
			{
				Require(KingdomSurvey.ActiveFor(Zone).TryBenefits(out var benefits, out failure), failure);
				var method = AccessTools.Method(typeof(KingdomLodging), "ProjectedOccupancy");
				Require(method != null, "production occupancy reader absent");
				var occupied = method.Invoke(null, new object[] { Zone, benefits }) as IDictionary;
				Require(occupied != null && occupied.Contains(Plot), "reserved household is absent");
				var residents = occupied[Plot] as IList;
				Require(residents != null, "household membership is unavailable");
				int matches = 0, totalMatches = 0;
				foreach (DictionaryEntry household in occupied)
					foreach (object member in (IList)household.Value)
					{
						var field = AccessTools.Field(member.GetType(), "ResidentId");
						Require(field != null, "household has no exact resident identity");
						if ((int)field.GetValue(member) != ResidentId) continue;
						totalMatches++;
						if ((string)household.Key == Plot) matches++;
					}
				Require(matches == 1 && totalMatches == 1, "resident reservation missing or duplicated");
				return residents.Count;
			}
		}

		private static void Require(bool Value, string Failure)
		{
			if (!Value) throw new InvalidOperationException(Failure);
		}
	}
}
