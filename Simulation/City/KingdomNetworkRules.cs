using System;

namespace ThousandAndFirst.Simulation.City
{

	/// <summary>
	/// Network graphs, the declared-topology law, and the traversal the flow solve runs on.
	/// <para>
	/// <b>Two layers, and this is the model one.</b> Attended, the founder's zone runs vanilla's own
	/// transmission family unchanged — that machinery already does network discovery
	/// (<c>IPowerTransmission.FindGrid</c>, <c>D/XRL/World/Parts/IPowerTransmission.cs:1099-1211</c>,
	/// a cardinal-only BFS over cells collecting <c>Producers</c>, <c>Consumers</c> and a
	/// <c>GridCapacity</c>), and charge delivery by event (<c>ChargeAvailableEvent</c> /
	/// <c>FinishChargeAvailableEvent</c> &rarr; <c>Process(E)</c>, <c>:383-393</c> and
	/// <c>:1698-1766</c>; demand gathered by <c>QueryChargeEvent</c> / <c>TestChargeEvent</c>,
	/// <c>:322-358</c>, each OR-ing the part's <c>GridBit</c> into the event's <c>GridMask</c>,
	/// which is the engine's own re-entrancy guard and the reason a cyclic grid terminates). Per
	/// Addendum 11(c) we do not reinvent any of it.
	/// </para>
	/// <para>
	/// <b>The one engine fact that forces a model layer at all.</b> That flood-fill expands with
	/// <c>GetLocalCellFromDirection</c>, which is <c>GetCellFromDirectionGlobal(..., bLocalOnly:
	/// true, ...)</c> (<c>D/XRL/World/Cell.cs:8051-8054</c>): <b>a vanilla network cannot cross a
	/// zone boundary.</b> For a city that spans zones the graph here is not an optimisation of
	/// vanilla's network, it is the only way a multi-zone network exists. Vanilla renders the part
	/// the founder is standing in; this owns the whole of it.
	/// </para>
	/// <para>
	/// <b>Topology changes only on placement</b>, never on time and never on stock — the identical
	/// cache discipline <c>KingdomDistanceRules</c> keeps. A graph carries the ground stamp it was
	/// built from; placing, removing or destroying a conduit or a node bumps the ground's stamp and
	/// <see cref="NeedsRebuild"/> then answers true. The rebuild reads the ground, so it happens
	/// when a zone renders and <b>never at reckon</b> (&sect;0.0(d)).
	/// </para>
	/// <para>
	/// Pure and engine-free, and total over representable input.
	/// </para>
	/// </summary>
	internal static partial class KingdomNetworkRules
	{
		/// <summary>LIVING-CITY-ARCHITECTURE &sect;3.11 / &sect;0.0(c).</summary>
		internal const int MaxNodes = KingdomBudgetRules.NetworkMaxNodes;

		/// <summary>LIVING-CITY-ARCHITECTURE &sect;3.11 / &sect;0.0(c).</summary>
		internal const int MaxEdges = KingdomBudgetRules.NetworkMaxEdges;

		/// <summary>LIVING-CITY-ARCHITECTURE &sect;3.11 / &sect;0.0(c).</summary>
		internal const int MaxNetworksPerCity = KingdomBudgetRules.NetworksPerCity;

		/// <summary>A node the traversal reached from no edge: a source, or a node nothing
		/// reaches. Two hundred and fifty-five, because a node index is at most thirty-one and an
		/// edge index at most forty-seven, so a byte carries both with a sentinel to spare.</summary>
		internal const byte NoParent = 255;

		/// <summary>
		/// What a node's bottleneck reads as when nothing between it and its source narrows —
		/// which is what a source itself always is. Saturating rather than wrapping: the solve
		/// takes a minimum against it and never adds to it.
		/// </summary>
		internal const int Unlimited = int.MaxValue;

		/// <summary>
		/// The solve's op ceiling for one network, LIVING-CITY-ARCHITECTURE &sect;0.0 /
		/// &sect;3.11: <c>O(nodes + edges)</c>, at most 32 + 48 = <b>80 node-visits</b>. Asserted
		/// rather than asserted-about: <see cref="KingdomNetworkGraph.TryBottleneck"/> reports what
		/// it actually spent and the test compares it to this.
		/// </summary>
		internal static int MaxSolveVisits(int nodeCount, int edgeCount)
		{
			int nodes = (nodeCount > 0) ? nodeCount : 0;
			int edges = (edgeCount > 0) ? edgeCount : 0;
			return nodes + edges;
		}

