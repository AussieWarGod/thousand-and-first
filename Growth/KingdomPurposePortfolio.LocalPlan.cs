using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static bool TryPlanLocalDebit(KingdomPurposeOperationReceipt Operation,
			Zone Zone, GameObject Input, out string Encoded, out string Failure)
		{
			Encoded = null;
			Failure = null;
			List<KingdomPurposeDebitLine> lines = new List<KingdomPurposeDebitLine>();
			KingdomSurvey survey = KingdomSurvey.Take(Zone);
			KingdomWaterDebit water = survey.ReserveExactWater(Operation.WaterRequested);
			KingdomWaterDebitLeg[] waterLegs = Array.Empty<KingdomWaterDebitLeg>();
			if (Operation.WaterRequested > 0
				&& (water.State != KingdomWaterDebitState.Reserved
					|| !water.TryDescribe(out waterLegs)))
				return Fail(water.Failure ?? "The exact purpose water cannot be reserved.",
					out Failure);
			if (Operation.WaterRequested > 0)
				for (int i = 0; i < waterLegs.Length; i++)
				{
					if (!KingdomConstructionInputLeaseAuthority.TryObjectAvailableForLocalDebit(
						waterLegs[i].Owner, out Failure))
					{
						water.Rollback();
						return false;
					}
					string ownerId = waterLegs[i].Owner.IDIfAssigned;
					if (string.IsNullOrEmpty(ownerId))
					{
						water.Rollback();
						return Fail("Purpose water lacks assigned container identity.", out Failure);
					}
					lines.Add(new KingdomPurposeDebitLine
					{
						Kind = KingdomPurposeDebitKind.Water,
						ContainerId = ownerId,
						ObjectId = ownerId, Blueprint = "",
						Before = waterLegs[i].BeforeVolume,
						After = waterLegs[i].AfterVolume, TypeIndex = -1,
						Capacity = waterLegs[i].MaxVolume
					});
				}
			water.Rollback();

			KingdomMaterialDebitCost cost;
			if (!KingdomMaterialDebitCost.TryParseClaim(Operation.MaterialRequested, out cost))
				return Fail("The purpose material claim is malformed.", out Failure);
			KingdomMaterials.MaterialStock stock =
				KingdomMaterials.StockForExactContainer(Zone, Input);
			KingdomMaterialDebit material = KingdomMaterialDebit.Reserve(stock, cost);
			if (material.Reservation.Outcome != KingdomMaterialDebitOutcome.Reserved
				|| !material.TryDescribe(out KingdomMaterialDebitLeg[] materialLegs))
				return Fail(material.Reservation.Failure
					?? "The exact purpose input store cannot cover its material claim.", out Failure);
			for (int i = 0; i < materialLegs.Length; i++)
			{
				KingdomMaterialDebitLeg leg = materialLegs[i];
				string containerId = leg.Container?.IDIfAssigned;
				string itemId = leg.Item?.IDIfAssigned;
				if (leg.Kind != KingdomMaterialDebitSourceKind.Material
					|| string.IsNullOrEmpty(containerId) || string.IsNullOrEmpty(itemId)
					|| containerId != Operation.SourceInputStoreId)
				{
					material.Cancel();
					return Fail("The purpose material plan escaped its exact input store.", out Failure);
				}
				lines.Add(new KingdomPurposeDebitLine
				{
					Kind = KingdomPurposeDebitKind.Material,
					ContainerId = containerId, ObjectId = itemId,
					Blueprint = leg.Blueprint, Before = leg.Before, After = leg.After,
					TypeIndex = leg.KindIndex
				});
			}
			material.Cancel();
			if (!TryPlanFood(survey, Operation.FoodRequested, lines, out Failure)) return false;
			Encoded = KingdomPurposePortfolioRules.EncodeLocalDebit(
				new KingdomPurposeLocalDebitReceipt
				{
					PairId = Operation.PairId, PairEpoch = Operation.PairEpoch,
					OperationId = Operation.OperationId,
					SourceSettlementId = Operation.SourceSettlementId,
					SourceZoneId = Operation.SourceZoneId, SourceWorkId = Operation.SourceWorkId,
					SourceInputStoreId = Operation.SourceInputStoreId,
					WaterRequested = Operation.WaterRequested,
					FoodRequested = Operation.FoodRequested,
					MaterialRequested = Operation.MaterialRequested, Lines = lines
				});
			return Encoded != null || Fail("The exact local-debit plan could not be encoded.",
				out Failure);
		}

		private static bool TryPlanFood(KingdomSurvey Survey, int Requested,
			List<KingdomPurposeDebitLine> Lines, out string Failure)
		{
			Failure = null;
			int remaining = Requested;
			List<GameObject> larders = new List<GameObject>(Survey.Larders);
			larders.Sort((a, b) => string.CompareOrdinal(a?.IDIfAssigned, b?.IDIfAssigned));
			for (int i = 0; i < larders.Count && remaining > 0; i++)
			{
				GameObject larder = larders[i];
				List<GameObject> foods = new List<GameObject>(
					larder?.Inventory?.Objects ?? new List<GameObject>());
				foods.Sort((a, b) => string.CompareOrdinal(a?.IDIfAssigned, b?.IDIfAssigned));
				for (int j = 0; j < foods.Count && remaining > 0; j++)
				{
					GameObject food = foods[j];
					if (!GameObject.Validate(food) || (!food.HasPart("Food")
						&& !food.HasPart("PreparedCookingIngredient"))) continue;
					if (!KingdomConstructionInputLeaseAuthority.TryObjectAvailableForLocalDebit(
						food, out Failure)) return false;
					string larderId = larder?.IDIfAssigned;
					string foodId = food.IDIfAssigned;
					if (string.IsNullOrEmpty(larderId) || string.IsNullOrEmpty(foodId))
						return Fail("Purpose food lacks assigned larder or item identity.", out Failure);
					int take = Math.Min(food.Count, remaining);
					Lines.Add(new KingdomPurposeDebitLine
					{
						Kind = KingdomPurposeDebitKind.Food, ContainerId = larderId,
						ObjectId = foodId, Blueprint = food.Blueprint,
						Before = food.Count, After = food.Count - take, TypeIndex = -1
					});
					remaining -= take;
				}
			}
			return remaining == 0 || Fail("Dedicated larders cannot cover the exact purpose food.",
				out Failure);
		}
	}
}
