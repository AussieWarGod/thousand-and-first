using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomTradeOperation
	{
		public long Sequence;
		public string Id;
		public KingdomTradeOperationKind Kind;
		public KingdomTradePhase Phase;
		public long CreatedTick;
		public long UpdatedTick;
		public string ZoneId;
		public string SettlementId;
		public string SettlementName;
		public string CharterId;
		public string ManifestId;
		public string DealKey;
		public string DealDisplayName;
		public string Faction;
		public int Cycles;
		public int IncomePerCycle;
		public long IntervalTicks;
		public long DueBefore;
		public long DueAfter;
		public string CaravanBlueprint;
		public string ProjectionId;
		public string ProjectionObjectId;
		public int ProjectionX;
		public int ProjectionY;
		public string PriorProjectionId;
		public string PriorProjectionObjectId;
		public string PriorProjectionZoneId;
		public KingdomTradePhysicalState ProjectionState;
		public KingdomTradePhysicalState PriorCleanupState;
		public KingdomTradeWaterDirection WaterDirection;
		public int RequestedWater;
		public int ProvedWater;
		public int AmbiguousWater;
		public List<KingdomTradeWaterLeg> WaterLegs = new List<KingdomTradeWaterLeg>();
		public string MaterialClaim;
		public int MaterialRequested;
		public int MaterialProved;
		public List<KingdomTradeMaterialOutput> MaterialOutputs = new List<KingdomTradeMaterialOutput>();
		public string OriginId;
		public string OriginName;
		public string DestinationId;
		public string DestinationName;
		public long ManifestLoadedTick;
		public long ManifestDeadlineTick;
		public int ManifestEscrowBefore;
		public int ManifestEscrowDebit;
		public int ManifestEscrowAfter;
		public KingdomTradePhysicalState ManifestEscrowState;
		public long RetainedBefore;
		public long RetainedDelta;
		public long RetainedAfter;
		public KingdomTradePhysicalState RetainedState;
		public KingdomTradeStandingCas Standing;
		public KingdomTradeOutbox Outbox;
		public KingdomTradePatternReceipt Pattern;
		public string Fault;

	}

	[Serializable]
	public sealed class KingdomTradeProof
	{
		public string RealmId;
		public long Sequence;
		public string Id;
		public string OperationEvidenceHash;
		public KingdomTradeOperationKind Kind;
		public KingdomTradePhase Disposition;
		public int ProvedWater;
		public int AmbiguousWater;
		public int RequestedWater;
		public string SettlementId;
		public string ManifestId;
		public int ManifestEscrowBefore;
		public int ManifestEscrowDebit;
		public int ManifestEscrowAfter;
		public KingdomTradePhysicalState ManifestEscrowState;
		public long RetainedBefore;
		public long RetainedDelta;
		public long RetainedAfter;
		public KingdomTradePhysicalState RetainedState;
		public int MaterialRequested;
		public int MaterialProved;
		public KingdomTradeSinkState ChronicleState;
		public KingdomTradeSinkState LedgerState;
		public KingdomTradeSinkState MessageState;
		public KingdomTradeSinkState DeedState;
		/// <summary>Receipt owns removal of its exact terminal manifest row.</summary>
		public bool ManifestCleanup;
		public long Tick;
		public string Fault;
	}

	[Serializable]
	public sealed class KingdomTradeArchive
	{
		public string RealmId;
		public List<string> SettlementIds = new List<string>();
		public long RetainedEscrowDrams;
		public int ManifestEscrowDrams;
		public string ManifestId;
		public KingdomTradeManifestStatus ManifestStatus;
		public int CharterCount;
		public int ProjectionCount;
		public int ProofCount;
		public string OpenOperationId;
		public string PendingRetirementId;
		public int OpenRequestedWater;
		public int OpenProvedWater;
		public int OpenAmbiguousWater;
		public long RetiredThrough;
		public string AuthorityEvidenceHash;
		public long ClosedTick;
		/// <summary>Domain-separated digest of every archive field above, including close tick.</summary>
		public string ReceiptEvidenceHash;
	}

	[Serializable]
	public sealed class KingdomTradeProofCompaction
	{
		public string RealmId;
		public long FirstSequence;
		public long LastSequence;
		public int ProofCount;
		public string EvidenceHash;
	}

	[Serializable]
	public sealed class KingdomTradeIncident
	{
		public string RealmId;
		public long Sequence;
		public string OperationId;
		public string EvidenceHash;
		public long Tick;
		public string Fault;
	}

	public sealed class KingdomTradeAuthoritySeal
	{
		internal byte[] BookBytes;
		internal IList<string> ClaimedZones;
		internal string[] ClaimedRows;
		internal IList<string> CityZones;
		internal string[] CityRows;
	}

	/// <summary>In-memory only witness for exact mutable object identity across a callback cut.</summary>
	public sealed class KingdomTradeReferenceSeal
	{
		internal object[] Rows;
	}
}
