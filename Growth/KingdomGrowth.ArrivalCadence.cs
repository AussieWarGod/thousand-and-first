using System;
using XRL.World;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		/// <summary>Publishes semantic elapsed-time debt without creating or moving an actor.
		/// The first head is catalog-frozen before a changed cohort can start another epoch.</summary>
		private static bool AdvanceArrivalCadence(KingdomSystem system, Zone zone, long tick)
		{
			KingdomGrowthBook growth = system?.LifecycleBook?.Growth;
			if (growth == null || zone == null) return false;
			int cohort;
			if (!TryArrivalCohort(system, growth, out cohort)) return false;
			long interval = Interval(system, zone, cohort);
			int rulesVersion = KingdomSemanticSelectionRules.RulesVersion;
			if (growth.ArrivalCadenceMigrationPending)
			{
				if (growth.ArrivalCandidate != null || growth.ArrivalOp != null) return true;
				if (!KingdomLifecycleRules.TryBindHistoricalGrowthArrivalCadence(growth, tick,
					interval, cohort, rulesVersion, out string migrationFailure))
					return CadenceFault("migration bind", migrationFailure);
				system.NextArrivalTick = growth.NextArrivalTick;
			}
			if (growth.ArrivalCandidate != null || growth.ArrivalOp != null)
			{
				system.NextArrivalTick = growth.NextArrivalTick;
				return true;
			}
			if (growth.ArrivalCadenceResumePending
				&& !KingdomLifecycleRules.TryRestartGrowthArrivalCadenceAfterPause(growth,
					tick, interval, cohort, rulesVersion, out string restartFailure))
				return CadenceFault("deferred resume", restartFailure);
			if (!KingdomLifecycleRules.TryAdvanceGrowthArrivalCadence(growth, tick, interval,
				cohort, rulesVersion, out string advanceFailure))
				return CadenceFault("advance", advanceFailure);
			if (growth.WorkPaused)
			{
				system.NextArrivalTick = growth.NextArrivalTick;
				return true;
			}
			if (growth.ArrivalOpportunity == null && growth.ArrivalDebtRanges.Count > 0)
			{
				KingdomGrowthArrivalDebtRange head = growth.ArrivalDebtRanges[0];
				bool firstGuest = head.FirstOrdinal == 1UL;
				if (firstGuest && !TryCivicStoryAllowsFirstGuest(system, tick,
					out firstGuest)) return CadenceFault("story observation", null);
				KingdomSemanticPersonPlan person;
				if (!KingdomSemanticSelection.TryPrepareGrowthArrivalPayload(system,
					head.FirstOrdinal, head.FirstDueTick, firstGuest, out person,
					out string semanticFailure)) return CadenceFault("catalog freeze", semanticFailure);
				KingdomGrowthArrivalOpportunity opportunity;
				if (!KingdomLifecycleRules.TryFreezeGrowthArrivalOpportunity(growth,
					person.RulesVersion, KingdomLifecycleRules.GrowthArrivalEventStreamId,
					KingdomLifecycleRules.GrowthArrivalEventKindCode, firstGuest,
					person.Blueprint, person.Origin, string.IsNullOrEmpty(person.Creed) ? "-" : person.Creed,
					person.Name, person.Arrived, out opportunity))
					return CadenceFault("head publication", null);
				if (!TryArrivalCohort(system, growth, out cohort)) return false;
				interval = Interval(system, zone, cohort);
				long transitionTick = Math.Max(opportunity.DueTick,
					growth.ArrivalProcessedThroughTick);
				if (!KingdomLifecycleRules.TryAdvanceGrowthArrivalCadence(growth,
					transitionTick, interval, cohort, rulesVersion, out advanceFailure)
					|| !KingdomLifecycleRules.TryAdvanceGrowthArrivalCadence(growth, tick,
						interval, cohort, rulesVersion, out advanceFailure))
					return CadenceFault("cohort transition", advanceFailure);
			}
			system.NextArrivalTick = growth.NextArrivalTick;
			return KingdomLifecycleRules.CanOwnGrowthAuthority(growth, growth.SettlementId);
		}

		private static bool TryArrivalCohort(KingdomSystem system, KingdomGrowthBook growth,
			out int cohort)
		{
			cohort = 0;
			if (system == null || growth == null || system.Population < 0
				|| system.Population == int.MaxValue) return false;
			bool outstanding = growth.ArrivalOpportunity != null || growth.ArrivalCandidate != null;
			cohort = system.Population + (outstanding ? 1 : 0);
			return cohort >= 0;
		}

		private static bool TransitionArrivalCadenceForRetirement(KingdomSystem system,
			Zone zone, KingdomGrowthBook growth, KingdomGrowthOperation operation,
			KingdomGrowthArrivalOpportunity opportunity, long observationTick)
		{
			if (growth == null || operation == null || zone == null) return false;
			if (growth.ArrivalCadenceMigrationPending) return opportunity == null;
			if (opportunity == null || system == null || system.Population < 0) return false;
			int cohort = system.Population;
			long interval = Interval(system, zone, cohort);
			return KingdomLifecycleRules.TryTransitionGrowthArrivalCadenceForRetirement(
				growth, opportunity, operation.UpdatedTick, observationTick, interval, cohort,
				KingdomSemanticSelectionRules.RulesVersion, out string failure)
				|| CadenceFault("retirement transition", failure);
		}

		private static bool CadenceFault(string stage, string failure)
		{
			KingdomLog.Log("growth arrival cadence " + stage + " refused"
				+ (failure == null ? string.Empty : ": " + failure));
			return false;
		}
	}
}
