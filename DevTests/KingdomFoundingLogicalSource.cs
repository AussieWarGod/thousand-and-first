#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomFoundingLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Core/KingdomFounding.cs",
			"Core/KingdomFounding.00.FirstFoundingRegistration.cs",
			"Core/KingdomFounding.01.FirstPublication.cs",
			"Core/KingdomFounding.02.FoundingStandings.cs",
			"Core/KingdomFounding.03.SiteJudgmentAndStyle.cs",
			"Core/KingdomFounding.04.Claims.cs",
			"Core/KingdomFounding.05.Citizenship.cs",
			"Core/KingdomFounding.06.RuinRestoration.cs",
			"Core/KingdomFounding.07.VillageCharter.cs"
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
