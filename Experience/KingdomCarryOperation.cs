using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomCarryOperation
	{
		public long Sequence;
		public string Id;
		public string PlanHash;
		public KingdomLifecyclePhase Phase;
		public long CreatedTick;
		public long UpdatedTick;
		public string OriginSettlementId;
		public string OriginZoneId;
		public int OriginX;
		public int OriginY;
		public string DestinationSettlementId;
		public string DestinationSettlementName;
		public List<string> SettlementIds = new List<string>();
		public string RealmTopologyHash;
		public string DestinationZoneId;
		public KingdomLifecycleTopology DestinationTopology;
		public string DestinationOwnerId;
		public int DestinationX = -1;
		public int DestinationY = -1;
		public long DueTick;
		public bool RiskFrozen;
		public bool LostOnRoad;
		public int SourceIndex;
		public int OutputIndex;
		public KingdomLifecycleResourceLease ScheduleLease;
		public string ScheduleReceiptId;
		public string ScheduleTopologyId;
		public int ScheduleBeforeMatches = -1;
		public int ScheduleAfterMatches = -1;
		public bool ScheduleSameReference;
		public string ScheduleProofId;
		public KingdomLifecyclePhysicalState ScheduleReceiptState;
		public List<KingdomCarrySource> Sources = new List<KingdomCarrySource>();
		public List<KingdomLifecycleProjection> Outputs = new List<KingdomLifecycleProjection>();
		public int Mud;
		public int Brush;
		public int Timber;
		public int Stone;
		public int Marble;
		public int Scrap;
		public int EscrowMud;
		public int EscrowBrush;
		public int EscrowTimber;
		public int EscrowStone;
		public int EscrowMarble;
		public int EscrowScrap;
		public int DeliveredMud;
		public int DeliveredBrush;
		public int DeliveredTimber;
		public int DeliveredStone;
		public int DeliveredMarble;
		public int DeliveredScrap;
		public int LostMud;
		public int LostBrush;
		public int LostTimber;
		public int LostStone;
		public int LostMarble;
		public int LostScrap;
		public KingdomLifecycleOutbox Outbox;
		public string Fault;

		// Carry-v6 exact-manifest authority. ManifestDigest freezes only immutable source facts;
		// ManifestRevision advances after proved sign/pickup/deposit callbacks. Job and trip ids
		// bind this lifecycle to the one central logistics registry rather than a second planner.
		public KingdomCarryAuthorityKind AuthorityKind;
		public int ManifestVersion;
		public string ManifestDigest;
		public long ManifestRevision;
		public List<int> JobIds = new List<int>();
		public List<int> TripIds = new List<int>();

		public string SignObjectId;
		public string SignBlueprint;
		public KingdomLifecycleTopology SignTopology;
		public string SignOwnerId;
		public string SignZoneId;
		public int SignX = -1;
		public int SignY = -1;
		public int SignCount;
		public string SignReceiptId;
		public int SignReceiptBeforeMatches = -1;
		public int SignReceiptAfterMatches = -1;
		public int SignReceiptBeforeCount = -1;
		public int SignReceiptAfterCount = -1;
		public bool SignReceiptSameReference;
		public string SignReceiptProofId;
		public KingdomLifecyclePhysicalState SignReceiptState;

		public bool DestinationSafetyWaiting;
		public long DestinationSafetyWaitTick;
		public string SpillZoneId;
		public int SpillX = -1;
		public int SpillY = -1;

		[NonSerialized]
		public object LiveAuthority;
	}
}
