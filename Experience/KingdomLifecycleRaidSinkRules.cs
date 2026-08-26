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
		internal static partial class RaidRuntimeAdapter
		{
			internal static bool BeginSink(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, KingdomLifecycleSinkMask sink)
			{
				KingdomLifecycleSinkState state;
				if (!ExactOperationAuthority(book, operation)
					|| operation.Phase != KingdomLifecyclePhase.Sinks
					|| !SingleSink(sink) || !GetSink(operation.Outbox, sink, out state)
					|| state != KingdomLifecycleSinkState.Pending) return false;
				SetSink(operation.Outbox, sink, KingdomLifecycleSinkState.Intent);
				return ExactOperationAuthority(book, operation);
			}

			internal static bool CommitSink(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, KingdomLifecycleSinkMask sink)
			{
				KingdomLifecycleSinkState state;
				if (!ExactOperationAuthority(book, operation)
					|| operation.Phase != KingdomLifecyclePhase.Sinks
					|| !SingleSink(sink) || !GetSink(operation.Outbox, sink, out state)
					|| state != KingdomLifecycleSinkState.Intent) return false;
				SetSink(operation.Outbox, sink, KingdomLifecycleSinkState.Delivered);
				return ExactOperationAuthority(book, operation);
			}

			private static bool SingleSink(KingdomLifecycleSinkMask sink)
			{
				byte value = (byte)sink;
				return value != 0 && (value & (value - 1)) == 0;
			}

			private static bool GetSink(KingdomLifecycleOutbox box,
				KingdomLifecycleSinkMask sink, out KingdomLifecycleSinkState state)
			{
				state = KingdomLifecycleSinkState.None;
				if (box == null) return false;
				switch (sink)
				{
				case KingdomLifecycleSinkMask.Chronicle: state = box.ChronicleState; return true;
				case KingdomLifecycleSinkMask.Ledger: state = box.LedgerState; return true;
				case KingdomLifecycleSinkMask.Message: state = box.MessageState; return true;
				case KingdomLifecycleSinkMask.Deed: state = box.DeedState; return true;
				case KingdomLifecycleSinkMask.Guestbook: state = box.GuestbookState; return true;
				default: return false;
				}
			}

			private static void SetSink(KingdomLifecycleOutbox box,
				KingdomLifecycleSinkMask sink, KingdomLifecycleSinkState state)
			{
				switch (sink)
				{
				case KingdomLifecycleSinkMask.Chronicle: box.ChronicleState = state; break;
				case KingdomLifecycleSinkMask.Ledger: box.LedgerState = state; break;
				case KingdomLifecycleSinkMask.Message: box.MessageState = state; break;
				case KingdomLifecycleSinkMask.Deed: box.DeedState = state; break;
				case KingdomLifecycleSinkMask.Guestbook: box.GuestbookState = state; break;
				}
			}
		}
	}
}
