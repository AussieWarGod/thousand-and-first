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
	/// Four things happen here and nothing else does: the heartbeat slice on its cadence
	/// (&sect;3.6), the retirement of jobs that ran out while nobody was watching (&sect;3.8 t2),
	/// one turn's amortised reify spend (&sect;3.5), and the prefetch (&sect;6.4). Every one of
	/// them returns immediately when there is no seated claimed zone and no debt.
	/// </para>
	/// </summary>
	public static class KingdomHeartbeat
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
		/// Called from <c>KingdomSystem</c>'s <c>EndTurnEvent</c> handler and nowhere else. Each
		/// step is guarded on its own, so a step that faults costs its own work and never the turn.
		/// </para>
		/// </summary>
		public static void OnEndTurn(KingdomSystem System)
		{
			if (System == null || !System.Founded || The.Game == null)
			{
				return;
			}
			long now = The.Game.TimeTicks;
			Zone seated = SeatedClaimedZone(System);
			KingdomSystem.Guard("slice", delegate
			{
				Slice(System, now);
			});
			KingdomSystem.Guard("jobs", delegate
			{
				KingdomPorters.Retire(System, now);
			});
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

		// ==================================================================================
		// The heartbeat
		// ==================================================================================

		/// <summary>
		/// Every fifty ticks &mdash; one in-game hour, <c>Calendar.TurnsPerHour</c> &mdash; advance
		/// every city's model by whatever elapsed and surface at most one line.
		/// <para>
		/// <b>All cities, not just the seated one</b> (&sect;3.6), which is what makes the second
		/// city's news reach a founder standing in the first. <b>No special case for travel</b>: a
		/// slice advances by whatever elapsed, so several cadence boundaries crossed at once is one
		/// slightly larger slice, still closed-form, still one propose pass. <c>N</c> decides how
		/// often we bother, never how much we advance.
		/// </para>
		/// </summary>
		private static void Slice(KingdomSystem System, long nowTick)
		{
			if (nowTick <= 0L)
			{
				return;
			}
			if (System.LastSliceTick > 0L && nowTick - System.LastSliceTick < KingdomBudgetRules.HeartbeatCadenceTicks)
			{
				return;
			}
			System.LastSliceTick = nowTick;
			int told = 0;
			told += Advance(System, System.City, System.SeatName, nowTick, told);
			if (System.Away != null)
			{
				told += Advance(System, System.Away.City, System.Away.SettlementName, nowTick, told);
			}
		}

		/// <summary>
		/// One city's happenings and its ambience, out of the same &le; 1 told line the slice is
		/// budgeted (&sect;3.6).
		/// <para>
		/// <b>Recording is unbudgeted and telling is not.</b> A wedding that happens while the
		/// budget is spent still happens, still reaches the chronicle, and is still in the ring for
		/// the homecoming report to count &mdash; what the budget rations is how often the founder
		/// is interrupted, never what the city does. The ambience speaks last of the three,
		/// because a line about the hour must never crowd out a line about a work that stopped.
		/// </para>
		/// </summary>
		private static int Happen(KingdomSystem System, KingdomCityBook book, string label, long nowTick, int alreadyTold)
		{
			int budget = KingdomBudgetRules.HeartbeatToldLinesPerSlice - alreadyTold;
			if (book == null || budget <= 0)
			{
				return 0;
			}
			// Framed as word from a named city, unconditionally, exactly as the slice's own
			// shortfall note already is: the heartbeat speaks for every city at once and has no
			// business claiming the founder is standing in whichever one it is currently reckoning.
			// The settlement pass, which knows, says "here".
			int told = KingdomHappenings.Reckon(System, book, label, false, nowTick, budget);
			if (told < budget)
			{
				told += KingdomAmbient.Speak(System, book, label, false, nowTick);
			}
			return told;
		}

		/// <summary>One city's slice, and its share of the &le; 1 told line per slice
		/// (&sect;3.6).</summary>
		private static int Advance(KingdomSystem System, KingdomCityBook book, string label, long nowTick, int alreadyTold)
		{
			if (book == null)
			{
				return 0;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!book.TryRead(out state, out fault))
			{
				return 0;
			}
			KingdomCatchUpCounter before = KingdomCityRules.CityCounter(state);
			KingdomSliceJob job = new KingdomSliceJob(
				string.IsNullOrEmpty(label) ? state.SettlementId : label,
				new KingdomCityAdvanceable(KingdomRules.TicksPerDay, null, null));
			KingdomComputeResult<KingdomCityState> result = KingdomCity.Seam.Submit(new KingdomReckonInput(state, nowTick), job);
			if (!result.Published)
			{
				return 0;
			}
			if (!book.TryPublish(result.Value, out fault))
			{
				Refuse("slice", fault);
				return 0;
			}
			KingdomCatchUpCounter after = KingdomCityRules.CityCounter(result.Value);
			int told = 0;
			if (alreadyTold < KingdomBudgetRules.HeartbeatToldLinesPerSlice && after.DrawThirds > before.DrawThirds)
			{
				// At most one ambient message an in-game hour, city-wide. A shortfall that has just
				// begun says itself once and then lives in the status report, which is what
				// KingdomWord's send-not-outbox contract already requires (§3.6). It speaks BEFORE
				// the happenings for the reason §8.1(3) gives: a shortfall is the thing the founder
				// can still act on, and it is never the line that gets summarised away.
				string note = KingdomCityRules.SliceNote(label, after.DrawThirds - before.DrawThirds);
				KingdomWord.Ambient(System, label, false, note);
				told = 1;
			}
			// W4. The same slice, and the same budget: the city's happenings are generated at the
			// tick the model was just advanced to, so a founder standing in one city hears about a
			// wedding in the other (§3.6, "all cities, not just the seated one").
			KingdomSystem.Guard("happenings", delegate
			{
				told += Happen(System, book, label, nowTick, alreadyTold + told);
			});
			return told;
		}

		// ==================================================================================
		// The prefetch
		// ==================================================================================

		/// <summary>
		/// Thaws at most one neighbouring claimed zone that owes something, holds it resident while
		/// the debt stands, and spends its counter before the founder crosses.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;6.4(2): <b>we never needed a zone to be live; we need it
		/// to be resident.</b> A suspended-but-resident zone has its whole object graph in RAM
		/// &mdash; suspend serializes nothing and drops nothing &mdash; so the survey can read it
		/// and materialisation can write into it, exactly as zone generation writes into a zone
		/// that has never been activated; and <c>ProcessSingleTurn</c> skips a suspended zone
		/// outright (<c>D/XRL/Core/ActionManager.cs:445</c>), so it costs <b>zero per turn</b>.
		/// </para>
		/// <para>
		/// <b>The prefetch invariant.</b> <i>A prefetched zone the founder never enters is
		/// indistinguishable from one that was never prefetched.</i> Prefetch may change WHEN work
		/// is done, never WHETHER or HOW MUCH &mdash; which is why the spend it runs is the same
		/// <see cref="KingdomCity.SpendTurn"/> the seated zone runs, against the same counter, with
		/// the same publish. Anything that would not also be true after a plain cold entry may not
		/// be done inside a prefetch, and nothing here is.
		/// </para>
		/// </summary>
		private static void Prefetch(KingdomSystem System, Zone Seated, long nowTick, bool Saturated)
		{
			if (!PrefetchEnabled || Seated == null || The.ZoneManager == null)
			{
				Release(System);
				return;
			}
			if (Saturated)
			{
				// Skipped under load, never queued: none when the seated zone has already saturated
				// the reify budget. A skipped prefetch costs the founder a normal vanilla thaw at
				// the boundary — what they would have paid anyway.
				Hold(System, nowTick);
				return;
			}
			string wanted = Candidate(System, Seated);
			if (string.IsNullOrEmpty(wanted))
			{
				Release(System);
				return;
			}
			if (string.Equals(System.PrefetchedZoneId, wanted, StringComparison.Ordinal))
			{
				Hold(System, nowTick);
				return;
			}
			Release(System);
			bool resident = The.ZoneManager.CachedZonesContains(wanted);
			Stopwatch watch = Stopwatch.StartNew();
			Zone held = The.ZoneManager.GetZone(wanted);
			watch.Stop();
			if (held == null)
			{
				return;
			}
			System.PrefetchedZoneId = wanted;
			held.MarkActive();
			// The thaw lane's own count IS the millisecond figure (§0.0: timed so a prefetch can be
			// seen, and budgeted nowhere), so the primary count is left at zero rather than printed
			// twice; whether the zone had to come off disk at all rides in the label instead.
			KingdomCity.Record(new KingdomPerfReceipt(
				KingdomBudgetLane.Thaw,
				wanted + (resident ? " reason=prefetch-resident" : " reason=prefetch"),
				(watch.ElapsedTicks * 1000000L) / Stopwatch.Frequency,
				KingdomComputeCounters.None,
				0L,
				KingdomBudgetVerdict.Within,
				KingdomBudgetVerdict.Within));
			KingdomCity.SpendTurn(System, held, nowTick);
		}

		/// <summary>Keeps the held zone from being written straight back to disk. One long
		/// assignment a turn (<c>D/XRL/World/Zone.cs:2304-2307</c>), and only while a debt stands:
		/// <b>the hold lives exactly as long as the debt.</b></summary>
		private static void Hold(KingdomSystem System, long nowTick)
		{
			if (string.IsNullOrEmpty(System.PrefetchedZoneId) || The.ZoneManager == null)
			{
				return;
			}
			Zone held = The.ZoneManager.CachedZonesContains(System.PrefetchedZoneId)
				? The.ZoneManager.GetZone(System.PrefetchedZoneId)
				: null;
			if (held == null)
			{
				System.PrefetchedZoneId = null;
				return;
			}
			if (KingdomCity.OwedThirds(System) <= 0)
			{
				// When the counter drains we stop calling MarkActive and the zone freezes itself at
				// the next CheckCached. A caught-up zone is never held.
				System.PrefetchedZoneId = null;
				return;
			}
			held.MarkActive();
			KingdomCity.SpendTurn(System, held, nowTick);
		}

		private static void Release(KingdomSystem System)
		{
			System.PrefetchedZoneId = null;
		}

		/// <summary>
		/// The neighbour worth holding: a claimed zone the founder could reach next, ranked by what
		/// it owes. Two considered, one held, and never more &mdash; O(neighbours), never O(city).
		/// </summary>
		private static string Candidate(KingdomSystem System, Zone Seated)
		{
			KingdomCityState state;
			KingdomCityFault fault;
			if (System.City == null || !System.City.TryRead(out state, out fault))
			{
				return null;
			}
			string world;
			int sx;
			int sy;
			int sz;
			if (!KingdomRules.TryParseZoneID(Seated.ZoneID, out world, out sx, out sy, out sz))
			{
				return null;
			}
			KingdomZoneNode here = new KingdomZoneNode(Seated.ZoneID, sx, sy, sz);
			string best = null;
			int bestOwed = 0;
			int considered = 0;
			for (int i = 0; i < state.ZoneCount && considered < PrefetchCandidates; i++)
			{
				KingdomZoneRow row;
				if (!state.TryZone(i, out row) || string.Equals(row.ZoneId, Seated.ZoneID, StringComparison.Ordinal))
				{
					continue;
				}
				string otherWorld;
				int ox;
				int oy;
				int oz;
				if (!KingdomRules.TryParseZoneID(row.ZoneId, out otherWorld, out ox, out oy, out oz)
					|| !string.Equals(world, otherWorld, StringComparison.Ordinal)
					|| !KingdomDistanceRules.Adjacent(here, new KingdomZoneNode(row.ZoneId, ox, oy, oz)))
				{
					continue;
				}
				considered++;
				int owed = KingdomCityRules.CounterFor(row).OwedThirds;
				if (owed > bestOwed)
				{
					bestOwed = owed;
					best = row.ZoneId;
				}
			}
			// Only while a debt stands. A zone with nothing owing is never held, so a founder who
			// settles in for a long stay ends up holding nothing.
			return (bestOwed > 0) ? best : null;
		}

		private static Zone SeatedClaimedZone(KingdomSystem System)
		{
			Zone zone = (The.Player == null) ? null : The.Player.CurrentZone;
			if (zone == null || System.ClaimedZones == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				return null;
			}
			return zone;
		}

		private static void Refuse(string step, KingdomCityFault fault)
		{
			KingdomLog.Log("city: " + step + " refused (" + fault + "); the book is unchanged");
		}
	}
}
