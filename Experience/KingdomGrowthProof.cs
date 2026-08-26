using System;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomGrowthProof
	{
		public KingdomGrowthSlotKind Slot;
		public string FieldId;
		public long Sequence;
		public string Id;
		public string PlanHash;
		public KingdomGrowthAction Action;
		public long Tick;
	}
}
