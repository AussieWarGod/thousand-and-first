#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomPetitionLifecycleLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Quests/KingdomPetitionLifecycle.cs",
			"Quests/KingdomPetitionLifecycle.Publication.cs",
			"Quests/KingdomPetitionLifecycle.Recovery.cs",
			"Quests/KingdomPetitionLifecycle.SnapshotAndOutbox.cs",
			"Quests/KingdomPetitionLifecycle.Projection.cs"
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
