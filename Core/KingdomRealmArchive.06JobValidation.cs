using System;

namespace ThousandAndFirst
{
	public sealed partial class KingdomRealmArchive
	{
		private static bool ValidJobs(Simulation.City.KingdomJobRegistry Value)
		{
			if (Value == null || Value.JobCounter < 0 || Value.JobIds == null || Value.Kinds == null
				|| Value.Cargos == null || Value.CargoAmounts == null || Value.SourceZoneIds == null
				|| Value.DestZoneIds == null || Value.StartTicks == null || Value.WalkTicksPerCell == null
				|| Value.Statuses == null || Value.OriginCodes == null || Value.DepositLegIndexes == null
				|| Value.SubjectIds == null || Value.SubjectNames == null || Value.TargetNames == null
				|| Value.DueTicks == null || Value.WaterCosts == null || Value.ProvisionCosts == null
				|| Value.OutcomeCodes == null || Value.DeliverySourceEndpointIds == null
				|| Value.DeliverySourceObjectIds == null || Value.DeliverySourceXs == null
				|| Value.DeliverySourceYs == null || Value.DeliveryTargetEndpointIds == null
				|| Value.DeliveryTargetObjectIds == null || Value.DeliveryTargetXs == null
				|| Value.DeliveryTargetYs == null || Value.DeliverySourceBeforeAmounts == null
				|| Value.DeliveryTripIds == null || Value.DeliveryStopOrdinals == null
				|| Value.DeliveryPhases == null || Value.DeliveryCargoAuthorityKinds == null
				|| Value.DeliveryOwnerOperationIds == null
				|| Value.DeliveryOwnerManifestVersions == null
				|| Value.DeliveryOwnerManifestDigests == null
				|| Value.DeliveryOwnerManifestRevisions == null
				|| Value.DeliveryManifestSourceStarts == null
				|| Value.DeliveryManifestSourceCounts == null
				|| Value.DeliveryTargetBeforeAmounts == null
				|| Value.DeliveryTargetReceiptStates == null
				|| Value.LegCounts == null || Value.LegZoneIds == null || Value.LegEnterX == null
				|| Value.LegEnterY == null || Value.LegExitX == null || Value.LegExitY == null
				|| Value.LegLengths == null || Value.LegDepartTicks == null || Value.LegArriveTicks == null)
				return false;
			int jobs = Value.JobIds.Count;
			if (jobs > MaxJobs || Value.Kinds.Count != jobs || Value.Cargos.Count != jobs
				|| Value.CargoAmounts.Count != jobs || Value.SourceZoneIds.Count != jobs
				|| Value.DestZoneIds.Count != jobs || Value.StartTicks.Count != jobs
				|| Value.WalkTicksPerCell.Count != jobs || Value.Statuses.Count != jobs
				|| Value.OriginCodes.Count != jobs || Value.DepositLegIndexes.Count != jobs
				|| Value.SubjectIds.Count != jobs || Value.SubjectNames.Count != jobs
				|| Value.TargetNames.Count != jobs || Value.DueTicks.Count != jobs
				|| Value.WaterCosts.Count != jobs || Value.ProvisionCosts.Count != jobs
				|| Value.OutcomeCodes.Count != jobs
				|| Value.DeliverySourceEndpointIds.Count != jobs
				|| Value.DeliverySourceObjectIds.Count != jobs
				|| Value.DeliverySourceXs.Count != jobs || Value.DeliverySourceYs.Count != jobs
				|| Value.DeliveryTargetEndpointIds.Count != jobs
				|| Value.DeliveryTargetObjectIds.Count != jobs
				|| Value.DeliveryTargetXs.Count != jobs || Value.DeliveryTargetYs.Count != jobs
				|| Value.DeliverySourceBeforeAmounts.Count != jobs
				|| Value.DeliveryTripIds.Count != jobs
				|| Value.DeliveryStopOrdinals.Count != jobs
				|| Value.DeliveryPhases.Count != jobs
				|| Value.DeliveryCargoAuthorityKinds.Count != jobs
				|| Value.DeliveryOwnerOperationIds.Count != jobs
				|| Value.DeliveryOwnerManifestVersions.Count != jobs
				|| Value.DeliveryOwnerManifestDigests.Count != jobs
				|| Value.DeliveryOwnerManifestRevisions.Count != jobs
				|| Value.DeliveryManifestSourceStarts.Count != jobs
				|| Value.DeliveryManifestSourceCounts.Count != jobs
				|| Value.DeliveryTargetBeforeAmounts.Count != jobs
				|| Value.DeliveryTargetReceiptStates.Count != jobs
				|| Value.LegCounts.Count != jobs || !BoundedStrings(Value.SourceZoneIds, 512)
				|| !BoundedStrings(Value.DestZoneIds, 512)
				|| !BoundedStrings(Value.SubjectNames, 512)
				|| !BoundedStrings(Value.TargetNames, 512)
				|| !BoundedStrings(Value.DeliverySourceObjectIds, 512)
				|| !BoundedStrings(Value.DeliveryTargetObjectIds, 512)
				|| !BoundedStrings(Value.DeliveryOwnerOperationIds, 512)
				|| !BoundedStrings(Value.DeliveryOwnerManifestDigests, 512)) return false;
			int legs = 0;
			for (int i = 0; i < jobs; i++)
			{
				if (Value.LegCounts[i] < 0 || Value.LegCounts[i] > 6
					|| Value.SubjectIds[i] < 0 || Value.DueTicks[i] < 0L
					|| Value.WaterCosts[i] < 0 || Value.ProvisionCosts[i] < 0
					|| Value.OutcomeCodes[i] < 0 || Value.OutcomeCodes[i] > 7
					|| Value.DeliverySourceEndpointIds[i] < 0
					|| Value.DeliveryTargetEndpointIds[i] < 0
					|| Value.DeliverySourceXs[i] < -1
					|| Value.DeliverySourceXs[i] >= Simulation.City.KingdomJobRules.ZoneWidth
					|| Value.DeliverySourceYs[i] < -1
					|| Value.DeliverySourceYs[i] >= Simulation.City.KingdomJobRules.ZoneHeight
					|| Value.DeliveryTargetXs[i] < -1
					|| Value.DeliveryTargetXs[i] >= Simulation.City.KingdomJobRules.ZoneWidth
					|| Value.DeliveryTargetYs[i] < -1
					|| Value.DeliveryTargetYs[i] >= Simulation.City.KingdomJobRules.ZoneHeight
					|| Value.DeliverySourceBeforeAmounts[i] < 0L
					|| Value.DeliveryTripIds[i] < 0 || Value.DeliveryStopOrdinals[i] < 0
					|| Value.DeliveryCargoAuthorityKinds[i] < 0
					|| Value.DeliveryCargoAuthorityKinds[i] > 2
					|| Value.DeliveryOwnerManifestVersions[i] < 0
					|| Value.DeliveryOwnerManifestRevisions[i] < 0L
					|| Value.DeliveryManifestSourceStarts[i] < 0
					|| Value.DeliveryManifestSourceCounts[i] < 0
					|| Value.DeliveryTargetBeforeAmounts[i] < 0L
					|| Value.DeliveryTargetReceiptStates[i] < 0
					|| Value.DeliveryTargetReceiptStates[i] > 1
					|| !Simulation.City.KingdomJobRules.IsDeliveryPhase(
						Value.DeliveryPhases[i])) return false;
				bool expedition = Value.Kinds[i]
					== (int)Simulation.City.KingdomJobKind.Expedition;
				if (expedition)
				{
					if (Value.SubjectIds[i] <= 0 || string.IsNullOrEmpty(Value.SubjectNames[i])
						|| string.IsNullOrEmpty(Value.TargetNames[i])
						|| Value.DueTicks[i] <= Value.StartTicks[i]
						|| Value.WaterCosts[i] <= 0
						|| Value.WaterCosts[i] > Simulation.City.KingdomJobRules.MaxExpeditionWaterCost
						|| Value.ProvisionCosts[i] <= 0
						|| Value.ProvisionCosts[i] > Simulation.City.KingdomJobRules.MaxExpeditionProvisionCost
						|| !Simulation.City.KingdomJobRules.ValidExpeditionOutcomeForPhase(
							Value.OriginCodes[i], Value.OutcomeCodes[i])
						|| !Simulation.City.KingdomJobRules.IsExpeditionPhase(
							Value.OriginCodes[i])) return false;
					for (int j = 0; j < i; j++)
						if (Value.Kinds[j] == (int)Simulation.City.KingdomJobKind.Expedition
							&& Value.SubjectIds[j] == Value.SubjectIds[i]) return false;
				}
				else if (Value.SubjectIds[i] != 0 || !string.IsNullOrEmpty(Value.SubjectNames[i])
					|| !string.IsNullOrEmpty(Value.TargetNames[i]) || Value.DueTicks[i] != 0L
					|| Value.WaterCosts[i] != 0 || Value.ProvisionCosts[i] != 0
					|| Value.OutcomeCodes[i] != 0) return false;
				bool delivery = Value.Kinds[i]
					== (int)Simulation.City.KingdomJobKind.Delivery;
				bool central = delivery && Value.DeliveryPhases[i]
					!= (int)Simulation.City.KingdomDeliveryPhase.Legacy;
				bool neutralDelivery = Value.DeliverySourceEndpointIds[i] == 0
					&& string.IsNullOrEmpty(Value.DeliverySourceObjectIds[i])
					&& Value.DeliverySourceXs[i] == -1 && Value.DeliverySourceYs[i] == -1
					&& Value.DeliveryTargetEndpointIds[i] == 0
					&& string.IsNullOrEmpty(Value.DeliveryTargetObjectIds[i])
					&& Value.DeliveryTargetXs[i] == -1 && Value.DeliveryTargetYs[i] == -1
					&& Value.DeliverySourceBeforeAmounts[i] == 0L
					&& Value.DeliveryTripIds[i] == 0 && Value.DeliveryStopOrdinals[i] == 0
					&& Value.DeliveryPhases[i] == 0
					&& Value.DeliveryCargoAuthorityKinds[i] == 0
					&& string.IsNullOrEmpty(Value.DeliveryOwnerOperationIds[i])
					&& Value.DeliveryOwnerManifestVersions[i] == 0
					&& string.IsNullOrEmpty(Value.DeliveryOwnerManifestDigests[i])
					&& Value.DeliveryOwnerManifestRevisions[i] == 0L
					&& Value.DeliveryManifestSourceStarts[i] == 0
					&& Value.DeliveryManifestSourceCounts[i] == 0
					&& Value.DeliveryTargetBeforeAmounts[i] == 0L
					&& Value.DeliveryTargetReceiptStates[i] == 0;
				if (!central && !neutralDelivery) return false;
				if (central && (Value.DeliverySourceEndpointIds[i] <= 0
					|| Value.DeliveryTargetEndpointIds[i] <= 0
					|| Value.DeliverySourceXs[i] < 0 || Value.DeliverySourceYs[i] < 0
					|| Value.DeliveryTargetXs[i] < 0 || Value.DeliveryTargetYs[i] < 0
					|| Value.CargoAmounts[i] < 0)) return false;
				bool scalar = central && Value.DeliveryCargoAuthorityKinds[i]
					== (int)Simulation.City.KingdomDeliveryCargoAuthority.ScalarStock;
				bool manifest = central && Value.DeliveryCargoAuthorityKinds[i]
					== (int)Simulation.City.KingdomDeliveryCargoAuthority.CarryBookManifest;
				bool construction = central && Value.DeliveryCargoAuthorityKinds[i]
					== (int)Simulation.City.KingdomDeliveryCargoAuthority.ConstructionInput;
				if (central && Value.DeliveryPhases[i]
						== (int)Simulation.City.KingdomDeliveryPhase.LandedAwaitingOwner
					&& !construction) return false;
				if (scalar && (Value.Cargos[i] != (int)Simulation.City.KingdomStockKind.Water
					&& Value.Cargos[i] != (int)Simulation.City.KingdomStockKind.Food)) return false;
				if (scalar && (string.IsNullOrEmpty(Value.DeliverySourceObjectIds[i])
					|| string.IsNullOrEmpty(Value.DeliveryTargetObjectIds[i]))) return false;
				if (scalar && (!string.IsNullOrEmpty(Value.DeliveryOwnerOperationIds[i])
					|| Value.DeliveryOwnerManifestVersions[i] != 0
					|| !string.IsNullOrEmpty(Value.DeliveryOwnerManifestDigests[i])
					|| Value.DeliveryOwnerManifestRevisions[i] != 0L
					|| Value.DeliveryManifestSourceStarts[i] != 0
					|| Value.DeliveryManifestSourceCounts[i] != 0)) return false;
				bool reservation = manifest && (Value.DeliveryPhases[i]
					== (int)Simulation.City.KingdomDeliveryPhase.ReservationPrepared
					|| Value.DeliveryPhases[i]
						== (int)Simulation.City.KingdomDeliveryPhase.Quarantined);
				if (manifest && (Value.Cargos[i] != (int)Simulation.City.KingdomStockKind.OpaqueManifest
					|| string.IsNullOrEmpty(Value.DeliveryOwnerOperationIds[i])
					|| Value.DeliveryManifestSourceCounts[i] <= 0
					|| Value.DeliveryManifestSourceCounts[i]
						> Simulation.City.KingdomLogisticsRules.CarrierCapacity
					|| Value.CargoAmounts[i] != Value.DeliveryManifestSourceCounts[i]
					|| Value.DeliverySourceBeforeAmounts[i] != 0L
					|| Value.DeliveryTargetBeforeAmounts[i] != 0L
					|| Value.DeliveryTargetReceiptStates[i] != 0
					|| (reservation && (Value.DeliveryOwnerManifestVersions[i] != 0
						|| !string.IsNullOrEmpty(Value.DeliveryOwnerManifestDigests[i])
						|| Value.DeliveryOwnerManifestRevisions[i] != 0L))
					|| (!reservation && (Value.DeliveryOwnerManifestVersions[i] <= 0
						|| string.IsNullOrEmpty(Value.DeliveryOwnerManifestDigests[i]))))) return false;
				bool constructionNeutral = Value.DeliveryOwnerManifestVersions[i] == 0
					&& string.IsNullOrEmpty(Value.DeliveryOwnerManifestDigests[i])
					&& Value.DeliveryOwnerManifestRevisions[i] == 0L;
				bool constructionBound = Value.DeliveryOwnerManifestVersions[i] > 0
					&& !string.IsNullOrEmpty(Value.DeliveryOwnerManifestDigests[i])
					&& Value.DeliveryOwnerManifestRevisions[i] >= 0L;
				bool constructionReservation = Value.DeliveryPhases[i]
					== (int)Simulation.City.KingdomDeliveryPhase.ReservationPrepared;
				bool constructionQuarantine = Value.DeliveryPhases[i]
					== (int)Simulation.City.KingdomDeliveryPhase.Quarantined;
				if (construction && (Value.Cargos[i]
						!= (int)Simulation.City.KingdomStockKind.OpaqueManifest
					|| string.IsNullOrEmpty(Value.DeliveryOwnerOperationIds[i])
					|| Value.DeliveryTargetBeforeAmounts[i] != 0L
					|| Value.DeliveryTargetReceiptStates[i] != 0
					|| Value.DeliveryPhases[i] == (int)Simulation.City.KingdomDeliveryPhase.Planned
					|| Value.DeliveryManifestSourceStarts[i] < 0
					|| Value.DeliveryManifestSourceCounts[i] <= 0
					|| Value.DeliveryManifestSourceCounts[i]
						> Simulation.City.KingdomLogisticsRules.CarrierCapacity
					|| Value.CargoAmounts[i] != Value.DeliveryManifestSourceCounts[i]
					|| Value.DeliverySourceBeforeAmounts[i] != 0L
					|| (constructionReservation && !constructionNeutral)
					|| (!constructionReservation && !constructionQuarantine
						&& !constructionBound)
					|| (constructionQuarantine && !constructionNeutral
						&& !constructionBound))) return false;
				if (central && !scalar && !manifest && !construction) return false;
				if (central && Value.DeliveryPhases[i]
					== (int)Simulation.City.KingdomDeliveryPhase.Planned)
				{
					if (!scalar || Value.CargoAmounts[i] <= 0
						|| Value.DeliverySourceBeforeAmounts[i] != 0L
						|| Value.DeliveryTripIds[i] != 0 || Value.DeliveryStopOrdinals[i] != 0
						|| Value.LegCounts[i] != 0 || Value.DeliveryTargetBeforeAmounts[i] != 0L
						|| Value.DeliveryTargetReceiptStates[i] != 0) return false;
				}
				else if (central && ((scalar && Value.DeliverySourceBeforeAmounts[i] <= 0L)
					|| Value.DeliveryTripIds[i] <= 0 || Value.DeliveryStopOrdinals[i] <= 0
					|| Value.DeliveryStopOrdinals[i]
						> Simulation.City.KingdomLogisticsRules.MaxStopsPerTrip
					|| Value.LegCounts[i] <= 0
					|| (Value.DeliveryPhases[i]
						== (int)Simulation.City.KingdomDeliveryPhase.SourceDebitPrepared
						&& Value.DeliveryTargetReceiptStates[i] != 0)
					|| (scalar && Value.CargoAmounts[i] == 0
						&& Value.DeliveryTargetReceiptStates[i]
							!= (int)Simulation.City.KingdomDeliveryTargetReceiptState.Prepared))) return false;
				legs += Value.LegCounts[i];
			}
			bool legShape = legs <= MaxLegs && Value.LegZoneIds.Count == legs
				&& Value.LegEnterX.Count == legs && Value.LegEnterY.Count == legs
				&& Value.LegExitX.Count == legs && Value.LegExitY.Count == legs
				&& Value.LegLengths.Count == legs && Value.LegDepartTicks.Count == legs
				&& Value.LegArriveTicks.Count == legs && BoundedStrings(Value.LegZoneIds, 512);
			return legShape && ValidDeliveryTrips(Value);
		}

	}
}
