#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomHeartbeatLogicalSource
	{
		internal const int FileCount = 3;

		private static readonly string[] Paths =
		{
			"Simulation/City/KingdomHeartbeat.cs",
			"Simulation/City/KingdomHeartbeat.z01.Slice.cs",
			"Simulation/City/KingdomHeartbeat.z02.Prefetch.cs"
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
