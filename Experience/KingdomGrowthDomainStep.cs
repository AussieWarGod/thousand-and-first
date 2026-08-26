using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomGrowthDomainStep
	{
		public KingdomGrowthDomainStepKind Kind;
		public KingdomGrowthDomainCallbackKind CallbackKind;
		public string CallbackBodyHash;
		public string EventId;
		public string ActorId;
		public string SubjectId;
		public long BeforeValue;
		public long AfterValue;
		public string BeforeGraphHash;
		public string AfterGraphHash;
		public string BeforeMapHash;
		public string AfterMapHash;
		public KingdomLifecyclePhysicalState State;
		public string ReceiptId;
		public long ReceiptBeforeValue;
		public long ReceiptAfterValue;
		public string ReceiptBeforeGraphHash;
		public string ReceiptAfterGraphHash;
		public string ReceiptBeforeMapHash;
		public string ReceiptAfterMapHash;
		public string ReceiptProofId;
		public KingdomLifecyclePhysicalState ReceiptState;
		public KingdomLifecycleResourceLease Lease;
		public KingdomGrowthScarcitySnapshot ScarcityBefore;
		public KingdomGrowthScarcitySnapshot ScarcityAfter;
		public KingdomGrowthAccountingSnapshot AccountingBefore;
		public KingdomGrowthAccountingSnapshot AccountingAfter;
		public KingdomGrowthFieldState FieldBefore;
		public KingdomGrowthFieldState FieldAfter;
		public List<KingdomGrowthCropRow> CropRowsBefore;
		/// <summary>Plan-stable row graph. A newly-created row has no ObjectId or graph
		/// witnesses until the exact Create receipt settles.</summary>
		public List<KingdomGrowthCropRow> CropRowsDeclaredAfter;
		/// <summary>Exact observed row graph, present only after the registry CAS proves.</summary>
		public List<KingdomGrowthCropRow> CropRowsAfter;
	}
}
