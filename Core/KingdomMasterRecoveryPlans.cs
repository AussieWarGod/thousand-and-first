using System;
using System.Collections.Generic;
using XRL.UI;

namespace ThousandAndFirst
{
	public static partial class KingdomMaster
	{
		private sealed class LifecyclePlan
		{
			private readonly long Now;
			private readonly KingdomLifecycleOptionState Locus;
			private readonly KingdomLifecycleOptionState Notable;
			private readonly KingdomLifecycleOptionState Raid;
			private readonly KingdomLifecycleOptionState Petition;
			private readonly long Arrival;

			private LifecyclePlan(long now, KingdomLifecycleOptionState locus,
				KingdomLifecycleOptionState notable, KingdomLifecycleOptionState raid,
				KingdomLifecycleOptionState petition, long arrival)
			{
				Now = now; Locus = locus; Notable = notable; Raid = raid;
				Petition = petition; Arrival = arrival;
			}

			internal static bool TryCreate(KingdomLifecycleBook book, long now, long arrival,
				out LifecyclePlan plan)
			{
				plan = null;
				if (book == null) return true;
				if (book.LocusOptionTick > now || book.NotableOptionTick > now
					|| book.RaidOptionTick > now || book.PetitionOptionTick > now) return false;
				plan = new LifecyclePlan(now,
					KingdomLocus.Enabled ? KingdomLifecycleOptionState.Enabled
						: KingdomLifecycleOptionState.Disabled,
					KingdomGuestbook.GuestsEnabled ? KingdomLifecycleOptionState.Enabled
						: KingdomLifecycleOptionState.Disabled,
					KingdomRaids.Enabled ? KingdomLifecycleOptionState.Enabled
						: KingdomLifecycleOptionState.Disabled,
					KingdomPetitions.Enabled ? KingdomLifecycleOptionState.Enabled
						: KingdomLifecycleOptionState.Disabled, arrival);
				return true;
			}

			internal void Publish(KingdomLifecycleBook book)
			{
				if (book == null) return;
				book.LocusOption = Locus; book.LocusOptionTick = Now;
				book.NotableOption = Notable; book.NotableOptionTick = Now;
				book.RaidOption = Raid; book.RaidOptionTick = Now;
				book.PetitionOption = Petition; book.PetitionOptionTick = Now;
				KingdomGrowthBook growth = book.Growth;
				if (growth == null) return;
				growth.OptionState = KingdomGrowth.Enabled ? KingdomLifecycleOptionState.Enabled
					: KingdomLifecycleOptionState.Disabled;
				growth.OptionTick = Now;
				growth.ScarcityOptionState = KingdomGrowth.ScarcityEnabled
					? KingdomLifecycleOptionState.Enabled : KingdomLifecycleOptionState.Disabled;
				growth.ScarcityOptionTick = Now;
				if (growth.HeartbeatOp == null) growth.LastHeartbeatTick = Now;
				if (growth.FetchOp == null) growth.LastFetchTick = Now;
				if (growth.MillOp == null) growth.LastMillTick = Now;
				if (growth.HeartbeatOp == null) growth.LastSubsidenceTick = Now;
				if (growth.DeliveryOp == null) growth.LastDeliveryTick = Now;
				if (growth.DepartureOp == null) growth.LastDepartureTick = Now;
				if (growth.ArrivalOp == null && growth.ArrivalCandidate == null)
					growth.NextArrivalTick = Arrival;
			}
		}

		private sealed class TradePlan
		{
			private readonly KingdomTradeOptionState State;
			private readonly long Tick;
			private readonly long Epoch;
			private readonly long[] CharterTicks;
			private readonly long ManifestDeadline;

			private TradePlan(KingdomTradeOptionState state, long tick, long epoch,
				long[] charterTicks, long manifestDeadline)
			{
				State = state; Tick = tick; Epoch = epoch; CharterTicks = charterTicks;
				ManifestDeadline = manifestDeadline;
			}

			internal static bool TryCreate(KingdomTradeBook book, long now, long disabledAt,
				out TradePlan plan)
			{
				plan = null;
				if (book == null) return true;
				if (!KingdomTradeRules.BookUsable(book) || book.OptionObservedTick > now
					|| book.Charters == null) return false;
				bool enabled = KingdomTrade.Enabled;
				KingdomTradeOptionState state = enabled ? KingdomTradeOptionState.Enabled
					: KingdomTradeOptionState.Disabled;
				long epoch = book.OptionEpoch;
				if (book.OptionState != state)
				{
					if (epoch == long.MaxValue) return false;
					epoch++;
				}
				long[] ticks = new long[book.Charters.Count];
				for (int i = 0; i < ticks.Length; i++)
				{
					KingdomTradeCharter charter = book.Charters[i];
					ticks[i] = charter?.NextTick ?? 0L;
					if (!enabled || charter == null || charter.Quarantined) continue;
					KingdomRules.DealEntry deal;
					if (!KingdomData.TryGetDeal(charter.DealKey, out deal)
						|| !KingdomMasterRules.TryFutureDeadline(now, deal.IntervalTicks,
							out ticks[i])) return false;
				}
				long manifest = book.Manifest?.DeadlineTick ?? 0L;
				if (book.Manifest != null
					&& book.Manifest.Status == KingdomTradeManifestStatus.InFlight
					&& !KingdomMasterRules.TryResumeCommittedDeadline(manifest, disabledAt,
						now, out manifest)) return false;
				plan = new TradePlan(state, now, epoch, ticks, manifest);
				return true;
			}

			internal void Publish(KingdomTradeBook book)
			{
				if (book == null) return;
				book.OptionState = State; book.OptionObservedTick = Tick;
				book.OptionEpoch = Epoch; book.RestampPending = false;
				if (book.Charters != null && CharterTicks != null
					&& book.Charters.Count == CharterTicks.Length)
					for (int i = 0; i < CharterTicks.Length; i++)
						if (book.Charters[i] != null) book.Charters[i].NextTick = CharterTicks[i];
				if (book.Manifest != null
					&& book.Manifest.Status == KingdomTradeManifestStatus.InFlight)
					book.Manifest.DeadlineTick = ManifestDeadline;
			}
		}
	}
}
