using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomGuestLifecycle
	{
		private static bool SettleSinks(KingdomSystem system, KingdomLifecycleBook book,
			KingdomLifecycleOperation op)
		{
			if (op.Creed == "causal-pilgrim"
				&& op.Action == KingdomLifecycleAction.Passages) ApplyCausalDomain(system, op);
			if (!KingdomLifecycleRules.RecoverOutbox(book, op)) return false;
			KingdomLifecycleSinkMask[] sinks =
			{
				KingdomLifecycleSinkMask.Chronicle, KingdomLifecycleSinkMask.Ledger,
				KingdomLifecycleSinkMask.Message, KingdomLifecycleSinkMask.Guestbook
			};
			for (int i = 0; i < sinks.Length; i++)
			{
				KingdomLifecycleSinkMask sink = sinks[i];
				if (SinkState(op.Outbox, sink) != KingdomLifecycleSinkState.Pending) continue;
				if (!KingdomLifecycleRules.RaidRuntimeAdapter.BeginSink(book, op, sink)) return false;
				bool delivered = DeliverSink(system, op, sink);
				if (!delivered) return false;
				if (!KingdomLifecycleRules.RaidRuntimeAdapter.CommitSink(book, op, sink)) return false;
			}
			return true;
		}

		private static bool DeliverSink(KingdomSystem system, KingdomLifecycleOperation op,
			KingdomLifecycleSinkMask sink)
		{
			switch (sink)
			{
			case KingdomLifecycleSinkMask.Chronicle:
				return KingdomChronicle.RecordOnce(system, op.Outbox.ChronicleReceiptId,
					op.Outbox.Chronicle, op.Outbox.ChronicleAccomplishment);
			case KingdomLifecycleSinkMask.Ledger:
				system.Ledger.Note(op.Outbox.Ledger); return true;
			case KingdomLifecycleSinkMask.Message:
				MessageQueue.AddPlayerMessage(op.Outbox.Message); return true;
			case KingdomLifecycleSinkMask.Guestbook:
				KingdomGuestbook.AppendLifecycleLine(system, op.Outbox.GuestbookLine); return true;
			default:
				return false;
			}
		}

		private static bool SettleSchedule(KingdomSystem system, Zone zone,
			KingdomLifecycleBook book, KingdomLifecycleOperation op)
		{
			long current = CurrentSchedule(system, op);
			KingdomLifecycleResourceLease lease = ScheduleLease(op);
			if (lease == null) return false;
			if (lease.State == KingdomLifecycleLeaseState.Intent)
				return current == lease.After
					&& KingdomLifecycleRules.GuestRuntimeAdapter.RecoverScheduleIntent(book, op,
						current);
			return KingdomLifecycleRules.TrustedAdapter.ProveLifecycleSchedule(book, op,
				new GuestWorld(system, zone, book, op));
		}

		private static long CurrentSchedule(KingdomSystem system, KingdomLifecycleOperation op)
		{
			if (op.Action == KingdomLifecycleAction.Passages)
				return op.Lane == KingdomLifecycleLane.PlainGuest
					? system.NextGuestTick : system.NextNotableGuestTick;
			long depart = op.Lane == KingdomLifecycleLane.PlainGuest
				? system.GuestDepartTick : system.NotableGuestDepartTick;
			if (op.Action == KingdomLifecycleAction.Spawn) return depart;
			KingdomLifecycleResourceLease schedule = ScheduleLease(op);
			if (schedule != null && schedule.State == KingdomLifecycleLeaseState.Prepared)
				return depart;
			if (depart > 0L) return depart;
			return op.Lane == KingdomLifecycleLane.PlainGuest
				? system.NextGuestTick : system.NextNotableGuestTick;
		}

		private static void SetSchedule(KingdomSystem system, KingdomLifecycleOperation op,
			long value)
		{
			if (op.Action == KingdomLifecycleAction.Passages)
			{
				if (op.Lane == KingdomLifecycleLane.PlainGuest) system.NextGuestTick = value;
				else system.NextNotableGuestTick = value;
			}
			else if (op.Action == KingdomLifecycleAction.Spawn)
			{
				if (op.Lane == KingdomLifecycleLane.PlainGuest) system.GuestDepartTick = value;
				else system.NotableGuestDepartTick = value;
			}
			else
			{
				if (op.Lane == KingdomLifecycleLane.PlainGuest)
				{
					system.GuestDepartTick = 0L; system.NextGuestTick = value;
				}
				else
				{
					system.NotableGuestDepartTick = 0L; system.NextNotableGuestTick = value;
				}
			}
		}

		private static bool PrepareWater(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, KingdomSurvey survey, int amount)
		{
			if (survey == null || amount <= 0) return false;
			int remaining = amount;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < survey.Stores.Count && remaining > 0; i++)
			{
				LiquidVolume liquid = survey.Stores[i];
				GameObject owner = liquid == null ? null : liquid.ParentObject;
				if (!GameObject.Validate(owner) || !ids.Add(owner.ID)
					|| owner.GetIntProperty("KingdomStores") != 1
					|| !KingdomLiquids.HasFreshWater(liquid) || liquid.MaxVolume < 0) continue;
				int take = Math.Min(remaining, liquid.Volume);
				if (KingdomLifecycleRules.GuestRuntimeAdapter.PrepareWater(book, op,
					op.WaterLegs.Count, owner.ID, owner.Blueprint, owner.CurrentZone.ZoneID,
					liquid.MaxVolume, liquid.Volume, take, "water") == null) return false;
				remaining -= take;
			}
			return remaining == 0 && op.WaterRequested == amount;
		}

		private static KingdomLifecycleBook Authority(KingdomSystem system)
		{
			if (system == null || system.LifecycleBook == null || system.City == null
				|| !string.Equals(system.LifecycleBook.SettlementId,
					system.City.SettlementId, StringComparison.Ordinal)) return null;
			KingdomLifecycleRules.Normalize(system.LifecycleBook);
			return KingdomLifecycleRules.CanOwnAuthority(system.LifecycleBook)
				? system.LifecycleBook : null;
		}

		private static KingdomLifecyclePhase Next(KingdomLifecycleOperation op)
		{
			foreach (KingdomLifecyclePhase phase in Enum.GetValues(typeof(KingdomLifecyclePhase)))
				if (phase != KingdomLifecyclePhase.Quarantined
					&& KingdomLifecycleRules.CanTransition(op.Action, op.Phase, phase)) return phase;
			return KingdomLifecyclePhase.Invalid;
		}

		private static KingdomLifecycleResourceLease FindLease(KingdomLifecycleOperation op,
			string key)
		{
			if (op == null || op.ResourceLeases == null) return null;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
				if (op.ResourceLeases[i] != null && op.ResourceLeases[i].Key == key)
					return op.ResourceLeases[i];
			return null;
		}

		private static KingdomLifecycleResourceLease DomainLease(KingdomLifecycleOperation op)
		{
			for (int i = 0; i < op.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceKind kind = op.ResourceLeases[i].Kind;
				if (kind != KingdomLifecycleResourceKind.Schedule
					&& kind != KingdomLifecycleResourceKind.WaterVessel
					&& kind != KingdomLifecycleResourceKind.Projection
					&& kind != KingdomLifecycleResourceKind.Object) return op.ResourceLeases[i];
			}
			return null;
		}

		private static KingdomLifecycleResourceLease ScheduleLease(KingdomLifecycleOperation op)
		{
			for (int i = 0; i < op.ResourceLeases.Count; i++)
				if (op.ResourceLeases[i].Kind == KingdomLifecycleResourceKind.Schedule)
					return op.ResourceLeases[i];
			return null;
		}

		private static KingdomLifecycleSinkState SinkState(KingdomLifecycleOutbox box,
			KingdomLifecycleSinkMask sink)
		{
			switch (sink)
			{
			case KingdomLifecycleSinkMask.Chronicle: return box.ChronicleState;
			case KingdomLifecycleSinkMask.Ledger: return box.LedgerState;
			case KingdomLifecycleSinkMask.Message: return box.MessageState;
			case KingdomLifecycleSinkMask.Guestbook: return box.GuestbookState;
			default: return KingdomLifecycleSinkState.Lost;
			}
		}

		private static GameObject FindExact(string id)
		{
			return string.IsNullOrEmpty(id) ? null : GameObject.FindByID(id);
		}

		private static bool ExactProjection(GameObject item, KingdomLifecycleProjection p)
		{
			return GameObject.Validate(item) && item.ID == p.ObjectId
				&& item.Blueprint == p.Blueprint && item.CurrentZone != null
				&& item.CurrentZone.ZoneID == p.ZoneId && item.CurrentCell != null
				&& item.CurrentCell.X == p.X && item.CurrentCell.Y == p.Y
				&& item.GetStringProperty(MarkerProperty) == p.Marker;
		}

	}
}
