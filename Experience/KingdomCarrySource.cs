using System;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomCarrySource
	{
		public string OperationId;
		public string SourceEventId;
		public string ObjectId;
		public string Blueprint;
		public KingdomLifecycleTopology Topology;
		public string OwnerId;
		public string ZoneId;
		public int X = -1;
		public int Y = -1;
		public int Material;
		public int OriginalCount;
		public int PlannedCount;
		public int Removed;
		public int UnitCursor;
		public int UnitBefore;
		public int UnitAfter;
		public string UnitEventId;
		public KingdomLifecyclePhysicalState UnitState;
		public string ReceiptId;
		public string ReceiptTopologyId;
		public int ReceiptBeforeIdMatches = -1;
		public int ReceiptAfterIdMatches = -1;
		public int ReceiptBeforeCount = -1;
		public int ReceiptAfterCount = -1;
		public bool ReceiptSameReference;
		public string ReceiptProofId;
		public string ReceiptChainId;
		public int ReceiptChainCount;
		public KingdomLifecyclePhysicalState ReceiptState;
		public KingdomLifecyclePhysicalState State;

		// Carry-v6 exact-manifest progress. A source is one whole GameObject/stack: every count is
		// either zero or PlannedCount and the same object reference moves between these topologies.
		public int LoadedCount;
		public int DeliveredCount;
		public int LostCount;
		public int CurrentTripId;
		public KingdomLifecycleTopology CurrentTopology;
		public string CurrentOwnerId;
		public string CurrentZoneId;
		public int CurrentX = -1;
		public int CurrentY = -1;
		public KingdomCarryTransferKind PendingTransfer;
		public KingdomLifecycleTopology PendingTopology;
		public string PendingOwnerId;
		public string PendingZoneId;
		public int PendingX = -1;
		public int PendingY = -1;

		[NonSerialized]
		internal object LiveAuthority;
	}
}
