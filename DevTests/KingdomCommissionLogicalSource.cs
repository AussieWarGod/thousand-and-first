#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomCommissionLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomCommission.cs",
			"Growth/KingdomCommission.Recovery.cs",
			"Growth/KingdomCommission.Projection.cs",
			"Growth/KingdomCommission.Placement.cs"
		};

		internal static string Read()
		{
			StringBuilder source = new StringBuilder();
			for (int i = 0; i < Paths.Length; i++)
			{
				source.Append(TestMain.ReadRepositoryText(Paths[i])).Append('\n');
			}
			return source.ToString();
		}
	}
}
#endif
