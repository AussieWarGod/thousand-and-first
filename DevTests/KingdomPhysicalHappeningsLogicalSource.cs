#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomPhysicalHappeningsLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Simulation/City/KingdomPhysicalHappenings.cs",
			"Simulation/City/KingdomPhysicalHappenings.00.QueueOpenAndDrive.cs",
			"Simulation/City/KingdomPhysicalHappenings.01.PublishToldEffectAndSinks.cs",
			"Simulation/City/KingdomPhysicalHappenings.02.ObservePrepareAndUse.cs",
			"Simulation/City/KingdomPhysicalHappenings.03.RestoreAndParticipants.cs",
			"Simulation/City/KingdomPhysicalHappenings.04.FixturesCellsAndPathing.cs",
			"Simulation/City/KingdomPhysicalHappenings.05.BodyReceiptsAndProjectionRestore.cs",
			"Simulation/City/KingdomPhysicalHappenings.06.LookupAndPersistence.cs"
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
