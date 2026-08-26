#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomResearchLogicalSource
	{
		private static readonly string[] Files =
		{
			"Growth/KingdomResearch.cs",
			"Growth/KingdomResearch.Notes.cs",
			"Growth/KingdomResearch.Holding.cs",
			"Growth/KingdomResearch.Bench.cs",
			"Growth/KingdomResearch.SeedSources.cs",
			"Growth/KingdomResearch.SeedReceipts.cs",
			"Growth/KingdomResearch.Advance.cs",
			"Growth/KingdomResearch.Completion.cs",
			"Growth/KingdomResearch.Knowledge.cs",
			"Growth/KingdomResearch.Reading.cs"
		};

		internal static string Read()
		{
			StringBuilder source = new StringBuilder();
			for (int i = 0; i < Files.Length; i++)
				source.Append(TestMain.ReadRepositoryText(Files[i]));
			return source.ToString();
		}
	}
}
#endif
