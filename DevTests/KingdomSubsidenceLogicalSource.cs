#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomSubsidenceLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomSubsidence.cs",
			"Growth/KingdomSubsidence.ScopeAndSightings.cs",
			"Growth/KingdomSubsidence.Reckoning.cs",
			"Growth/KingdomSubsidence.Breakpoints.cs"
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
