using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		/// <summary>
		/// The first time the founder shares water with one ritualist, remember that faction's way
		/// in the founder-held ledger. Vanilla owns the ritual and all of its awards; this is only
		/// the research source its start event makes observable and later projects at a seated city.
		/// </summary>
		public override bool HandleEvent(WaterRitualStartEvent E)
		{
			if (!KingdomMaster.NewWorkAllowed(this)) return base.HandleEvent(E);
			Guard("rite seed", delegate
			{
				// The record freezes the faction whose ritual actually paid reputation. Re-reading
				// conversation-global speaker state or its current allegiance can name another faction.
				KingdomResearch.RememberRite(this, E.Initial,
					(E.Record == null) ? null : E.Record.faction);
			});
			return base.HandleEvent(E);
		}

		/// <summary>
		/// A quest finished. The only thing in the world that can change whether a research node
		/// exists at all, so the cached verdicts are dropped here and nowhere else &mdash; there is
		/// no per-turn quest polling anywhere in this mod.
		/// </summary>
		public override bool HandleEvent(QuestFinishedEvent E)
		{
			if (!KingdomMaster.AutomaticWorkAllowed(this)) return base.HandleEvent(E);
			Guard("quest", delegate
			{
				KingdomResearch.ForgetQuests();
			});
			return base.HandleEvent(E);
		}

		/// <summary>
		/// One turn of the city. Everything inside returns immediately when there is no seated
		/// claimed zone and no debt, which is what makes this affordable at all (&sect;0.0(e)).
		/// </summary>
		public override bool HandleEvent(EndTurnEvent E)
		{
			XRLGame game = The.Game;
			if (game == null || !KingdomMaster.ObserveAutomaticWake(this, game.TimeTicks))
				return base.HandleEvent(E);
			Guard("pump", delegate
			{
				Simulation.City.KingdomHeartbeat.OnEndTurn(this, AttendSeatedSemantics);
			});
			return base.HandleEvent(E);
		}

		/// <summary>
		/// A zone off disk. LIVING-CITY-ARCHITECTURE &sect;3.5 binds debt intake here and &sect;3.8
		/// binds the stale-transient sweep here; <c>TicksFrozen</c> is a cross-check on the counter
		/// and never its source, because it measures frozen time only (&sect;3.4).
		/// </summary>
		public override bool HandleEvent(ZoneThawedEvent E)
		{
			XRLGame game = The.Game;
			if (game == null || !KingdomMaster.ObserveAutomaticWake(this, game.TimeTicks))
				return base.HandleEvent(E);
			Guard("thaw", delegate
			{
				Simulation.City.KingdomHeartbeat.OnThawed(this, E.Zone, E.TicksFrozen);
			});
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(SuspendingEvent E)
		{
			XRLGame game = The.Game;
			if (game == null || !KingdomMaster.ObserveAutomaticWake(this, game.TimeTicks))
				return base.HandleEvent(E);
			Guard("check-out", delegate
			{
				Simulation.City.KingdomCity.OnSuspending(this, E.Zone);
			});
			if (Founded && E.Zone != null && (ClaimedZones.Contains(E.Zone.ZoneID)
				|| (Away != null && Away.ClaimedZones.Contains(E.Zone.ZoneID))))
			{
				Guard("seal final read", delegate
				{
					string failure;
					if (!KingdomSeal.TryStageSemanticSnapshot("zone final read", out failure))
					{
						KingdomLog.Log("seal: zone final read was not staged ("
							+ (string.IsNullOrEmpty(failure) ? "unknown failure" : failure) + ")");
					}
				});
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneActivatedEvent E)
		{
			XRLGame game = The.Game;
			if (game == null || !KingdomMaster.ObserveAutomaticWake(this, game.TimeTicks))
				return base.HandleEvent(E);
			// The seat moves first. A second city's ground belongs to Away, not to ClaimedZones,
			// so a swap tested after the guard below could never fire: walking into your own
			// second city would read as walking into a stranger's zone.
			Guard("seat", delegate
			{
				if (TrySeat(E.Zone))
				{
					XRL.Messages.MessageQueue.AddPlayerMessage("You are in {{C|" + KingdomPresentation.Rich(SeatName) + "}}" + KingdomSettlement.VocationSuffix(Vocation) + ".");
				}
			});
			// Before the claim guard, for the same reason the seat is: a realm that put the
			// founder out no longer owns anything in ClaimedZones, so its ground reads as a
			// stranger's and this would never fire below.
			Guard("exile", delegate
			{
				OnZoneActivatedWhileExiled(E.Zone);
			});
			// Before the claim guard, for the same reason exile is: ground a city seceded from
			// stops being in ClaimedZones the moment it leaves (KingdomCreed.Secede), so a founder
			// standing on it would never be told below.
			Guard("seceded", delegate
			{
				if (E.Zone != null && KingdomCreed.SecededHolds(this, E.Zone.ZoneID))
				{
					XRL.Messages.MessageQueue.AddPlayerMessage("{{K|This ground isn't yours to keep anymore. (Charter: how your cities hold each other)}}");
				}
			});
			Guard("semantic activation", delegate
			{
				Simulation.City.KingdomSemanticDispatcher.OnZoneActivated(this, E.Zone,
					The.Game.TimeTicks, AttendSeatedSemantics);
			});
			return base.HandleEvent(E);
		}

		/// <summary>
		/// The single ordered attended settlement pass. Zone activation and the stationary
		/// end-turn scheduler both enter through <see cref="Simulation.City.KingdomSemanticDispatcher"/>,
		/// so waiting and crossing a boundary cannot select different implementations.
		/// </summary>
		private const long SemanticStepCheckIn = 1L << 0;
		private const long SemanticStepTrade = 1L << 1;
		private const long SemanticStepGrowth = 1L << 2;
		private const long SemanticStepPetitions = 1L << 3;
		private const long SemanticStepImprovement = 1L << 4;
		private const long SemanticStepBounties = 1L << 5;
		private const long SemanticStepRaids = 1L << 6;
		private const long SemanticStepWear = 1L << 7;
		private const long SemanticStepOffices = 1L << 8;
		private const long SemanticStepReach = 1L << 9;
		private const long SemanticStepLocus = 1L << 10;
		private const long SemanticStepGuestbook = 1L << 11;
		private const long SemanticStepCreed = 1L << 12;
		private const long SemanticStepFaith = 1L << 13;
		private const long SemanticStepHappenings = 1L << 14;
		private const long SemanticStepCheckOut = 1L << 15;
		private const long SemanticStepDigest = 1L << 16;
		private const long SemanticStepSeal = 1L << 17;
		private const long SemanticStepLab = 1L << 18;
		private const long SemanticStepConstruction = 1L << 19;
		private const long SemanticStepExpeditions = 1L << 20;

		private const long SemanticRequiredMask = (1L << 21) - 1L;

	}
}
