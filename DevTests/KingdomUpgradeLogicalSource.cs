#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomUpgradeLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomUpgrade.00.r_KingdomImprovement.Declarations.cs",
			"Growth/KingdomUpgrade.01.r_KingdomImprovement.Liquid.cs",
			"Growth/KingdomUpgrade.01b.r_KingdomImprovement.LiquidReceipts.cs",
			"Growth/KingdomUpgrade.02.r_KingdomImprovement.Inventory.cs",
			"Growth/KingdomUpgrade.02a.r_KingdomImprovement.Manifest.cs",
			"Growth/KingdomUpgrade.02b.r_KingdomImprovement.ManifestVerification.cs",
			"Growth/KingdomUpgrade.02c.r_KingdomImprovement.LiquidCustody.cs",
			"Growth/KingdomUpgrade.02d.r_KingdomImprovement.ManifestCleanup.cs",
			"Growth/KingdomUpgrade.02e.r_KingdomImprovement.ItemCleanup.cs",
			"Growth/KingdomUpgrade.02f.r_KingdomImprovement.ItemTransfer.cs",
			"Growth/KingdomUpgrade.03.r_KingdomImprovement.PendingItems.cs",
			"Growth/KingdomUpgrade.04.r_KingdomImprovement.Escrow.cs",
			"Growth/KingdomUpgrade.05.r_KingdomImprovement.CodecAndDescription.cs",
			"Growth/KingdomUpgrade.06.r_KingdomImprovement.Poll.cs",
			"Growth/KingdomUpgrade.07.ConstructionRetry.cs",
			"Growth/KingdomUpgrade.08.ConstructionInspect.cs",
			"Growth/KingdomUpgrade.09.RegistryAndIdentity.cs",
			"Growth/KingdomUpgrade.10.Assessment.cs",
			"Growth/KingdomUpgrade.11.Absorption.cs",
			"Growth/KingdomUpgrade.12.ShelterAndActivation.cs",
			"Growth/KingdomUpgrade.13.Resolve.cs",
			"Growth/KingdomUpgrade.14.Begin.cs",
			"Growth/KingdomUpgrade.15.Prepare.cs",
			"Growth/KingdomUpgrade.16.PlanChange.cs",
			"Growth/KingdomUpgrade.17.Project.cs",
			"Growth/KingdomUpgrade.18.ProjectionProofs.cs",
			"Growth/KingdomUpgrade.19.ExactHandoverFailure.cs",
			"Growth/KingdomUpgrade.19.ForceAndOffer.cs",
			"Growth/KingdomUpgrade.20.HandOver.cs",
			"Growth/KingdomUpgrade.21.HandoverProofs.cs",
			"Growth/KingdomUpgrade.22.CarryMarks.cs",
			"Growth/KingdomUpgrade.23.Menu.cs",
			"Growth/KingdomUpgrade.24.HandoverContents.cs",
			"Growth/KingdomUpgrade.25.HandoverRemoval.cs",
			"Growth/KingdomUpgrade.cs"
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
