using System;

namespace ThousandAndFirst
{
	internal static class KingdomRaidOutboxRules
	{
		internal static bool Deliver(KingdomLifecycleBook Book, KingdomLifecycleOperation Operation,
			KingdomLifecycleSinkMask Sink, Func<bool> Callback, Action<string> Diagnostics)
		{
			KingdomLifecycleSinkState state;
			if (Callback == null || !KingdomLifecycleRules.RaidRuntimeAdapter.ReadSink(
				Book, Operation, Sink, out state)) return false;
			if (KingdomLifecycleRules.SinkSettled(state)) return true;
			if (!KingdomLifecycleRules.RaidRuntimeAdapter.BeginSink(Book, Operation, Sink)) return false;
			bool delivered;
			try { delivered = Callback(); }
			catch (Exception error)
			{
				// Even Exception.Message may execute foreign code. Persist authority first.
				string fault = "raid outbox " + Sink + " callback threw";
				KingdomLifecycleRules.Quarantine(Operation, fault);
				try { Diagnostics?.Invoke(fault + " (" + error.Message + ")"); }
				catch { /* Diagnostic failure cannot restore a quarantined sink's authority. */ }
				return false;
			}
			return delivered && KingdomLifecycleRules.RaidRuntimeAdapter.CommitSink(Book, Operation, Sink);
		}
	}
}
