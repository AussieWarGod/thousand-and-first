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
		private static void RecoverFunding(GameObject Building, GameObject Actor, KingdomSystem System,
			r_KingdomLabJob Job, LabProcedure Procedure)
		{
			if (!CurrentAuthority(Building, Actor, System, Job, KingdomLabRegistryStatus.Active)
				|| !string.Equals(Job.PatientId, Actor?.ID, StringComparison.Ordinal)) return;
			bool waterExact = !Job.WaterQuarantined && Job.WaterPaid == Job.WaterOwed;
			if (!waterExact && !Job.WaterQuarantined)
			{
				KingdomSurvey survey = (Actor.CurrentZone == null) ? null : KingdomSurvey.Take(Actor.CurrentZone, System);
				KingdomWaterDebit debit;
				if (survey != null && survey.TryReserveExactWater(Job.WaterOwed - Job.WaterPaid, out debit))
				{
					if (!ValidApplicationTarget(Actor, Job, Procedure))
					{
						debit.Rollback();
						Job.Fault = "The frozen patient slot or bearer changed before retry payment. Nothing was charged.";
						return;
					}
					debit.Commit();
					if (!ValidApplicationTarget(Actor, Job, Procedure))
					{
						debit.Rollback();
						MergeWaterReceipt(Job, debit);
						Job.State = Job.WaterQuarantined ? KingdomLabJobPhase.ApplicationRecovery
							: KingdomLabJobPhase.FundingRecovery;
						Job.Fault = "The target changed during water retry callbacks; exact compensation was measured before any bit, kept, or body mutation.";
						EnsureJobGovernance(Job);
						return;
					}
					waterExact = MergeWaterReceipt(Job, debit);
				}
			}
			bool bitsExact = string.IsNullOrEmpty(Job.BitOutstanding);
			if (waterExact && !bitsExact)
			{
				if (!ValidApplicationTarget(Actor, Job, Procedure))
				{
					Job.Fault = "The frozen target changed before outstanding bit payment. Nothing further was charged.";
					return;
				}
				KingdomMaterialDebitCost cost;
				KingdomMaterialDebit debit = KingdomMaterialDebitCost.TryParseClaim(Job.BitOutstanding, out cost)
					? KingdomMaterials.ReserveComposite(Actor.CurrentZone, cost) : null;
				KingdomMaterialDebitResult result = (debit != null
					&& debit.Reservation.Outcome == KingdomMaterialDebitOutcome.Reserved)
					? debit.Commit() : null;
				bitsExact = result != null && result.Exact;
				if (result != null)
				{
					if (result.Outcome == KingdomMaterialDebitOutcome.RecoverablePartial
						&& debit.CanCompensate)
					{
						KingdomMaterialDebitResult compensation = debit.Compensate();
						if (compensation.Outcome == KingdomMaterialDebitOutcome.CompensatedExact)
						{
							result = compensation;
							bitsExact = false;
						}
					}
					Job.BitOutstanding = bitsExact ? "" : ((result.Outcome == KingdomMaterialDebitOutcome.CompensatedExact)
						? result.Requested.ToClaimString() : result.Outstanding.ToClaimString());
					Job.Fault = result.Failure ?? "";
				}
			}
			if (waterExact && bitsExact && !ValidApplicationTarget(Actor, Job, Procedure))
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The frozen target changed during funding callbacks. Paid receipts are preserved; no body effect was made.";
				EnsureJobGovernance(Job);
				return;
			}
			int keptOwed = Job.KeptOwed - Job.KeptPaid;
			KingdomKeptSpendPhase keptPhase = (keptOwed <= 0) ? KingdomKeptSpendPhase.SpentExact
				: KingdomKeptSpendPhase.RefusedClean;
			if (waterExact && bitsExact && keptOwed > 0)
			{
				if (!ValidApplicationTarget(Actor, Job, Procedure))
				{
					Job.Fault = "The frozen target changed before outstanding kept parts. Nothing further was consumed.";
					return;
				}
				KeptSpendPreparation preparation;
				keptPhase = PrepareKeptSpend(KeptParts(Actor), Procedure, out preparation, keptOwed);
				if (keptPhase == KingdomKeptSpendPhase.ApplyCounts)
				{
					keptPhase = SpendKeptExact(preparation);
					int measured = KeptSpent(preparation);
					Job.KeptPaid = Math.Min(Job.KeptOwed, Job.KeptPaid + measured);
					Job.KeptLost += measured;
					if (keptPhase == KingdomKeptSpendPhase.Partial)
					{
						Job.KeptMeasurementExact = false;
						Job.KeptQuarantined = true;
					}
				}
			}
			Job.State = Job.KeptQuarantined ? KingdomLabJobPhase.ApplicationRecovery
				: KingdomLabRules.FundingPhase(waterExact, bitsExact, keptPhase);
			EnsureJobGovernance(Job);
			if (Job.State == KingdomLabJobPhase.Working)
			{
				Job.Fault = "";
				Job.LastWorkedTick = The.Game?.TimeTicks ?? Job.LastWorkedTick;
				MessageQueue.AddPlayerMessage("{{G|The exact receipt is settled. The staffed work can begin.}}");
			}
			else
			{
				Popup.Show("The commission remains in funding recovery. No graft was made; every measured payment remains on its persisted receipt.");
			}
		}

		private static bool MergeWaterReceipt(r_KingdomLabJob Job, KingdomWaterDebit Debit)
		{
			if (Job == null || Debit == null)
			{
				return false;
			}
			KingdomLabWaterClaim claim = KingdomLabRules.MergeWaterClaim(Job.WaterOwed,
				Job.WaterPaid, Job.WaterLost, Job.WaterQuarantined,
				Debit.Spent, Debit.Lost, Debit.MeasurementExact);
			Job.WaterMeasurementExact = Job.WaterMeasurementExact && Debit.MeasurementExact;
			Job.WaterPaid = claim.Paid;
			Job.WaterLost = claim.Lost;
			Job.WaterQuarantined = claim.Quarantined;
			if (!claim.Settled && !string.IsNullOrEmpty(Debit.Failure))
			{
				Job.Fault = Debit.Failure;
			}
			if (claim.Quarantined)
			{
				Job.Fault = "The water receipt lost exact vessel identity or composition. Automatic retry is quarantined so the hall cannot charge an uncertain balance twice."
					+ (string.IsNullOrEmpty(Debit.Failure) ? "" : (" " + Debit.Failure));
			}
			return claim.Settled;
		}

	}
}
