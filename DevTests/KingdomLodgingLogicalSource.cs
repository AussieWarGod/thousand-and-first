#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomLodgingLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomLodging.cs",
			"Growth/KingdomLodging.Assignments.cs",
			"Growth/KingdomLodging.BrinkAndObservation.cs",
			"Growth/KingdomLodging.ResidentsAndCondemnation.cs",
			"Growth/KingdomLodging.HomesAndReporting.cs",
			"Growth/KingdomLodging.LabFriction.cs"
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
