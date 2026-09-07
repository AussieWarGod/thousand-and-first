using System;
using System.Collections.Generic;
using XRL.UI;

namespace ThousandAndFirst
{
	public static partial class KingdomMaster
	{
		private sealed class LifecyclePlan
		{
			private readonly KingdomLifecycleBook Source;
			private readonly bool Pristine;
			private readonly long Now;
			private readonly KingdomLifecycleOptionState Locus;
			private readonly KingdomLifecycleOptionState Notable;
			private readonly KingdomLifecycleOptionState Raid;
			private readonly KingdomLifecycleOptionState Petition;
			private readonly bool GrowthEnabled;
			private readonly bool ScarcityEnabled;
			private readonly KingdomMasterGrowthResumePlan Growth;
			internal bool HasArrivalAuthority { get { return Growth != null && Growth.HasArrivalAuthority; } }
			internal long NextArrivalTick { get { return Growth.NextArrivalTick; } }

			private LifecyclePlan(KingdomLifecycleBook source, bool pristine, long now, KingdomLifecycleOptionState locus,
				KingdomLifecycleOptionState notable, KingdomLifecycleOptionState raid,
				KingdomLifecycleOptionState petition, bool growthEnabled, bool scarcityEnabled,
				KingdomMasterGrowthResumePlan growth)
			{
				Source = source; Pristine = pristine;
				Now = now; Locus = locus; Notable = notable; Raid = raid;
				Petition = petition; GrowthEnabled = growthEnabled; ScarcityEnabled = scarcityEnabled;
				Growth = growth;
			}

			internal static bool TryCreate(KingdomLifecycleBook book, long now, long disabledAt,
				long interval, int cohort, int rulesVersion, out LifecyclePlan plan)
			{
				plan = null;
				if (book == null) return true;
				if (book.LocusOptionTick > now || book.NotableOptionTick > now
					|| book.RaidOptionTick > now || book.PetitionOptionTick > now) return false;
				bool pristine = KingdomLifecycleRules.IsPristineMasterResumeLifecycle(book);
				bool growthEnabled = KingdomGrowth.Enabled;
				bool scarcityEnabled = KingdomGrowth.ScarcityEnabled;
				KingdomMasterGrowthResumePlan growth = null;
				if (!pristine && !KingdomMasterGrowthResumePlan.TryCreate(book, disabledAt, now,
					growthEnabled, scarcityEnabled, interval, cohort, rulesVersion, out growth,
					out string _)) return false;
				plan = new LifecyclePlan(book, pristine, now,
					KingdomLocus.Enabled ? KingdomLifecycleOptionState.Enabled
						: KingdomLifecycleOptionState.Disabled,
					KingdomGuestbook.GuestsEnabled ? KingdomLifecycleOptionState.Enabled
						: KingdomLifecycleOptionState.Disabled,
					KingdomRaids.Enabled ? KingdomLifecycleOptionState.Enabled
						: KingdomLifecycleOptionState.Disabled,
					KingdomPetitions.Enabled ? KingdomLifecycleOptionState.Enabled
						: KingdomLifecycleOptionState.Disabled, growthEnabled, scarcityEnabled, growth);
				return true;
			}

			internal bool CanPublish(KingdomLifecycleBook book)
			{
				return ReferenceEquals(book, Source) && GrowthEnabled == KingdomGrowth.Enabled
					&& ScarcityEnabled == KingdomGrowth.ScarcityEnabled
					&& (Pristine ? KingdomLifecycleRules.IsPristineMasterResumeLifecycle(book)
						: Growth != null && Growth.CanPublish(book, out string _));
			}

			internal void Publish(KingdomLifecycleBook book)
			{
				if (Pristine) return;
				Growth.PublishPrevalidated();
				book.LocusOption = Locus; book.LocusOptionTick = Now;
				book.NotableOption = Notable; book.NotableOptionTick = Now;
				book.RaidOption = Raid; book.RaidOptionTick = Now;
				book.PetitionOption = Petition; book.PetitionOptionTick = Now;
			}
		}

		private sealed class TradePlan
		{
			private readonly KingdomTradeOptionState State;
			private readonly long Tick;
			private readonly long Epoch;
			private readonly long[] CharterTicks;
			private readonly long ManifestDeadline;
			private readonly byte[] SourceEnvelope;

			private TradePlan(KingdomTradeOptionState state, long tick, long epoch,
				long[] charterTicks, long manifestDeadline, byte[] sourceEnvelope)
			{
				State = state; Tick = tick; Epoch = epoch; CharterTicks = charterTicks;
				ManifestDeadline = manifestDeadline; SourceEnvelope = sourceEnvelope;
			}

			internal static bool TryCreate(KingdomTradeBook book, long now, long disabledAt,
				out TradePlan plan)
			{
				plan = null;
				if (book == null) return true;
				if (!KingdomTradeRules.BookUsable(book) || book.OptionObservedTick > now
					|| book.Charters == null) return false;
				byte[] sourceEnvelope;
				try { sourceEnvelope = KingdomTradeCodec.EncodeEnvelope(book); }
				catch (Exception) { return false; }
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
				plan = new TradePlan(state, now, epoch, ticks, manifest, sourceEnvelope);
				return true;
			}

			internal bool MatchesSource(KingdomTradeBook book)
			{
				if (book == null || SourceEnvelope == null) return false;
				try { return SameBytes(SourceEnvelope, KingdomTradeCodec.EncodeEnvelope(book)); }
				catch (Exception) { return false; }
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

			private static bool SameBytes(byte[] left, byte[] right)
			{
				if (left == null || right == null || left.Length != right.Length) return false;
				int difference = 0;
				for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
				return difference == 0;
			}
		}
	}
}
