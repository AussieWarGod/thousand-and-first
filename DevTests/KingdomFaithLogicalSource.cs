#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomFaithLogicalSource
	{
		internal const int FileCount = 4;

		private static readonly string[] Paths =
		{
			"Experience/KingdomFaith.cs",
			"Experience/KingdomFaith.z01.ShrinePass.cs",
			"Experience/KingdomFaith.z02.ShrinePressureAndEducation.cs",
			"Experience/KingdomFaith.z03.EducationAndConsecration.cs"
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
