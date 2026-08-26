#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomSealLogicalSource
	{
		private static readonly string[] Files =
		{
			"Core/KingdomSeal.cs",
			"Core/KingdomSeal.Semantic.cs",
			"Core/KingdomSeal.Imports.cs",
			"Core/KingdomSeal.Staging.cs",
			"Core/KingdomSeal.Succession.cs",
			"Core/KingdomSeal.Synchronization.cs",
			"Core/KingdomSeal.Capture.cs",
			"Core/KingdomSeal.Reconciliation.cs",
			"Core/KingdomSeal.SavedState.cs",
			"Core/KingdomSeal.Utilities.cs"
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
