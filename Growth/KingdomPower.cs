using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently. Only the parts move; the
// settlement-side resolver below stays where the rest of the mod's code lives.
namespace XRL.World.Parts
{
	/// <summary>
	/// A work the settlement raised to make power: a mill someone leans on, a wheel a brook
	/// turns, a vane the wind pushes. Carries its own tick stamp, so a city nobody has stood
	/// in for a week resolves its own absence the moment the founder walks back in, and needs
	/// no clock running behind them.
	/// </summary>
	[Serializable]
	public class r_KingdomPowerWork : IPart
	{
		/// <summary>
		/// What turns this work, named in XML as <c>Hands</c>, <c>Water</c>, or <c>Wind</c>. An
		/// unreadable value disables the work rather than defaulting it, so a third party's
		/// misspelling is inert instead of quietly becoming a mill.
		/// </summary>
		public string Source = "Hands";

		/// <summary>Tick this work was last credited to. Zero until the first settlement pass
		/// stamps it, which is why a work never pays out for the day it was raised.</summary>
		public long LastResolvedTick;

		/// <summary>Set once the founder has been told this work is standing still, so a wheel
		/// beside no water says so once rather than at every homecoming.</summary>
		public bool DryAnnounced;

		/// <summary>
		/// A day also turns over while the founder is standing there watching it. The
		/// settlement pass resolves absence, which is the hard half, but it only runs on zone
		/// activation &mdash; so a founder who commissions a mill and then stays put would see
		/// nothing happen for as long as they stayed. The comparison is against an absolute
		/// tick this part stored, not a countdown that must be delivered every turn to stay
		/// correct, so missing ticks costs nothing; <c>r_KingdomPlot</c> and
		/// <c>r_KingdomScaffold</c> both keep time the same way.
		/// </summary>
		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			if (LastResolvedTick <= 0 || TimeTick < LastResolvedTick + KingdomRules.TicksPerDay)
			{
				return;
			}
			Zone zone = ParentObject?.CurrentZone;
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (zone == null || system == null || !system.Founded || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				return;
			}
			// Surveying is the expensive part, so it happens only on the tick a day is due -
			// once per day, not once per turn. Crews are assigned first: a work commissioned
			// while the founder stood watching has never been through a growth pass, and
			// would otherwise be credited as though nobody in the settlement had come to it.
			KingdomSystem.Guard("power tick", delegate
			{
				KingdomSurvey survey = KingdomSurvey.Take(zone, system);
				KingdomGrowth.AssignWork(system, survey);
				KingdomPower.OnSettlementPass(system, zone, survey);
			});
		}
	}

	/// <summary>
	/// A bed of molten salt the settlement keeps hot: it holds what the day's works made and
	/// gives it back after dark. A marker part, so that any object carrying it and a
	/// <c>Capacitor</c> &mdash; ours, or a third party's own design &mdash; is a store the
	/// settlement will pour into, with no code change and no registry entry.
	/// </summary>
	[Serializable]
	public class r_KingdomPowerStore : IPart
	{
		/// <summary>Set the first time this store was filled to the brim, so the moment is
		/// written down once and never again.</summary>
		public bool EverFilled;

		/// <summary>
		/// Tick this store was last resolved to. Kept even though the works keep their own,
		/// so that a store outliving every work it was built beside still measures a day
		/// honestly and gives its salt back over one, rather than on every arrival.
		/// </summary>
		public long LastResolvedTick;
	}
}

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// The settlement's power, resolved on the settlement's own clock. A working mill is
	/// settlers leaning on a bar and a working post is somebody carrying the charge across to
	/// it, so there is no grid to wire and no machine to manage: the founder commissions the
	/// works, the settlement crews them, and what they make reaches whatever the settlement
	/// built to spend it.
	/// </summary>
	/// <remarks>
	/// Charge is vanilla charge throughout. The works are vanilla blueprints, the stores and
	/// posts are vanilla <c>Capacitor</c>s, and delivery goes through vanilla's own
	/// <c>ChargeAvailableEvent</c>, so anything with a charge-bearing part the settlement
	/// raised is powered for free. What is deliberately <em>not</em> used is vanilla's
	/// <c>IPowerTransmission</c> grid: it is built from cardinal-adjacent runs of matching
	/// conduit (<c>IPowerTransmission.FindGrid</c>), which would make the founder lay gearbox
	/// fence between a windmill and a charging post to get anywhere &mdash; a wiring puzzle,
	/// and one this mod's automatic placement could not guarantee a solution to. It also only
	/// exists in an active zone, so a dormant city would have no grid at all.
	/// </remarks>
	public static class KingdomPower
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionPower") != "No";

		/// <summary>
		/// Runs the settlement's power for however many days have passed since each work was
		/// last credited. Called from the growth pass, after crews are assigned and after every
		/// step that draws water, so a work is only ever credited for hands it actually had and
		/// can never be the reason the thirst ladder fires.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">The claimed zone being resolved.</param>
		/// <param name="Survey">The pass's shared survey. Null skips the pass.</param>
		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!Enabled || !KingdomGrowth.Enabled || System == null || !System.Founded || Z == null || Survey == null)
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			List<GameObject> works = new List<GameObject>();
			List<GameObject> stores = new List<GameObject>();
			List<GameObject> sinks = new List<GameObject>();
			Gather(Z, works, stores, sinks);
			if (works.Count == 0 && stores.Count == 0)
			{
				return;
			}
			// ---- One clock. -----------------------------------------------------------------
			// W7. Power used to count its own days, per work, off ElapsedDays and a
			// remainder-keeping checkpoint, and then do its own summing, its own store clamp and
			// its own delivery. That was a second accounting standing beside the model's, which is
			// exactly the thing W6 made unrepresentable for production and this wave makes
			// unrepresentable for charge. Days are now WORLD-DAY BOUNDARIES through the one counter
			// every other lane uses, so Days(a,b) + Days(b,c) == Days(a,c) for any split: a founder
			// who walks in twice in one day is not paid twice, and a horizon that falls mid-day
			// does not drop the remainder.
			long through = Through(works, stores);
			Plant(works, stores, timeTicks);
			long days;
			KingdomCityFault fault;
			if (through <= 0L
				|| !KingdomProductionRules.TryDaysBetween(through, timeTicks, KingdomRules.TicksPerDay, out days, out fault)
				|| days <= 0L)
			{
				return;
			}
			int capacity = Capacity(stores);
			int held = Held(stores);
			// ---- One graph, one solve. ------------------------------------------------------
			KingdomNetworkGraph graph;
			KingdomFlowDemand[] demands;
			int[] order;
			long supplyPerDay;
			if (!TryCompose(System, Z, works, stores, sinks, days, capacity, out graph, out demands, out order, out supplyPerDay))
			{
				return;
			}
			KingdomFlowSolution solution;
			if (!KingdomFlowRules.TrySolve(
					supplyPerDay,
					demands,
					demands.Length,
					order,
					held,
					capacity,
					KingdomPowerRules.ThroughputForDays(capacity, 1),
					days,
					out solution,
					out fault))
			{
				KingdomLog.Log("power: solve refused (" + fault + "); nothing was spent and no stamp moved");
				return;
			}
			// ---- One rendering. -------------------------------------------------------------
			// Every stamp advances to the same tick, and it advances only now that the solve has
			// succeeded: a refused solve leaves every clock where it was, so the day is owed rather
			// than lost.
			Stamp(works, stores, timeTicks);
			int drawn = (solution.Discharged > 0L) ? Withdraw(stores, (int)Clamp(solution.Discharged)) : 0;
			int pool = (int)Clamp(solution.Generated) + drawn;
			int delivered = Deliver(System, sinks, demands, order, solution.Stopped, (int)Clamp(solution.Delivered), pool);
			int spare = pool - delivered;
			if (spare > 0)
			{
				int poured = Deposit(stores, spare);
				if (poured > 0)
				{
					NoteStoreFilled(System, stores, Held(stores), capacity);
				}
				if (poured < spare)
				{
					// Made with nowhere to put it. Loss, never a queue -- the same ruling the
					// larder makes about a harvest it has no room for, and it is said rather than
					// quietly absorbed (STANDARDS 7b).
					KingdomLog.Log("power: " + (spare - poured) + " charge had nowhere to go; a larger salt store would keep it");
				}
			}
			Brownouts(System, Z, sinks, demands, order, solution.Stopped, timeTicks);
			if (delivered > 0)
			{
				System.Ledger.Note("{{c|The works of " + System.KingdomDisplayName + " made " + delivered + " charge, and it went where it was wanted.}}");
			}
			if (KingdomLog.Enabled) KingdomLog.Log("power pass " + Z.ZoneID + " days=" + days + " works=" + works.Count
				+ " generated=" + solution.Generated + " demanded=" + solution.Demanded + " delivered=" + delivered
				+ " charged=" + solution.Charged + " discharged=" + solution.Discharged + " spilled=" + solution.Spilled
				+ " stopped=" + solution.Stopped + " held=" + Held(stores) + "/" + capacity);
		}

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
			KingdomNetworkNode[] nodes = new KingdomNetworkNode[count];
			nodes[0] = new KingdomNetworkNode(0, KingdomNetworkRole.Store, KingdomWorkTier.Water, Capacity,
				KingdomPowerRules.ThroughputForDays(Capacity, 1));
			int at = 1;
			int[] sourceNode = new int[Works.Count];
			for (int i = 0; i < Works.Count && at < count; i++)
			{
				KingdomPowerRules.PowerSource source;
				int output = DailyOutput(Works[i], Z, (int)Clamp(Days), out source);
				Dry(System, Works[i], output, source);
				sourceNode[i] = at;
				nodes[at++] = new KingdomNetworkNode(
					KingdomCityRules.StableId(Works[i].ID),
					KingdomNetworkRole.Source,
					KingdomWorkTier.Water,
					0,
					output);
			}
			List<KingdomFlowDemand> wants = new List<KingdomFlowDemand>();
			List<GameObject> sunk = new List<GameObject>();
			for (int i = 0; i < Sinks.Count && at < count; i++)
			{
				int need = DailyNeedOf(Sinks[i]);
				nodes[at++] = new KingdomNetworkNode(
					KingdomCityRules.StableId(Sinks[i].ID),
					KingdomNetworkRole.Sink,
					TierOf(Sinks[i]),
					0,
					need);
				wants.Add(new KingdomFlowDemand(KingdomCityRules.StableId(Sinks[i].ID), TierOf(Sinks[i]), need));
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
				KingdomChronicle.Record(System, KingdomFlowRules.BrownoutTelling(named, System.KingdomDisplayName));
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

		/// <summary>
		/// The settlement's power as one line for the Charter's status, or empty when this
		/// ground has nothing to say about power.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Ground to report on; must be the kingdom's own claimed zone.</param>
		public static string StatusLine(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return "";
			}
			List<GameObject> works = new List<GameObject>();
			List<GameObject> stores = new List<GameObject>();
			List<GameObject> sinks = new List<GameObject>();
			Gather(Z, works, stores, sinks);
			int perDay = 0;
			string reason = KingdomPowerRules.IdleNoWorks;
			for (int i = 0; i < works.Count; i++)
			{
				int output = DailyOutput(works[i], Z, 1, out var source);
				if (output > 0)
				{
					perDay += output;
				}
				else if (perDay <= 0)
				{
					reason = KingdomPowerRules.IdleReason(source);
				}
			}
			KingdomPowerRules.SupplyTier tier = KingdomPowerRules.ClassifySupply(perDay, works.Count, sinks.Count);
			return KingdomPowerRules.SupplyLine(tier, perDay, Held(stores), Capacity(stores), reason);
		}

		private static void Gather(Zone Z, List<GameObject> Works, List<GameObject> Stores, List<GameObject> Sinks)
		{
			foreach (GameObject item in Z.GetObjects())
			{
				// The founder's designation is the whole of the grid's membership: what the
				// settlement raised, plus anything explicitly dedicated to it. Nothing the
				// player merely left lying about is ever charged, moved, or read.
				if (item.GetIntProperty("KingdomBuilt") != 1 && item.GetIntProperty("KingdomGrid") != 1)
				{
					continue;
				}
				if (item.GetPart<r_KingdomPowerWork>() != null)
				{
					Works.Add(item);
				}
				else if (item.GetPart<r_KingdomPowerStore>() != null && item.GetPart<Capacitor>() != null)
				{
					Stores.Add(item);
				}
				else if (ChargeAvailableEvent.Wanted(item))
				{
					Sinks.Add(item);
				}
			}
		}

		/// <summary>
		/// Says once why a work is standing still, and unsays it when it turns again.
		/// <para>
		/// This used to live inside the per-work credit loop, which is where power's second
		/// accounting lived. The TELLING survives the migration unchanged &mdash; STANDARDS 7b's
		/// applicable-but-blocked rule is about the founder, not about the arithmetic &mdash; and
		/// what left with the loop is the day counting and the summing.
		/// </para>
		/// </summary>
		private static void Dry(KingdomSystem System, GameObject Work, int Output, KingdomPowerRules.PowerSource Source)
		{
			r_KingdomPowerWork part = Work.GetPart<r_KingdomPowerWork>();
			if (part == null)
			{
				return;
			}
			if (Output > 0)
			{
				part.DryAnnounced = false;
				return;
			}
			if (part.DryAnnounced)
			{
				return;
			}
			part.DryAnnounced = true;
			System.Ledger.Note("{{r|" + KingdomPowerRules.IdleReason(Source) + "}}");
		}

		/// <summary>
		/// What one work makes in a day right now: its rating, cut by the crew the settlement
		/// gave it, by how worn it is, and by what the ground or the sky is giving it.
		/// </summary>
		private static int DailyOutput(GameObject Work, Zone Z, int Days, out KingdomPowerRules.PowerSource Source)
		{
			Source = KingdomPowerRules.PowerSource.Hands;
			r_KingdomPowerWork part = Work.GetPart<r_KingdomPowerWork>();
			if (part == null || !KingdomPowerRules.TryParseSource(part.Source, out Source))
			{
				return 0;
			}
			// What the work is actually managing: the fraction of the hands it asked for that it
			// got, or - for a work that asked for nobody - its own condition. Damage dims a power
			// work in proportion, staffed or not (Addendum 10(b): "solar panels reduce power
			// output"), and it reaches the staffed ones too. It did not before: this file runs
			// inside the growth pass and KingdomEffectiveness is the staffing pass's crew stretch
			// at that point, so a half-wrecked mill made a whole mill's charge.
			int crew = KingdomWear.EffectivenessOf(Work);
			int available;
			switch (Source)
			{
			case KingdomPowerRules.PowerSource.Water:
				available = KingdomPowerRules.WaterAvailabilityPercent(OpenWaterBeside(Work));
				break;
			case KingdomPowerRules.PowerSource.Wind:
				available = KingdomPowerRules.WindAvailabilityPercent(Z.CurrentWindSpeed, Days);
				break;
			default:
				available = 100;
				break;
			}
			return KingdomPowerRules.DailyOutput(Source, crew, available);
		}

		/// <summary>
		/// Open water standing in and beside a wheel's cell. Open pools only &mdash; a wheel is
		/// turned by a brook, never by the settlement's cisterns, which hold what the people
		/// drink and are not a fuel.
		/// </summary>
		private static int OpenWaterBeside(GameObject Work)
		{
			Cell cell = Work.CurrentCell;
			if (cell == null)
			{
				return 0;
			}
			int total = OpenWaterIn(cell);
			List<Cell> adjacent = cell.GetLocalAdjacentCells();
			if (adjacent != null)
			{
				for (int i = 0; i < adjacent.Count; i++)
				{
					total += OpenWaterIn(adjacent[i]);
				}
			}
			return total;
		}

		private static int OpenWaterIn(Cell C)
		{
			int total = 0;
			for (int i = 0; i < C.Objects.Count; i++)
			{
				LiquidVolume volume = C.Objects[i].GetPart<LiquidVolume>();
				if (volume != null && volume.MaxVolume < 0 && volume.Volume > 0)
				{
					total += volume.Volume;
				}
			}
			return total;
		}

		/// <summary>
		/// Offers charge to everything the solve did not stop, and returns what was actually taken.
		/// <para>
		/// The model decides <b>how much</b> and <b>to whom</b>; this only lands it and measures
		/// the landing. A sink the brownout order stopped is skipped entirely rather than offered
		/// a smaller share: works stop whole, because a half-lit forge is not a thing a founder can
		/// see or reason about.
		/// </para>
		/// </summary>
		/// <param name="Allocated">What the solve said would reach a sink.</param>
		/// <param name="Pool">What is actually on hand &mdash; the generated charge plus whatever
		/// the stores gave back, both measured. The offer is the lesser of the two, so a store
		/// that would not release as much as the model expected cannot conjure the difference.</param>
		private static int Deliver(KingdomSystem System, List<GameObject> Sinks, KingdomFlowDemand[] Demands, int[] Order, int Stopped, int Allocated, int Pool)
		{
			int remaining = (Allocated < Pool) ? Allocated : Pool;
			if (remaining <= 0)
			{
				return 0;
			}
			int offered = remaining;
			bool[] quiet = new bool[Sinks.Count];
			for (int i = 0; i < Stopped && i < Order.Length; i++)
			{
				if (Order[i] >= 0 && Order[i] < quiet.Length)
				{
					quiet[Order[i]] = true;
				}
			}
			for (int i = 0; i < Sinks.Count && remaining > 0; i++)
			{
				GameObject sink = Sinks[i];
				if (quiet[i] || !GameObject.Validate(sink))
				{
					continue;
				}
				// Forced, because a settlement pass is a day's work arriving at once, not one
				// turn's trickle: without it a Capacitor clamps intake to its per-turn ChargeRate
				// (Capacitor.Process, IInitialChargeProductionEvent) and a day's milling would be
				// throttled to a single turn's worth. The return is the event's own accumulated
				// delta (StartingAmount - Amount), not a convenience flag, but it is clamped
				// here anyway rather than trusted on its face.
				int used = sink.ChargeAvailable(remaining, 0L, 1, Forced: true);
				if (used <= 0)
				{
					continue;
				}
				if (used > remaining)
				{
					used = remaining;
				}
				remaining -= used;
				// Latched on the object rather than the settlement, so each thing the works
				// reach announces its own first charge once and never again - and so a dormant
				// city's posts keep their own memory of it without a field on the system.
				if (sink.GetIntProperty("KingdomPowered") != 1)
				{
					sink.SetIntProperty("KingdomPowered", 1);
					string drawing = KingdomDesign.ReferenceFor(sink, sink.ShortDisplayName);
					System.RecordDeed("the " + drawing + " of " + System.KingdomDisplayName + " drawing its first charge");
					KingdomChronicle.Record(System, "the works turned at " + System.KingdomDisplayName + ", and " + XRL.Language.Grammar.A(drawing) + " drew its first charge from hands and weather alone", Accomplishment: true);
					MessageQueue.AddPlayerMessage("{{G|The works of " + System.KingdomDisplayName + " are turning. The " + drawing + " draws from them.}}");
				}
			}
			return offered - remaining;
		}

		private static int Capacity(List<GameObject> Stores)
		{
			int total = 0;
			for (int i = 0; i < Stores.Count; i++)
			{
				Capacitor capacitor = Stores[i].GetPart<Capacitor>();
				if (capacitor != null && capacitor.MaxCharge > 0)
				{
					total += capacitor.MaxCharge;
				}
			}
			return total;
		}

		private static int Held(List<GameObject> Stores)
		{
			int total = 0;
			for (int i = 0; i < Stores.Count; i++)
			{
				Capacitor capacitor = Stores[i].GetPart<Capacitor>();
				if (capacitor != null && capacitor.Charge > 0)
				{
					total += capacitor.Charge;
				}
			}
			return total;
		}

		/// <summary>
		/// Pours charge into the stores and returns what actually went in, measured from the
		/// capacitors before and after rather than taken on the word of anything that was
		/// called. Never exceeds a store's own MaxCharge.
		/// </summary>
		private static int Deposit(List<GameObject> Stores, int Amount)
		{
			int remaining = Amount;
			for (int i = 0; i < Stores.Count && remaining > 0; i++)
			{
				Capacitor capacitor = Stores[i].GetPart<Capacitor>();
				if (capacitor == null || capacitor.Charge >= capacitor.MaxCharge)
				{
					continue;
				}
				int before = capacitor.Charge;
				capacitor.AddCharge(remaining);
				int added = capacitor.Charge - before;
				if (added > 0)
				{
					remaining -= added;
				}
			}
			return Amount - remaining;
		}

		/// <summary>Draws charge back out of the stores, measured the same way.</summary>
		private static int Withdraw(List<GameObject> Stores, int Amount)
		{
			int remaining = Amount;
			for (int i = 0; i < Stores.Count && remaining > 0; i++)
			{
				Capacitor capacitor = Stores[i].GetPart<Capacitor>();
				if (capacitor == null || capacitor.Charge <= 0)
				{
					continue;
				}
				int before = capacitor.Charge;
				capacitor.UseCharge(remaining);
				int taken = before - capacitor.Charge;
				if (taken > 0)
				{
					remaining -= taken;
				}
			}
			return Amount - remaining;
		}

		/// <summary>
		/// Writes the moment a bed of salt first ran full, once, and never again. Named
		/// parameters rather than a re-survey: the caller has just measured both figures.
		/// </summary>
		private static void NoteStoreFilled(KingdomSystem System, List<GameObject> Stores, int HeldCharge, int TotalCapacity)
		{
			if (TotalCapacity <= 0 || HeldCharge < TotalCapacity)
			{
				return;
			}
			for (int i = 0; i < Stores.Count; i++)
			{
				r_KingdomPowerStore store = Stores[i].GetPart<r_KingdomPowerStore>();
				if (store == null || store.EverFilled)
				{
					continue;
				}
				store.EverFilled = true;
				KingdomChronicle.Record(System, "the salt at " + System.KingdomDisplayName + " ran full and bright, and the settlement kept its first whole night of light");
				System.Ledger.Note("{{G|The molten-salt store is full. The settlement keeps the night now.}}");
				// One telling per pass, however many beds of salt the settlement keeps.
				return;
			}
		}
	}
}
