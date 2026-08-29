using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public enum KingdomGuestFeastPhase : byte
	{
		None = 0,
		AwaitingGuestChoice = 1,
		AwaitingPractice = 2,
		Cycling = 3,
		GuestDeclined = 4,
		PracticeRefused = 5,
		/// <summary>Historical wire-only terminal. Current transitions never create it.</summary>
		OutOfOrder = 6,
		Exhausted = 7,
		AwaitingGuestResult = 8,
		AwaitingLocus = 9,
		GuestCouldNotJoin = 10,
		PracticeArchived = 11,
		GuestDeparted = 12
	}

	public enum KingdomGuestFeastPointerKind : byte
	{
		None = 0,
		Curator = 1,
		CivicLead = 2
	}

	public sealed class KingdomGuestFeastLocusReceipt
	{
		public string ProjectionId;
		public string RealmId;
		public string SettlementId;
		public int WorkId;
		public string ObjectId;
		public string ZoneId;
		public string Blueprint;
		public long ObservedTick;
	}

	[Serializable]
	public sealed class KingdomGuestFeastReceipt
	{
		public const int CurrentVersion = 4;
		public int Version = CurrentVersion;
		public KingdomGuestFeastPhase Phase;
		public string SettlementId;
		public string OpportunityId;
		public string CauseId;
		public string GuestDecisionReceiptId;
		public string GrowthTerminalReceiptId;
		public string GuestCandidateId;
		public string GuestObjectId;
		public string GuestArrivalOperationId;
		public string GuestArrivalOutboxEventId;
		public string GuestName;
		public string GuestOrigin;
		public string GuestCreed;
		public string DeedId;
		public string PracticeId;
		public string PointerSourceId;
		public string PointerTargetId;
		public long CauseTick;
		public long GuestDecisionTick = -1L;
		public long GuestTerminalTick = -1L;
		public long PracticeDecisionTick = -1L;
		public long PointerTick = -1L;
		public int HomeCycles;
		public int GuestResidentId;
		public KingdomGrowthArrivalDisposition GuestResult;
		public KingdomFirstFeastPhase PracticeOutcome;
		public string LocusProjectionId;
		public string LocusRealmId;
		public string LocusSettlementId;
		public int LocusWorkId;
		public string LocusObjectId;
		public string LocusZoneId;
		public string LocusBlueprint;
		public long LocusObservedTick = -1L;
		public bool AwayArmed;
		public KingdomGuestFeastPointerKind PointerKind;
	}

	/// <summary>Finite O11 coordination references. Growth, First Feast, locus and pointer owners
	/// retain their facts; this section owns only ordered closure and cycle exhaustion.</summary>
	[Serializable]
	public sealed class KingdomGuestFeastBook
	{
		public KingdomExperienceSchemaState SchemaState =
			KingdomExperienceSchemaState.Compatible;
		public string SchemaFault;
		public string RealmId;
		public bool IdentityBound;
		public long Revision;
		public List<KingdomGuestFeastReceipt> Rows =
			new List<KingdomGuestFeastReceipt>();
		public int OpaqueWireVersion;
		public byte[] OpaqueFuturePayload;
		public byte[] OpaqueEnvelope;
	}
}
