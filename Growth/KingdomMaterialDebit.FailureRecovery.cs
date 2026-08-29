using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomMaterialDebit
	{
		private KingdomMaterialDebitResult FinishFailure(KingdomMaterialDebitFault Fault,
			string Failure)
		{
			CaptureRemoved();
			Result = Classify(Fault, Failure);
			// Physical stack-count compensation is safe only while every exact identity and
			// expected post-count still proves itself. Try it immediately; never improvise replacement.
			if (Result.Outcome == KingdomMaterialDebitOutcome.RecoverablePartial)
			{
				KingdomMaterialDebitResult compensated = CompensateDuringCommit();
				if (compensated != null)
				{
					return compensated;
				}
			}
			AdjustStockFor(Result.Lost);
			return Result;
		}

		private KingdomMaterialDebitResult CompensateDuringCommit()
		{
			if (TopologyUncertain || !AllObservationsExact()) return null;
			List<int> current;
			List<bool> same;
			ReadCurrent(out current, out same);
			if (!KingdomMaterialDebitRules.CanCompensate(Plan, Removed, current, same))
			{
				return null;
			}
			try
			{
				for (int i = 0; i < Plan.Steps.Count; i++)
				{
					if (Removed[i] > 0)
					{
						if (!TryRestoreCountAndFlush(i)) return null;
					}
				}
				if (!AllStillReserved())
				{
					return null;
				}
				Result = new KingdomMaterialDebitResult(
					KingdomMaterialDebitOutcome.CleanRefusal, Result.Fault,
					Plan.Requested, new KingdomMaterialDebitCost(), Plan.Requested,
					new KingdomMaterialDebitCost(), 0,
					Result.Failure + " Every measured stack count was restored exactly.");
				return Result;
			}
			catch
			{
				CaptureRemoved();
				Result = Classify(KingdomMaterialDebitFault.CompensationFailed,
					"Automatic exact-count compensation failed.");
				return null;
			}
		}

		private KingdomMaterialDebitResult Classify(KingdomMaterialDebitFault Fault,
			string Failure)
		{
			List<bool> same = new List<bool>();
			List<bool> exact = new List<bool>();
			for (int i = 0; i < Plan.Steps.Count; i++)
			{
				same.Add(StillSame(EntryFor(Plan.Steps[i])));
				exact.Add(!TopologyUncertain && ExactObservations[i]);
			}
			return KingdomMaterialDebitRules.Classify(Plan, Removed, same, exact, Fault, Failure);
		}

		/// <summary>
		/// Count restoration is the only safe inverse for a surviving stack. Qud's forward
		/// Stacker.Destroy path flushes the owning inventory's cached weight; the inverse must do the
		/// same or compensation can leave a physically restored stockpile carrying less cached mass.
		/// </summary>
		private bool TryRestoreCountAndFlush(int Index)
		{
			if (Index < 0 || Index >= Plan.Steps.Count || !ObservedStateMatches()) return false;
			KingdomMaterialDebitStep step = Plan.Steps[Index];
			Entry entry = EntryFor(step);
			int removed = Removed[Index];
			if (removed <= 0) return true;
			if (removed >= step.Original || !StillSame(entry) ||
				entry.Item.Count != step.Original - removed)
			{
				return false;
			}
			string leaseFailure;
			if (!KingdomConstructionInputLeaseAuthority.TryObjectAvailableForLocalDebit(
				entry.Item, out leaseFailure)) return false;
			try
			{
				entry.Item.Count = step.Original;
				entry.Witness.Inventory.FlushWeightCache();
				entry.Item.FlushContextWeightCaches();
			}
			catch
			{
				// Post-state proof below remains authoritative even when notification threw.
			}
			if (!StillSame(entry) || entry.Item.Count != step.Original)
			{
				ExactObservations[Index] = false;
				TopologyUncertain = true;
				return false;
			}
			Removed[Index] = 0;
			if (!TopologyMatchesObserved())
			{
				ExactObservations[Index] = false;
				TopologyUncertain = true;
				return false;
			}
			return true;
		}
	}
}
