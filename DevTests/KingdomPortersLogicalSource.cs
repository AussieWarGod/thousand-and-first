#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomPortersLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Simulation/City/KingdomPorters.cs",
			"Simulation/City/KingdomPorters.00.Opening.cs",
			"Simulation/City/KingdomPorters.01.RenderingSteppingAndRetirement.cs",
			"Simulation/City/KingdomPorters.02.CarrierRendering.cs",
			"Simulation/City/KingdomPorters.03.ClosingAndCustodyHandoff.cs",
			"Simulation/City/KingdomPorters.04.RoutePlanning.cs",
			"Simulation/City/KingdomPorters.05.CargoAndMovementHelpers.cs"
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
