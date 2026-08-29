using System;

namespace ThousandAndFirst
{
	/// <summary>Growth-owned, bounded correspondence authority for one not-yet-created arrival.
	/// Experience may present these facts and lease capacity, but never owns this row.</summary>
	[Serializable]
	public sealed class KingdomGrowthFirstGuestOpportunity
	{
		public int RulesVersion = 1;
		public string OpportunityId;
		public string CauseId;
		public long CauseTick;
		public long OfferedTick;
		public long CadenceTicks;
		public KingdomGrowthFirstGuestFactsState FactsState;
		public int CohortSize = 1;
		public int PopulationBefore = -1;
		public int PopulationCap = -1;
		public int SupportedLevel = -1;
		public int SupportCap = -1;
		public int WaterAvailable = -1;
		public int WaterRequired = -1;

		public KingdomGrowthFirstGuestChoiceState ChoiceState;
		public long DeferredTick = -1;
		public string DeferredReceiptId;
		public long DecisionTick = -1;
		public string DecisionReceiptId;

		/// <summary>Exact W0 request proof only. The Experience ledger remains the sole capacity
		/// counter and source-owned lease authority.</summary>
		public string BodyReservationId;
		public string BodyRealmId;
		public KingdomExperienceOptionKind BodyOptionKind;
		public long BodyEnableEpoch;
		public long BodyReservedTick = -1;
		public KingdomGrowthFirstGuestBodyLeaseState BodyLeaseState;

		/// <summary>Append-only rules-v2 physical guest evidence. Rules-v1 rows keep defaults.</summary>
		public KingdomGrowthFirstGuestGuestPhase GuestPhase;
		public KingdomGrowthFirstGuestTerminalState GuestTerminalState;
		public long GuestActionTick = -1;
		public string GuestActionReceiptId;
		public long GuestTerminalTick = -1;
		public string GuestTerminalReceiptId;
	}

	/// <summary>Growth's immutable terminal proof after candidate consumption. Optional
	/// experience rows may reference it, but never keep the candidate alive.</summary>
	[Serializable]
	public sealed class KingdomGrowthFirstGuestTerminalReceipt
	{
		public const int LegacyVersion = 1;
		public const int CurrentVersion = 2;
		public int Version = CurrentVersion;
		public string ReceiptId;
		public string SettlementId;
		public string CandidateId;
		public string CandidateObjectId;
		public string Blueprint;
		public string PersonName;
		public string PersonOrigin;
		public string PersonCreed;
		public int ResidentId;
		public KingdomGrowthArrivalDisposition Result;
		public string ArrivalOperationId;
		public string ArrivalOutboxEventId;
		public long TerminalTick;
		public KingdomGrowthFirstGuestOpportunity Opportunity;
	}
}
