namespace ThousandAndFirst.Simulation.City
{
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
	internal static partial class KingdomFlowRules
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
	}
}
