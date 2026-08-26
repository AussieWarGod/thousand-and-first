#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomSuccessionLogicalSource
	{
		private static readonly string[] Paths =
		{
			"Experience/KingdomSuccession.cs",
			"Experience/KingdomSuccession.DeathSelection.cs",
			"Experience/KingdomSuccession.DeathExecution.cs",
			"Experience/KingdomSuccession.PendingRite.cs",
			"Experience/KingdomSuccession.RiteRecovery.cs",
			"Experience/KingdomSuccession.BodyTransferAndRepair.cs",
			"Experience/KingdomSuccession.Accession.cs",
			"Experience/KingdomSuccession.HeirsAndNews.cs",
			"Experience/KingdomSuccession.ChronicleAndKnowledge.cs",
			"Experience/KingdomSuccession.PendingSeal.cs",
			"Experience/KingdomSuccession.SaveValidation.cs",
			"Experience/KingdomSuccession.TellingAndModels.cs"
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
