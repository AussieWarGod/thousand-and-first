using System;
using System.Collections.Generic;
using XRL.UI;

namespace ThousandAndFirst
{
	/// <summary>
	/// Runtime owner of the realm-wide master switch. It is intentionally reached before any
	/// guarded delegate is created: the steady disabled path reads four persisted scalars and the
	/// option, then returns without touching a city, zone, collection, random source, or logger.
	/// </summary>
	public static class KingdomMaster
	{
		public const string OptionId = "r_TAF_OptionMaster";

		public static bool ConfiguredEnabled
		{
			get { return Options.GetOption(OptionId, "Yes") != "No"; }
		}

		/// <summary>
		/// Observes one automatic wake. False also means “transition handled”: due work never runs on
		/// the same tick as disable or resume. A failed resume leaves the durable latch disabled.
		/// </summary>
		public static bool ObserveAutomaticWake(KingdomSystem system, long now)
		{
			if (system == null) return false;
			bool configured = ConfiguredEnabled;
			KingdomMasterDecision decision = KingdomMasterRules.Observe(system.MasterOption,
				system.MasterOptionTick, system.MasterResumeToken,
				system.MasterAppliedResumeToken, configured, now);
			if (!decision.Valid) return false;
			if (decision.Transition == KingdomMasterTransition.None)
				return decision.AutomaticWorkAllowed && decision.ChangedAtTick != now;

			if (decision.Transition == KingdomMasterTransition.ResumeRequired)
			{
				KingdomMasterResumePlan plan;
				if (!KingdomMasterResumePlan.TryCreate(system, decision.ChangedAtTick,
					system.MasterOptionTick, out plan)) return false;
				plan.Publish();
				decision = KingdomMasterRules.ApplyResume(decision);
				if (decision == null || !decision.AutomaticWorkAllowed) return false;
			}

			PublishLatch(system, decision);
			// Initialization and both real transitions consume this wake. This is the equal-tick
			// precedence rule, not a one-tick simulation delay: the next wake sees the published latch.
			return false;
		}

		/// <summary>Current explicit mutation gate. Reports and committed recovery use their own lanes.</summary>
		public static bool NewWorkAllowed(KingdomSystem system)
		{
			return system != null && ConfiguredEnabled
				&& (system.MasterOption == KingdomMasterLatchValue.Unobserved
					|| (system.MasterOption == KingdomMasterLatchValue.Enabled
						&& system.MasterResumeToken == system.MasterAppliedResumeToken));
		}

		/// <summary>Cheap guard for auxiliary systems which do not own transition observation.</summary>
		public static bool AutomaticWorkAllowed(KingdomSystem system)
		{
			long now = XRL.The.Game?.TimeTicks ?? -1L;
			return system != null && ConfiguredEnabled
				&& system.MasterOption == KingdomMasterLatchValue.Enabled
				&& system.MasterResumeToken == system.MasterAppliedResumeToken
				&& (now < 0L || system.MasterOptionTick != now);
		}

		private static void PublishLatch(KingdomSystem system, KingdomMasterDecision decision)
		{
			system.MasterOption = decision.State;
			system.MasterOptionTick = decision.ChangedAtTick;
			system.MasterResumeToken = decision.ResumeToken;
			system.MasterAppliedResumeToken = decision.AppliedResumeToken;
		}

		/// <summary>All allocations and list walks are confined to the one resume transition.</summary>
		private sealed class KingdomMasterResumePlan
		{
			private readonly KingdomSystem System;
			private readonly SettlementPlan Seat;
			private readonly SettlementPlan Away;
			private readonly TradePlan Trade;

			private KingdomMasterResumePlan(KingdomSystem system, SettlementPlan seat,
				SettlementPlan away, TradePlan trade)
			{
				System = system;
				Seat = seat;
				Away = away;
				Trade = trade;
			}

			internal static bool TryCreate(KingdomSystem system, long now, long disabledAt,
				out KingdomMasterResumePlan plan)
			{
				plan = null;
				if (system == null || now < disabledAt || disabledAt < 0L) return false;
				SettlementPlan seat;
				SettlementPlan away = null;
				TradePlan trade;
				if (!SettlementPlan.TryCreate(system, now, disabledAt, out seat)
					|| (system.Away != null && !SettlementPlan.TryCreate(system.Away,
						now, disabledAt, out away))
					|| !TradePlan.TryCreate(system.TradeBook, now, disabledAt, out trade))
					return false;
				plan = new KingdomMasterResumePlan(system, seat, away, trade);
				return true;
			}

			internal void Publish()
			{
				Seat.Publish(System);
				if (Away != null) Away.Publish(System.Away);
				if (Trade != null) Trade.Publish(System.TradeBook);
				// Realm-level renderer checkpoints. Existing open jobs and their semantic receipts are
				// deliberately untouched; they resume as the same committed recovery on the next wake.
				System.LastSliceTick = Seat.Now;
				System.ReifyTick = Seat.Now;
				System.ReifyQuietUntilTick = Seat.Now;
			}
		}

