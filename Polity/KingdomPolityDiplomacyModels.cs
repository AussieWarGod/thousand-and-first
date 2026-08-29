using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public enum KingdomPolityTermsChoice : byte
	{
		None = 0,
		Accept = 1,
		Counteroffer = 2,
		Truce = 3,
		Refuse = 4
	}

	[Serializable]
	public sealed class KingdomPolityGrievanceRequest
	{
		public string GrievanceId;
		public string IssuerPolityId;
		public string TargetPolityId;
		public KingdomPolityGrievanceCause Cause;
		public string SourceEventId;
		public int Severity;
		public List<string> EvidenceRefs = new List<string>();
	}

	[Serializable]
	public sealed class KingdomPolityTermsPlanRequest
	{
		public string GrievanceId;
		public string TermsPlanId;
		public string TermsIncidentId;
		public string ClashPlanId;
		public string ClashIncidentId;
		public string EnvoyCohortId;
		public List<string> ClashCohortRefs = new List<string>();
		public List<string> DisclosedStakeRefs = new List<string>();
		public List<string> EligibleSurfaceRefs = new List<string>();
		public List<string> TermKeys = new List<string>();
		public string EventStreamId;
		public int RulesVersion;
		public ulong EventOrdinal;
		public int MaxSystemicWound;
	}

	/// <summary>One exact optional serving consumed in a witnessed scene.</summary>
	[Serializable]
	public sealed class KingdomPolityHospitalityProof
	{
		public string ProofId;
		public string SourceAuthorityId;
		public string ItemOrServingId;
		public long BeforeQuantity;
		public long AfterQuantity;
		public long ConsumedQuantity;
		public string ReceiptId;
		public string ObservedFactId;
		public long CommitTick;
		public string ProofDigest;
	}

	/// <summary>Trusted loaded-scene input; never serialized into route authority.</summary>
	[Serializable]
	public sealed class KingdomPolityWitnessedClashProof
	{
		public string ProofId;
		public string IncidentPlanId;
		public string SurfaceRef;
		public string ZoneId;
		public long CommitTick;
		public List<string> ObservedFactIds = new List<string>();
		public List<string> ParticipantProjectionIds = new List<string>();
		public List<KingdomPolitySystemicDelta> SystemicDeltas =
			new List<KingdomPolitySystemicDelta>();
		public List<KingdomPolityRelationDelta> RelationDeltas =
			new List<KingdomPolityRelationDelta>();
		public List<string> ReceiptRefs = new List<string>();
		public string ProofDigest;
	}
}
