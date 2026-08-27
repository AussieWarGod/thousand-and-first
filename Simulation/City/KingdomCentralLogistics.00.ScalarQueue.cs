using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		/// <summary>Queues one scalar demand against the nearest exact observed source/target. Open
		/// rows reserve source cargo and target room, preventing repeated check-ins from authorizing
		/// the same physical units twice.</summary>
		internal static bool TryQueueScalar(KingdomSystem system, KingdomCityState state,
			string destinationZoneId, KingdomStockKind kind, long demand, long room,
			long now, out int queued, out KingdomCityFault fault)
		{
			queued = 0;
			if (system == null || system.City == null || system.Jobs == null || state == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			KingdomJobTable table;
			if (!system.Jobs.TryRead(out table, out fault)) return false;
			long destinationReserved = 0L;
			for (int i = 0; i < table.Count; i++)
			{
				KingdomJobRow row;
				if (!table.TryAt(i, out row) || !KingdomJobRules.IsCentralDelivery(row)
					|| row.DeliveryCargoAuthority != KingdomDeliveryCargoAuthority.ScalarStock
					|| row.Cargo != kind || row.CargoAmount <= 0
					|| !string.Equals(row.DestZoneId, destinationZoneId,
						StringComparison.Ordinal)) continue;
				destinationReserved += row.CargoAmount;
			}
			demand -= destinationReserved;
			room -= destinationReserved;
			if (demand <= 0L || room <= 0L || table.Count >= KingdomJobRules.MaxOpenJobs)
			{
				fault = KingdomCityFault.None;
				return true;
			}
			KingdomDistanceTransferPlan plan;
			if (!KingdomDistanceRuntime.TryPlan(system.City, state, destinationZoneId, kind,
				demand, room, out plan, out fault)) return false;
			if (plan.Amount <= 0L)
			{
				fault = KingdomCityFault.None;
				return true;
			}
			long sourceReserved = 0L;
			long targetReserved = 0L;
			for (int i = 0; i < table.Count; i++)
			{
				KingdomJobRow row;
				if (!table.TryAt(i, out row) || !KingdomJobRules.IsCentralDelivery(row)
					|| row.DeliveryCargoAuthority != KingdomDeliveryCargoAuthority.ScalarStock
					|| row.Cargo != kind || row.CargoAmount <= 0) continue;
				if (row.DeliverySourceEndpointId == plan.HolderId
					&& string.Equals(row.DeliverySourceObjectId, plan.HolderObjectId,
						StringComparison.Ordinal)) sourceReserved += row.CargoAmount;
				if (row.DeliveryTargetEndpointId == plan.TargetId
					&& string.Equals(row.DeliveryTargetObjectId, plan.TargetObjectId,
						StringComparison.Ordinal)) targetReserved += row.CargoAmount;
			}
			long amount = plan.Amount - sourceReserved;
			long targetLeft = room - targetReserved;
			if (amount > targetLeft) amount = targetLeft;
			if (amount > KingdomLogisticsRules.CarrierCapacity)
				amount = KingdomLogisticsRules.CarrierCapacity;
			if (amount <= 0L)
			{
				fault = KingdomCityFault.None;
				return true;
			}
			KingdomZoneRow sourceZone;
			if (!state.TryZone(plan.SourceZoneIndex, out sourceZone))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			int jobId = system.Jobs.MintJobId();
			KingdomJobRow opened = new KingdomJobRow(jobId, KingdomJobKind.Delivery,
				kind, (int)amount, sourceZone.ZoneId, destinationZoneId, now,
				KingdomItineraryRules.WalkTicksPerCellDefault, KingdomJobStatus.Open, 0, 0,
				new KingdomLeg[0], 0, deliverySourceEndpointId: plan.HolderId,
				deliverySourceObjectId: plan.HolderObjectId,
				deliverySourceX: plan.SourceX, deliverySourceY: plan.SourceY,
				deliveryTargetEndpointId: plan.TargetId,
				deliveryTargetObjectId: plan.TargetObjectId,
				deliveryTargetX: plan.TargetX, deliveryTargetY: plan.TargetY,
				deliveryPhase: KingdomDeliveryPhase.Planned);
			KingdomJobTable next;
			if (!table.TryOpen(opened, out next, out fault)
				|| !system.Jobs.TryPublish(next, out fault)) return false;
			queued = (int)amount;
			return true;
		}
	}
}
