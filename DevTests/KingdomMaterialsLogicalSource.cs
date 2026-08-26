#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomMaterialsLogicalSource
	{
		internal const int FileCount = 17;

		private static readonly string[] Paths =
		{
			"Growth/KingdomMaterials.00.r_KingdomClearance.cs",
			"Growth/KingdomMaterials.01.Declarations.cs",
			"Growth/KingdomMaterials.02.Registry.cs",
			"Growth/KingdomMaterials.03.StockClassification.cs",
			"Growth/KingdomMaterials.04.MaterialStock.cs",
			"Growth/KingdomMaterials.05.StockpileAndPaymentGates.cs",
			"Growth/KingdomMaterials.06.InfrastructureAndDelivery.cs",
			"Growth/KingdomMaterials.07.ClearanceOrdering.cs",
			"Growth/KingdomMaterials.08.StrikeOrdering.cs",
			"Growth/KingdomMaterials.09.StrikeStampAndCancellation.cs",
			"Growth/KingdomMaterials.10.SettlementPassAndYards.cs",
			"Growth/KingdomMaterials.11.StrikeWorkAndRecoveryEntry.cs",
			"Growth/KingdomMaterials.12.StrikeContinuation.cs",
			"Growth/KingdomMaterials.13.StrikeRemovalAndSalvage.cs",
			"Growth/KingdomMaterials.14.ClearanceWork.cs",
			"Growth/KingdomMaterials.15.GroundAndWalls.cs",
			"Growth/KingdomMaterials.cs",
		};

		internal static string Read()
		{
			StringBuilder source = new StringBuilder();
			for (int i = 0; i < Paths.Length; i++)
				source.Append(TestMain.ReadRepositoryText(Paths[i])).Append('\n');
			return source.ToString();
		}
	}
}
#endif
