namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomFlowRules
	{

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
