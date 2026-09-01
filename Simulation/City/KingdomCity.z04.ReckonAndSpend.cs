using System;
using System.Collections.Generic;
using System.Diagnostics;

using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomCity
	{
		// ==================================================================================
		// The reckoning, the reify, and the audit
		// ==================================================================================

		/// <summary>
		/// One city, one pass, through the executor and nowhere else. The receipt lands in the
		/// journal whether the job publishes or not, which is what makes a refusal legible instead
		/// of silent.
		/// </summary>
		private static KingdomCityState Reckon(KingdomSystem System, KingdomCityState state, long TimeTicks)
		{
			KingdomReckonJob job = new KingdomReckonJob(
				System.SeatName ?? state.SettlementId,
				new KingdomCityAdvanceable(KingdomRules.TicksPerDay, null,
					KingdomHostedArcology.FoodRateOverrides(System, state),
					KingdomResearch.MethodPercent(System)));
			KingdomComputeResult<KingdomCityState> result = Executor.Submit(new KingdomReckonInput(state, TimeTicks), job);
			KingdomCityState advanced = result.Published ? result.Value : state;
			Stamp(System, advanced);
			return advanced;
		}

		/// <summary>
		/// The one lock that makes double-billing unrepresentable rather than merely avoided.
		/// <para>
		/// W6, LIVING-CITY-ARCHITECTURE &sect;7.4. <c>LastWaterWorkTick</c> is no longer a clock
		/// anybody advances; it is the PUBLISHED MIRROR of the model's own
		/// <c>ProcessedThroughTick</c>, written here and nowhere else. Every day of making is
		/// counted once, by the model, off that one tick — so <c>KingdomGrowth</c> cannot bill a day
		/// the model has already billed, because it no longer owns a clock to bill it from, and a
		/// reckon that REFUSES leaves the tick where it was so the day is billed on the next pass
		/// instead of being lost.
		/// </para>
		/// <para>
		/// <b><c>LastFoodWorkTick</c> is deliberately NOT touched here</b>, and the asymmetry is the
		/// design rather than an oversight. The fields' clocked make moved onto the model with the
		/// water works'; the MILLS did not, because a mill makes nothing out of the day — it takes
		/// real crops off real shelves and puts real staples back, on the ground where the shelves
		/// are, and <c>KingdomCrops.MilledFoodPerDay</c> is subtracted out of the model's own rate
		/// precisely so the two can never both be paid. So the mill keeps that stamp and its
		/// elapsed. Writing it from here would set it to <i>now</i> on every check-in and the mills
		/// would never grind again.
		/// </para>
		/// </summary>
		/// <summary>
		/// The seat's mirror of the model's processed-through tick, advanced from the model and
		/// never independently.
		/// <para>
		/// W7 repair: the heartbeat needs this too. <c>KingdomHeartbeat.Advance</c> publishes an
		/// advanced book every slice and used not to move the mirror with it, so between two
		/// check-ins the growth pass's water clock read older than the model it mirrors &mdash; and
		/// the next pass would then bill days the model had already run. W6's whole ruling is that
		/// there is ONE clock and the seat's stamp is written FROM the model; a second writer that
		/// only sometimes writes is the same defect wearing a smaller hat.
		/// </para>
		/// </summary>
		internal static void StampSeat(KingdomSystem System, KingdomCityState state)
		{
			Stamp(System, state);
		}

		private static void Stamp(KingdomSystem System, KingdomCityState state)
		{
			if (System == null || state == null)
			{
				return;
			}
			System.LastWaterWorkTick = state.ProcessedThroughTick;
		}

		/// <summary>
		/// One turn's amortised spend against this zone's standing debt, for the pump.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.5: <b>entry must cost O(budget), never O(elapsed)</b>,
		/// and the way that is achieved is <c>ZoneRepair</c>'s counter with the spend taken per turn
		/// instead of per activation. <c>ZoneRepair</c> applies its whole backlog in one loop
		/// (<c>D/XRL/World/ZoneParts/ZoneRepair.cs:87-97</c>) because its unit is a
		/// <c>Cell.AddObject</c>; ours is body moves and container fills, so we keep the counter
		/// and spend it on a per-turn budget. <b>That single change is the whole of Addendum
		/// 12(b)'s <i>reification is AMORTISED</i>.</b>
		/// </para>
		/// <para>
		/// Returns whether the budget was saturated, which is the one thing the prefetch needs to
		/// know: a turn that spent its whole allowance is a turn not to thaw a neighbour on.
		/// </para>
		/// </summary>
		public static bool SpendTurn(KingdomSystem System, Zone Z, long TimeTicks)
		{
			if (System == null || !System.Founded || Z == null || System.City == null)
			{
				return false;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!System.City.TryRead(out state, out fault))
			{
				return false;
			}
			int index;
			KingdomZoneRow row;
			if (!IndexOf(state, Z.ZoneID, out index) || !state.TryZone(index, out row)
				|| KingdomCityRules.CounterFor(row).IsSettled)
			{
				// A caught-up zone costs literally nothing, which is ZoneRepair's own self-removal
				// property kept rather than reimplemented (:99-102). The survey below is the
				// expensive part of this method and a settled zone never reaches it.
				return false;
			}
			if (Allowance(System, TimeTicks) <= 0)
			{
				// The turn's whole allowance is already spent, by the pass or by another zone. The
				// debt stays owed and lands next turn, which is the amortisation working.
				return true;
			}
			if (TimeTicks < System.ReifyQuietUntilTick)
			{
				return false;
			}
			Stopwatch watch = Stopwatch.StartNew();
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			KingdomReifySpend spend;
			KingdomCityState written = Reify(System, Z, survey, state, index, TimeTicks, false, out spend);
			watch.Stop();
			Publish(System, written);
			Receipt(Z.ZoneID, spend, watch, GroundDemandThirds(Z, survey, written, index));
			if (spend.Units == 0)
			{
				// Nothing moved: the ground cannot serve this debt yet. Buy an hour of quiet rather
				// than paying for the same survey every turn to be told the same thing.
				System.ReifyQuietUntilTick = TimeTicks + KingdomBudgetRules.HeartbeatCadenceTicks;
			}
			return Allowance(System, TimeTicks) <= 0;
		}

	}
}
