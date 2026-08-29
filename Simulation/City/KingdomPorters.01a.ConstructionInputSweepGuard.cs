using System.Collections.Generic;

using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomPorters
	{
		/// <summary>The generic stale sweep has no licence over a body named by any durable
		/// construction row or parent child. Unreadable authority and routed markers are
		/// protected too: ambiguity is a no-write result, never permission to spill.</summary>
		private static bool ConstructionInputSweepProtected(KingdomSystem system,
			GameObject body)
		{
			if (!KingdomOrdinaryCustody.TryCollect(body,
				out List<GameObject> graph, out string _)) return true;
			for (int i = 0; i < graph.Count; i++)
				if (graph[i].HasStringProperty(KingdomConstruction.InputMarkerProperty)
					|| graph[i].HasIntProperty(KingdomConstruction.InputMarkerProperty))
					return true;
			int propertyTrip = body.GetIntProperty(KingdomResidents.JobIdProperty);
			int partTrip = body.GetPart<r_KingdomPorter>()?.JobId ?? 0;
			if (propertyTrip != partTrip && (propertyTrip != 0 || partTrip != 0)) return true;
			int tripId = propertyTrip;
			if (tripId <= 0) return false;
			if (system?.Jobs == null
				|| !system.Jobs.TryRead(out KingdomJobTable table, out KingdomCityFault _))
				return true;
			for (int i = 0; i < table.Count; i++)
			{
				if (!table.TryAt(i, out KingdomJobRow row)) return true;
				if (row.DeliveryCargoAuthority
						== KingdomDeliveryCargoAuthority.ConstructionInput
					&& (row.JobId == tripId || row.DeliveryTripId == tripId)) return true;
			}

			if (!KingdomConstruction.TryRead(out List<KingdomConstructionJob> parents,
				out string _)) return true;
			for (int i = 0; i < parents.Count; i++)
			{
				KingdomConstructionJob parent = parents[i];
				if (string.IsNullOrEmpty(parent.InputReceipt)) continue;
				if (!KingdomConstructionRules.TryGetInputReceipt(parent,
					out KingdomConstructionInputReceipt receipt)) return true;
				for (int j = 0; j < receipt.ChildCount; j++)
				{
					KingdomConstructionInputChild child = receipt.ChildAt(j);
					if (child.JobId == tripId || child.TripId == tripId) return true;
				}
			}
			return false;
		}
	}
}
