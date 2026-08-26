using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The two-level distance matrix, and the roads discount that scales it.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.10(2): <b>we never store <c>works&sup2;</c>.</b> Level 1 is
	/// the zone graph &mdash; at most nine nodes, all-pairs by Floyd&ndash;Warshall at 9&sup3; = 729
	/// integer operations, a table of at most 81 entries. Level 2 is within a zone &mdash;
	/// work-to-edge lengths and same-zone work pairs. Any cross-zone distance composes from them in
	/// O(1):
	/// </para>
	/// <code>
	/// Dist(a, b) = IntraZone(a -&gt; exitEdge) + Sum EdgeCrossing + IntraZone(entryEdge -&gt; b)
	/// </code>
	/// <para>
	/// Pure and engine-free. <b>Invalidation is by structure, never by time or by stock</b> &mdash;
	/// a dirty flag per zone, set only on work placement, work removal, or a road change, and the
	/// zone's slice recomputed the next time that zone renders. Never at reckon: recomputing needs
	/// the ground, and reckon may not touch it (&sect;0.0(d)).
	/// </para>
	/// </summary>
	internal static class KingdomDistanceRules
	{
		/// <summary>LIVING-CITY-ARCHITECTURE &sect;0.0(f): nine zones is one whole parasang, and
		/// the level-1 table is sized for it whatever the stage-gate cap says today.</summary>
		internal const int MaxNodes = 9;

		/// <summary>The six directions a neighbour can lie in.</summary>
		internal const int EdgesPerZone = 6;

		/// <summary>Entries the level-2 work-to-edge store may hold, per city.
		/// LIVING-CITY-ARCHITECTURE &sect;0.0(c).</summary>
		internal const int MaxWorkEdgeEntries = KingdomCityMemoryRules.DistanceWorkEdgeEntries;

		/// <summary>Entries the level-2 same-zone pair store may hold, per city.
		/// LIVING-CITY-ARCHITECTURE &sect;0.0(c).</summary>
		internal const int MaxSamePairEntries = KingdomCityMemoryRules.DistanceSameZoneEntries;

		/// <summary>
		/// What one hop across a zone boundary costs the metric, in cells.
		/// <para>
		/// A vanilla zone is eighty cells wide (<c>D/XRL/World/Zone.cs</c>'s own default
		/// dimensions), so a carrier that enters one edge and leaves by another crosses about half
		/// of it. Named rather than inlined for the same reason the sinuosity constants are: a
		/// metric constant nobody can find is a metric nobody can retune.
		/// </para>
		/// </summary>
		internal const int ZoneTransitCells = 40;

		/// <summary>An entry the table has no route for. <c>ushort.MaxValue</c>, so an
		/// unreachable pair reads as unreachable rather than as adjacent.</summary>
		internal const int NoRoute = 65535;

		/// <summary>Which way <paramref name="to"/> lies from <paramref name="from"/>, or
		/// <see cref="KingdomZoneStep.None"/> when they are not neighbours.
		/// <para>
		/// Orthogonal only in the same stratum &mdash; deliberately narrower than
		/// <c>KingdomRules.CoordsAdjacent</c>, which admits diagonals because a CLAIM may border a
		/// zone corner-to-corner. A carrier cannot walk through a corner, so the routing graph does
		/// not have that edge.
		/// </para>
		/// </summary>
		internal static KingdomZoneStep StepBetween(KingdomZoneNode from, KingdomZoneNode to)
		{
			int dx = to.GlobalX - from.GlobalX;
			int dy = to.GlobalY - from.GlobalY;
			int dz = to.Stratum - from.Stratum;
			if (dz != 0)
			{
				if (dx != 0 || dy != 0 || (dz != 1 && dz != -1))
				{
					return KingdomZoneStep.None;
				}
				return (dz > 0) ? KingdomZoneStep.Down : KingdomZoneStep.Up;
			}
			if (dx == 0 && (dy == 1 || dy == -1))
			{
				return (dy > 0) ? KingdomZoneStep.South : KingdomZoneStep.North;
			}
			if (dy == 0 && (dx == 1 || dx == -1))
			{
				return (dx > 0) ? KingdomZoneStep.East : KingdomZoneStep.West;
			}
			return KingdomZoneStep.None;
		}

		/// <summary>
		/// Whether two nodes share an edge a carrier can actually walk.
		/// <para>
		/// Not the same question as <see cref="StepBetween"/>, and the difference is the whole of
		/// the delve. A direction always exists between a zone and the one under it; an EDGE
		/// exists only where a shaft was cut (<see cref="KingdomDelveRules.ShaftJoinsStrata"/>).
		/// Symmetric, because a shaft is: the flag is read off the SHALLOWER node, which is the
		/// ground the winding gear stands on, whichever end the question is asked from.
		/// </para>
		/// </summary>
		internal static bool Adjacent(KingdomZoneNode from, KingdomZoneNode to)
		{
			KingdomZoneStep step = StepBetween(from, to);
			if (step == KingdomZoneStep.None)
			{
				return false;
			}
			if (step != KingdomZoneStep.Up && step != KingdomZoneStep.Down)
			{
				return true;
			}
			KingdomZoneNode head = (from.Stratum < to.Stratum) ? from : to;
			KingdomZoneNode foot = (from.Stratum < to.Stratum) ? to : from;
			return KingdomDelveRules.ShaftJoinsStrata(head.Stratum, foot.Stratum, head.Shaft);
		}

		/// <summary>
		/// A distance scaled by a road discount, in percent of the undiscounted figure.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.10(3): a leg following a laid road is scaled by
		/// <c>KingdomItineraryRules.RoadDiscountPercent</c>, <b>applied identically to the estimate
		/// and to the measured length, so a road cannot make the two disagree</b>. The consequence
		/// the player sees is the point: laying a road visibly shortens every itinerary that uses
		/// it.
		/// </para>
		/// <para>
		/// Rounds up, and never below one cell for a non-zero distance: a road makes a journey
		/// shorter and never instantaneous.
		/// </para>
		/// </summary>
		internal static bool TryDiscount(int cells, int roadDiscountPercent, out int discounted, out KingdomCityFault fault)
		{
			discounted = 0;
			if (cells < 0 || roadDiscountPercent <= 0 || roadDiscountPercent > 100)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			fault = KingdomCityFault.None;
			if (cells == 0)
			{
				return true;
			}
			long scaled = ((long)cells * roadDiscountPercent + 99L) / 100L;
			discounted = (scaled < 1L) ? 1 : ((scaled > NoRoute) ? NoRoute : (int)scaled);
			return true;
		}

		/// <summary>Same-zone pair index inside one zone's triangular slice. Total over any
		/// ordering of the two work slots, because a distance is symmetric and storing it twice is
		/// storing two answers.</summary>
		internal static bool TryPairIndex(int slotA, int slotB, int worksInZone, out int index, out KingdomCityFault fault)
		{
			index = -1;
			if (worksInZone < 0 || slotA < 0 || slotB < 0 || slotA >= worksInZone || slotB >= worksInZone || slotA == slotB)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			int low = (slotA < slotB) ? slotA : slotB;
			int high = (slotA < slotB) ? slotB : slotA;
			// The row-major upper triangle: rows shrink by one each step down.
			index = low * worksInZone - (low * (low + 1)) / 2 + (high - low - 1);
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>Entries a zone's triangular same-zone slice needs for this many works.</summary>
		internal static int PairSlots(int worksInZone)
		{
			if (worksInZone < 2)
			{
				return 0;
			}
			return worksInZone * (worksInZone - 1) / 2;
		}
	}
}
