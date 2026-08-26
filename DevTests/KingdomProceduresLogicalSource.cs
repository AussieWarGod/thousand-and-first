#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomProceduresLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomProcedures.00.Declarations.cs",
			"Growth/KingdomProcedures.01.Registry.cs",
			"Growth/KingdomProcedures.02.DiscoveryAndAnatomy.cs",
			"Growth/KingdomProcedures.03.Stamps.cs",
			"Growth/KingdomProcedures.04.GrantRouting.cs",
			"Growth/KingdomProcedures.05.GrantExecution.cs",
			"Growth/KingdomProcedures.06.OwnershipPublication.cs",
			"Growth/KingdomProcedures.07.RebuildAndSnapshots.cs",
			"Growth/KingdomProcedures.08.OwnershipClassification.cs",
			"Growth/KingdomProcedures.09.Removal.cs",
			"Growth/KingdomProcedures.10.Loader.cs",
			"Growth/KingdomProcedures.11.EffectLedger.Declarations.cs",
			"Growth/KingdomProcedures.12.EffectLedger.Tracking.cs",
			"Growth/KingdomProcedures.13.EffectLedger.Binding.cs",
			"Growth/KingdomProcedures.14.EffectLedger.Persistence.cs",
			"Growth/KingdomProcedures.15.Record.Declarations.cs",
			"Growth/KingdomProcedures.16.Record.Notes.cs",
			"Growth/KingdomProcedures.17.Record.Contracts.cs",
			"Growth/KingdomProcedures.18.Record.Persistence.cs",
			"Growth/KingdomProcedures.cs"
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
