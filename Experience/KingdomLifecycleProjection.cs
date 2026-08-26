using System;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomLifecycleProjection
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
		public int Material = -1;
		public int Count;
		public bool NoStack;
		public KingdomLifecyclePhysicalState State;
		// Carry output callback receipt. The id/topology are frozen by the plan; the
		// observations are written around the callback and never inferred from Count.
		public string ReceiptId;
		public string ReceiptTopologyId;
		public int ReceiptBeforeIdMatches = -1;
		public int ReceiptBeforeMarkerMatches = -1;
		public int ReceiptBeforeCount = -1;
		public int ReceiptAfterIdMatches = -1;
		public int ReceiptAfterMarkerMatches = -1;
		public int ReceiptAfterCount = -1;
		public bool ReceiptSameReference;
		public string ReceiptProofId;
		public KingdomLifecyclePhysicalState ReceiptState;

		// Opaque reference retained only during one live trusted-world pass. Runtime authority
		// is re-established from engine observations and never stored in the save DTO.
		[NonSerialized]
		internal object LiveAuthority;
	}
}
