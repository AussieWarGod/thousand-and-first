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
		private static int SignedRemainder(int owed, int magnitude)
		{
			if (magnitude <= 0 || owed == 0) return 0;
			return (owed < 0) ? -magnitude : magnitude;
		}

		/// <summary>
		/// What is left of this turn's reify allowance, in weighted thirds.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;0.0: <b>eight units a turn</b>, and the turn is the unit
		/// rather than the call site. The pass reifies the seated zone, the pump reifies it again
		/// on the turn's own tick, and the prefetch reifies a neighbour; all three draw on this.
		/// </para>
		/// </summary>
		private static int Allowance(KingdomSystem System, long TimeTicks)
		{
			Roll(System, TimeTicks);
			int left = KingdomCatchUpRules.BudgetThirdsPerTurn - System.ReifyThirdsSpent;
			return (left > 0) ? left : 0;
		}

		/// <summary>What is left of this turn's body-mint ceiling. Its own figure, because four
		/// mints is a frame cost and not an ordering preference (&sect;0.0(b)).</summary>
		private static int HeavyAllowance(KingdomSystem System, long TimeTicks)
		{
			Roll(System, TimeTicks);
			int left = KingdomBudgetRules.ReifyHeavyMintsPerTurn - System.ReifyHeavySpent;
			return (left > 0) ? left : 0;
		}

		private static void Roll(KingdomSystem System, long TimeTicks)
		{
			if (System.ReifyTick == TimeTicks)
			{
				return;
			}
			System.ReifyTick = TimeTicks;
			System.ReifyThirdsSpent = 0;
			System.ReifyHeavySpent = 0;
		}

		private static void Charge(KingdomSystem System, long TimeTicks, KingdomReifySpend spend)
		{
			Roll(System, TimeTicks);
			System.ReifyThirdsSpent += spend.ThirdsSpent;
			System.ReifyHeavySpent += spend.Heavy;
		}

		/// <summary>
		/// The city's networks, run for the same span the reckoning just ran (&sect;3.11).
		/// <para>
		/// Composition reads the ground and therefore happens HERE, on a zone render, and never at
		/// reckon (&sect;0.0(d)). The solve is arithmetic over rows composition already wrote, and
		/// its node-visit count is reported against &sect;0.0's network lane rather than assumed to
		/// be inside it.
		/// </para>
		/// </summary>
		private static KingdomCityState Networks(KingdomSystem System, Zone Z, KingdomCityState state, long fromTick, long TimeTicks)
		{
			if (state == null || Z == null)
			{
				return state;
			}
			Stopwatch watch = Stopwatch.StartNew();
			KingdomNetworks.Lines(System, Z);
			long days;
			KingdomCityFault fault;
			if (!KingdomProductionRules.TryDaysBetween(fromTick, TimeTicks, KingdomRules.TicksPerDay, out days, out fault) || days <= 0L)
			{
				watch.Stop();
				return state;
			}
			int visits;
			KingdomCityState next = KingdomNetworks.Run(System, Z, state, days, out visits);
			watch.Stop();
			if (visits <= 0)
			{
				return next;
			}
			long microseconds = (watch.ElapsedTicks * 1000000L) / Stopwatch.Frequency;
			Record(new KingdomPerfReceipt(
				KingdomBudgetLane.NetworkSolve,
				Z.ZoneID + " days=" + days,
				microseconds,
				KingdomComputeCounters.None,
				visits,
				KingdomBudgetRules.JudgeMicroseconds(KingdomBudgetLane.NetworkSolve, microseconds),
				KingdomBudgetRules.JudgeCount(KingdomBudgetLane.NetworkSolve, visits)));
			return next;
		}

		/// <summary>The per-turn reify line of &sect;6.5's receipt, in the shape the log-watcher
		/// already reads.</summary>
		private static void Receipt(string zoneId, KingdomReifySpend spend, Stopwatch watch, int owed)
		{
			long microseconds = (watch.ElapsedTicks * 1000000L) / Stopwatch.Frequency;
			KingdomComputeCounters counters = new KingdomComputeCounters(0, spend.Visible, 0, spend.ThirdsSpent, 0L);
			Record(new KingdomPerfReceipt(
				KingdomBudgetLane.Reify,
				zoneId + " owed=" + owed,
				microseconds,
				counters,
				spend.Units,
				KingdomBudgetRules.JudgeMicroseconds(KingdomBudgetLane.Reify, microseconds),
				KingdomBudgetRules.JudgeCount(KingdomBudgetLane.Reify, spend.Units)));
		}

		/// <summary>
		/// Exact remaining weighted demand in this surveyed zone. Performance receipts may not use
		/// the per-kind book marker: 220 half-filled vessels are 220 physical units, not one.
		/// </summary>
		private static int GroundDemandThirds(Zone Z, KingdomSurvey Survey,
			KingdomCityState state, int index)
		{
			KingdomZoneRow row;
			if (Z == null || Survey == null || state == null || !state.TryZone(index, out row))
			{
				return 0;
			}
			ContainerGround ground = ContainerGround.Take(Survey);
			KingdomContainerDemandReceipt measured;
			KingdomCityFault fault;
			if (!KingdomContainerCatchUpRules.TryMeasure(ground.Rows, ground.Rows.Length,
				row.OwedWater, row.OwedFood, row.OwedMaterials, out measured, out fault))
			{
				return 0;
			}
			Dictionary<int, GameObject> stations = KingdomStations.Index(Z);
			int bodies = Posted(Z, Survey, stations).Count;
			return measured.OwedThirds
				+ bodies * KingdomCatchUpRules.WeightThirds(KingdomUnitWeight.Heavy);
		}

	}
}
