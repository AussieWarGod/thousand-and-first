#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomArchitectureStamperLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomArchitectureStamper.cs",
			"Growth/KingdomArchitectureStamper.Preflight.cs",
			"Growth/KingdomArchitectureStamper.UpgradePreflight.cs",
			"Growth/KingdomArchitectureStamper.StrikeAndRestake.cs",
			"Growth/KingdomArchitectureStamper.UpgradeApplication.cs",
			"Growth/KingdomArchitectureStamper.OwnerReceipts.cs",
			"Growth/KingdomArchitectureStamper.Staging.cs",
			"Growth/KingdomArchitectureStamper.StagingCustody.cs",
			"Growth/KingdomArchitectureStamper.Verification.cs",
			"Growth/KingdomArchitectureStamper.AnchoredLookup.cs",
			"Growth/KingdomArchitectureStamper.Components.cs",
			"Growth/KingdomArchitectureStamper.Transitions.cs",
			"Growth/KingdomArchitectureStamper.UpgradeReceipts.cs",
			"Growth/KingdomArchitectureStamper.Passability.cs",
			"Growth/KingdomArchitectureStamper.Recovery.cs"
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
