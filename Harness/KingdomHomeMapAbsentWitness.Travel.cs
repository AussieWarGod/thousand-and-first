using System;
using ThousandAndFirst.Simulation.City;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomHomeMapAbsentWitness
	{
		private static Visit Depart(XRLGame game)
		{
			KingdomSystem system = game.GetSystem<KingdomSystem>();
			Zone home = The.Player.CurrentZone;
			Require(system?.Founded == true && system.ClaimedZones.Count == 2
				&& KingdomQuickstartRules.TryDecode(game.GetStringGameState(KingdomQuickstartRules.ReceiptState),
					out _), "completed two-map opening absent");
			KingdomQuickstartRules.TryDecode(game.GetStringGameState(KingdomQuickstartRules.ReceiptState), out var receipt);
			GameObject body = home.FindObjectByID(receipt.FounderObjectIds[0]);
			Require(GameObject.Validate(body) && body.IsAlive && KingdomCitizenship.BelongsTo(system, body)
				&& KingdomResidents.TryResident(system.City, KingdomResidents.IdOf(body), out _), "original traveler unavailable");
			KingdomResidents.TryResident(system.City, KingdomResidents.IdOf(body), out var original);
			var visit = new Visit { Body = body.IDIfAssigned, Resident = original.ResidentId, HomeZone = home.ZoneID,
				AwayZone = home.GetZoneIDFromDirection("E"), HomeWork = original.HomeWorkId,
				Plot = body.GetStringProperty(KingdomLodging.HomePlotIdProperty), X = body.CurrentCell.X, Y = body.CurrentCell.Y };
			Require(system.ClaimedZones.Contains(visit.AwayZone) && visit.AwayZone != home.ZoneID
				&& KingdomResidenceRules.TryDecode(original.Residence, out var originalHome)
				&& KingdomResidenceRules.SameHome(originalHome, home.ZoneID, visit.Plot), "traveler's real home or adjacent claim absent");
			visit.Occupied = KingdomHomeMapNativeProvider.Occupants(system, home, visit.Plot, visit.Resident);
			Zone away = The.ZoneManager.GetZone(visit.AwayZone);
			GameObject player = The.Player;
			Cell playerDeparture = player.CurrentCell;
			long turns = game.Turns, ticks = game.TimeTicks;
			try
			{
				Move(body, Landing(away));
				Move(player, Landing(away));
				The.ZoneManager.SetActiveZone(away);
				Settle(system, away);
				Require(KingdomResidents.TryResident(system.City, visit.Resident, out var observed)
					&& observed.HomeWorkId == visit.HomeWork && observed.Residence == original.Residence
					&& observed.BoundZoneId == away.ZoneID && body.IDIfAssigned == visit.Body
					&& body.CurrentZone == away && body.IsAlive && KingdomCitizenship.BelongsTo(system, body)
					&& body.GetStringProperty(KingdomLodging.HomePlotIdProperty) == visit.Plot,
					"absent visit changed home authority or physical citizen");
			}
			finally
			{
				Move(player, playerDeparture);
				The.ZoneManager.SetActiveZone(home);
			}
			Require(game.Turns == turns && game.TimeTicks == ticks && body.CurrentZone == away
				&& home.FindObjectByID(visit.Body) == null, "departure advanced time or returned the citizen");
			Capacity(game, visit);
			return visit;
		}

		private static void Capacity(XRLGame game, Visit visit)
		{
			Require(The.Player.CurrentZone.ZoneID == visit.HomeZone
				&& ReferenceEquals(The.Player.CurrentZone, The.ZoneManager.ActiveZone)
				&& The.Player.CurrentZone.FindObjectByID(visit.Body) == null, "home observer found traveler locally");
			Require(KingdomHomeMapNativeProvider.Occupants(game.GetSystem<KingdomSystem>(), The.Player.CurrentZone,
				visit.Plot, visit.Resident) == visit.Occupied, "absent owner lost exact source-home capacity");
		}

		private static void Return(XRLGame game, Visit visit)
		{
			KingdomSystem system = game.GetSystem<KingdomSystem>();
			Zone home = The.Player.CurrentZone;
			Require(KingdomResidents.TryResident(system.City, visit.Resident, out var before), "absent row vanished before return");
			Zone away = The.ZoneManager.GetZone(visit.AwayZone);
			GameObject body = away?.FindObjectByID(visit.Body);
			Require(GameObject.Validate(body) && body.IsAlive && body.CurrentZone == away
				&& body.IDIfAssigned == visit.Body && KingdomResidents.IdOf(body) == visit.Resident
				&& KingdomCitizenship.BelongsTo(system, body)
				&& body.GetStringProperty(KingdomLodging.HomePlotIdProperty) == visit.Plot,
				"cold remote lookup did not find the exact living citizen and home projection");
			long turns = game.Turns, ticks = game.TimeTicks;
			Move(body, home.GetCell(visit.X, visit.Y));
			Settle(system, home);
			Require(body.CurrentZone == home && home.FindObjectByID(visit.Body) == body
				&& away.FindObjectByID(visit.Body) == null && KingdomResidents.TryResident(system.City, visit.Resident, out var after)
				&& after.BoundZoneId == home.ZoneID && after.HomeWorkId == visit.HomeWork && after.Residence == before.Residence
				&& body.GetStringProperty(KingdomLodging.HomePlotIdProperty) == visit.Plot
				&& system.Bindings.TryReadExact(out KingdomBindingTable bindings, out _)
				&& bindings.TryGet(visit.Resident, KingdomBindingKind.Resident, out var binding)
				&& binding.ObjectId == visit.Body && binding.ZoneId == home.ZoneID, "ordinary return pass changed citizen home/binding");
			Require(KingdomHomeMapNativeProvider.Occupants(system, home, visit.Plot, visit.Resident) == visit.Occupied
				&& game.Turns == turns && game.TimeTicks == ticks, "return changed capacity or world clock");
		}

		private static void Settle(KingdomSystem system, Zone zone)
		{
			Require(The.Player.CurrentZone == zone && ReferenceEquals(The.ZoneManager.ActiveZone, zone)
				&& !KingdomSurvey.HasBoundPass, "settlement ground or scope differs");
			Require(KingdomSurvey.TryBindLocalOperation(zone, system, out var scope, out string failure), failure);
			using (scope)
			{
				KingdomSurvey survey = KingdomSurvey.ActiveFor(zone);
				KingdomCity.CheckIn(system, zone, survey, The.Game.TimeTicks);
				KingdomLodging.OnSettlementPass(system, zone, survey);
			}
			Require(!KingdomSurvey.HasBoundPass, "absent visit leaked survey scope");
		}

		private static Cell Landing(Zone zone)
		{
			Require(zone != null, "claimed destination unavailable");
			for (int y = 1; y < zone.Height - 1; y++)
				for (int x = 1; x < zone.Width - 1; x++)
					if (zone.GetCell(x, y).Objects.Count == 0) return zone.GetCell(x, y);
			throw new InvalidOperationException("no empty controlled landing");
		}

		private static void Move(GameObject body, Cell cell) => Require(cell != null && GameObject.Validate(body)
			&& body.SystemMoveTo(cell, energyCost: 0, forced: false, ignoreCombat: true, ignoreGravity: false, noStack: true)
			&& body.CurrentCell == cell, "controlled physical transfer refused");
	}
}
