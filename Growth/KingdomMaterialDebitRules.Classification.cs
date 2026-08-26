using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterialDebitRules
	{
		/// <summary>
		/// Computes exact requested credit and full physical loss from measured removals. The arrays
		/// are observations, not commands; malformed rows fail closed into an irreversible result.
		/// </summary>
		public static KingdomMaterialDebitResult Classify(
			KingdomMaterialDebitPlan Plan,
			IList<int> Removed,
			IList<bool> SameSurvivingSource,
			KingdomMaterialDebitFault Fault,
			string Failure)
		{
			List<bool> exact = new List<bool>();
			if (Plan != null && Removed != null && SameSurvivingSource != null &&
				Removed.Count == Plan.Steps.Count && SameSurvivingSource.Count == Plan.Steps.Count)
			{
				for (int i = 0; i < Plan.Steps.Count; i++)
				{
					exact.Add(SameSurvivingSource[i] || Removed[i] == Plan.Steps[i].Original);
				}
			}
			return Classify(Plan, Removed, SameSurvivingSource, exact, Fault, Failure);
		}

		/// <summary>Classifies persisted exact removals separately from any callback-damaged row.</summary>
		public static KingdomMaterialDebitResult Classify(
			KingdomMaterialDebitPlan Plan,
			IList<int> Removed,
			IList<bool> SameSurvivingSource,
			IList<bool> ExactObservation,
			KingdomMaterialDebitFault Fault,
			string Failure)
		{
			if (Plan == null || Removed == null || SameSurvivingSource == null
				|| ExactObservation == null || Removed.Count != Plan.Steps.Count
				|| SameSurvivingSource.Count != Plan.Steps.Count
				|| ExactObservation.Count != Plan.Steps.Count)
			{
				KingdomMaterialDebitCost empty = new KingdomMaterialDebitCost();
				return new KingdomMaterialDebitResult(KingdomMaterialDebitOutcome.InvalidReservation,
					KingdomMaterialDebitFault.InvalidSources, Plan?.Requested, empty,
					Plan?.Requested, empty, 0, Failure, false);
			}

			KingdomMaterialTally lostMaterials = new KingdomMaterialTally();
			KingdomBitTally lostBits = new KingdomBitTally();
			KingdomExoticTally lostExotics = new KingdomExoticTally();
			bool exact = true;
			bool measurementExact = true;
			bool any = false;
			bool recoverable = true;
			int finalized = 0;
			for (int i = 0; i < Plan.Steps.Count; i++)
			{
				KingdomMaterialDebitStep step = Plan.Steps[i];
				int removed = Removed[i];
				if (!ExactObservation[i])
				{
					exact = false;
					measurementExact = false;
					recoverable = false;
				}
				if (removed < 0 || removed > step.Original)
				{
					removed = (removed < 0) ? 0 : step.Original;
					exact = false;
					measurementExact = false;
					recoverable = false;
				}
				exact &= removed == step.Taken;
				if (removed < step.Original && !SameSurvivingSource[i])
				{
					exact = false;
					measurementExact = false;
					recoverable = false;
				}
				if (removed <= 0)
				{
					continue;
				}
				any = true;
				if (removed == step.Original)
				{
					finalized++;
					recoverable = false;
				}
				AddLost(step, removed, lostMaterials, lostBits, lostExotics);
			}

			KingdomMaterialDebitCost requested = Plan.Requested;
			KingdomMaterialDebitCost lost = new KingdomMaterialDebitCost(lostMaterials, lostBits, lostExotics);
			KingdomMaterialDebitCost spent = Credit(requested, lost);
			KingdomMaterialDebitCost outstanding = Subtract(requested, spent);
			KingdomMaterialDebitOutcome outcome;
			if (exact && outstanding.IsEmpty)
			{
				outcome = KingdomMaterialDebitOutcome.ExactCommit;
				Fault = KingdomMaterialDebitFault.None;
				Failure = null;
			}
			else if (!any && measurementExact)
			{
				outcome = KingdomMaterialDebitOutcome.CleanRefusal;
			}
			else
			{
				outcome = any && recoverable
					? KingdomMaterialDebitOutcome.RecoverablePartial
					: KingdomMaterialDebitOutcome.IrreversiblePartial;
			}
			return new KingdomMaterialDebitResult(outcome, Fault, requested, spent,
				outstanding, lost, finalized, Failure, measurementExact);
		}

		public static bool CanCompensate(KingdomMaterialDebitPlan Plan,
			IList<int> Removed, IList<int> CurrentCounts, IList<bool> SameSurvivingSource)
		{
			if (Plan == null || Removed == null || CurrentCounts == null || SameSurvivingSource == null
				|| Removed.Count != Plan.Steps.Count || CurrentCounts.Count != Plan.Steps.Count
				|| SameSurvivingSource.Count != Plan.Steps.Count)
			{
				return false;
			}
			for (int i = 0; i < Plan.Steps.Count; i++)
			{
				KingdomMaterialDebitStep step = Plan.Steps[i];
				int removed = Removed[i];
				if (removed < 0 || removed >= step.Original || !SameSurvivingSource[i]
					|| CurrentCounts[i] != step.Original - removed)
				{
					return false;
				}
			}
			return true;
		}
	}
}
