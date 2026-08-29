#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomDelveLinkLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomDelveLink.cs",
			"Growth/KingdomDelveLink.00.ReceiptDeclarationsAndPreflight.cs",
			"Growth/KingdomDelveLink.01.SettlementAndStrikePreflight.cs",
			"Growth/KingdomDelveLink.02.StrikeCompletionAndReceiptProof.cs",
			"Growth/KingdomDelveLink.02b.LoadedCompletionProof.cs",
			"Growth/KingdomDelveLink.03.DerivationAndFootSafety.cs",
			"Growth/KingdomDelveLink.04.ReceiptAndEndpointCustody.cs",
			"Growth/KingdomDelveLink.05.ConnectionStrikeAndFaultHelpers.cs"
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
