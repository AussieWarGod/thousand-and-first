#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomCarryRuntimeLogicalSource
	{
		internal const int FileCount = 5;

		private static readonly string[] Paths =
		{
			"Experience/KingdomCarryRuntime.cs",
			"Experience/KingdomCarryRuntime.z01.DriveAndSinks.cs",
			"Experience/KingdomCarryRuntime.z02.Designation.cs",
			"Experience/KingdomCarryRuntime.z03.TrustedWorld.cs",
			"Experience/KingdomCarryRuntime.z04.ScheduleObservations.cs"
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
