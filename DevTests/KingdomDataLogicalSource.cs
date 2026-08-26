#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomDataLogicalSource
	{
		private static readonly string[] Files =
		{
			"Core/KingdomData.cs",
			"Core/KingdomData.Loading.cs",
			"Core/KingdomData.Buildings.cs",
			"Core/KingdomData.Catalogue.cs",
			"Core/KingdomData.Styles.cs"
		};

		internal static string Read()
		{
			StringBuilder source = new StringBuilder();
			for (int i = 0; i < Files.Length; i++)
			{
				source.Append(TestMain.ReadRepositoryText(Files[i]));
			}
			return source.ToString();
		}
	}
}
#endif
