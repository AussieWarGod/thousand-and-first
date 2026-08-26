using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		public static KingdomConstructionStartResult TryFundNew(KingdomConstructionJob Job,
			KingdomWaterDebit Water, KingdomMaterialDebit Material,
			out KingdomConstructionJob Published, out string Failure)
		{
			Published = Job;
			Failure = null;
			if (Job == null || Water == null || Material == null
				|| Water.State != KingdomWaterDebitState.Reserved
				|| Material.Reservation.Outcome != KingdomMaterialDebitOutcome.Reserved)
			{
				Water?.Rollback();
				Material?.Cancel();
				Failure = Water != null && Water.Failure != null
					? Water.Failure
					: (Material == null ? "The material receipt is absent." : Material.Reservation.Failure);
				return KingdomConstructionStartResult.Refused;
			}
			if (!TryPublish(Job, out Failure))
			{
				Water.Rollback();
				Material.Cancel();
				return KingdomConstructionStartResult.Refused;
			}
			Published = Job.Copy();
			return Fund(Published, Water, Material, true, out Published, out Failure);
		}

		/// <summary>Retries only claims proved outstanding by an earlier exact receipt.</summary>
		public static KingdomConstructionStartResult TryResumeFunding(KingdomConstructionJob Job,
			Zone Z, KingdomSurvey Survey, out KingdomConstructionJob Updated, out string Failure)
		{
			return TryResumeFunding(Job, Z, Survey, null, out Updated, out Failure);
		}

		internal static KingdomConstructionStartResult TryResumeFunding(KingdomConstructionJob Job,
			Zone Z, KingdomSurvey Survey, GameObject RequiredItem,
			out KingdomConstructionJob Updated, out string Failure)
		{
			Updated = Job;
			Failure = null;
			if (Job == null || Z == null || Survey == null || Job.Claims == null || !Job.Claims.Exact
				|| KingdomConstructionRules.ResumeAction(Job) != KingdomConstructionResumeAction.ResumeFunding)
			{
				Failure = "The construction claim is not safe to retry automatically.";
				return KingdomConstructionStartResult.Outstanding;
			}
			KingdomMaterialDebitCost outstanding;
			if (!KingdomMaterialDebitCost.TryParseClaim(Job.Claims.MaterialOutstanding, out outstanding))
			{
				Failure = "The outstanding material claim cannot be read.";
				return KingdomConstructionStartResult.Outstanding;
			}
			KingdomWaterDebit water = Survey.ReserveExactWater(Job.Claims.WaterOutstanding);
			KingdomMaterialDebit material = RequiredItem == null
				? KingdomMaterials.ReserveComposite(Z, outstanding)
				: KingdomMaterials.ReserveCompositeWithRequiredItem(Z, outstanding, RequiredItem);
			if (water.State != KingdomWaterDebitState.Reserved
				|| material.Reservation.Outcome != KingdomMaterialDebitOutcome.Reserved)
			{
				water.Rollback();
				material.Cancel();
				Failure = water.Failure ?? material.Reservation.Failure;
				return KingdomConstructionStartResult.Outstanding;
			}
			return Fund(Job.Copy(), water, material, false, out Updated, out Failure);
		}

		private static KingdomConstructionStartResult Fund(KingdomConstructionJob Job,
			KingdomWaterDebit Water, KingdomMaterialDebit Material, bool NewJob,
			out KingdomConstructionJob Updated, out string Failure)
		{
			Updated = Job;
			Failure = null;
			KingdomConstructionClaims beforeWater = Job.Claims.Copy();
			if (!TransitionAndPublish(ref Job, KingdomConstructionPhase.WaterPending, null, out Failure))
			{
				Water.Rollback();
				Material.Cancel();
				Updated = Job;
				return AcceptedResult(Job, NewJob);
			}
			bool waterCommitted = Water.Commit();
			KingdomConstructionClaims measured;
			if (!KingdomConstructionRules.TryApplyWaterAttempt(beforeWater, Water.Amount,
				Water.Spent, Water.Outstanding, Water.Lost, Water.MeasurementExact, out measured))
			{
				Job.Claims.Exact = false;
				TransitionAndPublish(ref Job, KingdomConstructionPhase.InspectionRequired,
					"The exact water receipt could not be reconciled.", out _);
				Material.Cancel();
				Updated = Job;
				Failure = Water.Failure ?? "The exact water receipt could not be reconciled.";
				return KingdomConstructionStartResult.Outstanding;
			}
			Job.Claims = measured;
			KingdomConstructionPhase waterPhase = Water.MeasurementExact
				? KingdomConstructionPhase.WaterSettled
				: KingdomConstructionPhase.InspectionRequired;
			if (!TransitionAndPublish(ref Job, waterPhase, Water.Failure, out Failure))
			{
				Material.Cancel();
				Updated = Job;
				return KingdomConstructionStartResult.Outstanding;
			}
			if (!waterCommitted)
			{
				Material.Cancel();
				Updated = Job;
				Failure = Water.Failure;
				if (Water.MeasurementExact && Water.Spent == 0 && NewJob)
				{
					TransitionAndPublish(ref Job, KingdomConstructionPhase.Compensated, Failure, out _);
					Updated = Job;
					return KingdomConstructionStartResult.Refused;
				}
				return KingdomConstructionStartResult.Outstanding;
			}

			if (!TransitionAndPublish(ref Job, KingdomConstructionPhase.MaterialPending, null, out Failure))
			{
				Material.Cancel();
				Updated = Job;
				return KingdomConstructionStartResult.Outstanding;
			}
			KingdomMaterialDebitResult result = Material.Commit();
			KingdomConstructionClaims materialMeasured;
			if (!KingdomConstructionRules.TryApplyMaterial(Job.Claims, result, out materialMeasured))
			{
				Job.Claims.Exact = false;
				TransitionAndPublish(ref Job, KingdomConstructionPhase.InspectionRequired,
					"The material receipt could not be reconciled.", out _);
				Updated = Job;
				Failure = result.Failure ?? "The material receipt could not be reconciled.";
				return KingdomConstructionStartResult.Outstanding;
			}
			Job.Claims = materialMeasured;
			if (result.Exact)
			{
				if (!TransitionAndPublish(ref Job, KingdomConstructionPhase.Funded, null, out Failure))
				{
					Updated = Job;
					return KingdomConstructionStartResult.Outstanding;
				}
				Updated = Job;
				return KingdomConstructionStartResult.Funded;
			}
			Failure = result.Failure;
			if (!result.Clean)
			{
				// Both partial outcomes carry an exact spent/outstanding split. Retry only that
				// persisted outstanding claim; quarantine outcomes that cannot prove such a split.
				TransitionAndPublish(ref Job, result.Partial
					? KingdomConstructionPhase.Outstanding : KingdomConstructionPhase.InspectionRequired,
					Failure, out _);
				Updated = Job;
				return KingdomConstructionStartResult.Outstanding;
			}

			// This attempt took no material. Return only this attempt's water into its exact vessels.
			if (!TransitionAndPublish(ref Job, KingdomConstructionPhase.CompensationPending,
				Failure, out _))
			{
				Updated = Job;
				return KingdomConstructionStartResult.Outstanding;
			}
			bool rolledBack = Water.Rollback();
			KingdomConstructionClaims afterRollback;
			if (!KingdomConstructionRules.TryApplyWaterAttempt(beforeWater, Water.Amount,
				Water.Spent, Water.Outstanding, Water.Lost, Water.MeasurementExact, out afterRollback))
			{
				Job.Claims.Exact = false;
			}
			else
			{
				// Keep material accounting already merged; this clean result added zero to it.
				afterRollback.MaterialSpent = Job.Claims.MaterialSpent;
				afterRollback.MaterialOutstanding = Job.Claims.MaterialOutstanding;
				afterRollback.MaterialLost = Job.Claims.MaterialLost;
				Job.Claims = afterRollback;
			}
			if (rolledBack && Water.MeasurementExact && NewJob)
			{
				TransitionAndPublish(ref Job, KingdomConstructionPhase.Compensated, Failure, out _);
				Updated = Job;
				return KingdomConstructionStartResult.Refused;
			}
			TransitionAndPublish(ref Job, Water.MeasurementExact
				? KingdomConstructionPhase.Outstanding : KingdomConstructionPhase.InspectionRequired,
				Failure ?? Water.Failure, out _);
			Updated = Job;
			return KingdomConstructionStartResult.Outstanding;
		}

		private static KingdomConstructionStartResult AcceptedResult(KingdomConstructionJob Job, bool NewJob)
		{
			// Reaching this helper means TryPublish already durably accepted the job. Even when the
			// first phase update failed before a debit, that accepted job is the civic commit boundary
			// and the semantic resolver owns it. Only explicit, durably compensated paths return Refused.
			return KingdomConstructionStartResult.Outstanding;
		}

	}
}
