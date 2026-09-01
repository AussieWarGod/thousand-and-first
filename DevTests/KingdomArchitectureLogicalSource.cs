#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomArchitectureLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomArchitectureFault.cs",
			"Growth/KingdomArchitectureMapping.cs",
			"Growth/KingdomArchitecture.cs",
			"Growth/KingdomArchitecture.Loading.cs",
			"Growth/KingdomArchitecture.XmlRecords.cs",
			"Growth/KingdomArchitecture.RawMerge.cs",
			"Growth/KingdomArchitecture.Materialise.cs",
			"Growth/KingdomArchitecture.Drafts.cs",
			"Growth/KingdomArchitecture.Poses.cs",
			"Growth/KingdomArchitecturePoseParity.cs",
			"Growth/KingdomArchitecture.Records.cs",
			"Growth/KingdomArchitecture.Resolution.cs",
			"Growth/KingdomArchitecture.Attributes.cs",
			"Growth/KingdomArchitecture.Helpers.cs"
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
