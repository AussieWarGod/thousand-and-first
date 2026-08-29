using System;
using System.Collections.Generic;
using System.Diagnostics;

using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomHeartbeat
	{

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
			// The shaft flags are set on both ends because Adjacent now asks them: a neighbour under
			// the floor is only a neighbour where a delve was cut, and prefetching rock nobody can
			// reach would be holding a zone the carriers will never walk to.
			KingdomZoneNode here = new KingdomZoneNode(Seated.ZoneID, sx, sy, sz, KingdomDelve.ShaftStands(Seated.ZoneID));
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
					|| !KingdomDistanceRules.Adjacent(here, new KingdomZoneNode(row.ZoneId, ox, oy, oz, KingdomDelve.ShaftStands(row.ZoneId))))
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
