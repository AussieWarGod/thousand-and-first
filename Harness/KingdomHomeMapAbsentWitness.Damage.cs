using System;
using System.Collections.Generic;
using HarmonyLib;
using ThousandAndFirst.Simulation.City;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomHomeMapAbsentWitness
	{
		internal static string DamageProbe(XRLGame game)
		{
			Visit visit = Depart(game);
			KingdomSystem system = game.GetSystem<KingdomSystem>();
			Zone home = The.Player.CurrentZone;
			GameObject work = null;
			foreach (GameObject item in home.GetObjects())
				if (GameObject.Validate(item) && KingdomUpgrade.IsFunctionallyBuilt(item)
					&& item.GetStringProperty(KingdomPlots.PlotIdProperty) == visit.Plot)
				{
					Require(work == null, "duplicate real home root"); work = item;
				}
			Require(work != null && KingdomCityRules.StableId(work.IDIfAssigned) == visit.HomeWork,
				"paid source home vanished");
			Require(system.City.TryCaptureSubsidenceRoof(visit.Resident, out var initial)
				&& !initial.RoofStanding, "traveler already has a roof brink");
			Require(KingdomSurvey.TryBindLocalOperation(home, system, out var scope, out string refusal), refusal);
			int captured = 0, matches = 0;
			using (scope)
			{
				var roofs = new List<KingdomSubsidenceRungRoof>();
				var residents = new Dictionary<string, GameObject>(StringComparer.Ordinal);
				var method = AccessTools.Method(typeof(KingdomSubsidenceStepRuntime), "CaptureRungRoofs");
				Require(method != null && (bool)method.Invoke(null, new object[] { system, system.City,
					KingdomSurvey.ActiveFor(home), visit.Plot, visit.HomeWork, roofs, residents }), "production rung roof capture refused");
				captured = roofs.Count;
				foreach (var roof in roofs)
					if (roof.ResidentId == visit.Resident && roof.BodyObjectId == visit.Body
						&& roof.HomeZoneId == home.ZoneID) matches++;
			}
			Require(!KingdomSurvey.HasBoundPass, "damage capture leaked scope");
			r_KingdomWear wear = work.RequirePart<r_KingdomWear>();
			int originalWear = wear.Wear;
			Require(!KingdomLodging.IsCondemned(work) && !wear.LoadFailed && !wear.LifecycleQuarantined
				&& wear.IncidentPhase == 0 && wear.LeakPhase == 0 && wear.RepairEffortLeft == 0,
				"real home has competing wear authority");
			long at = game.TimeTicks;
			bool ownerRecorded = false, rowCleared = false, chronologyKept = false, repaired = false;
			int recorded = 0;
			try
			{
				wear.Wear = KingdomLodgingRules.CondemnedWearPercent;
				Require(KingdomLodging.IsCondemned(work), "controlled damage did not condemn the real home");
				recorded = KingdomLodging.RecordCondemnedRoofBrink(home, work, at);
				Require(system.City.TryCaptureSubsidenceRoof(visit.Resident, out var crossed), "damaged owner's roof row missing");
				ownerRecorded = crossed.RoofStanding && crossed.Reached == at && crossed.Warned == KingdomBrinkRules.Unwarned;
				Settle(system, home);
				Require(system.City.TryCaptureSubsidenceRoof(visit.Resident, out var settled)
					&& KingdomResidents.TryResident(system.City, visit.Resident, out _), "absent citizen lost from roll on damage");
				KingdomResidents.TryResident(system.City, visit.Resident, out var row);
				rowCleared = row.HomeWorkId == 0 && row.BoundZoneId == visit.AwayZone
					&& KingdomResidenceRules.TryDecode(row.Residence, out var residence) && !residence.HasHome;
				chronologyKept = settled.RoofStanding && settled.Reached == at
					&& settled.Warned == KingdomBrinkRules.Unwarned;
			}
			finally
			{
				wear.Wear = originalWear;
				Zone away = The.ZoneManager.GetZone(visit.AwayZone);
				GameObject body = away.FindObjectByID(visit.Body);
				Require(GameObject.Validate(body) && body.IsAlive && KingdomResidents.IdOf(body) == visit.Resident
					&& KingdomCitizenship.BelongsTo(system, body), "exact traveler unavailable after controlled damage");
				Move(body, home.GetCell(visit.X, visit.Y));
				Settle(system, home);
				Require(KingdomResidents.TryResident(system.City, visit.Resident, out var returned), "returned citizen row absent");
				repaired = returned.HomeWorkId > 0 && returned.BoundZoneId == home.ZoneID
					&& KingdomResidenceRules.TryDecode(returned.Residence, out var residence)
					&& KingdomResidenceRules.SameHome(residence, home.ZoneID,
						body.GetStringProperty(KingdomLodging.HomePlotIdProperty))
					&& !KingdomBrink.Of(body, BrinkKind.Roof).Stands;
			}
			string evidence = "resident=" + visit.Resident + "; body=" + visit.Body + "; captured=" + captured
				+ "; absent-owner-matches=" + matches + "; recorded=" + recorded + "; due=" + at
				+ "; absent-brink=" + Flag(ownerRecorded) + "; home-cleared=" + Flag(rowCleared)
				+ "; chronology-retained=" + Flag(chronologyKept) + "; ordinary-rehousing=" + Flag(repaired)
				+ "; synthetic-damage=true; synthetic-repair=true; synthetic-housing=false; paid-repair=false";
			bool correct = captured == 3 && recorded == 3 && matches == 1 && ownerRecorded && rowCleared && chronologyKept && repaired;
			Require(KingdomScenarioJournal.Append("home-map-damage-observation", correct, evidence) == null,
				"home damage evidence unavailable");
			Require(KingdomQuickstartSettlementChecks.Observe(game, home, system, "grown", out refusal), refusal);
			Require(correct, "absent owner omitted from damage capture or loss chronology; details retained in observation");
			return evidence;
		}
		private static string Flag(bool value) => value ? "true" : "false";
	}
}
