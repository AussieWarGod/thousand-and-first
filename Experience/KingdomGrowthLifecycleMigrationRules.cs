using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ThousandAndFirst.Simulation.City;
namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		public static bool CanOwnGrowthAuthority(KingdomLifecycleBook Parent)
		{
			return Parent != null && CanOwnAuthority(Parent) && Parent.Growth != null &&
				CanOwnGrowthAuthority(Parent.Growth, Parent.SettlementId);
		}

		public static bool CanOwnGrowthAuthority(KingdomGrowthBook Book, string SettlementId)
		{
			return Book != null && !Book.Quarantined && Book.OpaquePayload == null &&
				Book.FormatVersion == CurrentGrowthFormatVersion && !Book.MigrationPending &&
				ValidRootId(SettlementId) && Book.IdentityBound &&
				string.Equals(Book.SettlementId, SettlementId, StringComparison.Ordinal) &&
				string.Equals(Book.IdentityProof, GrowthIdentityProof(SettlementId),
					StringComparison.Ordinal) && GrowthRootShape(Book, ValidateOperations: true)
				&& KingdomLifecycleWireCodec.GrowthPayloadFitsAggregateCap(Book);
		}

		/// <summary>Structural writer gate. Opaque bytes are exact evidence; parsed current
		/// envelopes must be canonical authority, a staged v5 migration, or a canonical empty
		/// quarantine. Programmatic malformed state is refused rather than truncated.</summary>
		public static bool GrowthEnvelopeWritable(KingdomGrowthBook Book)
		{
			if (Book == null || Book.FormatVersion != CurrentGrowthFormatVersion ||
				TooLong(Book.Fault, MaxTextChars)) return false;
			if (Book.OpaquePayload != null)
				return KingdomLifecycleWireCodec.OpaqueGrowthEnvelopeWritable(Book);
			if (Book.OpaqueWireVersion != 0 || !GrowthCollectionsBounded(Book)) return false;
			bool shape = Book.MigrationPending ? StagedGrowthShape(Book)
				: Book.Quarantined ? CanonicalQuarantinedGrowth(Book)
					: GrowthRootShape(Book, ValidateOperations: true);
			return shape && KingdomLifecycleWireCodec.GrowthPayloadFitsAggregateCap(Book);
		}

		/// <summary>Validates the exact v5 outer graph before installing a dormant staged
		/// Growth book. No clock or pending-crop authority is fabricated here.</summary>
		public static bool TryStageGrowthMigrationFromV5(KingdomLifecycleBook Book,
			out KingdomGrowthBook Staged)
		{
			Staged = null;
			if (Book == null || Book.FormatVersion != LegacyLifecycleFormatVersion ||
				Book.WireRejected || !LegacyResourceKindsOnly(Book)) return false;
			int version = Book.FormatVersion;
			KingdomGrowthBook prior = Book.Growth;
			KingdomGrowthBook staged = NewStagedGrowth();
			Book.FormatVersion = CurrentFormatVersion;
			bool unbound = !Book.IdentityBound;
			Book.Growth = unbound ? new KingdomGrowthBook() : staged;
			bool valid = PristineLifecycleBook(Book) ||
				CanonicalLifecycleQuarantine(Book) ||
				(!Book.Quarantined && ValidRootId(Book.SettlementId) && Book.IdentityBound &&
				 ExactSettlementIdentityProof(Book) && LifecycleBookShape(Book));
			Book.FormatVersion = version;
			Book.Growth = prior;
			if (!valid) return false;
			Staged = unbound ? new KingdomGrowthBook() : staged;
			return true;
		}

		public static bool StageLegacyGrowthMigration(KingdomLifecycleBook Book)
		{
			if (!TryStageGrowthMigrationFromV5(Book, out KingdomGrowthBook staged)) return false;
			Book.FormatVersion = CurrentFormatVersion;
			Book.Growth = staged;
			return true;
		}

		/// <summary>Lifecycle v6 had no causal raid ledger. Its old open raid operation is
		/// retained as raw evidence but quarantined; it is never promoted from standing or faction
		/// fields into a grievance. Authority-free v6 books receive one empty v7 ledger.</summary>
		internal static bool StageRaidMigrationFromV6(KingdomLifecycleBook Book)
		{
			if (Book == null || Book.FormatVersion != PreviousLifecycleFormatVersion
				|| Book.WireRejected) return false;
			Book.RaidLedger = new KingdomRaidLedger();
			Book.FormatVersion = CurrentFormatVersion;
			QuarantineLegacyRaidAuthority(Book);
			Normalize(Book);
			return PristineLifecycleBook(Book) || CanonicalLifecycleQuarantine(Book)
				|| CanOwnAuthority(Book);
		}

		/// <summary>Preserves an old open raid plan without allowing it to mutate the world.</summary>
		internal static void QuarantineLegacyRaidAuthority(KingdomLifecycleBook Book)
		{
			if (Book == null || Book.Raid == null) return;
			Book.Quarantined = true;
			if (string.IsNullOrEmpty(Book.Fault))
				Book.Fault = "legacy raid authority retained without causal grievance";
		}

		public static KingdomGrowthMigrationResult ApplyGrowthMigration(
			KingdomLifecycleBook Parent, KingdomGrowthMigrationInput Input)
		{
			KingdomGrowthMigrationResult result = new KingdomGrowthMigrationResult
			{
				Failure = "growth migration input is invalid"
			};
			if (Parent == null || !CanOwnAuthority(Parent) ||
				!ValidRootId(Parent.SettlementId) || !Parent.IdentityBound ||
				!ExactSettlementIdentityProof(Parent) || Parent.Growth == null ||
				!StagedGrowthShape(Parent.Growth) || Input == null || !Input.HasNow ||
				Input.Now < 0L || Input.ArrivalIntervalTicks <= 0L ||
				!ValidCount(Input.PendingCrop) || TooLong(Input.PendingCropBlueprint, MaxNameChars) ||
				TooLong(Input.PendingCropZoneId, MaxNameChars)
				|| (Input.PendingCrop == 0 ? (!string.IsNullOrEmpty(Input.PendingCropBlueprint)
					|| !string.IsNullOrEmpty(Input.PendingCropZoneId))
					: (!ValidName(Input.PendingCropBlueprint) || !ValidName(Input.PendingCropZoneId))))
				return result;
			long arrival;
			if (!CheckedAdd(Input.Now, Input.ArrivalIntervalTicks, out arrival))
			{
				result.Failure = "growth arrival migration clock overflowed";
				return result;
			}
			KingdomGrowthBook growth = NewBoundGrowth(Parent.SettlementId);
			if (growth == null) return result;
			growth.MigratedFromLifecycleVersion = LegacyLifecycleFormatVersion;
			growth.MigrationTick = Input.Now;
			growth.OptionState = Input.OptionEnabled ? KingdomLifecycleOptionState.Enabled :
				KingdomLifecycleOptionState.Disabled;
			growth.OptionTick = Input.Now;
			growth.HealthState = Input.Healthy ? KingdomGrowthHealthState.Healthy :
				KingdomGrowthHealthState.Unhealthy;
			growth.HealthTick = Input.Now;
			growth.ScarcityOptionState = Input.ScarcityEnabled
				? KingdomLifecycleOptionState.Enabled : KingdomLifecycleOptionState.Disabled;
			growth.ScarcityOptionTick = Input.Now;
			growth.WorkPaused = !Input.OptionEnabled || !Input.Healthy;
			growth.WorkPauseStartedTick = growth.WorkPaused ? Input.Now : 0L;
			growth.WorkPausedTicks = 0L;
			growth.EffectiveWorkTick = Input.Now;
			growth.LastHeartbeatTick = Input.Now;
			growth.NextArrivalTick = growth.WorkPaused ? 0L : arrival;
			growth.ArrivalIntervalTicks = Input.ArrivalIntervalTicks;
			growth.LastFetchTick = Input.Now;
			growth.LastMillTick = Input.Now;
			growth.LastSubsidenceTick = Input.Now;
			growth.PendingCrop = Input.PendingCrop;
			growth.PendingCropBlueprint = Input.PendingCrop == 0 ? null : Input.PendingCropBlueprint;
			growth.PendingCropZoneId = Input.PendingCrop == 0 ? null : Input.PendingCropZoneId;
			if (!CanOwnGrowthAuthority(growth, Parent.SettlementId))
			{
				result.Failure = "detached growth migration result is malformed";
				return result;
			}
			result.Valid = true;
			result.Failure = null;
			result.Growth = growth;
			return result;
		}

		public static bool TryPublishGrowthMigration(KingdomLifecycleBook Parent,
			KingdomGrowthMigrationResult Result)
		{
			if (Parent == null || !CanOwnAuthority(Parent) || Result == null
				|| !Result.Valid || Result.Growth == null ||
				!StagedGrowthShape(Parent.Growth) || !ValidRootId(Parent.SettlementId) ||
				!ExactSettlementIdentityProof(Parent) ||
				!CanOwnGrowthAuthority(Result.Growth, Parent.SettlementId)) return false;
			KingdomGrowthBook detached;
			try
			{
				detached = KingdomLifecycleWireCodec.ReadGrowthPayload(
					KingdomLifecycleWireCodec.GrowthPayloadForWrite(Result.Growth));
			}
			catch (Exception) { return false; }
			if (!CanOwnGrowthAuthority(detached, Parent.SettlementId)) return false;
			Parent.Growth = detached;
			return true;
		}

		public static KingdomGrowthAvailabilityDecision ObserveGrowthAvailability(
			KingdomGrowthBook Book, bool OptionEnabled, bool Healthy, long Now,
			long CurrentArrivalIntervalTicks)
		{
			KingdomGrowthAvailabilityDecision result = new KingdomGrowthAvailabilityDecision
			{
				Failure = "growth availability observation is malformed",
				ReconcileOpen = HasOpenGrowthOperation(Book)
			};
			if (Book == null || Book.Quarantined || Book.MigrationPending || Now < 0L ||
				CurrentArrivalIntervalTicks <= 0L || Book.OptionTick < 0L || Book.HealthTick < 0L ||
				Book.EffectiveWorkTick < 0L || Book.WorkPausedTicks < 0L ||
				!KnownOption(Book.OptionState) || !KnownGrowthHealth(Book.HealthState)
				|| Now < Book.OptionTick || Now < Book.HealthTick || Now < Book.EffectiveWorkTick
				|| Now < Book.LastHeartbeatTick || Now < Book.LastFetchTick
				|| Now < Book.LastMillTick || Now < Book.LastSubsidenceTick
				|| Now < Book.MigrationTick) return result;
			bool active = OptionEnabled && Healthy;
			bool wasActive = Book.OptionState == KingdomLifecycleOptionState.Enabled &&
				Book.HealthState == KingdomGrowthHealthState.Healthy && !Book.WorkPaused;
			long paused = Book.WorkPausedTicks;
			if (Book.WorkPaused && Book.WorkPauseStartedTick > Now) return result;
			if (Book.WorkPaused && active &&
				!CheckedAdd(paused, Now - Book.WorkPauseStartedTick, out paused)) return result;
			long nextArrival = Book.NextArrivalTick;
			bool restamp = active != wasActive || Book.OptionState == KingdomLifecycleOptionState.Unknown ||
				Book.HealthState == KingdomGrowthHealthState.Unknown;
			bool openArrival = Book.ArrivalOp != null || Book.ArrivalCandidate != null
				|| HasGrowthArrivalSemanticDebt(Book);
			if (!Book.ArrivalCadenceMigrationPending) nextArrival = Book.NextArrivalTick;
			else if (!active) nextArrival = openArrival ? Book.NextArrivalTick : 0L;
			else if (restamp && !openArrival
				&& !CheckedAdd(Now, CurrentArrivalIntervalTicks, out nextArrival))
				return result;
			long effectiveAnchor = active ? Now : (Book.WorkPaused ?
				Book.WorkPauseStartedTick : Now);
			if (effectiveAnchor < paused) return result;
			long effectiveNow = effectiveAnchor - paused;
			result.Valid = true; result.Failure = null; result.AllowStarters = active;
			result.OptionState = OptionEnabled ? KingdomLifecycleOptionState.Enabled :
				KingdomLifecycleOptionState.Disabled;
			result.HealthState = Healthy ? KingdomGrowthHealthState.Healthy :
				KingdomGrowthHealthState.Unhealthy;
			result.ObservedTick = Now; result.WorkPaused = !active;
			result.PauseStartedTick = active ? 0L : (Book.WorkPaused ?
				Book.WorkPauseStartedTick : Now);
			result.PausedTicks = paused; result.EffectiveWorkTick = restamp ? effectiveNow :
				Book.EffectiveWorkTick; result.RestampClocks = restamp;
			result.NextArrivalTick = nextArrival;
			result.ArrivalIntervalTicks = CurrentArrivalIntervalTicks;
			return result;
		}

		public static bool ApplyGrowthAvailability(KingdomGrowthBook Book,
			KingdomGrowthAvailabilityDecision Decision)
		{
			if (Book == null || Decision == null || !Decision.Valid ||
				!CanOwnGrowthAuthority(Book, Book.SettlementId)) return false;
			KingdomGrowthAvailabilityDecision expected = ObserveGrowthAvailability(Book,
				Decision.OptionState == KingdomLifecycleOptionState.Enabled,
				Decision.HealthState == KingdomGrowthHealthState.Healthy,
				Decision.ObservedTick, Decision.ArrivalIntervalTicks);
			if (!expected.Valid || expected.AllowStarters != Decision.AllowStarters
				|| expected.ReconcileOpen != Decision.ReconcileOpen
				|| expected.OptionState != Decision.OptionState
				|| expected.HealthState != Decision.HealthState
				|| expected.WorkPaused != Decision.WorkPaused
				|| expected.PauseStartedTick != Decision.PauseStartedTick
				|| expected.PausedTicks != Decision.PausedTicks
				|| expected.EffectiveWorkTick != Decision.EffectiveWorkTick
				|| expected.RestampClocks != Decision.RestampClocks
				|| expected.NextArrivalTick != Decision.NextArrivalTick) return false;
			KingdomLifecycleOptionState oldOption = Book.OptionState;
			long oldOptionTick = Book.OptionTick;
			KingdomGrowthHealthState oldHealth = Book.HealthState;
			long oldHealthTick = Book.HealthTick;
			bool oldPaused = Book.WorkPaused; long oldPauseStart = Book.WorkPauseStartedTick;
			long oldPausedTicks = Book.WorkPausedTicks; long oldEffective = Book.EffectiveWorkTick;
			bool oldArrivalResume = Book.ArrivalCadenceResumePending;
			long oldArrival = Book.NextArrivalTick; long oldInterval = Book.ArrivalIntervalTicks;
			Book.OptionState = Decision.OptionState; Book.OptionTick = Decision.ObservedTick;
			Book.HealthState = Decision.HealthState; Book.HealthTick = Decision.ObservedTick;
			Book.WorkPaused = Decision.WorkPaused;
			Book.WorkPauseStartedTick = Decision.PauseStartedTick;
			Book.WorkPausedTicks = Decision.PausedTicks;
			Book.EffectiveWorkTick = Decision.EffectiveWorkTick;
			if (!Book.ArrivalCadenceMigrationPending && oldPaused && !Decision.WorkPaused)
				Book.ArrivalCadenceResumePending = true;
			Book.NextArrivalTick = Decision.NextArrivalTick;
			if (Book.ArrivalCadenceMigrationPending && Book.ArrivalCandidate == null
				&& Book.ArrivalOp == null)
				Book.ArrivalIntervalTicks = Decision.ArrivalIntervalTicks;
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			Book.OptionState = oldOption; Book.OptionTick = oldOptionTick;
			Book.HealthState = oldHealth; Book.HealthTick = oldHealthTick;
			Book.WorkPaused = oldPaused; Book.WorkPauseStartedTick = oldPauseStart;
			Book.WorkPausedTicks = oldPausedTicks; Book.EffectiveWorkTick = oldEffective;
			Book.ArrivalCadenceResumePending = oldArrivalResume;
			Book.NextArrivalTick = oldArrival; Book.ArrivalIntervalTicks = oldInterval;
			return false;
		}

		public static bool TryEffectiveWorkElapsed(KingdomGrowthBook Book, long Now,
			out long Elapsed)
		{
			Elapsed = 0L;
			if (Book == null || Book.WorkPaused ||
				Book.OptionState != KingdomLifecycleOptionState.Enabled ||
				Book.HealthState != KingdomGrowthHealthState.Healthy) return false;
			long effectiveNow;
			if (!TryGrowthEffectiveNow(Book, Now, out effectiveNow)
				|| effectiveNow < Book.EffectiveWorkTick) return false;
			Elapsed = effectiveNow - Book.EffectiveWorkTick;
			return true;
		}
	}
}
