using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Conversations;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{

		/// <summary>
		/// Picks which of the settler blueprints is walking up the road. The roster lives in the
		/// <c>r_KingdomSettlers</c> population table so other mods can put their own people on it
		/// by merging a line, and so the mix can be retuned without touching code.
		/// <para>
		/// Falls back to the base blueprint if the table is missing or rolls nothing: a settlement
		/// that stops growing because a table was overridden badly is a worse failure than a
		/// settlement whose arrivals all look alike.
		/// </para>
		/// </summary>
		[Obsolete("Blueprint selection requires a persisted arrival event coordinate.")]
		public static string SettlerBlueprint()
		{
			// Compatibility-only surface. The live path uses KingdomSemanticSelection and freezes
			// the selected merged-table row in its candidate before object creation.
			return DefaultSettlerBlueprint;
		}

		/// <summary>The neutral body used for non-resident civic transients. Arrivals must instead
		/// use their frozen semantic selection coordinate.</summary>
		internal const string DefaultSettlerBlueprint = "r_KingdomSettler";

		/// <summary>
		/// Why an arrival did not join, when one did not. Addendum 4b splits the one old "no
		/// room" into the two honest answers: there was nowhere to stand, or there was no home
		/// this settler would take.
		/// </summary>
		public struct ArrivalRefusal
		{
			/// <summary>True when the settlement has housing but none of it would take this
			/// person, from <c>KingdomLodging.WouldTakeArrival</c>. False means there was simply
			/// no ground to put them on.</summary>
			public bool NoAcceptableHome;

			/// <summary>Which of the lodging reasons decided it, for the founder's line.</summary>
			public KingdomLodgingRules.UnhousedReason Reason;
		}

		public static bool SpawnSettler(KingdomSystem System, Zone Z, KingdomSurvey Survey = null)
		{
			ArrivalRefusal refusal;
			return SpawnSettler(System, Z, Survey, out refusal);
		}

		/// <summary>
		/// Brings one settler in, or says why not. The lodging gate is asked of the settler
		/// themselves &mdash; created, judged, and let go again if the settlement has no home they
		/// would take &mdash; because what a person needs of a roof is a fact about that person
		/// and not about the blueprint they were rolled from.
		/// </summary>
		public static bool SpawnSettler(KingdomSystem System, Zone Z, KingdomSurvey Survey, out ArrivalRefusal Refusal)
		{
			Refusal = default(ArrivalRefusal);
			if (!KingdomMaster.NewWorkAllowed(System)) return false;
			if (System == null || Z == null || The.Game == null
				|| System.ClaimedZones == null || !System.ClaimedZones.Contains(Z.ZoneID)) return false;
			KingdomSurvey survey = Survey ?? KingdomSurvey.Take(Z, System);
			int reconciled;
			bool reconciledOpen;
			ArrivalResult reconciledResult;
			if (!SynchronizeArrivalAuthority(System, Z, survey, The.Game.TimeTicks,
				out reconciled, out reconciledOpen, out reconciledResult, out Refusal)) return false;
			if (reconciledOpen && reconciledResult != ArrivalResult.Deferred)
				return reconciledResult == ArrivalResult.Joined;
			if (!Enabled) return false;
			if (!AdvanceArrivalCadence(System, Z, The.Game.TimeTicks)) return false;
			return ResolveOrStartArrival(System, Z, survey, The.Game.TimeTicks,
				out Refusal) == ArrivalResult.Joined;
		}

		private static bool SynchronizeArrivalAuthority(KingdomSystem system, Zone zone,
			KingdomSurvey survey, long tick, out int reconciledArrivals,
			out bool reconciledOpen, out ArrivalResult reconciledResult,
			out ArrivalRefusal reconciledRefusal)
		{
			reconciledArrivals = 0;
			reconciledOpen = false;
			reconciledResult = ArrivalResult.Failed;
			reconciledRefusal = default(ArrivalRefusal);
			KingdomLifecycleBook parent = system?.LifecycleBook;
			string settlementId = system?.CurrentSettlementId;
			if (parent == null || zone == null || system.ClaimedZones == null
				|| !system.ClaimedZones.Contains(zone.ZoneID) || string.IsNullOrEmpty(settlementId)
				|| !string.Equals(parent.SettlementId, settlementId, StringComparison.Ordinal)
				|| !KingdomLifecycleRules.CanOwnAuthority(parent))
			{
				KingdomLog.Log("growth arrival refused: lifecycle authority is invalid or quarantined");
				return false;
			}
			if (!TryArrivalCohort(system, parent.Growth, out int cohort)) return false;
			long interval = Interval(system, zone, cohort);
			if (parent.Growth != null && parent.Growth.MigrationPending
				&& !TryMigrateArrivalAuthority(system, parent, tick, interval))
			{
				KingdomLog.Log("growth arrival refused: staged lifecycle migration did not publish");
				return false;
			}
			KingdomGrowthBook growth = parent.Growth;
			if (!KingdomLifecycleRules.CanOwnGrowthAuthority(parent)
				|| !KingdomLifecycleRules.CanOwnGrowthAuthority(growth, settlementId))
			{
				KingdomLog.Log("growth arrival refused: growth authority is invalid or quarantined");
				return false;
			}
			if (!growth.ArrivalCadenceMigrationPending
				&& !AdvanceArrivalCadence(system, zone, tick)) return false;
			bool lastObservedHealthy = growth.HealthState == KingdomGrowthHealthState.Healthy;
			bool wasPaused = growth.WorkPaused;
			KingdomGrowthAvailabilityDecision decision =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, Enabled,
					lastObservedHealthy, tick, interval);
			bool open = growth.ArrivalCandidate != null || growth.ArrivalOp != null;
			if (!decision.Valid || (open && system.NextArrivalTick != growth.NextArrivalTick)
				|| (!open && !decision.RestampClocks && system.NextArrivalTick > 0
					&& system.NextArrivalTick != growth.NextArrivalTick))
			{
				KingdomLog.Log("growth arrival refused: real clock cannot bind availability decision");
				return false;
			}
			if (!KingdomLifecycleRules.ApplyGrowthAvailability(growth, decision))
			{
				KingdomLog.Log("growth arrival refused: availability observation did not publish");
				return false;
			}
			if (growth.ArrivalCadenceResumePending && !growth.ArrivalCadenceMigrationPending
				&& growth.ArrivalCandidate == null && growth.ArrivalOp == null
				&& !KingdomLifecycleRules.TryRestartGrowthArrivalCadenceAfterPause(growth,
					tick, interval, cohort, KingdomSemanticSelectionRules.RulesVersion,
					out string restartFailure))
				return CadenceFault("resume", restartFailure);
			if (wasPaused && !growth.WorkPaused) system.NextArrivalTick = growth.NextArrivalTick;
			if (open)
			{
				reconciledResult = ReconcileArrival(system, zone, survey, tick,
					out reconciledRefusal, null, false);
				if (reconciledResult == ArrivalResult.Failed) return false;
				reconciledOpen = true;
				reconciledArrivals = reconciledResult == ArrivalResult.Joined ? 1 : 0;
			}
			else if (decision.RestampClocks || system.NextArrivalTick <= 0)
			{
				system.NextArrivalTick = growth.NextArrivalTick;
			}
			else if (system.NextArrivalTick != growth.NextArrivalTick)
			{
				KingdomLog.Log("growth arrival refused: real arrival clock differs from lifecycle authority");
				return false;
			}
			return KingdomLifecycleRules.CanOwnGrowthAuthority(parent)
				&& system.NextArrivalTick == growth.NextArrivalTick;
		}

		private static bool TryMigrateArrivalAuthority(KingdomSystem system,
			KingdomLifecycleBook parent, long tick, long interval)
		{
			KingdomGrowthMigrationInput input = new KingdomGrowthMigrationInput
			{
				HasNow = true,
				Now = tick,
				PendingCrop = system.PendingCrop,
				PendingCropBlueprint = system.PendingCropBlueprint,
				PendingCropZoneId = system.PendingCropZoneId,
				OptionEnabled = Enabled,
				ScarcityEnabled = ScarcityEnabled,
				Healthy = false,
				ArrivalIntervalTicks = interval
			};
			KingdomGrowthMigrationResult migration =
				KingdomLifecycleRules.ApplyGrowthMigration(parent, input);
			if (!migration.Valid
				|| !KingdomLifecycleRules.TryPublishGrowthMigration(parent, migration))
				return false;
			system.NextArrivalTick = parent.Growth.NextArrivalTick;
			return KingdomLifecycleRules.CanOwnGrowthAuthority(parent)
				&& system.NextArrivalTick == parent.Growth.NextArrivalTick;
		}

		private static bool PublishArrivalHealth(KingdomSystem system, Zone zone,
			long tick, bool healthy)
		{
			KingdomLifecycleBook parent = system?.LifecycleBook;
			KingdomGrowthBook growth = parent?.Growth;
			string settlementId = system?.CurrentSettlementId;
			if (growth == null || string.IsNullOrEmpty(settlementId)
				|| system.NextArrivalTick != growth.NextArrivalTick
				|| !KingdomLifecycleRules.CanOwnGrowthAuthority(parent)
				|| !KingdomLifecycleRules.CanOwnGrowthAuthority(growth, settlementId)) return false;
			if (!TryArrivalCohort(system, growth, out int cohort)) return false;
			long interval = Interval(system, zone, cohort);
			KingdomGrowthAvailabilityDecision decision =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, Enabled, healthy, tick,
					interval);
			if (!decision.Valid || !KingdomLifecycleRules.ApplyGrowthAvailability(growth,
				decision)) return false;
			if (growth.ArrivalCadenceResumePending && !growth.ArrivalCadenceMigrationPending
				&& growth.ArrivalCandidate == null && growth.ArrivalOp == null
				&& !KingdomLifecycleRules.TryRestartGrowthArrivalCadenceAfterPause(growth,
					tick, interval, cohort, KingdomSemanticSelectionRules.RulesVersion,
					out string restartFailure))
				return CadenceFault("health resume", restartFailure);
			system.NextArrivalTick = growth.NextArrivalTick;
			return KingdomLifecycleRules.CanOwnGrowthAuthority(parent)
				&& system.NextArrivalTick == growth.NextArrivalTick;
		}
	}
}
