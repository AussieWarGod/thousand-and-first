#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomHappeningsLogicalSource
	{
		internal const int FileCount = 5;

		private static readonly string[] Paths =
		{
			"Simulation/City/KingdomHappenings.cs",
			"Simulation/City/KingdomHappenings.z01.FestivalsAndPilgrims.cs",
			"Simulation/City/KingdomHappenings.z02.WeddingsAndBreakdowns.cs",
			"Simulation/City/KingdomHappenings.z03.Funerals.cs",
			"Simulation/City/KingdomHappenings.z04.ReportingAndPlumbing.cs"
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
