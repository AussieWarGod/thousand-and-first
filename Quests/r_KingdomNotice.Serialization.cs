using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace XRL.World.Parts
{
	public partial class r_KingdomNotice : IPart
	{

		/// <summary>
		/// Qud's default IPart serializer is positional reflection (IComponent.Write/Read). Adding
		/// schedule fields would make an older notice under-consume and get skipped. This part now
		/// writes a named payload, while the reader consumes the exact eighteen-field legacy layout
		/// when the first boxed integer is not our marker.
		/// </summary>
		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteObject(SerializationMagic);
			Writer.WriteObject(CurrentSerializationVersion);
			Writer.WriteNamedFields(this, typeof(r_KingdomNotice));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			object first = Reader.ReadObject();
			if (first is int && (int)first == SerializationMagic)
			{
				object savedVersion = Reader.ReadObject();
				if (!(savedVersion is int) || (int)savedVersion != CurrentSerializationVersion)
				{
					throw new InvalidOperationException("Unsupported ThousandAndFirst bounty notice save version.");
				}
				Reader.ReadNamedFields(this, typeof(r_KingdomNotice));
				NormalizeSerializedFields(Basis);
				return;
			}
			ReadLegacy(first, Reader);
			NormalizeSerializedFields(Basis);
		}

		private void ReadLegacy(object First, SerializationReader Reader)
		{
			TaskCode = Convert.ToInt32(First);
			Price = Convert.ToInt32(Reader.ReadObject());
			Paid = Convert.ToInt32(Reader.ReadObject());
			PostedTick = Convert.ToInt64(Reader.ReadObject());
			Passes = Convert.ToInt32(Reader.ReadObject());
			WorkerName = Reader.ReadObject() as string;
			TakenTick = Convert.ToInt64(Reader.ReadObject());
			DueTick = Convert.ToInt64(Reader.ReadObject());
			Magnitude = Convert.ToInt32(Reader.ReadObject());
			Done = Convert.ToBoolean(Reader.ReadObject());
			X1 = Convert.ToInt32(Reader.ReadObject());
			Y1 = Convert.ToInt32(Reader.ReadObject());
			X2 = Convert.ToInt32(Reader.ReadObject());
			Y2 = Convert.ToInt32(Reader.ReadObject());
			PileId = Reader.ReadObject() as string;
			AnnouncedBlock = Convert.ToInt32(Reader.ReadObject());
			StakeFailedAnnounced = Convert.ToBoolean(Reader.ReadObject());
			RefusalTold = Convert.ToBoolean(Reader.ReadObject());
		}

		private void NormalizeSerializedFields(GameObject Basis)
		{
			bool malformed = false;
			if (!SavedTextWithin(WorkerName, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(PileId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(EventStreamId, 128)
				|| !SavedTextWithin(LifecycleId, 180)
				|| !SavedTextWithin(QuarantineReason, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(PendingWorkerName, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(ManningWorkId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(ManningWorkName, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(ManningOptionRecord,
					ThousandAndFirst.KingdomElapsedOptionRules.MaxEncodedChars)
				|| !SavedTextWithin(TransferItemId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(TransferSourceId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(TransferDestinationId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(ScoutZoneId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(ScoutGround, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(PaymentZoneId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(PaymentVesselIds, ThousandAndFirst.KingdomBountyRules.MaxPaymentRowsChars)
				|| !SavedTextWithin(PaymentOriginalVolumes, ThousandAndFirst.KingdomBountyRules.MaxPaymentRowsChars)
				|| !SavedTextWithin(PaymentMaxVolumes, ThousandAndFirst.KingdomBountyRules.MaxPaymentRowsChars)
				|| !SavedTextWithin(PaymentAllocations, ThousandAndFirst.KingdomBountyRules.MaxPaymentRowsChars)
				|| !SavedTextWithin(CompletionExtra, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(PostChronicleLine, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(PostMessageLine, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(PostZoneId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(WithdrawChronicleLine, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(WithdrawMessageLine, ThousandAndFirst.KingdomBountyRules.MaxSavedTextChars)
				|| !SavedTextWithin(WithdrawPileId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars)
				|| !SavedTextWithin(WithdrawZoneId, ThousandAndFirst.KingdomBountyRules.MaxObjectIdChars))
			{
				malformed = true;
			}
			if (TaskCode < 0 || TaskCode >= ThousandAndFirst.KingdomBountyRules.TaskCount)
			{
				TaskCode = (int)ThousandAndFirst.BountyTask.Clearance;
				malformed = true;
			}
			if (Price < ThousandAndFirst.KingdomBountyRules.MinPrice
				|| Price > ThousandAndFirst.KingdomBountyRules.MaxPrice)
			{
				Price = ThousandAndFirst.KingdomBountyRules.ClampPrice(Price);
				malformed = true;
			}
			if (Paid < 0 || Paid > Price)
			{
				Paid = (Paid < 0) ? 0 : Price;
				malformed = true;
			}
			if (Passes < 0 || Passes > ThousandAndFirst.KingdomBountyRules.MaxPasses)
			{
				Passes = (Passes < 0) ? 0 : ThousandAndFirst.KingdomBountyRules.MaxPasses;
				malformed = true;
			}
			if (PostedTick < 0L || TakenTick < 0L || DueTick < 0L || NextAttemptTick < 0L
				|| ManningServedTicks < 0L || ManningCheckpointTick < 0L)
			{
				PostedTick = (PostedTick < 0L) ? 0L : PostedTick;
				TakenTick = (TakenTick < 0L) ? 0L : TakenTick;
				DueTick = (DueTick < 0L) ? 0L : DueTick;
				NextAttemptTick = (NextAttemptTick < 0L) ? 0L : NextAttemptTick;
				ManningServedTicks = (ManningServedTicks < 0L) ? 0L : ManningServedTicks;
				ManningCheckpointTick = (ManningCheckpointTick < 0L) ? 0L : ManningCheckpointTick;
				malformed = true;
			}
			if (Magnitude < 0 || TransferredUnits < 0 || PaymentAmount < 0
				|| PaymentPaidBefore < 0 || PaymentProved < 0 || TransferUnits < 0)
			{
				Magnitude = (Magnitude < 0) ? 0 : Magnitude;
				TransferredUnits = (TransferredUnits < 0) ? 0 : TransferredUnits;
				PaymentAmount = (PaymentAmount < 0) ? 0 : PaymentAmount;
				PaymentPaidBefore = (PaymentPaidBefore < 0) ? 0 : PaymentPaidBefore;
				PaymentProved = (PaymentProved < 0) ? 0 : PaymentProved;
				TransferUnits = (TransferUnits < 0) ? 0 : TransferUnits;
				malformed = true;
			}
			if (Done && string.IsNullOrEmpty(WorkerName)) malformed = true;
			if (ManningServedTicks > ThousandAndFirst.KingdomBountyManningRules.RequiredTicks)
			{
				ManningServedTicks = ThousandAndFirst.KingdomBountyManningRules.RequiredTicks;
				malformed = true;
			}
			if (TaskCode == (int)ThousandAndFirst.BountyTask.Manning
				&& (ManningVersion != 1 || string.IsNullOrEmpty(ManningWorkId)
					|| string.IsNullOrWhiteSpace(ManningWorkName)
					|| !ThousandAndFirst.KingdomElapsedOptionRules.TryDecode(
						ManningOptionRecord, out ThousandAndFirst.KingdomElapsedOptionRecord _)
					|| ManningResidentEpoch < 0 || ManningWorkEpoch < 0
					|| (!string.IsNullOrEmpty(WorkerName) && WorkerResidentId <= 0))) malformed = true;
			if (TaskCode == (int)ThousandAndFirst.BountyTask.Manning
				&& string.IsNullOrEmpty(WorkerName)
				&& (WorkerResidentId != 0 || ManningServedTicks != 0L
					|| ManningCheckpointTick != 0L || ManningAssigned
					|| ManningResidentEpoch != 0 || ManningWorkEpoch != 0)) malformed = true;
			if (TaskCode == (int)ThousandAndFirst.BountyTask.Manning && ManningAssigned
				&& (string.IsNullOrEmpty(WorkerName) || WorkerResidentId <= 0
					|| ManningCheckpointTick <= 0L
					|| ManningServedTicks >= ThousandAndFirst.KingdomBountyManningRules.RequiredTicks))
				malformed = true;
			if (TaskCode == (int)ThousandAndFirst.BountyTask.Manning && Done
				&& ManningServedTicks != ThousandAndFirst.KingdomBountyManningRules.RequiredTicks)
				malformed = true;
			if (TaskCode != (int)ThousandAndFirst.BountyTask.Manning && (ManningVersion != 0
				|| !string.IsNullOrEmpty(ManningWorkId) || !string.IsNullOrEmpty(ManningWorkName)
				|| WorkerResidentId != 0 || ManningServedTicks != 0L
				|| ManningCheckpointTick != 0L || ManningAssigned
				|| !string.IsNullOrEmpty(ManningOptionRecord) || ManningResidentEpoch != 0
				|| ManningWorkEpoch != 0)) malformed = true;
			if (ScheduleVersion != 0 && ScheduleVersion != 2) malformed = true;
			if (ScheduleVersion == 2
				&& (!ThousandAndFirst.KingdomBountyRules.IsNoticeEventStream(EventStreamId)
					|| (AttemptScheduleExhausted ? NextAttemptTick != 0L : NextAttemptTick <= 0L)))
			{
				malformed = true;
			}
			if (TakePhase < 0 || TakePhase > (int)ThousandAndFirst.BountyTakePhase.Quarantined
				|| TransferPhase < 0 || TransferPhase > (int)ThousandAndFirst.BountyTransferPhase.Quarantined
				|| PaymentPhase < 0 || PaymentPhase > (int)ThousandAndFirst.BountyPaymentPhase.Quarantined
				|| TerminalPhase < 0 || TerminalPhase > (int)ThousandAndFirst.BountyTerminalPhase.CleanupLost
				|| ScoutPhase < 0 || ScoutPhase > 5 || HaulPhase < 0 || HaulPhase > 4
				|| CompletionPhase < 0 || CompletionPhase > 4
				|| PostPhase < 0 || PostPhase > (int)ThousandAndFirst.BountyPostPhase.Complete
				|| WithdrawPhase < 0 || WithdrawPhase > (int)ThousandAndFirst.BountyWithdrawPhase.CleanupLost
				|| !ValidSink(QuarantineLedgerState) || !ValidSink(QuarantineMessageState)
				|| !ValidSink(PostMessageState) || !ValidSink(StakeCleanupState)
				|| !ValidSink(WithdrawMessageState) || !ValidSink(TakeLedgerState)
				|| !ValidSink(TakeMessageState) || !ValidSink(ScoutDeedState)
				|| !ValidSink(CompletionLedgerState) || !ValidSink(TerminalLedgerState)
				|| !ValidSink(TerminalMessageState))
			{
				malformed = true;
			}
			if (TakePhase != (int)ThousandAndFirst.BountyTakePhase.None
				&& TakePhase != (int)ThousandAndFirst.BountyTakePhase.Complete
				&& (string.IsNullOrEmpty(PendingWorkerName) || PendingAttemptTick < 0L))
			{
				malformed = true;
			}
			if (TransferPhase != (int)ThousandAndFirst.BountyTransferPhase.None
				&& TransferPhase != (int)ThousandAndFirst.BountyTransferPhase.Quarantined
				&& (string.IsNullOrEmpty(TransferItemId) || string.IsNullOrEmpty(TransferSourceId)
					|| string.IsNullOrEmpty(TransferDestinationId) || TransferUnits <= 0))
			{
				malformed = true;
			}
			if (PaymentPhase != (int)ThousandAndFirst.BountyPaymentPhase.None
				&& PaymentPhase != (int)ThousandAndFirst.BountyPaymentPhase.Credited
				&& PaymentPhase != (int)ThousandAndFirst.BountyPaymentPhase.Quarantined
				&& (PaymentAmount <= 0 || PaymentPaidBefore > Price
					|| string.IsNullOrEmpty(PaymentZoneId)
					|| string.IsNullOrEmpty(PaymentVesselIds)
					|| string.IsNullOrEmpty(PaymentOriginalVolumes)
					|| string.IsNullOrEmpty(PaymentMaxVolumes)
					|| string.IsNullOrEmpty(PaymentAllocations)))
			{
				malformed = true;
			}
			if (ScoutPhase > 0 && string.IsNullOrEmpty(ScoutZoneId)) malformed = true;
			if (PostPhase > (int)ThousandAndFirst.BountyPostPhase.None
				&& (string.IsNullOrEmpty(PostChronicleLine) || string.IsNullOrEmpty(PostMessageLine)
					|| string.IsNullOrEmpty(PostZoneId) || PostCellX < 0 || PostCellY < 0
					|| (!string.IsNullOrEmpty(PileId) && (PostPileCellX < 0 || PostPileCellY < 0))))
			{
				malformed = true;
			}
			if (WithdrawPhase > (int)ThousandAndFirst.BountyWithdrawPhase.None
				&& (string.IsNullOrEmpty(WithdrawChronicleLine)
					|| string.IsNullOrEmpty(WithdrawMessageLine)
					|| string.IsNullOrEmpty(WithdrawZoneId)
					|| WithdrawCellX < 0 || WithdrawCellY < 0
					|| (!string.IsNullOrEmpty(WithdrawPileId)
						&& (WithdrawPileCellX < 0 || WithdrawPileCellY < 0))))
			{
				malformed = true;
			}
			if (string.IsNullOrEmpty(LifecycleId))
			{
				LifecycleId = ThousandAndFirst.KingdomBountyRules.NoticeEventId(
					Basis?.IDIfAssigned);
			}
			else if (!ThousandAndFirst.KingdomBountyRules.IsNoticeEventId(LifecycleId))
			{
				malformed = true;
			}
			if (malformed)
			{
				LifecycleQuarantined = true;
				QuarantineReason = "The notice's saved lifecycle is malformed; no work or payment was guessed through it.";
				AttemptScheduleExhausted = true;
				NextAttemptTick = 0L;
			}
		}

		private static bool SavedTextWithin(string Text, int Maximum)
		{
			return Text == null || (Maximum >= 0 && Text.Length <= Maximum);
		}

		private static bool ValidSink(int Raw)
		{
			return Raw >= (int)ThousandAndFirst.BountySinkDisposition.None
				&& Raw <= (int)ThousandAndFirst.BountySinkDisposition.Lost;
		}
	}
}
