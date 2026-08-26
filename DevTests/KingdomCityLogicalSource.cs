#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomCityLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Simulation/City/KingdomCity.cs",
			"Simulation/City/KingdomCity.z01.CheckIn.cs",
			"Simulation/City/KingdomCity.z02.CheckOut.cs",
			"Simulation/City/KingdomCity.z03.Sightings.cs",
			"Simulation/City/KingdomCity.z04.ReckonAndSpend.cs",
			"Simulation/City/KingdomCity.z05.Reify.cs",
			"Simulation/City/KingdomCity.z06.PlacementAndContainers.cs",
			"Simulation/City/KingdomCity.z07.BudgetsAndNetworks.cs",
			"Simulation/City/KingdomCity.z08.CarryAndReconcile.cs",
			"Simulation/City/KingdomCity.z09.WorksAndAudit.cs",
			"Simulation/City/KingdomCity.z10.StateAndPublish.cs",
			"Simulation/City/KingdomCity.z11.DedicationAndHelpers.cs"
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
