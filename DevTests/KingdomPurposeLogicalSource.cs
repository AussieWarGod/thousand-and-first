#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomPurposeLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomPurpose.cs",
			"Growth/KingdomPurpose.00.CatalogueAndDispatch.cs",
			"Growth/KingdomPurpose.01.Transport.cs",
			"Growth/KingdomPurpose.02.Commitments.cs",
			"Growth/KingdomPurpose.03.CargoIdentityAndEscrow.cs",
			"Growth/KingdomPurpose.04.DeliveryAndLookup.cs",
			"Growth/KingdomPurpose.05.Siting.cs",
			"Growth/KingdomPurpose.06.PortfolioSiting.cs",
			"Growth/KingdomPurposeBodyAuthority.cs",
			"Growth/KingdomPurposePortfolio.RuntimeRegistry.cs",
			"Growth/KingdomPurposePortfolio.Lifecycle.cs",
			"Growth/KingdomPurposePortfolio.Open.cs",
			"Growth/KingdomPurposePortfolio.Interaction.cs",
			"Growth/KingdomPurposePortfolio.Pairing.cs",
			"Growth/KingdomPurposePortfolio.PairingHelpers.cs",
			"Growth/KingdomPurposePortfolio.SecondEndpoint.cs",
			"Growth/KingdomPurposePortfolio.ConstructionCargo.cs",
			"Growth/KingdomPurposePortfolio.Funding.cs",
			"Growth/KingdomPurposePortfolio.RuntimeLookup.cs",
			"Growth/KingdomPurposePortfolio.LocalPlan.cs",
			"Growth/KingdomPurposePortfolio.LocalDebitRuntime.cs",
			"Growth/KingdomPurposePortfolio.OperationControl.cs",
			"Growth/KingdomPurposePortfolio.BodyRuntime.cs",
			"Growth/KingdomPurposePortfolio.InputRuntime.cs",
			"Growth/KingdomPurposePortfolio.EffectRuntime.cs",
			"Growth/KingdomPurposePortfolio.EffectAttemptRecovery.cs",
			"Growth/KingdomPurposePortfolio.EffectDebit.cs",
			"Growth/KingdomPurposePortfolio.EffectDebitEvidence.cs",
			"Growth/KingdomPurposePortfolio.EffectDebitRetirement.cs",
			"Growth/KingdomPurposePortfolio.EffectDriveHelpers.cs",
			"Growth/KingdomPurposePortfolio.EffectGround.cs",
			"Growth/KingdomPurposePortfolio.EffectManualRuntime.cs",
			"Growth/KingdomPurposePortfolio.EffectPreflight.cs",
			"Growth/KingdomPurposePortfolio.EffectProductCensus.cs",
			"Growth/KingdomPurposePortfolio.EffectProductRuntime.cs",
			"Growth/KingdomPurposePortfolio.EffectProductShape.cs",
			"Growth/KingdomPurposePortfolio.EffectProse.cs",
			"Growth/KingdomPurposePortfolio.EffectRecord.cs",
			"Growth/KingdomPurposePortfolio.EffectRetirement.cs",
			"Growth/KingdomPurposePortfolio.EffectRoster.cs",
			"Growth/KingdomPurposePortfolio.OutputRuntime.cs",
			"Growth/KingdomPurposePortfolio.OperationDrive.cs",
			"Growth/KingdomPurposePortfolio.LandingFood.cs",
			"Growth/KingdomPurposePortfolio.LandingProof.cs",
			"Growth/KingdomPurposePortfolio.LandingRecord.cs",
			"Growth/KingdomPurposePortfolio.LandingGround.cs",
			"Growth/KingdomPurposePortfolio.CargoRoot.cs",
			"Growth/r_KingdomPurposeWork.cs"
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
