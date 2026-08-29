#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomJobRegistryLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Simulation/City/KingdomJobRegistry.cs",
			"Simulation/City/KingdomJobRegistry.z01.LegPlan.cs",
			"Simulation/City/KingdomJobRegistry.z02.JobRow.cs",
			"Simulation/City/KingdomJobRegistry.z03.JobRowMutations.cs",
			"Simulation/City/KingdomJobRegistry.z04.Rules.cs",
			"Simulation/City/KingdomJobRegistry.z05.Itinerary.cs",
			"Simulation/City/KingdomJobRegistry.z06.Draws.cs",
			"Simulation/City/KingdomJobRegistry.z07.Table.cs",
			"Simulation/City/KingdomJobRegistry.z08.TableValidation.cs",
			"Simulation/City/KingdomJobRegistry.z09.TableMutations.cs",
			"Simulation/City/KingdomJobRegistry.z10.RegistryFields.cs",
			"Simulation/City/KingdomJobRegistry.z11.RegistryNormalize.cs",
			"Simulation/City/KingdomJobRegistry.z12.RegistryPersistence.cs",
			"Simulation/City/KingdomJobRegistry.z13.RegistryHelpers.cs",
			"Simulation/City/KingdomJobRegistry.z14.WireFixture.cs",
			"Simulation/City/KingdomJobRegistry.z15.TableExact.cs"
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
