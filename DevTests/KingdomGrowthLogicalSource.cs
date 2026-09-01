#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomGrowthLogicalSource
	{
		private static readonly string[] Files =
		{
			"Growth/KingdomGrowth.cs",
			"Growth/KingdomGrowth.FirstGuestCapacity.cs",
			"Growth/KingdomGrowth.FirstGuestCompletion.cs",
			"Growth/KingdomGrowth.FirstGuestInteraction.cs",
			"Growth/KingdomGrowth.FirstGuestRecovery.cs",
			"Growth/KingdomGrowth.FirstGuestStart.cs",
			"Growth/KingdomGrowth.ArrivalCadence.cs",
			"Growth/KingdomGrowth.ArrivalStart.cs",
			"Growth/KingdomGrowth.z01.Activation.cs",
			"Growth/KingdomGrowth.z02.ScarcityHeartbeat.cs",
			"Growth/KingdomGrowth.z03.FoodAndHarvest.cs",
			"Growth/KingdomGrowth.z04.ArrivalEntryAndAuthority.cs",
			"Growth/KingdomGrowth.z05.ArrivalStartAndReconcile.cs",
			"Growth/KingdomGrowth.z06.ArrivalPreparation.cs",
			"Growth/KingdomGrowth.z07.ArrivalCompletion.cs",
			"Growth/KingdomGrowth.z08.ArrivalDomainsAndOutbox.cs",
			"Growth/KingdomGrowth.z09.ArrivalRetirementAndCandidate.cs",
			"Growth/KingdomGrowth.z10.ArrivalProofAndDomainHash.cs",
			"Growth/KingdomGrowth.z11.ArrivalGraphValidation.cs",
			"Growth/KingdomGrowth.z12.ArrivalGraphWriters.cs",
			"Growth/KingdomGrowth.z13.ConversationAndIdentityHashes.cs",
			"Growth/KingdomGrowth.z14.HashUtilities.cs",
			"Growth/KingdomGrowth.z15.WorkAssignment.cs",
			"Growth/KingdomGrowth.z16.Emigration.cs",
			"Growth/KingdomGrowth.z17.WaterAndCapacity.cs",
			"Growth/KingdomGrowth.z18.StageAndShops.cs",
			"Growth/KingdomGrowth.z18b.MarketOffice.cs",
			"Growth/KingdomGrowth.z18c.MarketProjection.cs",
			"Growth/KingdomResidentDepartureOperation.cs",
			"Growth/KingdomResidentDepartureRules.cs",
			"Growth/KingdomResidentDepartureRuntime.Authority.cs",
			"Growth/KingdomResidentDepartureRuntime.Begin.cs",
			"Growth/KingdomResidentDepartureRuntime.Effects.cs",
			"Growth/KingdomResidentDepartureRuntime.Recovery.cs",
			"Growth/KingdomResidentDepartureRuntime.Rollback.cs"
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
