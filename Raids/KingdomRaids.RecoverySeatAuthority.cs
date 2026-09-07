using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRaids
	{
		private static bool CurrentRaidOwner(XRLGame game, KingdomSystem system,
			KingdomLifecycleBook book)
		{
			return game != null && ReferenceEquals(The.Game, game) && system != null
				&& ReferenceEquals(game.GetSystem<KingdomSystem>(), system) && system.Founded
				&& !system.LoadFailed && !system.RealmRetirementBlocksWork
				&& ReferenceEquals(system.LifecycleBook, book)
				&& KingdomLifecycleRules.CanOwnAuthority(book);
		}

		/// <summary>One synchronous recovery decision. No saved state or cached absence authority.</summary>
		private sealed class RecoverySeatAuthority
		{
			private readonly XRLGame Game;
			private readonly KingdomSystem System;
			private readonly KingdomLifecycleBook Book;
			private readonly KingdomRaidLedger Ledger;
			private readonly KingdomRaidIncident Recovery;
			private readonly Zone Zone;
			private readonly GameObject Player;
			private readonly string GameId, SettlementId, ZoneId, IncidentId, AttackId, QuestId, StepId, Faction;
			private readonly long Tick, Revision, Sequence;
			private readonly KingdomRaidRecoveryState State;
			private readonly bool Automatic;

			private RecoverySeatAuthority(KingdomSystem system, Zone zone,
				KingdomRaidIncident recovery, bool automatic)
			{
				Game = The.Game; System = system; Book = system.LifecycleBook; Ledger = Book.RaidLedger;
				Recovery = recovery; Zone = zone; Player = The.Player; Automatic = automatic;
				GameId = Game.GameID; SettlementId = Book.SettlementId; ZoneId = recovery.TargetZoneId;
				IncidentId = recovery.Id; AttackId = recovery.AttackOperationId;
				QuestId = recovery.RecoveryQuestId; StepId = recovery.RecoveryStepId; Faction = recovery.AttackerFactionId;
				Tick = Game.TimeTicks; Revision = Ledger.StateRevision; Sequence = Book.RaidNextSequence;
				State = recovery.RecoveryState;
			}

			internal static bool TryCapture(KingdomSystem system, Zone zone,
				KingdomRaidIncident recovery, bool automatic, out RecoverySeatAuthority authority)
			{
				authority = null;
				if (KingdomSurvey.HasBoundPass || zone == null || recovery == null
					|| !CurrentRaidOwner(The.Game, system, system?.LifecycleBook)
					|| system.LifecycleBook.RaidLedger == null
					|| (automatic && (!Enabled || !KingdomMaster.AutomaticWorkAllowed(system)))) return false;
				var candidate = new RecoverySeatAuthority(system, zone, recovery, automatic);
				if (!candidate.Exact()) return false;
				authority = candidate; return true;
			}

			private bool SameRecovery(KingdomRaidIncident row)
			{
				return row != null && row.Id == IncidentId && row.SettlementId == SettlementId
					&& row.TargetZoneId == ZoneId && row.AttackOperationId == AttackId
					&& row.RecoveryQuestId == QuestId && row.RecoveryStepId == StepId
					&& row.AttackerFactionId == Faction && row.State == KingdomRaidIncidentState.Resolved
					&& row.Resolution == KingdomRaidResolution.StoresPlundered && row.PlunderProved > 0;
			}

			private bool Exact()
			{
				return !KingdomSurvey.HasBoundPass && CurrentRaidOwner(Game, System, Book)
					&& Game.GameID == GameId && Game.TimeTicks == Tick && ReferenceEquals(The.Player, Player)
					&& Player != null && ReferenceEquals(Player.Physics?._CurrentCell?.ParentZone, Zone)
					&& ReferenceEquals(The.ZoneManager?.ActiveZone, Zone) && Zone.ZoneID == ZoneId
					&& Book.SettlementId == SettlementId && Book.Raid == null && Book.RaidNextSequence == Sequence
					&& ReferenceEquals(Book.RaidLedger, Ledger) && Ledger.StateRevision == Revision
					&& ReferenceEquals(FindRecovery(Ledger, SettlementId), Recovery) && SameRecovery(Recovery)
					&& Recovery.RecoveryState == State
					&& (State == KingdomRaidRecoveryState.Active || State == KingdomRaidRecoveryState.Ready)
					&& !string.IsNullOrEmpty(AttackId) && System.ClaimedZones != null && System.ClaimedZones.Contains(ZoneId)
					&& (!Automatic || (Enabled && KingdomMaster.AutomaticWorkAllowed(System)));
			}

			internal bool DraftMatches(KingdomLifecycleOperation operation)
			{
				return Exact() && operation != null && operation.Lane == KingdomLifecycleLane.Raid
					&& operation.Action == (State == KingdomRaidRecoveryState.Active
						? KingdomLifecycleAction.RaidRecoveryReady : KingdomLifecycleAction.RaidRecoveryResolve)
					&& operation.SettlementId == SettlementId && operation.Sequence == Sequence && operation.CreatedTick == Tick
					&& operation.ZoneId == ZoneId && operation.ObjectId == IncidentId && operation.Faction == Faction
					&& operation.Detail == AttackId && operation.Outbox != null;
			}

			internal bool ProvesFreshAbsence()
			{
				if (!Exact() || !KingdomSurvey.TryTakeUnboundRecovery(Zone, out KingdomSurvey survey)
					|| !Exact()) return false;
				foreach (GameObject actor in survey.Objects)
				{
					if (!GameObject.Validate(actor) || !ReferenceEquals(actor.CurrentZone, Zone)) return false;
					var part = actor.GetPart<XRL.World.Parts.r_KingdomRaiderObjective>();
					if (part != null && part.OperationId == AttackId && actor.IsAlive) return false;
				}
				return Exact();
			}

			internal bool QuestStillExact(Quest expected)
			{
				return Exact() && ExactActiveRecoveryQuest(Recovery, out Quest actual)
					&& ReferenceEquals(actual, expected) && !Game.FinishedQuests.ContainsKey(QuestId);
			}

			internal bool TryPublishedRecovery(Quest expected, out KingdomRaidIncident resolved)
			{
				resolved = null;
				if (!CurrentRaidOwner(Game, System, Book) || Game.GameID != GameId) return false;
				resolved = KingdomRaidIncidentRules.Incident(Book.RaidLedger, IncidentId);
				return SameRecovery(resolved) && resolved.RecoveryState == KingdomRaidRecoveryState.Resolved
					&& ExactActiveRecoveryQuest(resolved, out Quest actual) && ReferenceEquals(actual, expected)
					&& !Game.FinishedQuests.ContainsKey(QuestId);
			}
		}
	}
}
