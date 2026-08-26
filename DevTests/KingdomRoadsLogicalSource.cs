#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomRoadsLogicalSource
	{
		internal const int FileCount = 10;

		private static readonly string[] Paths =
		{
			"Growth/KingdomRoads.00.DeclarationsAndRetry.cs",
			"Growth/KingdomRoads.01.ZoneStateAndGround.cs",
			"Growth/KingdomRoads.02.SettlementPass.cs",
			"Growth/KingdomRoads.03.Errands.cs",
			"Growth/KingdomRoads.04.WearAndPresentation.cs",
			"Growth/KingdomRoads.05.PavingEntry.cs",
			"Growth/KingdomRoads.06.PavingProjection.cs",
			"Growth/KingdomRoads.07.RoadReceiptCodec.cs",
			"Growth/KingdomRoads.08.RoadReceiptHelpersAndStatus.cs",
			"Growth/KingdomRoads.cs",
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
