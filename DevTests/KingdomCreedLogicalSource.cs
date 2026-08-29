#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomCreedLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Core/KingdomCreed.cs",
			"Core/KingdomCreed.00.ContentAndDraw.cs",
			"Core/KingdomCreed.01.ResidentHistory.cs",
			"Core/KingdomCreed.02.DissentAndBrink.cs",
			"Core/KingdomCreed.03.RiteAndDeclaration.cs",
			"Core/KingdomCreed.03a.PublicationTransactions.cs",
			"Core/KingdomCreed.04.SecessionAndRejoin.cs",
			"Core/KingdomCreed.05.ReportingAndReconciliation.cs"
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
