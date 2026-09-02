#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomBountyLogicalSource
	{
		internal const int FileCount = 22;

		private static readonly string[] Paths =
		{
			"Quests/r_KingdomNotice.cs",
			"Quests/r_KingdomNotice.Serialization.cs",
			"Quests/KingdomBounty.cs",
			"Quests/KingdomBounty.NoticesAndWithdrawal.cs",
			"Quests/KingdomBounty.Posting.cs",
			"Quests/KingdomBounty.TargetSelection.cs",
			"Quests/KingdomBounty.ManningSelection.cs",
			"Quests/KingdomBounty.PassAndSchedule.cs",
			"Quests/KingdomBounty.Publication.cs",
			"Quests/KingdomBounty.Sinks.cs",
			"Quests/KingdomBounty.Cleanup.cs",
			"Quests/KingdomBounty.Schedule.cs",
			"Quests/KingdomBounty.Take.cs",
			"Quests/KingdomBounty.Manning.cs",
			"Quests/KingdomBounty.ManningOption.cs",
			"Quests/KingdomBounty.WorkAndCarry.cs",
			"Quests/KingdomBounty.Transfer.cs",
			"Quests/KingdomBounty.CompletionAndScouting.cs",
			"Quests/KingdomBounty.PaymentPlanning.cs",
			"Quests/KingdomBounty.PaymentObservation.cs",
			"Quests/KingdomBounty.Terminal.cs",
			"Quests/KingdomBounty.ReadingGround.cs"
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
