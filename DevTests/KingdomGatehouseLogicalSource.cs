#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomGatehouseLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomGatehouse.cs",
			"Growth/KingdomGatehouse.Projection.cs",
			"Growth/KingdomGatehouse.ProjectionEvidence.cs",
			"Growth/KingdomGatehouse.ProjectionEvidenceScan.cs",
			"Growth/KingdomGatehouse.Validation.cs",
			"Growth/r_KingdomGatehouse.cs"
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
