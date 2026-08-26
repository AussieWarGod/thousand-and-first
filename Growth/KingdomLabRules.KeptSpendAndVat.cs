using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLabRules
	{
		/// <summary>
		/// Plans a deterministic first-source-first debit without touching an engine object. Zero and
		/// negative source counts carry nothing. A false result has no partial plan.
		/// </summary>
		internal static bool TryPlanKeptSpend(IList<int> Available, int Owed, out KingdomKeptSpendPlan Plan)
		{
			Plan = null;
			if (Available == null || Owed < 0)
			{
				return false;
			}
			List<KingdomKeptSpendStep> steps = new List<KingdomKeptSpendStep>();
			int remaining = Owed;
			for (int i = 0; i < Available.Count && remaining > 0; i++)
			{
				int held = Available[i];
				if (held <= 0)
				{
					continue;
				}
				int take = (held < remaining) ? held : remaining;
				steps.Add(new KingdomKeptSpendStep(i, held, take));
				remaining -= take;
			}
			if (remaining != 0)
			{
				return false;
			}
			Plan = new KingdomKeptSpendPlan(Owed, steps);
			return true;
		}

		/// <summary>
		/// Pure phase classifier used by engine transaction and exhaustive tests. Once any terminal
		/// source vanished, failure is partial and reversible counts must not masquerade as rollback.
		/// </summary>
		internal static KingdomKeptSpendPhase KeptSpendPhase(KingdomKeptSpendPlan Plan,
			bool PreflightPassed, bool CountsApplied, int Finalized, bool OperationRefused,
			bool CountsRestored)
		{
			if (Plan == null || Finalized < 0 || Finalized > (Plan?.Finalizers ?? 0))
			{
				return KingdomKeptSpendPhase.Partial;
			}
			if (!PreflightPassed)
			{
				return (Finalized == 0 && CountsRestored)
					? KingdomKeptSpendPhase.RefusedClean
					: KingdomKeptSpendPhase.Partial;
			}
			if (!CountsApplied)
			{
				return OperationRefused
					? ((Finalized == 0 && CountsRestored)
						? KingdomKeptSpendPhase.RefusedClean
						: KingdomKeptSpendPhase.Partial)
					: KingdomKeptSpendPhase.ApplyCounts;
			}
			if (OperationRefused)
			{
				return (Finalized == 0 && CountsRestored)
					? KingdomKeptSpendPhase.RefusedClean
					: KingdomKeptSpendPhase.Partial;
			}
			if (Finalized < Plan.Finalizers)
			{
				return KingdomKeptSpendPhase.Finalize;
			}
			return KingdomKeptSpendPhase.SpentExact;
		}

		/// <summary>Whether an engine call durably changed the requested procedure despite what it
		/// returned or threw. Addition wants a larger presence count; removal wants a smaller one.</summary>
		internal static bool ProcedureEffectChanged(int Before, int After, bool Removing)
		{
			return Before >= 0 && After >= 0 && (Removing ? After < Before : After > Before);
		}

		internal static KingdomVatAccrual AccrueVat(long LastTick, long TimeTick, int RemainingTicks,
			int CrewEffectiveness, int WearEffectiveness, bool Settled, bool Cancelled)
		{
			return AccrueVat(LastTick, TimeTick, RemainingTicks, CrewEffectiveness,
				WearEffectiveness, Settled, Cancelled,
				KingdomIdentityAffinityRules.NeutralPercent);
		}

		internal static KingdomVatAccrual AccrueVat(long LastTick, long TimeTick,
			int RemainingTicks, int CrewEffectiveness, int WearEffectiveness, bool Settled,
			bool Cancelled, int IdentityAffinity)
		{
			int remaining = (RemainingTicks > 0) ? RemainingTicks : 0;
			if (Settled || Cancelled)
			{
				return new KingdomVatAccrual((TimeTick > LastTick) ? TimeTick : LastTick,
					remaining, 0, Complete: false);
			}
			if (remaining == 0)
			{
				return new KingdomVatAccrual((TimeTick > LastTick) ? TimeTick : LastTick,
					0, 0, Complete: true);
			}
			if (LastTick <= 0L)
			{
				return new KingdomVatAccrual((TimeTick > 0L) ? TimeTick : 0L,
					remaining, 0, Complete: false);
			}
			if (TimeTick <= LastTick)
			{
				return new KingdomVatAccrual(LastTick, remaining, 0, Complete: false);
			}
			int worked = KingdomProcedureRules.VatWorked(TimeTick - LastTick,
				CrewEffectiveness, WearEffectiveness, IdentityAffinity);
			if (worked <= 0)
			{
				return new KingdomVatAccrual(TimeTick, remaining, 0, Complete: false);
			}
			if (worked >= remaining)
			{
				return new KingdomVatAccrual(TimeTick, 0, remaining, Complete: true);
			}
			return new KingdomVatAccrual(TimeTick, remaining - worked, worked, Complete: false);
		}

		internal static KingdomVatSettlement VatSettlement(bool InputPresent, bool OutputPresent,
			bool WorkComplete, bool CancelRequested)
		{
			if (CancelRequested)
			{
				if (OutputPresent)
				{
					return KingdomVatSettlement.CollectOutput;
				}
				return InputPresent ? KingdomVatSettlement.ReturnInput : KingdomVatSettlement.Missing;
			}
			if (!WorkComplete)
			{
				return InputPresent ? KingdomVatSettlement.Wait
					: (OutputPresent ? KingdomVatSettlement.CollectOutput : KingdomVatSettlement.Missing);
			}
			if (OutputPresent)
			{
				return InputPresent ? KingdomVatSettlement.ConsumeInput : KingdomVatSettlement.CollectOutput;
			}
			return InputPresent ? KingdomVatSettlement.CreateOutput : KingdomVatSettlement.Missing;
		}

	}
}
