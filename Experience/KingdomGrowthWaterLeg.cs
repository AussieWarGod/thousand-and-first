using System;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomGrowthWaterLeg
	{
		public string OperationId;
		public string EventId;
		public string LeaseKey;
		public KingdomGrowthWaterMutationKind MutationKind;
		public KingdomGrowthWaterContainerKind ContainerKind;
		public string ContainerId;
		public KingdomGrowthLocationKind BeforeLocation;
		public KingdomGrowthLocationKind AfterLocation;
		public string BeforeOwnerId;
		public string AfterOwnerId;
		public string BeforeZoneId;
		public string AfterZoneId;
		public int BeforeX = -1;
		public int BeforeY = -1;
		public int AfterX = -1;
		public int AfterY = -1;
		public bool OwnerRemovedAfter;
		public KingdomLifecycleTopology OwnerTopology;
		public string OwnerId;
		public string Blueprint;
		public string ZoneId;
		public int X = -1;
		public int Y = -1;
		public int Capacity;
		public int Before;
		public int Delta;
		public int After;
		public string BeforeComposition;
		public string AfterComposition;
		public string BeforeOwnerGraphHash;
		public string AfterOwnerGraphHash;
		public string BeforePartGraphHash;
		public string AfterPartGraphHash;
		public string BeforeTopologyHash;
		public string AfterTopologyHash;
		public KingdomLifecyclePhysicalState State;
		public string ReceiptId;
		public int ReceiptBeforeMatches = -1;
		public int ReceiptAfterMatches = -1;
		public string ReceiptBeforeOwnerGraphHash;
		public string ReceiptAfterOwnerGraphHash;
		public string ReceiptBeforePartGraphHash;
		public string ReceiptAfterPartGraphHash;
		public string ReceiptBeforeTopologyHash;
		public string ReceiptAfterTopologyHash;
		public string ReceiptCallbackContainerId;
		public string ReceiptCallbackReferenceHash;
		public bool ReceiptSameReference;
		public string ReceiptProofId;
		public KingdomLifecyclePhysicalState ReceiptState;
		public KingdomLifecycleResourceLease Lease;
	}
}
