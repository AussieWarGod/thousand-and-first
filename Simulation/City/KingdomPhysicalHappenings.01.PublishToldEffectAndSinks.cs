using System;
using System.Collections.Generic;
using System.Globalization;

using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.AI;
using XRL.World.AI.GoalHandlers;
using XRL.World.AI.Pathfinding;
using XRL.World.Effects;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomPhysicalHappenings
	{
		private static int PublishGeneric(KingdomSystem system, KingdomCityBook book,
			KingdomHappeningOperation operation, string label, int pushBudget, long nowTick)
		{
			int pushed = 0;
			if (operation.ChronicleState == KingdomHappeningSinkState.Pending)
			{
				string line = operation.Attended ? operation.ChronicleAttended
					: operation.ChronicleUnattended;
				bool delivered = false;
				try { delivered = KingdomChronicle.RecordOnce(system,
					operation.EventId + ":chronicle", line); }
				catch { }
					SetSink(book, operation.EventId, SinkLane.Chronicle,
						delivered ? KingdomHappeningSinkState.Delivered
							: KingdomHappeningSinkState.Pending, nowTick);
					if (!delivered) KingdomLog.Log("happening physical: chronicle deferred for "
						+ operation.EventId);
			}
			if (!TryRead(book, nowTick, out KingdomHappeningLifecycleBook lifecycle)
				|| lifecycle.Active == null) return pushed;
			operation = lifecycle.Active;
			if (operation.ToldState == KingdomHappeningSinkState.Pending)
			{
				bool delivered = PublishTold(book, operation);
					SetSink(book, operation.EventId, SinkLane.Told,
						delivered ? KingdomHappeningSinkState.Delivered
							: KingdomHappeningSinkState.Pending, nowTick);
					if (!delivered) KingdomLog.Log("happening physical: told receipt deferred for "
					+ operation.EventId);
			}
			if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null) return pushed;
			operation = lifecycle.Active;
			if (operation.EffectState == KingdomHappeningSinkState.Attempting
				|| operation.LedgerState == KingdomHappeningSinkState.Attempting
				|| operation.MessageState == KingdomHappeningSinkState.Attempting)
			{
				KingdomHappeningLifecycleBook recovered =
					KingdomHappeningLifecycleRules.RecoverInterruptedSinks(lifecycle, nowTick);
				if (!ReferenceEquals(recovered, lifecycle)) Write(book, recovered);
				lifecycle = recovered;
				operation = recovered.Active;
			}
			if (operation.EffectState == KingdomHappeningSinkState.Pending)
			{
				if (BeginUninspectable(book, operation.EventId, SinkLane.Effect, nowTick))
				{
					bool delivered = ApplyEffect(book, operation);
					SetSink(book, operation.EventId, SinkLane.Effect,
						delivered ? KingdomHappeningSinkState.Delivered
							: KingdomHappeningSinkState.Lost, nowTick);
				}
			}
			if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null) return pushed;
			operation = lifecycle.Active;
			string ledger = operation.Attended ? operation.LedgerAttended
				: operation.LedgerUnattended;
			string message = operation.Attended ? operation.MessageAttended
				: operation.MessageUnattended;
			if (string.IsNullOrWhiteSpace(ledger)
				&& operation.LedgerState == KingdomHappeningSinkState.Pending)
			{
				SetSink(book, operation.EventId, SinkLane.Ledger,
					KingdomHappeningSinkState.Skipped, nowTick);
			}
			if (TryRead(book, nowTick, out lifecycle) && lifecycle.Active != null)
				operation = lifecycle.Active;
			if (string.IsNullOrWhiteSpace(message)
				&& operation.MessageState == KingdomHappeningSinkState.Pending)
			{
				SetSink(book, operation.EventId, SinkLane.Message,
					KingdomHappeningSinkState.Skipped, nowTick);
			}
			if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null) return pushed;
			operation = lifecycle.Active;
			if (pushBudget <= 0)
			{
				if (operation.LedgerState == KingdomHappeningSinkState.Pending)
					SetSink(book, operation.EventId, SinkLane.Ledger,
						KingdomHappeningSinkState.Skipped, nowTick);
				if (TryRead(book, nowTick, out lifecycle) && lifecycle.Active != null
					&& lifecycle.Active.MessageState == KingdomHappeningSinkState.Pending)
					SetSink(book, operation.EventId, SinkLane.Message,
						KingdomHappeningSinkState.Skipped, nowTick);
				return pushed;
			}
			if (operation.LedgerState == KingdomHappeningSinkState.Pending)
			{
				bool delivered = BeginUninspectable(book, operation.EventId, SinkLane.Ledger,
					nowTick);
				if (delivered)
				{
					try { system.Ledger.Note("{{K|" + ledger + "}}"); }
					catch { delivered = false; }
					SetSink(book, operation.EventId, SinkLane.Ledger,
						delivered ? KingdomHappeningSinkState.Delivered
							: KingdomHappeningSinkState.Lost, nowTick);
				}
			}
			if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null) return pushed;
			operation = lifecycle.Active;
			if (operation.MessageState == KingdomHappeningSinkState.Pending)
			{
				bool delivered = BeginUninspectable(book, operation.EventId, SinkLane.Message,
					nowTick);
				if (delivered)
				{
					try
					{
						string said = operation.Attended ? message
							: KingdomBrinkRules.WordFrom(KingdomPresentation.Rich(
								KingdomWord.CityName(system, label)), message);
						MessageQueue.AddPlayerMessage(said);
						pushed = 1;
					}
					catch { delivered = false; }
					SetSink(book, operation.EventId, SinkLane.Message,
						delivered ? KingdomHappeningSinkState.Delivered
							: KingdomHappeningSinkState.Lost, nowTick);
				}
			}
			return pushed;
		}

		private static bool PublishTold(KingdomCityBook book,
			KingdomHappeningOperation operation)
		{
			if (!book.TryRead(out KingdomCityState state, out KingdomCityFault fault)) return false;
			if (ExactTold(state, operation.Kind, operation.EventTick, operation.SubjectA,
				operation.SubjectB, operation.ZoneId, operation.Outcome)) return true;
			KingdomToldKind kind = ToldKind(operation.Kind);
			if (kind == KingdomToldKind.None) return false;
			if (!state.TryTell(new KingdomToldRow(kind, operation.EventTick,
				operation.SubjectA, operation.SubjectB, operation.ZoneId, operation.Outcome),
				out KingdomCityState next, out fault)) return false;
			return book.TryPublish(next, out fault);
		}

		private static KingdomToldKind ToldKind(KingdomPhysicalHappeningKind kind)
		{
			switch (kind)
			{
			case KingdomPhysicalHappeningKind.Wedding: return KingdomToldKind.Wedding;
			case KingdomPhysicalHappeningKind.Funeral: return KingdomToldKind.Funeral;
			case KingdomPhysicalHappeningKind.Feast: return KingdomToldKind.Festival;
			case KingdomPhysicalHappeningKind.Raising: return KingdomToldKind.Raising;
			default: return KingdomToldKind.None;
			}
		}

		private static bool ApplyEffect(KingdomCityBook book,
			KingdomHappeningOperation operation)
		{
			if (string.IsNullOrEmpty(operation.Effect)) return true;
			if (operation.Kind != KingdomPhysicalHappeningKind.Feast) return false;
			int split = operation.Effect.IndexOf('\n');
			if (split <= 0 || split >= operation.Effect.Length - 1) return false;
				return KingdomHappenings.AccruePilgrim(book,
					operation.Effect.Substring(0, split), operation.Effect.Substring(split + 1),
					operation.EventTick);
		}

		private static bool BeginUninspectable(KingdomCityBook book, string eventId,
			SinkLane lane, long nowTick)
		{
			return SetSink(book, eventId, lane, KingdomHappeningSinkState.Attempting, nowTick);
		}

		private static bool SetSink(KingdomCityBook book, string eventId, SinkLane lane,
			KingdomHappeningSinkState value, long nowTick)
		{
			// This read must be raw. Attempting is the durable before-callback receipt; recovering
			// it here would turn the live callback's own receipt into Lost before it can acknowledge
			// Delivered. Recovery belongs only at a later top-level resume boundary.
			if (!TryReadRaw(book, out KingdomHappeningLifecycleBook lifecycle)
				|| lifecycle.Active == null
				|| !string.Equals(lifecycle.Active.EventId, eventId, StringComparison.Ordinal))
				return false;
			KingdomHappeningOperation operation = lifecycle.Active;
			KingdomHappeningSinkState chronicle = operation.ChronicleState;
			KingdomHappeningSinkState told = operation.ToldState;
			KingdomHappeningSinkState effect = operation.EffectState;
			KingdomHappeningSinkState ledger = operation.LedgerState;
			KingdomHappeningSinkState message = operation.MessageState;
			switch (lane)
			{
			case SinkLane.Chronicle: chronicle = value; break;
			case SinkLane.Told: told = value; break;
			case SinkLane.Effect: effect = value; break;
			case SinkLane.Ledger: ledger = value; break;
			case SinkLane.Message: message = value; break;
			}
			return KingdomHappeningLifecycleRules.TrySetSinks(lifecycle, eventId, chronicle,
				told, effect, ledger, message, nowTick,
				out KingdomHappeningLifecycleBook changed,
				out KingdomHappeningLifecycleFault fault) && Write(book, changed);
		}
	}
}
