#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomZoningLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomZoning.cs",
			"Growth/KingdomZoning.00.GateRegistry.cs",
			"Growth/KingdomZoning.01.RosterAndLearning.cs",
			"Growth/KingdomZoning.02.OffersAndJudgment.cs",
			"Growth/KingdomZoning.03.KeepersMenu.cs",
			"Growth/KingdomZoning.04.GroundJudgmentAndMegastructure.cs",
			"Growth/KingdomZoning.05.RefusalComposition.cs",
			"Growth/KingdomZoning.06.KeeperKnowledgeTransfer.cs",
			"Growth/KingdomZoning.07.RosterStorage.cs"
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
