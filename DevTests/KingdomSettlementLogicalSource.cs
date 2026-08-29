#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomSettlementLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Core/KingdomSettlement.cs",
			"Core/KingdomSettlement.Fields.cs",
			"Core/KingdomSettlement.Normalize.cs",
			"Core/KingdomSettlement.Transfer.cs",
			"Core/KingdomSettlement.Vocations.cs",
			"Core/KingdomSettlement.Reflection.cs"
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
