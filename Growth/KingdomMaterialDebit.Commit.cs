using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomMaterialDebit
	{
		/// <summary>
		/// Attempts the planned debit once. Exact success, clean refusal, recoverable partial and
		/// irreversible partial are separate results. A second call is idempotent and never mutates.
		/// </summary>
		public KingdomMaterialDebitResult Commit()
		{
			if (Result.Outcome != KingdomMaterialDebitOutcome.Reserved)
			{
				return Result;
			}
			if (Operating)
			{
				return Transient(KingdomMaterialDebitFault.Busy, "The material receipt is already operating.");
			}
			Operating = true;
			try
			{
				if (!CurrentLeaseAuthorityAllowsPlan() || !AllStillReserved())
				{
					MarkAllUncertain();
					return FinishFailure(KingdomMaterialDebitFault.SourceChanged,
						"A reserved stockpile source changed before the debit began.");
				}

				// Nonterminal stack work first. Stacker deliberately returns false after decrementing
				// one; the before/after count, not the boolean, is authoritative.
				for (int i = 0; i < Plan.Steps.Count; i++)
				{
					KingdomMaterialDebitStep step = Plan.Steps[i];
					if (step.NeedsFinalization)
					{
						continue;
					}
					Entry entry = EntryFor(step);
					for (int unit = 0; unit < step.Taken; unit++)
					{
						int expectedBefore = step.Original - unit;
						if (!CurrentLeaseAuthorityAllowsPlan()
							|| !ObservedStateMatches() || !StillSame(entry) ||
							entry.Item.Count != expectedBefore || Removed[i] != unit)
						{
							MarkAllUncertain();
							CaptureRemoved();
							return FinishFailure(KingdomMaterialDebitFault.SourceChanged,
								"A stack changed between measured decrements.");
						}
						try
						{
							MutationStarted = true;
							entry.Item.Destroy(null, Silent: true);
						}
						catch (Exception ex)
						{
							CaptureRemoved();
							return FinishFailure(KingdomMaterialDebitFault.Exception, Describe(ex));
						}
						CaptureRemoved();
						if (TopologyUncertain || !ExactObservations[i] ||
							Removed[i] != unit + 1 || !ObservedStateMatches() ||
							!StillSame(entry) || entry.Item.Count != expectedBefore - 1)
						{
							return FinishFailure(KingdomMaterialDebitFault.OperationRefused,
								"A stack did not yield exactly one measured unit.");
						}
					}
				}

				// Whole sources are necessarily irreversible after teardown. Each is last and calls
				// Obliterate exactly once; its one BeforeDestroy callback is authoritative.
				for (int i = 0; i < Plan.Steps.Count; i++)
				{
					KingdomMaterialDebitStep step = Plan.Steps[i];
					if (!step.NeedsFinalization)
					{
						continue;
					}
					Entry entry = EntryFor(step);
					if (!CurrentLeaseAuthorityAllowsPlan()
						|| !ObservedStateMatches() || !StillSame(entry) ||
						entry.Item.Count != step.Original || Removed[i] != 0)
					{
						MarkAllUncertain();
						CaptureRemoved();
						return FinishFailure(KingdomMaterialDebitFault.SourceChanged,
							"A terminal source changed before finalization.");
					}
					bool returned = false;
					try
					{
						MutationStarted = true;
						returned = entry.Item.Obliterate(null, Silent: true);
					}
					catch (Exception ex)
					{
						CaptureRemoved();
						return FinishFailure(KingdomMaterialDebitFault.Exception, Describe(ex));
					}
					CaptureRemoved();
					if (!TopologyUncertain && ExactObservations[i] &&
						Removed[i] == step.Original && !GameObject.Validate(entry.Item) &&
						ObservedStateMatches())
					{
						continue;
					}
					if (!TopologyUncertain && ExactObservations[i] && Removed[i] == 0 &&
						StillSame(entry) && entry.Item.Count == step.Original)
					{
						return FinishFailure(returned
							? KingdomMaterialDebitFault.OperationMismatch
							: KingdomMaterialDebitFault.OperationRefused,
							"A terminal source did not reach its promised final state.");
					}
					return FinishFailure(KingdomMaterialDebitFault.OperationMismatch,
						"A terminal callback left source ownership or count uncertain.");
				}

				CaptureRemoved();
				if (!AllAtPlannedResult())
				{
					return FinishFailure(KingdomMaterialDebitFault.OperationMismatch,
						"The physical post-debit state does not match the receipt.");
				}
				Result = Classify(KingdomMaterialDebitFault.None, null);
				AdjustStockFor(Result.Lost);
				return Result;
			}
			catch (Exception ex)
			{
				CaptureRemoved();
				return FinishFailure(KingdomMaterialDebitFault.Exception, Describe(ex));
			}
			finally
			{
				Operating = false;
			}
		}
	}
}
