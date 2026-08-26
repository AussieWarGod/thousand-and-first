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

	}
}
