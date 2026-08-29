#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomConstructionLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomConstruction.cs",
			"Growth/KingdomConstruction.BuildTruth.cs",
			"Growth/KingdomConstruction.Registry.cs",
			"Growth/KingdomConstruction.Funding.cs",
			"Growth/KingdomConstruction.Transitions.cs",
			"Growth/KingdomConstruction.Physical.cs",
			"Growth/KingdomConstruction.Settlement.cs",
			"Growth/KingdomConstruction.PlotLabour.cs"
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
