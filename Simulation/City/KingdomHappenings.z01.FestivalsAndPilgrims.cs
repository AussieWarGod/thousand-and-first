using System;

using XRL;
using XRL.UI;
using XRL.World;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomHappenings
	{

		// ==================================================================================
		// Festivals — Qud's own calendar, never an invented holiday
		// ==================================================================================

		private static KingdomCityState Festivals(KingdomSystem System, KingdomCityBook book, KingdomCityState state, string label, bool here, long nowTick, ref int pushed, int pushBudget)
		{
			if (book.LastFestivalTick <= 0L)
			{
				// Never looked. Stamp now and keep nothing: a city founded in Tebet Ux did not
				// miss the Ides of Nivvun Ut, it did not exist for them.
				book.LastFestivalTick = nowTick;
				return state;
			}
			long cursor = book.LastFestivalTick;
			int kept = 0;
			long due;
			KingdomFestivalAnchor anchor;
			while (kept < MaxFestivalScan
				&& KingdomHappeningRules.TryNextFestival(cursor, out due, out anchor)
				&& due <= nowTick)
			{
				KingdomCityState next;
				if (!KeepFeast(System, book, state, label, here, due, anchor, ref pushed,
					pushBudget, out next)) break;
				state = next;
				cursor = due;
				kept++;
			}
			if (kept >= MaxFestivalScan)
			{
				// Out of scan and still behind. Jump, closed-form, rather than keep walking:
				// §0.0(a) bans any term containing the elapsed, and the walk is the term.
				long last;
				KingdomFestivalAnchor lastAnchor;
				if (KingdomHappeningRules.TryLastFestival(nowTick, out last, out lastAnchor) && last > cursor)
				{
					cursor = last;
				}
			}
			book.LastFestivalTick = cursor;
			return state;
		}

		private static bool KeepFeast(KingdomSystem System, KingdomCityBook book,
			KingdomCityState state, string label, bool here, long tick, KingdomFestivalAnchor anchor,
			ref int pushed, int pushBudget, out KingdomCityState next)
		{
			next = state;
			if (HasTold(state, KingdomToldKind.Festival, tick, 0, 0, (int)anchor)) return true;
			int mouths = OnTheRoll(state);
			string dish = (System.DishName ?? "").Trim();
			string place = KingdomWord.CityName(System, label);
			string shownDish = KingdomPresentation.Rich(dish);
			string shownPlace = KingdomPresentation.Rich(place);
			string telling = KingdomHappeningRules.FestivalTelling(anchor, shownPlace,
				shownDish, mouths);
			// Former RecordDisputed semantics still reach outsider history through RecordOnce's
			// canonical outsider sink; lifecycle identity now makes retries safe as well.
			Zone zone = here ? The.Player?.CurrentZone : null;
			KingdomPhysicalQueueResult result = KingdomPhysicalHappenings.QueueGeneric(System,
				book, KingdomPhysicalHappeningKind.Feast, tick, 0, 0, (int)anchor, zone, null,
				telling, DatedReport(tick, telling), "", "",
				KingdomVoices.Say(System, VoiceOccasion.Feast,
					KingdomHappeningRules.FestivalNotice(anchor, shownPlace, shownDish)), "",
				KingdomLocusRules.PilgrimCause(KingdomHappeningRules.AnchorName(anchor), place,
					dish) + "\n" + place, KingdomHappeningRules.AnchorName(anchor), CurrentTick(tick));
			next = Refresh(book, state);
			bool told = HasTold(next, KingdomToldKind.Festival, tick, 0, 0, (int)anchor);
			if (told) KingdomLog.Log("happening: feast " + anchor + " at " + label
				+ " physical=" + (result == KingdomPhysicalQueueResult.AttendedReady));
			return told;
		}

		/// <summary>The typed history-to-body seam. A feast increments one city-owned loudness
		/// counter; only the threshold transition freezes a cause. The Locus later renders that
		/// exact opportunity at the rite ground.</summary>
		internal static bool AccruePilgrim(KingdomCityBook book, string cause,
			string place, long tick)
		{
			if (book == null || string.IsNullOrEmpty(cause) || string.IsNullOrWhiteSpace(place)
				|| place.Length > KingdomLocusRules.MaxPilgrimPlaceChars || tick <= 0L) return false;
			book.Normalize();
			KingdomLocusRules.PilgrimState state =
				(KingdomLocusRules.PilgrimState)book.PilgrimState;
			KingdomLocusRules.PilgrimAccrual accrual =
				KingdomLocusRules.AccruePilgrim(book.PilgrimLoudness, state);
			book.PilgrimLoudness = accrual.Loudness;
			if (!accrual.Minted) return true;
			if (book.PilgrimSequence == int.MaxValue)
			{
				// Fail closed rather than reuse a receipt identity. Retain two stories so one may
				// mint if a future migration safely widens the counter.
				book.PilgrimLoudness = KingdomLocusRules.PilgrimStoryThreshold - 1;
				return true;
			}
			book.PilgrimSequence++;
			book.PilgrimState = (int)KingdomLocusRules.PilgrimState.Waiting;
			book.PilgrimCauseTick = tick;
			book.PilgrimCause = cause;
			book.PilgrimObjectId = "";
			book.PilgrimName = "";
			book.PilgrimPlaceName = place;
			book.PilgrimGreeted = 0;
			KingdomLog.Log("pilgrim: opportunity " + book.PilgrimSequence + " at "
				+ book.SettlementId + " caused by " + cause);
			return true;
		}
	}
}
