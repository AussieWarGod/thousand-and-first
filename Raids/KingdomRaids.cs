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
	public static class KingdomRaids
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

		public static void OnZoneActivated(KingdomSystem system, Zone zone,
			KingdomSurvey shared = null)
		{
			if (system == null || !system.Founded || zone == null || system.LifecycleBook == null)
				return;
			long now = The.Game.TimeTicks;
			OnWorldWake(system, now, zone);
			KingdomLifecycleBook book = system.LifecycleBook;
			if (!KingdomLifecycleRules.CanOwnAuthority(book) || book.Raid != null) return;
			ReconcileRecoveryAtSeat(system, zone);
			if (book.Raid != null) return;
			KingdomRaidIncident incident = KingdomRaidIncidentRules.Active(book.RaidLedger);
			if (incident == null) return;
			if (!Enabled) return;
			if (!string.Equals(incident.TargetZoneId, zone.ZoneID, StringComparison.Ordinal)) return;
			KingdomSurvey survey = shared ?? KingdomSurvey.Take(zone, system);
			if (incident.State == KingdomRaidIncidentState.FortifyOrdered)
			{
				ExecuteFortifyOrder(system, incident, survey);
				return;
			}
			if ((incident.State == KingdomRaidIncidentState.FightCommitted
				|| incident.State == KingdomRaidIncidentState.Fortified)
				&& (incident.DueTick == 0L || now >= incident.DueTick))
				LaunchRaid(system, zone, survey, incident);
		}

		/// <summary>Compatibility read: active causal incident only. Never scans standings.</summary>
		public static string FindProvokedFaction(KingdomSystem system)
		{
			KingdomRaidIncident incident = KingdomRaidIncidentRules.Active(
				system?.LifecycleBook?.RaidLedger);
			return incident?.AttackerFactionId;
		}

		public static bool TryThreat(KingdomSystem system, out KingdomRaidIncident incident)
		{
			incident = KingdomRaidIncidentRules.Active(system?.LifecycleBook?.RaidLedger);
			return incident != null && (incident.State == KingdomRaidIncidentState.Warned
				|| incident.State == KingdomRaidIncidentState.ConfrontationReady);
		}

		public static bool CanAnswerAt(KingdomSystem system, Zone zone,
			out KingdomRaidIncident incident, out string failure)
		{
			return CanAnswerHere(system, zone, out incident, out failure);
		}

		public static bool TryRecovery(KingdomSystem system,
			out KingdomRaidIncident incident)
		{
			KingdomLifecycleBook book = system?.LifecycleBook;
			incident = FindRecovery(book?.RaidLedger, book?.SettlementId);
			return incident != null;
		}

		public static bool TryAcceptRecovery(KingdomSystem system, out string failure)
		{
			failure = null;
			KingdomRaidIncident recovery;
			if (!TryRecovery(system, out recovery)
				|| recovery.RecoveryState != KingdomRaidRecoveryState.Offered)
			{
				failure = "No unacknowledged raid recovery is offered.";
				return false;
			}
			if (system.ClaimedZones == null
				|| !system.ClaimedZones.Contains(recovery.TargetZoneId))
			{
				failure = "The recovery target is no longer part of this settlement; nothing was started.";
				return false;
			}
			if (!CanProjectRecoveryQuest(recovery, out failure)) return false;
			KingdomLifecycleOperation op = ResponseOperation(system, recovery,
				KingdomLifecycleAction.RaidRecoveryAccept,
				"acknowledged the work of setting the watch in order",
				"{{W|Recovery begun: prove the raiding band gone, then turn in at the seat. The disordered watch remains one point weaker until then.}}",
				recovery.AttackOperationId);
			if (op == null) { failure = "Recovery could not reserve raid authority."; return false; }
			op.Origin = recovery.RecoveryQuestId;
			op.ObjectMarker = recovery.RecoveryStepId;
			if (!PublishSimple(system, op))
			{
				failure = "Recovery acknowledgement could not be recorded.";
				return false;
			}
			KingdomRaidIncident active = KingdomRaidIncidentRules.Incident(
				system.LifecycleBook.RaidLedger, recovery.Id);
			if (!EnsureRecoveryQuestProjection(system, active))
				failure = "Recovery was recorded, but its quest projection is waiting for a safe retry.";
			KingdomGovernanceScope.Commit("accept raid recovery");
			return true;
		}

		public static bool TryDeclineRecovery(KingdomSystem system, out string failure)
		{
			failure = null;
			KingdomRaidIncident recovery;
			if (!TryRecovery(system, out recovery)
				|| recovery.RecoveryState != KingdomRaidRecoveryState.Offered)
			{
				failure = "No unacknowledged raid recovery is offered.";
				return false;
			}
			KingdomLifecycleOperation op = ResponseOperation(system, recovery,
				KingdomLifecycleAction.RaidRecoveryDecline,
				"left the watch's raid-disarray unresolved",
				"{{W|The recovery is declined. The bounded one-point watch penalty remains; it does not compound or expire.}}",
				recovery.AttackOperationId);
			if (!PublishSimple(system, op))
			{
				failure = "The recovery decision could not be recorded.";
				return false;
			}
			KingdomGovernanceScope.Commit("decline raid recovery");
			return true;
		}

		public static bool TryResolveRecovery(KingdomSystem system, Zone zone,
			out string failure)
		{
			failure = null;
			KingdomRaidIncident recovery;
			if (!TryRecovery(system, out recovery)
				|| recovery.RecoveryState != KingdomRaidRecoveryState.Ready)
			{
				failure = "No raid recovery is ready for turn-in.";
				return false;
			}
			if (zone == null || !string.Equals(zone.ZoneID, recovery.TargetZoneId,
				StringComparison.Ordinal))
			{
				failure = "Set the watch in order at its exact seat: " + recovery.TargetZoneId + ".";
				return false;
			}
			if (!ExactActiveRecoveryQuest(recovery, out Quest quest))
			{
				failure = "The exact recovery quest is absent or collides with foreign quest state.";
				return false;
			}
			KingdomLifecycleOperation op = ResponseOperation(system, recovery,
				KingdomLifecycleAction.RaidRecoveryResolve,
				"set the watch in order after the raid",
				"{{G|The watch is in order again; its one-point service wound is removed.}}",
				recovery.AttackOperationId);
			if (!PublishSimple(system, op))
			{
				failure = "Recovery turn-in could not be recorded.";
				return false;
			}
			if (!FinishRecoveryQuest(recovery, quest))
				failure = "Recovery is semantically complete; the quest ledger will reconcile on the next safe wake.";
			KingdomGovernanceScope.Commit("turn in raid recovery");
			return true;
		}

		public static bool TryTribute(KingdomSystem system, Zone zone, out string failure)
		{
			failure = null;
			KingdomRaidIncident incident;
			if (!CanAnswerHere(system, zone, out incident, out failure)) return false;
			KingdomWaterDebit debit;
			bool local = zone != null && string.Equals(zone.ZoneID, incident.TargetZoneId,
				StringComparison.Ordinal);
			if (local)
			{
				KingdomSurvey survey = KingdomSurvey.Take(zone, system);
				debit = survey.ReserveExactWater(incident.DisclosedStake);
			}
			else debit = KingdomWaterDebit.ReserveCarried(The.Player, incident.DisclosedStake);
			if (debit == null || debit.State != KingdomWaterDebitState.Reserved)
			{
				failure = "Tribute costs {{C|" + incident.DisclosedStake
					+ (local ? " drams}} from the dedicated stores here, and they cannot bear it."
						: " drams}} of pure water in loose, unsealed vessels you directly carry.");
				return false;
			}
			KingdomLifecycleOperation op = ResponseOperation(system, incident,
				KingdomLifecycleAction.RaidTribute,
				"paid the exact water demanded by " + DisplayFaction(incident.AttackerFactionId),
				"{{G|The exact tribute is paid. The warband turns away.}}", null);
			if (op == null || !debit.Commit()
				|| !KingdomLifecycleRules.RaidRuntimeAdapter.PrepareCommittedTribute(op,
					debit.Amount, debit.Spent, debit.Outstanding, debit.Lost, debit.MeasurementExact)
				|| !KingdomLifecycleRules.RaidRuntimeAdapter.PrepareLeases(system.LifecycleBook, op)
				|| !KingdomLifecycleRules.TryPublish(system.LifecycleBook, op))
			{
				bool restored = RestoreDebitOrQuarantine(system, op, debit,
					"tribute receipt failed after a physical debit");
				failure = restored
					? "The stores or raid receipt changed before exact tribute could be recorded. Nothing was paid."
					: "The tribute receipt became physically uncertain. Raid authority was quarantined; inspect the named stores.";
				return false;
			}
			KingdomGovernanceScope.Commit("pay tribute");
			ResumeOpen(system, zone);
			return true;
		}

		public static bool TryTalkDown(KingdomSystem system, out string failure)
		{
			return TryTalkDown(system, The.Player?.CurrentZone, out failure);
		}

		public static bool TryTalkDown(KingdomSystem system, Zone zone, out string failure)
		{
			failure = null;
			KingdomRaidIncident incident;
			if (!CanAnswerHere(system, zone, out incident, out failure)) return false;
			if (system.GetStanding(incident.AttackerFactionId) < KingdomRules.DiplomacyStandingRequired)
			{
				failure = "They require {{C|" + KingdomRules.DiplomacyStandingRequired
					+ " standing}} before they will hear an envoy.";
				return false;
			}
			KingdomLifecycleOperation op = ResponseOperation(system, incident,
				KingdomLifecycleAction.RaidTalkDown,
				"called in its regard with " + DisplayFaction(incident.AttackerFactionId)
					+ ", owing a doubled demand if that faction is provoked again",
				"{{G|The envoy is heard. No water changes hands; one obligation remains.}}", null);
			if (!PublishSimple(system, op))
			{
				failure = "The diplomatic answer could not be recorded.";
				return false;
			}
			KingdomGovernanceScope.Commit("answer threat by envoy");
			return true;
		}

		public static bool TryFight(KingdomSystem system, Zone zone, out string failure)
		{
			failure = null;
			KingdomRaidIncident incident;
			if (!CanAnswerHere(system, zone, out incident, out failure)) return false;
			KingdomLifecycleOperation op = ResponseOperation(system, incident,
				KingdomLifecycleAction.RaidFight,
				"refused the demand of " + DisplayFaction(incident.AttackerFactionId)
					+ " and chose to meet the warband",
				"{{r|The demand is refused. The watch will meet the warband at the named hour.}}", null);
			if (!PublishSimple(system, op))
			{
				failure = "The decision to fight could not be recorded.";
				return false;
			}
			KingdomGovernanceScope.Commit("meet threat");
			return true;
		}

		public static bool TryFortify(KingdomSystem system, Zone zone, out string failure)
		{
			failure = null;
			KingdomRaidIncident incident;
			if (!CanAnswerHere(system, zone, out incident, out failure)) return false;
			if (zone == null || !string.Equals(zone.ZoneID, incident.TargetZoneId,
				StringComparison.Ordinal))
			{
				KingdomLifecycleOperation order = ResponseOperation(system, incident,
					KingdomLifecycleAction.RaidFortifyOrder,
					"ordered the named seat to muster against "
						+ DisplayFaction(incident.AttackerFactionId),
					"{{G|The muster order travels ahead. Exact works and crews will be proved at the seat; failure will reopen every answer without penalty.}}",
					null);
				if (!PublishSimple(system, order))
				{
					failure = "The remote muster order could not be recorded.";
					return false;
				}
				KingdomGovernanceScope.Commit("order fortification against threat");
				return true;
			}
			KingdomSurvey survey = KingdomSurvey.Take(zone, system);
			string commitment;
			int defence;
			if (!FreezeDefence(system, survey, out commitment, out defence))
			{
				failure = "No named defensive work here is presently able to answer the muster.";
				return false;
			}
			defence = ApplyWatchDisarray(system, defence);
			if (defence <= 0)
			{
				failure = "Raid-disarray leaves no effective defensive point to bind.";
				return false;
			}
			KingdomLifecycleOperation op = ResponseOperation(system, incident,
				KingdomLifecycleAction.RaidFortify,
				"mustered named works against " + DisplayFaction(incident.AttackerFactionId),
				"{{G|The named works are mustered. Their crews will be checked again when the warband comes.}}",
				commitment);
			if (op == null)
			{
				failure = "The muster could not reserve raid authority.";
				return false;
			}
			op.Defence = defence;
			if (!PublishSimple(system, op))
			{
				failure = "The muster could not be recorded.";
				return false;
			}
			KingdomGovernanceScope.Commit("fortify against threat");
			return true;
		}

		internal static void StepRaider(GameObject actor, r_KingdomRaiderObjective part,
			long timeTick)
		{
			if (!GameObject.Validate(actor) || part == null || actor.CurrentCell == null) return;
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (!Enabled || !KingdomMaster.AutomaticWorkAllowed(system)) return;
			KingdomLifecycleOperation op = system?.LifecycleBook?.Raid;
			if (op == null || op.Action != KingdomLifecycleAction.RaidAttack
				|| op.Phase != KingdomLifecyclePhase.EffectIntent
				|| !string.Equals(op.Id, part.OperationId, StringComparison.Ordinal)) return;
			int distance = Math.Abs(actor.CurrentCell.X - part.TargetX)
				+ Math.Abs(actor.CurrentCell.Y - part.TargetY);
			if (distance > 1) return;
			KingdomSystem.Guard("raid objective contact", delegate
			{
				ProveObjectiveContact(system, actor.CurrentZone, op, part.TargetObjectId,
					part.TargetX, part.TargetY);
			});
		}

		internal static void RaiderDying(GameObject actor, r_KingdomRaiderObjective part)
		{
			if (actor == null || part == null) return;
			Zone zone = actor.CurrentZone;
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			KingdomLifecycleOperation op = system?.LifecycleBook?.Raid;
			if (zone == null || system?.LifecycleBook == null) return;
			if (op != null && op.Action == KingdomLifecycleAction.RaidAttack
				&& op.Phase == KingdomLifecyclePhase.EffectIntent
				&& string.Equals(op.Id, part.OperationId, StringComparison.Ordinal)
				&& CountLiveRaiders(zone, op.Id, actor) == 0)
			{
				KingdomSystem.Guard("last raid body died", delegate
				{
					if (!KingdomLifecycleRules.RaidRuntimeAdapter.SkipEffectWithoutContact(
						system.LifecycleBook, op)
							|| !KingdomLifecycleRules.AdvancePhase(system.LifecycleBook, op,
								KingdomLifecyclePhase.EffectsSettled, The.Game.TimeTicks)) return;
					ResumeOpen(system, zone);
				});
				return;
			}
			KingdomRaidIncident recovery = FindRecovery(system.LifecycleBook.RaidLedger,
				system.LifecycleBook.SettlementId);
			if (recovery == null || recovery.RecoveryState != KingdomRaidRecoveryState.Active
				|| !string.Equals(recovery.AttackOperationId, part.OperationId,
					StringComparison.Ordinal)
				|| CountLiveRaiders(zone, part.OperationId, actor) != 0) return;
			KingdomSystem.Guard("last recovery-marked raider died", delegate
			{
				ReconcileRecoveryAtSeat(system, zone, actor);
			});
		}

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
				if (!GameObject.Validate(item) || item.ID != incident.DemandObjectId) continue;
				r_KingdomRaidDemand part = item.GetPart<r_KingdomRaidDemand>();
				if (part == null || part.Inert || part.IncidentId != incident.Id
					|| part.ChannelId != incident.DemandChannelId
					|| part.Revision != incident.ChannelRevision
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

		public static bool HasWatchDisarray(KingdomSystem system)
		{
			KingdomLifecycleBook book = system?.LifecycleBook;
			KingdomRaidLedger ledger = book?.RaidLedger;
			if (ledger == null || ledger.Version != KingdomRaidLedger.CurrentVersion
				|| ledger.OpaqueFuturePayload != null || ledger.Incidents == null) return false;
			for (int i = 0; i < ledger.Incidents.Count; i++)
			{
				KingdomRaidIncident q = ledger.Incidents[i];
				if (q != null && string.Equals(q.SettlementId, book.SettlementId,
					StringComparison.Ordinal)
					&& (q.RecoveryState == KingdomRaidRecoveryState.Offered
						|| q.RecoveryState == KingdomRaidRecoveryState.Active
						|| q.RecoveryState == KingdomRaidRecoveryState.Ready
						|| q.RecoveryState == KingdomRaidRecoveryState.Declined)) return true;
			}
			return false;
		}

		private static int ApplyWatchDisarray(KingdomSystem system, int defence)
		{
			return HasWatchDisarray(system) ? Math.Max(0, defence - 1) : defence;
		}

		private static bool CanProjectRecoveryQuest(KingdomRaidIncident recovery,
			out string failure)
		{
			failure = null;
			if (The.Game == null || recovery == null)
			{
				failure = "The quest ledger is unavailable.";
				return false;
			}
			if (The.Game.Quests.TryGetValue(recovery.RecoveryQuestId, out Quest active))
			{
				if (RecoveryQuestShape(active, recovery, false)) return true;
				failure = "A different active quest already owns the stable recovery ID.";
				return false;
			}
			if (The.Game.FinishedQuests.ContainsKey(recovery.RecoveryQuestId))
			{
				failure = "A finished quest already owns the stable recovery ID.";
				return false;
			}
			int count = 0;
			foreach (Quest quest in The.Game.Quests.Values)
				if (IsOwnedRecoveryQuest(quest)) count++;
			foreach (Quest quest in The.Game.FinishedQuests.Values)
				if (IsOwnedRecoveryQuest(quest)) count++;
			if (count >= MaxRecoveryQuestRows)
			{
				failure = "The bounded raid-recovery quest ledger is full.";
				return false;
			}
			return true;
		}

		private static bool EnsureRecoveryQuestProjection(KingdomSystem system,
			KingdomRaidIncident recovery)
		{
			if (system == null || recovery == null || The.Game == null
				|| (recovery.RecoveryState != KingdomRaidRecoveryState.Active
					&& recovery.RecoveryState != KingdomRaidRecoveryState.Ready)) return false;
			if (ExactActiveRecoveryQuest(recovery, out _)) return true;
			if (!CanProjectRecoveryQuest(recovery, out _)) return false;
			QuestStep step = new QuestStep
			{
				ID = recovery.RecoveryStepId,
				Name = "Prove the raiding band gone",
				Text = "Return to " + recovery.TargetZoneId
					+ ", prove the marked raiding band gone, then report at the seat.",
				Value = "", XP = 0, Ordinal = 0, Flags = QuestStep.FLAG_COLLAPSE
			};
			Quest quest = new Quest
			{
				ID = recovery.RecoveryQuestId,
				Name = "Set the watch in order",
				SystemType = null,
				Accomplishment = "", Achievement = "", BonusAtLevel = "", Level = 0,
				Factions = "", Reputation = "",
				QuestGiverName = KingdomPresentation.Rich(system.SeatName ?? "the settlement charter"),
				QuestGiverLocationName = KingdomPresentation.Rich(system.SeatName ?? "the settlement seat"),
				QuestGiverLocationZoneID = recovery.TargetZoneId,
				Hagiograph = "", HagiographCategory = "", Gospel = "", Finished = false,
				_dynamicReward = null, _Manager = null,
				Properties = new Dictionary<string, object>(StringComparer.Ordinal)
				{
					{ "TAFKind", RecoveryQuestKind }, { "IncidentId", recovery.Id }
				},
				IntProperties = new Dictionary<string, int>(StringComparer.Ordinal),
				StepsByID = new Dictionary<string, QuestStep>(StringComparer.Ordinal)
				{
					{ step.ID, step }
				}
			};
			try { The.Game.StartQuest(quest); }
			catch { return false; }
			return ExactActiveRecoveryQuest(recovery, out _);
		}

		private static bool ExactActiveRecoveryQuest(KingdomRaidIncident recovery,
			out Quest quest)
		{
			quest = null;
			return The.Game != null && recovery != null
				&& The.Game.Quests.TryGetValue(recovery.RecoveryQuestId, out quest)
				&& RecoveryQuestShape(quest, recovery, false);
		}

		private static bool RecoveryQuestShape(Quest quest,
			KingdomRaidIncident recovery, bool completed)
		{
			if (quest == null || recovery == null || quest.ID != recovery.RecoveryQuestId
				|| quest.Name != "Set the watch in order" || quest.SystemType != null
				|| quest._Manager != null || quest._dynamicReward != null
				|| quest.Accomplishment != "" || quest.Achievement != ""
				|| quest.BonusAtLevel != "" || quest.Level != 0 || quest.Factions != ""
				|| quest.Reputation != "" || quest.Hagiograph != ""
				|| quest.HagiographCategory != "" || quest.Gospel != ""
				|| quest.Properties == null || quest.Properties.Count != 2
				|| quest.GetProperty("TAFKind") != RecoveryQuestKind
				|| quest.GetProperty("IncidentId") != recovery.Id
				|| quest.IntProperties == null || quest.IntProperties.Count != 0
				|| quest.StepsByID == null || quest.StepsByID.Count != 1
				|| !quest.StepsByID.TryGetValue(recovery.RecoveryStepId, out QuestStep step)
				|| step == null || step.ID != recovery.RecoveryStepId || step.XP != 0
				|| step.Value != "" || step.Base || step.Optional || step.Failed) return false;
			return completed
				? quest.Finished && step.Finished && step.Awarded
				: !quest.Finished && !step.Finished && !step.Awarded;
		}

		private static bool IsOwnedRecoveryQuest(Quest quest)
		{
			return quest != null && quest.GetProperty("TAFKind") == RecoveryQuestKind;
		}

		private static bool FinishRecoveryQuest(KingdomRaidIncident recovery, Quest quest)
		{
			if (The.Game == null || !RecoveryQuestShape(quest, recovery, false)
				|| !The.Game.FinishQuestStep(recovery.RecoveryQuestId,
					recovery.RecoveryStepId, 0, true, recovery.TargetZoneId)) return false;
			return RecoveryQuestShape(quest, recovery, true);
		}

		private static void ReconcileRecoveryQuestProjection(KingdomSystem system)
		{
			KingdomRaidLedger ledger = system?.LifecycleBook?.RaidLedger;
			if (ledger == null || ledger.Version != KingdomRaidLedger.CurrentVersion
				|| ledger.OpaqueFuturePayload != null || ledger.Incidents == null) return;
			for (int i = 0; i < ledger.Incidents.Count; i++)
			{
				KingdomRaidIncident recovery = ledger.Incidents[i];
				if (recovery == null) continue;
				if (recovery.RecoveryState == KingdomRaidRecoveryState.Active
					|| recovery.RecoveryState == KingdomRaidRecoveryState.Ready)
					EnsureRecoveryQuestProjection(system, recovery);
				else if (recovery.RecoveryState == KingdomRaidRecoveryState.Resolved
					&& ExactActiveRecoveryQuest(recovery, out Quest quest))
					FinishRecoveryQuest(recovery, quest);
			}
		}

		private static void ReconcileRecoveryAtSeat(KingdomSystem system, Zone zone,
			GameObject excluded = null)
		{
			KingdomLifecycleBook book = system?.LifecycleBook;
			if (book == null || zone == null || book.Raid != null) return;
			KingdomRaidIncident recovery = FindRecovery(book.RaidLedger, book.SettlementId);
			if (recovery == null
				|| recovery.RecoveryState != KingdomRaidRecoveryState.Active
				|| !string.Equals(recovery.TargetZoneId, zone.ZoneID, StringComparison.Ordinal)
				|| CountLiveRaiders(zone, recovery.AttackOperationId, excluded) != 0) return;
			KingdomLifecycleOperation op = ResponseOperation(system, recovery,
				KingdomLifecycleAction.RaidRecoveryReady,
				"proved the raiding band absent and made watch recovery ready for turn-in",
				"{{G|The raiding band is gone. Return to the seat's charter to set the watch in order.}}",
				recovery.AttackOperationId);
			if (op == null) return;
			op.Origin = recovery.AttackOperationId;
			op.ObjectMarker = recovery.RecoveryStepId;
			PublishSimple(system, op);
		}

		private static bool TryNaturalSnapjawProvocation(KingdomSystem system)
		{
			if (!Enabled || system == null || system.Stage < GrowthStage.Steading
				|| system.Gate != KingdomRules.GatePolicy.Guarded
				|| !KingdomMaster.NewWorkAllowed(system)) return false;
			KingdomRaidProfile profile;
			if (!KingdomRaidProfiles.TryGet("Snapjaws", out profile)
				|| !string.Equals(profile.NaturalTrigger, "guarded-gate", StringComparison.Ordinal)
				|| string.IsNullOrEmpty(profile.NaturalCause)
				|| string.IsNullOrEmpty(profile.NaturalEvidence)) return false;
			string source = KingdomLifecycleRules.ChildId(system.LifecycleBook.SettlementId,
				"natural-snapjaw-guarded-gate", 0);
			return RecordProvocation(system, profile.Faction, profile.NaturalCause, source,
				profile.NaturalEvidence, ExactTargetZone(system, null), 1);
		}

		private static void ExecuteFortifyOrder(KingdomSystem system,
			KingdomRaidIncident incident, KingdomSurvey survey)
		{
			string commitment;
			int defence;
			if (!FreezeDefence(system, survey, out commitment, out defence))
			{
				PublishFortifyFailure(system, incident,
					"The remote muster found no exact named work with a live crew. Every answer is open again; nothing was lost.");
				return;
			}
			defence = ApplyWatchDisarray(system, defence);
			if (defence <= 0)
			{
				PublishFortifyFailure(system, incident,
					"The disordered watch left no effective defensive point. Every answer is open again; nothing was lost.");
				return;
			}
			KingdomLifecycleOperation op = ResponseOperation(system, incident,
				KingdomLifecycleAction.RaidFortify,
				"executed the remote muster against "
					+ DisplayFaction(incident.AttackerFactionId),
				"{{G|The remote order is now bound to exact named works and their current crews.}}",
				commitment);
			if (op == null) return;
			op.Defence = defence;
			PublishSimple(system, op);
		}

		private static bool PublishFortifyFailure(KingdomSystem system,
			KingdomRaidIncident incident, string notice)
		{
			KingdomLifecycleOperation op = ResponseOperation(system, incident,
				KingdomLifecycleAction.RaidFortifyFailure,
				"could not prove the named defensive muster against "
					+ DisplayFaction(incident.AttackerFactionId),
				"{{W|" + notice + "}}", null);
			return PublishSimple(system, op);
		}

		private static void LaunchRaid(KingdomSystem system, Zone zone,
			KingdomSurvey survey, KingdomRaidIncident incident)
		{
			KingdomRaidProfile profile;
			GrowthStage frozenStage;
			if (!KingdomRaidProfiles.TryResolveFrozen(incident.AttackerFactionId,
				incident.ForceProfileId, incident.Seed, incident.PlannedPartySize,
				out profile, out frozenStage))
			{
				CancelIncident(system, incident, KingdomRaidResolution.SourceInvalid,
					"The frozen faction profile no longer exists; the threat was cancelled without loss.");
				return;
			}
			GameObject objective = ExactStore(survey, incident.Seed);
			if (objective == null || objective.CurrentCell == null)
			{
				CancelIncident(system, incident, KingdomRaidResolution.NoValidObjective,
					"No exact dedicated water store remained at the named target; no substitute was chosen.");
				return;
			}
			int defence = 0;
			if (incident.State == KingdomRaidIncidentState.Fortified)
			{
				defence = ApplyWatchDisarray(system,
					RevalidateDefence(system, survey, incident));
				if (defence != incident.DefenceEstimate)
				{
					PublishFortifyFailure(system, incident,
						"One or more exact named works or crews no longer match the muster. Every answer is open again; no substitute work and no penalty were chosen.");
					return;
				}
			}
			int size = Math.Max(1, incident.PlannedPartySize);
			KingdomRules.RaidOutcome outcome = KingdomRules.ResolveRaid(defence, size);
			int party = KingdomRules.RaidingPartySize(size, defence, outcome);
			if (party <= 0)
			{
				CancelIncident(system, incident, KingdomRaidResolution.Repelled,
					"The named works turned the warband back before it entered the settlement.");
				return;
			}
			List<Cell> cells = DeterministicEntryCells(zone, objective.CurrentCell, incident.Seed);
			party = Math.Min(party, Math.Min(cells.Count, KingdomRaidIncidentRules.MaxParty));
			if (party <= 0)
			{
				CancelIncident(system, incident, KingdomRaidResolution.NoValidObjective,
					"No exact entry cell could receive the warband; no raid loss was inferred.");
				return;
			}
			List<GameObject> bodies = new List<GameObject>();
			for (int i = 0; i < party; i++)
			{
				string blueprint = KingdomRaidProfiles.Blueprint(profile, frozenStage, incident.Seed, i);
				GameObject body = null;
				try { body = GameObject.Create(blueprint); } catch { }
				if (!GameObject.Validate(body) || !string.Equals(body.Blueprint, blueprint,
					StringComparison.Ordinal)) return;
				bodies.Add(body);
			}
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(
				system.LifecycleBook, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidAttack, The.Game.TimeTicks);
			if (op == null) return;
			op.ZoneId = incident.TargetZoneId;
			op.ObjectId = incident.Id;
			op.Faction = incident.AttackerFactionId;
			op.DisplayFaction = DisplayFaction(incident.AttackerFactionId);
			op.Origin = objective.ID;
			op.ArrivalText = "stores";
			op.Target = objective.CurrentCell.X;
			op.Count = objective.CurrentCell.Y;
			op.Defence = defence;
			op.PartySize = party;
			op.PlunderRequested = KingdomRules.RaidPlunder(
				incident.MaximumPlunder, defence, outcome);
			op.EffectState = KingdomLifecyclePhysicalState.Prepared;
			for (int i = 0; i < party; i++)
			{
				string objectId = KingdomLifecycleRules.ChildId(op.Id, "raider", i);
				KingdomLifecycleProjection projection = KingdomLifecycleRules.RaidRuntimeAdapter.PrepareProjection(
					system.LifecycleBook, op, i, objectId, bodies[i].Blueprint,
					zone.ZoneID, cells[i].X, cells[i].Y);
				if (projection == null) return;
					bodies[i].ID = projection.ObjectId;
					PrepareRaiderBody(bodies[i], op, projection, incident.Id);
			}
			string display = op.DisplayFaction;
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op,
				"a warband of " + display + " entered " + KingdomPresentation.Rich(system.SeatName)
					+ " seeking the exact dedicated stores named in its grievance",
				"Raiders entered for store " + objective.ID + "; no plunder is recorded before contact.",
				"{{R|A warband of " + display + " enters the settlement and moves on the named stores!}}",
				"the watch that met the warband of " + display, null);
			if (!KingdomLifecycleRules.RaidRuntimeAdapter.PrepareLeases(system.LifecycleBook, op)
				|| !KingdomLifecycleRules.TryPublish(system.LifecycleBook, op)
				|| !KingdomLifecycleRules.AdvancePhase(system.LifecycleBook, op,
					KingdomLifecyclePhase.ProjectionIntent, The.Game.TimeTicks)) return;
			for (int i = 0; i < party; i++)
			{
				KingdomLifecycleProjection projection = op.Projections[i];
				GameObject ignored;
				int idsBefore;
				int markersBefore;
				CountProjection(zone, projection, out idsBefore, out markersBefore, out ignored);
				if (!KingdomLifecycleRules.RaidRuntimeAdapter.BeginProjection(system.LifecycleBook,
					op, projection, idsBefore, markersBefore)) return;
				GameObject accepted = null;
				try { accepted = cells[i].AddObject(bodies[i]); } catch { }
				KingdomSurvey.ObserveAddResultInActive(zone, bodies[i], accepted);
				GameObject exact;
				int idsAfter;
				int markersAfter;
				CountProjection(zone, projection, out idsAfter, out markersAfter, out exact);
				if (!ReferenceEquals(accepted, bodies[i]) || !ReferenceEquals(exact, bodies[i])
					|| !KingdomLifecycleRules.RaidRuntimeAdapter.CommitProjection(system.LifecycleBook,
						op, projection, idsAfter, markersAfter, bodies[i].Blueprint,
						zone.ZoneID, cells[i].X, cells[i].Y)) return;
					ActivateRaiderBody(bodies[i], system, objective);
			}
			ResumeOpen(system, zone);
		}

		private static void ResumeOpen(KingdomSystem system, Zone zone)
		{
			KingdomLifecycleBook book = system?.LifecycleBook;
			KingdomLifecycleOperation op = book?.Raid;
			if (op == null) return;
			for (int guard = 0; guard < 24 && book.Raid == op; guard++)
			{
				long now = Math.Max(The.Game.TimeTicks, op.UpdatedTick);
				switch (op.Phase)
				{
				case KingdomLifecyclePhase.Prepared:
					if (!KingdomLifecycleRules.AdvancePhase(book, op, NextAfterPrepared(op.Action), now)) return;
					break;
				case KingdomLifecyclePhase.ProjectionIntent:
					if (op.Action == KingdomLifecycleAction.RaidAttack
						&& !ResumeAttackProjections(system, zone, op)) return;
					if (op.Action == KingdomLifecycleAction.RaidDeliverDemand
						&& !ResumeDemandProjection(system, zone, op)) return;
					if (!AllProjectionsProved(op)
						|| !KingdomLifecycleRules.AdvancePhase(book, op,
							KingdomLifecyclePhase.Projected, now)) return;
					break;
				case KingdomLifecyclePhase.Projected:
					if (!KingdomLifecycleRules.AdvancePhase(book, op,
						op.Action == KingdomLifecycleAction.RaidAttack
							? KingdomLifecyclePhase.WaterIntent
							: KingdomLifecyclePhase.DomainIntent, now)) return;
					break;
				case KingdomLifecyclePhase.WaterIntent:
					if (!KingdomLifecycleRules.AdvancePhase(book, op,
						KingdomLifecyclePhase.WaterSettled, now)) return;
					break;
				case KingdomLifecyclePhase.WaterSettled:
					if (!KingdomLifecycleRules.AdvancePhase(book, op,
						KingdomLifecyclePhase.DomainIntent, now)) return;
					break;
				case KingdomLifecyclePhase.DomainIntent:
					if (!KingdomLifecycleRules.RaidRuntimeAdapter.ProveDomain(book, op)
						|| !KingdomLifecycleRules.AdvancePhase(book, op,
							KingdomLifecyclePhase.DomainSettled, now)) return;
					break;
				case KingdomLifecyclePhase.DomainSettled:
					if (!KingdomLifecycleRules.AdvancePhase(book, op,
						op.Action == KingdomLifecycleAction.RaidAttack
							? KingdomLifecyclePhase.EffectIntent : KingdomLifecyclePhase.Sinks, now)) return;
					break;
				case KingdomLifecyclePhase.EffectIntent:
					InspectOpenAttack(system, zone, op);
					return;
				case KingdomLifecyclePhase.EffectsSettled:
					if (!KingdomLifecycleRules.AdvancePhase(book, op, KingdomLifecyclePhase.Sinks, now)) return;
					break;
				case KingdomLifecyclePhase.Sinks:
					if (!DispatchOutbox(system, op)
						|| !KingdomLifecycleRules.AdvancePhase(book, op,
							KingdomLifecyclePhase.ScheduleIntent, now)) return;
					break;
				case KingdomLifecyclePhase.ScheduleIntent:
					if (!KingdomLifecycleRules.RaidRuntimeAdapter.ProveSchedule(book, op)
						|| !KingdomLifecycleRules.AdvancePhase(book, op,
							KingdomLifecyclePhase.Terminal, now)) return;
					break;
				case KingdomLifecyclePhase.Terminal:
					if (op.Action == KingdomLifecycleAction.RaidAttack)
					{
						KingdomRaidResolution result;
						string notice;
						if (!TryDeriveAttackResult(zone, op, out result, out notice))
						{
							KingdomLifecycleRules.Quarantine(op,
								"settled raid attack had no exact terminal result witness");
							return;
						}
						int plunder = op.PlunderProved;
						if (!KingdomLifecycleRules.Retire(book, op, now)) return;
						ResolveIncident(system, result, plunder, notice);
						return;
					}
					KingdomLifecycleRules.Retire(book, op, now);
					return;
				default:
					return;
				}
			}
		}

		private static KingdomLifecyclePhase NextAfterPrepared(KingdomLifecycleAction action)
		{
			if (action == KingdomLifecycleAction.RaidAttack
				|| action == KingdomLifecycleAction.RaidDeliverDemand)
				return KingdomLifecyclePhase.ProjectionIntent;
			if (action == KingdomLifecycleAction.RaidTribute) return KingdomLifecyclePhase.WaterIntent;
			return KingdomLifecyclePhase.DomainIntent;
		}

		private static void InspectOpenAttack(KingdomSystem system, Zone zone,
			KingdomLifecycleOperation op)
		{
			if (zone == null || !string.Equals(zone.ZoneID, op.ZoneId, StringComparison.Ordinal)) return;
			if (op.EffectState == KingdomLifecyclePhysicalState.Intent)
			{
				KingdomLifecycleRules.Quarantine(op,
					"raid contact intent survived without an exact debit receipt");
				return;
			}
			if (op.EffectState == KingdomLifecyclePhysicalState.Proved
				|| op.EffectState == KingdomLifecyclePhysicalState.Skipped)
			{
				if (KingdomLifecycleRules.AdvancePhase(system.LifecycleBook, op,
					KingdomLifecyclePhase.EffectsSettled, The.Game.TimeTicks))
					ResumeOpen(system, zone);
				return;
			}
			GameObject target = FindExact(zone, op.Origin);
			if (target == null || target.CurrentCell == null || target.CurrentCell.X != op.Target
				|| target.CurrentCell.Y != op.Count || target.GetIntProperty("KingdomStores") != 1
				|| target.GetPart<LiquidVolume>() == null)
			{
				if (KingdomLifecycleRules.RaidRuntimeAdapter.SkipEffectWithoutContact(
					system.LifecycleBook, op))
				{
					KingdomLifecycleRules.AdvancePhase(system.LifecycleBook, op,
						KingdomLifecyclePhase.EffectsSettled, The.Game.TimeTicks);
					ResumeOpen(system, zone);
				}
				return;
			}
			if (CountLiveRaiders(zone, op.Id) == 0
				&& KingdomLifecycleRules.RaidRuntimeAdapter.SkipEffectWithoutContact(
					system.LifecycleBook, op))
			{
				KingdomLifecycleRules.AdvancePhase(system.LifecycleBook, op,
					KingdomLifecyclePhase.EffectsSettled, The.Game.TimeTicks);
				ResumeOpen(system, zone);
			}
		}

		private static void ProveObjectiveContact(KingdomSystem system, Zone zone,
			KingdomLifecycleOperation op, string targetId, int x, int y)
		{
			if (system == null || zone == null || op == null
				|| op.Phase != KingdomLifecyclePhase.EffectIntent
				|| !string.Equals(op.Origin, targetId, StringComparison.Ordinal)
				|| op.Target != x || op.Count != y) return;
			GameObject target = FindExact(zone, targetId);
			LiquidVolume liquid = target?.GetPart<LiquidVolume>();
			if (!GameObject.Validate(target) || target.CurrentCell == null
				|| target.CurrentCell.X != x || target.CurrentCell.Y != y
				|| target.GetIntProperty("KingdomStores") != 1 || liquid == null) return;
			int amount = KingdomLiquids.HasFreshWater(liquid)
				? Math.Min(op.PlunderRequested, liquid.Volume) : 0;
			KingdomWaterDebit debit = null;
			if (amount > 0)
			{
				KingdomSurvey exact = new KingdomSurvey();
				exact.Stores.Add(liquid);
				exact.StoredWater = liquid.Volume;
				exact.StorageCapacity = liquid.MaxVolume;
				exact.StorageSpace = Math.Max(0, liquid.MaxVolume - liquid.Volume);
				debit = exact.ReserveExactWater(amount);
				if (debit == null) return;
				if (!debit.Commit())
				{
					RestoreDebitOrQuarantine(system, op, debit,
						"raid plunder debit could not prove an exact physical result");
					return;
				}
				if (debit.Spent != amount || debit.Outstanding != 0 || !debit.MeasurementExact)
				{
					RestoreDebitOrQuarantine(system, op, debit,
						"raid plunder debit disagreed with its exact receipt");
					return;
				}
			}
			if (!KingdomLifecycleRules.RaidRuntimeAdapter.BeginEffect(system.LifecycleBook, op, true))
			{
				RestoreDebitOrQuarantine(system, op, debit,
					"raid contact proof rejected after a physical debit");
				return;
			}
			if (!KingdomLifecycleRules.RaidRuntimeAdapter.CommitEffect(system.LifecycleBook,
				op, true, amount))
			{
				bool restored = RestoreDebitOrQuarantine(system, op, debit,
					"raid contact proof failed after its intent was recorded");
				if (restored) KingdomLifecycleRules.Quarantine(op,
					"raid contact proof failed after its intent was recorded");
				return;
			}
			if (!KingdomLifecycleRules.AdvancePhase(system.LifecycleBook, op,
				KingdomLifecyclePhase.EffectsSettled, The.Game.TimeTicks))
			{
				KingdomLifecycleRules.Quarantine(op,
					"proved raid plunder could not advance to its settled phase");
				return;
			}
			ResumeOpen(system, zone);
		}

		private static bool TryDeriveAttackResult(Zone zone,
			KingdomLifecycleOperation op, out KingdomRaidResolution resolution,
			out string notice)
		{
			resolution = KingdomRaidResolution.None;
			notice = null;
			if (zone == null || op == null || op.Action != KingdomLifecycleAction.RaidAttack
				|| !string.Equals(zone.ZoneID, op.ZoneId, StringComparison.Ordinal)) return false;
			if (op.EffectState == KingdomLifecyclePhysicalState.Proved)
			{
				if (op.PlunderProved > 0)
				{
					resolution = KingdomRaidResolution.StoresPlundered;
					notice = op.PlunderProved
						+ " drams were physically taken after contact with the named store.";
				}
				else
				{
					resolution = KingdomRaidResolution.ObjectiveLost;
					notice = "The warband reached the named store, but found no water to take.";
				}
				return true;
			}
			if (op.EffectState != KingdomLifecyclePhysicalState.Skipped
				|| op.PlunderProved != 0) return false;
			GameObject target = FindExact(zone, op.Origin);
			if (!GameObject.Validate(target) || target.CurrentCell == null
				|| target.CurrentCell.X != op.Target || target.CurrentCell.Y != op.Count
				|| target.GetIntProperty("KingdomStores") != 1
				|| target.GetPart<LiquidVolume>() == null)
			{
				resolution = KingdomRaidResolution.ObjectiveLost;
				notice = "The named store was gone; the raid took no substitute objective.";
				return true;
			}
			if (CountLiveRaiders(zone, op.Id) != 0) return false;
			resolution = KingdomRaidResolution.RaidersDefeated;
			notice = "The last marked raider fell before reaching the stores.";
			return true;
		}

		private static bool ResolveIncident(KingdomSystem system,
			KingdomRaidResolution resolution, int plunder, string notice)
		{
			KingdomRaidIncident incident = KingdomRaidIncidentRules.Active(system?.LifecycleBook?.RaidLedger);
			if (incident == null || incident.State != KingdomRaidIncidentState.Active) return false;
			KingdomLifecycleOperation op = ResponseOperation(system, incident,
				KingdomLifecycleAction.RaidResolve,
				"the raid of " + DisplayFaction(incident.AttackerFactionId) + " ended: " + notice,
				"{{W|" + notice + "}}", null);
			if (op == null) return false;
			op.Kind = (int)resolution;
			op.Target = plunder;
			return PublishSimple(system, op);
		}

		private static bool CancelIncident(KingdomSystem system,
			KingdomRaidIncident incident, KingdomRaidResolution resolution, string notice)
		{
			KingdomLifecycleOperation op = ResponseOperation(system, incident,
				KingdomLifecycleAction.RaidCancel,
				"the raid of " + DisplayFaction(incident.AttackerFactionId) + " ended: " + notice,
				"{{W|" + notice + "}}", null);
			if (op == null) return false;
			op.Kind = (int)resolution;
			return PublishSimple(system, op);
		}

		private static bool DispatchOutbox(KingdomSystem system, KingdomLifecycleOperation op)
		{
			if (op?.Outbox == null) return false;
			KingdomLifecycleRules.RecoverOutbox(system.LifecycleBook, op);
			if (!Deliver(system, op, KingdomLifecycleSinkMask.Chronicle,
				delegate { return KingdomChronicle.RecordOnce(system, op.Outbox.ChronicleReceiptId,
					op.Outbox.Chronicle, op.Outbox.ChronicleAccomplishment); })) return false;
			if (!Deliver(system, op, KingdomLifecycleSinkMask.Ledger,
				delegate { system.Ledger.Note(op.Outbox.Ledger); return true; })) return false;
			if (!Deliver(system, op, KingdomLifecycleSinkMask.Message,
				delegate { MessageQueue.AddPlayerMessage(op.Outbox.Message); return true; })) return false;
			if (!Deliver(system, op, KingdomLifecycleSinkMask.Deed,
				delegate { system.RecordDeed(op.Outbox.Deed); return true; })) return false;
			return KingdomLifecycleRules.SinkSettled(op.Outbox.ChronicleState)
				&& KingdomLifecycleRules.SinkSettled(op.Outbox.LedgerState)
				&& KingdomLifecycleRules.SinkSettled(op.Outbox.MessageState)
				&& KingdomLifecycleRules.SinkSettled(op.Outbox.DeedState)
				&& KingdomLifecycleRules.SinkSettled(op.Outbox.GuestbookState);
		}

		private static bool Deliver(KingdomSystem system, KingdomLifecycleOperation op,
			KingdomLifecycleSinkMask sink, Func<bool> callback)
		{
			KingdomLifecycleSinkState state = SinkState(op.Outbox, sink);
			if (KingdomLifecycleRules.SinkSettled(state)) return true;
			if (!KingdomLifecycleRules.RaidRuntimeAdapter.BeginSink(system.LifecycleBook, op, sink))
				return false;
			bool delivered = false;
			try { delivered = callback(); } catch { }
			return delivered && KingdomLifecycleRules.RaidRuntimeAdapter.CommitSink(
				system.LifecycleBook, op, sink);
		}

		private static KingdomLifecycleSinkState SinkState(KingdomLifecycleOutbox box,
			KingdomLifecycleSinkMask sink)
		{
			switch (sink)
			{
			case KingdomLifecycleSinkMask.Chronicle: return box.ChronicleState;
			case KingdomLifecycleSinkMask.Ledger: return box.LedgerState;
			case KingdomLifecycleSinkMask.Message: return box.MessageState;
			case KingdomLifecycleSinkMask.Deed: return box.DeedState;
			default: return box.GuestbookState;
			}
		}

		private static void ObserveOption(KingdomLifecycleBook book, long now)
		{
			KingdomLifecycleOptionDecision decision = KingdomLifecycleRules.ObserveOption(
				book.RaidOption, book.RaidOptionTick, Enabled, now, book.Raid != null);
			if (!decision.Valid) return;
			book.RaidOption = decision.State;
			book.RaidOptionTick = decision.Tick;
		}

		private static void MigrateLegacyEvidence(KingdomSystem system)
		{
			KingdomLifecycleBook book = system.LifecycleBook;
			if (book == null || !KingdomLifecycleRules.CanOwnAuthority(book)
				|| book.Raid != null || book.RaidLedger.LegacyEvidenceArchived
				|| KingdomRaidIncidentRules.Active(book.RaidLedger) != null) return;
			if (system.RaidState == 0 && string.IsNullOrEmpty(system.RaidFactionName)
				&& system.RaidDueTick == 0L && system.LastRaidTick == 0L
				&& system.RaidTimesDeferred == 0) return;
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.Raid, KingdomLifecycleAction.RaidCancel, The.Game.TimeTicks);
			if (op == null) return;
			op.Kind = (int)KingdomRaidResolution.LegacyWarningDispersed;
			op.Target = Math.Max(0, system.RaidState);
			op.Count = Math.Max(0, system.RaidTimesDeferred);
			op.Faction = system.RaidFactionName;
			op.DepartTick = Math.Max(0L, system.RaidDueTick);
			op.Origin = Math.Max(0L, system.LastRaidTick).ToString(CultureInfo.InvariantCulture);
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op,
				"an old standing-derived raid warning was archived and dispersed without causal reinterpretation",
				"Legacy raid evidence retained: state " + op.Target + ", faction "
					+ (op.Faction ?? "none") + ", due " + op.DepartTick + ", last " + op.Origin
					+ ", deferrals " + op.Count + ".",
				"{{W|An old standing-derived warning is archived. It causes no raid and takes nothing.}}",
				null, null);
			if (!PublishSimple(system, op) || !book.RaidLedger.LegacyEvidenceArchived) return;
			system.RaidState = 0;
			system.RaidFactionName = null;
			system.RaidDueTick = 0L;
			system.LastRaidTick = 0L;
			system.RaidTimesDeferred = 0;
		}

		private static string ExactTargetZone(KingdomSystem system, string preferred)
		{
			if (system?.ClaimedZones == null || system.ClaimedZones.Count == 0) return null;
			if (!string.IsNullOrEmpty(preferred) && system.ClaimedZones.Contains(preferred)) return preferred;
			string best = null;
			for (int i = 0; i < system.ClaimedZones.Count; i++)
				if (!string.IsNullOrEmpty(system.ClaimedZones[i]) && (best == null
					|| string.CompareOrdinal(system.ClaimedZones[i], best) < 0)) best = system.ClaimedZones[i];
			return best;
		}

		private static bool IncidentSourceStillValid(KingdomSystem system,
			KingdomRaidIncident incident)
		{
			if (system == null || incident == null || system.ClaimedZones == null
				|| !system.ClaimedZones.Contains(incident.TargetZoneId)
				|| Factions.GetIfExists(incident.AttackerFactionId) == null) return false;
			KingdomRaidProfile profile;
			GrowthStage frozenStage;
			return KingdomRaidProfiles.TryResolveFrozen(incident.AttackerFactionId,
				incident.ForceProfileId, incident.Seed, incident.PlannedPartySize,
				out profile, out frozenStage);
		}

		private static bool FreezeDefence(KingdomSystem system, KingdomSurvey survey,
			out string commitment,
			out int total)
		{
			commitment = null; total = 0;
			if (system == null || survey?.Ground == null || survey.Defences == null
				|| survey.Settlers == null) return false;
			Dictionary<int, KingdomResidentRow> residentRows;
			Dictionary<int, GameObject> residentBodies;
			if (!TryDefenceResidents(system, survey, out residentRows, out residentBodies)) return false;
			List<KingdomRaidDefenceReservation> rows =
				new List<KingdomRaidDefenceReservation>();
			HashSet<int> works = new HashSet<int>();
			HashSet<int> reservedCrew = new HashSet<int>();
			for (int i = 0; i < survey.Defences.Count; i++)
			{
				GameObject work = survey.Defences[i];
				int score = DefenceOf(work);
				if (!GameObject.Validate(work) || score <= 0) continue;
				int workId = KingdomCityRules.StableId(work.ID);
				if (workId <= 0 || !works.Add(workId)
					|| rows.Count >= KingdomRaidIncidentRules.MaxDefenceWorks) return false;
				List<int> crew;
				if (!TryExactDefenceCrew(system, survey, work, workId, residentRows,
					residentBodies, reservedCrew, out crew)) return false;
				rows.Add(new KingdomRaidDefenceReservation
				{
					WorkId = workId,
					FrozenScore = score,
					CrewSemanticIds = crew
				});
			}
			return KingdomRaidIncidentRules.TryEncodeDefenceReservations(rows,
				out commitment, out total);
		}

		private static int RevalidateDefence(KingdomSystem system, KingdomSurvey survey,
			KingdomRaidIncident incident)
		{
			if (system == null || survey?.Ground == null || survey.Defences == null
				|| incident == null
				|| incident.DefenceReservationVersion
					!= KingdomRaidIncidentRules.CurrentDefenceReservationVersion) return 0;
			List<KingdomRaidDefenceReservation> decoded;
			int frozenTotal;
			if (!KingdomRaidIncidentRules.TryDecodeDefenceReservations(
				incident.DefenceCommitment, out decoded, out frozenTotal)
				|| frozenTotal != incident.DefenceEstimate
				|| !SameDefenceReservations(decoded, incident.DefenceReservations)) return 0;
			Dictionary<int, KingdomResidentRow> residentRows;
			Dictionary<int, GameObject> residentBodies;
			if (!TryDefenceResidents(system, survey, out residentRows, out residentBodies)) return 0;
			Dictionary<int, GameObject> current = new Dictionary<int, GameObject>();
			for (int i = 0; i < survey.Defences.Count; i++)
			{
				GameObject work = survey.Defences[i];
				int score = DefenceOf(work);
				if (!GameObject.Validate(work) || score <= 0) continue;
				int workId = KingdomCityRules.StableId(work.ID);
				if (workId <= 0 || current.ContainsKey(workId)) return 0;
				current.Add(workId, work);
			}
			HashSet<int> reservedCrew = new HashSet<int>();
			long total = 0L;
			for (int i = 0; i < decoded.Count; i++)
			{
				KingdomRaidDefenceReservation frozen = decoded[i];
				GameObject work;
				if (!current.TryGetValue(frozen.WorkId, out work)
					|| DefenceOf(work) != frozen.FrozenScore) return 0;
				List<int> liveCrew;
				if (!TryExactDefenceCrew(system, survey, work, frozen.WorkId, residentRows,
					residentBodies, reservedCrew, out liveCrew)
					|| !SameIds(liveCrew, frozen.CrewSemanticIds)) return 0;
				total += frozen.FrozenScore;
				if (total > KingdomLifecycleRules.MaxPhysicalCount) return 0;
			}
			return (int)total;
		}

		private static bool TryDefenceResidents(KingdomSystem system, KingdomSurvey survey,
			out Dictionary<int, KingdomResidentRow> rows,
			out Dictionary<int, GameObject> bodies)
		{
			rows = new Dictionary<int, KingdomResidentRow>();
			bodies = new Dictionary<int, GameObject>();
			List<KingdomResidentRow> roll = KingdomResidents.RollRows(system, true);
			for (int i = 0; i < roll.Count; i++)
			{
				KingdomResidentRow row = roll[i];
				if (row.ResidentId <= 0 || rows.ContainsKey(row.ResidentId)) return false;
				rows.Add(row.ResidentId, row);
			}
			for (int i = 0; i < survey.Settlers.Count; i++)
			{
				GameObject body = survey.Settlers[i];
				int residentId = KingdomResidents.IdOf(body);
				if (residentId <= 0) continue;
				if (bodies.ContainsKey(residentId)) return false;
				bodies.Add(residentId, body);
			}
			return true;
		}

		private static bool TryExactDefenceCrew(KingdomSystem system, KingdomSurvey survey,
			GameObject work, int workId, Dictionary<int, KingdomResidentRow> rows,
			Dictionary<int, GameObject> bodies, HashSet<int> reserved, out List<int> crew)
		{
			crew = new List<int>();
			if (!GameObject.Validate(work) || workId <= 0 || rows == null || bodies == null
				|| reserved == null) return false;
			foreach (KeyValuePair<int, GameObject> pair in bodies)
			{
				GameObject body = pair.Value;
				if (KingdomStations.PostOf(body) != workId) continue;
				KingdomResidentRow row;
				GameObject exact;
				string zoneId;
				if (!rows.TryGetValue(pair.Key, out row) || row.JobWorkId != workId
					|| !string.Equals(row.BoundZoneId, survey.Ground.ZoneID,
						StringComparison.Ordinal)
					|| !KingdomResidents.TryResolveBoundBody(system, pair.Key, false,
						out exact, out zoneId)
					|| !ReferenceEquals(exact, body)
					|| !string.Equals(zoneId, survey.Ground.ZoneID, StringComparison.Ordinal)
					|| !reserved.Add(pair.Key)) return false;
				crew.Add(pair.Key);
			}
			crew.Sort();
			int need = work.GetIntProperty("KingdomStaffNeeded");
			return need > 0 ? crew.Count > 0 && crew.Count <= need : crew.Count == 0;
		}

		private static bool SameDefenceReservations(IList<KingdomRaidDefenceReservation> a,
			IList<KingdomRaidDefenceReservation> b)
		{
			if (a == null || b == null || a.Count != b.Count) return false;
			for (int i = 0; i < a.Count; i++)
				if (a[i] == null || b[i] == null || a[i].WorkId != b[i].WorkId
					|| a[i].FrozenScore != b[i].FrozenScore
					|| !SameIds(a[i].CrewSemanticIds, b[i].CrewSemanticIds)) return false;
			return true;
		}

		private static bool SameIds(IList<int> a, IList<int> b)
		{
			if (a == null || b == null || a.Count != b.Count) return false;
			for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
			return true;
		}

		private static int DefenceOf(GameObject work)
		{
			if (!GameObject.Validate(work)) return 0;
			int need = work.GetIntProperty("KingdomStaffNeeded");
			int effectiveness = need > 0 ? work.GetIntProperty("KingdomEffectiveness") : 100;
			effectiveness = KingdomCrews.ApplyAffinity(work, effectiveness);
			int defence = work.GetIntProperty("KingdomDefence");
			if (defence <= 0 || effectiveness <= 0) return 0;
			long score = (long)defence * effectiveness / 100L;
			return (int)Math.Min(KingdomLifecycleRules.MaxPhysicalCount, score);
		}

		private static GameObject ExactStore(KingdomSurvey survey, long seed)
		{
			List<GameObject> stores = new List<GameObject>();
			if (survey?.Stores == null) return null;
			for (int i = 0; i < survey.Stores.Count; i++)
			{
				LiquidVolume liquid = survey.Stores[i];
				GameObject owner = liquid?.ParentObject;
				if (GameObject.Validate(owner) && owner.CurrentCell != null
					&& owner.GetIntProperty("KingdomStores") == 1
					&& liquid.Volume > 0 && KingdomLiquids.HasFreshWater(liquid)) stores.Add(owner);
			}
			stores.Sort(delegate(GameObject a, GameObject b) { return string.CompareOrdinal(a.ID, b.ID); });
			return stores.Count == 0 ? null : stores[(int)(seed % stores.Count)];
		}

		private static List<Cell> DeterministicEntryCells(Zone zone, Cell objective, long seed)
		{
			if (zone == null || objective == null) return new List<Cell>();
			bool[,] reachable = new bool[zone.Width, zone.Height];
			Queue<Cell> pending = new Queue<Cell>();
			int[] dx = new int[4] { -1, 1, 0, 0 };
			int[] dy = new int[4] { 0, 0, -1, 1 };
			for (int i = 0; i < 4; i++)
			{
				int x = objective.X + dx[i];
				int y = objective.Y + dy[i];
				if (x < 0 || x >= zone.Width || y < 0 || y >= zone.Height) continue;
				Cell start = zone.GetCell(x, y);
				if (start == null || !start.IsPassable(null, false) || reachable[x, y]) continue;
				reachable[x, y] = true;
				pending.Enqueue(start);
			}
			while (pending.Count > 0)
			{
				Cell from = pending.Dequeue();
				for (int i = 0; i < 4; i++)
				{
					int x = from.X + dx[i];
					int y = from.Y + dy[i];
					if (x < 0 || x >= zone.Width || y < 0 || y >= zone.Height
						|| reachable[x, y]) continue;
					Cell next = zone.GetCell(x, y);
					if (next == null || !next.IsPassable(null, false)) continue;
					reachable[x, y] = true;
					pending.Enqueue(next);
				}
			}
			List<Cell> cells = zone.GetEmptyCells(delegate(Cell c)
			{
				return (c.X == 0 || c.X == zone.Width - 1 || c.Y == 0 || c.Y == zone.Height - 1)
					&& c.IsPassable(null, false) && reachable[c.X, c.Y];
			}) ?? new List<Cell>();
			cells.Sort(delegate(Cell a, Cell b)
			{
				int x = a.X.CompareTo(b.X); return x != 0 ? x : a.Y.CompareTo(b.Y);
			});
			if (cells.Count > 1)
			{
				int offset = (int)(seed % cells.Count);
				List<Cell> rotated = new List<Cell>(cells.Count);
				for (int i = 0; i < cells.Count; i++) rotated.Add(cells[(i + offset) % cells.Count]);
				return rotated;
			}
			return cells;
		}

		private static void CountProjection(Zone zone, KingdomLifecycleProjection projection,
			out int ids, out int markers, out GameObject exact)
		{
			ids = 0; markers = 0; exact = null;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(zone))
			{
				if (item.ID == projection.ObjectId) { ids++; exact = item; }
				if (item.GetStringProperty(ProjectionMarkerProperty) == projection.Marker) markers++;
			}
		}

		private static void CountDemandProjection(GameObject owner,
			KingdomLifecycleProjection projection, out int ids, out int markers,
			out GameObject exact)
		{
			ids = 0; markers = 0; exact = null;
			if (!GameObject.Validate(owner) || owner.Inventory == null
				|| owner.Inventory.Objects == null) return;
			for (int i = 0; i < owner.Inventory.Objects.Count; i++)
			{
				GameObject item = owner.Inventory.Objects[i];
				if (item.ID == projection.ObjectId) { ids++; exact = item; }
				if (item.GetStringProperty(ProjectionMarkerProperty) == projection.Marker) markers++;
			}
		}

		private static bool ResumeDemandProjection(KingdomSystem system, Zone zone,
			KingdomLifecycleOperation op)
		{
			GameObject owner = The.Player;
			if (system == null || zone == null || op == null || !GameObject.Validate(owner)
				|| op.Action != KingdomLifecycleAction.RaidDeliverDemand
				|| op.Projections.Count != 1 || owner.ID != op.Projections[0].OwnerId
				|| zone.ZoneID != op.Projections[0].ZoneId) return false;
			KingdomLifecycleProjection projection = op.Projections[0];
			for (int guard = 0; guard < 2; guard++)
			{
				GameObject exact;
				int ids;
				int markers;
				CountDemandProjection(owner, projection, out ids, out markers, out exact);
				if (projection.State == KingdomLifecyclePhysicalState.Proved)
					return ids == 1 && markers == 1 && ExactDemandBody(exact, op, projection);
				if (projection.State == KingdomLifecyclePhysicalState.Intent)
				{
					if (ids == 0 && markers == 0)
					{
						if (!KingdomLifecycleRules.RaidRuntimeAdapter.ResetAbsentProjectionIntent(
							system.LifecycleBook, op, projection, ids, markers)) return false;
						continue;
					}
					if (!ExactDemandBody(exact, op, projection)
						|| !KingdomLifecycleRules.RaidRuntimeAdapter.CommitProjection(
							system.LifecycleBook, op, projection, ids, markers, exact.Blueprint,
							owner.ID, zone.ZoneID, -1, -1))
					{
						KingdomLifecycleRules.Quarantine(op,
							"demand delivery intent had ambiguous physical evidence");
						return false;
					}
					return true;
				}
				if (projection.State != KingdomLifecyclePhysicalState.Prepared
					|| ids != 0 || markers != 0) return false;
				GameObject body = null;
				try { body = GameObject.Create(projection.Blueprint); } catch { }
				if (!GameObject.Validate(body) || body.Blueprint != projection.Blueprint) return false;
				body.ID = projection.ObjectId;
				PrepareDemandBody(body, op, projection);
				if (!KingdomLifecycleRules.RaidRuntimeAdapter.BeginProjection(
					system.LifecycleBook, op, projection, ids, markers)) return false;
				GameObject accepted = null;
				try { accepted = owner.Inventory.AddObject(body, null, Silent: true, NoStack: true); }
				catch { }
				KingdomSurvey.ObserveCurrentTopologyInActive(zone, owner);
				KingdomSurvey.ObserveAddResultInActive(zone, body, accepted);
				CountDemandProjection(owner, projection, out ids, out markers, out exact);
				if (!ReferenceEquals(accepted, body) || !ReferenceEquals(exact, body)
					|| !KingdomLifecycleRules.RaidRuntimeAdapter.CommitProjection(
						system.LifecycleBook, op, projection, ids, markers, body.Blueprint,
						owner.ID, zone.ZoneID, -1, -1)) return false;
				return true;
			}
			return false;
		}

		private static void PrepareDemandBody(GameObject body,
			KingdomLifecycleOperation op, KingdomLifecycleProjection projection)
		{
			body.SetStringProperty(ProjectionMarkerProperty, projection.Marker);
			r_KingdomRaidDemand part = body.RequirePart<r_KingdomRaidDemand>();
			part.IncidentId = op.ObjectId; part.ChannelId = op.Origin;
			part.Revision = op.Target; part.Inert = false;
		}

		private static bool ExactDemandBody(GameObject body,
			KingdomLifecycleOperation op, KingdomLifecycleProjection projection)
		{
			if (!GameObject.Validate(body) || body.ID != projection.ObjectId
				|| body.Blueprint != projection.Blueprint
				|| body.GetStringProperty(ProjectionMarkerProperty) != projection.Marker
				|| !ReferenceEquals(body.InInventory, The.Player)) return false;
			r_KingdomRaidDemand part = body.GetPart<r_KingdomRaidDemand>();
			return part != null && !part.Inert && part.IncidentId == op.ObjectId
				&& part.ChannelId == op.Origin && part.Revision == op.Target;
		}

		private static bool ResumeAttackProjections(KingdomSystem system, Zone zone,
			KingdomLifecycleOperation op)
		{
			if (system == null || zone == null || op == null
				|| !string.Equals(zone.ZoneID, op.ZoneId, StringComparison.Ordinal)) return false;
			GameObject objective = FindExact(zone, op.Origin);
			if (!GameObject.Validate(objective) || objective.CurrentCell == null
				|| objective.CurrentCell.X != op.Target || objective.CurrentCell.Y != op.Count
				|| objective.GetIntProperty("KingdomStores") != 1)
			{
				KingdomLifecycleRules.Quarantine(op,
					"raid projection recovery lost its frozen objective witness");
				return false;
			}
			for (int i = 0; i < op.Projections.Count; i++)
			{
				KingdomLifecycleProjection projection = op.Projections[i];
				if (projection.State == KingdomLifecyclePhysicalState.Proved) continue;
				GameObject exact;
				int ids;
				int markers;
				CountProjection(zone, projection, out ids, out markers, out exact);
				if (projection.State == KingdomLifecyclePhysicalState.Intent)
				{
					if (ids == 0 && markers == 0)
					{
						if (!KingdomLifecycleRules.RaidRuntimeAdapter.ResetAbsentProjectionIntent(
							system.LifecycleBook, op, projection, ids, markers))
						{
							KingdomLifecycleRules.Quarantine(op,
								"absent raid projection intent could not be retried exactly");
							return false;
						}
					}
					else
					{
						if (!ExactRaiderBody(exact, op, projection)
							|| !KingdomLifecycleRules.RaidRuntimeAdapter.CommitProjection(
								system.LifecycleBook, op, projection, ids, markers,
								exact.Blueprint, zone.ZoneID, exact.CurrentCell.X,
								exact.CurrentCell.Y))
						{
							KingdomLifecycleRules.Quarantine(op,
								"raid projection intent had ambiguous physical evidence");
							return false;
						}
						ActivateRaiderBody(exact, system, objective);
						continue;
					}
				}
				if (projection.State != KingdomLifecyclePhysicalState.Prepared
					|| ids != 0 || markers != 0)
				{
					KingdomLifecycleRules.Quarantine(op,
						"prepared raid projection had non-pristine physical evidence");
					return false;
				}
				GameObject body = null;
				try { body = GameObject.Create(projection.Blueprint); } catch { }
				Cell cell = zone.GetCell(projection.X, projection.Y);
				if (!GameObject.Validate(body) || body.Blueprint != projection.Blueprint
					|| cell == null || !cell.IsPassable(null, false))
				{
					KingdomLifecycleRules.Quarantine(op,
						"raid projection body or frozen entry cell could not be recreated");
					return false;
				}
				body.ID = projection.ObjectId;
				PrepareRaiderBody(body, op, projection, op.ObjectId);
				if (!KingdomLifecycleRules.RaidRuntimeAdapter.BeginProjection(
					system.LifecycleBook, op, projection, ids, markers)) return false;
				GameObject accepted = null;
				try { accepted = cell.AddObject(body); } catch { }
				KingdomSurvey.ObserveAddResultInActive(zone, body, accepted);
				CountProjection(zone, projection, out ids, out markers, out exact);
				if (!ReferenceEquals(accepted, body) || !ReferenceEquals(exact, body)
					|| !KingdomLifecycleRules.RaidRuntimeAdapter.CommitProjection(
						system.LifecycleBook, op, projection, ids, markers, body.Blueprint,
						zone.ZoneID, cell.X, cell.Y)) return false;
				ActivateRaiderBody(body, system, objective);
			}
			return AllProjectionsProved(op);
		}

		private static void PrepareRaiderBody(GameObject body,
			KingdomLifecycleOperation op, KingdomLifecycleProjection projection,
			string incidentId)
		{
			body.SetStringProperty(ProjectionMarkerProperty, projection.Marker);
			body.SetIntProperty("KingdomRaider", 1);
			body.RequirePart<NoXPGain>();
			body.AddPart(new r_KingdomRaiderObjective(op.Id, incidentId,
				op.Origin, op.Target, op.Count));
		}

		private static bool ExactRaiderBody(GameObject body,
			KingdomLifecycleOperation op, KingdomLifecycleProjection projection)
		{
			if (!GameObject.Validate(body) || body.CurrentCell == null
				|| body.ID != projection.ObjectId || body.Blueprint != projection.Blueprint
				|| body.CurrentZone?.ZoneID != projection.ZoneId
				|| body.CurrentCell.X != projection.X || body.CurrentCell.Y != projection.Y
				|| body.GetStringProperty(ProjectionMarkerProperty) != projection.Marker
				|| body.GetIntProperty("KingdomRaider") != 1
				|| body.GetPart<NoXPGain>() == null) return false;
			r_KingdomRaiderObjective part = body.GetPart<r_KingdomRaiderObjective>();
			return part != null && part.OperationId == op.Id && part.IncidentId == op.ObjectId
				&& part.TargetObjectId == op.Origin && part.TargetX == op.Target
				&& part.TargetY == op.Count;
		}

		private static void ActivateRaiderBody(GameObject body, KingdomSystem system,
			GameObject objective)
		{
			body.MakeActive();
			if (body.Brain == null || objective?.CurrentCell == null) return;
			body.Brain.Allegiance["Player"] = -100;
			if (!string.IsNullOrEmpty(system.KingdomFactionName))
				body.Brain.Allegiance[system.KingdomFactionName] = -100;
			body.Brain.PushGoal(new MoveTo(objective.CurrentCell, careful: true));
		}

		private static bool AllProjectionsProved(KingdomLifecycleOperation op)
		{
			for (int i = 0; i < op.Projections.Count; i++)
				if (op.Projections[i].State != KingdomLifecyclePhysicalState.Proved) return false;
			return op.Projections.Count > 0;
		}

		private static GameObject FindExact(Zone zone, string id)
		{
			GameObject found = null;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(zone))
				if (item.ID == id) { if (found != null) return null; found = item; }
			return found;
		}

		private static int CountLiveRaiders(Zone zone, string operationId,
			GameObject excluded = null)
		{
			int count = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(zone))
			{
				r_KingdomRaiderObjective part = item.GetPart<r_KingdomRaiderObjective>();
				if (!ReferenceEquals(item, excluded) && part != null
					&& part.OperationId == operationId && GameObject.Validate(item)
					&& item.IsAlive) count++;
			}
			return count;
		}

		private static bool RestoreDebitOrQuarantine(KingdomSystem system,
			KingdomLifecycleOperation op, KingdomWaterDebit debit, string fault)
		{
			if (debit == null || debit.Rollback() || debit.RestorationExact) return true;
			KingdomLifecycleBook book = system?.LifecycleBook;
			if (op != null && ReferenceEquals(book?.Raid, op))
				KingdomLifecycleRules.Quarantine(op, fault);
			if (book != null)
			{
				book.Quarantined = true;
				book.Fault = fault;
			}
			return false;
		}

		private static string DisplayFaction(string faction)
		{
			try { return Faction.GetFormattedName(faction); }
			catch { return faction ?? "unknown raiders"; }
		}

		private static long SafeAdd(long value, long delta)
		{
			if (value < 0L || delta < 0L || value > long.MaxValue - delta) return long.MaxValue;
			return value + delta;
		}
	}
}

