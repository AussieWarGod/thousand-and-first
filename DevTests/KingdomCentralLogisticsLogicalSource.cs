#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomCentralLogisticsLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Simulation/City/KingdomCentralLogistics.cs",
			"Simulation/City/KingdomCentralLogistics.00.ScalarQueue.cs",
			"Simulation/City/KingdomCentralLogistics.01.ScalarStartAndRecovery.cs",
			"Simulation/City/KingdomCentralLogistics.02.ScalarArrivalAndReceiptSweep.cs",
			"Simulation/City/KingdomCentralLogistics.03.ManifestPlanningAndReservation.cs",
			"Simulation/City/KingdomCentralLogistics.04.ManifestActivationAndTripView.cs",
			"Simulation/City/KingdomCentralLogistics.05.ManifestArrivalAndAcknowledgements.cs",
			"Simulation/City/KingdomCentralLogistics.06.ManifestOwnershipAndRoute.cs",
			"Simulation/City/KingdomCentralLogistics.07.RouteSegmentsAndPassages.cs",
			"Simulation/City/KingdomCentralLogistics.08.ScalarCustodyAndReceiptHelpers.cs",
			"Simulation/City/KingdomCentralLogistics.09.ConstructionInputReservation.cs",
			"Simulation/City/KingdomCentralLogistics.10.ConstructionInputActivationAndTripView.cs",
			"Simulation/City/KingdomCentralLogistics.11.ConstructionInputArrivalAndAcknowledgements.cs",
			"Simulation/City/KingdomCentralLogistics.12.ConstructionInputRecovery.cs",
			"Simulation/City/KingdomCentralLogistics.13.ConstructionInputRouteProof.cs",
			"Simulation/City/KingdomCentralLogistics.18.ConstructionInputObservedRoute.cs",
			"Simulation/City/KingdomCentralLogistics.19.ConstructionInputTransitCustody.cs"
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
