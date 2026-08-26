using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	internal static partial class KingdomLab
	{
		/// <summary>
		/// Builds and preflights the physical receipt before water or body changes. Qud 2.0.211.51's
		/// <c>Stacker.BeforeDestroyObjectEvent</c> decrements and vetoes a non-obliterating destroy
		/// whenever Count is above one. The same check with <c>Obliterate=true</c> bypasses only that
		/// decrement, not any other veto, so every terminal source is asked before any is lost.
		/// </summary>
		private static KingdomKeptSpendPhase PrepareKeptSpend(List<GameObject> Kept, LabProcedure Procedure,
			out KeptSpendPreparation Preparation, int Owed = -1)
		{
			Preparation = null;
			List<GameObject> sources = new List<GameObject>();
			List<string> stamps = new List<string>();
			List<int> counts = new List<int>();
			for (int i = 0; Kept != null && i < Kept.Count; i++)
			{
				GameObject item = Kept[i];
				if (!GameObject.Validate(item) || item.Count <= 0 || sources.Contains(item))
				{
					continue;
				}
				string stamp = item.GetStringProperty(KingdomProcedures.StampProperty);
				if (!KingdomProcedureRules.StampCarries(stamp, Procedure.Grants)
					|| !KingdomProcedureRules.MagnitudeAdmits(Procedure, stamp))
				{
					continue;
				}
				sources.Add(item);
				stamps.Add(stamp);
				counts.Add(item.Count);
			}
			KingdomKeptSpendPlan plan;
			int owed = (Owed >= 0) ? Owed : Procedure.Preserved;
			if (!KingdomLabRules.TryPlanKeptSpend(counts, owed, out plan))
			{
				return KingdomKeptSpendPhase.RefusedClean;
			}
			Preparation = new KeptSpendPreparation(sources, stamps, Procedure, plan);
			return PreflightKeptSpend(Preparation);
		}

		private static KingdomKeptSpendPhase PreflightKeptSpend(KeptSpendPreparation Preparation)
		{
			KingdomKeptSpendPlan plan = Preparation.Plan;
			// Destroy() itself dispatches BeforeDestroyObjectEvent. Calling Check here dispatched
			// the destructive callback twice for every terminal source and let the first callback
			// mutate topology before the durable spend began. Preflight is therefore observation
			// only; every consumed unit gets exactly the one callback owned by Destroy below.
			if (!SourcesAtOriginal(Preparation))
			{
				return KingdomLabRules.KeptSpendPhase(plan, PreflightPassed: false,
					CountsApplied: false, Finalized: 0, OperationRefused: true,
					CountsRestored: false);
			}
			return KingdomLabRules.KeptSpendPhase(plan, PreflightPassed: true,
				CountsApplied: false, Finalized: 0, OperationRefused: false, CountsRestored: true);
		}

		/// <summary>
		/// Spends every unit through Qud's ordinary <c>Destroy</c> path. Stacker owns each decrement;
		/// the last unit owns the real object lifecycle. No direct count write may bypass a callback.
		/// If a later final unit refuses, reversible stack decrements are restored only while no whole
		/// source has vanished; after that, the caller receives Partial and keeps the receipt.
		/// </summary>
		private static KingdomKeptSpendPhase SpendKeptExact(KeptSpendPreparation Preparation)
		{
			if (Preparation == null || !SourcesAtOriginal(Preparation))
			{
				return KingdomKeptSpendPhase.Partial;
			}
			KingdomKeptSpendPlan plan = Preparation.Plan;
			List<int> changed = new List<int>();
			int finalized = 0;
			for (int i = 0; i < plan.Steps.Count; i++)
			{
				KingdomKeptSpendStep step = plan.Steps[i];
				GameObject item = Preparation.Sources[step.Source];
				if (!changed.Contains(step.Source))
				{
					changed.Add(step.Source);
				}
				for (int unit = 0; unit < step.Taken; unit++)
				{
					int expected = step.Original - unit;
					if (!GameObject.Validate(item) || item.Count != expected)
					{
						bool restored = finalized == 0
							&& RestoreChangedCounts(Preparation, changed);
						return KingdomLabRules.KeptSpendPhase(plan, PreflightPassed: true,
							CountsApplied: finalized > 0, Finalized: finalized,
							OperationRefused: true, CountsRestored: restored);
					}
					try
					{
						item.Destroy(null, Silent: true);
					}
					catch (Exception ex)
					{
						KingdomLog.Log("lab: kept unit release threw (" + ex.Message + ")");
					}
					bool last = expected == 1;
					bool measured = last ? !GameObject.Validate(item)
						: (GameObject.Validate(item) && item.Count == expected - 1);
					if (!measured)
					{
						bool restored = finalized == 0
							&& RestoreChangedCounts(Preparation, changed);
						return KingdomLabRules.KeptSpendPhase(plan, PreflightPassed: true,
							CountsApplied: finalized > 0, Finalized: finalized,
							OperationRefused: true, CountsRestored: restored);
					}
				}
				if (step.NeedsFinalization)
				{
					finalized++;
				}
			}
			bool exact = SourcesAtPlannedResult(Preparation);
			return KingdomLabRules.KeptSpendPhase(plan, PreflightPassed: true,
				CountsApplied: true, Finalized: finalized, OperationRefused: !exact,
				CountsRestored: false);
		}

		private static bool SourcesAtOriginal(KeptSpendPreparation Preparation)
		{
			for (int i = 0; i < Preparation.Plan.Steps.Count; i++)
			{
				KingdomKeptSpendStep step = Preparation.Plan.Steps[i];
				GameObject item = Preparation.Sources[step.Source];
				string stamp = GameObject.Validate(item)
					? item.GetStringProperty(KingdomProcedures.StampProperty)
					: null;
				if (!GameObject.Validate(item) || item.Count != step.Original
					|| !string.Equals(stamp, Preparation.Stamps[step.Source], StringComparison.Ordinal)
					|| !KingdomProcedureRules.StampCarries(stamp, Preparation.Procedure.Grants)
					|| !KingdomProcedureRules.MagnitudeAdmits(Preparation.Procedure, stamp))
				{
					return false;
				}
			}
			return true;
		}

		private static bool SourcesAtPlannedResult(KeptSpendPreparation Preparation)
		{
			for (int i = 0; i < Preparation.Plan.Steps.Count; i++)
			{
				KingdomKeptSpendStep step = Preparation.Plan.Steps[i];
				GameObject item = Preparation.Sources[step.Source];
				string stamp = GameObject.Validate(item)
					? item.GetStringProperty(KingdomProcedures.StampProperty)
					: null;
				if (step.NeedsFinalization ? GameObject.Validate(item)
					: (!GameObject.Validate(item) || item.Count != step.Remaining
						|| !string.Equals(stamp, Preparation.Stamps[step.Source], StringComparison.Ordinal)
						|| !KingdomProcedureRules.StampCarries(stamp, Preparation.Procedure.Grants)
						|| !KingdomProcedureRules.MagnitudeAdmits(Preparation.Procedure, stamp)))
				{
					return false;
				}
			}
			return true;
		}

		private static bool RestoreChangedCounts(KeptSpendPreparation Preparation, List<int> Changed)
		{
			for (int i = Changed.Count - 1; i >= 0; i--)
			{
				int source = Changed[i];
				KingdomKeptSpendStep step = StepForSource(Preparation.Plan, source);
				GameObject item = Preparation.Sources[source];
				try
				{
					if (GameObject.Validate(item) && item.Count != step.Original)
					{
						item.Count = step.Original;
						item.FlushTransientCache();
						item.FlushContextWeightCaches();
						item.InInventory?.Inventory?.FlushWeightCache();
					}
				}
				catch (Exception ex)
				{
					KingdomLog.Log("lab: kept count rollback threw (" + ex.Message + ")");
				}
			}
			return SourcesAtOriginal(Preparation);
		}

		private static KingdomKeptSpendStep StepForSource(KingdomKeptSpendPlan Plan, int Source)
		{
			for (int i = 0; i < Plan.Steps.Count; i++)
			{
				if (Plan.Steps[i].Source == Source)
				{
					return Plan.Steps[i];
				}
			}
			return new KingdomKeptSpendStep(Source, 0, 0);
		}
	}
}
