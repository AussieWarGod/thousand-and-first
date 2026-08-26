using System;

namespace ThousandAndFirst
{
	public sealed partial class KingdomRealmArchive
	{
		private static bool ValidDeliveryTrips(Simulation.City.KingdomJobRegistry value)
		{
			int jobs = value.JobIds.Count;
			int[] offsets = new int[jobs];
			int at = 0;
			for (int i = 0; i < jobs; i++) { offsets[i] = at; at += value.LegCounts[i]; }
			for (int i = 0; i < jobs; i++)
			{
				if (value.DeliveryPhases[i]
					<= (int)Simulation.City.KingdomDeliveryPhase.Planned) continue;
				int tripId = value.DeliveryTripIds[i];
				if (value.JobIds[i] != tripId) continue;
				int members = 0;
				for (int j = 0; j < jobs; j++)
					if (value.DeliveryTripIds[j] == tripId) members++;
				if (members <= 0 || members > Simulation.City.KingdomLogisticsRules.MaxStopsPerTrip)
					return false;
				long load = 0L;
				long priorArrival = -1L;
				string priorDestination = null;
				for (int ordinal = 1; ordinal <= members; ordinal++)
				{
					int found = -1;
					for (int j = 0; j < jobs; j++)
					{
						if (value.DeliveryTripIds[j] != tripId
							|| value.DeliveryStopOrdinals[j] != ordinal) continue;
						if (found >= 0) return false;
						found = j;
					}
					if (found < 0 || value.DeliveryPhases[found] != value.DeliveryPhases[i]
						|| value.DeliverySourceEndpointIds[found]
							!= value.DeliverySourceEndpointIds[i]
						|| !string.Equals(value.DeliverySourceObjectIds[found],
							value.DeliverySourceObjectIds[i], StringComparison.Ordinal)
						|| value.DeliverySourceXs[found] != value.DeliverySourceXs[i]
						|| value.DeliverySourceYs[found] != value.DeliverySourceYs[i]
						|| !string.Equals(value.SourceZoneIds[found], value.SourceZoneIds[i],
							StringComparison.Ordinal)
						|| value.Cargos[found] != value.Cargos[i]
						|| value.DeliverySourceBeforeAmounts[found]
							!= value.DeliverySourceBeforeAmounts[i]
						|| value.DeliveryCargoAuthorityKinds[found]
							!= value.DeliveryCargoAuthorityKinds[i]
						|| !string.Equals(value.DeliveryOwnerOperationIds[found],
							value.DeliveryOwnerOperationIds[i], StringComparison.Ordinal)
						|| value.DeliveryOwnerManifestVersions[found]
							!= value.DeliveryOwnerManifestVersions[i]
						|| !string.Equals(value.DeliveryOwnerManifestDigests[found],
							value.DeliveryOwnerManifestDigests[i], StringComparison.Ordinal)
						|| value.DeliveryOwnerManifestRevisions[found]
							!= value.DeliveryOwnerManifestRevisions[i]
						|| value.LegCounts[found] <= 0) return false;
					int first = offsets[found];
					int last = first + value.LegCounts[found] - 1;
					string expectedFirst = ordinal == 1 ? value.SourceZoneIds[found]
						: priorDestination;
					if (!string.Equals(value.LegZoneIds[first], expectedFirst,
							StringComparison.Ordinal)
						|| !string.Equals(value.LegZoneIds[last], value.DestZoneIds[found],
							StringComparison.Ordinal)
						|| (ordinal > 1 && value.LegDepartTicks[first] < priorArrival)) return false;
					load += value.DeliveryCargoAuthorityKinds[found]
						== (int)Simulation.City.KingdomDeliveryCargoAuthority.CarryBookManifest
						? value.DeliveryManifestSourceCounts[found] : value.CargoAmounts[found];
					if (load > Simulation.City.KingdomLogisticsRules.CarrierCapacity) return false;
					priorArrival = value.LegArriveTicks[last];
					priorDestination = value.DestZoneIds[found];
				}
				if (value.DeliveryStopOrdinals[i] != 1) return false;
			}
			for (int i = 0; i < jobs; i++)
			{
				if (value.DeliveryPhases[i]
					<= (int)Simulation.City.KingdomDeliveryPhase.Planned) continue;
				bool leader = false;
				for (int j = 0; j < jobs; j++)
					if (value.JobIds[j] == value.DeliveryTripIds[i]
						&& value.DeliveryTripIds[j] == value.DeliveryTripIds[i]
						&& value.DeliveryStopOrdinals[j] == 1) { leader = true; break; }
				if (!leader) return false;
			}
			for (int i = 0; i < jobs; i++)
			{
				if (value.DeliveryCargoAuthorityKinds[i]
					!= (int)Simulation.City.KingdomDeliveryCargoAuthority.CarryBookManifest) continue;
				long leftEnd = (long)value.DeliveryManifestSourceStarts[i]
					+ value.DeliveryManifestSourceCounts[i];
				for (int j = i + 1; j < jobs; j++)
				{
					if (value.DeliveryCargoAuthorityKinds[j]
						!= (int)Simulation.City.KingdomDeliveryCargoAuthority.CarryBookManifest
						|| !string.Equals(value.DeliveryOwnerOperationIds[i],
							value.DeliveryOwnerOperationIds[j], StringComparison.Ordinal)) continue;
					if (value.DeliveryOwnerManifestVersions[i]
							!= value.DeliveryOwnerManifestVersions[j]
						|| !string.Equals(value.DeliveryOwnerManifestDigests[i],
							value.DeliveryOwnerManifestDigests[j], StringComparison.Ordinal)) return false;
					long rightEnd = (long)value.DeliveryManifestSourceStarts[j]
						+ value.DeliveryManifestSourceCounts[j];
					if (leftEnd > int.MaxValue || rightEnd > int.MaxValue
						|| (value.DeliveryManifestSourceStarts[i] < rightEnd
							&& value.DeliveryManifestSourceStarts[j] < leftEnd)) return false;
				}
			}
			return true;
		}

	}
}
