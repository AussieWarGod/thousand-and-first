using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Ephemeral explicit-consent selector. Physical truth is re-proved by runtime.</summary>
	[Serializable]
	public sealed class KingdomPolityConsentedEscrowRequest
	{
		public string IncidentPlanId;
		public string SurfaceRef;
		public string ZoneId;
		public long ConsentTick;
		public string ConsentFactId;
		public List<string> ParticipantProjectionIds = new List<string>();
		public string StakeRef;
		public string CollateralObjectId;
		public string SnapshotDigest;
	}

	/// <summary>Trusted loaded-ground proof emitted only after exact lease observation.</summary>
	[Serializable]
	public sealed class KingdomPolityEscrowCustodyProof
	{
		public string ProjectionId;
		public string IncidentPlanId;
		public string ZoneId;
		public string CollateralObjectId;
		public string SnapshotDigest;
		public string AppliedDigest;
		public long CommitTick;
		public string ProofDigest;
	}

	/// <summary>Trusted loaded-ground proof that exact pre-state was restored.</summary>
	[Serializable]
	public sealed class KingdomPolityEscrowRefundProof
	{
		public string ProjectionId;
		public string IncidentPlanId;
		public string ZoneId;
		public string CollateralObjectId;
		public string SnapshotDigest;
		public long RefundTick;
		public string ProofDigest;
	}
}
