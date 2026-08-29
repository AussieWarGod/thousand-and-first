using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.AI.GoalHandlers;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomRaids
	{
		private static bool PublishSimple(KingdomSystem system, KingdomLifecycleOperation op)
		{
			if (system == null || op == null || op.Outbox == null
				|| !KingdomLifecycleRules.RaidRuntimeAdapter.PrepareLeases(system.LifecycleBook, op)
				|| !KingdomLifecycleRules.TryPublish(system.LifecycleBook, op)) return false;
			ResumeOpen(system, The.Player?.CurrentZone);
			return true;
		}

		private static KingdomLifecycleOperation ResponseOperation(KingdomSystem system,
			KingdomRaidIncident incident, KingdomLifecycleAction action,
			string chronicle, string message, string detail)
		{
			if (system == null || incident == null) return null;
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(
				system.LifecycleBook, KingdomLifecycleLane.Raid, action, The.Game.TimeTicks);
			if (op == null) return null;
			op.ZoneId = incident.TargetZoneId;
			op.ObjectId = incident.Id;
			op.Faction = incident.AttackerFactionId;
			op.DisplayFaction = DisplayFaction(incident.AttackerFactionId);
			op.Detail = detail;
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op, chronicle,
				"Raid incident " + incident.Id + ": " + chronicle + ".", message, null, null);
			return op;
		}

		private static bool CanAnswerHere(KingdomSystem system, Zone zone,
			out KingdomRaidIncident incident, out string failure)
		{
			failure = null;
			if (!TryThreat(system, out incident))
			{
				failure = "No answerable raid threatens.";
				return false;
			}
			bool atSeat = zone != null && string.Equals(zone.ZoneID, incident.TargetZoneId,
				StringComparison.Ordinal);
			GameObject witness;
			if (!atSeat && !HasExactDemandWitness(incident, The.Player, out witness))
			{
				failure = "No live answer-channel is present. Return to " + incident.TargetZoneId
					+ " or read the exact redelivered demand you carry; channel loss pauses its clock.";
				return false;
			}
			return true;
		}

		internal static bool HasExactDemandWitness(KingdomRaidIncident incident,
			GameObject carrier, out GameObject witness)
		{
			witness = null;
			if (incident == null || !GameObject.Validate(carrier) || carrier.Inventory == null
				|| carrier.Inventory.Objects == null || incident.DemandObjectId == null) return false;
			for (int i = 0; i < carrier.Inventory.Objects.Count; i++)
			{
				GameObject item = carrier.Inventory.Objects[i];
				if (!GameObject.Validate(item)) continue;
				r_KingdomRaidDemand part = item.GetPart<r_KingdomRaidDemand>();
				bool admitted = part != null && !part.Inert && part.IncidentId == incident.Id
					&& part.ChannelId == incident.DemandChannelId
					&& part.Revision == incident.ChannelRevision;
				if (item.IDIfAssigned != incident.DemandObjectId) continue;
				if (!admitted
					|| !ReferenceEquals(item.InInventory, carrier) || witness != null) return false;
				witness = item;
			}
			return witness != null;
		}

		internal static bool TryAcknowledgeDemand(KingdomSystem system, GameObject actor,
			GameObject witness, out string failure)
		{
			failure = null;
			KingdomRaidIncident incident = KingdomRaidIncidentRules.Active(
				system?.LifecycleBook?.RaidLedger);
			GameObject exact;
			if (incident == null || witness == null || !ReferenceEquals(actor, The.Player)
				|| !HasExactDemandWitness(incident, actor, out exact)
				|| !ReferenceEquals(exact, witness)
				|| incident.ChannelState != KingdomRaidChannelState.Issued)
			{
				failure = "This witness is no longer the live demand-channel. No clock was started.";
				return false;
			}
			long now = The.Game.TimeTicks;
			long lead = incident.State == KingdomRaidIncidentState.Rumored
				? incident.DemandLeadTicks : incident.RemainingLeadTicks;
			if (incident.State != KingdomRaidIncidentState.ConfrontationReady && lead <= 0L)
				lead = incident.DemandLeadTicks;
			long due = incident.State == KingdomRaidIncidentState.ConfrontationReady
				? 0L : SafeAdd(now, lead);
			if (incident.State != KingdomRaidIncidentState.ConfrontationReady && due <= now)
			{
				failure = "The demand's answer window could not be represented safely.";
				return false;
			}
			KingdomLifecycleOperation op = ResponseOperation(system, incident,
				KingdomLifecycleAction.RaidAcknowledgeDemand,
				"acknowledged the delivered demand of "
					+ DisplayFaction(incident.AttackerFactionId),
				"{{r|Demand acknowledged: " + incident.DisclosedStake
					+ " drams of water, raid stake up to " + incident.MaximumPlunder
					+ (due == 0L ? "; confrontation ready.}}" : "; answer before " + due + ".}}"),
				null);
			if (op == null) { failure = "The demand could not reserve raid authority."; return false; }
			op.Origin = incident.DemandObjectId; op.DepartTick = due;
			if (!PublishSimple(system, op))
			{
				failure = "The demand acknowledgement could not be recorded.";
				return false;
			}
			return true;
		}

		internal static bool IsDemandActionable(GameObject item, GameObject actor,
			out bool needsAcknowledgement)
		{
			needsAcknowledgement = false;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			KingdomRaidIncident incident = KingdomRaidIncidentRules.Active(
				system?.LifecycleBook?.RaidLedger);
			GameObject exact;
			if (incident == null || !ReferenceEquals(actor, The.Player)
				|| !HasExactDemandWitness(incident, actor, out exact)
				|| !ReferenceEquals(exact, item)) return false;
			needsAcknowledgement = incident.ChannelState == KingdomRaidChannelState.Issued;
			return needsAcknowledgement
				|| incident.ChannelState == KingdomRaidChannelState.Acknowledged && TryThreat(system, out _);
		}

		internal static void UseDemand(GameObject actor, GameObject item)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			bool acknowledge;
			if (!IsDemandActionable(item, actor, out acknowledge))
			{
				Popup.Show("The marks no longer carry a live answer. The object is only evidence now.");
				return;
			}
			if (acknowledge && !TryAcknowledgeDemand(system, actor, item, out string failure))
			{
				Popup.Show(failure);
				return;
			}
			KingdomCharterPart charter = actor.GetPart<KingdomCharterPart>();
			if (charter == null) { Popup.Show("The Charter cannot presently be reached."); return; }
			using (KingdomGovernanceScope scope = KingdomGovernanceScope.Begin(actor))
				charter.AnswerThreat(system);
		}

		private static bool TryIssueDemand(KingdomSystem system,
			KingdomRaidIncident incident, Zone currentZone)
		{
			GameObject player = The.Player;
			if (!GameObject.Validate(player) || currentZone == null || player.Inventory == null) return false;
			KingdomRaidProfile profile;
			GrowthStage stage;
			if (!KingdomRaidProfiles.TryResolveFrozen(incident.AttackerFactionId,
				incident.ForceProfileId, incident.Seed, incident.PlannedPartySize,
				out profile, out stage) || string.IsNullOrEmpty(profile.ChannelBlueprint)) return false;
			int revision = incident.ChannelRevision + 1;
			string objectId = KingdomRaidIncidentRules.DemandObjectId(
				incident.DemandChannelId, revision);
			KingdomLifecycleOperation op = ResponseOperation(system, incident,
				KingdomLifecycleAction.RaidDeliverDemand,
				"received a physical demand-channel from "
					+ DisplayFaction(incident.AttackerFactionId),
				"{{W|A " + profile.Grievance + " has reached you as an exact physical demand. Read and acknowledge it to start the answer clock; losing it starts no default and queues redelivery.}}",
				null);
			if (op == null) return false;
			op.Origin = incident.DemandChannelId; op.ObjectMarker = objectId;
			op.Target = revision; op.Count = 1; op.Blueprint = profile.ChannelBlueprint;
			KingdomLifecycleProjection projection =
				KingdomLifecycleRules.RaidRuntimeAdapter.PrepareInventoryProjection(
					system.LifecycleBook, op, 0, objectId, profile.ChannelBlueprint,
					player.ID, currentZone.ZoneID);
			if (projection == null
				|| !KingdomLifecycleRules.RaidRuntimeAdapter.PrepareLeases(system.LifecycleBook, op)
				|| !KingdomLifecycleRules.TryPublish(system.LifecycleBook, op)
				|| !KingdomLifecycleRules.AdvancePhase(system.LifecycleBook, op,
					KingdomLifecyclePhase.ProjectionIntent, The.Game.TimeTicks)) return false;
			ResumeOpen(system, currentZone);
			return true;
		}

		private static bool RecordChannelLoss(KingdomSystem system,
			KingdomRaidIncident incident)
		{
			KingdomLifecycleOperation op = ResponseOperation(system, incident,
				KingdomLifecycleAction.RaidLoseChannel,
				"lost the exact demand-channel of "
					+ DisplayFaction(incident.AttackerFactionId),
				"{{W|The demand-channel is gone. Its clock is paused; no answer was chosen and an authored replacement is queued.}}",
				null);
			if (op == null) return false;
			op.Origin = incident.DemandObjectId;
			return PublishSimple(system, op);
		}

		private static bool AdvanceDeadline(KingdomSystem system,
			KingdomRaidIncident incident)
		{
			KingdomLifecycleOperation op = ResponseOperation(system, incident,
				KingdomLifecycleAction.RaidDeadline,
				"the acknowledged demand of " + DisplayFaction(incident.AttackerFactionId)
					+ " reached confrontation readiness without physical loss",
				"{{r|The answer window has closed, but nothing was taken and no response was chosen. Talk, Tribute, Fight, and Fortify remain available through the live channel.}}",
				null);
			return PublishSimple(system, op);
		}

		private static KingdomRaidIncident FindRecovery(KingdomRaidLedger ledger,
			string settlementId)
		{
			if (ledger == null || ledger.Version != KingdomRaidLedger.CurrentVersion
				|| ledger.OpaqueFuturePayload != null || ledger.Incidents == null
				|| string.IsNullOrEmpty(settlementId)) return null;
			KingdomRaidIncident found = null;
			for (int i = 0; i < ledger.Incidents.Count; i++)
			{
				KingdomRaidIncident incident = ledger.Incidents[i];
				if (incident == null
					|| !string.Equals(incident.SettlementId, settlementId,
						StringComparison.Ordinal)
					|| (incident.RecoveryState != KingdomRaidRecoveryState.Offered
						&& incident.RecoveryState != KingdomRaidRecoveryState.Active
						&& incident.RecoveryState != KingdomRaidRecoveryState.Ready)) continue;
				if (found == null || incident.ResolvedTick < found.ResolvedTick
					|| (incident.ResolvedTick == found.ResolvedTick
						&& string.CompareOrdinal(incident.Id, found.Id) < 0)) found = incident;
			}
			return found;
		}

	}
}
