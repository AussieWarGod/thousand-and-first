using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static bool TryPreflightPurposeEffect(KingdomSystem System,
			KingdomPurposeOperationReceipt Operation, out string Failure)
		{
			Failure = null;
			if (!KingdomPurposePortfolioRules.EffectIsOwed(Operation.SourceKind)) return true;
			if (!TryPurposeEffectContext(System, Operation,
				out KingdomPurposeEffectRuntimeContext context, out Failure)
				|| !PurposeEffectGroundStartsClean(context, out Failure)) return false;
			if (Operation.SourceKind == KingdomPurposeKind.Harvest)
				return TryPreflightHarvestEffect(Operation, context, out Failure);
			return TryPreflightRefineEffect(Operation, context, out Failure);
		}

		private static bool TryPreflightRefineEffect(KingdomPurposeOperationReceipt Operation,
			KingdomPurposeEffectRuntimeContext Context, out string Failure)
		{
			Failure = null;
			if (!KingdomMaterialDebitCost.TryParseClaim(Operation.MaterialRequested,
				out KingdomMaterialDebitCost row))
				return Fail("The purpose material claim is malformed.", out Failure);
			KingdomMaterialTally combined = row.Materials.Copy();
			combined.Add(Context.RawMaterial,
				KingdomPurposePortfolioRules.PurposeEffectRawUnits);
			KingdomMaterialDebit debit = KingdomMaterialDebit.Reserve(
				KingdomMaterials.StockForExactContainer(Context.Zone, Context.Store),
				new KingdomMaterialDebitCost(combined, row.Bits, row.Exotics));
			bool reserved = debit.Reservation.Outcome == KingdomMaterialDebitOutcome.Reserved;
			string reason = debit.Reservation.Failure;
			debit.Cancel();
			if (!reserved)
				return Fail(reason ?? "This work's own input store cannot cover its local and bounded-effect material claims together.",
					out Failure);
			return PurposeEffectProductIsMakeable(Context,
				KingdomPurposeEffectProductRole.Refined)
				|| Fail("The bounded effect's exact refined product cannot be made safely.",
					out Failure);
		}

		private static bool TryPreflightHarvestEffect(KingdomPurposeOperationReceipt Operation,
			KingdomPurposeEffectRuntimeContext Context, out string Failure)
		{
			Failure = null;
			KingdomSurvey survey = KingdomSurvey.Take(Context.Zone);
			List<KingdomPurposeDebitLine> row = new List<KingdomPurposeDebitLine>();
			if (!TryPlanFood(survey, Operation.FoodRequested, row, out Failure)) return false;
			int larderMatches = 0;
			for (int i = 0; i < survey.Larders.Count; i++)
				if (ReferenceEquals(survey.Larders[i], Context.Store)) larderMatches++;
			if (larderMatches != 1)
				return Fail("The Granary-Colossus is not indexed once as its city's exact larder.",
					out Failure);
			if (!KingdomOrdinaryFoodAuthority.TryCapture(
				out KingdomConstructionInputLeaseSnapshot leases, out Failure)) return false;
			int projected = 0;
			List<GameObject> items = Context.Store.Inventory.Objects;
			for (int i = 0; items != null && i < items.Count; i++)
			{
				GameObject item = items[i];
				if (!GameObject.Validate(item) || item.Blueprint != Context.CropBlueprint
					|| !ReferenceEquals(item.InInventory, Context.Store)
					|| !KingdomOrdinaryFoodAuthority.CanSpend(leases, item)
					|| AnyPurposeLandingField(item) || AnyPurposeEffectField(item)) continue;
				int remaining = item.Count - PlannedFoodTake(row, item.IDIfAssigned);
				if (remaining > 0) projected = projected > int.MaxValue - remaining
					? int.MaxValue : projected + remaining;
			}
			if (projected < KingdomPurposePortfolioRules.PurposeEffectCropUnits)
				return Fail("This granary's own store cannot cover its three-crop bounded effect after the local food plan.",
					out Failure);
			int room = KingdomSurvey.CapacityOf(Context.Store)
				- KingdomSurvey.HeldIn(Context.Store);
			if (room < KingdomPurposePortfolioRules.PurposeEffectStapleUnits)
				return Fail("This granary's own store lacks room for the six preserved measures.",
					out Failure);
			return PurposeEffectProductIsMakeable(Context,
					KingdomPurposeEffectProductRole.Seed)
				&& PurposeEffectProductIsMakeable(Context,
					KingdomPurposeEffectProductRole.Staple)
				|| Fail("This granary's exact seed or preserved product cannot be made safely.",
					out Failure);
		}

		private static int PlannedFoodTake(List<KingdomPurposeDebitLine> Lines, string ObjectId)
		{
			int total = 0;
			for (int i = 0; Lines != null && i < Lines.Count; i++)
				if (Lines[i].Kind == KingdomPurposeDebitKind.Food
					&& Lines[i].ObjectId == ObjectId)
					total += Lines[i].Before - Lines[i].After;
			return total;
		}
	}
}
