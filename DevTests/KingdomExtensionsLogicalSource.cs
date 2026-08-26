#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomExtensionsLogicalSource
	{
		private static readonly string[] Files =
		{
			"Api/KingdomExtensions.cs",
			"Api/KingdomExtensions.Registration.cs",
			"Api/KingdomExtensions.Asks.cs",
			"Api/KingdomExtensions.Happenings.cs",
			"Api/KingdomExtensions.Identity.cs",
			"Api/KingdomExtensions.Helpers.cs",
			"Api/KingdomExtensions.Jobs.cs"
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
