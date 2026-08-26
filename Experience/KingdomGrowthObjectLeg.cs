using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomGrowthObjectLeg
	{
		public string OperationId;
		public string EventId;
		public string ObjectId;
		public string Marker;
		public string Blueprint;
		public string ZoneId;
		public KingdomLifecycleTopology Topology;
		public string OwnerId;
		public int X = -1;
		public int Y = -1;
		public int BeforeCount;
		public int Delta;
		public int AfterCount;
		public bool NoStack;
		public KingdomGrowthObjectMutationKind MutationKind;
		public string BeforeOwnerGraphHash;
		public string AfterOwnerGraphHash;
		public string BeforeObjectGraphHash;
		public string AfterObjectGraphHash;
		public string BeforeTopologyHash;
		public string AfterTopologyHash;
		public string CreatedMarker;
		public string DetachedMarker;
		public KingdomGrowthLocationKind BeforeLocation;
		public KingdomGrowthLocationKind AfterLocation;
		public string EscrowKey;
		public int CallbackCursor;
		public List<KingdomGrowthObjectCallbackStep> Callbacks =
			new List<KingdomGrowthObjectCallbackStep>();
		public KingdomLifecycleResourceLease Lease;
		public KingdomLifecyclePhysicalState State;
		public string ReceiptId;
		public string ReceiptTopologyId;
		public int ReceiptBeforeIdMatches = -1;
		public int ReceiptBeforeMarkerMatches = -1;
		public int ReceiptBeforeCount = -1;
		public int ReceiptAfterIdMatches = -1;
		public int ReceiptAfterMarkerMatches = -1;
		public int ReceiptAfterCount = -1;
		public string ReceiptBeforeOwnerGraphHash;
		public string ReceiptAfterOwnerGraphHash;
		public string ReceiptBeforeObjectGraphHash;
		public string ReceiptAfterObjectGraphHash;
		public string ReceiptBeforeTopologyHash;
		public string ReceiptAfterTopologyHash;
		public string ReceiptCallbackObjectId;
		public string ReceiptCallbackMarker;
		public string ReceiptCallbackReferenceHash;
		public bool ReceiptSameReference;
		public string ReceiptProofId;
		public KingdomLifecyclePhysicalState ReceiptState;
	}
}
