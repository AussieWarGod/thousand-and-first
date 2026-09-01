#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomScaffoldLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomScaffoldLabourRules.cs",
			"Growth/KingdomScaffold.cs",
			"Growth/KingdomScaffold.LabourWindow.cs",
			"Growth/KingdomScaffold.WorkInitialization.cs",
			"Growth/KingdomScaffold.Durable.cs",
			"Growth/KingdomScaffold.RemovalProof.cs",
			"Growth/KingdomScaffold.SuccessorProof.cs",
			"Growth/KingdomScaffold.CompletionAndLegacy.cs"
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
