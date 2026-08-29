using System;

using XRL.World;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		/// <summary>Fresh central row, binding, exact carrier reference, and complete bounded
		/// phase-aware child graph. Every cancellation callback passes through this door.</summary>
		private static bool ExactCancellationCarrierManifest(KingdomSystem system, Zone zone,
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputCargoLine cargo, GameObject carrier)
		{
			int childOrdinal = ChildOrdinalForTrip(receipt, cargo?.ChildTripId ?? 0);
			return childOrdinal >= 0 && GameObject.Validate(carrier)
				&& KingdomCentralLogistics.TryInspectConstructionInputCancellationCarrier(
					system, job.Id, receipt.Schema, receipt.PlanDigest, receipt.Revision,
					cargo.ChildJobId, cargo.ChildTripId, job, receipt, childOrdinal, zone,
					out GameObject exact, out KingdomCityFault _)
				&& ReferenceEquals(exact, carrier);
		}
	}
}
