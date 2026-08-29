#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomPlanMarkerLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomPlanMarker.cs",
			"Growth/KingdomPlanMarker.Realization.cs",
			"Growth/KingdomPlanMarker.RecoveryAndInspection.cs",
			"Growth/KingdomPlanMarker.LookupAndCommands.cs",
			"Growth/KingdomPlanMarker.Provenance.cs",
			"Growth/KingdomPlanMarker.CustodyAndRegistry.cs",
			"Growth/KingdomPlanMarker.Commands.cs"
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
