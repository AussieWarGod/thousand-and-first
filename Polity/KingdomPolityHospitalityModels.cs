using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public enum KingdomPolityHospitalityDebitKind : byte
	{
		None = 0,
		Food = 1,
		Water = 2
	}

	public enum KingdomPolityHospitalityPhase : byte
	{
		Planned = 0,
		Debited = 1,
		Applied = 2,
		Abandoned = 3,
		Quarantined = 4
	}

	[Serializable]
	public sealed class KingdomPolityHospitalityDebitLine
	{
		public KingdomPolityHospitalityDebitKind Kind;
		public string ContainerId;
		public string ObjectId;
		public string Blueprint;
		public int Before;
		public int After;
		public int Capacity;

		public KingdomPolityHospitalityDebitLine Copy()
		{
			return (KingdomPolityHospitalityDebitLine)MemberwiseClone();
		}
	}

	[Serializable]
	public sealed class KingdomPolityHospitalityPlanRequest
	{
		public string SurfaceRef;
		public string ZoneId;
		public long PlannedTick;
		public List<KingdomPolityHospitalityDebitLine> Lines =
			new List<KingdomPolityHospitalityDebitLine>();
	}

	/// <summary>Exact loaded-scene food and water transaction owned by one terms plan.</summary>
	[Serializable]
	public sealed class KingdomPolityHospitalityTransaction
	{
		public string TransactionId;
		public string TermsPlanId;
		public string SurfaceRef;
		public string ZoneId;
		public KingdomPolityHospitalityPhase Phase;
		public long PlannedTick;
		public long DebitedTick;
		public List<KingdomPolityHospitalityDebitLine> Lines =
			new List<KingdomPolityHospitalityDebitLine>();
		public string PlanDigest;
		public KingdomPolityHospitalityProof Proof;
		public string Fault;
	}
}
