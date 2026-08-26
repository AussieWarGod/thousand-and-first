#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomTradeLogicalSource
	{
		internal const int FileCount = 23;

		private static readonly string[] Paths =
		{
			"Trade/KingdomTrade.00.Declarations.cs",
			"Trade/KingdomTrade.01.AuthorityFrames.cs",
			"Trade/KingdomTrade.02.ExileSeals.cs",
			"Trade/KingdomTrade.03.LoadedTopologyCapture.cs",
			"Trade/KingdomTrade.04.LoadedTopologyResolution.cs",
			"Trade/KingdomTrade.05.ReceiptFrames.cs",
			"Trade/KingdomTrade.06.PublicAuthority.cs",
			"Trade/KingdomTrade.07.DealStrike.cs",
			"Trade/KingdomTrade.08.ActivationAndManifest.cs",
			"Trade/KingdomTrade.09.ExileAndClocks.cs",
			"Trade/KingdomTrade.10.DeliveryPreparation.cs",
			"Trade/KingdomTrade.11.OperationAndResources.cs",
			"Trade/KingdomTrade.12.WaterMutation.cs",
			"Trade/KingdomTrade.13.WaterRecovery.cs",
			"Trade/KingdomTrade.14.MaterialMutation.cs",
			"Trade/KingdomTrade.15.MaterialRecovery.cs",
			"Trade/KingdomTrade.16.ProjectionMutation.cs",
			"Trade/KingdomTrade.17.ProjectionRecovery.cs",
			"Trade/KingdomTrade.18.DomainAccounting.cs",
			"Trade/KingdomTrade.19.Outbox.cs",
			"Trade/KingdomTrade.20.PatternBook.cs",
			"Trade/KingdomTrade.21.Quarantine.cs",
			"Trade/KingdomTrade.cs"
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
