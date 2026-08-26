using System;

namespace ThousandAndFirst.Simulation.City
{
	internal sealed partial class KingdomDistanceMatrix
	{

		/// <summary>Whether this zone's level-2 slice needs recomputing before it can be
		/// believed.</summary>
		internal bool IsDirty(int zoneIndex)
		{
			return zoneIndex < 0 || zoneIndex >= dirty.Length || dirty[zoneIndex];
		}

		/// <summary>
		/// Marks one zone's slice stale. The only three callers &sect;3.10(2) permits are work
		/// placement, work removal, and a road change &mdash; never a clock and never a stock
		/// level.
		/// </summary>
		internal void MarkDirty(string zoneId)
		{
			int index;
			if (graph.TryIndexOf(zoneId, out index))
			{
				dirty[index] = true;
			}
		}

		/// <summary>Writes one zone's whole level-2 slice and clears its flag. All of it or none of
		/// it: a half-written slice is a matrix that answers some pairs from this pass and some
		/// from the one before.</summary>
		internal bool TryWriteZone(int zoneIndex, int[] ids, ushort[] edges, ushort[] pairs, out KingdomCityFault fault)
		{
			if (zoneIndex < 0 || zoneIndex >= graph.Count)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (ids == null || edges == null || pairs == null
				|| edges.Length != ids.Length * KingdomDistanceRules.EdgesPerZone
				|| pairs.Length != KingdomDistanceRules.PairSlots(ids.Length))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			for (int i = 0; i < ids.Length; i++)
			{
				if (ids[i] <= 0)
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				for (int j = 0; j < i; j++)
				{
					if (ids[j] == ids[i])
					{
						fault = KingdomCityFault.InvalidIndex;
						return false;
					}
				}
			}
			int nextEdges = workEdgeEntries - workEdge[zoneIndex].Length + edges.Length;
			int nextPairs = samePairEntries - samePair[zoneIndex].Length + pairs.Length;
			if (nextEdges > KingdomDistanceRules.MaxWorkEdgeEntries
				|| nextPairs > KingdomDistanceRules.MaxSamePairEntries)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			endpointIds[zoneIndex] = (int[])ids.Clone();
			workEdge[zoneIndex] = (ushort[])edges.Clone();
			samePair[zoneIndex] = (ushort[])pairs.Clone();
			workEdgeEntries = nextEdges;
			samePairEntries = nextPairs;
			dirty[zoneIndex] = false;
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>How far one sparse endpoint stands from one of its zone's six edges, in cells.</summary>
		internal bool TryWorkToEdge(int zoneIndex, int endpointId, KingdomZoneStep step, out int cells)
		{
			cells = 0;
			int slot;
			int direction = (int)step;
			if (zoneIndex < 0 || zoneIndex >= graph.Count || direction < 0
				|| direction >= KingdomDistanceRules.EdgesPerZone
				|| !TrySlot(zoneIndex, endpointId, out slot))
			{
				return false;
			}
			int at = slot * KingdomDistanceRules.EdgesPerZone + direction;
			int value = workEdge[zoneIndex][at];
			if (value >= KingdomDistanceRules.NoRoute)
			{
				return false;
			}
			cells = value;
			return true;
		}

		/// <summary>How far two works in the same zone stand from each other, in cells.</summary>
		internal bool TrySameZone(int zoneIndex, int endpointA, int endpointB, out int cells)
		{
			cells = 0;
			if (zoneIndex < 0 || zoneIndex >= graph.Count)
			{
				return false;
			}
			if (endpointA == endpointB && endpointA > 0)
			{
				int standing;
				return TrySlot(zoneIndex, endpointA, out standing);
			}
			int slotA;
			int slotB;
			if (!TrySlot(zoneIndex, endpointA, out slotA) || !TrySlot(zoneIndex, endpointB, out slotB))
			{
				return false;
			}
			int index;
			KingdomCityFault fault;
			if (!KingdomDistanceRules.TryPairIndex(slotA, slotB, endpointIds[zoneIndex].Length, out index, out fault))
			{
				return false;
			}
			int value = samePair[zoneIndex][index];
			if (value >= KingdomDistanceRules.NoRoute)
			{
				return false;
			}
			cells = value;
			return true;
		}

		/// <summary>
		/// <c>Dist(a, b)</c>, composed in O(1) from the three stores, exactly as &sect;3.10(2)
		/// writes it. Refuses when either endpoint's slice is dirty: a distance composed out of a
		/// slice the city knows is stale is worse than no distance, because a route planned on one
		/// is a carrier walking past a nearer holder (I6).
		/// </summary>
		internal bool TryCompose(int fromZone, int fromEndpoint, int toZone, int toEndpoint, out int cells, out KingdomCityFault fault)
		{
			cells = 0;
			if (IsDirty(fromZone) || IsDirty(toZone))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (fromZone == toZone)
			{
				if (!TrySameZone(fromZone, fromEndpoint, toEndpoint, out cells))
				{
					fault = KingdomCityFault.OutsideItinerary;
					return false;
				}
				fault = KingdomCityFault.None;
				return true;
			}
			KingdomZoneStep out_;
			KingdomZoneStep in_;
			int between;
			if (!graph.TryDistance(fromZone, toZone, out between)
				|| !graph.TryRouteSteps(fromZone, toZone, out out_, out in_))
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			int leaving;
			int arriving;
			if (!TryWorkToEdge(fromZone, fromEndpoint, out_, out leaving)
				|| !TryWorkToEdge(toZone, toEndpoint, in_, out arriving))
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			long total = (long)leaving + between + arriving;
			if (total > KingdomDistanceRules.NoRoute)
			{
				fault = KingdomCityFault.ArithmeticOverflow;
				return false;
			}
			cells = (int)total;
			fault = KingdomCityFault.None;
			return true;
		}

		private bool TrySlot(int zoneIndex, int endpointId, out int slot)
		{
			slot = -1;
			if (zoneIndex < 0 || zoneIndex >= endpointIds.Length || endpointId <= 0)
			{
				return false;
			}
			int[] ids = endpointIds[zoneIndex];
			for (int i = 0; i < ids.Length; i++)
			{
				if (ids[i] == endpointId)
				{
					slot = i;
					return true;
				}
			}
			return false;
		}
	}
}