		private sealed class SettlementPlan
		{
			internal readonly long Now;
			private readonly long Heartbeat;
			private readonly long Fetch;
			private readonly long WaterWork;
			private readonly long FoodWork;
			private readonly long Subsidence;
			private readonly long Semantic;
			private readonly long Visit;
			private readonly long Arrival;
			private readonly long Guest;
			private readonly long GuestDepart;
			private readonly long Notable;
			private readonly long NotableDepart;
			private readonly long Processed;
			private readonly long Festival;
			private readonly long Extension;
			private readonly string ExtensionHappeningCursors;
			private readonly string ExtensionModel;
			private readonly long[] WorkRan;
			private readonly long[] WorkNext;
			private readonly long[] ClockNext;
			private readonly LifecyclePlan Lifecycle;

			private SettlementPlan(long now, long heartbeat, long fetch, long waterWork,
				long foodWork, long subsidence, long semantic, long visit, long arrival,
				long guest, long guestDepart, long notable, long notableDepart, long processed,
				long festival, long extension, string extensionHappeningCursors,
				string extensionModel, long[] workRan, long[] workNext, long[] clockNext,
				LifecyclePlan lifecycle)
			{
				Now = now; Heartbeat = heartbeat; Fetch = fetch; WaterWork = waterWork;
				FoodWork = foodWork; Subsidence = subsidence; Semantic = semantic; Visit = visit;
				Arrival = arrival; Guest = guest; GuestDepart = guestDepart; Notable = notable;
				NotableDepart = notableDepart; Processed = processed; Festival = festival;
				Extension = extension; ExtensionHappeningCursors = extensionHappeningCursors;
				ExtensionModel = extensionModel; WorkRan = workRan; WorkNext = workNext;
				ClockNext = clockNext; Lifecycle = lifecycle;
			}

			internal static bool TryCreate(KingdomSystem source, long now, long disabledAt,
				out SettlementPlan plan)
			{
				return TryCreateCore(source.LastHeartbeatTick, source.LastFetchTick,
					source.LastWaterWorkTick, source.LastFoodWorkTick, source.LastSubsidenceTick,
					source.SemanticPassActive, source.LastSemanticTick, source.LastVisitTick,
					source.NextArrivalTick, source.NextGuestTick, source.GuestDepartTick,
					source.NextNotableGuestTick, source.NotableGuestDepartTick, source.Population,
					source.Gate, source.Stores, source.LifecycleBook, source.City, now, disabledAt,
					out plan);
			}

			internal static bool TryCreate(KingdomSettlement source, long now, long disabledAt,
				out SettlementPlan plan)
			{
				if (source == null) { plan = null; return false; }
				return TryCreateCore(source.LastHeartbeatTick, source.LastFetchTick,
					source.LastWaterWorkTick, source.LastFoodWorkTick, source.LastSubsidenceTick,
					source.SemanticPassActive, source.LastSemanticTick, source.LastVisitTick,
					source.NextArrivalTick, source.NextGuestTick, source.GuestDepartTick,
					source.NextNotableGuestTick, source.NotableGuestDepartTick, source.Population,
					source.Gate, source.Stores, source.LifecycleBook, source.City, now, disabledAt,
					out plan);
			}

