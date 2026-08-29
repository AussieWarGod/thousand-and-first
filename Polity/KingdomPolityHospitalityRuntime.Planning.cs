using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityHospitalityRuntime
	{
		private static bool TryBuildRequest(KingdomSystem System,
			KingdomPolityIncidentRecord Terms, long Tick,
			out KingdomPolityHospitalityPlanRequest Request, out string Failure)
		{
			Request = null;
			Failure = null;
			Zone zone = The.Player?.CurrentZone;
			KingdomPolityCohortPlan cohort = Terms.ParticipantCohortRefs.Count == 1
				? KingdomPolityAuthority.Cohort(System.PolityLedger,
					Terms.ParticipantCohortRefs[0]) : null;
			if (zone == null || cohort == null || System.City?.SettlementId != cohort.SurfaceRef ||
				System.ClaimedZones == null || !System.ClaimedZones.Contains(zone.ZoneID) ||
				!KingdomWord.StandsIn(zone))
				return Fail("hospitality requires the delegation's loaded settlement", out Failure);
			KingdomSurvey survey = KingdomSurvey.Take(zone);
			if (!TryPlanFood(survey, out KingdomPolityHospitalityDebitLine food, out Failure))
				return false;
			if (!TryPlanWater(survey, out KingdomPolityHospitalityDebitLine water, out Failure))
				return false;
			Request = new KingdomPolityHospitalityPlanRequest
			{
				SurfaceRef = cohort.SurfaceRef, ZoneId = zone.ZoneID, PlannedTick = Tick,
				Lines = new List<KingdomPolityHospitalityDebitLine> { food, water }
			};
			return true;
		}

		private static bool TryPlanFood(KingdomSurvey Survey,
			out KingdomPolityHospitalityDebitLine Line, out string Failure)
		{
			Line = null;
			Failure = null;
			List<GameObject> larders = new List<GameObject>(Survey.Larders);
			larders.Sort((a, b) => string.CompareOrdinal(a?.IDIfAssigned, b?.IDIfAssigned));
			for (int i = 0; i < larders.Count; i++)
			{
				GameObject larder = larders[i];
				List<GameObject> foods = new List<GameObject>(
					larder?.Inventory?.Objects ?? new List<GameObject>());
				foods.Sort((a, b) => string.CompareOrdinal(a?.IDIfAssigned, b?.IDIfAssigned));
				for (int j = 0; j < foods.Count; j++)
				{
					GameObject food = foods[j];
					if (!GameObject.Validate(food) || food.Count < 1 ||
						(!food.HasPart("Food") &&
						 !food.HasPart("PreparedCookingIngredient"))) continue;
					if (!KingdomConstructionInputLeaseAuthority.TryObjectAvailableForLocalDebit(
						food, out string _)) continue;
					string larderId = larder?.IDIfAssigned;
					string foodId = food.IDIfAssigned;
					if (string.IsNullOrEmpty(larderId) || string.IsNullOrEmpty(foodId))
						return Fail("Hospitality food lacks assigned physical identity.", out Failure);
					Line = new KingdomPolityHospitalityDebitLine
					{
						Kind = KingdomPolityHospitalityDebitKind.Food,
						ContainerId = larderId, ObjectId = foodId,
						Blueprint = food.Blueprint, Before = food.Count,
						After = food.Count - 1
					};
					return true;
				}
			}
			return Fail("No uncommitted larder serving is available; diplomacy remains open.",
				out Failure);
		}

		private static bool TryPlanWater(KingdomSurvey Survey,
			out KingdomPolityHospitalityDebitLine Line, out string Failure)
		{
			Line = null;
			Failure = null;
			if (!Survey.TryReserveExactWater(1, out KingdomWaterDebit debit) ||
				!debit.TryDescribe(out KingdomWaterDebitLeg[] legs) || legs.Length != 1)
			{
				debit?.Rollback();
				return Fail("No uncommitted dram of fresh water is available; diplomacy remains open.",
					out Failure);
			}
			KingdomWaterDebitLeg leg = legs[0];
			string ownerId = leg.Owner?.IDIfAssigned;
			if (string.IsNullOrEmpty(ownerId))
			{
				debit.Rollback();
				return Fail("Hospitality water lacks assigned vessel identity.", out Failure);
			}
			Line = new KingdomPolityHospitalityDebitLine
			{
				Kind = KingdomPolityHospitalityDebitKind.Water,
				ContainerId = ownerId, ObjectId = ownerId,
				Blueprint = leg.Owner.Blueprint, Before = leg.BeforeVolume,
				After = leg.AfterVolume, Capacity = leg.MaxVolume
			};
			if (debit.Rollback()) return true;
			Line = null;
			return Fail("The exact hospitality water reservation could not be released.", out Failure);
		}
	}
}
