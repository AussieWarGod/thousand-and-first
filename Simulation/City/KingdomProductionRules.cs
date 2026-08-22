namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What one span of production did to one stock row: what landed in the book, and what the
	/// ground now owes for it.
	/// </summary>
	internal readonly struct KingdomProductionStep
	{
		/// <summary>The row's level after the span, clamped to the row's own capacity.</summary>
		internal readonly long NextLevel;

		/// <summary>The row's signed debt after the span. Moved by exactly the same amount the
		/// level moved, which is the whole of invariant I1.</summary>
		internal readonly int NextOwed;

		/// <summary>What the level actually moved by. Signed.</summary>
		internal readonly long Landed;

		/// <summary>
		/// What the rate promised and the capacity refused. A harvest with nowhere to go is left
		/// in the field (the same loss <c>KingdomGrowth.StoreHarvest</c> has always taken), and it
		/// is reported rather than silently absorbed.
		/// </summary>
		internal readonly long Spilled;

		internal KingdomProductionStep(long nextLevel, int nextOwed, long landed, long spilled)
		{
			NextLevel = nextLevel;
			NextOwed = nextOwed;
			Landed = landed;
			Spilled = spilled;
		}
	}

	/// <summary>
	/// Per-zone production, integrated in closed form, billed exactly once.
	/// <para>
	/// <b>Why this exists as its own file.</b> W1 shipped the breakpoint integration with a net
	/// rate of zero and said why: <i>"the attended pass already credits the seated zone's works
	/// for the settlement's whole elapsed (<c>KingdomGrowth</c>'s <c>LastWaterWorkTick</c> is a
	/// settlement stamp, not a zone one), so a model that also credited them here would pay the
	/// same day twice."</i> W6 owns the rates, and the way it makes double-billing
	/// <b>unrepresentable</b> rather than merely avoided is this: there is now exactly ONE clock
	/// that days are counted off, and exactly ONE place a made dram is written down.
	/// </para>
	/// <para>
	/// <b>The clock: world-day boundaries, never elapsed remainders.</b>
	/// <see cref="DaysBetween"/> counts the day boundaries of world time that a span crosses, so
	/// <c>Days(a,b) + Days(b,c) == Days(a,c)</c> for every split — the property a breakpoint
	/// integration needs and the property an "elapsed / TicksPerDay" count does <b>not</b> have.
	/// Under an elapsed count, a reckon whose horizon fell mid-day would drop the remainder and a
	/// founder who walked in twice a day would produce nothing for ever; under this one, a day is
	/// paid on the boundary it crosses and no split of the same span can pay it twice or lose it.
	/// LIVING-CITY-ARCHITECTURE &sect;2.3.
	/// </para>
	/// <para>
	/// <b>The ledger: level and debt move together.</b> Production is not a transfer, so it cannot
	/// be booked the way &sect;3.9's carry is; but it must still satisfy invariant I1 —
	/// <i>model total == ground total + counter-owed, per stock kind, at every instant</i>. It does,
	/// because <see cref="TryProduce"/> raises the row's level and the row's debt against real
	/// containers by the same clamped amount. The ground has not changed and the identity
	/// <c>level - owed == ground</c> is undisturbed: what the works made is a claim on a vessel
	/// that nobody has poured yet, and &sect;3.5's amortised reify is what pours it.
	/// </para>
	/// <para>
	/// Pure and engine-free, and total over representable input.
	/// </para>
	/// </summary>
	internal static class KingdomProductionRules
	{
		/// <summary>
		/// World-day boundaries strictly after <paramref name="fromTick"/> and at or before
		/// <paramref name="toTick"/>.
		/// <para>
		/// Additive across any split of the span, which is what lets the breakpoint loop apply a
		/// rate in pieces and reach the same total as one closing pass — and what makes
		/// "reckon twice at the same tick" a no-op rather than a second day's pay.
		/// </para>
		/// </summary>
		internal static bool TryDaysBetween(long fromTick, long toTick, long ticksPerDay, out long days, out KingdomCityFault fault)
		{
			days = 0L;
			if (ticksPerDay <= 0L)
			{
				fault = KingdomCityFault.InvalidInterval;
				return false;
			}
			if (fromTick < 0L || toTick < 0L)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			if (toTick < fromTick)
			{
				fault = KingdomCityFault.ClockRegression;
				return false;
			}
			fault = KingdomCityFault.None;
			days = (toTick / ticksPerDay) - (fromTick / ticksPerDay);
			return true;
		}

		/// <summary>
		/// One span of one zone's making, posted to the row.
		/// <para>
		/// A zero rate is a no-op and not a fault: a zone with no works is a legal zone. A row
		/// whose capacity is already met takes nothing and reports the difference as
		/// <see cref="KingdomProductionStep.Spilled"/> — never as a queue, because a granary with
		/// no room does not remember the harvest it could not hold.
		/// </para>
		/// </summary>
		/// <param name="level">The row's level now.</param>
		/// <param name="capacity">The row's capacity now.</param>
		/// <param name="owed">The row's signed debt now.</param>
		/// <param name="ratePerDay">What this zone's works make in a day. Negative is legal and
		/// means a lane that consumes; it drains the level and the debt together, exactly as a
		/// positive rate fills them.</param>
		/// <param name="days">Whole days, from <see cref="TryDaysBetween"/>.</param>
		internal static bool TryProduce(long level, long capacity, int owed, long ratePerDay, long days, out KingdomProductionStep step, out KingdomCityFault fault)
		{
			step = new KingdomProductionStep(level, owed, 0L, 0L);
			if (capacity < 0L || level < 0L || level > capacity)
			{
				fault = KingdomCityFault.InvalidCapacity;
				return false;
			}
			if (days < 0L)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			fault = KingdomCityFault.None;
			if (days == 0L || ratePerDay == 0L)
			{
				return true;
			}
			long magnitude = (ratePerDay > 0L) ? ratePerDay : -ratePerDay;
			if (days > long.MaxValue / magnitude)
			{
				fault = KingdomCityFault.ArithmeticOverflow;
				return false;
			}
			long wanted = days * ratePerDay;
			long room = (wanted > 0L) ? (capacity - level) : level;
			long moved = (wanted > 0L)
				? ((wanted < room) ? wanted : room)
				: ((-wanted < room) ? wanted : -room);
			long nextLevel = level + moved;
			long nextOwed = (long)owed + moved;
			if (nextOwed > int.MaxValue || nextOwed < int.MinValue)
			{
				fault = KingdomCityFault.ArithmeticOverflow;
				return false;
			}
			step = new KingdomProductionStep(nextLevel, (int)nextOwed, moved, wanted - moved);
			return true;
		}

		// --- The third factor (RESEARCH-SYSTEM-DESIGN §8.2) ----------------------------------

		/// <summary>
		/// What method is worth to a realm that has researched nothing: exactly what it was worth
		/// before the tree existed. <c>KingdomResearchRules.MethodPercent</c> returns this for an
		/// empty roster, and the two agreeing is what the baseline-unchanged test holds.
		/// </summary>
		internal const int BaselineMethodPercent = 100;

		/// <summary>
		/// One percent-scaled production factor with the keepers' method folded into it.
		/// <para>
		/// RESEARCH-SYSTEM-DESIGN &sect;8.2: <i>output = base &times; crew &times; wear &times;
		/// method</i>, and method is a THIRD factor, never folded into the first two. It is folded
		/// into neither here either &mdash; the caller multiplies it into the one factor it owns and
		/// leaves crew and condition alone, so <c>min(0, x) = 0</c> still means an unstaffed work
		/// makes nothing however much the keepers know (Addendum 8 clause 2), and a broken building
		/// is still broken.
		/// </para>
		/// <para>
		/// <b>A bonus lane, never a tax.</b> Below the baseline the method percent is read AS the
		/// baseline, so no roster and no research produce exactly today's number and no path
		/// through here can make a realm that abstained produce less than one that never heard of
		/// the tree. A non-positive quantity &mdash; the consuming lane, where a negative rate
		/// drains a row &mdash; is returned untouched for the same reason: multiplying a draw by
		/// the method factor would charge a city for what its keepers worked out.
		/// </para>
		/// <para>
		/// A factor, never a timer (Addendum 8): what this scales is the RATE the crew works at,
		/// and the days it works are counted by <see cref="TryDaysBetween"/> and by nothing here.
		/// </para>
		/// <para>
		/// Total over every representable input: an <c>int</c> times an <c>int</c> cannot leave
		/// <c>long</c>, and the one clamp is at the top of the <c>int</c> the caller carries.
		/// </para>
		/// </summary>
		/// <param name="quantity">The factor the caller owns, as a percent. Non-positive is
		/// returned unchanged.</param>
		/// <param name="methodPercent">The realm's method, from
		/// <c>KingdomResearch.MethodPercent</c>. Anything under
		/// <see cref="BaselineMethodPercent"/> is read as the baseline.</param>
		internal static int Methoded(int quantity, int methodPercent)
		{
			if (quantity <= 0)
			{
				return quantity;
			}
			int method = (methodPercent < BaselineMethodPercent) ? BaselineMethodPercent : methodPercent;
			if (method == BaselineMethodPercent)
			{
				return quantity;
			}
			long scaled = (long)quantity * method / BaselineMethodPercent;
			return (scaled > int.MaxValue) ? int.MaxValue : (int)scaled;
		}

		/// <summary>
		/// The ground wins for what is physical; the outstanding claim survives it.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.1 step 4 and &sect;3.9. Before W6 the reconcile wrote
		/// the ground straight over the level and left the debt where it was, which silently broke
		/// I1 for any row that still owed something the containers could not take. This states the
		/// identity instead of hoping for it: the new level is the ground plus whatever is still
		/// owed, and the new debt is <b>re-derived</b> from that, so
		/// <c>level - owed == ground</c> holds <b>by construction</b> at the end of every attended
		/// pass. That equality is exactly what the audit line prints.
		/// </para>
		/// <para>
		/// A claim that no longer fits the containers is dropped and reported as
		/// <see cref="KingdomProductionStep.Spilled"/>, for the same reason production over a full
		/// granary is: the model may not hold goods against room that does not exist.
		/// </para>
		/// </summary>
		/// <param name="groundLevel">What the ground was just measured holding.</param>
		/// <param name="groundCapacity">What the ground can hold.</param>
		/// <param name="owed">The row's signed debt before the reconcile.</param>
		internal static bool TryReconcile(long groundLevel, long groundCapacity, int owed, out KingdomProductionStep step, out KingdomCityFault fault)
		{
			step = new KingdomProductionStep(groundLevel, 0, 0L, 0L);
			if (groundCapacity < 0L || groundLevel < 0L || groundLevel > groundCapacity)
			{
				fault = KingdomCityFault.InvalidCapacity;
				return false;
			}
			fault = KingdomCityFault.None;
			long wanted = groundLevel + owed;
			long nextLevel = wanted;
			if (nextLevel < 0L)
			{
				nextLevel = 0L;
			}
			if (nextLevel > groundCapacity)
			{
				nextLevel = groundCapacity;
			}
			// Re-derived, never carried: this is the one line that makes the audit exact rather
			// than approximately true.
			long nextOwed = nextLevel - groundLevel;
			if (nextOwed > int.MaxValue || nextOwed < int.MinValue)
			{
				fault = KingdomCityFault.ArithmeticOverflow;
				return false;
			}
			step = new KingdomProductionStep(nextLevel, (int)nextOwed, 0L, wanted - nextLevel);
			return true;
		}
	}
}
