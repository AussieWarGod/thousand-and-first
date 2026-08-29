#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomConstructionInputCentralLogicalSource
	{
		internal static readonly string[] Paths = new string[]
		{
			"Simulation/City/KingdomCentralLogistics.09.ConstructionInputReservation.cs",
			"Simulation/City/KingdomCentralLogistics.10.ConstructionInputActivationAndTripView.cs",
			"Simulation/City/KingdomCentralLogistics.11.ConstructionInputArrivalAndAcknowledgements.cs",
			"Simulation/City/KingdomCentralLogistics.12.ConstructionInputRecovery.cs",
			"Simulation/City/KingdomCentralLogistics.13.ConstructionInputRouteProof.cs",
			"Simulation/City/KingdomCentralLogistics.18.ConstructionInputObservedRoute.cs",
			"Simulation/City/KingdomCentralLogistics.19.ConstructionInputTransitCustody.cs",
			"Simulation/City/KingdomCentralLogistics.20.ConstructionInputCancellationSource.cs",
			"Simulation/City/KingdomCentralLogistics.21.ConstructionInputRetirement.cs",
			"Simulation/City/KingdomCentralLogistics.22.ConstructionInputCancellationManifest.cs",
			"Simulation/City/KingdomCentralLogistics.23.ConstructionInputRootedPickup.cs",
			"Simulation/City/KingdomCentralLogistics.24.ConstructionInputCancellationTargetCut.cs",
			"Simulation/City/KingdomCentralLogistics.25.ConstructionInputPendingRetirement.cs",
			"Simulation/City/KingdomCentralLogistics.26.ConstructionInputExileBindingGate.cs",
			"Simulation/City/KingdomCentralLogistics.17.ConstructionInputOrphanRecovery.cs"
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
