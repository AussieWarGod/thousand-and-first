using System;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomNetworkRules
	{
		/// <summary>
		/// A network transfer posted to the book. <b>A transfer is a carry</b>: level and debt move
		/// together, on both sides, in one publish.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.9 and invariant I1 — <i>model total == ground total +
		/// counter-owed, per stock kind, at every instant</i>. Nothing physical has happened yet: a
		/// main that ran a hundred drams from the granary zone's cistern to the forge zone's has
		/// changed no container. So the giving row loses a hundred from its LEVEL and a hundred
		/// from its DEBT, and the taking row gains a hundred of each, which leaves
		/// <c>level - owed</c> — the ground — untouched on both, and leaves the city's totals of
		/// both untouched as well. &sect;3.5's amortised reify is what later opens the real vessels,
		/// in <c>KingdomDrainRules</c>' dedication order, and 12(d)'s <i>containers hold true
		/// numbers</i> is that landing rather than a second ledger.
		/// </para>
		/// <para>
		/// This is <c>KingdomCityRules.TryApplyTransfer</c>'s identity, stated for the case where
		/// <b>both</b> ends are model rows because neither zone is under the founder's feet. That
		/// one posts only the giving side, because its taking side is the seated zone whose real
		/// containers took the goods in the same breath and were measured doing it.
		/// </para>
		/// </summary>
		/// <param name="amount">What the line wants to move. Clamped to what the giver can spare
		/// and what the taker has room for; the clamped figure is reported as
		/// <paramref name="moved"/> rather than being silently the same number.</param>
		internal static bool TryPostTransfer(
			KingdomCityState state,
			KingdomStockKind kind,
			int fromZoneIndex,
			int toZoneIndex,
			long amount,
			out KingdomCityState next,
			out long moved,
			out KingdomCityFault fault)
		{
			next = state;
			moved = 0L;
			if (state == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (fromZoneIndex == toZoneIndex)
			{
				// Loud rather than a quiet no-op: a line that believes it is moving water from a
				// zone to itself has a topology bug, and a silent zero would hide it.
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			KingdomZoneRow giver;
			KingdomZoneRow taker;
			if (!state.TryZone(fromZoneIndex, out giver) || !state.TryZone(toZoneIndex, out taker))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			KingdomStockPair fromPair;
			KingdomStockPair toPair;
			if (!giver.Stocks.TryGet(kind, out fromPair) || !taker.Stocks.TryGet(kind, out toPair))
			{
				fault = KingdomCityFault.InvalidRate;
				return false;
			}
			fault = KingdomCityFault.None;
			if (amount <= 0L)
			{
				return true;
			}
			// A zone already owing a draw has that much of its level spoken for: the vessels have
			// not paid it yet, so a main may not carry it away a second time. The same guard
			// KingdomCityRules.TryPlanTransfer keeps, for the same reason.
			long spokenFor = (giver.OwedOf(kind) < 0) ? -(long)giver.OwedOf(kind) : 0L;
			long spare = fromPair.Level - spokenFor;
			if (spare < 0L)
			{
				spare = 0L;
			}
			long room = toPair.Capacity - toPair.Level;
			if (room < 0L)
			{
				room = 0L;
			}
			long take = amount;
			if (take > spare)
			{
				take = spare;
			}
			if (take > room)
			{
				take = room;
			}
			if (take <= 0L)
			{
				return true;
			}
			long nextGiverOwed = (long)giver.OwedOf(kind) - take;
			long nextTakerOwed = (long)taker.OwedOf(kind) + take;
			if (nextGiverOwed < int.MinValue || nextTakerOwed > int.MaxValue)
			{
				fault = KingdomCityFault.ArithmeticOverflow;
				return false;
			}
			KingdomStocks lowered;
			KingdomStocks raised;
			if (!giver.Stocks.TryWith(kind, new KingdomStockPair(fromPair.Level - take, fromPair.Capacity), out lowered)
				|| !taker.Stocks.TryWith(kind, new KingdomStockPair(toPair.Level + take, toPair.Capacity), out raised))
			{
				fault = KingdomCityFault.InvalidRate;
				return false;
			}
			KingdomCityState afterGiver;
			KingdomCityState afterTaker;
			if (!state.TryWithZone(
					fromZoneIndex,
					giver.WithReading(giver.LastReadTick, lowered, giver.Roofs, giver.Defence, giver.WaterCarry, giver.FoodCarry)
						.WithOwedOf(kind, (int)nextGiverOwed),
					out afterGiver,
					out fault))
			{
				return false;
			}
			if (!afterGiver.TryZone(toZoneIndex, out taker))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (!afterGiver.TryWithZone(
					toZoneIndex,
					taker.WithReading(taker.LastReadTick, raised, taker.Roofs, taker.Defence, taker.WaterCarry, taker.FoodCarry)
						.WithOwedOf(kind, (int)nextTakerOwed),
					out afterTaker,
					out fault))
			{
				return false;
			}
			next = afterTaker;
			moved = take;
			return true;
		}
	}
}
