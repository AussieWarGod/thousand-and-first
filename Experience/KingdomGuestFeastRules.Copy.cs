using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestFeastRules
	{
		internal static KingdomGuestFeastReceipt Copy(KingdomGuestFeastReceipt row)
		{
			return new KingdomGuestFeastReceipt
			{
				Version = row.Version, Phase = row.Phase, SettlementId = row.SettlementId,
				OpportunityId = row.OpportunityId, CauseId = row.CauseId,
				GuestDecisionReceiptId = row.GuestDecisionReceiptId,
				GrowthTerminalReceiptId = row.GrowthTerminalReceiptId,
				GuestCandidateId = row.GuestCandidateId,
				GuestObjectId = row.GuestObjectId,
				GuestArrivalOperationId = row.GuestArrivalOperationId,
				GuestArrivalOutboxEventId = row.GuestArrivalOutboxEventId,
				GuestName = row.GuestName, GuestOrigin = row.GuestOrigin,
				GuestCreed = row.GuestCreed, DeedId = row.DeedId,
				PracticeId = row.PracticeId, PointerSourceId = row.PointerSourceId,
				PointerTargetId = row.PointerTargetId, CauseTick = row.CauseTick,
				GuestDecisionTick = row.GuestDecisionTick,
				GuestTerminalTick = row.GuestTerminalTick,
				PracticeDecisionTick = row.PracticeDecisionTick,
				PointerTick = row.PointerTick, HomeCycles = row.HomeCycles,
				GuestResidentId = row.GuestResidentId, GuestResult = row.GuestResult,
				PracticeOutcome = row.PracticeOutcome,
				LocusProjectionId = row.LocusProjectionId,
				LocusRealmId = row.LocusRealmId,
				LocusSettlementId = row.LocusSettlementId,
				LocusWorkId = row.LocusWorkId, LocusObjectId = row.LocusObjectId,
				LocusZoneId = row.LocusZoneId, LocusBlueprint = row.LocusBlueprint,
				LocusObservedTick = row.LocusObservedTick, AwayArmed = row.AwayArmed,
				PointerKind = row.PointerKind
			};
		}

		internal static KingdomGuestFeastBook Clone(KingdomGuestFeastBook book)
		{
			KingdomGuestFeastBook copy = new KingdomGuestFeastBook
			{
				SchemaState = book.SchemaState, SchemaFault = book.SchemaFault,
				RealmId = book.RealmId, IdentityBound = book.IdentityBound,
				Revision = book.Revision, OpaqueWireVersion = book.OpaqueWireVersion,
				OpaqueFuturePayload = book.OpaqueFuturePayload == null ? null
					: (byte[])book.OpaqueFuturePayload.Clone(),
				OpaqueEnvelope = book.OpaqueEnvelope == null ? null
					: (byte[])book.OpaqueEnvelope.Clone()
			};
			for (int i = 0; i < book.Rows.Count; i++) copy.Rows.Add(Copy(book.Rows[i]));
			return copy;
		}

		internal static void Replace(KingdomGuestFeastBook target, KingdomGuestFeastBook source)
		{
			target.SchemaState = source.SchemaState; target.SchemaFault = source.SchemaFault;
			target.RealmId = source.RealmId; target.IdentityBound = source.IdentityBound;
			target.Revision = source.Revision; target.Rows.Clear();
			for (int i = 0; i < source.Rows.Count; i++) target.Rows.Add(Copy(source.Rows[i]));
			target.OpaqueWireVersion = source.OpaqueWireVersion;
			target.OpaqueFuturePayload = source.OpaqueFuturePayload;
			target.OpaqueEnvelope = source.OpaqueEnvelope;
		}
	}
}
