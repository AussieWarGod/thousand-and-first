using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPower
	{
		/// <summary>
		/// The city's power as one graph row and its two argument lists.
		/// <para>
		/// A star: node 0 is the settlement's own carrying and everything else hangs off it. The
		/// reason is <c>KingdomPower</c>'s own and unchanged &mdash; a working post is somebody
		/// walking the charge across, not a fence of gearboxes &mdash; and the point of composing
		/// it as a real graph anyway is that it then goes through the SAME bottleneck relaxation and
		/// the SAME solve a laid liquid main gets. One accounting.
		/// </para>
		/// </summary>
		private static bool TryCompose(
			KingdomSystem System,
			Zone Z,
			List<GameObject> Works,
			List<GameObject> Stores,
			List<GameObject> Sinks,
			long Days,
			int Capacity,
			out KingdomNetworkGraph graph,
			out KingdomFlowDemand[] demands,
			out int[] order,
			out long supplyPerDay)
		{
			graph = null;
			demands = new KingdomFlowDemand[0];
			order = new int[0];
			supplyPerDay = 0L;
			int count = 1 + Works.Count + Sinks.Count;
			if (count > KingdomNetworkRules.MaxNodes)
			{
				count = KingdomNetworkRules.MaxNodes;
			}
			for (int i = 0; i < Works.Count; i++)
				if (string.IsNullOrEmpty(Works[i]?.IDIfAssigned)) return false;
			for (int i = 0; i < Sinks.Count; i++)
				if (string.IsNullOrEmpty(Sinks[i]?.IDIfAssigned)) return false;
			KingdomNetworkNode[] nodes = new KingdomNetworkNode[count];
			nodes[0] = new KingdomNetworkNode(0, KingdomNetworkRole.Store, KingdomWorkTier.Water, Capacity,
				KingdomPowerRules.ThroughputForDays(Capacity, 1));
			int at = 1;
			int[] sourceNode = new int[Works.Count];
			for (int i = 0; i < Works.Count && at < count; i++)
			{
				string workId = Works[i].IDIfAssigned;
				KingdomPowerRules.PowerSource source;
				int output = DailyOutput(Works[i], Z, (int)Clamp(Days), out source);
				Dry(System, Works[i], output, source);
				sourceNode[i] = at;
				nodes[at++] = new KingdomNetworkNode(
					KingdomCityRules.StableId(workId),
					KingdomNetworkRole.Source,
					KingdomWorkTier.Water,
					0,
					output);
			}
			List<KingdomFlowDemand> wants = new List<KingdomFlowDemand>();
			List<GameObject> sunk = new List<GameObject>();
			for (int i = 0; i < Sinks.Count && at < count; i++)
			{
				string sinkId = Sinks[i].IDIfAssigned;
				int need = DailyNeedOf(Sinks[i]);
				nodes[at++] = new KingdomNetworkNode(
					KingdomCityRules.StableId(sinkId),
					KingdomNetworkRole.Sink,
					TierOf(Sinks[i]),
					0,
					need);
				wants.Add(new KingdomFlowDemand(KingdomCityRules.StableId(sinkId), TierOf(Sinks[i]), need));
				sunk.Add(Sinks[i]);
			}
			Sinks.Clear();
			Sinks.AddRange(sunk);
			KingdomCityFault fault;
			if (!KingdomNetworks.TryStar(0, nodes, at, int.MaxValue, out graph, out fault))
			{
				KingdomLog.Log("power: refused to compose the grid (" + fault + ")");
				return false;
			}
			int[] bottleneck = new int[graph.NodeCount];
			int visits;
			if (!graph.TryBottleneck(bottleneck, out visits, out fault))
			{
				KingdomLog.Log("power: bottleneck refused (" + fault + ")");
				return false;
			}
			for (int i = 0; i < Works.Count; i++)
			{
				KingdomNetworkNode node;
				// Zero means "this work did not fit inside the node cap", never node zero: the hub
				// is node zero and the first work is node one. Reading it as an index would have
				// added the hub's own throughput to the supply once per work that did not fit.
				if (sourceNode[i] <= 0 || sourceNode[i] >= graph.NodeCount || !graph.TryNode(sourceNode[i], out node))
				{
					continue;
				}
				int reach = bottleneck[sourceNode[i]];
				supplyPerDay += (node.RatePerDay < reach) ? node.RatePerDay : reach;
			}
			demands = wants.ToArray();
			order = new int[demands.Length];
			if (!KingdomFlowRules.TryBrownoutOrder(demands, demands.Length, order, out fault))
			{
				KingdomLog.Log("power: brownout order refused (" + fault + ")");
				return false;
			}
			return true;
		}

		/// <summary>
		/// What one thing that spends charge wants in a day. A post is the default and the unit the
		/// whole report is written in; anything that costs more than a post declares it on itself,
		/// so this lane never has to learn what any particular work is.
		/// </summary>
		private static int DailyNeedOf(GameObject Sink)
		{
			int declared = Sink.GetIntProperty("KingdomDailyDraw");
			return (declared > 0) ? declared : KingdomPowerRules.PostDailyNeedCharge;
		}

		/// <summary>Where a thing that spends charge sits on the brownout ladder, read off the
		/// catalogue's own <c>Category</c> rather than off a second table. Anything the catalogue
		/// does not know lands on the middle rung.</summary>
		private static KingdomWorkTier TierOf(GameObject Sink)
		{
			string key = KingdomUpgrade.DesignKeyOf(Sink);
			KingdomRules.BuildEntry entry;
			if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out entry))
			{
				return KingdomWorkTier.Amenity;
			}
			return KingdomFlowRules.TierOfCategory(entry.Category);
		}

		/// <summary>
		/// The network's own resolved-through tick: the oldest planted stamp among its nodes.
		/// <para>
		/// One span for the whole solve, which is what makes the netting a netting rather than a
		/// pile of per-work sums. A node with no stamp is not counted here &mdash; it is planted
		/// instead, and credited nothing this pass, so a work never pays out for the day it was
		/// raised. After the pass every stamp is the same, so the span cannot fray.
		/// </para>
		/// </summary>
		private static long Through(List<GameObject> Works, List<GameObject> Stores)
		{
			long oldest = 0L;
			for (int i = 0; i < Works.Count; i++)
			{
				r_KingdomPowerWork part = Works[i].GetPart<r_KingdomPowerWork>();
				if (part != null && part.LastResolvedTick > 0L && (oldest == 0L || part.LastResolvedTick < oldest))
				{
					oldest = part.LastResolvedTick;
				}
			}
			for (int i = 0; i < Stores.Count; i++)
			{
				r_KingdomPowerStore store = Stores[i].GetPart<r_KingdomPowerStore>();
				if (store != null && store.LastResolvedTick > 0L && (oldest == 0L || store.LastResolvedTick < oldest))
				{
					oldest = store.LastResolvedTick;
				}
			}
			return oldest;
		}

		/// <summary>Stamps anything that has never been stamped, so it joins the network at now
		/// rather than at tick zero and is credited nothing for the day it was raised.</summary>
		private static void Plant(List<GameObject> Works, List<GameObject> Stores, long TimeTicks)
		{
			for (int i = 0; i < Works.Count; i++)
			{
				r_KingdomPowerWork part = Works[i].GetPart<r_KingdomPowerWork>();
				if (part != null && part.LastResolvedTick <= 0L)
				{
					part.LastResolvedTick = TimeTicks;
				}
			}
			for (int i = 0; i < Stores.Count; i++)
			{
				r_KingdomPowerStore store = Stores[i].GetPart<r_KingdomPowerStore>();
				if (store != null && store.LastResolvedTick <= 0L)
				{
					store.LastResolvedTick = TimeTicks;
				}
			}
		}

		/// <summary>Advances every stamp to the same tick, and only once the solve has succeeded.
		/// A refused solve leaves every clock where it was, so the day is owed rather than lost.</summary>
		private static void Stamp(List<GameObject> Works, List<GameObject> Stores, long TimeTicks)
		{
			for (int i = 0; i < Works.Count; i++)
			{
				r_KingdomPowerWork part = Works[i].GetPart<r_KingdomPowerWork>();
				if (part != null)
				{
					part.LastResolvedTick = TimeTicks;
				}
			}
			for (int i = 0; i < Stores.Count; i++)
			{
				r_KingdomPowerStore store = Stores[i].GetPart<r_KingdomPowerStore>();
				if (store != null)
				{
					store.LastResolvedTick = TimeTicks;
				}
			}
		}

		/// <summary>
		/// Tells the founder what went quiet, once each, and unsays it for anything that came back.
		/// <para>
		/// Addendum 12(c) and STANDARDS 7b: announced once, on the ledger where the founder will
		/// see it, and <b>recovery says nothing</b> &mdash; the latch is cleared so the next failure
		/// can be told again, and no line is written for the good news. The latch lives on the
		/// object, exactly as <c>r_KingdomPowerWork.DryAnnounced</c> does, so a dormant city keeps
		/// its own memory of what it has already said with no field on the system.
		/// </para>
		/// </summary>
		private static void Brownouts(KingdomSystem System, Zone Z, List<GameObject> Sinks, KingdomFlowDemand[] Demands, int[] Order, int Stopped, long TimeTicks)
		{
			bool[] quiet = new bool[Sinks.Count];
			for (int i = 0; i < Stopped && i < Order.Length; i++)
			{
				if (Order[i] >= 0 && Order[i] < quiet.Length)
				{
					quiet[Order[i]] = true;
				}
			}
			for (int i = 0; i < Sinks.Count; i++)
			{
				GameObject sink = Sinks[i];
				if (!GameObject.Validate(sink))
				{
					continue;
				}
				bool announced = sink.GetIntProperty("KingdomBrownout") == 1;
				if (!quiet[i])
				{
					if (announced)
					{
						// Unsaid, and unsaid in silence. A settlement that announced every recovery
						// would be a settlement that talks about itself constantly, which is the
						// thing 7b's complaint is actually about.
						sink.SetIntProperty("KingdomBrownout", 0);
					}
					continue;
				}
				if (announced)
				{
					continue;
				}
				sink.SetIntProperty("KingdomBrownout", 1);
				string named = KingdomDesign.ReferenceFor(sink, sink.ShortDisplayName);
				System.Ledger.Note("{{r|" + KingdomFlowRules.BrownoutNotice(named) + "}}");
				KingdomChronicle.Record(System, KingdomFlowRules.BrownoutTelling(named, KingdomPresentation.Rich(System.KingdomDisplayName)));
				Simulation.City.KingdomHappenings.TellBrownout(System, Demands[i].WorkId, (int)Demands[i].Tier, Z.ZoneID, TimeTicks);
			}
		}

		private static long Clamp(long value)
		{
			if (value <= 0L)
			{
				return 0L;
			}
			return (value > int.MaxValue) ? int.MaxValue : value;
		}

	}
}
