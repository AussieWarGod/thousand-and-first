using System;
using System.Collections.Generic;
#if TAF_TESTS
using System.IO;
using System.Text;
#endif

using ThousandAndFirst.Simulation.Kernel;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomJobRegistry
	{
		/// <summary>
		/// Repairs a registry read from a save written by an older build. Null columns become
		/// empty; ragged columns are truncated to the shortest; a duplicate or zero job id is
		/// dropped; a job whose declared legs are not all present is dropped whole, because half an
		/// itinerary is a carrier with no answer to where they are.
		/// </summary>
		public void Normalize()
		{
			JobIds = Repair(JobIds);
			Kinds = Repair(Kinds);
			Cargos = Repair(Cargos);
			CargoAmounts = Repair(CargoAmounts);
			SourceZoneIds = Repair(SourceZoneIds);
			DestZoneIds = Repair(DestZoneIds);
			StartTicks = Repair(StartTicks);
			WalkTicksPerCell = Repair(WalkTicksPerCell);
			Statuses = Repair(Statuses);
			OriginCodes = Repair(OriginCodes);
			DepositLegIndexes = Repair(DepositLegIndexes);
			SubjectIds = Repair(SubjectIds);
			SubjectNames = Repair(SubjectNames);
			TargetNames = Repair(TargetNames);
			DueTicks = Repair(DueTicks);
			WaterCosts = Repair(WaterCosts);
			ProvisionCosts = Repair(ProvisionCosts);
			OutcomeCodes = Repair(OutcomeCodes);
			ExpeditionDeedDispositions = Repair(ExpeditionDeedDispositions);
			ExpeditionDeedPolityIds = Repair(ExpeditionDeedPolityIds);
			ExpeditionDeedCauseRefs = Repair(ExpeditionDeedCauseRefs);
			ExpeditionDeedFigureRefs = Repair(ExpeditionDeedFigureRefs);
			DeliverySourceEndpointIds = Repair(DeliverySourceEndpointIds);
			DeliverySourceObjectIds = Repair(DeliverySourceObjectIds);
			DeliverySourceXs = Repair(DeliverySourceXs);
			DeliverySourceYs = Repair(DeliverySourceYs);
			DeliveryTargetEndpointIds = Repair(DeliveryTargetEndpointIds);
			DeliveryTargetObjectIds = Repair(DeliveryTargetObjectIds);
			DeliveryTargetXs = Repair(DeliveryTargetXs);
			DeliveryTargetYs = Repair(DeliveryTargetYs);
			DeliverySourceBeforeAmounts = Repair(DeliverySourceBeforeAmounts);
			DeliveryTripIds = Repair(DeliveryTripIds);
			DeliveryStopOrdinals = Repair(DeliveryStopOrdinals);
			DeliveryPhases = Repair(DeliveryPhases);
			DeliveryCargoAuthorityKinds = Repair(DeliveryCargoAuthorityKinds);
			DeliveryOwnerOperationIds = Repair(DeliveryOwnerOperationIds);
			DeliveryOwnerManifestVersions = Repair(DeliveryOwnerManifestVersions);
			DeliveryOwnerManifestDigests = Repair(DeliveryOwnerManifestDigests);
			DeliveryOwnerManifestRevisions = Repair(DeliveryOwnerManifestRevisions);
			DeliveryManifestSourceStarts = Repair(DeliveryManifestSourceStarts);
			DeliveryManifestSourceCounts = Repair(DeliveryManifestSourceCounts);
			DeliveryTargetBeforeAmounts = Repair(DeliveryTargetBeforeAmounts);
			DeliveryTargetReceiptStates = Repair(DeliveryTargetReceiptStates);
			LegCounts = Repair(LegCounts);
			LegZoneIds = Repair(LegZoneIds);
			LegEnterX = Repair(LegEnterX);
			LegEnterY = Repair(LegEnterY);
			LegExitX = Repair(LegExitX);
			LegExitY = Repair(LegExitY);
			LegLengths = Repair(LegLengths);
			LegDepartTicks = Repair(LegDepartTicks);
			LegArriveTicks = Repair(LegArriveTicks);
			if (JobCounter < 0)
			{
				JobCounter = 0;
			}

			int legacyJobs = Shortest(new int[]
			{
				JobIds.Count, Kinds.Count, Cargos.Count, CargoAmounts.Count, SourceZoneIds.Count,
				DestZoneIds.Count, StartTicks.Count, WalkTicksPerCell.Count, Statuses.Count,
				OriginCodes.Count, DepositLegIndexes.Count, LegCounts.Count
			});
			bool legacyMissionEnvelope = SubjectIds.Count == 0 && SubjectNames.Count == 0
				&& TargetNames.Count == 0 && DueTicks.Count == 0 && WaterCosts.Count == 0
				&& ProvisionCosts.Count == 0 && OutcomeCodes.Count == 0;
			if (legacyMissionEnvelope)
			{
				Pad(SubjectIds, legacyJobs, 0);
				Pad(SubjectNames, legacyJobs, "");
				Pad(TargetNames, legacyJobs, "");
				Pad(DueTicks, legacyJobs, 0L);
				Pad(WaterCosts, legacyJobs, 0);
				Pad(ProvisionCosts, legacyJobs, 0);
				Pad(OutcomeCodes, legacyJobs, 0);
			}
			int missionJobs = Shortest(new int[]
			{
				legacyJobs, SubjectIds.Count, SubjectNames.Count, TargetNames.Count, DueTicks.Count,
				WaterCosts.Count, ProvisionCosts.Count, OutcomeCodes.Count
			});
			bool legacyResultEnvelope = ExpeditionDeedDispositions.Count == 0
				&& ExpeditionDeedPolityIds.Count == 0 && ExpeditionDeedCauseRefs.Count == 0
				&& ExpeditionDeedFigureRefs.Count == 0;
			if (legacyResultEnvelope)
			{
				Pad(ExpeditionDeedDispositions, missionJobs, 0);
				Pad(ExpeditionDeedPolityIds, missionJobs, "");
				Pad(ExpeditionDeedCauseRefs, missionJobs, "");
				Pad(ExpeditionDeedFigureRefs, missionJobs, "");
			}
			int resultJobs = Shortest(new int[] { missionJobs,
				ExpeditionDeedDispositions.Count, ExpeditionDeedPolityIds.Count,
				ExpeditionDeedCauseRefs.Count, ExpeditionDeedFigureRefs.Count });
			bool legacyDeliveryEnvelope = DeliverySourceEndpointIds.Count == 0
				&& DeliverySourceObjectIds.Count == 0 && DeliveryTargetEndpointIds.Count == 0
				&& DeliverySourceXs.Count == 0 && DeliverySourceYs.Count == 0
				&& DeliveryTargetObjectIds.Count == 0 && DeliveryTargetXs.Count == 0
				&& DeliveryTargetYs.Count == 0 && DeliverySourceBeforeAmounts.Count == 0
				&& DeliveryTripIds.Count == 0 && DeliveryStopOrdinals.Count == 0
				&& DeliveryPhases.Count == 0 && DeliveryCargoAuthorityKinds.Count == 0
				&& DeliveryOwnerOperationIds.Count == 0
				&& DeliveryOwnerManifestVersions.Count == 0
				&& DeliveryOwnerManifestDigests.Count == 0
				&& DeliveryOwnerManifestRevisions.Count == 0
				&& DeliveryManifestSourceStarts.Count == 0
				&& DeliveryManifestSourceCounts.Count == 0
				&& DeliveryTargetBeforeAmounts.Count == 0
				&& DeliveryTargetReceiptStates.Count == 0;
			if (legacyDeliveryEnvelope)
			{
				Pad(DeliverySourceEndpointIds, resultJobs, 0);
				Pad(DeliverySourceObjectIds, resultJobs, "");
				Pad(DeliverySourceXs, resultJobs, -1); Pad(DeliverySourceYs, resultJobs, -1);
				Pad(DeliveryTargetEndpointIds, resultJobs, 0);
				Pad(DeliveryTargetObjectIds, resultJobs, "");
				Pad(DeliveryTargetXs, resultJobs, -1); Pad(DeliveryTargetYs, resultJobs, -1);
				Pad(DeliverySourceBeforeAmounts, resultJobs, 0L);
				Pad(DeliveryTripIds, resultJobs, 0); Pad(DeliveryStopOrdinals, resultJobs, 0);
				Pad(DeliveryPhases, resultJobs, 0); Pad(DeliveryCargoAuthorityKinds, resultJobs, 0);
				Pad(DeliveryOwnerOperationIds, resultJobs, "");
				Pad(DeliveryOwnerManifestVersions, resultJobs, 0);
				Pad(DeliveryOwnerManifestDigests, resultJobs, "");
				Pad(DeliveryOwnerManifestRevisions, resultJobs, 0L);
				Pad(DeliveryManifestSourceStarts, resultJobs, 0);
				Pad(DeliveryManifestSourceCounts, resultJobs, 0);
				Pad(DeliveryTargetBeforeAmounts, resultJobs, 0L);
				Pad(DeliveryTargetReceiptStates, resultJobs, 0);
			}
			int jobs = Shortest(new int[]
			{
				resultJobs, DeliverySourceEndpointIds.Count, DeliverySourceObjectIds.Count,
				DeliverySourceXs.Count, DeliverySourceYs.Count, DeliveryTargetEndpointIds.Count,
				DeliveryTargetObjectIds.Count, DeliveryTargetXs.Count, DeliveryTargetYs.Count,
				DeliverySourceBeforeAmounts.Count, DeliveryTripIds.Count,
				DeliveryStopOrdinals.Count, DeliveryPhases.Count,
				DeliveryCargoAuthorityKinds.Count, DeliveryOwnerOperationIds.Count,
				DeliveryOwnerManifestVersions.Count, DeliveryOwnerManifestDigests.Count,
				DeliveryOwnerManifestRevisions.Count, DeliveryManifestSourceStarts.Count,
				DeliveryManifestSourceCounts.Count, DeliveryTargetBeforeAmounts.Count,
				DeliveryTargetReceiptStates.Count
			});
			Trim(JobIds, jobs);
			Trim(Kinds, jobs);
			Trim(Cargos, jobs);
			Trim(CargoAmounts, jobs);
			Trim(SourceZoneIds, jobs);
			Trim(DestZoneIds, jobs);
			Trim(StartTicks, jobs);
			Trim(WalkTicksPerCell, jobs);
			Trim(Statuses, jobs);
			Trim(OriginCodes, jobs);
			Trim(DepositLegIndexes, jobs);
			Trim(SubjectIds, jobs);
			Trim(SubjectNames, jobs);
			Trim(TargetNames, jobs);
			Trim(DueTicks, jobs);
			Trim(WaterCosts, jobs);
			Trim(ProvisionCosts, jobs);
			Trim(OutcomeCodes, jobs);
			Trim(ExpeditionDeedDispositions, jobs);
			Trim(ExpeditionDeedPolityIds, jobs); Trim(ExpeditionDeedCauseRefs, jobs);
			Trim(ExpeditionDeedFigureRefs, jobs);
			Trim(DeliverySourceEndpointIds, jobs);
			Trim(DeliverySourceObjectIds, jobs);
			Trim(DeliverySourceXs, jobs);
			Trim(DeliverySourceYs, jobs);
			Trim(DeliveryTargetEndpointIds, jobs);
			Trim(DeliveryTargetObjectIds, jobs);
			Trim(DeliveryTargetXs, jobs);
			Trim(DeliveryTargetYs, jobs);
			Trim(DeliverySourceBeforeAmounts, jobs);
			Trim(DeliveryTripIds, jobs);
			Trim(DeliveryStopOrdinals, jobs);
			Trim(DeliveryPhases, jobs);
			Trim(DeliveryCargoAuthorityKinds, jobs);
			Trim(DeliveryOwnerOperationIds, jobs);
			Trim(DeliveryOwnerManifestVersions, jobs);
			Trim(DeliveryOwnerManifestDigests, jobs);
			Trim(DeliveryOwnerManifestRevisions, jobs);
			Trim(DeliveryManifestSourceStarts, jobs);
			Trim(DeliveryManifestSourceCounts, jobs);
			Trim(DeliveryTargetBeforeAmounts, jobs);
			Trim(DeliveryTargetReceiptStates, jobs);
			Trim(LegCounts, jobs);

			int legs = Shortest(new int[]
			{
				LegZoneIds.Count, LegEnterX.Count, LegEnterY.Count, LegExitX.Count, LegExitY.Count,
				LegLengths.Count, LegDepartTicks.Count, LegArriveTicks.Count
			});
			Trim(LegZoneIds, legs);
			Trim(LegEnterX, legs);
			Trim(LegEnterY, legs);
			Trim(LegExitX, legs);
			Trim(LegExitY, legs);
			Trim(LegLengths, legs);
			Trim(LegDepartTicks, legs);
			Trim(LegArriveTicks, legs);

			int consumed = 0;
			for (int i = 0; i < JobIds.Count; i++)
			{
				int count = LegCounts[i];
				if (count < 0 || count > KingdomItineraryRules.MaxLegs)
				{
					count = 0;
					LegCounts[i] = 0;
				}
				bool broken = JobIds[i] <= 0 || Duplicated(i) || consumed + count > legs;
				if (!broken)
				{
					if (SourceZoneIds[i] == null) { SourceZoneIds[i] = ""; }
					if (DestZoneIds[i] == null) { DestZoneIds[i] = ""; }
					if (StartTicks[i] < 0L) { StartTicks[i] = 0L; }
					if (SubjectNames[i] == null) { SubjectNames[i] = ""; }
					if (TargetNames[i] == null) { TargetNames[i] = ""; }
					if (SubjectIds[i] < 0) { SubjectIds[i] = 0; }
					if (DueTicks[i] < 0L) { DueTicks[i] = 0L; }
					if (WaterCosts[i] < 0) { WaterCosts[i] = 0; }
					if (ProvisionCosts[i] < 0) { ProvisionCosts[i] = 0; }
					if (OutcomeCodes[i] < 0) { OutcomeCodes[i] = 0; }
					if (ExpeditionDeedDispositions[i] < 0
						|| ExpeditionDeedDispositions[i] > 3) ExpeditionDeedDispositions[i] = 0;
					if (ExpeditionDeedPolityIds[i] == null) ExpeditionDeedPolityIds[i] = "";
					if (ExpeditionDeedCauseRefs[i] == null) ExpeditionDeedCauseRefs[i] = "";
					if (ExpeditionDeedFigureRefs[i] == null) ExpeditionDeedFigureRefs[i] = "";
					if (DeliverySourceObjectIds[i] == null) { DeliverySourceObjectIds[i] = ""; }
					if (DeliveryTargetObjectIds[i] == null) { DeliveryTargetObjectIds[i] = ""; }
					if (DeliverySourceEndpointIds[i] < 0) { DeliverySourceEndpointIds[i] = 0; }
					if (DeliveryTargetEndpointIds[i] < 0) { DeliveryTargetEndpointIds[i] = 0; }
					if (DeliverySourceBeforeAmounts[i] < 0L) { DeliverySourceBeforeAmounts[i] = 0L; }
					if (DeliveryTripIds[i] < 0) { DeliveryTripIds[i] = 0; }
					if (DeliveryStopOrdinals[i] < 0) { DeliveryStopOrdinals[i] = 0; }
					if (!KingdomJobRules.IsDeliveryPhase(DeliveryPhases[i])) { DeliveryPhases[i] = 0; }
					if (DeliveryCargoAuthorityKinds[i] < 0
						|| DeliveryCargoAuthorityKinds[i] > 2) { DeliveryCargoAuthorityKinds[i] = 0; }
					if (DeliveryOwnerOperationIds[i] == null) { DeliveryOwnerOperationIds[i] = ""; }
					if (DeliveryOwnerManifestVersions[i] < 0) { DeliveryOwnerManifestVersions[i] = 0; }
					if (DeliveryOwnerManifestDigests[i] == null) { DeliveryOwnerManifestDigests[i] = ""; }
					if (DeliveryOwnerManifestRevisions[i] < 0L) { DeliveryOwnerManifestRevisions[i] = 0L; }
					if (DeliveryManifestSourceStarts[i] < 0) { DeliveryManifestSourceStarts[i] = 0; }
					if (DeliveryManifestSourceCounts[i] < 0) { DeliveryManifestSourceCounts[i] = 0; }
					if (DeliveryTargetBeforeAmounts[i] < 0L) { DeliveryTargetBeforeAmounts[i] = 0L; }
					if (DeliveryTargetReceiptStates[i] < 0
						|| DeliveryTargetReceiptStates[i] > 1) { DeliveryTargetReceiptStates[i] = 0; }
					if (WalkTicksPerCell[i] <= 0) { WalkTicksPerCell[i] = KingdomItineraryRules.WalkTicksPerCellDefault; }
					if (DepositLegIndexes[i] < 0) { DepositLegIndexes[i] = 0; }
					consumed += count;
					continue;
				}
				RemoveLegs(consumed, count);
				RemoveJob(i);
				legs -= count;
				i--;
			}
			// Legs past the last job's share belong to no job and are dropped rather than kept for
			// a job that might be added later: an itinerary nobody claims is not an itinerary.
			if (LegZoneIds.Count > consumed)
			{
				RemoveLegs(consumed, LegZoneIds.Count - consumed);
			}
			DropOverCap();
		}
	}
}
