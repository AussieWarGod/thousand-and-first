using System;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomGrowthObjectCallbackStep
	{
		public string EventId;
		public KingdomGrowthObjectMutationKind Kind;
		public KingdomGrowthLocationKind FromLocation;
		public KingdomGrowthLocationKind ToLocation;
		public string EscrowKey;
		public string BeforeOwnerId;
		public string AfterOwnerId;
		public string BeforeZoneId;
		public string AfterZoneId;
		public int BeforeX = -1;
		public int BeforeY = -1;
		public int AfterX = -1;
		public int AfterY = -1;
		public int BeforeCount;
		public int AfterCount;
		public bool NoStack;
		public bool BeforeHasHarvestable;
		public bool AfterHasHarvestable;
		public bool BeforeRipe;
		public bool AfterRipe;
		public int BeforeRegenTimer;
		public int AfterRegenTimer;
		public string BeforeRegenTime;
		public string AfterRegenTime;
		public int BeforeTileIndex;
		public int AfterTileIndex;
		public string BeforeRenderTile;
		public string AfterRenderTile;
		public string BeforeRenderColor;
		public string AfterRenderColor;
		public string BeforeRenderDetail;
		public string AfterRenderDetail;
		public string BeforeRenderString;
		public string AfterRenderString;
		public string BeforeTileColor;
		public string AfterTileColor;
		public string BeforeOwnerGraphHash;
		public string AfterOwnerGraphHash;
		public string BeforeObjectGraphHash;
		public string AfterObjectGraphHash;
		public string BeforeTopologyHash;
		public string AfterTopologyHash;
		public KingdomLifecyclePhysicalState State;
		public string ReceiptId;
		public int ReceiptBeforeMatches = -1;
		public int ReceiptAfterMatches = -1;
		public int ReceiptBeforeCount = -1;
		public int ReceiptAfterCount = -1;
		public string ReceiptCallbackObjectId;
		public string ReceiptCallbackMarker;
		public string ReceiptCallbackReferenceHash;
		public bool ReceiptSameReference;
		public string ReceiptBeforeOwnerGraphHash;
		public string ReceiptAfterOwnerGraphHash;
		public string ReceiptBeforeObjectGraphHash;
		public string ReceiptAfterObjectGraphHash;
		public string ReceiptBeforeTopologyHash;
		public string ReceiptAfterTopologyHash;
		public string ReceiptProofId;
		public KingdomLifecyclePhysicalState ReceiptState;
	}
}
