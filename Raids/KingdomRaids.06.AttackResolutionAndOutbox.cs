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

	}
}
