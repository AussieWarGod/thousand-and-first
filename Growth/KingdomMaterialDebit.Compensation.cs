using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomMaterialDebit
	{
		/// <summary>
		/// Restores only counts on the same surviving objects, and only when every count is exactly
		/// the measured post-debit value. A finalized object is never resurrected or replaced.
		/// </summary>
		public KingdomMaterialDebitResult Compensate()
		{
			if (Result.Outcome == KingdomMaterialDebitOutcome.CompensatedExact
				|| Result.Outcome == KingdomMaterialDebitOutcome.Cancelled)
			{
				return Result;
			}
			if (Result.Outcome != KingdomMaterialDebitOutcome.RecoverablePartial
				&& Result.Outcome != KingdomMaterialDebitOutcome.ExactCommit)
			{
				return Transient(KingdomMaterialDebitFault.WrongPhase,
					"This receipt is not in a compensable phase.");
			}
			if (Operating)
			{
				return Transient(KingdomMaterialDebitFault.Busy,
					"The material receipt is already operating.");
			}
			Operating = true;
			try
			{
				if (!CanCompensate)
				{
					return Transient(KingdomMaterialDebitFault.CompensationUnsafe,
						"The exact original object/count proof no longer holds.");
				}
				KingdomMaterialDebitCost lossBefore = Result.Lost.Copy();
				for (int i = 0; i < Plan.Steps.Count; i++)
				{
					if (Removed[i] <= 0)
					{
						continue;
					}
					if (!TryRestoreCountAndFlush(i))
					{
						Result = Classify(KingdomMaterialDebitFault.CompensationFailed,
							"A restoration callback changed exact source ownership or count.");
						ReconcileStockFor(Result.Lost);
						return Result;
					}
				}
				if (!AllStillReserved())
				{
					Result = Classify(KingdomMaterialDebitFault.CompensationFailed,
						"One or more original stack counts could not be restored exactly.");
					ReconcileStockFor(Result.Lost);
					return Result;
				}
				if (StockAdjusted)
				{
					RestoreStockAdjustment(lossBefore);
				}
				Result = new KingdomMaterialDebitResult(
					KingdomMaterialDebitOutcome.CompensatedExact,
					KingdomMaterialDebitFault.None, Plan.Requested,
					new KingdomMaterialDebitCost(), Plan.Requested,
					new KingdomMaterialDebitCost(), 0, null);
				return Result;
			}
			catch (Exception ex)
			{
				CaptureRemoved();
				Result = Classify(KingdomMaterialDebitFault.CompensationFailed, Describe(ex));
				ReconcileStockFor(Result.Lost);
				return Result;
			}
			finally
			{
				Operating = false;
			}
		}

		/// <summary>Cancels a read-only reservation. No physical source is touched.</summary>
		public KingdomMaterialDebitResult Cancel()
		{
			if (Result.Outcome == KingdomMaterialDebitOutcome.Cancelled)
			{
				return Result;
			}
			if (Result.Outcome != KingdomMaterialDebitOutcome.Reserved)
			{
				return Transient(KingdomMaterialDebitFault.WrongPhase,
					"Only an unused material reservation can be cancelled.");
			}
			Result = new KingdomMaterialDebitResult(KingdomMaterialDebitOutcome.Cancelled,
				KingdomMaterialDebitFault.None, Plan.Requested, new KingdomMaterialDebitCost(),
				Plan.Requested, new KingdomMaterialDebitCost(), 0, null);
			return Result;
		}
	}
}
