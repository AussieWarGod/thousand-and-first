using System;
using System.Collections.Generic;
using System.Diagnostics;

using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One micro-reckon slice, in the only shape the executor accepts.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.6: <b>it is the same <c>TryAdvance</c>, not a second code
	/// path.</b> A run of micro-reckons followed by a homecoming reckon <i>is one advancement,
	/// split</i> &mdash; remainder kept, never re-anchored, idempotent at a repeated tick. If the
	/// slice were a second implementation of the clock this whole section would be a bug factory;
	/// it is one call site more on a total function, and the only thing that differs from
	/// <see cref="KingdomReckonJob"/> is which row of the constitution it answers to.
	/// </para>
	/// </summary>
	internal sealed class KingdomSliceJob : IKingdomComputation<KingdomReckonInput, KingdomCityState>
	{
		private readonly KingdomCityAdvanceable model;

		private readonly string label;

		internal KingdomSliceJob(string label, KingdomCityAdvanceable model)
		{
			this.label = label;
			this.model = model;
		}

		public string Label
		{
			get { return label ?? ""; }
		}

		public KingdomBudgetLane Lane
		{
			get { return KingdomBudgetLane.Heartbeat; }
		}

		public bool TryRun(KingdomReckonInput input, out KingdomCityState output, out KingdomComputeCounters counters, out KingdomCityFault fault)
		{
			output = null;
			counters = KingdomComputeCounters.None;
			if (input.State == null || model == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			KingdomAdvanceOutcome<KingdomCityState> outcome;
			if (!KingdomAdvanceRules.TryRun(model, input.State, input.State.ProcessedThroughTick, input.ToTick, out outcome, out fault))
			{
				return false;
			}
			output = outcome.State;
			counters = new KingdomComputeCounters(outcome.Steps, outcome.RowVisits, 0, 0, 0L);
			fault = KingdomCityFault.None;
			return true;
		}
	}

	/// <summary>
	/// The pump: the one per-turn cost this design adds anywhere.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;0.0(e): <b>a clock is not a pump.</b> Game-level
	/// <c>EndTurnEvent.Send(game)</c> fires once per ten segments, immediately before
	/// <c>ProcessSingleTurn</c> (<c>D/XRL/Core/ActionManager.cs:1644-1650</c>) &mdash; <b>one</b>
	/// dispatch, not the 2,000-cell broadcast a live zone pays. It does not fire during world-map
	/// travel, which is exactly why &sect;2.1 bans it as the city's <i>clock</i>; but a founder on
	/// the world map is standing in no city zone and is owed no reification, so the same blind spot
	/// is harmless in a <i>pump</i>. How much work is owed is always derived from
	/// <c>The.Game.TimeTicks</c> deltas; the pump only decides <b>when a slice of it is spent</b>.
	/// </para>
	/// <para>
	/// Five things happen here and nothing else does: the heartbeat slice on its cadence
	/// (&sect;3.6), the retirement of jobs that ran out while nobody was watching (&sect;3.8 t2),
	/// the O(1) attended-semantic due check, one turn's amortised reify spend (&sect;3.5), and the
	/// prefetch (&sect;6.4). Every one returns immediately when there is no seated claimed zone and
	/// no debt; the semantic check does not take a survey until its absolute day boundary is due.
	/// </para>
	/// </summary>
	public static partial class KingdomHeartbeat
	{
		/// <summary>
		/// The prefetch gate. LIVING-CITY-ARCHITECTURE &sect;6.4: <i>"prefetch is a spike, not a
		/// promise &hellip; W3 ships it behind the option gate with the receipt attached, and it is
		/// the one thing in this design that is safe to cut."</i>
		/// <para>
		/// The ID is live the moment a checkbox line for it lands in <c>Options.xml</c> beside the
		/// mod's others; until then the gate reads <see cref="PrefetchDefault"/>. <b>Cutting the
		/// feature is setting it to No, and nothing else in the design depends on it</b>, because it
		/// buys smoothness and not correctness.
		/// </para>
		/// </summary>
		public const string PrefetchOption = "r_TAF_OptionPrefetch";

		/// <summary>
		/// What the gate reads while <c>Options.xml</c> has no line for it: <b>No</b>.
		/// <para>
		/// Every other option this mod ships defaults to Yes, and this one deliberately does not.
		/// &sect;6.4 is explicit that the combination &mdash; write into a suspended-resident zone,
		/// hold it with <c>MarkActive</c>, let it freeze on drain &mdash; is <b>untested in play</b>,
		/// and a player cannot turn off a spike whose checkbox does not exist yet. So the mechanism
		/// ships complete and asserted, and reads its own default until the checkbox lands beside
		/// the others; adding that one line is what turns it on. <b>The feature is smoothness, not
		/// correctness</b>, and nothing else in the design depends on it.
		/// </para>
		/// </summary>
		public const string PrefetchDefault = "No";

		/// <summary>
		/// Neighbours the prefetch will look at. LIVING-CITY-ARCHITECTURE &sect;6.4: the engine's
		/// topology gives at most six; <b>we consider two (ranked by debt) and hold one.</b> This is
		/// O(neighbours), never O(city) &mdash; a founder in a thirty-zone city pays exactly what a
		/// founder in a two-zone city pays.
		/// </summary>
		public const int PrefetchCandidates = 2;

		/// <summary>Zones held resident beyond the seated one. &sect;0.0's own row, and
		/// <see cref="KingdomBudgetLane.ResidentZones"/> judges against it.</summary>
		public const int PrefetchHeld = 1;

		private static bool PrefetchEnabled
		{
			get { return Options.GetOption(PrefetchOption, PrefetchDefault) == "Yes"; }
		}

		/// <summary>
		/// One turn of the city, whether or not anybody is looking at it.
		/// <para>
		/// Compatibility overload. Core uses the callback overload below once the attended pass is
		/// wired; callers which cannot supply that pass retain the city-book pump without semantics.
		/// </para>
		/// </summary>
		public static void OnEndTurn(KingdomSystem System)
		{
			OnEndTurn(System, null);
		}

		/// <summary>
		/// One turn of the city, with the canonical attended semantic pass supplied by
		/// <c>KingdomSystem</c>. The overload above preserves callers while Core wires the pass.
		/// </summary>
		public static void OnEndTurn(KingdomSystem System, KingdomSemanticDispatcher.AttendedPass AttendedPass)
		{
			if (System == null || !System.Founded || The.Game == null)
			{
				return;
			}
			long now = The.Game.TimeTicks;
			Zone seated = SeatedClaimedZone(System);
			KingdomSystem.Guard("raid wake", delegate
			{
				KingdomRaids.OnWorldWake(System, now, The.Player?.CurrentZone);
			});
			KingdomSystem.Guard("slice", delegate
			{
				Slice(System, now);
			});
			KingdomSystem.Guard("jobs", delegate
			{
				KingdomPorters.Retire(System, now);
			});
			if (seated != null && AttendedPass != null)
			{
				KingdomSystem.Guard("semantic", delegate
				{
					KingdomSemanticDispatcher.OnEndTurn(System, seated, now, AttendedPass);
				});
			}
			if (seated != null)
			{
				// Presentation heartbeat, independent of the once-per-day semantic cadence. An
				// absence begins when the founder actually stopped standing here, not at the last
				// daily boundary; unread ledger days remain untouched until the report is read.
				System.LastVisitTick = now;
			}
			bool saturated = false;
			if (seated != null)
			{
				KingdomSystem.Guard("reify", delegate
				{
					saturated = KingdomCity.SpendTurn(System, seated, now);
				});
			}
			KingdomSystem.Guard("prefetch", delegate
			{
				Prefetch(System, seated, now, saturated);
			});
		}

		/// <summary>
		/// The stale-transient sweep, at the one instant &sect;3.8 names for it: a zone coming off
		/// disk, <b>before intake and before any reify</b>.
		/// <para>
		/// <c>ZoneThawedEvent</c> reaches <c>The.Game</c> as well as the zone
		/// (<c>D/XRL/World/ZoneThawedEvent.cs:39-46</c>), fires for any zone active or not, and
		/// carries <c>TicksFrozen</c> &mdash; which is a cross-check on the counter and deliberately
		/// never its source, because it measures frozen time only and says nothing about
		/// suspended-but-resident time (&sect;3.4).
		/// </para>
		/// </summary>
		public static void OnThawed(KingdomSystem System, Zone Z, long TicksFrozen)
		{
			if (System == null || !System.Founded || Z == null || System.ClaimedZones == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			KingdomPorters.Sweep(System, Z);
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("city: thawed " + Z.ZoneID + " frozen=" + TicksFrozen + " owed=" + KingdomCity.OwedThirds(System));
			}
		}
	}
}
