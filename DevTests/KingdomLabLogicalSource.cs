#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomLabLogicalSource
	{
		private static readonly string[] Files =
		{
			"Growth/r_KingdomButcherSlab.cs",
			"Growth/r_KingdomVatHouse.cs",
			"Growth/r_KingdomGraftingHall.cs",
			"Growth/r_KingdomChimericTheatre.cs",
			"Growth/KingdomLabCivicEnums.cs",
			"Growth/KingdomLabCivicReceipt.cs",
			"Growth/KingdomLabCivicOwnerBook.cs",
			"Growth/KingdomLabCivicRules.Identity.cs",
			"Growth/KingdomLabCivicRules.Validation.cs",
			"Growth/KingdomLabCivicRules.Transitions.cs",
			"Growth/KingdomLabCivicRules.Prose.cs",
			"Growth/KingdomLabCivicOwnerRules.cs",
			"Growth/r_KingdomLabJob.cs",
			"Growth/r_KingdomLabRemovalJob.cs",
			"Growth/r_KingdomLabCivicFriction.cs",
			"Growth/KingdomLabCivicOwnership.cs",
			"Growth/KingdomLab.CivicSelection.cs",
			"Growth/KingdomLab.CivicRuntime.cs",
			"Growth/KingdomLab.CivicDepartureProjection.cs",
			"Growth/KingdomLab.CivicReconciliation.cs",
			"Growth/KingdomLab.CivicReceipts.cs",
			"Growth/KingdomLab.CivicInteraction.cs",
			"Growth/KingdomLab.cs",
			"Growth/KingdomLab.Governance.cs",
			"Growth/KingdomLab.Preparation.cs",
			"Growth/KingdomLab.VatAdvance.cs",
			"Growth/KingdomLab.VatProduction.cs",
			"Growth/KingdomLab.VatReceipts.cs",
			"Growth/KingdomLab.Slate.cs",
			"Growth/KingdomLab.Commission.cs",
			"Growth/KingdomLab.Semantic.cs",
			"Growth/KingdomLab.Funding.cs",
			"Growth/KingdomLab.Application.cs",
			"Growth/KingdomLab.ApplicationRecovery.cs",
			"Growth/KingdomLab.RemovalOffer.cs",
			"Growth/KingdomLab.RemovalFunding.cs",
			"Growth/KingdomLab.RemovalCompletion.cs",
			"Growth/KingdomLab.Candidates.cs",
			"Growth/KingdomLab.KeptSpend.cs",
			"Growth/KingdomLab.Lookup.cs"
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
