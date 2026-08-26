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

	}
}
