#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomCityBookLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Simulation/City/KingdomCityBook.cs",
			"Simulation/City/KingdomCityBook.00.CoreZoneAndWorkColumns.cs",
			"Simulation/City/KingdomCityBook.01.ResidentClockAndToldColumns.cs",
			"Simulation/City/KingdomCityBook.02.MemosAndSidecars.cs",
			"Simulation/City/KingdomCityBook.03.CompositeAndCounts.cs",
			"Simulation/City/KingdomCityBook.04.NormalizeAuthority.cs",
			"Simulation/City/KingdomCityBook.05.NormalizeZoneAndWork.cs",
			"Simulation/City/KingdomCityBook.06.NormalizeResidents.cs",
			"Simulation/City/KingdomCityBook.07.NormalizeClockToldAndMetadata.cs",
			"Simulation/City/KingdomCityBook.08.ResidentAndBrinkAccess.cs",
			"Simulation/City/KingdomCityBook.09.ZoneAndStateRead.cs",
			"Simulation/City/KingdomCityBook.10.StatePublish.cs",
			"Simulation/City/KingdomCityBook.11.ClearAndColumnHelpers.cs"
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
