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
		public const string ProjectionMarkerProperty = "KingdomRaidProjection";
		private const int MaxRecoveryQuestRows = 64;
		private const string RecoveryQuestKind = "TAF:raid-recovery";
		public static bool Enabled => Options.GetOption("r_TAF_OptionRaids") != "No";

		/// <summary>An authored act is the only entrance. Standing is absent by design.</summary>
		public static bool RecordProvocation(KingdomSystem system, string faction,
			string causeCode, string sourceEventId, string evidence, string sourceZoneId = null,
			int severity = 1)
		{
			if (!Enabled || system == null || !system.Founded
				|| !KingdomMaster.NewWorkAllowed(system)
				|| system.LifecycleBook == null || string.IsNullOrEmpty(faction)
				|| string.IsNullOrEmpty(causeCode) || string.IsNullOrEmpty(sourceEventId)
				|| string.IsNullOrEmpty(evidence)) return false;
			KingdomLifecycleBook book = system.LifecycleBook;
			if (!KingdomLifecycleRules.CanOwnAuthority(book)) return false;
			if (KingdomRaidIncidentRules.SourceConsumed(book.RaidLedger, sourceEventId)) return true;
			if (book.Raid != null && book.Raid.Action == KingdomLifecycleAction.RaidWarning
				&& string.Equals(book.Raid.Origin, sourceEventId, StringComparison.Ordinal)) return true;
			KingdomRaidProfile profile;
			if (!KingdomRaidProfiles.TryGet(faction, out profile)
				|| !profile.AllowsCause(causeCode)) return false;
			string targetZone = ExactTargetZone(system, sourceZoneId);
			if (targetZone == null) return false;
			long now = The.Game.TimeTicks;
			long due = SafeAdd(now, KingdomRules.RaidWarningLeadTicks);
			if (due <= now) return false;
			int count = Math.Max(1, Math.Min(KingdomRaidIncidentRules.MaxParty,
				KingdomRules.RaidSize(system.Stage)));
			string incidentId = KingdomRaidIncidentRules.IncidentId(
				KingdomRaidIncidentRules.GrievanceId(sourceEventId));
			string frozenPlan = KingdomRaidProfiles.FreezePlan(profile, system.Stage,
				KingdomRaidIncidentRules.SeedFor(incidentId), count);
			if (string.IsNullOrEmpty(frozenPlan)) return false;
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.Raid, KingdomLifecycleAction.RaidWarning, now);
			if (op == null) return false;
			int stake = KingdomRules.RaidTributeDrams;
			if (KingdomRaidIncidentRules.HasTalkObligation(book.RaidLedger, faction))
				stake = Math.Min(KingdomRaidIncidentRules.MaxStake,
					stake + KingdomRules.RaidTributeDrams);
			op.ZoneId = targetZone;
			op.Origin = sourceEventId;
			op.ObjectId = KingdomRaidIncidentRules.GrievanceId(sourceEventId);
			op.ObjectMarker = KingdomRaidIncidentRules.IncidentId(op.ObjectId);
			op.ObjectName = "authored act";
			op.Faction = faction;
			op.DisplayFaction = profile == null ? "unproven faction reach" : profile.Reach;
			op.Creed = causeCode;
			op.Detail = evidence;
			op.ArrivalText = sourceZoneId;
			op.Target = Math.Max(1, Math.Min(KingdomRaidIncidentRules.MaxSeverity, severity));
			op.Count = count;
			op.DepartTick = due;
			op.PlunderRequested = stake;
			op.Kind = KingdomRules.RaidPlunderDrams;
			op.Blueprint = frozenPlan;
			string display = DisplayFaction(faction);
			string seat = KingdomPresentation.Rich(system.SeatName);
			string rumor = "Rumor from " + profile.Reach + ": " + display
				+ " name a grievance against " + seat + ". No demand has been delivered,"
				+ " and no clock is running.";
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op,
				"rumor carried " + profile.Reach + " word of a grievance by " + display
					+ " against " + seat + ": " + evidence,
				"Rumored grievance: " + display + " — " + evidence
					+ "; no delivered demand or deadline.",
				"{{W|" + rumor + "}}", null, null);
			return PublishSimple(system, op);
		}

		/// <summary>Absolute-time raid wake. It may deliver/repair an exact channel or advance an
		/// acknowledged clock to ConfrontationReady; it never surveys, debits, wounds, spawns, or
		/// terminally resolves a raid.</summary>
		public static void OnWorldWake(KingdomSystem system, long now, Zone currentZone = null)
		{
			if (system == null || !system.Founded || The.Game == null
				|| system.LifecycleBook == null) return;
			MigrateLegacyEvidence(system);
			KingdomLifecycleBook book = system.LifecycleBook;
			if (!KingdomLifecycleRules.CanOwnAuthority(book)) return;
			ReconcileRecoveryQuestProjection(system);
			ObserveOption(book, now);
			if (book.Raid != null)
			{
				ResumeOpen(system, currentZone ?? The.Player?.CurrentZone);
				if (book.Raid != null) return;
			}
			KingdomRaidIncident incident = KingdomRaidIncidentRules.Active(book.RaidLedger);
			if (incident == null)
			{
				TryNaturalSnapjawProvocation(system);
				incident = KingdomRaidIncidentRules.Active(book.RaidLedger);
				if (incident == null) return;
			}
			if (!IncidentSourceStillValid(system, incident))
			{
				CancelIncident(system, incident, KingdomRaidResolution.SourceInvalid,
					"The frozen attacker, reach, cause, or target is no longer valid; the threat was cancelled without loss.");
				return;
			}
			if (!Enabled)
			{
				if (incident.State != KingdomRaidIncidentState.Active)
					CancelIncident(system, incident, KingdomRaidResolution.OptionDisabled,
						"Raid play was disabled; the open threat dispersed without loss.");
				return;
			}
			bool atSeat = currentZone != null && string.Equals(currentZone.ZoneID,
				incident.TargetZoneId, StringComparison.Ordinal);
			GameObject witness;
			bool carried = HasExactDemandWitness(incident, The.Player, out witness);
			if ((incident.ChannelState == KingdomRaidChannelState.AwaitingDelivery
				|| incident.ChannelState == KingdomRaidChannelState.RedeliveryQueued)
				&& TryIssueDemand(system, incident, currentZone ?? The.Player?.CurrentZone)) return;
			if ((incident.ChannelState == KingdomRaidChannelState.Issued
				|| incident.ChannelState == KingdomRaidChannelState.Acknowledged)
				&& !carried && !atSeat)
			{
				RecordChannelLoss(system, incident);
				return;
			}
			if (incident.State == KingdomRaidIncidentState.Warned
				&& incident.ChannelState == KingdomRaidChannelState.Acknowledged
				&& incident.DueTick > 0L && now >= incident.DueTick)
				AdvanceDeadline(system, incident);
		}
	}
}
