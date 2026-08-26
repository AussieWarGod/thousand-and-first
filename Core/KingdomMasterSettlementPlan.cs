using System;
using System.Collections.Generic;
using XRL.UI;

namespace ThousandAndFirst
{
	public static partial class KingdomMaster
	{
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
	}
}
