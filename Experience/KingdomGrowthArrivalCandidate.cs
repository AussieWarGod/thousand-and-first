using System;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomGrowthArrivalCandidate
	{
		public long Sequence;
		public string Id;
		public string PlanHash;
		public string SettlementId;
		public long CreatedTick;
		public long UpdatedTick;
		public KingdomGrowthArrivalCandidatePhase Phase;
		public KingdomGrowthArrivalCandidatePhase EvidencePhase;
		/// <summary>Historical Growth-v1 candidates published before lodging did not bind an
		/// origin zone. This compatibility state grants no starter authority; the first claimed
		/// zone must bind it transactionally before reconciliation can continue.</summary>
		public bool LegacyGrowthV1UnboundZone;
		/// <summary>Growth-v1/v2 or compatibility-prepared candidate whose person payload has
		/// not yet been adopted into the versioned semantic plan.</summary>
		public bool LegacySemanticPlan;
		public int SemanticPlanVersion;
		public string SemanticStreamId;
		public uint SemanticEventKind;
		public string PlannedOrigin;
		public string PlannedCreed;
		public string PlannedName;
		public string PlannedArrived;
		public int ArrivalX = -1;
		public int ArrivalY = -1;
		public KingdomGrowthArrivalDisposition Disposition;
		public KingdomGrowthArrivalRefusalReason RefusalReason;
		public string ObjectId;
		public string Marker;
		public string Blueprint;
		public string EscrowKey;
		public KingdomLifecycleResourceLease CandidateLease;
		public KingdomLifecycleResourceLease LodgingLease;
		public KingdomLifecycleResourceLease EscrowLease;
		public KingdomGrowthObjectCallbackStep CreateStep;
		public KingdomGrowthObjectCallbackStep DispositionStep;
		public string LodgingZoneId;
		public int LodgingX = -1;
		public int LodgingY = -1;
		public string LodgingBeforeGraphHash;
		public string LodgingDeclaredGraphHash;
		public string LodgingReceiptGraphHash;
		public string LodgingCallbackReferenceHash;
		public bool LodgingSameReference;
		public string LodgingReceiptId;
		public KingdomLifecyclePhysicalState LodgingState;
		public string ConsumingOperationId;
		public long ConsumingOperationSequence;
		public string Fault;
	}
}
