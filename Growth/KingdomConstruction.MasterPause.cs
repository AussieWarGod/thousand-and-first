using System.Collections.Generic;

using XRL;

namespace ThousandAndFirst
{
	internal sealed class KingdomConstructionMasterPauseTarget
	{
		internal readonly string OperationId;
		internal readonly int TripId;
		internal readonly long DesiredArrivalTick;

		internal KingdomConstructionMasterPauseTarget(string operationId, int tripId,
			long desiredArrivalTick)
		{
			OperationId = operationId;
			TripId = tripId;
			DesiredArrivalTick = desiredArrivalTick;
		}
	}

	internal sealed class KingdomConstructionMasterPausePlan
	{
		private readonly string Expected;
		private readonly string Updated;
		private readonly KingdomConstructionMasterPauseTarget[] _targets;

		internal KingdomConstructionMasterPausePlan(string expected, string updated,
			KingdomConstructionMasterPauseTarget[] targets)
		{
			Expected = expected;
			Updated = updated;
			_targets = targets == null ? new KingdomConstructionMasterPauseTarget[0]
				: (KingdomConstructionMasterPauseTarget[])targets.Clone();
		}

		internal KingdomConstructionMasterPauseTarget[] CopyTargets()
		{ return (KingdomConstructionMasterPauseTarget[])_targets.Clone(); }

		internal bool Publish(out string failure)
		{
			if (!CanPublish(out failure)) return false;
			PublishPrevalidated(); return true;
		}

		internal bool CanPublish(out string failure)
		{
			failure = null;
			if (The.Game == null)
			{ failure = "The game has no construction registry."; return false; }
			if (The.Game.GetStringGameState(KingdomConstruction.RegistryStateKey, null)
				== Expected) return true;
			failure = "The construction registry changed during master-resume staging.";
			return false;
		}

		internal void PublishPrevalidated()
		{
			if (Updated != Expected)
				The.Game.SetStringGameState(KingdomConstruction.RegistryStateKey, Updated);
		}
	}

	public static partial class KingdomConstruction
	{
		internal static bool TryPrepareMasterResume(long disabledAt, long now,
			out KingdomConstructionMasterPausePlan plan, out string failure)
		{
			plan = null;
			failure = null;
			if (The.Game == null || disabledAt < 0L || now < disabledAt)
			{ failure = "The master-resume construction clock is invalid."; return false; }
			string expected = The.Game.GetStringGameState(RegistryStateKey, null);
			if (!TryRead(out List<KingdomConstructionJob> jobs, out failure)) return false;
			bool changed = false;
			List<KingdomConstructionMasterPauseTarget> targets =
				new List<KingdomConstructionMasterPauseTarget>();
			for (int i = 0; i < jobs.Count && now > disabledAt; i++)
			{
				KingdomConstructionJob current = jobs[i];
				if (string.IsNullOrEmpty(current.InputReceipt)
					|| !KingdomConstructionRules.TryGetInputReceipt(current,
						out KingdomConstructionInputReceipt receipt)
					|| KingdomConstructionInputRules.IsTerminal(receipt)) continue;
				if (receipt.Paused)
				{
					failure = "A routed construction receipt was already paused by another owner.";
					return false;
				}
				if (!KingdomConstructionInputRules.TryRebaseMasterPause(receipt,
					receipt.Revision, disabledAt, now, out KingdomConstructionInputReceipt rebased,
					out KingdomConstructionInputFault fault))
				{
					failure = "A routed construction clock could not rebase (" + fault + ").";
					return false;
				}
				for (int childIndex = 0; childIndex < rebased.ChildCount; childIndex++)
				{
					KingdomConstructionInputChild child = rebased.ChildAt(childIndex);
					if (child.CentralPhase
						== (int)Simulation.City.KingdomDeliveryPhase.LandedAwaitingOwner)
						continue;
					long desired;
					if (!KingdomConstructionInputRules.TryEffectiveArrivalTick(
						child.ArrivalTick, rebased.PausedTicks, out desired))
					{
						failure = "A routed construction pause target exceeds the exact clock range.";
						return false;
					}
					targets.Add(new KingdomConstructionMasterPauseTarget(
						rebased.ConstructionJobId, child.TripId, desired));
				}
				KingdomConstructionJob next = KingdomConstructionRules.Transition(current,
					current.Phase, now, current.Failure);
				if (!KingdomConstructionRules.UpdateInputReceipt(ref next, rebased)
					|| !KingdomConstructionRules.ValidRegistryUpdate(current, next))
				{
					failure = "A routed construction pause update failed registry validation.";
					return false;
				}
				jobs[i] = next;
				changed = true;
			}
			string updated = expected;
			if (changed && !KingdomConstructionRules.TryEncode(jobs, out updated))
			{ failure = "The rebased construction registry exceeds its durable bounds."; return false; }
			plan = new KingdomConstructionMasterPausePlan(expected, updated,
				targets.ToArray());
			return true;
		}
	}
}
