using System;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomLifecycleProof
	{
		public long Sequence;
		public string Id;
		public string PlanHash;
		public KingdomLifecycleLane Lane;
		public KingdomLifecycleAction Action;
		public long Tick;
	}
}
