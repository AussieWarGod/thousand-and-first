namespace ThousandAndFirst
{
	using XRL.World;
	using XRL.World.Parts;

	internal static partial class KingdomLab
	{
		/// <summary>
		/// Settles a fresh commission's funding: commits the reserved water, runs the bit
		/// callbacks, and decides whether either has to be compensated.
		/// <para>
		/// The whole span lives inside ONE declared compensation window, and the window is closed
		/// in a <c>finally</c> rather than by a statement at the bottom. Every step in here can
		/// throw &mdash; <c>ValidApplicationTarget</c> reads a live body, <c>MergeWaterReceipt</c>
		/// writes a persisted receipt, the bit debit commits and compensates against real objects,
		/// and <c>EnsureJobGovernance</c> republishes the job &mdash; and an escaping exception
		/// would otherwise leave this receipt Committed with its window still open. A debit in
		/// that state keeps answering <c>HoldsVessels</c> for every vessel it bound (see
		/// <c>KingdomWaterDebit.OpenReservations</c>), so the hall's own basin could never be
		/// widened again: the next reconcile would refuse the rung's capacity and tell the founder
		/// an open debit is bound to it, for a debit whose caller is long gone.
		/// </para>
		/// <para>
		/// The window is opened BEFORE the commit because the compensation this covers happens
		/// AFTER it: each <c>Rollback</c> below re-proves that every bound vessel still measures
		/// the MaxVolume it was reserved at, so a basin widening landing mid-span would turn a
		/// recoverable interruption into water the founder never gets back.
		/// </para>
		/// </summary>
		/// <returns>True when the funding settled far enough for the caller to go on and spend
		/// kept parts; false when the receipt was compensated or quarantined and the commission
		/// must stop where it stands. Both payment measurements are reported either way.</returns>
		private static bool SettleCommissionFunding(GameObject Actor, r_KingdomLabJob job,
			LabProcedure frozen, KingdomWaterDebit debit, KingdomMaterialDebit bitDebit,
			KingdomBitTally bitCost, out bool waterExact, out bool bitsExact)
		{
			waterExact = false;
			bitsExact = false;
			debit.BeginCompensationWindow();
			try
			{
				debit.Commit();
				if (!ValidApplicationTarget(Actor, job, frozen))
				{
					debit.Rollback();
					MergeWaterReceipt(job, debit);
					bitDebit?.Cancel();
					job.State = job.WaterQuarantined ? KingdomLabJobPhase.ApplicationRecovery
						: KingdomLabJobPhase.FundingRecovery;
					job.Fault = job.WaterQuarantined
						? "The target changed during water callbacks and exact compensation could not be proved. The receipt is quarantined."
						: "The target changed during water callbacks. The exact debit was compensated; retry charges only the outstanding price.";
					EnsureJobGovernance(job);
					return false;
				}
				waterExact = MergeWaterReceipt(job, debit);
				bitsExact = bitCost.IsEmpty();
				if (!waterExact)
				{
					bitDebit?.Cancel();
				}
				else if (bitDebit != null)
				{
					if (!ValidApplicationTarget(Actor, job, frozen))
					{
						debit.Rollback();
						MergeWaterReceipt(job, debit);
						bitDebit.Cancel();
						job.State = job.WaterQuarantined ? KingdomLabJobPhase.ApplicationRecovery
							: KingdomLabJobPhase.FundingRecovery;
						job.Fault = "The exact target changed before bit commit. Water compensation was measured; no bits or body effect were touched.";
						EnsureJobGovernance(job);
						return false;
					}
					KingdomMaterialDebitResult bitResult = bitDebit.Commit();
					bitsExact = bitResult.Exact;
					if (bitResult.Outcome == KingdomMaterialDebitOutcome.RecoverablePartial
						&& bitDebit.CanCompensate)
					{
						KingdomMaterialDebitResult compensation = bitDebit.Compensate();
						if (compensation.Outcome == KingdomMaterialDebitOutcome.CompensatedExact)
						{
							bitResult = compensation;
						}
					}
					job.BitOutstanding = bitsExact ? "" : ((bitResult.Outcome == KingdomMaterialDebitOutcome.CompensatedExact)
						? bitDebit.Reservation.Requested.ToClaimString()
						: bitResult.Outstanding.ToClaimString());
					if (!bitsExact)
					{
						job.Fault = bitResult.Failure ?? "The exact bit debit was interrupted.";
					}
				}
				if (waterExact && bitsExact && !ValidApplicationTarget(Actor, job, frozen))
				{
					bool bitsRestored = bitDebit == null;
					if (bitDebit != null && bitDebit.CanCompensate)
					{
						KingdomMaterialDebitResult compensation = bitDebit.Compensate();
						bitsRestored = compensation.Outcome == KingdomMaterialDebitOutcome.CompensatedExact;
						if (bitsRestored) job.BitOutstanding = job.BitClaim;
					}
					bool waterRestored = debit.Rollback();
					MergeWaterReceipt(job, debit);
					job.State = (bitsRestored && waterRestored && !job.WaterQuarantined)
						? KingdomLabJobPhase.FundingRecovery : KingdomLabJobPhase.ApplicationRecovery;
					job.Fault = (bitsRestored && waterRestored && !job.WaterQuarantined)
						? "The exact target changed during funding callbacks. Water and bits were compensated; no kept part or body effect was touched."
						: "The exact target changed during funding callbacks and complete compensation could not be proved. The receipt is quarantined.";
					EnsureJobGovernance(job);
					return false;
				}
				return true;
			}
			finally
			{
				// Closed before the kept parts and the body effect below, which must be free to
				// widen whatever this receipt drained -- and closed on the throwing path too, so
				// no abandoned receipt can hold a vessel still after its caller has unwound.
				debit.EndCompensationWindow();
			}
		}
	}
}
