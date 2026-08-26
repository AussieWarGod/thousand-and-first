using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCityRules
	{
		/// <summary>
		/// How much of one kind is carried out of the city's OTHER zones to cover a shortfall where
		/// the founder is standing, and out of which zones.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;1.2(a) and &sect;3.9 together: consumption anywhere draws
		/// on the same rows, but a dram is drunk out of a particular urn — so every zone the demand
		/// reaches owes its own vessels the difference. The apportionment is
		/// <c>KingdomDrainRules</c>'s own, with a zone standing in for a vessel: one rule, one
		/// order, one home.
		/// </para>
		/// <para>
		/// <b>W6 decides WHICH zone by distance</b> (&sect;3.10(1), invariant I6): the demand is
		/// spread over the city's grounds <i>nearest first</i>, on the level-1 zone graph, tie-broken
		/// on the lower row index — a stored fact, stable under a reload because rows are never
		/// reordered. Before W6 it was spread in row order, which is how a carrier ends up crossing
		/// the city past a nearer store. Inside a ground, the oldest dedication still pays first and
		/// nothing about I4 moves: the two rules answer different questions.
		/// </para>
		/// <para>
		/// Nothing is created, and the exact sense of that is worth stating because the loose
		/// reading of it is false. A row's LEVEL includes what its works have made and nobody has
		/// poured yet -- that is what a positive <c>owed</c> means -- so a carry can and should be
		/// able to deliver a harvest that is still a claim. What it moves is the CLAIM: the giving
		/// row loses level and debt together, the taking ground receives real goods that the model
		/// had already booked as made, and the city's total of both is unchanged. What
		/// <c>spokenFor</c> reserves is the other sign only -- a row already owing a DRAW has that
		/// much of its level promised to a vessel nobody has opened, and it may not be given away
		/// twice. So: no dram is invented, but a dram may land in a vessel other than the one it
		/// was booked against, which is exactly what a porter is for. I1 holds across the transfer
		/// by construction: what leaves one row arrives on another as a debt against real
		/// containers.
		/// </para>
		/// </summary>
		/// <param name="state">The book.</param>
		/// <param name="seatedZoneId">The zone the founder is standing in. It is never a source.</param>
		/// <param name="kind">Which stock is short.</param>
		/// <param name="demand">What the seated zone is short by. Zero or less moves nothing.</param>
		/// <param name="room">What the seated zone's own containers can still take.</param>
		/// <param name="moved">Per zone row, indexed as the rows are: what this zone gives up.</param>
		/// <param name="total">The sum of <paramref name="moved"/>.</param>
		internal static bool TryPlanTransfer(
			KingdomCityState state,
			string seatedZoneId,
			KingdomStockKind kind,
			long demand,
			long room,
			long[] moved,
			out long total,
			out KingdomCityFault fault)
		{
			return TryPlanTransfer(state, seatedZoneId, kind, demand, room, null, moved, out total, out fault);
		}

		/// <summary>The same apportionment, over a graph that knows which rock has been opened.</summary>
		/// <param name="shafts">Zone ids carrying a finished delve; see
		/// <see cref="TryZoneGraph(KingdomCityState, string[], out KingdomZoneGraph, out KingdomCityFault)"/>.</param>
		internal static bool TryPlanTransfer(
			KingdomCityState state,
			string seatedZoneId,
			KingdomStockKind kind,
			long demand,
			long room,
			string[] shafts,
			long[] moved,
			out long total,
			out KingdomCityFault fault)
		{
			total = 0L;
			if (state == null || moved == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			int zones = state.ZoneCount;
			if (moved.Length < zones)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			for (int i = 0; i < zones; i++)
			{
				moved[i] = 0L;
			}
			fault = KingdomCityFault.None;
			long wanted = (demand < room) ? demand : room;
			if (wanted <= 0L)
			{
				return true;
			}
			// W6, LIVING-CITY-ARCHITECTURE §3.10(1). The order the city is drawn on is
			// NEAREST-HOLDER, not zone-row order: an input job binds to the closest ground
			// actually holding the resource. Dedication order is untouched and still decides which
			// urn inside that ground pays (§3.9, I4) — the two rules are about different
			// questions, and stacking them this way is what lets both be true at once.
			int[] cells = new int[zones];
			if (!TryZoneDistances(state, seatedZoneId, shafts, cells, out fault))
			{
				return false;
			}
			KingdomVesselRow[] sources = new KingdomVesselRow[zones];
			for (int i = 0; i < zones; i++)
			{
				KingdomZoneRow row;
				if (!state.TryZone(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				KingdomStockPair pair;
				long available = 0L;
				if (row.LastReadTick > 0L
					&& !string.Equals(row.ZoneId, seatedZoneId, StringComparison.Ordinal)
					&& row.Stocks.TryGet(kind, out pair))
				{
					// A zone already owing a draw has that much of its level spoken for: the
					// vessels have not paid it yet, so it may not be given away twice.
					long spokenFor = -(long)Min(row.OwedOf(kind), 0);
					available = pair.Level - spokenFor;
					if (available < 0L)
					{
						available = 0L;
					}
				}
				// The drain's own ordering keys, carrying the logistics order: the "dedication
				// ordinal" it sorts on first is the distance to the seat, and the zone row index
				// beneath it is the frozen tie-break. One sort, one implementation, and no second
				// opinion about precedence.
				sources[i] = new KingdomVesselRow(i, cells[i], kind, available, available, true);
			}
			long[] drawn = new long[zones];
			long shortfall;
			if (!KingdomDrainRules.TryApportion(sources, zones, kind, wanted, drawn, out shortfall, out fault))
			{
				return false;
			}
			for (int i = 0; i < zones; i++)
			{
				moved[i] = drawn[i];
				total += drawn[i];
			}
			return true;
		}

	}
}
