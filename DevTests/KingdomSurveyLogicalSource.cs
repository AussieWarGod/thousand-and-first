#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomSurveyLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomSurvey.00.Declarations.cs",
			"Growth/KingdomSurvey.01.Capture.cs",
			"Growth/KingdomSurvey.02.IndexMaintenance.cs",
			"Growth/KingdomSurvey.03.LookupAndWaterDebit.cs",
			"Growth/KingdomSurvey.04.FoodConsumption.cs",
			"Growth/KingdomSurvey.05.FoodStorage.cs",
			"Growth/KingdomSurvey.06.ExactSpoilage.cs",
			"Growth/KingdomSurvey.07.ExactLeakage.cs",
			"Growth/KingdomSurvey.08.WaterStorage.cs",
			"Growth/KingdomSurvey.09.PoolsAndSynchronization.cs",
			"Growth/KingdomSurvey.cs"
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
