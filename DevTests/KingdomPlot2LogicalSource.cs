#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomPlot2LogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomPlot2.00.r_KingdomYielding.cs",
			"Growth/KingdomPlot2.01.r_KingdomPlotWorks.cs",
			"Growth/KingdomPlot2.02.KingdomPlotQuote.cs",
			"Growth/KingdomPlot2.03.RegistryAndDeclarations.cs",
			"Growth/KingdomPlot2.04.Ground.cs",
			"Growth/KingdomPlot2.05.NestedDeclarations.cs",
			"Growth/KingdomPlot2.06.Geometry.cs",
			"Growth/KingdomPlot2.06b.PendingReservations.cs",
			"Growth/KingdomPlot2.07.HeartGeometry.cs",
			"Growth/KingdomPlot2.07a.FoundingHeartAuthority.cs",
			"Growth/KingdomPlot2.07b.FoundingHeartIdentity.cs",
			"Growth/KingdomPlot2.07c.FoundingHeartMarks.cs",
			"Growth/KingdomPlot2.07d.FoundingHeartWorks.cs",
			"Growth/KingdomPlot2.07e.FoundingHeartLegacy.cs",
			"Growth/KingdomPlot2.07f.FoundingHeartStakeTruth.cs",
			"Growth/KingdomPlot2.07g.FoundingHeartCustody.cs",
			"Growth/KingdomPlot2.07h.FoundingHeartSeal.cs",
			"Growth/KingdomPlot2.07i.FoundingHeartTerminalAuthority.cs",
			"Growth/KingdomPlot2.07j.FoundingHeartTerminalDrive.cs",
			"Growth/KingdomPlot2.07k.FoundingHeartTerminalSettlement.cs",
			"Growth/KingdomPlot2.07l.FoundingHeartReservations.cs",
			"Growth/KingdomPlot2.07m.FoundingHeartTombstones.cs",
			"Growth/KingdomPlot2.08.Siting.cs",
			"Growth/KingdomPlot2.09.CommissionQuote.cs",
			"Growth/KingdomPlot2.10.Commission.cs",
			"Growth/KingdomPlot2.11.Stake.cs",
			"Growth/KingdomPlot2.11a.StakeAdd.cs",
			"Growth/KingdomPlot2.12.Projection.cs",
			"Growth/KingdomPlot2.13.PayloadCodec.cs",
			"Growth/KingdomPlot2.14.PayloadLegacy.cs",
			"Growth/KingdomPlot2.15.RecoveryRetry.cs",
			"Growth/KingdomPlot2.16.RecoveryInspect.cs",
			"Growth/KingdomPlot2.17.PlanQuote.cs",
			"Growth/KingdomPlot2.18.PlanValidation.cs",
			"Growth/KingdomPlot2.19.PlanStaking.cs",
			"Growth/KingdomPlot2.19b.PlanRemovalProof.cs",
			"Growth/KingdomPlot2.20.Adoption.cs",
			"Growth/KingdomPlot2.20b.AuthoredGrowthReservation.cs",
			"Growth/KingdomPlot2.21.GrowthRules.cs",
			"Growth/KingdomPlot2.22.Growth.cs",
			"Growth/KingdomPlot2.23.GrowthPlanning.cs",
			"Growth/KingdomPlot2.24.GrowthProofs.cs",
			"Growth/KingdomPlot2.25.GrowthCodec.cs",
			"Growth/KingdomPlot2.26.Labour.cs",
			"Growth/KingdomPlot2.26b.LabourWindow.cs",
			"Growth/KingdomPlot2.27.FinalBuilding.cs",
			"Growth/KingdomPlot2.28.ClearPayout.cs",
			"Growth/KingdomPlot2.29.ClearProofs.cs",
			"Growth/KingdomPlot2.29b.BuildingShell.cs",
			"Growth/KingdomPlot2.30.Finish.cs",
			"Growth/KingdomPlot2.31.FinishOutput.cs",
			"Growth/KingdomPlot2.31b.FinishOutputCustody.cs",
			"Growth/KingdomPlot2.32.FinishRemoval.cs",
			"Growth/KingdomPlot2.32b.FinishRemovalRecovery.cs",
			"Growth/KingdomPlot2.33.FinishEffects.cs",
			"Growth/KingdomPlot2.33b.LegacyEffects.cs",
			"Growth/KingdomPlot2.34.EffectsAndFurnishing.cs",
			"Growth/KingdomPlot2.35.FurnishCodec.cs",
			"Growth/KingdomPlot2.cs"
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
