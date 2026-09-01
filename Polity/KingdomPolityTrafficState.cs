using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	[Serializable]
	public sealed class KingdomPolityRouteRecord
	{
		public string RouteId;
		public string EventStreamId;
		public string OriginId;
		public string DestinationId;
		public List<string> OrderedPath = new List<string>();
		public KingdomPolityRouteMode Mode;
		public KingdomPolityRoutePurpose Purpose;
		public KingdomPolityRoutePhase Phase;
		public ulong DepartureOrdinal;
		public long DepartureTick;
		public int SegmentIndex;
		public long NextDueTick;
		public string ManifestOrErrandId;
		public string CounterpartyRef;
		public string FrontId;
		public string DepartureReceiptId;
		public string DeliveryReceiptId;
		public string ReturnReceiptId;
		public string ActiveManifestationId;
	}

	[Serializable]
	public sealed class KingdomPolityGrievanceRecord
	{
		public string GrievanceId;
		public string IssuerPolityId;
		public string TargetPolityId;
		public KingdomPolityGrievanceCause Cause;
		public string SourceEventId;
		public int Severity;
		public List<string> EvidenceRefs = new List<string>();
		public KingdomPolityGrievancePhase Phase;
		public string ConsumedByIncidentId;
		public string ResolutionRef;
	}

	[Serializable]
	public sealed class KingdomPolityFrontRecord
	{
		public string FrontId;
		public KingdomPolityFrontTarget TargetKind;
		public string TargetRef;
		public int PressureBand;
		public long NextDueEventTick;
		public List<string> GrievanceRefs = new List<string>();
		public KingdomPolityFrontPhase Phase;
	}

	[Serializable]
	public sealed class KingdomPolityCohortMember
	{
		public int Ordinal;
		public string MemberKey;
		public string BlueprintKey;
		public string LoadoutKey;
		public string SignatureKey;
	}

	[Serializable]
	public sealed class KingdomPolityCohortPlan
	{
		public string CohortId;
		public KingdomPolityCohortPurpose Purpose;
		public string SourceRef;
		public string PolityId;
		public string ProfileId;
		public int ProfileRevision;
		public int MinimumLevel;
		public int MaximumLevel;
		public string SurfaceRef;
		public int ScaleBudget;
		public List<string> RoleSlots = new List<string>();
		public List<KingdomPolityCohortMember> ResolvedMembers =
			new List<KingdomPolityCohortMember>();
		public int NamedRepresentativeAllowance;
		public string EventStreamId;
		public int RulesVersion;
		public ulong EventOrdinal;
		public KingdomExperienceOptionKind PresentationOptionKind;
		public long PresentationEnableEpoch;
		public long PresentationReservedTick;
		public KingdomPolityCohortPhase Phase;
		public string ManifestationReceiptId;
		public string RewardEventId;
		/// <summary>Frozen semantic authority for weekly ambient cohorts. Null is permitted
		/// only for non-ambient cohorts; migrated weekly rows carry an explicit unresolved row.</summary>
		public KingdomPolityAmbientTransaction AmbientTransaction;
	}
}
