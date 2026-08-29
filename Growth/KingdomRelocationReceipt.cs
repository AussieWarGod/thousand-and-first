using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Sole durable authority for one consented, sequential heart ring call.</summary>
	public sealed class KingdomRelocationReceipt
	{
		public int Schema;
		public string PlanId;
		public string ZoneId;
		public string RealmId;
		public string HeartId;
		public string SuccessorKey;
		public KingdomRelocationRect HeartGround;
		public long CreatedTick;
		public int Generation;
		public int CurrentMove;
		public bool Held;
		public bool ObstructionAnnounced;
		public KingdomRelocationPhase Phase;
		public string Failure;
		public List<KingdomRelocationMove> Moves = new List<KingdomRelocationMove>();
	}
}
