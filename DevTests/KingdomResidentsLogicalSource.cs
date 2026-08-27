#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomResidentsLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Simulation/City/KingdomResidents.cs",
			"Simulation/City/KingdomResidents.00.IdentityAndRoll.cs",
			"Simulation/City/KingdomResidents.01.BindingInspection.cs",
			"Simulation/City/KingdomResidents.02.BindingMutation.cs",
			"Simulation/City/KingdomResidents.03.RosterAndEnrollment.cs",
			"Simulation/City/KingdomResidents.04.ResidentTransitionsAndAccession.cs",
			"Simulation/City/KingdomResidents.05.AccessionRepair.cs",
			"Simulation/City/KingdomResidents.06.Helpers.cs"
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
