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
		public static void NormalizeGrowth(KingdomGrowthBook Book)
		{
			if (Book == null || GrowthEnvelopeWritable(Book)) return;
			Book.FormatVersion = CurrentGrowthFormatVersion;
			Book.Quarantined = true;
			Book.Fault = "malformed growth authority was quarantined";
			Book.OpaqueWireVersion = 0;
			Book.OpaquePayload = null;
			Book.SettlementId = null; Book.IdentityBound = false; Book.IdentityProof = null;
			Book.MigratedFromLifecycleVersion = 0; Book.MigrationPending = false;
			Book.MigrationTick = 0L; Book.OptionState = KingdomLifecycleOptionState.Unknown;
			Book.OptionTick = 0L; Book.HealthState = KingdomGrowthHealthState.Unknown;
			Book.HealthTick = 0L; Book.ScarcityOptionState = KingdomLifecycleOptionState.Unknown;
			Book.ScarcityOptionTick = 0L; Book.WorkPaused = false; Book.WorkPauseStartedTick = 0L;
			Book.WorkPausedTicks = 0L; Book.EffectiveWorkTick = 0L;
			Book.LastHeartbeatTick = 0L; Book.NextArrivalTick = 0L;
			Book.ArrivalIntervalTicks = 0L; Book.LastFetchTick = 0L;
			Book.LastMillTick = 0L; Book.LastSubsidenceTick = 0L;
			Book.LastDeliveryTick = 0L; Book.LastDepartureTick = 0L;
			Book.PendingCrop = 0; Book.PendingCropBlueprint = null; Book.PendingCropZoneId = null;
			Book.HeartbeatNextSequence = Book.ArrivalNextSequence =
				Book.DepartureNextSequence = Book.DeliveryNextSequence = 1L;
			Book.FetchNextSequence = Book.MillNextSequence = 1L;
			Book.ArrivalCandidateNextSequence = 1L;
			Book.HeartbeatRetiredThrough = Book.ArrivalRetiredThrough =
				Book.DepartureRetiredThrough = Book.DeliveryRetiredThrough = 0L;
			Book.FetchRetiredThrough = Book.MillRetiredThrough = 0L;
			Book.ArrivalCandidateRetiredThrough = 0L;
			Book.HeartbeatOp = Book.ArrivalOp = Book.DepartureOp = Book.DeliveryOp = null;
			Book.FetchOp = Book.MillOp = null; Book.ArrivalCandidate = null;
			Book.FieldOps = new List<KingdomGrowthFieldSlot>();
			Book.CropRows = new List<KingdomGrowthCropRow>();
			Book.Resources = new List<KingdomLifecycleResourceRevision>();
			Book.RecentProofs = new List<KingdomGrowthProof>();
		}

		private static KingdomGrowthBook NewStagedGrowth()
		{
			return new KingdomGrowthBook
			{
				FormatVersion = CurrentGrowthFormatVersion,
				MigratedFromLifecycleVersion = LegacyLifecycleFormatVersion,
				MigrationPending = true
			};
		}

		private static KingdomGrowthBook NewBoundGrowth(string settlementId)
		{
			if (!ValidRootId(settlementId)) return null;
			KingdomGrowthBook result = new KingdomGrowthBook
			{
				FormatVersion = CurrentGrowthFormatVersion,
				SettlementId = settlementId,
				IdentityBound = true,
				IdentityProof = GrowthIdentityProof(settlementId)
			};
			return result;
		}

		private static string GrowthIdentityProof(string settlementId)
		{
			return HashId("growth-binding", delegate(BinaryWriter w)
			{
				CanonicalString(w, settlementId);
			});
		}

		private static bool KnownGrowthHealth(KingdomGrowthHealthState state)
		{
			return Enum.IsDefined(typeof(KingdomGrowthHealthState), state);
		}

		private static bool KnownGrowthAction(KingdomGrowthAction action)
		{
			return Enum.IsDefined(typeof(KingdomGrowthAction), action) &&
				action != KingdomGrowthAction.None;
		}

		private static bool KnownGrowthPhase(KingdomGrowthPhase phase)
		{
			return Enum.IsDefined(typeof(KingdomGrowthPhase), phase) &&
				phase != KingdomGrowthPhase.Invalid;
		}

		private static bool KnownGrowthSlot(KingdomGrowthSlotKind slot)
		{
			return Enum.IsDefined(typeof(KingdomGrowthSlotKind), slot) &&
				slot != KingdomGrowthSlotKind.None;
		}

		private static bool GrowthCollectionsBounded(KingdomGrowthBook book)
		{
			return book != null && book.FieldOps != null && book.FieldOps.Count <= MaxGrowthFields
				&& book.CropRows != null && book.CropRows.Count <= MaxGrowthCropRows
				&& book.Resources != null && book.Resources.Count <= MaxResourceRows
				&& book.RecentProofs != null && book.RecentProofs.Count <= MaxRecentProofs;
		}

		private static bool PristineGrowthBook(KingdomGrowthBook book)
		{
			return book != null && book.FormatVersion == CurrentGrowthFormatVersion
				&& !book.Quarantined && book.Fault == null
				&& book.OpaqueWireVersion == 0 && book.OpaquePayload == null
				&& book.SettlementId == null && !book.IdentityBound
				&& book.IdentityProof == null
				&& book.MigratedFromLifecycleVersion == 0 && !book.MigrationPending
				&& book.MigrationTick == 0L && book.OptionState == KingdomLifecycleOptionState.Unknown
				&& book.OptionTick == 0L && book.HealthState == KingdomGrowthHealthState.Unknown
				&& book.HealthTick == 0L
				&& book.ScarcityOptionState == KingdomLifecycleOptionState.Unknown
				&& book.ScarcityOptionTick == 0L && !book.WorkPaused
				&& book.WorkPauseStartedTick == 0L
				&& book.WorkPausedTicks == 0L && book.EffectiveWorkTick == 0L
				&& book.LastHeartbeatTick == 0L && book.NextArrivalTick == 0L
				&& book.ArrivalIntervalTicks == 0L && book.LastFetchTick == 0L
				&& book.LastMillTick == 0L && book.LastSubsidenceTick == 0L
				&& book.LastDeliveryTick == 0L && book.LastDepartureTick == 0L
				&& book.PendingCrop == 0 && book.PendingCropBlueprint == null
				&& book.PendingCropZoneId == null
				&& book.HeartbeatNextSequence == 1L && book.HeartbeatRetiredThrough == 0L
				&& book.ArrivalNextSequence == 1L && book.ArrivalRetiredThrough == 0L
				&& book.DepartureNextSequence == 1L && book.DepartureRetiredThrough == 0L
				&& book.DeliveryNextSequence == 1L && book.DeliveryRetiredThrough == 0L
				&& book.FetchNextSequence == 1L && book.FetchRetiredThrough == 0L
				&& book.MillNextSequence == 1L && book.MillRetiredThrough == 0L
				&& book.ArrivalCandidateNextSequence == 1L
				&& book.ArrivalCandidateRetiredThrough == 0L
				&& book.HeartbeatOp == null && book.ArrivalOp == null
				&& book.DepartureOp == null && book.DeliveryOp == null
				&& book.FetchOp == null && book.MillOp == null && book.ArrivalCandidate == null
				&& GrowthCollectionsBounded(book) && book.FieldOps.Count == 0
				&& book.CropRows.Count == 0 && book.Resources.Count == 0
				&& book.RecentProofs.Count == 0;
		}

		internal static bool OpaqueGrowthParsedStateIsPristine(KingdomGrowthBook book)
		{
			if (book == null) return false;
			bool quarantined = book.Quarantined;
			string fault = book.Fault;
			int wireVersion = book.OpaqueWireVersion;
			byte[] payload = book.OpaquePayload;
			try
			{
				book.Quarantined = false;
				book.Fault = null;
				book.OpaqueWireVersion = 0;
				book.OpaquePayload = null;
				return PristineGrowthBook(book);
			}
			finally
			{
				book.Quarantined = quarantined;
				book.Fault = fault;
				book.OpaqueWireVersion = wireVersion;
				book.OpaquePayload = payload;
			}
		}

		private static bool StagedGrowthShape(KingdomGrowthBook book)
		{
			if (book == null || book.FormatVersion != CurrentGrowthFormatVersion || book.Quarantined
				|| book.Fault != null || book.OpaquePayload != null
				|| book.OpaqueWireVersion != 0 || !book.MigrationPending
				|| book.MigratedFromLifecycleVersion != LegacyLifecycleFormatVersion) return false;
			bool pending = book.MigrationPending;
			int migrated = book.MigratedFromLifecycleVersion;
			book.MigrationPending = false; book.MigratedFromLifecycleVersion = 0;
			bool result = PristineGrowthBook(book);
			book.MigrationPending = pending; book.MigratedFromLifecycleVersion = migrated;
			return result;
		}

		private static bool CanonicalQuarantinedGrowth(KingdomGrowthBook book)
		{
			if (book == null || !book.Quarantined || string.IsNullOrEmpty(book.Fault)
				|| TooLong(book.Fault, MaxTextChars) || book.OpaquePayload != null) return false;
			bool quarantined = book.Quarantined; string fault = book.Fault;
			book.Quarantined = false; book.Fault = null;
			bool result = PristineGrowthBook(book);
			book.Quarantined = quarantined; book.Fault = fault;
			return result;
		}

		private static bool GrowthAttachmentValid(KingdomLifecycleBook book)
		{
			if (book == null || book.Growth == null) return false;
			if (book.Growth.OpaquePayload != null || book.Growth.Quarantined)
				return GrowthEnvelopeWritable(book.Growth);
			if (book.Growth.MigrationPending) return StagedGrowthShape(book.Growth);
			if (!book.IdentityBound) return PristineGrowthBook(book.Growth);
			return CanOwnGrowthAuthority(book.Growth, book.SettlementId);
		}

		private static bool GrowthRootShape(KingdomGrowthBook book, bool ValidateOperations)
		{
			if (book == null || book.FormatVersion != CurrentGrowthFormatVersion || book.Quarantined
				|| book.OpaquePayload != null || book.OpaqueWireVersion != 0 || book.MigrationPending
				|| TooLong(book.Fault, MaxTextChars) || book.Fault != null
				|| !GrowthCollectionsBounded(book) || !KnownOption(book.OptionState)
				|| !KnownOption(book.ScarcityOptionState)
				|| !KnownGrowthHealth(book.HealthState) || book.OptionTick < 0L || book.HealthTick < 0L
				|| book.ScarcityOptionTick < 0L
				|| book.WorkPauseStartedTick < 0L || book.WorkPausedTicks < 0L
				|| book.EffectiveWorkTick < 0L || book.LastHeartbeatTick < 0L
				|| book.NextArrivalTick < 0L || book.ArrivalIntervalTicks < 0L
				|| book.LastFetchTick < 0L || book.LastMillTick < 0L || book.LastSubsidenceTick < 0L
				|| book.LastDeliveryTick < 0L || book.LastDepartureTick < 0L
				|| !ValidCount(book.PendingCrop)
				|| TooLong(book.PendingCropBlueprint, MaxNameChars)
				|| TooLong(book.PendingCropZoneId, MaxNameChars)
				|| !CounterShape(book.HeartbeatNextSequence, book.HeartbeatRetiredThrough)
				|| !CounterShape(book.ArrivalNextSequence, book.ArrivalRetiredThrough)
				|| !CounterShape(book.DepartureNextSequence, book.DepartureRetiredThrough)
				|| !CounterShape(book.DeliveryNextSequence, book.DeliveryRetiredThrough)
				|| !CounterShape(book.FetchNextSequence, book.FetchRetiredThrough)
				|| !CounterShape(book.MillNextSequence, book.MillRetiredThrough)
				|| !CounterShape(book.ArrivalCandidateNextSequence,
					book.ArrivalCandidateRetiredThrough)) return false;
			if (!book.IdentityBound || !ValidRootId(book.SettlementId)
				|| !string.Equals(book.IdentityProof, GrowthIdentityProof(book.SettlementId),
					StringComparison.Ordinal)) return false;
			if (book.WorkPaused)
			{
				if (book.WorkPauseStartedTick > Math.Max(book.OptionTick, book.HealthTick)
					|| !PausedArrivalClockAllowed(book)) return false;
			}
			else if ((book.OptionState == KingdomLifecycleOptionState.Disabled
				|| book.HealthState == KingdomGrowthHealthState.Unhealthy)) return false;
			if (!GrowthEffectiveWorkBounded(book)) return false;
			if (book.ArrivalIntervalTicks == 0L && book.NextArrivalTick != 0L) return false;
			if (book.PendingCrop == 0 ? (book.PendingCropBlueprint != null
				|| book.PendingCropZoneId != null)
				: (!ValidName(book.PendingCropBlueprint) || !ValidName(book.PendingCropZoneId)))
				return false;
			if (!GrowthFieldRowsValid(book) || !GrowthCropRowsValid(book)
				|| !GrowthResourceRowsValid(book) || !GrowthProofRowsValid(book)
				|| !GrowthArrivalCandidateShape(book, book.ArrivalCandidate, false)
				|| !GrowthActiveResourcesValid(book)
				|| !GrowthActiveIdentityClaimsValid(book, null)) return false;
			return !ValidateOperations || GrowthOperationsValid(book);
		}

		private static bool PausedArrivalClockAllowed(KingdomGrowthBook book)
		{
			if (book == null || !book.WorkPaused) return false;
			KingdomGrowthOperation operation = book.ArrivalOp;
			if (operation == null) return book.ArrivalCandidate == null
				? book.NextArrivalTick == 0L : book.NextArrivalTick > 0L;
			if (operation.Action != KingdomGrowthAction.Arrival || operation.ClockLease == null)
				return false;
			bool proved = operation.ClockState == KingdomLifecyclePhysicalState.Proved
				&& operation.ClockLease.State == KingdomLifecycleLeaseState.Proved;
			bool before = operation.ClockState == KingdomLifecyclePhysicalState.Prepared
				&& operation.ClockLease.State == KingdomLifecycleLeaseState.Prepared
				|| operation.ClockState == KingdomLifecyclePhysicalState.Intent
					&& operation.ClockLease.State == KingdomLifecycleLeaseState.Intent;
			return (proved && book.NextArrivalTick == operation.ClockLease.After)
				|| (before && book.NextArrivalTick == operation.ClockLease.Before);
		}

	}
}
