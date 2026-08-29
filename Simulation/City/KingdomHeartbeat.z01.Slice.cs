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
			// The visible half of §3.11, and it is rendering rather than accounting: a few drams
			// crossing between two real vessels on one line, in the zone the founder is standing
			// in. Both vessels are in that zone, so the zone's level and the zone's ground move by
			// exactly zero and no row is touched -- it is the same water in a different cask. What
			// it buys is a founder SEEING a main run instead of reading that it did. Bounded to
			// KingdomNetworks.HeartbeatTransferDrams and one move a slice.
			Zone seated = SeatedClaimedZone(System);
			if (seated != null)
			{
				KingdomSystem.Guard("network slice", delegate
				{
					KingdomNetworks.Attend(System, seated, nowTick);
				});
			}
			int told = 0;
			told += Advance(System, System.City, System.SeatName, nowTick, told);
			List<KingdomSettlement> nonSeat = System.NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++)
				told += Advance(System, nonSeat[i].City, nonSeat[i].SettlementName,
					nowTick, told);
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
			if (book == null)
			{
				return 0;
			}
			string playerZone = The.Player?.CurrentZone?.ZoneID;
			bool here = !string.IsNullOrEmpty(playerZone)
				&& ((ReferenceEquals(book, System.City) && System.ClaimedZones != null
					&& System.ClaimedZones.Contains(playerZone))
					|| (System.FindNonSeatSettlementByBook(book)?.ClaimedZones?.Contains(playerZone)
						?? false));
			int told = KingdomHappenings.Reckon(System, book, label, here, nowTick,
				budget > 0 ? budget : 0);
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
				new KingdomCityAdvanceable(KingdomRules.TicksPerDay, null, null, KingdomResearch.MethodPercent(System)));
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
			// W7 repair. The seat's water clock is a MIRROR of the model's processed-through tick
			// (W6: the stamp is written FROM the model, so two owners of one day is unreachable),
			// and a slice that advanced the model without moving the mirror left the growth pass
			// reading a clock older than the book it mirrors -- which is a day the next pass would
			// bill twice. Only the seated book owns that mirror; the away city's stamp travels with
			// its own settlement.
			if (ReferenceEquals(book, System.City))
			{
				KingdomCity.StampSeat(System, result.Value);
			}
			KingdomBehaviourRuntime.Reckon(System, book, label);
			KingdomCatchUpCounter after = KingdomCityRules.CityCounter(result.Value);
			int told = 0;
			if (alreadyTold < KingdomBudgetRules.HeartbeatToldLinesPerSlice && after.DrawThirds > before.DrawThirds)
			{
				// At most one ambient message an in-game hour, city-wide. A shortfall that has just
				// begun says itself once and then lives in the status report, which is what
				// KingdomWord's send-not-outbox contract already requires (§3.6). It speaks BEFORE
				// the happenings for the reason §8.1(3) gives: a shortfall is the thing the founder
				// can still act on, and it is never the line that gets summarised away.
				string note = KingdomCityRules.SliceNote(KingdomPresentation.Rich(label), after.DrawThirds - before.DrawThirds);
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
	}
}