			private static bool TryCreateCore(long oldHeartbeat, long oldFetch, long oldWater,
				long oldFood, long oldSubsidence, bool semanticActive, long oldSemantic,
				long oldVisit, long oldArrival, long oldGuest, long oldGuestDepart,
				long oldNotable, long oldNotableDepart, int population,
				KingdomRules.GatePolicy gate, KingdomRules.StoresPolicy stores,
				KingdomLifecycleBook lifecycle, Simulation.City.KingdomCityBook city,
				long now, long disabledAt, out SettlementPlan plan)
			{
				plan = null;
				long arrivalInterval = lifecycle?.Growth?.ArrivalIntervalTicks ?? 0L;
				if (arrivalInterval <= 0L)
					arrivalInterval = KingdomRules.PolicyInterval(
						KingdomRules.ArrivalIntervalTicks(population), gate, stores);
				long arrival = oldArrival;
				bool openArrival = lifecycle?.Growth?.ArrivalOp != null
					|| lifecycle?.Growth?.ArrivalCandidate != null;
				if (!openArrival)
				{
					if (KingdomGrowth.Enabled)
					{
						if (!KingdomMasterRules.TryFutureDeadline(now, arrivalInterval,
							out arrival)) return false;
					}
					else arrival = 0L;
				}

				long guest = oldGuest;
				if (KingdomLocus.Enabled && !KingdomMasterRules.TryFutureDeadline(now,
					KingdomLocusRules.GuestIntervalTicks, out guest)) return false;
				if (!KingdomLocus.Enabled) guest = 0L;
				long notable = oldNotable;
				if (KingdomGuestbook.GuestsEnabled && !KingdomMasterRules.TryFutureDeadline(now,
					KingdomGuestRules.NotableGuestIntervalTicks, out notable)) return false;
				if (!KingdomGuestbook.GuestsEnabled) notable = 0L;
				long guestDepart = oldGuestDepart;
				long notableDepart = oldNotableDepart;
				// An open lifecycle operation owns its schedule lease. Keep that receipt byte-for-byte;
				// the recovery lane may settle it after resume. Only an unleased standing guest clock
				// is frozen across the disabled span.
				if ((lifecycle?.PlainGuest == null
						&& !KingdomMasterRules.TryResumeCommittedDeadline(oldGuestDepart, disabledAt,
							now, out guestDepart))
					|| (lifecycle?.NotableGuest == null
						&& !KingdomMasterRules.TryResumeCommittedDeadline(oldNotableDepart, disabledAt,
							now, out notableDepart))) return false;

				long futureDay;
				if (!KingdomMasterRules.TryFutureDeadline(now, KingdomRules.TicksPerDay,
					out futureDay)) return false;
				long[] workRan = CopyFilled(city?.WorkRanThroughTicks, now);
				long[] workNext = CopyFilled(city?.WorkNextTicks, futureDay);
				long[] clockNext = CopyFilled(city?.ClockNextDueTicks, futureDay);
				string extensionHappeningCursors;
				string extensionModel;
				if (!Api.KingdomHappeningCursorRules.TryRebaseAfterPause(
						city?.ExtensionHappeningCursors, disabledAt, now,
						out extensionHappeningCursors)
					|| !Api.KingdomBehaviourRules.TryRebaseAfterPause(city?.ExtensionModel,
						disabledAt, now, out extensionModel)) return false;
				LifecyclePlan lifecyclePlan;
				if (!LifecyclePlan.TryCreate(lifecycle, now, arrival, out lifecyclePlan)) return false;
				plan = new SettlementPlan(now,
					lifecycle?.Growth?.HeartbeatOp == null ? now : oldHeartbeat,
					lifecycle?.Growth?.FetchOp == null ? now : oldFetch,
					now, lifecycle?.Growth?.MillOp == null ? now : oldFood,
					lifecycle?.Growth?.HeartbeatOp == null ? now : oldSubsidence,
					semanticActive ? oldSemantic : now, now, arrival, guest, guestDepart,
					notable, notableDepart, now, now, now, extensionHappeningCursors,
					extensionModel, workRan, workNext, clockNext, lifecyclePlan);
				return true;
			}

			private static long[] CopyFilled(List<long> source, long value)
			{
				if (source == null) return null;
				long[] rows = new long[source.Count];
				for (int i = 0; i < rows.Length; i++) rows[i] = value;
				return rows;
			}

			internal void Publish(KingdomSystem target)
			{
				target.LastHeartbeatTick = Heartbeat; target.LastFetchTick = Fetch;
				target.LastWaterWorkTick = WaterWork; target.LastFoodWorkTick = FoodWork;
				target.LastSubsidenceTick = Subsidence; target.LastSemanticTick = Semantic;
				target.LastVisitTick = Visit; target.NextArrivalTick = Arrival;
				target.NextGuestTick = Guest; target.GuestDepartTick = GuestDepart;
				target.NextNotableGuestTick = Notable;
				target.NotableGuestDepartTick = NotableDepart;
				PublishCity(target.City); Lifecycle?.Publish(target.LifecycleBook);
			}

			internal void Publish(KingdomSettlement target)
			{
				target.LastHeartbeatTick = Heartbeat; target.LastFetchTick = Fetch;
				target.LastWaterWorkTick = WaterWork; target.LastFoodWorkTick = FoodWork;
				target.LastSubsidenceTick = Subsidence; target.LastSemanticTick = Semantic;
				target.LastVisitTick = Visit; target.NextArrivalTick = Arrival;
				target.NextGuestTick = Guest; target.GuestDepartTick = GuestDepart;
				target.NextNotableGuestTick = Notable;
				target.NotableGuestDepartTick = NotableDepart;
				PublishCity(target.City); Lifecycle?.Publish(target.LifecycleBook);
			}

			private void PublishCity(Simulation.City.KingdomCityBook city)
			{
				if (city == null) return;
				city.ProcessedThroughTick = Processed; city.LastFestivalTick = Festival;
				city.LastExtensionTick = Extension;
				city.ExtensionHappeningCursors = ExtensionHappeningCursors;
				city.ExtensionModel = ExtensionModel;
				PublishRows(city.WorkRanThroughTicks, WorkRan);
				PublishRows(city.WorkNextTicks, WorkNext);
				PublishRows(city.ClockNextDueTicks, ClockNext);
			}

			private static void PublishRows(List<long> target, long[] values)
			{
				if (target == null || values == null || target.Count != values.Length) return;
				for (int i = 0; i < values.Length; i++) target[i] = values[i];
			}
		}

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