namespace XRL.World.Parts
{
	[Serializable]
	public sealed class r_KingdomRaidDemand : IPart
	{
		public string IncidentId;
		public string ChannelId;
		public int Revision;
		public bool Inert;

		public override bool CanGenerateStacked() { return false; }
		public override bool SameAs(IPart Part) { return ReferenceEquals(this, Part); }

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			IncidentId = null; ChannelId = null; Revision = 0; Inert = true;
			ParentObject?.RemoveProperty(ThousandAndFirst.KingdomRaids.ProjectionMarkerProperty);
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID || ID == CanBeReplicatedEvent.ID;
		}

		public override bool HandleEvent(CanBeReplicatedEvent E) { return false; }

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			bool acknowledge;
			if (ThousandAndFirst.KingdomRaids.IsDemandActionable(ParentObject, E.Actor,
				out acknowledge))
				E.AddAction(acknowledge ? "Read and acknowledge demand" : "Answer demand",
					acknowledge ? "read and acknowledge demand" : "answer demand",
					"r_KingdomRaidDemand", null, 'a', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_KingdomRaidDemand" && E.Actor != null && E.Actor.IsPlayer())
			{
				ThousandAndFirst.KingdomRaids.UseDemand(E.Actor, ParentObject);
				E.RequestInterfaceExit();
			}
			return base.HandleEvent(E);
		}
	}

	[Serializable]
	public sealed class r_KingdomRaiderObjective : IPart
	{
		public string OperationId;
		public string IncidentId;
		public string TargetObjectId;
		public int TargetX;
		public int TargetY;

		public r_KingdomRaiderObjective() { }

		public r_KingdomRaiderObjective(string operationId, string incidentId,
			string targetObjectId, int targetX, int targetY)
		{
			OperationId = operationId; IncidentId = incidentId;
			TargetObjectId = targetObjectId; TargetX = targetX; TargetY = targetY;
		}

		public override bool WantTurnTick() { return true; }

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == BeforeDeathRemovalEvent.ID;
		}

		public override bool HandleEvent(BeforeDeathRemovalEvent E)
		{
			ThousandAndFirst.KingdomRaids.RaiderDying(ParentObject, this);
			return base.HandleEvent(E);
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			if (!ThousandAndFirst.KingdomMaster.AutomaticWorkAllowed(
				The.Game?.GetSystem<ThousandAndFirst.KingdomSystem>())) return;
			ThousandAndFirst.KingdomRaids.StepRaider(ParentObject, this, TimeTick);
		}
	}
}
