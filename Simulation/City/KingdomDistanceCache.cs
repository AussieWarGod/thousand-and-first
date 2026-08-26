using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>One city's non-serialized §3.10 cache. Matrix geometry and holder observations
	/// disappear safely on cold load; a missing observation refuses nearest-holder planning rather
	/// than falling back to zone-row order and claiming locality.</summary>
	internal sealed class KingdomDistanceCache
	{
		internal ulong GraphA;

		internal ulong GraphB;

		internal KingdomDistanceMatrix Matrix;

		internal KingdomDistanceZoneCache[] Zones = new KingdomDistanceZoneCache[0];

		internal bool TryZone(int index, out KingdomDistanceZoneCache zone)
		{
			zone = null;
			if (index < 0 || index >= Zones.Length) return false;
			zone = Zones[index];
			return zone != null;
		}

		internal bool TryEndpoint(int zoneIndex, int endpointId,
			out KingdomDistanceEndpointState endpoint)
		{
			endpoint = default(KingdomDistanceEndpointState);
			KingdomDistanceZoneCache zone;
			if (!TryZone(zoneIndex, out zone)) return false;
			for (int i = 0; i < zone.Endpoints.Length; i++)
			{
				if (zone.Endpoints[i].EndpointId != endpointId) continue;
				endpoint = zone.Endpoints[i];
				return true;
			}
			return false;
		}

		internal bool TryEndpointAt(int zoneIndex, string objectId, int x, int y,
			out KingdomDistanceEndpointState endpoint)
		{
			endpoint = default(KingdomDistanceEndpointState);
			KingdomDistanceZoneCache zone;
			if (!TryZone(zoneIndex, out zone) || !zone.Observed) return false;
			for (int i = 0; i < zone.Endpoints.Length; i++)
			{
				KingdomDistanceEndpointState row = zone.Endpoints[i];
				if (row.X != x || row.Y != y || (!string.IsNullOrEmpty(objectId)
					&& !string.Equals(row.ObjectId, objectId, StringComparison.Ordinal))) continue;
				endpoint = row;
				return true;
			}
			return false;
		}

		internal bool TryPortal(int zoneIndex, KingdomZoneStep edge, out short x, out short y)
		{
			x = y = -1;
			KingdomDistanceZoneCache zone;
			int at = (int)edge;
			if (at < 0 || at >= KingdomDistanceRules.EdgesPerZone
				|| !TryZone(zoneIndex, out zone) || !zone.Observed
				|| zone.PortalX == null || zone.PortalY == null
				|| at >= zone.PortalX.Length || at >= zone.PortalY.Length
				|| zone.PortalX[at] < 0 || zone.PortalY[at] < 0) return false;
			x = zone.PortalX[at]; y = zone.PortalY[at];
			return true;
		}

		internal bool TryPortalPair(int zoneIndex, KingdomZoneStep enter,
			KingdomZoneStep exit, out int cells)
		{
			cells = 0;
			KingdomDistanceZoneCache zone;
			int a = (int)enter;
			int b = (int)exit;
			if (a < 0 || b < 0 || a >= KingdomDistanceRules.EdgesPerZone
				|| b >= KingdomDistanceRules.EdgesPerZone || !TryZone(zoneIndex, out zone)
				|| !zone.Observed || zone.PortalPairs == null) return false;
			int value = zone.PortalPairs[a * KingdomDistanceRules.EdgesPerZone + b];
			if (value >= KingdomDistanceRules.NoRoute) return false;
			cells = value;
			return true;
		}

		/// <summary>Prices the exact portal cells every frozen leg will use. The zone graph chooses
		/// topology; each traversed paved segment comes from its rendered slice, never a 40-cell proxy.</summary>
		internal bool TryCompose(int fromZone, int fromEndpoint, int toZone, int toEndpoint,
			out int cells, out KingdomCityFault fault)
		{
			cells = 0;
			if (Matrix == null) { fault = KingdomCityFault.OutsideItinerary; return false; }
			if (fromZone == toZone)
			{
				if (!Matrix.TrySameZone(fromZone, fromEndpoint, toEndpoint, out cells))
				{ fault = KingdomCityFault.OutsideItinerary; return false; }
				fault = KingdomCityFault.None;
				return true;
			}
			int[] path = new int[KingdomDistanceRules.MaxNodes];
			int count;
			if (!Matrix.Graph.TryPath(fromZone, toZone, path, out count, out fault)
				|| count < 2) return false;
			long total = 0L;
			for (int i = 0; i < count; i++)
			{
				KingdomZoneStep leaving = KingdomZoneStep.None;
				KingdomZoneStep arriving = KingdomZoneStep.None;
				if (i + 1 < count && !Matrix.Graph.TryStep(path[i], path[i + 1], out leaving))
				{ fault = KingdomCityFault.OutsideItinerary; return false; }
				if (i > 0 && !Matrix.Graph.TryStep(path[i], path[i - 1], out arriving))
				{ fault = KingdomCityFault.OutsideItinerary; return false; }
				int part;
				if (i == 0)
				{
					if (!Matrix.TryWorkToEdge(path[i], fromEndpoint, leaving, out part))
					{ fault = KingdomCityFault.OutsideItinerary; return false; }
				}
				else if (i == count - 1)
				{
					if (!Matrix.TryWorkToEdge(path[i], toEndpoint, arriving, out part))
					{ fault = KingdomCityFault.OutsideItinerary; return false; }
				}
				else if (!TryPortalPair(path[i], arriving, leaving, out part))
				{ fault = KingdomCityFault.OutsideItinerary; return false; }
				total += part;
				if (i + 1 < count) total += 1L;
				if (total >= KingdomDistanceRules.NoRoute)
				{ fault = KingdomCityFault.ArithmeticOverflow; return false; }
			}
			cells = (int)total;
			fault = KingdomCityFault.None;
			return true;
		}

		internal bool TrySpend(int zoneIndex, int endpointId, KingdomStockKind kind, long amount)
		{
			KingdomDistanceZoneCache zone;
			if (amount < 0L || !TryZone(zoneIndex, out zone)) return false;
			for (int i = 0; i < zone.Endpoints.Length; i++)
			{
				if (zone.Endpoints[i].EndpointId != endpointId) continue;
				KingdomDistanceEndpointState row = zone.Endpoints[i];
				long held = row.Amount(kind);
				if (amount > held) return false;
				if (kind == KingdomStockKind.Water) row.WaterAmount -= amount;
				else if (kind == KingdomStockKind.Food) row.FoodAmount -= amount;
				else return false;
				zone.Endpoints[i] = row;
				return true;
			}
			return false;
		}

		internal bool TryFill(int zoneIndex, int endpointId, KingdomStockKind kind, long amount)
		{
			KingdomDistanceZoneCache zone;
			if (amount < 0L || !TryZone(zoneIndex, out zone)) return false;
			for (int i = 0; i < zone.Endpoints.Length; i++)
			{
				if (zone.Endpoints[i].EndpointId != endpointId) continue;
				KingdomDistanceEndpointState row = zone.Endpoints[i];
				long room = row.Room(kind);
				if (amount > room) return false;
				if (kind == KingdomStockKind.Water)
				{
					row.WaterRoom -= amount;
					row.WaterAmount += amount;
				}
				else if (kind == KingdomStockKind.Food)
				{
					row.FoodRoom -= amount;
					row.FoodAmount += amount;
				}
				else return false;
				zone.Endpoints[i] = row;
				return true;
			}
			return false;
		}
	}
}
