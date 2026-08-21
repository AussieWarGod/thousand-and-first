namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One sink as the solve sees it: which work, where it sits on the brownout ladder, and what it
	/// can actually draw in a day once the narrowest segment between it and a source has had its
	/// say.
	/// <para>
	/// The demand is <b>already bottleneck-limited</b> by
	/// <c>KingdomNetworkGraph.TryBottleneck</c> before it reaches here. That split is deliberate:
	/// the graph knows about pipes and this file knows about arithmetic, and neither has to know
	/// the other's shape.
	/// </para>
	/// </summary>
	internal readonly struct KingdomFlowDemand
	{
		internal readonly int WorkId;

		internal readonly KingdomWorkTier Tier;

		internal readonly int PerDay;

		internal KingdomFlowDemand(int workId, KingdomWorkTier tier, int perDay)
		{
			WorkId = workId;
			Tier = tier;
			PerDay = (perDay > 0) ? perDay : 0;
		}
	}

	/// <summary>
	/// What one span did to one network. Every figure is in the network's own unit &mdash; vanilla
	/// charge for the power families, drams for a liquid line &mdash; and the whole of it satisfies
	/// one identity, which is what makes it checkable rather than merely plausible:
	/// <code>
	/// Generated + Discharged == Delivered + Charged + Spilled
	/// </code>
	/// </summary>
	internal readonly struct KingdomFlowSolution
	{
		/// <summary>What the sources made, throttled by their own segments.</summary>
		internal readonly long Generated;

		/// <summary>What the sinks asked for, throttled by theirs.</summary>
		internal readonly long Demanded;

		/// <summary>What actually reached a sink. Below <see cref="Demanded"/> exactly when
		/// something stopped.</summary>
		internal readonly long Delivered;

		/// <summary>What went into the stores.</summary>
		internal readonly long Charged;

		/// <summary>What came back out of them.</summary>
		internal readonly long Discharged;

		/// <summary>What was made with nowhere to put it. Loss, never a queue &mdash; the same
		/// ruling <c>KingdomProductionRules</c> makes about a harvest over a full granary.</summary>
		internal readonly long Spilled;

		/// <summary>How far short the span ran once the stores had given everything they could.
		/// The brownout's size, and the figure the stop loop is asked to cover.</summary>
		internal readonly long Shortfall;

		/// <summary>How many sinks went quiet, counted off the front of the brownout order.</summary>
		internal readonly int Stopped;

		internal KingdomFlowSolution(long generated, long demanded, long delivered, long charged, long discharged, long spilled, long shortfall, int stopped)
		{
			Generated = generated;
			Demanded = demanded;
			Delivered = delivered;
			Charged = charged;
			Discharged = discharged;
			Spilled = spilled;
			Shortfall = shortfall;
			Stopped = stopped;
		}

		/// <summary>Whether anything went quiet. The predicate the happening is generated from.</summary>
		internal bool Brownout
		{
			get { return Stopped > 0; }
		}

		internal static KingdomFlowSolution None
		{
			get { return new KingdomFlowSolution(0L, 0L, 0L, 0L, 0L, 0L, 0L, 0); }
		}
	}

	/// <summary>
	/// The closed-form flow solve, netted per network, and the brownout ladder it falls back on.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.11. Between two breakpoints every rate in the model is
	/// constant (&sect;2.3), so a network's behaviour over an interval is arithmetic and not a
	/// simulation:
	/// </para>
	/// <code>
	/// surplus = &Sigma; source rates (each scaled by its own condition, Addendum 10(b))
	///         - &Sigma; sink demands
	/// surplus &gt;= 0 -&gt; stores charge, capped by headroom over the interval
	/// surplus &lt;  0 -&gt; stores discharge; when they empty, BROWNOUT
	/// </code>
	/// <para>
	/// <b>No term in the elapsed.</b> Days multiply the rates once and appear nowhere else, so a
	/// one-day span and a ninety-day span cost identical work &mdash; &sect;0.0(a)'s identity, kept
	/// here as it is kept in the reckoning.
	/// </para>
	/// <para>
	/// <b>Additive across a split, exactly where it must be.</b> With the store neither filling nor
	/// emptying inside the span, solving <c>[a,c]</c> equals solving <c>[a,b]</c> then
	/// <c>[b,c]</c>: every clamp scales with the days and the stop set is decided on per-day
	/// figures. The two moments where that fails &mdash; a store hitting full, a store hitting
	/// empty &mdash; are precisely the breakpoints <see cref="TryStoreCrossing"/> proposes, which is
	/// what makes the breakpoint loop the correct integration of this rather than an approximation
	/// of it.
	/// </para>
	/// <para>
	/// Pure and engine-free, total over representable input, and no draw anywhere: a brownout is
	/// arithmetic and a ladder, never chance.
	/// </para>
	/// </summary>
	internal static class KingdomFlowRules
	{
		/// <summary>
		/// The order works go quiet in, and it is stated rather than emergent:
		/// <b>lower tier first, and within a tier the higher work id first.</b>
		/// <para>
		/// The tier ladder and its justification live on <see cref="KingdomWorkTier"/>. The
		/// tie-break is the other half of the design statement: the <i>newest-built</i> work goes
		/// quiet before the oldest, because a work id is minted in build order and rises. It is
		/// stable, stored, needs no draw, survives a reload, and reads right &mdash; a city protects
		/// what it has had longest. It is also the exact mirror of <c>KingdomDrainRules</c>' ruling
		/// one lane over, where the OLDEST dedication is drained first and the founder's newest
		/// gift is the reserve: both say the same thing about a settlement, from the two ends.
		/// </para>
		/// <para>
		/// A selection sort over a bounded row set, written out rather than delegated to a
		/// comparer, because the comparison IS the invariant and an indirection would hide it.
		/// </para>
		/// </summary>
		/// <param name="order">Filled with indices into <paramref name="demands"/>, worst-served
		/// first.</param>
		internal static bool TryBrownoutOrder(KingdomFlowDemand[] demands, int count, int[] order, out KingdomCityFault fault)
		{
			if (demands == null || order == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > demands.Length || count > order.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			fault = KingdomCityFault.None;
			for (int i = 0; i < count; i++)
			{
				order[i] = i;
			}
			for (int i = 0; i < count; i++)
			{
				int pick = i;
				for (int j = i + 1; j < count; j++)
				{
					KingdomFlowDemand candidate = demands[order[j]];
					KingdomFlowDemand best = demands[order[pick]];
					if (candidate.Tier < best.Tier || (candidate.Tier == best.Tier && candidate.WorkId > best.WorkId))
					{
						pick = j;
					}
				}
				if (pick != i)
				{
					int swap = order[i];
					order[i] = order[pick];
					order[pick] = swap;
				}
			}
			return true;
		}

		/// <summary>
		/// One network, one span, netted.
		/// <para>
		/// The order of the two remedies is the design and not an implementation detail:
		/// <b>the stores are spent before anything stops.</b> &sect;3.11 says it in one line —
		/// <i>"stores discharge; when they empty, BROWNOUT"</i> — and it is what a bed of molten
		/// salt is FOR. A city that let its forge go quiet while the salt was still hot would be
		/// telling the founder their store was decorative.
		/// </para>
		/// </summary>
		/// <param name="supplyPerDay">&Sigma; over sources of <c>min(rate, its bottleneck)</c>.</param>
		/// <param name="demands">The sinks, in any order the caller likes.</param>
		/// <param name="order">The brownout order, from <see cref="TryBrownoutOrder"/>. Must index
		/// <paramref name="demands"/>; a null order is a fault rather than an excuse to stop things
		/// in array order.</param>
		/// <param name="storedLevel">What the network's stores hold. Seeded from the ground at
		/// check-in (&sect;3.1) &mdash; the ground wins.</param>
		/// <param name="storeThroughputPerDay">What the stores can take in, or give back, in a day.
		/// A store is never a bucket that empties in an instant.</param>
		/// <param name="days">Whole world-day boundaries, from
		/// <c>KingdomProductionRules.TryDaysBetween</c>. <b>The one clock</b>: nothing here counts
		/// a day of its own.</param>
		internal static bool TrySolve(
			long supplyPerDay,
			KingdomFlowDemand[] demands,
			int demandCount,
			int[] order,
			long storedLevel,
			long storedCapacity,
			long storeThroughputPerDay,
			long days,
			out KingdomFlowSolution solution,
			out KingdomCityFault fault)
		{
			solution = KingdomFlowSolution.None;
			if (demands == null || order == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (demandCount < 0 || demandCount > demands.Length || demandCount > order.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (days < 0L)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			if (supplyPerDay < 0L || storeThroughputPerDay < 0L)
			{
				fault = KingdomCityFault.InvalidRate;
				return false;
			}
			if (storedCapacity < 0L || storedLevel < 0L || storedLevel > storedCapacity)
			{
				fault = KingdomCityFault.InvalidCapacity;
				return false;
			}
			fault = KingdomCityFault.None;
			if (days == 0L)
			{
				return true;
			}
			long demandPerDay = 0L;
			for (int i = 0; i < demandCount; i++)
			{
				if (order[i] < 0 || order[i] >= demandCount)
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				demandPerDay += demands[i].PerDay;
			}
			// Guarded once rather than at every partial sum: every figure below is bounded by one
			// of these three, so proving these three fit proves the whole solve fits.
			long generated;
			long demanded;
			long throughput;
			if (!TryScale(supplyPerDay, days, out generated)
				|| !TryScale(demandPerDay, days, out demanded)
				|| !TryScale(storeThroughputPerDay, days, out throughput))
			{
				fault = KingdomCityFault.ArithmeticOverflow;
				return false;
			}
			long headroom = storedCapacity - storedLevel;
			long chargeCap = (headroom < throughput) ? headroom : throughput;
			long dischargeCap = (storedLevel < throughput) ? storedLevel : throughput;
			long deficit = demanded - generated;
			long shortfall = deficit - dischargeCap;
			if (shortfall < 0L)
			{
				shortfall = 0L;
			}
			long relieved = 0L;
			int stopped = 0;
			// Works stop WHOLE. A half-lit forge is not a thing a founder can see or reason about,
			// and a fractional stop would make the ladder unreadable: the point of a stated order is
			// that the founder can name which work goes next.
			while (stopped < demandCount && relieved < shortfall)
			{
				relieved += (long)demands[order[stopped]].PerDay * days;
				stopped++;
			}
			long delivered = demanded - relieved;
			if (delivered < 0L)
			{
				delivered = 0L;
			}
			long net = generated - delivered;
			long charged = 0L;
			long discharged = 0L;
			long spilled = 0L;
			if (net >= 0L)
			{
				charged = (net < chargeCap) ? net : chargeCap;
				spilled = net - charged;
			}
			else
			{
				// Bounded by construction, and worth saying why rather than clamping and hoping:
				// -net = deficit - relieved, and the loop above ran until relieved >= shortfall =
				// deficit - dischargeCap, so -net <= dischargeCap. The store is never overdrawn.
				discharged = -net;
			}
			solution = new KingdomFlowSolution(generated, demanded, delivered, charged, discharged, spilled, shortfall, stopped);
			return true;
		}

		/// <summary>
		/// When this network's store next fills or empties at the current net rate &mdash; the
		/// breakpoint that makes <see cref="TrySolve"/> exact across a long span rather than
		/// approximately right.
		/// <para>
		/// <b>Deliberately not a second implementation.</b> It is
		/// <c>KingdomAdvanceRules.TryCrossingTicks</c>, which already answers "when does this level
		/// hit a bound at this rate" for every stock row in the model. A network store is a stock
		/// row wearing a different hat, and two answers to one question is exactly the thing
		/// &sect;3.11's migration exists to stop.
		/// </para>
		/// </summary>
		/// <param name="netPerDay">Supply minus demand, per day. Positive fills the store,
		/// negative empties it, zero proposes nothing.</param>
		internal static bool TryStoreCrossing(
			long storedLevel,
			long storedCapacity,
			long netPerDay,
			long ticksPerDay,
			long fromTick,
			out KingdomBreakpoint breakpoint,
			out KingdomCityFault fault)
		{
			breakpoint = KingdomBreakpoint.None;
			long ticksUntil;
			KingdomBreakpointKind kind;
			// The crossing solver's own contract, carried across unchanged: false with
			// KingdomCityFault.None means "this level is not going anywhere, so it will not
			// arrive", which is an ordinary answer and not a fault. Callers distinguish the two by
			// reading the fault, exactly as they do one lane over.
			if (!KingdomAdvanceRules.TryCrossingTicks(storedLevel, storedCapacity, netPerDay, ticksPerDay, out ticksUntil, out kind, out fault))
			{
				return false;
			}
			if (fromTick < 0L || ticksUntil > long.MaxValue - fromTick)
			{
				fault = KingdomCityFault.ArithmeticOverflow;
				return false;
			}
			breakpoint = new KingdomBreakpoint(kind, fromTick + ticksUntil, -1);
			return true;
		}

		/// <summary>
		/// What a founder is told when the lights go down, once.
		/// <para>
		/// STANDARDS 7b: <i>applicable but blocked</i> announces, and announces <b>once</b>. The
		/// latch lives on the thing that went quiet, not on the settlement, so each work remembers
		/// its own telling and a dormant city keeps that memory with no field on the system &mdash;
		/// the idiom <c>r_KingdomPowerWork.DryAnnounced</c> already uses.
		/// </para>
		/// <para>
		/// <b>Recovery says nothing, and that is the rule rather than an omission</b>
		/// (Addendum 12(c), felt-and-announced): the latch is UNSAID when supply returns, so the
		/// next failure can be told again, and no line is written for the good news. A settlement
		/// that announced every recovery would be a settlement that talks about itself constantly,
		/// and 7b's whole complaint is about the founder being unable to find the one line that
		/// mattered.
		/// </para>
		/// </summary>
		/// <param name="workName">What went quiet, in the founder's own words for it.</param>
		internal static string BrownoutNotice(string workName)
		{
			string named = string.IsNullOrEmpty(workName) ? "a work" : workName;
			return "The " + named + " has gone quiet. There is not enough to go round, and it is the first thing this city gives up.";
		}

		/// <summary>The same moment, dated, for the chronicle &mdash; where a founder three zones
		/// away reads it at the homecoming.</summary>
		internal static string BrownoutTelling(string workName, string cityName)
		{
			string named = string.IsNullOrEmpty(workName) ? "a work" : workName;
			string city = string.IsNullOrEmpty(cityName) ? "the city" : cityName;
			return "the " + named + " of " + city + " went quiet, the lines running thin and the salt cold";
		}

		/// <summary>
		/// The ladder in one line, for a founder who wants to know what goes next. Read off
		/// <see cref="KingdomWorkTier"/> in its own order, never restated as a literal, so the
		/// enum stays the single place the order is written.
		/// </summary>
		internal static string LadderLine()
		{
			return "When the lines run thin, works stop in this order: "
				+ TierName(KingdomWorkTier.Industry) + ", then "
				+ TierName(KingdomWorkTier.Refining) + ", then "
				+ TierName(KingdomWorkTier.Amenity) + ", then "
				+ TierName(KingdomWorkTier.Food) + ", then "
				+ TierName(KingdomWorkTier.Water) + ", and last of all "
				+ TierName(KingdomWorkTier.Watch) + ". The newest built goes before the oldest.";
		}

		/// <summary>A tier as a founder would name it.</summary>
		internal static string TierName(KingdomWorkTier tier)
		{
			switch (tier)
			{
			case KingdomWorkTier.Industry:
				return "the forges and the workshops";
			case KingdomWorkTier.Refining:
				return "the yards that refine";
			case KingdomWorkTier.Amenity:
				return "comfort and lodging";
			case KingdomWorkTier.Food:
				return "the food works";
			case KingdomWorkTier.Water:
				return "the water works";
			default:
				return "the watch";
			}
		}

		/// <summary>
		/// Where a design sits on the brownout ladder, read off the catalogue's own
		/// <c>Category</c> vocabulary rather than off a second table nobody would keep in step.
		/// <para>
		/// The ten categories <c>KingdomBuildings.xml</c> actually uses map onto six rungs. An
		/// unknown category &mdash; a third party's own, arriving through the extension API
		/// (&sect;5) &mdash; lands on <see cref="KingdomWorkTier.Amenity"/>, the middle rung:
		/// a stranger's work is neither the first thing this city gives up nor the last, which is
		/// the only honest default when we do not know what it does.
		/// </para>
		/// </summary>
		internal static KingdomWorkTier TierOfCategory(string category)
		{
			if (string.IsNullOrEmpty(category))
			{
				return KingdomWorkTier.Amenity;
			}
			switch (category.Trim().ToLowerInvariant())
			{
			case "craft":
				return KingdomWorkTier.Industry;
			case "knowledge":
				return KingdomWorkTier.Refining;
			case "housing":
			case "civic":
			case "faith":
			case "memorial":
				return KingdomWorkTier.Amenity;
			case "food":
				return KingdomWorkTier.Food;
			case "storage":
			case "power":
				return KingdomWorkTier.Water;
			case "defense":
				return KingdomWorkTier.Watch;
			default:
				return KingdomWorkTier.Amenity;
			}
		}

		/// <summary>
		/// Which way a liquid line runs, and how far, in closed form.
		/// <para>
		/// <b>A line runs downhill and stops level.</b> That is the whole verb, and it is the one
		/// thing the hydraulic family does not do for us: vanilla's pipes hold working fluid and
		/// <c>MingleAdjacent</c> equalises with a <i>directly adjacent</i> volume
		/// (<c>D/XRL/World/Parts/IPowerTransmission.cs:1605</c>,
		/// <c>D/XRL/World/Parts/LiquidVolume.cs:5973</c>) and routes nothing. Ours routes, and
		/// routing is all it adds.
		/// </para>
		/// <para>
		/// The amount is solved rather than stepped. Moving <c>m</c> from a vessel at
		/// <c>Lf / Cf</c> into one at <c>Lt / Ct</c> levels them when
		/// <c>(Lf - m) / Cf == (Lt + m) / Ct</c>, which rearranges to
		/// <c>m = (Ct&middot;Lf - Cf&middot;Lt) / (Cf + Ct)</c>. One expression, no loop, no draw,
		/// and it cannot overshoot into an inverted pair &mdash; a founder watching a main run
		/// between two cisterns sees them come level and stop, which is what a main does.
		/// </para>
		/// <para>
		/// Ends are chosen by <b>fill fraction</b>, compared by cross-multiplication so that no
		/// division rounds a choice: fullest gives, emptiest takes, ties broken on the lower index
		/// of the caller's own list. Deterministic across a reload, because every key is a stored
		/// figure.
		/// </para>
		/// </summary>
		/// <param name="zoneIndices">The zone rows this network actually reaches, in the caller's
		/// order. The tie-break is that order, so it must be stable &mdash; it is, because it comes
		/// off the graph's own node array.</param>
		/// <param name="budget">What the line will carry over this span: its bottleneck times the
		/// days. Zero moves nothing, which is a level answer and not a fault.</param>
		internal static bool TryChooseDownhill(
			KingdomCityState state,
			KingdomStockKind kind,
			int[] zoneIndices,
			int count,
			long budget,
			out int from,
			out int to,
			out long amount,
			out KingdomCityFault fault)
		{
			from = -1;
			to = -1;
			amount = 0L;
			if (state == null || zoneIndices == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > zoneIndices.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			fault = KingdomCityFault.None;
			if (count < 2 || budget <= 0L)
			{
				return true;
			}
			int fullest = -1;
			int emptiest = -1;
			long fullLevel = 0L;
			long fullCap = 0L;
			long lowLevel = 0L;
			long lowCap = 0L;
			for (int i = 0; i < count; i++)
			{
				KingdomZoneRow row;
				if (!state.TryZone(zoneIndices[i], out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				KingdomStockPair pair;
				if (!row.Stocks.TryGet(kind, out pair) || pair.Capacity <= 0L)
				{
					// A zone with no vessels for this kind is on the line and holds nothing on it.
					// Not a fault: a length of main across bare ground is an ordinary thing to lay.
					continue;
				}
				if (fullest < 0 || pair.Level * fullCap > fullLevel * pair.Capacity)
				{
					fullest = zoneIndices[i];
					fullLevel = pair.Level;
					fullCap = pair.Capacity;
				}
				if (emptiest < 0 || pair.Level * lowCap < lowLevel * pair.Capacity)
				{
					emptiest = zoneIndices[i];
					lowLevel = pair.Level;
					lowCap = pair.Capacity;
				}
			}
			if (fullest < 0 || emptiest < 0 || fullest == emptiest)
			{
				return true;
			}
			long uphill = lowCap * fullLevel - fullCap * lowLevel;
			if (uphill <= 0L)
			{
				// Already level, or the "fullest" is not actually fuller. Nothing runs.
				return true;
			}
			long level = uphill / (fullCap + lowCap);
			if (level <= 0L)
			{
				return true;
			}
			from = fullest;
			to = emptiest;
			amount = (level < budget) ? level : budget;
			return true;
		}

		/// <summary>One rate times one day count, or a refusal. Never a saturation: a saturated
		/// flow figure would be a lie the conservation identity could not catch.</summary>
		private static bool TryScale(long perDay, long days, out long total)
		{
			total = 0L;
			if (perDay < 0L || days < 0L)
			{
				return false;
			}
			if (perDay != 0L && days > long.MaxValue / perDay)
			{
				return false;
			}
			total = perDay * days;
			return true;
		}
	}
}
