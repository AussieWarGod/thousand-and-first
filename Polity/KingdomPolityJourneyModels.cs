using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public enum KingdomPolityManifestAuthorityKind : byte
	{
		None = 0,
		Errand = 1,
		PhysicalCargo = 2
	}

	public enum KingdomPolityCorrespondencePhase : byte
	{
		Prepared = 0,
		Outbound = 1,
		Available = 2,
		Blocked = 3,
		Confrontation = 4,
		EntitlementRecorded = 5,
		Returned = 6,
		Cancelled = 7
	}

	/// <summary>
	/// Adapter-neutral snapshot of an owning inventory authority. Polity never owns these
	/// quantities; it only verifies conservation before recording a semantic route transition.
	/// </summary>
	[Serializable]
	public sealed class KingdomPolityManifestProof
	{
		public string ProofId;
		public string SourceAuthorityId;
		public string ManifestOrErrandId;
		public KingdomPolityManifestAuthorityKind Kind;
		public string UnitKey;
		public long SourceBefore;
		public long SourceAfter;
		public long Debited;
		public long InCustody;
		public long Delivered;
		public long Returned;
		public string DebitReceiptId;
		public string DeliveryReceiptId;
		public string ReturnReceiptId;
		public string ProofDigest;
	}

	[Serializable]
	public sealed class KingdomPolityRoutePlanRequest
	{
		public string RouteId;
		public string EventStreamId;
		public string OriginId;
		public string DestinationId;
		public List<string> OrderedPath = new List<string>();
		public KingdomPolityRouteMode Mode;
		public KingdomPolityRoutePurpose Purpose;
		public ulong DepartureOrdinal;
		public long FirstDueTick;
		public string ManifestOrErrandId;
		public string CounterpartyRef;
	}

	[Serializable]
	public sealed class KingdomPolityCorrespondenceProof
	{
		public string CorrespondenceId;
		public string RouteId;
		public string CounterpartyRef;
		public string NeedRef;
		public string NewsRef;
		public string ManifestOrErrandId;
		public string ReturnRef;
		public string ProofDigest;
	}

	[Serializable]
	public sealed class KingdomPolityCorrespondenceView
	{
		public string CorrespondenceId;
		public string RouteId;
		public string CounterpartyRef;
		public string NeedRef;
		public string NewsRef;
		public string ManifestOrErrandId;
		public string ReturnRef;
		public string PurposeVerb;
		public KingdomPolityCorrespondencePhase Phase;
		public int SegmentIndex;
		public int SegmentCount;
		public long NextDueTick;
	}

	[Serializable]
	public sealed class KingdomPolityPresentationAuthorityProof
	{
		public KingdomExperienceOptionKind OptionKind;
		public long EnableEpoch;
		public long ReservedTick;
	}

	[Serializable]
	public sealed class KingdomPolityCohortPlanRequest
	{
		public string CohortId;
		public KingdomPolityCohortPurpose Purpose;
		public string SourceRef;
		public string PolityId;
		public string SurfaceRef;
		public int MemberCount;
		public string NamedFigureId;
		public string EventStreamId;
		public int RulesVersion;
		public ulong EventOrdinal;
		public KingdomPolityPresentationAuthorityProof PresentationAuthority;
	}
}
