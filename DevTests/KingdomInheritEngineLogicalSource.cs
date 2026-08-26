#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomInheritEngineLogicalSource
	{
		private static readonly string[] Files =
		{
			"Core/KingdomInheritApplyStatus.cs",
			"Core/KingdomInheritApplyFault.cs",
			"Core/KingdomInheritApplyResult.cs",
			"Core/KingdomInheritCellFacts.cs",
			"Core/KingdomInheritBuildSpec.cs",
			"Core/IKingdomInheritEngineHost.cs",
			"Core/KingdomInheritEngine.cs",
			"Core/KingdomInheritEngine.Preparation.cs",
			"Core/KingdomInheritEngine.Preflight.cs",
			"Core/KingdomInheritEngine.Cairn.cs",
			"Core/KingdomInheritEngine.ZoneHost.Creation.cs",
			"Core/KingdomInheritEngine.ZoneHost.Placement.cs",
			"Core/KingdomInheritEngine.ZoneHost.Architecture.cs"
		};

		internal static string Read()
		{
			StringBuilder source = new StringBuilder();
			for (int i = 0; i < Files.Length; i++)
			{
				source.Append(TestMain.ReadRepositoryText(Files[i]));
			}
			return source.ToString();
		}
	}
}
#endif
