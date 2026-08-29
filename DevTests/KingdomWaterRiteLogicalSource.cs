#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomWaterRiteLogicalSource
	{
		internal const int FileCount = 5;

		private static readonly string[] Paths =
		{
			"Experience/KingdomWaterRite.cs",
			"Experience/KingdomWaterRite.z01.SharedLiving.cs",
			"Experience/KingdomWaterRite.z02.RiteTransaction.cs",
			"Experience/KingdomWaterRite.z03.OfferAndGates.cs",
			"Experience/KingdomWaterRite.z04.StampsAndCandidates.cs"
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