		/// <summary>North, in the four-bit declaration mask. The four cardinals and no diagonal,
		/// because that is the walk vanilla's own network discovery makes
		/// (<c>Cell.DirectionListCardinalOnly</c>, <c>D/XRL/World/Cell.cs:328</c>, read at
		/// <c>D/XRL/World/Parts/IPowerTransmission.cs:1189-1197</c>) and a founder should not have
		/// to learn two rules about what touches what.</summary>
		internal const int JoinNorth = 1;

		internal const int JoinSouth = 2;

		internal const int JoinEast = 4;

		internal const int JoinWest = 8;

		/// <summary>All four. What an ordinary length of main declares.</summary>
		internal const int JoinAll = JoinNorth | JoinSouth | JoinEast | JoinWest;

		/// <summary>
		/// Reads a segment's declaration off its XML: <c>"NS"</c>, <c>"EW"</c>, <c>"NSEW"</c>, or
		/// the empty string for a segment that joins nothing at all.
		/// <para>
		/// <b>Third-party XML is untrusted and this refuses rather than defaults</b>, the same way
		/// <c>KingdomPowerRules.TryParseSource</c> does: a misspelt declaration makes a segment
		/// that joins nothing, which is inert and visible, rather than one that quietly joins
		/// everything, which is a silent merge and the one thing the LIQUID LAW forbids outright.
		/// </para>
		/// </summary>
		internal static bool TryParseJoins(string text, out int mask)
		{
			mask = 0;
			if (text == null)
			{
				return false;
			}
			string trimmed = text.Trim();
			if (trimmed.Length == 0)
			{
				// Declared nothing, and that is a legal declaration: a capped end.
				return true;
			}
			for (int i = 0; i < trimmed.Length; i++)
			{
				switch (char.ToUpperInvariant(trimmed[i]))
				{
				case 'N':
					mask |= JoinNorth;
					break;
				case 'S':
					mask |= JoinSouth;
					break;
				case 'E':
					mask |= JoinEast;
					break;
				case 'W':
					mask |= JoinWest;
					break;
				default:
					mask = 0;
					return false;
				}
			}
			return true;
		}

		/// <summary>The other end of one cardinal. Zero for anything that is not exactly one of
		/// the four.</summary>
		internal static int OppositeJoin(int single)
		{
			switch (single)
			{
			case JoinNorth:
				return JoinSouth;
			case JoinSouth:
				return JoinNorth;
			case JoinEast:
				return JoinWest;
			case JoinWest:
				return JoinEast;
			default:
				return 0;
			}
		}

		/// <summary>
		/// Whether two neighbouring segments have <b>both</b> declared toward each other.
		/// <para>
		/// This is the whole of <i>declared, never inferred</i>, and the reason it takes both masks
		/// is that one segment's declaration is an offer and not a connection. Tile adjacency is
		/// not consulted anywhere: the caller has already established that these two are neighbours
		/// in <paramref name="direction"/>, and being neighbours is exactly what does not join
		/// them.
		/// </para>
		/// </summary>
		internal static bool DeclaredToward(int mineMask, int theirMask, int direction)
		{
			int back = OppositeJoin(direction);
			if (back == 0)
			{
				return false;
			}
			return (mineMask & direction) != 0 && (theirMask & back) != 0;
		}

		/// <summary>
		/// Which way a crossover carries something that arrived from <paramref name="from"/>, or
		/// zero when it carries nothing that way.
		/// <para>
		/// A crossover pairs opposite cardinals and <b>nothing else</b>: what comes in the north
		/// face leaves by the south, what comes in the east leaves by the west, and neither pair
		/// ever meets the other. That is the piece's entire behaviour and the reason it needs no
		/// liquid of its own — it types nothing, so it can never merge anything.
		/// </para>
		/// </summary>
		internal static int CrossoverExit(int pairsMask, int from)
		{
			int back = OppositeJoin(from);
			if (back == 0)
			{
				return 0;
			}
			// Both faces of the pair must be declared, or the piece is a dead end that way rather
			// than a through-route: half a crossover carries nothing.
			return ((pairsMask & from) != 0 && (pairsMask & back) != 0) ? back : 0;
		}

	}
}
