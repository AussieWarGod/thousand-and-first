using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomPetitionLifecycle
	{
		private static bool PublishOffer(KingdomSystem system, Zone zone, KingdomSurvey survey,
			KingdomLifecycleBook book, KingdomRules.PetitionKind kind, string faction,
			string eventId, long now, bool legacy)
		{
			KingdomLifecycleOperation prior = book.Petition;
			if (prior != null && (!KingdomPetitionRules.FrozenSnapshotValid(prior)
				|| !KingdomPetitionRules.CanFollow(prior.Action,
					KingdomLifecycleAction.PetitionOffer))) return false;
			if (!TryRequester(system, survey, null, out GameObject body, out string name))
				return false;
			int target = KingdomPetitionRules.SnapshotTarget(kind, system.Population);
			if (!KingdomPetitionRules.TargetValid(kind, target)
				|| (eventId != null && !KingdomPetitionRules.EventIdValid(eventId))
				|| !KingdomPetitionRules.SnapshotTextValid(faction,
					KingdomLifecycleRules.MaxNameChars, true)) return false;
			if (!KingdomPetitionRules.TryDeadline(now, KingdomRules.PetitionLifetimeTicks,
				out long deadline)) return false;
			if (prior != null && !KingdomLifecycleRules.Retire(book, prior, now)) return false;
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.Petition, KingdomLifecycleAction.PetitionOffer, now);
			if (op == null) return QuarantineAfterRetirement(book, prior,
				"petition offer could not reserve its lane");
			FreezeOffer(op, body, name, book.SettlementId, zone.ZoneID, kind, faction,
				target, eventId, now, deadline);
			op.Outbox = Outbox(system, op, legacy ? "adopted" : "offered");
			return PublishAndDrive(system, book, op, now);
		}

		private static bool PublishTransition(KingdomSystem system, KingdomLifecycleBook book,
			KingdomLifecycleOperation source, KingdomLifecycleAction action, string clock,
			long deadline, long now, string reason)
		{
			if (!KingdomPetitionRules.FrozenSnapshotValid(source)
				|| source.Phase != KingdomLifecyclePhase.Terminal
				|| !KingdomPetitionRules.CanFollow(source.Action, action)
				|| deadline <= 0L || now < source.UpdatedTick) return false;
			if (!KingdomLifecycleRules.Retire(book, source, now)) return false;
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.Petition, action, now);
			if (op == null)
				return QuarantineAfterRetirement(book, source,
					"petition transition could not reserve its lane");
			CopySnapshot(source, op);
			op.Creed = clock;
			op.DepartTick = deadline;
			if (action == KingdomLifecycleAction.PetitionResolve) op.Count = system.PetitionsMet;
			op.Outbox = Outbox(system, op, reason);
			if (!KingdomPetitionRules.SameFrozenSnapshot(source, op))
				return QuarantineAfterRetirement(book, source,
					"petition transition changed frozen offer semantics");
			return PublishAndDrive(system, book, op, now);
		}

		private static bool PublishAndDrive(KingdomSystem system, KingdomLifecycleBook book,
			KingdomLifecycleOperation op, long now)
		{
			if (op.Outbox == null || !KingdomLifecycleRules.PetitionRuntimeAdapter.PrepareLeases(
				book, op) || !KingdomLifecycleRules.TryPublish(book, op))
			{
				book.Quarantined = true;
				book.Fault = "petition plan publication failed without clearing its legacy projection";
				return false;
			}
			Project(system, op);
			if (KingdomLog.Enabled)
				KingdomLog.Log("petition action: " + op.Action + " id=" + op.ObjectMarker
					+ " operation=" + op.Id);
			return Drive(system, book, now);
		}

		private static bool Drive(KingdomSystem system, KingdomLifecycleBook book, long now)
		{
			KingdomLifecycleOperation op = book?.Petition;
			if (op == null) return true;
			if (!KingdomPetitionRules.FrozenSnapshotValid(op)) return false;
			for (int guard = 0; guard < 12; guard++)
			{
				long tick = Math.Max(now, op.UpdatedTick);
				switch (op.Phase)
				{
				case KingdomLifecyclePhase.Prepared:
					if (!KingdomLifecycleRules.AdvancePhase(book, op,
						KingdomLifecyclePhase.DomainIntent, tick)) return false;
					break;
				case KingdomLifecyclePhase.DomainIntent:
					if (!SettleDomain(system, book, op)
						|| !KingdomLifecycleRules.AdvancePhase(book, op,
							KingdomLifecyclePhase.DomainSettled, tick)) return false;
					break;
				case KingdomLifecyclePhase.DomainSettled:
					if (!KingdomLifecycleRules.AdvancePhase(book, op,
						KingdomLifecyclePhase.Sinks, tick)) return false;
					break;
				case KingdomLifecyclePhase.Sinks:
					if (!DispatchOutbox(system, book, op)
						|| !KingdomLifecycleRules.AdvancePhase(book, op,
							KingdomLifecyclePhase.ScheduleIntent, tick)) return false;
					break;
				case KingdomLifecyclePhase.ScheduleIntent:
					if (!KingdomLifecycleRules.PetitionRuntimeAdapter.ProveSchedule(book, op)
						|| !KingdomLifecycleRules.AdvancePhase(book, op,
							KingdomLifecyclePhase.Terminal, tick)) return false;
					break;
				case KingdomLifecyclePhase.Terminal:
					Project(system, op);
					return true;
				case KingdomLifecyclePhase.Quarantined:
					return false;
				default:
					KingdomLifecycleRules.Quarantine(op,
						"petition entered a phase outside its bounded action graph");
					return false;
				}
			}
			KingdomLifecycleRules.Quarantine(op, "petition exceeded its bounded phase budget");
			return false;
		}

		private static bool SettleDomain(KingdomSystem system, KingdomLifecycleBook book,
			KingdomLifecycleOperation op)
		{
			if (op.Action != KingdomLifecycleAction.PetitionResolve)
				return KingdomLifecycleRules.PetitionRuntimeAdapter.ProveDomain(book, op);
			KingdomLifecycleLeaseState state =
				KingdomLifecycleRules.PetitionRuntimeAdapter.DomainState(book, op);
			if (state == KingdomLifecycleLeaseState.Proved)
				return system.PetitionsMet == op.Count + 1;
			if (op.Count < 0 || op.Count == int.MaxValue
				|| (system.PetitionsMet != op.Count && system.PetitionsMet != op.Count + 1))
			{
				KingdomLifecycleRules.Quarantine(op,
					"petition completion count disagreed with its exact intent");
				return false;
			}
			if (state == KingdomLifecycleLeaseState.Prepared
				&& !KingdomLifecycleRules.PetitionRuntimeAdapter.BeginDomain(book, op)) return false;
			if (system.PetitionsMet == op.Count) system.PetitionsMet++;
			return KingdomLifecycleRules.PetitionRuntimeAdapter.CommitDomain(book, op);
		}

		private static bool DispatchOutbox(KingdomSystem system, KingdomLifecycleBook book,
			KingdomLifecycleOperation op)
		{
			if (!KingdomLifecycleRules.RecoverOutbox(book, op)) return false;
			if (!Deliver(book, op, KingdomLifecycleSinkMask.Chronicle, delegate
			{
				return KingdomChronicle.RecordOnce(system, op.Outbox.ChronicleReceiptId,
					op.Outbox.Chronicle, op.Outbox.ChronicleAccomplishment);
			})) return false;
			if (!Deliver(book, op, KingdomLifecycleSinkMask.Ledger, delegate
			{
				system.Ledger.Note(op.Outbox.Ledger); return true;
			})) return false;
			if (!Deliver(book, op, KingdomLifecycleSinkMask.Message, delegate
			{
				MessageQueue.AddPlayerMessage(op.Outbox.Message); return true;
			})) return false;
			if (!Deliver(book, op, KingdomLifecycleSinkMask.Deed, delegate
			{
				system.RecordDeed(op.Outbox.Deed); return true;
			})) return false;
			return Settled(op.Outbox);
		}

		private static bool Deliver(KingdomLifecycleBook book, KingdomLifecycleOperation op,
			KingdomLifecycleSinkMask sink, Func<bool> callback)
		{
			KingdomLifecycleSinkState state = SinkState(op.Outbox, sink);
			if (KingdomLifecycleRules.SinkSettled(state)) return true;
			if (!KingdomLifecycleRules.PetitionRuntimeAdapter.BeginSink(book, op, sink)) return false;
			bool delivered = false;
			try { delivered = callback(); }
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst petition outbox", error);
			}
			return delivered && KingdomLifecycleRules.PetitionRuntimeAdapter.CommitSink(book, op, sink);
		}

	}
}
