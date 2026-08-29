using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public enum KingdomPolityInterventionChoice : byte
	{
		None = 0,
		MediateCeasefire = 1,
		SupportSettlement = 2,
		SupportVisitor = 3,
		Observe = 4,
		ConsentAbstractResolution = 5
	}

	public enum KingdomPolityAftermathKind : byte
	{
		None = 0,
		Ceasefire = 1,
		WitnessedWithdrawal = 2,
		ConsentedResolution = 3
	}

	/// <summary>One explicit player stance in an exact loaded confrontation.</summary>
	[Serializable]
	public sealed class KingdomPolityInterventionRecord
	{
		public string InterventionId;
		public string IncidentPlanId;
		public KingdomPolityInterventionChoice Choice;
		public string SurfaceRef;
		public string ZoneId;
		public long CommitTick;
		public string ObservedFactId;
		public List<string> ParticipantProjectionIds = new List<string>();
		public string ReceiptId;
		public string ProofDigest;
	}

	/// <summary>Neutral witnessed consequence; never a victor, casualty, death, or conquest claim.</summary>
	[Serializable]
	public sealed class KingdomPolityAftermathRecord
	{
		public string AftermathId;
		public string IncidentPlanId;
		public string ConclusionId;
		public KingdomPolityAftermathKind Kind;
		public string SurfaceRef;
		public string ZoneId;
		public long CommitTick;
		public string ObservedFactId;
		public string InterventionId;
		public string ReceiptId;
		public string ProofDigest;
	}
}
