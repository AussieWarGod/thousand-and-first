using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	[Serializable]
	public sealed class KingdomPolityNamedFigureRecord
	{
		public string FigureId;
		public string PolityId;
		public string DisplayName;
		public string RoleKey;
		public KingdomPolityFigureOrigin Origin;
		public KingdomPolityFigurePhase Phase;
		public string CauseRef;
		public string ChronicleRef;
		public string ConclusionRef;
		/// <summary>
		/// Exact bridge to current-realm resident authority. Zero/null means no resident bridge;
		/// runtime object ids are deliberately not polity authority.
		/// </summary>
		public int ResidentId;
		public string ResidentSettlementId;
	}

	[Serializable]
	public sealed class KingdomPolitySystemicDelta
	{
		public KingdomPolitySystemicDeltaKind Kind;
		public string TargetId;
		public int Amount;
		public string ReceiptId;
	}

	[Serializable]
	public sealed class KingdomPolityRelationDelta
	{
		public string RelationId;
		public KingdomPolityRelationBand Before;
		public KingdomPolityRelationBand After;
		public string ReceiptId;
	}

	[Serializable]
	public sealed class KingdomPolityIncidentConclusion
	{
		public string ConclusionId;
		public KingdomPolityResolutionKind ResolutionKind;
		public long CommitTick;
		public List<string> ObservedFactIds = new List<string>();
		public List<KingdomPolitySystemicDelta> SystemicDeltas =
			new List<KingdomPolitySystemicDelta>();
		public List<KingdomPolityRelationDelta> RelationDeltas =
			new List<KingdomPolityRelationDelta>();
		public List<string> ReceiptRefs = new List<string>();
		public string ConsentReceiptId;
		public string EscrowReceiptId;
		public string SnapshotReceiptId;
	}

	[Serializable]
	public sealed class KingdomPolityIncidentRecord
	{
		public string IncidentPlanId;
		public string IncidentId;
		public List<string> GrievanceRefs = new List<string>();
		public List<string> ParticipantCohortRefs = new List<string>();
		public List<string> DisclosedStakeRefs = new List<string>();
		public int MaxSystemicWound;
		public KingdomPolityCohortPurpose Purpose;
		public string EventStreamId;
		public int RulesVersion;
		public ulong EventOrdinal;
		public List<string> EligibleSurfaceRefs = new List<string>();
		public List<string> InterventionOptionKeys = new List<string>();
		public KingdomPolityHospitalityTransaction Hospitality;
		public KingdomPolityInterventionRecord Intervention;
		public KingdomPolityAftermathRecord Aftermath;
		public KingdomPolityIncidentConclusion Conclusion;
	}

	[Serializable]
	public sealed class KingdomPolityProjectionReceipt
	{
		public string ProjectionId;
		public KingdomPolityProjectionKind Kind;
		public string SourceRef;
		public KingdomPolityProjectionPhase Phase;
		public string ZoneId;
		public List<string> ObjectIds = new List<string>();
		public string PriorDigest;
		public string AppliedDigest;
		public long PreparedTick;
		public long CommittedTick;
	}

	[Serializable]
	public sealed class KingdomPolityCompactionReceipt
	{
		public string ReceiptId;
		public long SourceRevision;
		public long CommittedRevision;
		public long CommitTick;
		public List<KingdomPolityProfileRef> RemovedProfiles =
			new List<KingdomPolityProfileRef>();
		public string RemovedDigest;
	}

	[Serializable]
	public sealed class KingdomPolityOptions
	{
		public KingdomPolityImportPolicy ImportPolicy;
		public bool ImportPolicyFrozen;
		public KingdomPolityPresentationState Presentation;
		public long ObservedTick;
		public long EnableEpoch;
		public long FutureCauseFloorTick;
	}
}
