#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class FounderBasinLogicalSource
	{
		internal const int FileCount = 7;

		private static readonly string[] Paths = new string[]
		{
			"Founding/FounderBasin.cs",
			"Founding/FounderBasin.Receipt.cs",
			"Founding/FounderBasin.ReceiptStorage.cs",
			"Founding/FounderBasin.VillageEffect.cs",
			"Founding/FounderBasin.Runtime.cs",
			"Founding/FounderBasin.Rite.cs",
			"Founding/FounderBasin.RiteText.cs"
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
