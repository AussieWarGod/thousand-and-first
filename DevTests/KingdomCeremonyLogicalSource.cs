#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomCeremonyLogicalSource
	{
		internal const int FileCount = 4;

		private static readonly string[] Paths =
		{
			"Experience/KingdomCeremony.cs",
			"Experience/KingdomCeremony.z01.Raising.cs",
			"Experience/KingdomCeremony.z02.ConstructionOutbox.cs",
			"Experience/KingdomCeremony.z03.NotableAndPatternBook.cs"
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
