using System;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		/// <summary>Refuses release of an exact store endpoint while any durable delivery row
		/// still names it. The caller changes no designation when this read is unavailable.</summary>
		internal static bool TryCanReleaseDesignation(KingdomSystem system, string objectId,
			out bool canRelease, out KingdomCityFault fault)
		{
			canRelease = false;
			fault = KingdomCityFault.NullArgument;
			if (system == null || system.Jobs == null || string.IsNullOrEmpty(objectId))
				return false;
			KingdomJobTable table;
			if (!system.Jobs.TryRead(out table, out fault)) return false;
			for (int i = 0; i < table.Count; i++)
			{
				KingdomJobRow row;
				if (!table.TryAt(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				if (row.Kind == KingdomJobKind.Delivery
					&& (string.Equals(row.DeliverySourceObjectId, objectId,
							StringComparison.Ordinal)
						|| string.Equals(row.DeliveryTargetObjectId, objectId,
							StringComparison.Ordinal)))
				{
					fault = KingdomCityFault.None;
					return true;
				}
			}
			canRelease = true;
			fault = KingdomCityFault.None;
			return true;
		}
	}
}
