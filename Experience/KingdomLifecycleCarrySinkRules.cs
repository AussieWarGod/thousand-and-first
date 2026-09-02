using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		/// <summary>Publishes one carry outbox intent before the engine sink callback. Carry has
		/// its own realm authority and cannot borrow a settlement lifecycle operation merely to
		/// deliver a chronicle or ledger line.</summary>
		public static bool BeginCarrySink(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomLifecycleSinkMask Sink)
		{
			KingdomLifecycleSinkState state;
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.Sinks
				|| !SingleCarrySink(Sink) || !TryCarrySink(Operation.Outbox, Sink, out state)
				|| state != KingdomLifecycleSinkState.Pending) return false;
			SetCarrySink(Operation.Outbox, Sink, KingdomLifecycleSinkState.Intent);
			return ExactCarryAuthority(Book, Operation);
		}

		/// <summary>Commits only after the named sink callback returned. An interrupted intent is
		/// first normalized by <see cref="RecoverCarryOutbox"/>, so non-idempotent sinks are never
		/// guessed delivered.</summary>
		public static bool CommitCarrySink(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomLifecycleSinkMask Sink)
		{
			KingdomLifecycleSinkState state;
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.Sinks
				|| !SingleCarrySink(Sink) || !TryCarrySink(Operation.Outbox, Sink, out state)
				|| state != KingdomLifecycleSinkState.Intent) return false;
			SetCarrySink(Operation.Outbox, Sink, KingdomLifecycleSinkState.Delivered);
			return ExactCarryAuthority(Book, Operation);
		}

		private static bool SingleCarrySink(KingdomLifecycleSinkMask Sink)
		{
			byte value = (byte)Sink;
			return value != 0 && (value & (value - 1)) == 0;
		}

		private static bool TryCarrySink(KingdomLifecycleOutbox Box,
			KingdomLifecycleSinkMask Sink, out KingdomLifecycleSinkState State)
		{
			State = KingdomLifecycleSinkState.None;
			if (Box == null) return false;
			switch (Sink)
			{
			case KingdomLifecycleSinkMask.Chronicle: State = Box.ChronicleState; return true;
			case KingdomLifecycleSinkMask.Ledger: State = Box.LedgerState; return true;
			case KingdomLifecycleSinkMask.Message: State = Box.MessageState; return true;
			case KingdomLifecycleSinkMask.Deed: State = Box.DeedState; return true;
			case KingdomLifecycleSinkMask.Guestbook: State = Box.GuestbookState; return true;
			default: return false;
			}
		}

		private static void SetCarrySink(KingdomLifecycleOutbox Box,
			KingdomLifecycleSinkMask Sink, KingdomLifecycleSinkState State)
		{
			switch (Sink)
			{
			case KingdomLifecycleSinkMask.Chronicle: Box.ChronicleState = State; break;
			case KingdomLifecycleSinkMask.Ledger: Box.LedgerState = State; break;
			case KingdomLifecycleSinkMask.Message: Box.MessageState = State; break;
			case KingdomLifecycleSinkMask.Deed: Box.DeedState = State; break;
			case KingdomLifecycleSinkMask.Guestbook: Box.GuestbookState = State; break;
			}
		}
	}
}
