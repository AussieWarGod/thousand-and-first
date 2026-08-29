#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomAnnexeLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomAnnexe.cs",
			"Growth/KingdomAnnexe.Register.cs",
			"Growth/KingdomAnnexe.Enrollment.cs",
			"Growth/KingdomAnnexe.Lookup.cs",
			"Growth/KingdomAnnexe.Purpose.cs"
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
