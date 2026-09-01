#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomExpeditionsLogicalSource
	{
		private static readonly string[] Paths =
		{
			"Experience/KingdomExpeditions.cs",
			"Experience/KingdomExpeditions.Dispatch.cs",
			"Experience/KingdomExpeditions.PassAndDeath.cs",
			"Experience/KingdomExpeditions.DispatchRecovery.cs",
			"Experience/KingdomExpeditions.Resolution.cs",
			"Experience/KingdomExpeditions.Discovery.cs",
			"Experience/KingdomExpeditions.ResidentsAndBodies.cs",
			"Experience/KingdomExpeditions.RewardsAndTelling.cs",
			"Experience/KingdomExpeditions.DebitReceipts.cs",
			"Experience/KingdomExpeditions.Polity.cs"
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
