#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomGatehouseLogicalSource
	{
		private static readonly string[] RuntimePaths = new string[]
		{
			"Growth/KingdomGatehouse.Construction.cs",
			"Growth/KingdomGatehouse.cs",
			"Growth/KingdomGatehouse.Audit.cs",
			"Growth/KingdomGatehouse.Strike.cs"
		};

		private static readonly string[] ProjectionPaths = new string[]
		{
			"Growth/KingdomGatehouse.Projection.cs",
			"Growth/KingdomGatehouse.ProjectionAuthority.cs"
		};

		private static readonly string[] EvidencePaths = new string[]
		{
			"Growth/KingdomGatehouse.ProjectionEvidence.cs",
			"Growth/KingdomGatehouse.ProjectionEvidence.LegacyCustody.cs",
			"Growth/KingdomGatehouse.ProjectionEvidence.Callbacks.cs",
			"Growth/KingdomGatehouse.ProjectionEvidence.Presentation.cs"
		};

		private static readonly string[] RulePaths = new string[]
		{
			"Growth/KingdomGatehouseRules.cs",
			"Growth/KingdomGatehouseRules.Receipt.cs",
			"Growth/KingdomGatehouseRules.Forms.cs",
			"Growth/KingdomGatehouseRules.Geometry.cs"
		};

		private static readonly string[] SupportingPaths = new string[]
		{
			"Growth/KingdomGatehouse.ProjectionEvidenceScan.cs",
			"Growth/KingdomGatehouse.Validation.cs",
			"Growth/r_KingdomGatehouse.cs"
		};

		internal static string Read()
		{
			return ReadFiles(RuntimePaths) + ReadFiles(ProjectionPaths)
				+ ReadFiles(EvidencePaths) + ReadFiles(SupportingPaths);
		}

		internal static string ReadRuntime()
		{
			return ReadFiles(RuntimePaths);
		}

		internal static string ReadProjection()
		{
			return ReadFiles(ProjectionPaths);
		}

		internal static string ReadProjectionEvidence()
		{
			return ReadFiles(EvidencePaths);
		}

		internal static string ReadRules()
		{
			return ReadFiles(RulePaths);
		}

		private static string ReadFiles(string[] Paths)
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
