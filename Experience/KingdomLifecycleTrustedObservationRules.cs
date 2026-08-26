using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		internal static partial class TrustedAdapter
		{
			private static bool ExactSourceAt(Snapshot x, KingdomCarrySource source,
				KingdomLifecycleTopology topology, string ownerId, string zoneId, int px, int py)
			{
				return x != null && source != null
					&& string.Equals(x.ObjectId, source.ObjectId, StringComparison.Ordinal)
					&& string.Equals(x.Blueprint, source.Blueprint, StringComparison.Ordinal)
					&& x.Count == source.PlannedCount
					&& ExactTopology(x, topology, ownerId, zoneId, px, py);
			}

			private static void ClearPendingTransfer(KingdomCarrySource source)
			{
				if (source == null) return;
				source.PendingTransfer = KingdomCarryTransferKind.None;
				source.PendingTopology = KingdomLifecycleTopology.None;
				source.PendingOwnerId = null; source.PendingZoneId = null;
				source.PendingX = -1; source.PendingY = -1;
			}

			private static bool ValidCarryDestinationTarget(KingdomCarryOperation operation,
				KingdomLifecycleProjection output, bool lost,
				KingdomLifecycleTopology topology, string ownerId, string zoneId, int px, int py)
			{
				if (operation == null || output == null
					|| !TopologyValid(topology, ownerId, zoneId, px, py)) return false;
				if (lost) return topology == KingdomLifecycleTopology.Cell;
				bool target = topology == output.Topology
					&& string.Equals(ownerId, output.OwnerId, StringComparison.Ordinal)
					&& string.Equals(zoneId, output.ZoneId, StringComparison.Ordinal)
					&& px == output.X && py == output.Y;
				bool spill = topology == KingdomLifecycleTopology.Cell && ownerId == null
					&& string.Equals(zoneId, operation.SpillZoneId, StringComparison.Ordinal)
					&& px == operation.SpillX && py == operation.SpillY;
				return target || spill;
			}

			private static bool ExactLifecycleObjectFields(Snapshot x,
				KingdomLifecycleOperation operation, int count)
			{
				return x != null && operation != null
					&& string.Equals(x.ObjectId, operation.ObjectId, StringComparison.Ordinal)
					&& string.Equals(x.Blueprint, operation.Blueprint, StringComparison.Ordinal)
					&& x.Count == count && ExactTopology(x, operation.ObjectTopology,
						operation.ObjectOwnerId, operation.ZoneId,
						operation.ObjectX, operation.ObjectY);
			}

			private static bool ExactWaterFields(Snapshot x,
				KingdomLifecycleWaterLeg leg, long value)
			{
				return x != null && string.Equals(x.ObjectId, leg.OwnerId, StringComparison.Ordinal)
					&& string.Equals(x.Blueprint, leg.Blueprint, StringComparison.Ordinal)
					&& string.Equals(x.ZoneId, leg.ZoneId, StringComparison.Ordinal)
					&& x.Capacity == leg.Capacity && x.Value == value
					&& string.Equals(x.Composition, leg.Composition, StringComparison.Ordinal);
			}
		}
	}
}
