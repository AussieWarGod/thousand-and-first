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
		/// <summary>The registry as the frozen table the rules layer works on.</summary>
		internal bool TryRead(out KingdomJobTable table, out KingdomCityFault fault)
		{
			Normalize();
			KingdomJobRow[] rows = new KingdomJobRow[JobIds.Count];
			int at = 0;
			for (int i = 0; i < rows.Length; i++)
			{
				int count = LegCounts[i];
				KingdomLeg[] legs = new KingdomLeg[count];
				for (int j = 0; j < count; j++)
				{
					legs[j] = new KingdomLeg(
						LegZoneIds[at + j] ?? "",
						(short)LegEnterX[at + j], (short)LegEnterY[at + j],
						(short)LegExitX[at + j], (short)LegExitY[at + j],
						LegLengths[at + j], LegDepartTicks[at + j], LegArriveTicks[at + j]);
				}
				at += count;
				rows[i] = new KingdomJobRow(
					JobIds[i],
					KindOf(Kinds[i]),
					CargoOf(Cargos[i]),
					CargoAmounts[i],
					SourceZoneIds[i],
					DestZoneIds[i],
					StartTicks[i],
					WalkTicksPerCell[i],
					StatusOf(Statuses[i]),
					OriginCodes[i],
					DepositLegIndexes[i],
					legs,
					count,
					SubjectIds[i],
					SubjectNames[i],
					TargetNames[i],
					DueTicks[i],
					WaterCosts[i],
					ProvisionCosts[i],
					OutcomeCodes[i],
					DeliverySourceEndpointIds[i],
					DeliverySourceObjectIds[i],
					DeliverySourceXs[i],
					DeliverySourceYs[i],
					DeliveryTargetEndpointIds[i],
					DeliveryTargetObjectIds[i],
					DeliveryTargetXs[i],
					DeliveryTargetYs[i],
					DeliverySourceBeforeAmounts[i],
					DeliveryTripIds[i],
					DeliveryStopOrdinals[i],
					(KingdomDeliveryPhase)DeliveryPhases[i],
					(KingdomDeliveryCargoAuthority)DeliveryCargoAuthorityKinds[i],
					DeliveryOwnerOperationIds[i], DeliveryOwnerManifestVersions[i],
					DeliveryOwnerManifestDigests[i], DeliveryOwnerManifestRevisions[i],
					DeliveryManifestSourceStarts[i], DeliveryManifestSourceCounts[i],
					DeliveryTargetBeforeAmounts[i],
					(KingdomDeliveryTargetReceiptState)DeliveryTargetReceiptStates[i]);
			}
			return KingdomJobTable.TryCreate(rows, out table, out fault);
		}

		/// <summary>Writes one frozen table into the columns, in one call and after the rules have
		/// succeeded. The single publisher &sect;1.3 requires.</summary>
		internal bool TryPublish(KingdomJobTable table, out KingdomCityFault fault)
		{
			if (!CanPublish(table, out fault)) return false;
			PublishPrevalidated(table);
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>Copy-only publisher. Caller must pass the immutable table through
		/// <see cref="CanPublish"/> before any cross-owner publication starts.</summary>
		internal void PublishPrevalidated(KingdomJobTable table)
		{
			JobIds.Clear(); Kinds.Clear(); Cargos.Clear(); CargoAmounts.Clear();
			SourceZoneIds.Clear(); DestZoneIds.Clear(); StartTicks.Clear(); WalkTicksPerCell.Clear();
			Statuses.Clear(); OriginCodes.Clear(); DepositLegIndexes.Clear(); LegCounts.Clear();
			SubjectIds.Clear(); SubjectNames.Clear(); TargetNames.Clear(); DueTicks.Clear();
			WaterCosts.Clear(); ProvisionCosts.Clear(); OutcomeCodes.Clear();
			DeliverySourceEndpointIds.Clear(); DeliverySourceObjectIds.Clear();
			DeliverySourceXs.Clear(); DeliverySourceYs.Clear();
			DeliveryTargetEndpointIds.Clear(); DeliveryTargetObjectIds.Clear();
			DeliveryTargetXs.Clear(); DeliveryTargetYs.Clear();
			DeliverySourceBeforeAmounts.Clear(); DeliveryTripIds.Clear();
			DeliveryStopOrdinals.Clear(); DeliveryPhases.Clear();
			DeliveryCargoAuthorityKinds.Clear(); DeliveryOwnerOperationIds.Clear();
			DeliveryOwnerManifestVersions.Clear(); DeliveryOwnerManifestDigests.Clear();
			DeliveryOwnerManifestRevisions.Clear(); DeliveryManifestSourceStarts.Clear();
			DeliveryManifestSourceCounts.Clear(); DeliveryTargetBeforeAmounts.Clear();
			DeliveryTargetReceiptStates.Clear();
			LegZoneIds.Clear(); LegEnterX.Clear(); LegEnterY.Clear(); LegExitX.Clear();
			LegExitY.Clear(); LegLengths.Clear(); LegDepartTicks.Clear(); LegArriveTicks.Clear();
			for (int i = 0; i < table.Count; i++)
			{
				KingdomJobRow row;
				table.TryAt(i, out row);
				JobIds.Add(row.JobId);
				Kinds.Add((int)row.Kind);
				Cargos.Add((int)row.Cargo);
				CargoAmounts.Add(row.CargoAmount);
				SourceZoneIds.Add(row.SourceZoneId ?? "");
				DestZoneIds.Add(row.DestZoneId ?? "");
				StartTicks.Add(row.StartTick);
				WalkTicksPerCell.Add(row.WalkTicksPerCell);
				Statuses.Add((int)row.Status);
				OriginCodes.Add(row.OriginCode);
				DepositLegIndexes.Add(row.DepositLegIndex);
				SubjectIds.Add(row.SubjectId);
				SubjectNames.Add(row.SubjectName ?? "");
				TargetNames.Add(row.TargetName ?? "");
				DueTicks.Add(row.DueTick);
				WaterCosts.Add(row.WaterCost);
				ProvisionCosts.Add(row.ProvisionCost);
				OutcomeCodes.Add(row.OutcomeCode);
				DeliverySourceEndpointIds.Add(row.DeliverySourceEndpointId);
				DeliverySourceObjectIds.Add(row.DeliverySourceObjectId ?? "");
				DeliverySourceXs.Add(row.DeliverySourceX);
				DeliverySourceYs.Add(row.DeliverySourceY);
				DeliveryTargetEndpointIds.Add(row.DeliveryTargetEndpointId);
				DeliveryTargetObjectIds.Add(row.DeliveryTargetObjectId ?? "");
				DeliveryTargetXs.Add(row.DeliveryTargetX);
				DeliveryTargetYs.Add(row.DeliveryTargetY);
				DeliverySourceBeforeAmounts.Add(row.DeliverySourceBeforeAmount);
				DeliveryTripIds.Add(row.DeliveryTripId);
				DeliveryStopOrdinals.Add(row.DeliveryStopOrdinal);
				DeliveryPhases.Add((int)row.DeliveryPhase);
				DeliveryCargoAuthorityKinds.Add((int)row.DeliveryCargoAuthority);
				DeliveryOwnerOperationIds.Add(row.DeliveryOwnerOperationId ?? "");
				DeliveryOwnerManifestVersions.Add(row.DeliveryOwnerManifestVersion);
				DeliveryOwnerManifestDigests.Add(row.DeliveryOwnerManifestDigest ?? "");
				DeliveryOwnerManifestRevisions.Add(row.DeliveryOwnerManifestRevision);
				DeliveryManifestSourceStarts.Add(row.DeliveryManifestSourceStart);
				DeliveryManifestSourceCounts.Add(row.DeliveryManifestSourceCount);
				DeliveryTargetBeforeAmounts.Add(row.DeliveryTargetBeforeAmount);
				DeliveryTargetReceiptStates.Add((int)row.DeliveryTargetReceiptState);
				LegCounts.Add(row.LegCount);
				for (int j = 0; j < row.LegCount; j++)
				{
					KingdomLeg leg;
					row.TryLeg(j, out leg);
					LegZoneIds.Add(leg.ZoneId ?? "");
					LegEnterX.Add(leg.EnterX);
					LegEnterY.Add(leg.EnterY);
					LegExitX.Add(leg.ExitX);
					LegExitY.Add(leg.ExitY);
					LegLengths.Add(leg.PathLength);
					LegDepartTicks.Add(leg.DepartTick);
					LegArriveTicks.Add(leg.ArriveTick);
				}
			}
		}
	}
}
