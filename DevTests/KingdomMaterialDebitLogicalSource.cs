#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomMaterialDebitLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomMaterialDebit.cs",
			"Growth/KingdomMaterialDebit.Commit.cs",
			"Growth/KingdomMaterialDebit.Compensation.cs",
			"Growth/KingdomMaterialDebit.Sources.cs",
			"Growth/KingdomMaterialDebit.Validation.cs",
			"Growth/KingdomMaterialDebit.FailureRecovery.cs",
			"Growth/KingdomMaterialDebit.Observation.cs",
			"Growth/KingdomMaterialDebit.StockAndHelpers.cs"
		};

		internal static string Read()
		{
			StringBuilder source = new StringBuilder();
			for (int i = 0; i < Paths.Length; i++)
			{
				source.Append(TestMain.ReadRepositoryText(Paths[i])).Append('\n');
			}
			return source.ToString();
		}
	}
}
#endif
