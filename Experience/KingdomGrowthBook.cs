using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{

	/// <summary>Nested, settlement-bound Growth authority. Future payload bytes are retained
	/// exactly and re-emitted, including malformed known-version payloads whose outer length is
	/// intact. Opaque evidence grants no Growth authority and does not poison enclosing lifecycle.</summary>
	[Serializable]
	public sealed class KingdomGrowthBook
	{
		public int FormatVersion = KingdomLifecycleRules.CurrentGrowthFormatVersion;
		public bool Quarantined;
		public string Fault;
		public int OpaqueWireVersion;
		public byte[] OpaquePayload;
		public string SettlementId;
		public bool IdentityBound;
		public string IdentityProof;
		public int MigratedFromLifecycleVersion;
		public bool MigrationPending;
		public long MigrationTick;
		public KingdomLifecycleOptionState OptionState;
		public long OptionTick;
		public KingdomGrowthHealthState HealthState;
		public long HealthTick;
		public KingdomLifecycleOptionState ScarcityOptionState;
		public long ScarcityOptionTick;
		public bool WorkPaused;
		public long WorkPauseStartedTick;
		public long WorkPausedTicks;
		public long EffectiveWorkTick;
		public long LastHeartbeatTick;
		public long NextArrivalTick;
		public long ArrivalIntervalTicks;
		public string ArrivalEventStreamId = KingdomLifecycleRules.GrowthArrivalEventStreamId;
		public int ArrivalRulesVersion;
		public long ArrivalRateEpoch;
		public long ArrivalRateEpochStartedTick;
		public long ArrivalProcessedThroughTick;
		public long ArrivalCadenceNextDueTick;
		public int ArrivalRateCohort;
		public ulong ArrivalOrdinalHighWater;
		public ulong ArrivalOrdinalRetiredThrough;
		public bool ArrivalCadenceMigrationPending = true;
		public bool ArrivalCadenceResumePending;
		public KingdomGrowthArrivalOpportunity ArrivalOpportunity;
		public List<KingdomGrowthArrivalDebtRange> ArrivalDebtRanges =
			new List<KingdomGrowthArrivalDebtRange>();
		public long LastFetchTick;
		public long LastMillTick;
		public long LastSubsidenceTick;
		public long LastDeliveryTick;
		public long LastDepartureTick;
		public int PendingCrop;
		public string PendingCropBlueprint;
		public string PendingCropZoneId;
		public long HeartbeatNextSequence = 1L;
		public long HeartbeatRetiredThrough;
		public long ArrivalNextSequence = 1L;
		public long ArrivalRetiredThrough;
		public long DepartureNextSequence = 1L;
		public long DepartureRetiredThrough;
		public long DeliveryNextSequence = 1L;
		public long DeliveryRetiredThrough;
		public long FetchNextSequence = 1L;
		public long FetchRetiredThrough;
		public long MillNextSequence = 1L;
		public long MillRetiredThrough;
		public long ArrivalCandidateNextSequence = 1L;
		public long ArrivalCandidateRetiredThrough;
		public KingdomGrowthOperation HeartbeatOp;
		public KingdomGrowthOperation ArrivalOp;
		public KingdomGrowthOperation DepartureOp;
		public KingdomGrowthOperation DeliveryOp;
		public KingdomGrowthOperation FetchOp;
		public KingdomGrowthOperation MillOp;
		public KingdomGrowthArrivalCandidate ArrivalCandidate;
		public KingdomGrowthFirstGuestTerminalReceipt FirstGuestTerminal;
		public List<KingdomGrowthFieldSlot> FieldOps = new List<KingdomGrowthFieldSlot>();
		public List<KingdomGrowthCropRow> CropRows = new List<KingdomGrowthCropRow>();
		public List<KingdomLifecycleResourceRevision> Resources =
			new List<KingdomLifecycleResourceRevision>();
		public List<KingdomGrowthProof> RecentProofs = new List<KingdomGrowthProof>();
	}
}
