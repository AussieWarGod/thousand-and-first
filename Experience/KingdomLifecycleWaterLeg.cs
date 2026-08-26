using System;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomLifecycleWaterLeg
	{
		public string OperationId;
		public string LeaseKey;
		public string OwnerId;
		public string Blueprint;
		public string ZoneId;
		public int Capacity;
		public int Before;
		public int Delta;
		public int After;
		public string Composition;
		public string ReceiptId;
		public int ReceiptBeforeMatches = -1;
		public int ReceiptAfterMatches = -1;
		public bool ReceiptSameReference;
		public string ReceiptProofId;
		public KingdomLifecyclePhysicalState ReceiptState;
		public KingdomLifecyclePhysicalState State;

		// Opaque reference retained only during one live trusted-world pass. Runtime authority
		// is re-established from engine observations and never stored in the save DTO.
		[NonSerialized]
		internal object LiveAuthority;
	}
}
