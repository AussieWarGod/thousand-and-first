#if TAF_TESTS
using System.Collections.Generic;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomPurposePortfolioTestData
	{
		internal static string LocalDebit(KingdomPurposeOperationReceipt Operation)
		{
			List<KingdomPurposeDebitLine> lines = new List<KingdomPurposeDebitLine>();
			if (Operation.WaterRequested > 0)
				lines.Add(new KingdomPurposeDebitLine
				{
					Kind = KingdomPurposeDebitKind.Water,
					ContainerId = "water-" + Operation.OperationId,
					ObjectId = "water-" + Operation.OperationId,
					Blueprint = "", Before = Operation.WaterRequested + 1, After = 1,
					TypeIndex = -1, Capacity = Operation.WaterRequested + 1
				});
			if (KingdomMaterialDebitCost.TryParseClaim(Operation.MaterialRequested,
				out KingdomMaterialDebitCost cost))
				for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				{
					int count = cost.Materials.Get((KingdomMaterial)i);
					if (count > 0)
						lines.Add(new KingdomPurposeDebitLine
						{
							Kind = KingdomPurposeDebitKind.Material,
							ContainerId = Operation.SourceInputStoreId,
							ObjectId = "material-" + i + "-" + Operation.OperationId,
							Blueprint = "test-material-" + i, Before = count, After = 0,
							TypeIndex = i
						});
				}
			if (Operation.FoodRequested > 0)
				lines.Add(new KingdomPurposeDebitLine
				{
					Kind = KingdomPurposeDebitKind.Food,
					ContainerId = "larder-" + Operation.OperationId,
					ObjectId = "food-" + Operation.OperationId,
					Blueprint = "test-food", Before = Operation.FoodRequested,
					After = 0, TypeIndex = -1
				});
			return KingdomPurposePortfolioRules.EncodeLocalDebit(
				new KingdomPurposeLocalDebitReceipt
				{
					PairId = Operation.PairId, PairEpoch = Operation.PairEpoch,
					OperationId = Operation.OperationId,
					SourceSettlementId = Operation.SourceSettlementId,
					SourceZoneId = Operation.SourceZoneId,
					SourceWorkId = Operation.SourceWorkId,
					SourceInputStoreId = Operation.SourceInputStoreId,
					WaterRequested = Operation.WaterRequested,
					FoodRequested = Operation.FoodRequested,
					MaterialRequested = Operation.MaterialRequested, Lines = lines
				});
		}
	}
}
#endif
