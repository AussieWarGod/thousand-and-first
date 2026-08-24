using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	// Numeric values are an append-only wire contract.
	public enum KingdomLifecycleLane : byte
	{
		None = 0,
		PlainGuest = 1,
		NotableGuest = 2,
		Raid = 3,
		Petition = 4
	}

	public enum KingdomLifecycleAction : byte
	{
		None = 0,
		Passages = 1,
		Spawn = 2,
		Depart = 3,
		OfferWater = 4,
		Lodge = 5,
		RaidWarning = 6,
		RaidRewarning = 7,
		RaidTribute = 8,
		RaidTalkDown = 9,
		RaidAttack = 10,
		RaidCancel = 11,
		PetitionOffer = 12,
		PetitionAccept = 13,
		PetitionDecline = 14,
		PetitionResolve = 15,
		PetitionExpire = 16
	}

	public enum KingdomLifecyclePhase : byte
	{
		Invalid = 0,
		Prepared = 1,
		ProjectionIntent = 2,
		Projected = 3,
		WaterIntent = 4,
		WaterSettled = 5,
		RemovalIntent = 6,
		Removed = 7,
		DomainIntent = 8,
		DomainSettled = 9,
		EffectIntent = 10,
		EffectsSettled = 11,
		Sinks = 12,
		ScheduleIntent = 13,
		Terminal = 14,
		Quarantined = 15
	}

	public enum KingdomLifecyclePhysicalState : byte
	{
		None = 0,
		Prepared = 1,
		Intent = 2,
		Proved = 3,
		Skipped = 4,
		Lost = 5
	}

	public enum KingdomLifecycleSinkState : byte
	{
		None = 0,
		Pending = 1,
		Intent = 2,
		Delivered = 3,
		Skipped = 4,
		Lost = 5
	}

	public enum KingdomLifecycleSinkDisposition : byte
	{
		Unknown = 0,
		Deliver = 1,
		Skip = 2
	}

	public enum KingdomLifecycleOptionState : byte
	{
		Unknown = 0,
		Disabled = 1,
		Enabled = 2
	}

	public enum KingdomLifecycleTopology : byte
	{
		None = 0,
		Cell = 1,
		Inventory = 2
	}

	public enum KingdomLifecycleResourceKind : byte
	{
		None = 0,
		Population = 1,
		Roster = 2,
		OriginRoster = 3,
		CreedRoster = 4,
		Standing = 5,
		Schedule = 6,
		WaterVessel = 7,
		Object = 8,
		Projection = 9,
		Petition = 10,
		Raid = 11,
		GrowthClock = 12,
		GrowthPendingCrop = 13,
		GrowthField = 14,
		GrowthHealth = 15,
		GrowthScarcity = 16,
		GrowthAccounting = 17,
		GrowthCropRegistry = 18,
		GrowthSubsidenceSchedule = 19,
		GrowthPorterJob = 20,
		GrowthEscrowRelease = 21,
		GrowthArrivalCandidate = 22
	}

	public enum KingdomLifecycleLeaseState : byte
	{
		None = 0,
		Prepared = 1,
		Intent = 2,
		Proved = 3,
		Skipped = 4
	}

	// Numeric values are append-only nested Growth wire contracts.
	public enum KingdomGrowthAction : byte
	{
		None = 0,
		Heartbeat = 1,
		Arrival = 2,
		Departure = 3,
		Delivery = 4,
		Sow = 5,
		Withdraw = 6,
		Ripen = 7,
		Harvest = 8,
		Fetch = 9,
		Mill = 10,
		Irrigate = 11
	}

	public enum KingdomGrowthPhase : byte
	{
		Invalid = 0,
		Prepared = 1,
		WaterIntent = 2,
		WaterSettled = 3,
		SourceIntent = 4,
		SourcesSettled = 5,
		OutputIntent = 6,
		OutputsSettled = 7,
		DomainIntent = 8,
		DomainSettled = 9,
		ClockIntent = 10,
		Sinks = 11,
		Terminal = 12,
		Quarantined = 13
	}

	public enum KingdomGrowthHealthState : byte
	{
		Unknown = 0,
		Healthy = 1,
		Unhealthy = 2
	}

	public enum KingdomGrowthSlotKind : byte
	{
		None = 0,
		Heartbeat = 1,
		Arrival = 2,
		Departure = 3,
		Delivery = 4,
		Field = 5,
		Fetch = 6,
		Mill = 7
	}

	public enum KingdomGrowthDomainStepKind : byte
	{
		None = 0,
		Enrollment = 1,
		Roster = 2,
		Creed = 3,
		Population = 4,
		PendingCrop = 5,
		Field = 6,
		Clock = 7,
		Scarcity = 8,
		Accounting = 9,
		CropRegistry = 10,
		SubsidenceSchedule = 11,
		PorterJob = 12,
		EscrowRelease = 13
	}

	public enum KingdomGrowthObjectMutationKind : byte
	{
		None = 0,
		Create = 1,
		CellAdd = 2,
		InventoryAdd = 3,
		Receive = 4,
		DestroyOne = 5,
		Obliterate = 6,
		HarvestableRipeSet = 7
	}

	public enum KingdomGrowthWaterMutationKind : byte
	{
		None = 0,
		Drain = 1,
		Fill = 2
	}

	public enum KingdomGrowthWaterContainerKind : byte
	{
		None = 0,
		LiquidVolume = 1
	}

	public enum KingdomGrowthDomainCallbackKind : byte
	{
		None = 0,
		Enroll = 1,
		RosterAdd = 2,
		RosterRemove = 3,
		CreedSet = 4,
		PopulationAdjust = 5,
		PendingCropSet = 6,
		FieldSet = 7,
		ScarcitySet = 8,
		AccountingSet = 9,
		CropRegistrySet = 10,
		SubsidenceScheduleSet = 11,
		PorterJobSet = 12,
		EscrowRelease = 13
	}

	// Growth-only location contract. Outer lifecycle/carry topology stays byte- and enum-sealed.
	public enum KingdomGrowthLocationKind : byte
	{
		None = 0,
		Absent = 1,
		Escrow = 2,
		Cell = 3,
		Inventory = 4,
		Graveyard = 5
	}

	public enum KingdomGrowthArrivalDisposition : byte
	{
		None = 0,
		Joined = 1,
		WaterUnavailable = 2,
		NoAcceptableHome = 3,
		NoGround = 4,
		PopulationCap = 5,
		SupportCap = 6
	}

	public enum KingdomGrowthArrivalRefusalReason : byte
	{
		None = 0,
		NoRoofAtAll = 1,
		NeedsUnmet = 2,
		Full = 3,
		Refused = 4,
		Condemned = 5
	}

	public enum KingdomGrowthDeliveryMode : byte
	{
		None = 0,
		PlainLarder = 1,
		PorterJob = 2
	}

	public enum KingdomGrowthDepartureCauseKind : byte
	{
		None = 0,
		Scarcity = 1,
		Subsidence = 2,
		Lodging = 3,
		Conversion = 4,
		Exile = 5,
		Death = 6
	}

	public enum KingdomGrowthThirstOutcome : byte
	{
		Sustained = 0,
		Warned = 1,
		Emigration = 2,
		Withering = 3
	}

	public enum KingdomGrowthHungerOutcome : byte
	{
		Fed = 0,
		Warned = 1,
		Emigration = 2,
		Famine = 3
	}

	public enum KingdomGrowthComposedBite : byte
	{
		None = 0,
		Warned = 1,
		Departure = 2,
		Terminal = 3
	}

	public enum KingdomGrowthArrivalCandidatePhase : byte
	{
		Prepared = 1,
		CreateIntent = 2,
		Escrowed = 3,
		LodgingIntent = 4,
		Observed = 5,
		ConsumeIntent = 6,
		RefusalIntent = 7,
		Settled = 8,
		Quarantined = 9
	}

	public enum KingdomGrowthOutboxSinkKind : byte
	{
		None = 0,
		Chronicle = 1,
		Ledger = 2,
		Message = 3,
		Deed = 4,
		Guestbook = 5
	}

	[Serializable]
	public sealed class KingdomLifecycleWaterLeg
	{
		public string OperationId;
		public string LeaseKey;
		public string OwnerId;
		public string Blueprint;
		public string ZoneId;
		public int Capacity;
		public int Before;
		public int Delta;
		public int After;
		public string Composition;
		public string ReceiptId;
		public int ReceiptBeforeMatches = -1;
		public int ReceiptAfterMatches = -1;
		public bool ReceiptSameReference;
		public string ReceiptProofId;
		public KingdomLifecyclePhysicalState ReceiptState;
		public KingdomLifecyclePhysicalState State;

		[NonSerialized]
		internal object LiveAuthority;
	}

	[Serializable]
	public sealed class KingdomLifecycleProjection
	{
		public string OperationId;
		public string EventId;
		public string ObjectId;
		public string Marker;
		public string Blueprint;
		public string ZoneId;
		public KingdomLifecycleTopology Topology;
		public string OwnerId;
		public int X = -1;
		public int Y = -1;
		public int Material = -1;
		public int Count;
		public bool NoStack;
		public KingdomLifecyclePhysicalState State;
		// Carry output callback receipt. The id/topology are frozen by the plan; the
		// observations are written around the callback and never inferred from Count.
		public string ReceiptId;
		public string ReceiptTopologyId;
		public int ReceiptBeforeIdMatches = -1;
		public int ReceiptBeforeMarkerMatches = -1;
		public int ReceiptBeforeCount = -1;
		public int ReceiptAfterIdMatches = -1;
		public int ReceiptAfterMarkerMatches = -1;
		public int ReceiptAfterCount = -1;
		public bool ReceiptSameReference;
		public string ReceiptProofId;
		public KingdomLifecyclePhysicalState ReceiptState;

		[NonSerialized]
		internal object LiveAuthority;
	}

	[Serializable]
	public sealed class KingdomLifecycleOutbox
	{
		public string OperationId;
		public string EventId;
		public string ChronicleReceiptId;
		public string Chronicle;
		public bool ChronicleAccomplishment;
		public KingdomLifecycleSinkDisposition ChronicleDisposition;
		public KingdomLifecycleSinkState ChronicleState;
		public string Ledger;
		public KingdomLifecycleSinkDisposition LedgerDisposition;
		public KingdomLifecycleSinkState LedgerState;
		public string Message;
		public KingdomLifecycleSinkDisposition MessageDisposition;
		public KingdomLifecycleSinkState MessageState;
		public string Deed;
		public KingdomLifecycleSinkDisposition DeedDisposition;
		public KingdomLifecycleSinkState DeedState;
		public string GuestbookLine;
		public KingdomLifecycleSinkDisposition GuestbookDisposition;
		public KingdomLifecycleSinkState GuestbookState;
	}

	[Serializable]
	public sealed class KingdomLifecycleResourceLease
	{
		public string OperationId;
		public KingdomLifecycleResourceKind Kind;
		public string ScopeId;
		public string SubjectId;
		public string Key;
		public long Before;
		public long Delta;
		public long After;
		public long BeforeRevision;
		public long AfterRevision;
		public KingdomLifecycleLeaseState State;
	}

	/// <summary>
	/// Shared typed CAS witness. LastOperationId is the proof which disambiguates equal scalar
	/// values produced by different lanes. ActiveOperationId is a persisted exclusive lease.
	/// Rows are never evicted; hitting the bounded cap refuses new work.
	/// </summary>
	[Serializable]
	public sealed class KingdomLifecycleResourceRevision
	{
		public KingdomLifecycleResourceKind Kind;
		public string ScopeId;
		public string SubjectId;
		public string Key;
		public long Revision;
		public string ActiveOperationId;
		public string LastOperationId;
	}

	[Serializable]
	public sealed class KingdomLifecycleOperation
	{
		public long Sequence;
		public string Id;
		public string PlanHash;
		public KingdomLifecycleLane Lane;
		public KingdomLifecycleAction Action;
		public KingdomLifecyclePhase Phase;
		public long CreatedTick;
		public long UpdatedTick;
		public string SettlementId;
		public string ZoneId;
		public string ObjectId;
		public string ObjectMarker;
		public string Blueprint;
		public KingdomLifecycleTopology ObjectTopology;
		public string ObjectOwnerId;
		public int ObjectX = -1;
		public int ObjectY = -1;
		public string ObjectName;
		public string Origin;
		public string Faction;
		public string DisplayFaction;
		public string Detail;
		public string Creed;
		public int Kind;
		public int Target;
		public int Count;
		public int DepartedCount;
		public long DueBefore;
		public long DueAfter;
		public long DepartTick;
		public int WaterRequested;
		public int WaterProved;
		public int WaterOutstanding;
		public int WaterLost;
		public int WaterAmbiguous;
		public KingdomLifecyclePhysicalState WaterState;
		public List<KingdomLifecycleWaterLeg> WaterLegs = new List<KingdomLifecycleWaterLeg>();
		public KingdomLifecyclePhysicalState RemovalState;
		public List<KingdomLifecycleProjection> Projections = new List<KingdomLifecycleProjection>();
		public KingdomLifecyclePhysicalState EffectState;
		public List<KingdomLifecycleResourceLease> ResourceLeases =
			new List<KingdomLifecycleResourceLease>();
		public int Defence;
		public int PartySize;
		public int Spawned;
		public int PlunderRequested;
		public int PlunderProved;
		public string ArrivalText;
		public KingdomLifecycleOutbox Outbox;
		public string Fault;

		[NonSerialized]
		public object LiveAuthority;
	}

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

	/// <summary>Wave-1 migration input. Runtime Wave 2 supplies the load tick and the old
	/// city-carried pending crop tuple. All legacy clocks are deliberately restamped to Now;
	/// none of their elapsed pre-transactional time becomes backlog.</summary>
	public sealed class KingdomGrowthMigrationInput
	{
		public bool HasNow;
		public long Now;
		public int PendingCrop;
		public string PendingCropBlueprint;
		public string PendingCropZoneId;
		public bool OptionEnabled;
		public bool ScarcityEnabled;
		public bool Healthy;
		public long ArrivalIntervalTicks;
	}

	public sealed class KingdomGrowthMigrationResult
	{
		public bool Valid;
		public string Failure;
		public KingdomGrowthBook Growth;
	}

	[Serializable]
	public sealed class KingdomGrowthScarcitySnapshot
	{
		public int DryStreak;
		public bool Withered;
		public int HungerStreak;
		public bool Famished;
		public KingdomRules.MealVerdict LastMeal;
		public int MealShade;
		public bool ScrapsAnnounced;
		public long ElapsedTicks;
		public int Days;
		public int Population;
		public int Stage;
		public int UpkeepRequested;
		public int WaterAvailable;
		public int RationsAvailable;
		public int Foraged;
		public int Eaten;
		public int FromDish;
		public int Kitchens;
		public string DishName;
		public string DishText;
		public string DishStaple;
		public string DishSource;
		public KingdomGrowthComposedBite ComposedBite;
		public int RequestedWater;
		public int ProvedWater;
		public int RequestedRations;
		public int ProvedRations;
		public int StoresPolicy;
		public int DistrictPercent;
		public KingdomGrowthThirstOutcome ThirstOutcome;
		public KingdomGrowthHungerOutcome HungerOutcome;
		public bool Thirsting;
		public bool Starving;
		public bool Withering;
		public bool Famishing;
		public bool Healthy;
	}

	[Serializable]
	public sealed class KingdomGrowthAccountingSnapshot
	{
		public int Fetched;
		public int UpkeepDrawn;
		public int ArrivalCost;
		public int Delivered;
		public int Harvested;
		public int Foraged;
		public int RationsDrawn;
		public int Milled;
		public int HarvestLost;
		public int Plundered;
		public int Arrivals;
		public int Departures;
	}

	[Serializable]
	public sealed class KingdomGrowthOutboxEvent
	{
		public string EventId;
		public string Kind;
		public int ChronicleBeforeCount;
		public int ChronicleDeclaredAfterCount;
		public int ChronicleObservedCount = -1;
		public string ChronicleBeforeHash;
		public string ChronicleDeclaredAfterHash;
		public string ChronicleObservedHash;
		public int LedgerBeforeCount;
		public int LedgerDeclaredAfterCount;
		public int LedgerObservedCount = -1;
		public string LedgerBeforeHash;
		public string LedgerDeclaredAfterHash;
		public string LedgerObservedHash;
		public KingdomLifecycleOutbox Outbox;
	}

	[Serializable]
	public sealed class KingdomGrowthObjectCallbackStep
	{
		public string EventId;
		public KingdomGrowthObjectMutationKind Kind;
		public KingdomGrowthLocationKind FromLocation;
		public KingdomGrowthLocationKind ToLocation;
		public string EscrowKey;
		public string BeforeOwnerId;
		public string AfterOwnerId;
		public string BeforeZoneId;
		public string AfterZoneId;
		public int BeforeX = -1;
		public int BeforeY = -1;
		public int AfterX = -1;
		public int AfterY = -1;
		public int BeforeCount;
		public int AfterCount;
		public bool NoStack;
		public bool BeforeHasHarvestable;
		public bool AfterHasHarvestable;
		public bool BeforeRipe;
		public bool AfterRipe;
		public int BeforeRegenTimer;
		public int AfterRegenTimer;
		public string BeforeRegenTime;
		public string AfterRegenTime;
		public int BeforeTileIndex;
		public int AfterTileIndex;
		public string BeforeRenderTile;
		public string AfterRenderTile;
		public string BeforeRenderColor;
		public string AfterRenderColor;
		public string BeforeRenderDetail;
		public string AfterRenderDetail;
		public string BeforeRenderString;
		public string AfterRenderString;
		public string BeforeTileColor;
		public string AfterTileColor;
		public string BeforeOwnerGraphHash;
		public string AfterOwnerGraphHash;
		public string BeforeObjectGraphHash;
		public string AfterObjectGraphHash;
		public string BeforeTopologyHash;
		public string AfterTopologyHash;
		public KingdomLifecyclePhysicalState State;
		public string ReceiptId;
		public int ReceiptBeforeMatches = -1;
		public int ReceiptAfterMatches = -1;
		public int ReceiptBeforeCount = -1;
		public int ReceiptAfterCount = -1;
		public string ReceiptCallbackObjectId;
		public string ReceiptCallbackMarker;
		public string ReceiptCallbackReferenceHash;
		public bool ReceiptSameReference;
		public string ReceiptBeforeOwnerGraphHash;
		public string ReceiptAfterOwnerGraphHash;
		public string ReceiptBeforeObjectGraphHash;
		public string ReceiptAfterObjectGraphHash;
		public string ReceiptBeforeTopologyHash;
		public string ReceiptAfterTopologyHash;
		public string ReceiptProofId;
		public KingdomLifecyclePhysicalState ReceiptState;
	}

	[Serializable]
	public sealed class KingdomGrowthArrivalCandidate
	{
		public long Sequence;
		public string Id;
		public string PlanHash;
		public string SettlementId;
		public long CreatedTick;
		public long UpdatedTick;
		public KingdomGrowthArrivalCandidatePhase Phase;
		public KingdomGrowthArrivalCandidatePhase EvidencePhase;
		public KingdomGrowthArrivalDisposition Disposition;
		public KingdomGrowthArrivalRefusalReason RefusalReason;
		public string ObjectId;
		public string Marker;
		public string Blueprint;
		public string EscrowKey;
		public KingdomLifecycleResourceLease CandidateLease;
		public KingdomLifecycleResourceLease LodgingLease;
		public KingdomLifecycleResourceLease EscrowLease;
		public KingdomGrowthObjectCallbackStep CreateStep;
		public KingdomGrowthObjectCallbackStep DispositionStep;
		public string LodgingZoneId;
		public int LodgingX = -1;
		public int LodgingY = -1;
		public string LodgingBeforeGraphHash;
		public string LodgingDeclaredGraphHash;
		public string LodgingReceiptGraphHash;
		public string LodgingCallbackReferenceHash;
		public bool LodgingSameReference;
		public string LodgingReceiptId;
		public KingdomLifecyclePhysicalState LodgingState;
		public string ConsumingOperationId;
		public long ConsumingOperationSequence;
		public string Fault;
	}

	[Serializable]
	public sealed class KingdomGrowthWaterLeg
	{
		public string OperationId;
		public string EventId;
		public string LeaseKey;
		public KingdomGrowthWaterMutationKind MutationKind;
		public KingdomGrowthWaterContainerKind ContainerKind;
		public string ContainerId;
		public KingdomGrowthLocationKind BeforeLocation;
		public KingdomGrowthLocationKind AfterLocation;
		public string BeforeOwnerId;
		public string AfterOwnerId;
		public string BeforeZoneId;
		public string AfterZoneId;
		public int BeforeX = -1;
		public int BeforeY = -1;
		public int AfterX = -1;
		public int AfterY = -1;
		public bool OwnerRemovedAfter;
		public KingdomLifecycleTopology OwnerTopology;
		public string OwnerId;
		public string Blueprint;
		public string ZoneId;
		public int X = -1;
		public int Y = -1;
		public int Capacity;
		public int Before;
		public int Delta;
		public int After;
		public string BeforeComposition;
		public string AfterComposition;
		public string BeforeOwnerGraphHash;
		public string AfterOwnerGraphHash;
		public string BeforePartGraphHash;
		public string AfterPartGraphHash;
		public string BeforeTopologyHash;
		public string AfterTopologyHash;
		public KingdomLifecyclePhysicalState State;
		public string ReceiptId;
		public int ReceiptBeforeMatches = -1;
		public int ReceiptAfterMatches = -1;
		public string ReceiptBeforeOwnerGraphHash;
		public string ReceiptAfterOwnerGraphHash;
		public string ReceiptBeforePartGraphHash;
		public string ReceiptAfterPartGraphHash;
		public string ReceiptBeforeTopologyHash;
		public string ReceiptAfterTopologyHash;
		public string ReceiptCallbackContainerId;
		public string ReceiptCallbackReferenceHash;
		public bool ReceiptSameReference;
		public string ReceiptProofId;
		public KingdomLifecyclePhysicalState ReceiptState;
		public KingdomLifecycleResourceLease Lease;
	}

	[Serializable]
	public sealed class KingdomGrowthObjectLeg
	{
		public string OperationId;
		public string EventId;
		public string ObjectId;
		public string Marker;
		public string Blueprint;
		public string ZoneId;
		public KingdomLifecycleTopology Topology;
		public string OwnerId;
		public int X = -1;
		public int Y = -1;
		public int BeforeCount;
		public int Delta;
		public int AfterCount;
		public bool NoStack;
		public KingdomGrowthObjectMutationKind MutationKind;
		public string BeforeOwnerGraphHash;
		public string AfterOwnerGraphHash;
		public string BeforeObjectGraphHash;
		public string AfterObjectGraphHash;
		public string BeforeTopologyHash;
		public string AfterTopologyHash;
		public string CreatedMarker;
		public string DetachedMarker;
		public KingdomGrowthLocationKind BeforeLocation;
		public KingdomGrowthLocationKind AfterLocation;
		public string EscrowKey;
		public int CallbackCursor;
		public List<KingdomGrowthObjectCallbackStep> Callbacks =
			new List<KingdomGrowthObjectCallbackStep>();
		public KingdomLifecycleResourceLease Lease;
		public KingdomLifecyclePhysicalState State;
		public string ReceiptId;
		public string ReceiptTopologyId;
		public int ReceiptBeforeIdMatches = -1;
		public int ReceiptBeforeMarkerMatches = -1;
		public int ReceiptBeforeCount = -1;
		public int ReceiptAfterIdMatches = -1;
		public int ReceiptAfterMarkerMatches = -1;
		public int ReceiptAfterCount = -1;
		public string ReceiptBeforeOwnerGraphHash;
		public string ReceiptAfterOwnerGraphHash;
		public string ReceiptBeforeObjectGraphHash;
		public string ReceiptAfterObjectGraphHash;
		public string ReceiptBeforeTopologyHash;
		public string ReceiptAfterTopologyHash;
		public string ReceiptCallbackObjectId;
		public string ReceiptCallbackMarker;
		public string ReceiptCallbackReferenceHash;
		public bool ReceiptSameReference;
		public string ReceiptProofId;
		public KingdomLifecyclePhysicalState ReceiptState;
	}

	[Serializable]
	public sealed class KingdomGrowthCropRow
	{
		public string FieldId;
		public string RowId;
		public string ObjectId;
		public string Marker;
		public string Blueprint;
		public string ZoneId;
		public string OwnerId;
		public int X = -1;
		public int Y = -1;
		public int Count;
		public bool HasHarvestable;
		public bool Ripe;
		public int RegenTimer;
		public string RegenTime;
		public int TileIndex;
		public string RenderTile;
		public string RenderColor;
		public string RenderDetail;
		public string RenderString;
		public string TileColor;
		public string PartGraphHash;
		public string ObjectGraphHash;
		public string TopologyHash;
		public long Revision;
		public string LastOperationId;
	}

	[Serializable]
	public sealed class KingdomGrowthFieldState
	{
		public string FieldId;
		public string WorkObjectId;
		public string WorkPartId;
		public string Marker;
		public string Blueprint;
		public string ZoneId;
		public int X = -1;
		public int Y = -1;
		public string CropBlueprint;
		public int Stage;
		public long NextStageTick;
		public long SownTick;
		public int Cycles;
		public int SaidWant;
		public int DeclaredRows;
		public int EffectivenessPercent;
		public int MethodPercent;
		public bool NoLarderAnnounced;
		public string SeedBlueprint;
		public string PartGraphHash;
		public string ObjectGraphHash;
		public string TopologyHash;
	}

	[Serializable]
	public sealed class KingdomGrowthOperation
	{
		public long Sequence;
		public string Id;
		public string PlanHash;
		public KingdomGrowthAction Action;
		public KingdomGrowthPhase Phase;
		public long CreatedTick;
		public long UpdatedTick;
		public string SettlementId;
		public string FieldId;
		public string ZoneId;
		public string TargetId;
		public string TargetMarker;
		public string Blueprint;
		public KingdomLifecycleTopology TargetTopology;
		public KingdomGrowthLocationKind TargetLocation;
		public string TargetOwnerId;
		public int TargetX = -1;
		public int TargetY = -1;
		public KingdomLifecycleOptionState OptionState;
		public long OptionTick;
		public KingdomGrowthHealthState HealthState;
		public long HealthTick;
		public long EffectiveWorkBefore;
		public long EffectiveWorkAfter;
		public long FieldClockBefore;
		public long FieldClockAfter;
		public long HeartbeatBefore;
		public long HeartbeatAfter;
		public long ArrivalBefore;
		public long ArrivalAfter;
		public long FetchBefore;
		public long FetchAfter;
		public long MillBefore;
		public long MillAfter;
		public string MillCropBlueprint;
		public string MillStapleBlueprint;
		public long SubsidenceBefore;
		public long SubsidenceAfter;
		public long DeliveryBefore;
		public long DeliveryAfter;
		public long DepartureBefore;
		public long DepartureAfter;
		public KingdomGrowthArrivalDisposition ArrivalDisposition;
		public string ArrivalCandidateId;
		public KingdomGrowthDeliveryMode DeliveryMode;
		public KingdomGrowthDepartureCauseKind DepartureCauseKind;
		public string DepartureCause;
		public string DepartureNote;
		public string DepartureName;
		public string DepartureOrigin;
		public long DepartureArrivedTick;
		public string DepartureCreed;
		public bool DepartureChronicled;
		public string TriggeredByOperationId;
		public KingdomLifecycleOptionState ScarcityOptionState;
		public long ScarcityOptionTick;
		public int PendingCropBefore;
		public int PendingCropDelta;
		public int PendingCropAfter;
		public string PendingCropBlueprintBefore;
		public string PendingCropZoneIdBefore;
		public string PendingCropBlueprintAfter;
		public string PendingCropZoneIdAfter;
		public int PopulationBefore;
		public int PopulationDelta;
		public int PopulationAfter;
		// Exact pure oracle inputs for a harvest. They are zero/default for every
		// other action; the expected yield is recomputed and never caller-credited.
		public int HarvestStandingRows;
		public int HarvestRipeRows;
		public int HarvestCycles;
		public bool HarvestCountsRipeLast;
		public int HarvestEffectivenessPercent;
		public int HarvestMethodPercent;
		public ulong HarvestFirstOrdinal;
		public string HarvestCropBlueprint;
		public string HarvestSeedBlueprint;
		public int WaterCursor;
		public List<KingdomGrowthWaterLeg> WaterLegs =
			new List<KingdomGrowthWaterLeg>();
		public int SourceCursor;
		public List<KingdomGrowthObjectLeg> Sources = new List<KingdomGrowthObjectLeg>();
		public int OutputCursor;
		public List<KingdomGrowthObjectLeg> Outputs = new List<KingdomGrowthObjectLeg>();
		public int DomainCursor;
		public List<KingdomGrowthDomainStep> DomainSteps =
			new List<KingdomGrowthDomainStep>();
		public KingdomLifecycleResourceLease ClockLease;
		public KingdomLifecyclePhysicalState ClockState;
		public List<KingdomGrowthOutboxEvent> OutboxEvents =
			new List<KingdomGrowthOutboxEvent>();
		public string Fault;
	}

	[Serializable]
	public sealed class KingdomGrowthDomainStep
	{
		public KingdomGrowthDomainStepKind Kind;
		public KingdomGrowthDomainCallbackKind CallbackKind;
		public string CallbackBodyHash;
		public string EventId;
		public string ActorId;
		public string SubjectId;
		public long BeforeValue;
		public long AfterValue;
		public string BeforeGraphHash;
		public string AfterGraphHash;
		public string BeforeMapHash;
		public string AfterMapHash;
		public KingdomLifecyclePhysicalState State;
		public string ReceiptId;
		public long ReceiptBeforeValue;
		public long ReceiptAfterValue;
		public string ReceiptBeforeGraphHash;
		public string ReceiptAfterGraphHash;
		public string ReceiptBeforeMapHash;
		public string ReceiptAfterMapHash;
		public string ReceiptProofId;
		public KingdomLifecyclePhysicalState ReceiptState;
		public KingdomLifecycleResourceLease Lease;
		public KingdomGrowthScarcitySnapshot ScarcityBefore;
		public KingdomGrowthScarcitySnapshot ScarcityAfter;
		public KingdomGrowthAccountingSnapshot AccountingBefore;
		public KingdomGrowthAccountingSnapshot AccountingAfter;
		public KingdomGrowthFieldState FieldBefore;
		public KingdomGrowthFieldState FieldAfter;
		public List<KingdomGrowthCropRow> CropRowsBefore;
		/// <summary>Plan-stable row graph. A newly-created row has no ObjectId or graph
		/// witnesses until the exact Create receipt settles.</summary>
		public List<KingdomGrowthCropRow> CropRowsDeclaredAfter;
		/// <summary>Exact observed row graph, present only after the registry CAS proves.</summary>
		public List<KingdomGrowthCropRow> CropRowsAfter;
	}

	[Serializable]
	public sealed class KingdomGrowthFieldSlot
	{
		public string FieldId;
		public long NextSequence = 1L;
		public long RetiredThrough;
		public long ClockTick;
		public long CommitRevision;
		public string LastOperationId;
		public string WorkObjectId;
		public string WorkPartId;
		public string Marker;
		public string Blueprint;
		public string ZoneId;
		public int X = -1;
		public int Y = -1;
		public string CropBlueprint;
		public int Stage;
		public long NextStageTick;
		public long SownTick;
		public int Cycles;
		public int SaidWant;
		public int DeclaredRows;
		public int EffectivenessPercent;
		public int MethodPercent;
		public bool NoLarderAnnounced;
		public string SeedBlueprint;
		public string PartGraphHash;
		public string ObjectGraphHash;
		public string TopologyHash;
		public bool Quarantined;
		public string Fault;
		public KingdomGrowthOperation Operation;
	}

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

	/// <summary>Nested, settlement-bound Growth authority. Future payload bytes are retained
	/// exactly and re-emitted, including malformed known-version payloads whose outer length is
	/// intact. Opaque evidence grants no Growth authority and does not poison enclosing lifecycle.</summary>
	[Serializable]
	public sealed class KingdomGrowthBook
	{
		public int FormatVersion = KingdomLifecycleRules.CurrentGrowthFormatVersion;
		public bool Quarantined;
		public string Fault;
		public int OpaqueWireVersion;
		public byte[] OpaquePayload;
		public string SettlementId;
		public bool IdentityBound;
		public string IdentityProof;
		public int MigratedFromLifecycleVersion;
		public bool MigrationPending;
		public long MigrationTick;
		public KingdomLifecycleOptionState OptionState;
		public long OptionTick;
		public KingdomGrowthHealthState HealthState;
		public long HealthTick;
		public KingdomLifecycleOptionState ScarcityOptionState;
		public long ScarcityOptionTick;
		public bool WorkPaused;
		public long WorkPauseStartedTick;
		public long WorkPausedTicks;
		public long EffectiveWorkTick;
		public long LastHeartbeatTick;
		public long NextArrivalTick;
		public long ArrivalIntervalTicks;
		public long LastFetchTick;
		public long LastMillTick;
		public long LastSubsidenceTick;
		public long LastDeliveryTick;
		public long LastDepartureTick;
		public int PendingCrop;
		public string PendingCropBlueprint;
		public string PendingCropZoneId;
		public long HeartbeatNextSequence = 1L;
		public long HeartbeatRetiredThrough;
		public long ArrivalNextSequence = 1L;
		public long ArrivalRetiredThrough;
		public long DepartureNextSequence = 1L;
		public long DepartureRetiredThrough;
		public long DeliveryNextSequence = 1L;
		public long DeliveryRetiredThrough;
		public long FetchNextSequence = 1L;
		public long FetchRetiredThrough;
		public long MillNextSequence = 1L;
		public long MillRetiredThrough;
		public long ArrivalCandidateNextSequence = 1L;
		public long ArrivalCandidateRetiredThrough;
		public KingdomGrowthOperation HeartbeatOp;
		public KingdomGrowthOperation ArrivalOp;
		public KingdomGrowthOperation DepartureOp;
		public KingdomGrowthOperation DeliveryOp;
		public KingdomGrowthOperation FetchOp;
		public KingdomGrowthOperation MillOp;
		public KingdomGrowthArrivalCandidate ArrivalCandidate;
		public List<KingdomGrowthFieldSlot> FieldOps = new List<KingdomGrowthFieldSlot>();
		public List<KingdomGrowthCropRow> CropRows = new List<KingdomGrowthCropRow>();
		public List<KingdomLifecycleResourceRevision> Resources =
			new List<KingdomLifecycleResourceRevision>();
		public List<KingdomGrowthProof> RecentProofs = new List<KingdomGrowthProof>();
	}

	/// <summary>Per-settlement authority. Every lane has its own monotone replay barrier.</summary>
	[Serializable]
	public sealed class KingdomLifecycleBook
#if !TAF_TESTS
		: IComposite
#endif
	{
		public int FormatVersion = KingdomLifecycleRules.CurrentFormatVersion;
		public bool LegacyIdentity;
		public string LegacyMigrationKey;
		public bool Quarantined;
		public string Fault;
		public string SettlementId;
		public bool IdentityBound;
		public string IdentityProof;
		public long PlainGuestNextSequence = 1L;
		public long PlainGuestRetiredThrough;
		public long NotableGuestNextSequence = 1L;
		public long NotableGuestRetiredThrough;
		public long RaidNextSequence = 1L;
		public long RaidRetiredThrough;
		public long PetitionNextSequence = 1L;
		public long PetitionRetiredThrough;
		public KingdomLifecycleOptionState LocusOption;
		public long LocusOptionTick;
		public KingdomLifecycleOptionState NotableOption;
		public long NotableOptionTick;
		public KingdomLifecycleOptionState RaidOption;
		public long RaidOptionTick;
		public KingdomLifecycleOptionState PetitionOption;
		public long PetitionOptionTick;
		public KingdomLifecycleOperation PlainGuest;
		public KingdomLifecycleOperation NotableGuest;
		public KingdomLifecycleOperation Raid;
		public KingdomLifecycleOperation Petition;
		public List<KingdomLifecycleResourceRevision> Resources =
			new List<KingdomLifecycleResourceRevision>();
		public List<KingdomLifecycleProof> RecentProofs = new List<KingdomLifecycleProof>();
		public KingdomGrowthBook Growth = new KingdomGrowthBook();

		[NonSerialized]
		public bool WireRejected;

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			KingdomLifecycleWireCodec.WriteLifecycle(Writer, this);
		}

		public void Read(SerializationReader Reader)
		{
			try
			{
				KingdomLifecycleWireCodec.ReadLifecycle(Reader, this);
			}
			catch (Exception)
			{
				WireRejected = true;
				Quarantined = true;
				PlainGuest = null;
				NotableGuest = null;
				Raid = null;
				Petition = null;
				Resources = new List<KingdomLifecycleResourceRevision>();
				RecentProofs = new List<KingdomLifecycleProof>();
				throw;
			}
		}
#endif
	}

	[Serializable]
	public sealed class KingdomCarrySource
	{
		public string OperationId;
		public string SourceEventId;
		public string ObjectId;
		public string Blueprint;
		public KingdomLifecycleTopology Topology;
		public string OwnerId;
		public string ZoneId;
		public int X = -1;
		public int Y = -1;
		public int Material;
		public int OriginalCount;
		public int PlannedCount;
		public int Removed;
		public int UnitCursor;
		public int UnitBefore;
		public int UnitAfter;
		public string UnitEventId;
		public KingdomLifecyclePhysicalState UnitState;
		public string ReceiptId;
		public string ReceiptTopologyId;
		public int ReceiptBeforeIdMatches = -1;
		public int ReceiptAfterIdMatches = -1;
		public int ReceiptBeforeCount = -1;
		public int ReceiptAfterCount = -1;
		public bool ReceiptSameReference;
		public string ReceiptProofId;
		public string ReceiptChainId;
		public int ReceiptChainCount;
		public KingdomLifecyclePhysicalState ReceiptState;
		public KingdomLifecyclePhysicalState State;

		[NonSerialized]
		internal object LiveAuthority;
	}

	[Serializable]
	public sealed class KingdomCarryOperation
	{
		public long Sequence;
		public string Id;
		public string PlanHash;
		public KingdomLifecyclePhase Phase;
		public long CreatedTick;
		public long UpdatedTick;
		public string OriginSettlementId;
		public string OriginZoneId;
		public int OriginX;
		public int OriginY;
		public string DestinationSettlementId;
		public string DestinationSettlementName;
		public List<string> SettlementIds = new List<string>();
		public string RealmTopologyHash;
		public string DestinationZoneId;
		public KingdomLifecycleTopology DestinationTopology;
		public string DestinationOwnerId;
		public int DestinationX = -1;
		public int DestinationY = -1;
		public long DueTick;
		public bool RiskFrozen;
		public bool LostOnRoad;
		public int SourceIndex;
		public int OutputIndex;
		public KingdomLifecycleResourceLease ScheduleLease;
		public string ScheduleReceiptId;
		public string ScheduleTopologyId;
		public int ScheduleBeforeMatches = -1;
		public int ScheduleAfterMatches = -1;
		public bool ScheduleSameReference;
		public string ScheduleProofId;
		public KingdomLifecyclePhysicalState ScheduleReceiptState;
		public List<KingdomCarrySource> Sources = new List<KingdomCarrySource>();
		public List<KingdomLifecycleProjection> Outputs = new List<KingdomLifecycleProjection>();
		public int Mud;
		public int Brush;
		public int Timber;
		public int Stone;
		public int Marble;
		public int Scrap;
		public int EscrowMud;
		public int EscrowBrush;
		public int EscrowTimber;
		public int EscrowStone;
		public int EscrowMarble;
		public int EscrowScrap;
		public int DeliveredMud;
		public int DeliveredBrush;
		public int DeliveredTimber;
		public int DeliveredStone;
		public int DeliveredMarble;
		public int DeliveredScrap;
		public int LostMud;
		public int LostBrush;
		public int LostTimber;
		public int LostStone;
		public int LostMarble;
		public int LostScrap;
		public KingdomLifecycleOutbox Outbox;
		public string Fault;

		[NonSerialized]
		public object LiveAuthority;
	}

	[Serializable]
	public sealed class KingdomCarryBook
#if !TAF_TESTS
		: IComposite
#endif
	{
		public int FormatVersion = KingdomLifecycleRules.CurrentCarryFormatVersion;
		public bool LegacyIdentity;
		public string LegacyMigrationKey;
		public bool Quarantined;
		public string Fault;
		public string RealmId;
		public List<string> SettlementIds = new List<string>();
		public bool IdentityBound;
		public string IdentityProof;
		public long NextSequence = 1L;
		public long RetiredThrough;
		public KingdomCarryOperation Open;
		public List<KingdomLifecycleResourceRevision> Resources =
			new List<KingdomLifecycleResourceRevision>();
		public List<KingdomLifecycleProof> RecentProofs = new List<KingdomLifecycleProof>();

		[NonSerialized]
		public bool WireRejected;

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			KingdomLifecycleWireCodec.WriteCarry(Writer, this);
		}

		public void Read(SerializationReader Reader)
		{
			try
			{
				KingdomLifecycleWireCodec.ReadCarry(Reader, this);
			}
			catch (Exception)
			{
				WireRejected = true;
				Quarantined = true;
				Open = null;
				SettlementIds = new List<string>();
				Resources = new List<KingdomLifecycleResourceRevision>();
				RecentProofs = new List<KingdomLifecycleProof>();
				throw;
			}
		}
#endif
	}

	/// <summary>
	/// Version-first, manually bounded codec. It validates list counts and UTF-8 byte lengths
	/// before allocating. Only the two owning books are engine composites; nested rows cannot be
	/// independently reflection-deserialized into oversized lists.
	/// </summary>
	public static class KingdomLifecycleWireCodec
	{
		public const int LifecycleMagic = 0x544C4332; // TLC2
		public const int CarryMagic = 0x54434332; // TCC2
		public const int GrowthMagic = 0x54475231; // TGR1
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		private sealed class GrowthCappedWriteStream : Stream
		{
			private readonly MemoryStream Inner = new MemoryStream();
			private readonly long Maximum;

			public GrowthCappedWriteStream(long maximum)
			{
				if (maximum < 0L) throw new ArgumentOutOfRangeException(nameof(maximum));
				Maximum = maximum;
			}

			public byte[] ToArray() { return Inner.ToArray(); }

			private void RequireCapacity(long count)
			{
				if (count < 0L || Position > Maximum - count)
					throw new InvalidDataException(
						"growth aggregate cap reached before write allocation");
			}

			public override bool CanRead => true;
			public override bool CanSeek => true;
			public override bool CanWrite => true;
			public override long Length => Inner.Length;
			public override long Position
			{
				get { return Inner.Position; }
				set
				{
					if (value < 0L || value > Maximum)
						throw new InvalidDataException("growth stream position exceeds cap");
					Inner.Position = value;
				}
			}

			public override void Flush() { Inner.Flush(); }
			public override int Read(byte[] buffer, int offset, int count)
			{
				return Inner.Read(buffer, offset, count);
			}
			public override long Seek(long offset, SeekOrigin origin)
			{
				long target;
				switch (origin)
				{
				case SeekOrigin.Begin: target = offset; break;
				case SeekOrigin.Current: target = Position + offset; break;
				case SeekOrigin.End: target = Length + offset; break;
				default: throw new ArgumentOutOfRangeException(nameof(origin));
				}
				Position = target;
				return target;
			}
			public override void SetLength(long value)
			{
				if (value < 0L || value > Maximum)
					throw new InvalidDataException("growth stream length exceeds cap");
				Inner.SetLength(value);
			}
			public override void Write(byte[] buffer, int offset, int count)
			{
				RequireCapacity(count); Inner.Write(buffer, offset, count);
			}
			public override void WriteByte(byte value)
			{
				RequireCapacity(1L); Inner.WriteByte(value);
			}
			protected override void Dispose(bool disposing)
			{
				if (disposing) Inner.Dispose();
				base.Dispose(disposing);
			}
		}

		public static void WriteLifecycle(BinaryWriter Writer, KingdomLifecycleBook Book)
		{
			WriteLifecycleCore(Writer, Book, KingdomLifecycleRules.CurrentFormatVersion,
				IncludeGrowth: true);
		}

		#if TAF_TESTS
		internal static void WriteLifecycleV5Fixture(BinaryWriter Writer,
			KingdomLifecycleBook Book)
		{
			WriteLifecycleCore(Writer, Book, KingdomLifecycleRules.LegacyLifecycleFormatVersion,
				IncludeGrowth: false);
		}
		#endif

		private static void WriteLifecycleCore(BinaryWriter Writer, KingdomLifecycleBook Book,
			int WireVersion, bool IncludeGrowth)
		{
			if (Writer == null || Book == null || Book.WireRejected
				|| Book.FormatVersion != KingdomLifecycleRules.CurrentFormatVersion)
				throw new InvalidDataException("lifecycle authority is not writable");
			EnsureCount(Book.Resources, KingdomLifecycleRules.MaxResourceRows, "resource rows");
			EnsureCount(Book.RecentProofs, KingdomLifecycleRules.MaxRecentProofs, "proof rows");
			EnsureOuterResourceKinds(Book.Resources, Book.PlainGuest, Book.NotableGuest,
				Book.Raid, Book.Petition);
			Writer.Write(LifecycleMagic);
			Writer.Write(WireVersion);
			Writer.Write(Book.LegacyIdentity);
			WriteString(Writer, Book.LegacyMigrationKey, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.Quarantined);
			WriteString(Writer, Book.Fault, KingdomLifecycleRules.MaxTextBytes);
			WriteString(Writer, Book.SettlementId, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.IdentityBound);
			WriteString(Writer, Book.IdentityProof, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.PlainGuestNextSequence);
			Writer.Write(Book.PlainGuestRetiredThrough);
			Writer.Write(Book.NotableGuestNextSequence);
			Writer.Write(Book.NotableGuestRetiredThrough);
			Writer.Write(Book.RaidNextSequence);
			Writer.Write(Book.RaidRetiredThrough);
			Writer.Write(Book.PetitionNextSequence);
			Writer.Write(Book.PetitionRetiredThrough);
			WriteOption(Writer, Book.LocusOption, Book.LocusOptionTick);
			WriteOption(Writer, Book.NotableOption, Book.NotableOptionTick);
			WriteOption(Writer, Book.RaidOption, Book.RaidOptionTick);
			WriteOption(Writer, Book.PetitionOption, Book.PetitionOptionTick);
			WriteOperation(Writer, Book.PlainGuest);
			WriteOperation(Writer, Book.NotableGuest);
			WriteOperation(Writer, Book.Raid);
			WriteOperation(Writer, Book.Petition);
			Writer.Write(Book.Resources.Count);
			for (int i = 0; i < Book.Resources.Count; i++) WriteResource(Writer, Book.Resources[i]);
			Writer.Write(Book.RecentProofs.Count);
			for (int i = 0; i < Book.RecentProofs.Count; i++) WriteProof(Writer, Book.RecentProofs[i]);
			if (IncludeGrowth)
			{
				byte[] payload = GrowthPayloadForWrite(Book.Growth);
				Writer.Write(payload.Length);
				Writer.Write(payload);
			}
		}

		public static void ReadLifecycle(BinaryReader Reader, KingdomLifecycleBook Target)
		{
			ReadLifecycle(Reader, Target, null);
		}

		public static void ReadLifecycle(BinaryReader Reader, KingdomLifecycleBook Target,
			KingdomGrowthMigrationInput Migration)
		{
			if (Reader == null || Target == null) throw new ArgumentNullException();
			try
			{
				if (Reader.ReadInt32() != LifecycleMagic) Reject(Target, "invalid lifecycle framing");
				int version = Reader.ReadInt32();
				Target.FormatVersion = version;
				if (version != KingdomLifecycleRules.CurrentFormatVersion &&
					version != KingdomLifecycleRules.LegacyLifecycleFormatVersion)
					Reject(Target, "unsupported lifecycle version");
				KingdomLifecycleBook value = new KingdomLifecycleBook();
				value.FormatVersion = version;
				value.LegacyIdentity = ReadExactBoolean(Reader);
				value.LegacyMigrationKey = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				value.Quarantined = ReadExactBoolean(Reader);
				value.Fault = ReadString(Reader, KingdomLifecycleRules.MaxTextBytes);
				value.SettlementId = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				value.IdentityBound = ReadExactBoolean(Reader);
				value.IdentityProof = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				value.PlainGuestNextSequence = Reader.ReadInt64();
				value.PlainGuestRetiredThrough = Reader.ReadInt64();
				value.NotableGuestNextSequence = Reader.ReadInt64();
				value.NotableGuestRetiredThrough = Reader.ReadInt64();
				value.RaidNextSequence = Reader.ReadInt64();
				value.RaidRetiredThrough = Reader.ReadInt64();
				value.PetitionNextSequence = Reader.ReadInt64();
				value.PetitionRetiredThrough = Reader.ReadInt64();
				ReadOption(Reader, out value.LocusOption, out value.LocusOptionTick);
				ReadOption(Reader, out value.NotableOption, out value.NotableOptionTick);
				ReadOption(Reader, out value.RaidOption, out value.RaidOptionTick);
				ReadOption(Reader, out value.PetitionOption, out value.PetitionOptionTick);
				bool legacyWire = version == KingdomLifecycleRules.LegacyLifecycleFormatVersion;
				value.PlainGuest = ReadOperation(Reader, legacyWire);
				value.NotableGuest = ReadOperation(Reader, legacyWire);
				value.Raid = ReadOperation(Reader, legacyWire);
				value.Petition = ReadOperation(Reader, legacyWire);
				int resources = ReadCount(Reader, KingdomLifecycleRules.MaxResourceRows);
				value.Resources = new List<KingdomLifecycleResourceRevision>(resources);
				for (int i = 0; i < resources; i++)
					value.Resources.Add(ReadResource(Reader, legacyWire));
				int proofs = ReadCount(Reader, KingdomLifecycleRules.MaxRecentProofs);
				value.RecentProofs = new List<KingdomLifecycleProof>(proofs);
				for (int i = 0; i < proofs; i++) value.RecentProofs.Add(ReadProof(Reader));
				if (version == KingdomLifecycleRules.LegacyLifecycleFormatVersion)
				{
					if (!KingdomLifecycleRules.TryStageGrowthMigrationFromV5(value,
						out KingdomGrowthBook staged))
						throw new InvalidDataException("legacy lifecycle v5 graph is malformed");
					value.FormatVersion = KingdomLifecycleRules.CurrentFormatVersion;
					value.Growth = staged;
					if (Migration != null && staged.MigrationPending)
					{
						KingdomGrowthMigrationResult migrated =
							KingdomLifecycleRules.ApplyGrowthMigration(value, Migration);
						if (!migrated.Valid ||
							!KingdomLifecycleRules.TryPublishGrowthMigration(value, migrated))
							throw new InvalidDataException(migrated.Failure);
					}
				}
				else value.Growth = ReadGrowthSection(Reader);
				KingdomLifecycleRules.Normalize(value);
				Copy(value, Target);
			}
			catch (Exception)
			{
				Poison(Target, "malformed lifecycle wire was rejected");
				throw;
			}
		}

		public static byte[] GrowthPayloadForWrite(KingdomGrowthBook Book)
		{
			if (Book == null) throw new InvalidDataException("growth authority is absent");
			if (Book.OpaquePayload != null)
			{
				if (!KingdomLifecycleRules.GrowthEnvelopeWritable(Book))
					throw new InvalidDataException("opaque growth envelope is malformed");
				return (byte[])Book.OpaquePayload.Clone();
			}
			if (!KingdomLifecycleRules.GrowthEnvelopeWritable(Book))
				throw new InvalidDataException("growth envelope is not bounded and writable");
			using (GrowthCappedWriteStream stream =
				new GrowthCappedWriteStream(KingdomLifecycleRules.MaxGrowthSectionBytes))
			using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
			{
				WriteGrowth(writer, Book);
				writer.Flush();
				return stream.ToArray();
			}
		}

		internal static bool GrowthPayloadFitsAggregateCap(KingdomGrowthBook Book)
		{
			if (Book == null || Book.OpaquePayload != null) return false;
			try
			{
				using (GrowthCappedWriteStream stream =
					new GrowthCappedWriteStream(KingdomLifecycleRules.MaxGrowthSectionBytes))
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					WriteGrowth(writer, Book); writer.Flush();
					return stream.Length <= KingdomLifecycleRules.MaxGrowthSectionBytes;
				}
			}
			catch (Exception ex) when (ex is InvalidDataException || ex is IOException
				|| ex is EncoderFallbackException || ex is ArgumentException)
			{
				return false;
			}
		}

		internal static bool OpaqueGrowthEnvelopeWritable(KingdomGrowthBook Book)
		{
			if (Book == null || !Book.Quarantined || Book.OpaquePayload == null
				|| string.IsNullOrEmpty(Book.Fault)
				|| Book.OpaquePayload.Length > KingdomLifecycleRules.MaxGrowthSectionBytes) return false;
			try
			{
				KingdomGrowthBook derived = ReadGrowthPayload(Book.OpaquePayload);
				if (derived.OpaquePayload == null
					|| derived.OpaqueWireVersion != Book.OpaqueWireVersion
					|| !string.Equals(derived.Fault, Book.Fault, StringComparison.Ordinal)
					|| derived.OpaquePayload.Length != Book.OpaquePayload.Length) return false;
				for (int i = 0; i < Book.OpaquePayload.Length; i++)
					if (derived.OpaquePayload[i] != Book.OpaquePayload[i]) return false;
				return KingdomLifecycleRules.OpaqueGrowthParsedStateIsPristine(Book);
			}
			catch (Exception) { return false; }
		}

		public static KingdomGrowthBook ReadGrowthPayload(byte[] Payload)
		{
			if (Payload == null || Payload.Length > KingdomLifecycleRules.MaxGrowthSectionBytes)
				throw new InvalidDataException("growth payload framing is malformed");
			if (Payload.Length < 8)
				return OpaqueGrowth(Payload, 0, "growth payload is too short");
			int headerVersion = 0;
			bool hasHeaderVersion = false;
			try
			{
				using (MemoryStream stream = new MemoryStream(Payload, false))
				using (BinaryReader reader = new BinaryReader(stream, StrictUtf8, true))
				{
					if (reader.ReadInt32() != GrowthMagic)
						return OpaqueGrowth(Payload, 0, "growth payload marker is malformed");
					int version = reader.ReadInt32();
					headerVersion = version;
					hasHeaderVersion = true;
					if (version > KingdomLifecycleRules.CurrentGrowthFormatVersion)
						return OpaqueGrowth(Payload, version,
							"future growth payload preserved as opaque evidence");
					if (version != KingdomLifecycleRules.CurrentGrowthFormatVersion)
						return OpaqueGrowth(Payload, version,
							"growth payload version is unsupported");
					KingdomGrowthBook value = ReadGrowth(reader);
					if (stream.Position != stream.Length)
						return OpaqueGrowth(Payload, version,
							"growth payload has trailing bytes");
					if (!KingdomLifecycleRules.GrowthEnvelopeWritable(value))
						return OpaqueGrowth(Payload, version,
							"malformed current growth payload preserved as opaque evidence");
					return value;
				}
			}
			catch (Exception ex)
			{
				return OpaqueGrowth(Payload, hasHeaderVersion ? headerVersion : 0,
					"malformed growth payload: " + BoundFault(ex.Message));
			}
		}

		private static KingdomGrowthBook ReadGrowthSection(BinaryReader Reader)
		{
			int length = Reader.ReadInt32();
			if (length < 0 || length > KingdomLifecycleRules.MaxGrowthSectionBytes)
				throw new InvalidDataException("growth section length exceeds framing bounds");
			byte[] payload = Reader.ReadBytes(length);
			if (payload.Length != length)
				throw new EndOfStreamException("growth section is truncated");
			return ReadGrowthPayload(payload);
		}

		private static void WriteGrowth(BinaryWriter w, KingdomGrowthBook b)
		{
			EnsureCount(b.FieldOps, KingdomLifecycleRules.MaxGrowthFields, "growth field slots");
			EnsureCount(b.CropRows, KingdomLifecycleRules.MaxGrowthCropRows, "growth crop rows");
			EnsureCount(b.Resources, KingdomLifecycleRules.MaxResourceRows, "growth resources");
			EnsureCount(b.RecentProofs, KingdomLifecycleRules.MaxRecentProofs, "growth proofs");
			w.Write(GrowthMagic); w.Write(KingdomLifecycleRules.CurrentGrowthFormatVersion);
			w.Write(b.Quarantined); S(w, b.Fault, false, true);
			S(w, b.SettlementId, true); w.Write(b.IdentityBound); S(w, b.IdentityProof, true);
			w.Write(b.MigratedFromLifecycleVersion); w.Write(b.MigrationPending);
			w.Write(b.MigrationTick);
			w.Write((byte)b.OptionState); w.Write(b.OptionTick);
			w.Write((byte)b.HealthState); w.Write(b.HealthTick);
			w.Write((byte)b.ScarcityOptionState); w.Write(b.ScarcityOptionTick);
			w.Write(b.WorkPaused);
			w.Write(b.WorkPauseStartedTick); w.Write(b.WorkPausedTicks); w.Write(b.EffectiveWorkTick);
			w.Write(b.LastHeartbeatTick); w.Write(b.NextArrivalTick);
			w.Write(b.ArrivalIntervalTicks); w.Write(b.LastFetchTick);
			w.Write(b.LastMillTick); w.Write(b.LastSubsidenceTick);
			w.Write(b.LastDeliveryTick); w.Write(b.LastDepartureTick); w.Write(b.PendingCrop);
			S(w, b.PendingCropBlueprint, false); S(w, b.PendingCropZoneId, false);
			w.Write(b.HeartbeatNextSequence); w.Write(b.HeartbeatRetiredThrough);
			w.Write(b.ArrivalNextSequence); w.Write(b.ArrivalRetiredThrough);
			w.Write(b.DepartureNextSequence); w.Write(b.DepartureRetiredThrough);
			w.Write(b.DeliveryNextSequence); w.Write(b.DeliveryRetiredThrough);
			w.Write(b.FetchNextSequence); w.Write(b.FetchRetiredThrough);
			w.Write(b.MillNextSequence); w.Write(b.MillRetiredThrough);
			w.Write(b.ArrivalCandidateNextSequence); w.Write(b.ArrivalCandidateRetiredThrough);
			WriteGrowthOperation(w, b.HeartbeatOp); WriteGrowthOperation(w, b.ArrivalOp);
			WriteGrowthOperation(w, b.DepartureOp); WriteGrowthOperation(w, b.DeliveryOp);
			WriteGrowthOperation(w, b.FetchOp); WriteGrowthOperation(w, b.MillOp);
			WriteGrowthArrivalCandidate(w, b.ArrivalCandidate);
			w.Write(b.FieldOps.Count);
			for (int i = 0; i < b.FieldOps.Count; i++) WriteGrowthField(w, b.FieldOps[i]);
			w.Write(b.CropRows.Count);
			for (int i = 0; i < b.CropRows.Count; i++) WriteCropRow(w, b.CropRows[i]);
			w.Write(b.Resources.Count);
			for (int i = 0; i < b.Resources.Count; i++) WriteResource(w, b.Resources[i]);
			w.Write(b.RecentProofs.Count);
			for (int i = 0; i < b.RecentProofs.Count; i++) WriteGrowthProof(w, b.RecentProofs[i]);
		}

		private static KingdomGrowthBook ReadGrowth(BinaryReader r)
		{
			KingdomGrowthBook b = new KingdomGrowthBook
			{
				FormatVersion = KingdomLifecycleRules.CurrentGrowthFormatVersion,
				Quarantined = ReadExactBoolean(r), Fault = S(r, false, true),
				SettlementId = S(r, true), IdentityBound = ReadExactBoolean(r),
				IdentityProof = S(r, true), MigratedFromLifecycleVersion = r.ReadInt32(),
				MigrationPending = ReadExactBoolean(r), MigrationTick = r.ReadInt64(),
				OptionState = (KingdomLifecycleOptionState)r.ReadByte(),
				OptionTick = r.ReadInt64(), HealthState = (KingdomGrowthHealthState)r.ReadByte(),
				HealthTick = r.ReadInt64(),
				ScarcityOptionState = (KingdomLifecycleOptionState)r.ReadByte(),
				ScarcityOptionTick = r.ReadInt64(), WorkPaused = ReadExactBoolean(r),
				WorkPauseStartedTick = r.ReadInt64(), WorkPausedTicks = r.ReadInt64(),
				EffectiveWorkTick = r.ReadInt64(),
				LastHeartbeatTick = r.ReadInt64(), NextArrivalTick = r.ReadInt64(),
				ArrivalIntervalTicks = r.ReadInt64(), LastFetchTick = r.ReadInt64(),
				LastMillTick = r.ReadInt64(),
				LastSubsidenceTick = r.ReadInt64(), LastDeliveryTick = r.ReadInt64(),
				LastDepartureTick = r.ReadInt64(), PendingCrop = r.ReadInt32(),
				PendingCropBlueprint = S(r, false), PendingCropZoneId = S(r, false),
				HeartbeatNextSequence = r.ReadInt64(), HeartbeatRetiredThrough = r.ReadInt64(),
				ArrivalNextSequence = r.ReadInt64(), ArrivalRetiredThrough = r.ReadInt64(),
				DepartureNextSequence = r.ReadInt64(), DepartureRetiredThrough = r.ReadInt64(),
				DeliveryNextSequence = r.ReadInt64(), DeliveryRetiredThrough = r.ReadInt64(),
				FetchNextSequence = r.ReadInt64(), FetchRetiredThrough = r.ReadInt64(),
				MillNextSequence = r.ReadInt64(), MillRetiredThrough = r.ReadInt64(),
				ArrivalCandidateNextSequence = r.ReadInt64(),
				ArrivalCandidateRetiredThrough = r.ReadInt64(),
				HeartbeatOp = ReadGrowthOperation(r), ArrivalOp = ReadGrowthOperation(r),
				DepartureOp = ReadGrowthOperation(r), DeliveryOp = ReadGrowthOperation(r),
				FetchOp = ReadGrowthOperation(r), MillOp = ReadGrowthOperation(r),
				ArrivalCandidate = ReadGrowthArrivalCandidate(r)
			};
			int fields = ReadCount(r, KingdomLifecycleRules.MaxGrowthFields);
			b.FieldOps = new List<KingdomGrowthFieldSlot>(fields);
			for (int i = 0; i < fields; i++) b.FieldOps.Add(ReadGrowthField(r));
			int crops = ReadCount(r, KingdomLifecycleRules.MaxGrowthCropRows);
			b.CropRows = new List<KingdomGrowthCropRow>(crops);
			for (int i = 0; i < crops; i++) b.CropRows.Add(ReadCropRow(r));
			int resources = ReadCount(r, KingdomLifecycleRules.MaxResourceRows);
			b.Resources = new List<KingdomLifecycleResourceRevision>(resources);
			for (int i = 0; i < resources; i++) b.Resources.Add(ReadResource(r));
			int proofs = ReadCount(r, KingdomLifecycleRules.MaxRecentProofs);
			b.RecentProofs = new List<KingdomGrowthProof>(proofs);
			for (int i = 0; i < proofs; i++) b.RecentProofs.Add(ReadGrowthProof(r));
			return b;
		}

		private static void WriteGrowthOperation(BinaryWriter w, KingdomGrowthOperation o)
		{
			w.Write(o != null); if (o == null) return;
			EnsureCount(o.WaterLegs, KingdomLifecycleRules.MaxWaterLegs, "growth water legs");
			EnsureCount(o.Sources, KingdomLifecycleRules.MaxGrowthSources, "growth sources");
			EnsureCount(o.Outputs, KingdomLifecycleRules.MaxGrowthOutputs, "growth outputs");
			EnsureCount(o.DomainSteps, KingdomLifecycleRules.MaxResourceLeases,
				"growth domain leases");
			EnsureCount(o.OutboxEvents, KingdomLifecycleRules.MaxGrowthOutboxEvents,
				"growth outbox events");
			w.Write(o.Sequence); S(w, o.Id, true); S(w, o.PlanHash, true);
			w.Write((byte)o.Action); w.Write((byte)o.Phase); w.Write(o.CreatedTick);
			w.Write(o.UpdatedTick); S(w, o.SettlementId, true); S(w, o.FieldId, true);
			S(w, o.ZoneId, false); S(w, o.TargetId, true); S(w, o.TargetMarker, true);
			S(w, o.Blueprint, false); w.Write((byte)o.TargetTopology);
			w.Write((byte)o.TargetLocation);
			S(w, o.TargetOwnerId, true); w.Write(o.TargetX); w.Write(o.TargetY);
			w.Write((byte)o.OptionState); w.Write(o.OptionTick); w.Write((byte)o.HealthState);
			w.Write(o.HealthTick); w.Write(o.EffectiveWorkBefore); w.Write(o.EffectiveWorkAfter);
			w.Write(o.FieldClockBefore); w.Write(o.FieldClockAfter);
			w.Write(o.HeartbeatBefore); w.Write(o.HeartbeatAfter); w.Write(o.ArrivalBefore);
			w.Write(o.ArrivalAfter); w.Write(o.FetchBefore); w.Write(o.FetchAfter);
			w.Write(o.MillBefore); w.Write(o.MillAfter); S(w, o.MillCropBlueprint, false);
			S(w, o.MillStapleBlueprint, false); w.Write(o.SubsidenceBefore);
			w.Write(o.SubsidenceAfter); w.Write(o.DeliveryBefore); w.Write(o.DeliveryAfter);
			w.Write(o.DepartureBefore); w.Write(o.DepartureAfter);
			w.Write((byte)o.ArrivalDisposition); S(w, o.ArrivalCandidateId, true);
			w.Write((byte)o.DeliveryMode); w.Write((byte)o.DepartureCauseKind);
			S(w, o.DepartureCause, false); S(w, o.DepartureNote, false, true);
			S(w, o.DepartureName, false); S(w, o.DepartureOrigin, false);
			w.Write(o.DepartureArrivedTick); S(w, o.DepartureCreed, false);
			w.Write(o.DepartureChronicled); S(w, o.TriggeredByOperationId, true);
			w.Write((byte)o.ScarcityOptionState); w.Write(o.ScarcityOptionTick);
			w.Write(o.PendingCropBefore);
			w.Write(o.PendingCropDelta); w.Write(o.PendingCropAfter);
			S(w, o.PendingCropBlueprintBefore, false); S(w, o.PendingCropZoneIdBefore, false);
			S(w, o.PendingCropBlueprintAfter, false); S(w, o.PendingCropZoneIdAfter, false);
			w.Write(o.PopulationBefore); w.Write(o.PopulationDelta); w.Write(o.PopulationAfter);
			w.Write(o.HarvestStandingRows); w.Write(o.HarvestRipeRows);
			w.Write(o.HarvestCycles); w.Write(o.HarvestCountsRipeLast);
			w.Write(o.HarvestEffectivenessPercent); w.Write(o.HarvestMethodPercent);
			w.Write(o.HarvestFirstOrdinal);
			S(w, o.HarvestCropBlueprint, false); S(w, o.HarvestSeedBlueprint, false);
			w.Write(o.WaterCursor); w.Write(o.WaterLegs.Count);
			for (int i = 0; i < o.WaterLegs.Count; i++) WriteGrowthWater(w, o.WaterLegs[i]);
			w.Write(o.SourceCursor); w.Write(o.Sources.Count);
			for (int i = 0; i < o.Sources.Count; i++) WriteGrowthObject(w, o.Sources[i]);
			w.Write(o.OutputCursor); w.Write(o.Outputs.Count);
			for (int i = 0; i < o.Outputs.Count; i++) WriteGrowthObject(w, o.Outputs[i]);
			w.Write(o.DomainCursor); w.Write(o.DomainSteps.Count);
			for (int i = 0; i < o.DomainSteps.Count; i++)
			{
				if (o.DomainSteps[i] == null)
					throw new InvalidDataException("null growth domain step");
				KingdomGrowthDomainStep d = o.DomainSteps[i];
				w.Write((byte)d.Kind); w.Write((byte)d.CallbackKind);
				S(w, d.CallbackBodyHash, true); S(w, d.EventId, true); S(w, d.ActorId, true);
				S(w, d.SubjectId, true); w.Write(d.BeforeValue); w.Write(d.AfterValue);
				S(w, d.BeforeGraphHash, true); S(w, d.AfterGraphHash, true);
				S(w, d.BeforeMapHash, true); S(w, d.AfterMapHash, true);
				w.Write((byte)d.State); S(w, d.ReceiptId, true);
				w.Write(d.ReceiptBeforeValue); w.Write(d.ReceiptAfterValue);
				S(w, d.ReceiptBeforeGraphHash, true); S(w, d.ReceiptAfterGraphHash, true);
				S(w, d.ReceiptBeforeMapHash, true); S(w, d.ReceiptAfterMapHash, true);
				S(w, d.ReceiptProofId, true); w.Write((byte)d.ReceiptState);
				WriteLease(w, d.Lease); WriteGrowthScarcity(w, d.ScarcityBefore);
				WriteGrowthScarcity(w, d.ScarcityAfter);
				WriteGrowthAccounting(w, d.AccountingBefore);
				WriteGrowthAccounting(w, d.AccountingAfter);
				WriteGrowthFieldState(w, d.FieldBefore);
				WriteGrowthFieldState(w, d.FieldAfter);
				WriteGrowthCropRows(w, d.CropRowsBefore);
				WriteGrowthCropRows(w, d.CropRowsDeclaredAfter);
				WriteGrowthCropRows(w, d.CropRowsAfter);
			}
			WriteLease(w, o.ClockLease);
			w.Write((byte)o.ClockState);
			w.Write(o.OutboxEvents.Count);
			for (int i = 0; i < o.OutboxEvents.Count; i++) WriteGrowthOutboxEvent(w,
				o.OutboxEvents[i]);
			S(w, o.Fault, false, true);
		}

		private static KingdomGrowthOperation ReadGrowthOperation(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			KingdomGrowthOperation o = new KingdomGrowthOperation
			{
				Sequence = r.ReadInt64(), Id = S(r, true), PlanHash = S(r, true),
				Action = (KingdomGrowthAction)r.ReadByte(), Phase = (KingdomGrowthPhase)r.ReadByte(),
				CreatedTick = r.ReadInt64(), UpdatedTick = r.ReadInt64(), SettlementId = S(r, true),
				FieldId = S(r, true), ZoneId = S(r, false), TargetId = S(r, true),
				TargetMarker = S(r, true), Blueprint = S(r, false),
				TargetTopology = (KingdomLifecycleTopology)r.ReadByte(),
				TargetLocation = (KingdomGrowthLocationKind)r.ReadByte(), TargetOwnerId = S(r, true),
				TargetX = r.ReadInt32(), TargetY = r.ReadInt32(),
				OptionState = (KingdomLifecycleOptionState)r.ReadByte(), OptionTick = r.ReadInt64(),
				HealthState = (KingdomGrowthHealthState)r.ReadByte(), HealthTick = r.ReadInt64(),
				EffectiveWorkBefore = r.ReadInt64(), EffectiveWorkAfter = r.ReadInt64(),
				FieldClockBefore = r.ReadInt64(), FieldClockAfter = r.ReadInt64(),
				HeartbeatBefore = r.ReadInt64(), HeartbeatAfter = r.ReadInt64(),
				ArrivalBefore = r.ReadInt64(), ArrivalAfter = r.ReadInt64(),
				FetchBefore = r.ReadInt64(), FetchAfter = r.ReadInt64(),
				MillBefore = r.ReadInt64(), MillAfter = r.ReadInt64(),
				MillCropBlueprint = S(r, false), MillStapleBlueprint = S(r, false),
				SubsidenceBefore = r.ReadInt64(), SubsidenceAfter = r.ReadInt64(),
				DeliveryBefore = r.ReadInt64(), DeliveryAfter = r.ReadInt64(),
				DepartureBefore = r.ReadInt64(), DepartureAfter = r.ReadInt64(),
				ArrivalDisposition = (KingdomGrowthArrivalDisposition)r.ReadByte(),
				ArrivalCandidateId = S(r, true),
				DeliveryMode = (KingdomGrowthDeliveryMode)r.ReadByte(),
				DepartureCauseKind = (KingdomGrowthDepartureCauseKind)r.ReadByte(),
				DepartureCause = S(r, false), DepartureNote = S(r, false, true),
				DepartureName = S(r, false), DepartureOrigin = S(r, false),
				DepartureArrivedTick = r.ReadInt64(), DepartureCreed = S(r, false),
				DepartureChronicled = ReadExactBoolean(r), TriggeredByOperationId = S(r, true),
				ScarcityOptionState = (KingdomLifecycleOptionState)r.ReadByte(),
				ScarcityOptionTick = r.ReadInt64(),
				PendingCropBefore = r.ReadInt32(), PendingCropDelta = r.ReadInt32(),
				PendingCropAfter = r.ReadInt32(), PendingCropBlueprintBefore = S(r, false),
				PendingCropZoneIdBefore = S(r, false), PendingCropBlueprintAfter = S(r, false),
				PendingCropZoneIdAfter = S(r, false), PopulationBefore = r.ReadInt32(),
				PopulationDelta = r.ReadInt32(), PopulationAfter = r.ReadInt32(),
				HarvestStandingRows = r.ReadInt32(), HarvestRipeRows = r.ReadInt32(),
				HarvestCycles = r.ReadInt32(), HarvestCountsRipeLast = ReadExactBoolean(r),
				HarvestEffectivenessPercent = r.ReadInt32(),
				HarvestMethodPercent = r.ReadInt32(),
				HarvestFirstOrdinal = r.ReadUInt64(),
				HarvestCropBlueprint = S(r, false), HarvestSeedBlueprint = S(r, false)
			};
			o.WaterCursor = r.ReadInt32();
			int water = ReadCount(r, KingdomLifecycleRules.MaxWaterLegs);
			o.WaterLegs = new List<KingdomGrowthWaterLeg>(water);
			for (int i = 0; i < water; i++) o.WaterLegs.Add(ReadGrowthWater(r));
			o.SourceCursor = r.ReadInt32();
			int sources = ReadCount(r, KingdomLifecycleRules.MaxGrowthSources);
			o.Sources = new List<KingdomGrowthObjectLeg>(sources);
			for (int i = 0; i < sources; i++) o.Sources.Add(ReadGrowthObject(r));
			o.OutputCursor = r.ReadInt32();
			int outputs = ReadCount(r, KingdomLifecycleRules.MaxGrowthOutputs);
			o.Outputs = new List<KingdomGrowthObjectLeg>(outputs);
			for (int i = 0; i < outputs; i++) o.Outputs.Add(ReadGrowthObject(r));
			o.DomainCursor = r.ReadInt32();
			int leases = ReadCount(r, KingdomLifecycleRules.MaxResourceLeases);
			o.DomainSteps = new List<KingdomGrowthDomainStep>(leases);
			for (int i = 0; i < leases; i++) o.DomainSteps.Add(new KingdomGrowthDomainStep
			{
				Kind = (KingdomGrowthDomainStepKind)r.ReadByte(),
				CallbackKind = (KingdomGrowthDomainCallbackKind)r.ReadByte(),
				CallbackBodyHash = S(r, true), EventId = S(r, true),
				ActorId = S(r, true), SubjectId = S(r, true), BeforeValue = r.ReadInt64(),
				AfterValue = r.ReadInt64(), BeforeGraphHash = S(r, true),
				AfterGraphHash = S(r, true), BeforeMapHash = S(r, true),
				AfterMapHash = S(r, true),
				State = (KingdomLifecyclePhysicalState)r.ReadByte(), ReceiptId = S(r, true),
				ReceiptBeforeValue = r.ReadInt64(), ReceiptAfterValue = r.ReadInt64(),
				ReceiptBeforeGraphHash = S(r, true), ReceiptAfterGraphHash = S(r, true),
				ReceiptBeforeMapHash = S(r, true), ReceiptAfterMapHash = S(r, true),
				ReceiptProofId = S(r, true),
				ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte(), Lease = ReadLease(r),
				ScarcityBefore = ReadGrowthScarcity(r), ScarcityAfter = ReadGrowthScarcity(r),
				AccountingBefore = ReadGrowthAccounting(r),
				AccountingAfter = ReadGrowthAccounting(r),
				FieldBefore = ReadGrowthFieldState(r), FieldAfter = ReadGrowthFieldState(r),
				CropRowsBefore = ReadGrowthCropRows(r),
				CropRowsDeclaredAfter = ReadGrowthCropRows(r),
				CropRowsAfter = ReadGrowthCropRows(r)
			});
			o.ClockLease = ReadLease(r);
			o.ClockState = (KingdomLifecyclePhysicalState)r.ReadByte();
			int outbox = ReadCount(r, KingdomLifecycleRules.MaxGrowthOutboxEvents);
			o.OutboxEvents = new List<KingdomGrowthOutboxEvent>(outbox);
			for (int i = 0; i < outbox; i++) o.OutboxEvents.Add(ReadGrowthOutboxEvent(r));
			o.Fault = S(r, false, true); return o;
		}

		private static void WriteGrowthObject(BinaryWriter w, KingdomGrowthObjectLeg x)
		{
			if (x == null) throw new InvalidDataException("null growth object leg");
			S(w, x.OperationId, true); S(w, x.EventId, true); S(w, x.ObjectId, true);
			S(w, x.Marker, true); S(w, x.Blueprint, false); S(w, x.ZoneId, false);
			w.Write((byte)x.Topology); S(w, x.OwnerId, true); w.Write(x.X); w.Write(x.Y);
			w.Write(x.BeforeCount); w.Write(x.Delta); w.Write(x.AfterCount); w.Write(x.NoStack);
			w.Write((byte)x.MutationKind); S(w, x.BeforeOwnerGraphHash, true);
			S(w, x.AfterOwnerGraphHash, true); S(w, x.BeforeObjectGraphHash, true);
			S(w, x.AfterObjectGraphHash, true); S(w, x.BeforeTopologyHash, true);
			S(w, x.AfterTopologyHash, true); S(w, x.CreatedMarker, true);
			S(w, x.DetachedMarker, true);
			w.Write((byte)x.BeforeLocation);
			w.Write((byte)x.AfterLocation); S(w, x.EscrowKey, true);
			w.Write(x.CallbackCursor);
			EnsureCount(x.Callbacks, KingdomLifecycleRules.MaxGrowthObjectCallbacks,
				"growth object callbacks");
			w.Write(x.Callbacks.Count);
			for (int i = 0; i < x.Callbacks.Count; i++) WriteGrowthObjectCallback(w,
				x.Callbacks[i]);
			WriteLease(w, x.Lease);
			w.Write((byte)x.State); S(w, x.ReceiptId, false); S(w, x.ReceiptTopologyId, false);
			w.Write(x.ReceiptBeforeIdMatches); w.Write(x.ReceiptBeforeMarkerMatches);
			w.Write(x.ReceiptBeforeCount); w.Write(x.ReceiptAfterIdMatches);
			w.Write(x.ReceiptAfterMarkerMatches); w.Write(x.ReceiptAfterCount);
			S(w, x.ReceiptBeforeOwnerGraphHash, true);
			S(w, x.ReceiptAfterOwnerGraphHash, true); S(w, x.ReceiptBeforeObjectGraphHash, true);
			S(w, x.ReceiptAfterObjectGraphHash, true); S(w, x.ReceiptBeforeTopologyHash, true);
			S(w, x.ReceiptAfterTopologyHash, true); S(w, x.ReceiptCallbackObjectId, true);
			S(w, x.ReceiptCallbackMarker, true); S(w, x.ReceiptCallbackReferenceHash, true);
			w.Write(x.ReceiptSameReference); S(w, x.ReceiptProofId, false);
			w.Write((byte)x.ReceiptState);
		}

		private static KingdomGrowthObjectLeg ReadGrowthObject(BinaryReader r)
		{
			KingdomGrowthObjectLeg x = new KingdomGrowthObjectLeg
			{
				OperationId = S(r, true), EventId = S(r, true), ObjectId = S(r, true),
				Marker = S(r, true), Blueprint = S(r, false), ZoneId = S(r, false),
				Topology = (KingdomLifecycleTopology)r.ReadByte(), OwnerId = S(r, true),
				X = r.ReadInt32(), Y = r.ReadInt32(), BeforeCount = r.ReadInt32(),
				Delta = r.ReadInt32(), AfterCount = r.ReadInt32(), NoStack = ReadExactBoolean(r),
				MutationKind = (KingdomGrowthObjectMutationKind)r.ReadByte(),
				BeforeOwnerGraphHash = S(r, true), AfterOwnerGraphHash = S(r, true),
				BeforeObjectGraphHash = S(r, true), AfterObjectGraphHash = S(r, true),
				BeforeTopologyHash = S(r, true), AfterTopologyHash = S(r, true),
				CreatedMarker = S(r, true), DetachedMarker = S(r, true),
				BeforeLocation = (KingdomGrowthLocationKind)r.ReadByte(),
				AfterLocation = (KingdomGrowthLocationKind)r.ReadByte(), EscrowKey = S(r, true),
				CallbackCursor = r.ReadInt32()
			};
			int callbacks = ReadCount(r, KingdomLifecycleRules.MaxGrowthObjectCallbacks);
			x.Callbacks = new List<KingdomGrowthObjectCallbackStep>(callbacks);
			for (int i = 0; i < callbacks; i++) x.Callbacks.Add(ReadGrowthObjectCallback(r));
			x.Lease = ReadLease(r);
			x.State = (KingdomLifecyclePhysicalState)r.ReadByte(); x.ReceiptId = S(r, false);
			x.ReceiptTopologyId = S(r, false); x.ReceiptBeforeIdMatches = r.ReadInt32();
			x.ReceiptBeforeMarkerMatches = r.ReadInt32(); x.ReceiptBeforeCount = r.ReadInt32();
			x.ReceiptAfterIdMatches = r.ReadInt32(); x.ReceiptAfterMarkerMatches = r.ReadInt32();
			x.ReceiptAfterCount = r.ReadInt32();
			x.ReceiptBeforeOwnerGraphHash = S(r, true); x.ReceiptAfterOwnerGraphHash = S(r, true);
			x.ReceiptBeforeObjectGraphHash = S(r, true); x.ReceiptAfterObjectGraphHash = S(r, true);
			x.ReceiptBeforeTopologyHash = S(r, true); x.ReceiptAfterTopologyHash = S(r, true);
			x.ReceiptCallbackObjectId = S(r, true); x.ReceiptCallbackMarker = S(r, true);
			x.ReceiptCallbackReferenceHash = S(r, true);
			x.ReceiptSameReference = ReadExactBoolean(r); x.ReceiptProofId = S(r, false);
			x.ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte();
			return x;
		}

		private static void WriteGrowthObjectCallback(BinaryWriter w,
			KingdomGrowthObjectCallbackStep x)
		{
			if (x == null) throw new InvalidDataException("null growth object callback");
			S(w, x.EventId, true); w.Write((byte)x.Kind); w.Write((byte)x.FromLocation);
			w.Write((byte)x.ToLocation); S(w, x.EscrowKey, true); S(w, x.BeforeOwnerId, true);
			S(w, x.AfterOwnerId, true); S(w, x.BeforeZoneId, false); S(w, x.AfterZoneId, false);
			w.Write(x.BeforeX); w.Write(x.BeforeY); w.Write(x.AfterX); w.Write(x.AfterY);
			w.Write(x.BeforeCount);
			w.Write(x.AfterCount); w.Write(x.NoStack); w.Write(x.BeforeHasHarvestable);
			w.Write(x.AfterHasHarvestable); w.Write(x.BeforeRipe); w.Write(x.AfterRipe);
			w.Write(x.BeforeRegenTimer); w.Write(x.AfterRegenTimer);
			S(w, x.BeforeRegenTime, false); S(w, x.AfterRegenTime, false);
			w.Write(x.BeforeTileIndex); w.Write(x.AfterTileIndex);
			S(w, x.BeforeRenderTile, false); S(w, x.AfterRenderTile, false);
			S(w, x.BeforeRenderColor, false); S(w, x.AfterRenderColor, false);
			S(w, x.BeforeRenderDetail, false); S(w, x.AfterRenderDetail, false);
			S(w, x.BeforeRenderString, false); S(w, x.AfterRenderString, false);
			S(w, x.BeforeTileColor, false); S(w, x.AfterTileColor, false);
			S(w, x.BeforeOwnerGraphHash, true);
			S(w, x.AfterOwnerGraphHash, true); S(w, x.BeforeObjectGraphHash, true);
			S(w, x.AfterObjectGraphHash, true); S(w, x.BeforeTopologyHash, true);
			S(w, x.AfterTopologyHash, true); w.Write((byte)x.State); S(w, x.ReceiptId, true);
			w.Write(x.ReceiptBeforeMatches); w.Write(x.ReceiptAfterMatches);
			w.Write(x.ReceiptBeforeCount); w.Write(x.ReceiptAfterCount);
			S(w, x.ReceiptCallbackObjectId, true); S(w, x.ReceiptCallbackMarker, true);
			S(w, x.ReceiptCallbackReferenceHash, true); w.Write(x.ReceiptSameReference);
			S(w, x.ReceiptBeforeOwnerGraphHash, true); S(w, x.ReceiptAfterOwnerGraphHash, true);
			S(w, x.ReceiptBeforeObjectGraphHash, true); S(w, x.ReceiptAfterObjectGraphHash, true);
			S(w, x.ReceiptBeforeTopologyHash, true); S(w, x.ReceiptAfterTopologyHash, true);
			S(w, x.ReceiptProofId, true); w.Write((byte)x.ReceiptState);
		}

		private static KingdomGrowthObjectCallbackStep ReadGrowthObjectCallback(BinaryReader r)
		{
			return new KingdomGrowthObjectCallbackStep
			{
				EventId = S(r, true), Kind = (KingdomGrowthObjectMutationKind)r.ReadByte(),
				FromLocation = (KingdomGrowthLocationKind)r.ReadByte(),
				ToLocation = (KingdomGrowthLocationKind)r.ReadByte(), EscrowKey = S(r, true),
				BeforeOwnerId = S(r, true), AfterOwnerId = S(r, true),
				BeforeZoneId = S(r, false), AfterZoneId = S(r, false),
				BeforeX = r.ReadInt32(), BeforeY = r.ReadInt32(),
				AfterX = r.ReadInt32(), AfterY = r.ReadInt32(),
				BeforeCount = r.ReadInt32(), AfterCount = r.ReadInt32(),
				NoStack = ReadExactBoolean(r), BeforeHasHarvestable = ReadExactBoolean(r),
				AfterHasHarvestable = ReadExactBoolean(r), BeforeRipe = ReadExactBoolean(r),
				AfterRipe = ReadExactBoolean(r), BeforeRegenTimer = r.ReadInt32(),
				AfterRegenTimer = r.ReadInt32(), BeforeRegenTime = S(r, false),
				AfterRegenTime = S(r, false), BeforeTileIndex = r.ReadInt32(),
				AfterTileIndex = r.ReadInt32(), BeforeRenderTile = S(r, false),
				AfterRenderTile = S(r, false), BeforeRenderColor = S(r, false),
				AfterRenderColor = S(r, false), BeforeRenderDetail = S(r, false),
				AfterRenderDetail = S(r, false), BeforeRenderString = S(r, false),
				AfterRenderString = S(r, false), BeforeTileColor = S(r, false),
				AfterTileColor = S(r, false),
				BeforeOwnerGraphHash = S(r, true), AfterOwnerGraphHash = S(r, true),
				BeforeObjectGraphHash = S(r, true), AfterObjectGraphHash = S(r, true),
				BeforeTopologyHash = S(r, true), AfterTopologyHash = S(r, true),
				State = (KingdomLifecyclePhysicalState)r.ReadByte(), ReceiptId = S(r, true),
				ReceiptBeforeMatches = r.ReadInt32(), ReceiptAfterMatches = r.ReadInt32(),
				ReceiptBeforeCount = r.ReadInt32(), ReceiptAfterCount = r.ReadInt32(),
				ReceiptCallbackObjectId = S(r, true), ReceiptCallbackMarker = S(r, true),
				ReceiptCallbackReferenceHash = S(r, true),
				ReceiptSameReference = ReadExactBoolean(r),
				ReceiptBeforeOwnerGraphHash = S(r, true), ReceiptAfterOwnerGraphHash = S(r, true),
				ReceiptBeforeObjectGraphHash = S(r, true), ReceiptAfterObjectGraphHash = S(r, true),
				ReceiptBeforeTopologyHash = S(r, true), ReceiptAfterTopologyHash = S(r, true),
				ReceiptProofId = S(r, true),
				ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte()
			};
		}

		private static void WriteGrowthField(BinaryWriter w, KingdomGrowthFieldSlot x)
		{
			if (x == null) throw new InvalidDataException("null growth field slot");
			S(w, x.FieldId, true); w.Write(x.NextSequence); w.Write(x.RetiredThrough);
			w.Write(x.ClockTick); w.Write(x.CommitRevision); S(w, x.LastOperationId, true);
			S(w, x.WorkObjectId, true); S(w, x.WorkPartId, true); S(w, x.Marker, true);
			S(w, x.Blueprint, false); S(w, x.ZoneId, false); w.Write(x.X); w.Write(x.Y);
			S(w, x.CropBlueprint, false); w.Write(x.Stage); w.Write(x.NextStageTick);
			w.Write(x.SownTick); w.Write(x.Cycles); w.Write(x.SaidWant);
			w.Write(x.DeclaredRows); w.Write(x.EffectivenessPercent); w.Write(x.MethodPercent);
			w.Write(x.NoLarderAnnounced); S(w, x.SeedBlueprint, false);
			S(w, x.PartGraphHash, true); S(w, x.ObjectGraphHash, true);
			S(w, x.TopologyHash, true);
			w.Write(x.Quarantined); S(w, x.Fault, false, true); WriteGrowthOperation(w, x.Operation);
		}

		private static void WriteGrowthFieldState(BinaryWriter w, KingdomGrowthFieldState x)
		{
			w.Write(x != null); if (x == null) return;
			S(w, x.FieldId, true); S(w, x.WorkObjectId, true); S(w, x.WorkPartId, true);
			S(w, x.Marker, true); S(w, x.Blueprint, false); S(w, x.ZoneId, false);
			w.Write(x.X); w.Write(x.Y); S(w, x.CropBlueprint, false); w.Write(x.Stage);
			w.Write(x.NextStageTick); w.Write(x.SownTick); w.Write(x.Cycles);
			w.Write(x.SaidWant); w.Write(x.DeclaredRows); w.Write(x.EffectivenessPercent);
			w.Write(x.MethodPercent); w.Write(x.NoLarderAnnounced);
			S(w, x.SeedBlueprint, false); S(w, x.PartGraphHash, true);
			S(w, x.ObjectGraphHash, true); S(w, x.TopologyHash, true);
		}

		private static KingdomGrowthFieldState ReadGrowthFieldState(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			return new KingdomGrowthFieldState
			{
				FieldId = S(r, true), WorkObjectId = S(r, true), WorkPartId = S(r, true),
				Marker = S(r, true), Blueprint = S(r, false), ZoneId = S(r, false),
				X = r.ReadInt32(), Y = r.ReadInt32(), CropBlueprint = S(r, false),
				Stage = r.ReadInt32(), NextStageTick = r.ReadInt64(), SownTick = r.ReadInt64(),
					Cycles = r.ReadInt32(), SaidWant = r.ReadInt32(), DeclaredRows = r.ReadInt32(),
					EffectivenessPercent = r.ReadInt32(), MethodPercent = r.ReadInt32(),
				NoLarderAnnounced = ReadExactBoolean(r), SeedBlueprint = S(r, false),
				PartGraphHash = S(r, true), ObjectGraphHash = S(r, true),
				TopologyHash = S(r, true)
			};
		}

		private static void WriteGrowthCropRows(BinaryWriter w,
			List<KingdomGrowthCropRow> rows)
		{
			w.Write(rows != null); if (rows == null) return;
			EnsureCount(rows, KingdomLifecycleRules.MaxGrowthCropRows, "growth domain crop rows");
			w.Write(rows.Count);
			for (int i = 0; i < rows.Count; i++) WriteCropRow(w, rows[i]);
		}

		private static List<KingdomGrowthCropRow> ReadGrowthCropRows(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			int count = ReadCount(r, KingdomLifecycleRules.MaxGrowthCropRows);
			List<KingdomGrowthCropRow> rows = new List<KingdomGrowthCropRow>(count);
			for (int i = 0; i < count; i++) rows.Add(ReadCropRow(r));
			return rows;
		}

		private static KingdomGrowthFieldSlot ReadGrowthField(BinaryReader r)
		{
			return new KingdomGrowthFieldSlot
			{
				FieldId = S(r, true), NextSequence = r.ReadInt64(), RetiredThrough = r.ReadInt64(),
				ClockTick = r.ReadInt64(), CommitRevision = r.ReadInt64(),
				LastOperationId = S(r, true), WorkObjectId = S(r, true), WorkPartId = S(r, true),
				Marker = S(r, true), Blueprint = S(r, false), ZoneId = S(r, false),
				X = r.ReadInt32(), Y = r.ReadInt32(), CropBlueprint = S(r, false),
				Stage = r.ReadInt32(), NextStageTick = r.ReadInt64(), SownTick = r.ReadInt64(),
					Cycles = r.ReadInt32(), SaidWant = r.ReadInt32(), DeclaredRows = r.ReadInt32(),
					EffectivenessPercent = r.ReadInt32(), MethodPercent = r.ReadInt32(),
				NoLarderAnnounced = ReadExactBoolean(r), SeedBlueprint = S(r, false),
				PartGraphHash = S(r, true), ObjectGraphHash = S(r, true),
				TopologyHash = S(r, true),
				Quarantined = ReadExactBoolean(r), Fault = S(r, false, true),
				Operation = ReadGrowthOperation(r)
			};
		}

		private static void WriteCropRow(BinaryWriter w, KingdomGrowthCropRow x)
		{
			if (x == null) throw new InvalidDataException("null growth crop row");
			S(w, x.FieldId, true); S(w, x.RowId, true); S(w, x.ObjectId, true);
			S(w, x.Marker, true); S(w, x.Blueprint, false); S(w, x.ZoneId, false);
			S(w, x.OwnerId, true); w.Write(x.X); w.Write(x.Y); w.Write(x.Count);
			w.Write(x.HasHarvestable); w.Write(x.Ripe); w.Write(x.RegenTimer);
			S(w, x.RegenTime, false); w.Write(x.TileIndex); S(w, x.RenderTile, false);
			S(w, x.RenderColor, false); S(w, x.RenderDetail, false);
			S(w, x.RenderString, false); S(w, x.TileColor, false);
			S(w, x.PartGraphHash, true); S(w, x.ObjectGraphHash, true);
			S(w, x.TopologyHash, true); w.Write(x.Revision); S(w, x.LastOperationId, true);
		}

		private static KingdomGrowthCropRow ReadCropRow(BinaryReader r)
		{
			return new KingdomGrowthCropRow
			{
				FieldId = S(r, true), RowId = S(r, true), ObjectId = S(r, true),
				Marker = S(r, true), Blueprint = S(r, false), ZoneId = S(r, false),
				OwnerId = S(r, true), X = r.ReadInt32(), Y = r.ReadInt32(), Count = r.ReadInt32(),
				HasHarvestable = ReadExactBoolean(r), Ripe = ReadExactBoolean(r),
				RegenTimer = r.ReadInt32(), RegenTime = S(r, false), TileIndex = r.ReadInt32(),
				RenderTile = S(r, false), RenderColor = S(r, false), RenderDetail = S(r, false),
				RenderString = S(r, false), TileColor = S(r, false),
				PartGraphHash = S(r, true),
				ObjectGraphHash = S(r, true), TopologyHash = S(r, true),
				Revision = r.ReadInt64(), LastOperationId = S(r, true)
			};
		}

		private static void WriteGrowthProof(BinaryWriter w, KingdomGrowthProof x)
		{
			if (x == null) throw new InvalidDataException("null growth proof");
			w.Write((byte)x.Slot); S(w, x.FieldId, true); w.Write(x.Sequence);
			S(w, x.Id, true); S(w, x.PlanHash, true); w.Write((byte)x.Action); w.Write(x.Tick);
		}

		private static KingdomGrowthProof ReadGrowthProof(BinaryReader r)
		{
			return new KingdomGrowthProof
			{
				Slot = (KingdomGrowthSlotKind)r.ReadByte(), FieldId = S(r, true),
				Sequence = r.ReadInt64(), Id = S(r, true), PlanHash = S(r, true),
				Action = (KingdomGrowthAction)r.ReadByte(), Tick = r.ReadInt64()
			};
		}

		private static void WriteGrowthScarcity(BinaryWriter w,
			KingdomGrowthScarcitySnapshot x)
		{
			w.Write(x != null); if (x == null) return;
			w.Write(x.DryStreak); w.Write(x.Withered); w.Write(x.HungerStreak);
			w.Write(x.Famished); w.Write((int)x.LastMeal); w.Write(x.MealShade);
			w.Write(x.ScrapsAnnounced); w.Write(x.ElapsedTicks); w.Write(x.Days);
			w.Write(x.Population); w.Write(x.Stage); w.Write(x.UpkeepRequested);
			w.Write(x.WaterAvailable);
			w.Write(x.RationsAvailable); w.Write(x.Foraged); w.Write(x.Eaten);
			w.Write(x.FromDish); w.Write(x.Kitchens); S(w, x.DishName, false, true);
			S(w, x.DishText, false, true); S(w, x.DishStaple, false, true);
			S(w, x.DishSource, false, true);
			w.Write((byte)x.ComposedBite);
			w.Write(x.RequestedWater); w.Write(x.ProvedWater); w.Write(x.RequestedRations);
			w.Write(x.ProvedRations); w.Write(x.StoresPolicy); w.Write(x.DistrictPercent);
			w.Write((byte)x.ThirstOutcome); w.Write((byte)x.HungerOutcome);
			w.Write(x.Thirsting); w.Write(x.Starving); w.Write(x.Withering);
			w.Write(x.Famishing); w.Write(x.Healthy);
		}

		private static KingdomGrowthScarcitySnapshot ReadGrowthScarcity(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			return new KingdomGrowthScarcitySnapshot
			{
				DryStreak = r.ReadInt32(), Withered = ReadExactBoolean(r),
				HungerStreak = r.ReadInt32(), Famished = ReadExactBoolean(r),
				LastMeal = (KingdomRules.MealVerdict)r.ReadInt32(), MealShade = r.ReadInt32(),
				ScrapsAnnounced = ReadExactBoolean(r), ElapsedTicks = r.ReadInt64(),
				Days = r.ReadInt32(), Population = r.ReadInt32(), Stage = r.ReadInt32(),
				UpkeepRequested = r.ReadInt32(), WaterAvailable = r.ReadInt32(),
				RationsAvailable = r.ReadInt32(), Foraged = r.ReadInt32(), Eaten = r.ReadInt32(),
				FromDish = r.ReadInt32(), Kitchens = r.ReadInt32(),
				DishName = S(r, false, true), DishText = S(r, false, true),
				DishStaple = S(r, false, true), DishSource = S(r, false, true),
				ComposedBite = (KingdomGrowthComposedBite)r.ReadByte(),
				RequestedWater = r.ReadInt32(), ProvedWater = r.ReadInt32(),
				RequestedRations = r.ReadInt32(), ProvedRations = r.ReadInt32(),
				StoresPolicy = r.ReadInt32(), DistrictPercent = r.ReadInt32(),
				ThirstOutcome = (KingdomGrowthThirstOutcome)r.ReadByte(),
				HungerOutcome = (KingdomGrowthHungerOutcome)r.ReadByte(),
				Thirsting = ReadExactBoolean(r), Starving = ReadExactBoolean(r),
				Withering = ReadExactBoolean(r), Famishing = ReadExactBoolean(r),
				Healthy = ReadExactBoolean(r)
			};
		}

		private static void WriteGrowthAccounting(BinaryWriter w,
			KingdomGrowthAccountingSnapshot x)
		{
			w.Write(x != null); if (x == null) return;
			w.Write(x.Fetched); w.Write(x.UpkeepDrawn); w.Write(x.ArrivalCost);
			w.Write(x.Delivered); w.Write(x.Harvested); w.Write(x.Foraged);
			w.Write(x.RationsDrawn); w.Write(x.Milled); w.Write(x.HarvestLost);
			w.Write(x.Plundered); w.Write(x.Arrivals); w.Write(x.Departures);
		}

		private static KingdomGrowthAccountingSnapshot ReadGrowthAccounting(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			return new KingdomGrowthAccountingSnapshot
			{
				Fetched = r.ReadInt32(), UpkeepDrawn = r.ReadInt32(), ArrivalCost = r.ReadInt32(),
				Delivered = r.ReadInt32(), Harvested = r.ReadInt32(), Foraged = r.ReadInt32(),
				RationsDrawn = r.ReadInt32(), Milled = r.ReadInt32(),
				HarvestLost = r.ReadInt32(), Plundered = r.ReadInt32(), Arrivals = r.ReadInt32(),
				Departures = r.ReadInt32()
			};
		}

		private static void WriteGrowthOutboxEvent(BinaryWriter w, KingdomGrowthOutboxEvent x)
		{
			if (x == null) throw new InvalidDataException("null growth outbox event");
			S(w, x.EventId, true); S(w, x.Kind, false); w.Write(x.ChronicleBeforeCount);
			w.Write(x.ChronicleDeclaredAfterCount); w.Write(x.ChronicleObservedCount);
			S(w, x.ChronicleBeforeHash, true); S(w, x.ChronicleDeclaredAfterHash, true);
			S(w, x.ChronicleObservedHash, true); w.Write(x.LedgerBeforeCount);
			w.Write(x.LedgerDeclaredAfterCount); w.Write(x.LedgerObservedCount);
			S(w, x.LedgerBeforeHash, true); S(w, x.LedgerDeclaredAfterHash, true);
			S(w, x.LedgerObservedHash, true); WriteOutbox(w, x.Outbox);
		}

		private static KingdomGrowthOutboxEvent ReadGrowthOutboxEvent(BinaryReader r)
		{
			return new KingdomGrowthOutboxEvent
			{
				EventId = S(r, true), Kind = S(r, false), ChronicleBeforeCount = r.ReadInt32(),
				ChronicleDeclaredAfterCount = r.ReadInt32(), ChronicleObservedCount = r.ReadInt32(),
				ChronicleBeforeHash = S(r, true), ChronicleDeclaredAfterHash = S(r, true),
				ChronicleObservedHash = S(r, true), LedgerBeforeCount = r.ReadInt32(),
				LedgerDeclaredAfterCount = r.ReadInt32(), LedgerObservedCount = r.ReadInt32(),
				LedgerBeforeHash = S(r, true), LedgerDeclaredAfterHash = S(r, true),
				LedgerObservedHash = S(r, true), Outbox = ReadOutbox(r)
			};
		}

		private static void WriteGrowthArrivalCandidate(BinaryWriter w,
			KingdomGrowthArrivalCandidate x)
		{
			w.Write(x != null); if (x == null) return;
			w.Write(x.Sequence); S(w, x.Id, true); S(w, x.PlanHash, true);
			S(w, x.SettlementId, true); w.Write(x.CreatedTick); w.Write(x.UpdatedTick);
			w.Write((byte)x.Phase); w.Write((byte)x.EvidencePhase);
			w.Write((byte)x.Disposition); w.Write((byte)x.RefusalReason); S(w, x.ObjectId, true);
			S(w, x.Marker, true); S(w, x.Blueprint, false); S(w, x.EscrowKey, true);
			WriteLease(w, x.CandidateLease); WriteLease(w, x.LodgingLease);
			WriteLease(w, x.EscrowLease);
			WriteGrowthOptionalObjectCallback(w, x.CreateStep);
			WriteGrowthOptionalObjectCallback(w, x.DispositionStep); S(w, x.LodgingZoneId, false);
			w.Write(x.LodgingX); w.Write(x.LodgingY); S(w, x.LodgingBeforeGraphHash, true);
			S(w, x.LodgingDeclaredGraphHash, true); S(w, x.LodgingReceiptGraphHash, true);
			S(w, x.LodgingCallbackReferenceHash, true); w.Write(x.LodgingSameReference);
			S(w, x.LodgingReceiptId, true); w.Write((byte)x.LodgingState);
			S(w, x.ConsumingOperationId, true); w.Write(x.ConsumingOperationSequence);
			S(w, x.Fault, false, true);
		}

		private static KingdomGrowthArrivalCandidate ReadGrowthArrivalCandidate(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			return new KingdomGrowthArrivalCandidate
			{
				Sequence = r.ReadInt64(), Id = S(r, true), PlanHash = S(r, true),
				SettlementId = S(r, true), CreatedTick = r.ReadInt64(), UpdatedTick = r.ReadInt64(),
				Phase = (KingdomGrowthArrivalCandidatePhase)r.ReadByte(),
				EvidencePhase = (KingdomGrowthArrivalCandidatePhase)r.ReadByte(),
				Disposition = (KingdomGrowthArrivalDisposition)r.ReadByte(),
				RefusalReason = (KingdomGrowthArrivalRefusalReason)r.ReadByte(),
				ObjectId = S(r, true),
				Marker = S(r, true), Blueprint = S(r, false), EscrowKey = S(r, true),
				CandidateLease = ReadLease(r), LodgingLease = ReadLease(r),
				EscrowLease = ReadLease(r),
				CreateStep = ReadGrowthOptionalObjectCallback(r),
				DispositionStep = ReadGrowthOptionalObjectCallback(r), LodgingZoneId = S(r, false),
				LodgingX = r.ReadInt32(), LodgingY = r.ReadInt32(),
				LodgingBeforeGraphHash = S(r, true), LodgingDeclaredGraphHash = S(r, true),
				LodgingReceiptGraphHash = S(r, true), LodgingCallbackReferenceHash = S(r, true),
				LodgingSameReference = ReadExactBoolean(r), LodgingReceiptId = S(r, true),
				LodgingState = (KingdomLifecyclePhysicalState)r.ReadByte(),
				ConsumingOperationId = S(r, true), ConsumingOperationSequence = r.ReadInt64(),
				Fault = S(r, false, true)
			};
		}

		private static void WriteGrowthOptionalObjectCallback(BinaryWriter w,
			KingdomGrowthObjectCallbackStep x)
		{
			w.Write(x != null); if (x != null) WriteGrowthObjectCallback(w, x);
		}

		private static KingdomGrowthObjectCallbackStep ReadGrowthOptionalObjectCallback(BinaryReader r)
		{
			return ReadExactBoolean(r) ? ReadGrowthObjectCallback(r) : null;
		}

		private static KingdomGrowthBook PoisonGrowth(string Fault)
		{
			return new KingdomGrowthBook
			{
				FormatVersion = KingdomLifecycleRules.CurrentGrowthFormatVersion,
				Quarantined = true,
				Fault = BoundFault(Fault)
			};
		}

		private static KingdomGrowthBook OpaqueGrowth(byte[] Payload, int WireVersion,
			string Fault)
		{
			return new KingdomGrowthBook
			{
				FormatVersion = KingdomLifecycleRules.CurrentGrowthFormatVersion,
				Quarantined = true,
				Fault = BoundFault(Fault),
				OpaqueWireVersion = WireVersion,
				OpaquePayload = Payload == null ? null : (byte[])Payload.Clone()
			};
		}

		private static string BoundFault(string Fault)
		{
			if (string.IsNullOrEmpty(Fault)) return "growth payload was rejected";
			return Fault.Length <= KingdomLifecycleRules.MaxTextChars ? Fault :
				Fault.Substring(0, KingdomLifecycleRules.MaxTextChars);
		}

		public static void WriteCarry(BinaryWriter Writer, KingdomCarryBook Book)
		{
			if (Writer == null || Book == null || Book.WireRejected
				|| Book.FormatVersion != KingdomLifecycleRules.CurrentCarryFormatVersion)
				throw new InvalidDataException("carry authority is not writable");
			EnsureCount(Book.SettlementIds, KingdomLifecycleRules.MaxSettlementIds,
				"settlement ids");
			EnsureCount(Book.Resources, KingdomLifecycleRules.MaxResourceRows, "resource rows");
			EnsureCount(Book.RecentProofs, KingdomLifecycleRules.MaxRecentProofs, "proof rows");
			EnsureOuterResourceKinds(Book.Resources);
			if (Book.Open != null && Book.Open.ScheduleLease != null &&
				(byte)Book.Open.ScheduleLease.Kind > (byte)KingdomLifecycleResourceKind.Raid)
				throw new InvalidDataException("carry lease kind exceeds v5 contract");
			Writer.Write(CarryMagic);
			Writer.Write(KingdomLifecycleRules.CurrentCarryFormatVersion);
			Writer.Write(Book.LegacyIdentity);
			WriteString(Writer, Book.LegacyMigrationKey, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.Quarantined);
			WriteString(Writer, Book.Fault, KingdomLifecycleRules.MaxTextBytes);
			WriteString(Writer, Book.RealmId, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.SettlementIds.Count);
			for (int i = 0; i < Book.SettlementIds.Count; i++)
				WriteString(Writer, Book.SettlementIds[i], KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.IdentityBound);
			WriteString(Writer, Book.IdentityProof, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.NextSequence);
			Writer.Write(Book.RetiredThrough);
			WriteCarryOperation(Writer, Book.Open);
			Writer.Write(Book.Resources.Count);
			for (int i = 0; i < Book.Resources.Count; i++) WriteResource(Writer, Book.Resources[i]);
			Writer.Write(Book.RecentProofs.Count);
			for (int i = 0; i < Book.RecentProofs.Count; i++) WriteProof(Writer, Book.RecentProofs[i]);
		}

		public static void ReadCarry(BinaryReader Reader, KingdomCarryBook Target)
		{
			if (Reader == null || Target == null) throw new ArgumentNullException();
			try
			{
				if (Reader.ReadInt32() != CarryMagic) Reject(Target, "invalid carry framing");
				int version = Reader.ReadInt32();
				Target.FormatVersion = version;
				if (version != KingdomLifecycleRules.CurrentCarryFormatVersion)
					Reject(Target, "unsupported carry version");
				KingdomCarryBook value = new KingdomCarryBook();
				value.FormatVersion = version;
				value.LegacyIdentity = ReadExactBoolean(Reader);
				value.LegacyMigrationKey = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				value.Quarantined = ReadExactBoolean(Reader);
				value.Fault = ReadString(Reader, KingdomLifecycleRules.MaxTextBytes);
				value.RealmId = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				int settlements = ReadCount(Reader, KingdomLifecycleRules.MaxSettlementIds);
				value.SettlementIds = new List<string>(settlements);
				for (int i = 0; i < settlements; i++)
					value.SettlementIds.Add(ReadString(Reader, KingdomLifecycleRules.MaxIdBytes));
				value.IdentityBound = ReadExactBoolean(Reader);
				value.IdentityProof = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				value.NextSequence = Reader.ReadInt64();
				value.RetiredThrough = Reader.ReadInt64();
				value.Open = ReadCarryOperation(Reader);
				int resources = ReadCount(Reader, KingdomLifecycleRules.MaxResourceRows);
				value.Resources = new List<KingdomLifecycleResourceRevision>(resources);
				for (int i = 0; i < resources; i++) value.Resources.Add(ReadResource(Reader, true));
				int proofs = ReadCount(Reader, KingdomLifecycleRules.MaxRecentProofs);
				value.RecentProofs = new List<KingdomLifecycleProof>(proofs);
				for (int i = 0; i < proofs; i++) value.RecentProofs.Add(ReadProof(Reader));
				KingdomLifecycleRules.Normalize(value);
				Copy(value, Target);
			}
			catch (Exception)
			{
				Poison(Target, "malformed carry wire was rejected");
				throw;
			}
		}

		public static int ReadCount(BinaryReader Reader, int Maximum)
		{
			int count = Reader.ReadInt32();
			if (count < 0 || count > Maximum)
				throw new InvalidDataException("bounded row count exceeded");
			return count;
		}

		private static bool ReadExactBoolean(BinaryReader Reader)
		{
			byte value = Reader.ReadByte();
			if (value > 1) throw new InvalidDataException("noncanonical boolean byte");
			return value == 1;
		}

		public static string ReadString(BinaryReader Reader, int MaximumBytes)
		{
			int length = Reader.ReadInt32();
			if (length == -1) return null;
			if (length < 0 || length > MaximumBytes)
				throw new InvalidDataException("bounded string length exceeded");
			byte[] bytes = Reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			return StrictUtf8.GetString(bytes);
		}

		public static void WriteString(BinaryWriter Writer, string Value, int MaximumBytes)
		{
			if (Value == null)
			{
				Writer.Write(-1);
				return;
			}
			int byteCount = StrictUtf8.GetByteCount(Value);
			if (byteCount > MaximumBytes)
				throw new InvalidDataException("bounded string length exceeded");
			byte[] bytes = StrictUtf8.GetBytes(Value);
			Writer.Write(byteCount);
			Writer.Write(bytes);
		}

		private static void WriteOperation(BinaryWriter w, KingdomLifecycleOperation o)
		{
			w.Write(o != null);
			if (o == null) return;
			EnsureCount(o.WaterLegs, KingdomLifecycleRules.MaxWaterLegs, "water legs");
			EnsureCount(o.Projections, KingdomLifecycleRules.MaxProjections, "projections");
			EnsureCount(o.ResourceLeases, KingdomLifecycleRules.MaxResourceLeases, "resource leases");
			w.Write(o.Sequence);
			S(w, o.Id, true); S(w, o.PlanHash, true);
			w.Write((byte)o.Lane); w.Write((byte)o.Action); w.Write((byte)o.Phase);
			w.Write(o.CreatedTick); w.Write(o.UpdatedTick);
			S(w, o.SettlementId, true); S(w, o.ZoneId, false); S(w, o.ObjectId, true);
			S(w, o.ObjectMarker, true); S(w, o.Blueprint, false);
			w.Write((byte)o.ObjectTopology); S(w, o.ObjectOwnerId, true);
			w.Write(o.ObjectX); w.Write(o.ObjectY); S(w, o.ObjectName, false);
			S(w, o.Origin, false); S(w, o.Faction, false); S(w, o.DisplayFaction, false);
			S(w, o.Detail, false, true); S(w, o.Creed, false);
			w.Write(o.Kind); w.Write(o.Target); w.Write(o.Count); w.Write(o.DepartedCount);
			w.Write(o.DueBefore); w.Write(o.DueAfter); w.Write(o.DepartTick);
			w.Write(o.WaterRequested); w.Write(o.WaterProved); w.Write(o.WaterOutstanding);
			w.Write(o.WaterLost); w.Write(o.WaterAmbiguous); w.Write((byte)o.WaterState);
			w.Write(o.WaterLegs.Count);
			for (int i = 0; i < o.WaterLegs.Count; i++) WriteWater(w, o.WaterLegs[i]);
			w.Write((byte)o.RemovalState);
			w.Write(o.Projections.Count);
			for (int i = 0; i < o.Projections.Count; i++) WriteProjection(w, o.Projections[i]);
			w.Write((byte)o.EffectState);
			w.Write(o.ResourceLeases.Count);
			for (int i = 0; i < o.ResourceLeases.Count; i++) WriteLease(w, o.ResourceLeases[i]);
			w.Write(o.Defence); w.Write(o.PartySize); w.Write(o.Spawned);
			w.Write(o.PlunderRequested); w.Write(o.PlunderProved);
			S(w, o.ArrivalText, false, true); WriteOutbox(w, o.Outbox); S(w, o.Fault, false, true);
		}

		private static KingdomLifecycleOperation ReadOperation(BinaryReader r)
		{
			return ReadOperation(r, false);
		}

		private static KingdomLifecycleOperation ReadOperation(BinaryReader r, bool legacyWire)
		{
			if (!ReadExactBoolean(r)) return null;
			KingdomLifecycleOperation o = new KingdomLifecycleOperation();
			o.Sequence = r.ReadInt64();
			o.Id = S(r, true); o.PlanHash = S(r, true);
			o.Lane = (KingdomLifecycleLane)r.ReadByte();
			o.Action = (KingdomLifecycleAction)r.ReadByte();
			o.Phase = (KingdomLifecyclePhase)r.ReadByte();
			o.CreatedTick = r.ReadInt64(); o.UpdatedTick = r.ReadInt64();
			o.SettlementId = S(r, true); o.ZoneId = S(r, false); o.ObjectId = S(r, true);
			o.ObjectMarker = S(r, true); o.Blueprint = S(r, false);
			o.ObjectTopology = (KingdomLifecycleTopology)r.ReadByte();
			o.ObjectOwnerId = S(r, true); o.ObjectX = r.ReadInt32(); o.ObjectY = r.ReadInt32();
			o.ObjectName = S(r, false);
			o.Origin = S(r, false); o.Faction = S(r, false); o.DisplayFaction = S(r, false);
			o.Detail = S(r, false, true); o.Creed = S(r, false);
			o.Kind = r.ReadInt32(); o.Target = r.ReadInt32(); o.Count = r.ReadInt32();
			o.DepartedCount = r.ReadInt32(); o.DueBefore = r.ReadInt64();
			o.DueAfter = r.ReadInt64(); o.DepartTick = r.ReadInt64();
			o.WaterRequested = r.ReadInt32(); o.WaterProved = r.ReadInt32();
			o.WaterOutstanding = r.ReadInt32(); o.WaterLost = r.ReadInt32();
			o.WaterAmbiguous = r.ReadInt32(); o.WaterState = (KingdomLifecyclePhysicalState)r.ReadByte();
			int water = ReadCount(r, KingdomLifecycleRules.MaxWaterLegs);
			o.WaterLegs = new List<KingdomLifecycleWaterLeg>(water);
			for (int i = 0; i < water; i++) o.WaterLegs.Add(ReadWater(r));
			o.RemovalState = (KingdomLifecyclePhysicalState)r.ReadByte();
			int projections = ReadCount(r, KingdomLifecycleRules.MaxProjections);
			o.Projections = new List<KingdomLifecycleProjection>(projections);
			for (int i = 0; i < projections; i++) o.Projections.Add(ReadProjection(r));
			o.EffectState = (KingdomLifecyclePhysicalState)r.ReadByte();
			int leases = ReadCount(r, KingdomLifecycleRules.MaxResourceLeases);
			o.ResourceLeases = new List<KingdomLifecycleResourceLease>(leases);
			for (int i = 0; i < leases; i++) o.ResourceLeases.Add(ReadLease(r, legacyWire));
			o.Defence = r.ReadInt32(); o.PartySize = r.ReadInt32(); o.Spawned = r.ReadInt32();
			o.PlunderRequested = r.ReadInt32(); o.PlunderProved = r.ReadInt32();
			o.ArrivalText = S(r, false, true); o.Outbox = ReadOutbox(r); o.Fault = S(r, false, true);
			return o;
		}

		private static void WriteWater(BinaryWriter w, KingdomLifecycleWaterLeg x)
		{
			if (x == null) throw new InvalidDataException("null water leg");
			S(w, x.OperationId, true); S(w, x.LeaseKey, true); S(w, x.OwnerId, true);
			S(w, x.Blueprint, false); S(w, x.ZoneId, false); w.Write(x.Capacity);
			w.Write(x.Before); w.Write(x.Delta);
			w.Write(x.After); S(w, x.Composition, false, true); S(w, x.ReceiptId, false);
			w.Write(x.ReceiptBeforeMatches); w.Write(x.ReceiptAfterMatches);
			w.Write(x.ReceiptSameReference); S(w, x.ReceiptProofId, false);
			w.Write((byte)x.ReceiptState); w.Write((byte)x.State);
		}

		private static KingdomLifecycleWaterLeg ReadWater(BinaryReader r)
		{
			return new KingdomLifecycleWaterLeg
			{
				OperationId = S(r, true), LeaseKey = S(r, true), OwnerId = S(r, true),
				Blueprint = S(r, false), ZoneId = S(r, false), Capacity = r.ReadInt32(),
				Before = r.ReadInt32(),
				Delta = r.ReadInt32(), After = r.ReadInt32(), Composition = S(r, false, true),
				ReceiptId = S(r, false), ReceiptBeforeMatches = r.ReadInt32(),
				ReceiptAfterMatches = r.ReadInt32(), ReceiptSameReference = ReadExactBoolean(r),
				ReceiptProofId = S(r, false),
				ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte(),
				State = (KingdomLifecyclePhysicalState)r.ReadByte()
			};
		}

		private static void WriteGrowthWater(BinaryWriter w, KingdomGrowthWaterLeg x)
		{
			if (x == null) throw new InvalidDataException("null growth water leg");
			S(w, x.OperationId, true); S(w, x.EventId, true); S(w, x.LeaseKey, true);
			w.Write((byte)x.MutationKind); w.Write((byte)x.ContainerKind);
			S(w, x.ContainerId, true); w.Write((byte)x.BeforeLocation);
			w.Write((byte)x.AfterLocation); S(w, x.BeforeOwnerId, true);
			S(w, x.AfterOwnerId, true); S(w, x.BeforeZoneId, false);
			S(w, x.AfterZoneId, false); w.Write(x.BeforeX); w.Write(x.BeforeY);
			w.Write(x.AfterX); w.Write(x.AfterY); w.Write(x.OwnerRemovedAfter);
			w.Write((byte)x.OwnerTopology); S(w, x.OwnerId, true);
			S(w, x.Blueprint, false); S(w, x.ZoneId, false); w.Write(x.X); w.Write(x.Y);
			w.Write(x.Capacity); w.Write(x.Before); w.Write(x.Delta); w.Write(x.After);
			S(w, x.BeforeComposition, false, true); S(w, x.AfterComposition, false, true);
			S(w, x.BeforeOwnerGraphHash, true);
			S(w, x.AfterOwnerGraphHash, true); S(w, x.BeforePartGraphHash, true);
			S(w, x.AfterPartGraphHash, true); S(w, x.BeforeTopologyHash, true);
			S(w, x.AfterTopologyHash, true); S(w, x.ReceiptId, false);
			w.Write(x.ReceiptBeforeMatches); w.Write(x.ReceiptAfterMatches);
			S(w, x.ReceiptBeforeOwnerGraphHash, true);
			S(w, x.ReceiptAfterOwnerGraphHash, true); S(w, x.ReceiptBeforePartGraphHash, true);
			S(w, x.ReceiptAfterPartGraphHash, true); S(w, x.ReceiptBeforeTopologyHash, true);
			S(w, x.ReceiptAfterTopologyHash, true); S(w, x.ReceiptCallbackContainerId, true);
			S(w, x.ReceiptCallbackReferenceHash, true); w.Write(x.ReceiptSameReference);
			S(w, x.ReceiptProofId, false);
			w.Write((byte)x.ReceiptState); w.Write((byte)x.State); WriteLease(w, x.Lease);
		}

		private static KingdomGrowthWaterLeg ReadGrowthWater(BinaryReader r)
		{
			return new KingdomGrowthWaterLeg
			{
				OperationId = S(r, true), EventId = S(r, true), LeaseKey = S(r, true),
				MutationKind = (KingdomGrowthWaterMutationKind)r.ReadByte(),
				ContainerKind = (KingdomGrowthWaterContainerKind)r.ReadByte(),
				ContainerId = S(r, true),
				BeforeLocation = (KingdomGrowthLocationKind)r.ReadByte(),
				AfterLocation = (KingdomGrowthLocationKind)r.ReadByte(),
				BeforeOwnerId = S(r, true), AfterOwnerId = S(r, true),
				BeforeZoneId = S(r, false), AfterZoneId = S(r, false),
				BeforeX = r.ReadInt32(), BeforeY = r.ReadInt32(),
				AfterX = r.ReadInt32(), AfterY = r.ReadInt32(),
				OwnerRemovedAfter = ReadExactBoolean(r),
				OwnerTopology = (KingdomLifecycleTopology)r.ReadByte(), OwnerId = S(r, true),
				Blueprint = S(r, false), ZoneId = S(r, false), X = r.ReadInt32(), Y = r.ReadInt32(),
				Capacity = r.ReadInt32(), Before = r.ReadInt32(), Delta = r.ReadInt32(),
				After = r.ReadInt32(), BeforeComposition = S(r, false, true),
				AfterComposition = S(r, false, true),
				BeforeOwnerGraphHash = S(r, true), AfterOwnerGraphHash = S(r, true),
				BeforePartGraphHash = S(r, true), AfterPartGraphHash = S(r, true),
				BeforeTopologyHash = S(r, true), AfterTopologyHash = S(r, true),
				ReceiptId = S(r, false), ReceiptBeforeMatches = r.ReadInt32(),
				ReceiptAfterMatches = r.ReadInt32(),
				ReceiptBeforeOwnerGraphHash = S(r, true), ReceiptAfterOwnerGraphHash = S(r, true),
				ReceiptBeforePartGraphHash = S(r, true), ReceiptAfterPartGraphHash = S(r, true),
				ReceiptBeforeTopologyHash = S(r, true), ReceiptAfterTopologyHash = S(r, true),
				ReceiptCallbackContainerId = S(r, true),
				ReceiptCallbackReferenceHash = S(r, true),
				ReceiptSameReference = ReadExactBoolean(r),
				ReceiptProofId = S(r, false),
				ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte(),
				State = (KingdomLifecyclePhysicalState)r.ReadByte(), Lease = ReadLease(r)
			};
		}

		private static void WriteProjection(BinaryWriter w, KingdomLifecycleProjection x)
		{
			if (x == null) throw new InvalidDataException("null projection");
			S(w, x.OperationId, true); S(w, x.EventId, true); S(w, x.ObjectId, true);
			S(w, x.Marker, true); S(w, x.Blueprint, false); S(w, x.ZoneId, false);
			w.Write((byte)x.Topology); S(w, x.OwnerId, true); w.Write(x.X); w.Write(x.Y);
			w.Write(x.Material); w.Write(x.Count); w.Write(x.NoStack); w.Write((byte)x.State);
			S(w, x.ReceiptId, false); S(w, x.ReceiptTopologyId, false);
			w.Write(x.ReceiptBeforeIdMatches); w.Write(x.ReceiptBeforeMarkerMatches);
			w.Write(x.ReceiptBeforeCount); w.Write(x.ReceiptAfterIdMatches);
			w.Write(x.ReceiptAfterMarkerMatches); w.Write(x.ReceiptAfterCount);
			w.Write(x.ReceiptSameReference); S(w, x.ReceiptProofId, false);
			w.Write((byte)x.ReceiptState);
		}

		private static KingdomLifecycleProjection ReadProjection(BinaryReader r)
		{
			return new KingdomLifecycleProjection
			{
				OperationId = S(r, true), EventId = S(r, true), ObjectId = S(r, true),
				Marker = S(r, true), Blueprint = S(r, false), ZoneId = S(r, false),
				Topology = (KingdomLifecycleTopology)r.ReadByte(), OwnerId = S(r, true),
				X = r.ReadInt32(), Y = r.ReadInt32(), Material = r.ReadInt32(),
					Count = r.ReadInt32(), NoStack = ReadExactBoolean(r),
					State = (KingdomLifecyclePhysicalState)r.ReadByte(),
					ReceiptId = S(r, false), ReceiptTopologyId = S(r, false),
					ReceiptBeforeIdMatches = r.ReadInt32(),
					ReceiptBeforeMarkerMatches = r.ReadInt32(),
					ReceiptBeforeCount = r.ReadInt32(),
					ReceiptAfterIdMatches = r.ReadInt32(),
					ReceiptAfterMarkerMatches = r.ReadInt32(),
					ReceiptAfterCount = r.ReadInt32(),
					ReceiptSameReference = ReadExactBoolean(r),
					ReceiptProofId = S(r, false),
					ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte()
			};
		}

		private static void WriteLease(BinaryWriter w, KingdomLifecycleResourceLease x)
		{
			if (x == null) throw new InvalidDataException("null lease");
			S(w, x.OperationId, true); w.Write((byte)x.Kind); S(w, x.ScopeId, true);
			S(w, x.SubjectId, true); S(w, x.Key, true); w.Write(x.Before); w.Write(x.Delta);
			w.Write(x.After); w.Write(x.BeforeRevision); w.Write(x.AfterRevision);
			w.Write((byte)x.State);
		}

		private static KingdomLifecycleResourceLease ReadLease(BinaryReader r)
		{
			return ReadLease(r, false);
		}

		private static KingdomLifecycleResourceLease ReadLease(BinaryReader r, bool legacyWire)
		{
			string operationId = S(r, true);
			byte rawKind = r.ReadByte();
			if (legacyWire && rawKind > (byte)KingdomLifecycleResourceKind.Raid)
				throw new InvalidDataException("legacy lifecycle lease kind is unsupported");
			return new KingdomLifecycleResourceLease
			{
				OperationId = operationId, Kind = (KingdomLifecycleResourceKind)rawKind,
				ScopeId = S(r, true), SubjectId = S(r, true), Key = S(r, true),
				Before = r.ReadInt64(), Delta = r.ReadInt64(), After = r.ReadInt64(),
				BeforeRevision = r.ReadInt64(), AfterRevision = r.ReadInt64(),
				State = (KingdomLifecycleLeaseState)r.ReadByte()
			};
		}

		private static void WriteResource(BinaryWriter w, KingdomLifecycleResourceRevision x)
		{
			if (x == null) throw new InvalidDataException("null resource row");
			w.Write((byte)x.Kind); S(w, x.ScopeId, true); S(w, x.SubjectId, true);
			S(w, x.Key, true); w.Write(x.Revision); S(w, x.ActiveOperationId, true);
			S(w, x.LastOperationId, true);
		}

		private static KingdomLifecycleResourceRevision ReadResource(BinaryReader r)
		{
			return ReadResource(r, false);
		}

		private static KingdomLifecycleResourceRevision ReadResource(BinaryReader r, bool legacyWire)
		{
			byte rawKind = r.ReadByte();
			if (legacyWire && rawKind > (byte)KingdomLifecycleResourceKind.Raid)
				throw new InvalidDataException("legacy lifecycle resource kind is unsupported");
			return new KingdomLifecycleResourceRevision
			{
				Kind = (KingdomLifecycleResourceKind)rawKind, ScopeId = S(r, true),
				SubjectId = S(r, true), Key = S(r, true), Revision = r.ReadInt64(),
				ActiveOperationId = S(r, true), LastOperationId = S(r, true)
			};
		}

		private static void WriteOutbox(BinaryWriter w, KingdomLifecycleOutbox x)
		{
			w.Write(x != null);
			if (x == null) return;
			S(w, x.OperationId, true); S(w, x.EventId, true); S(w, x.ChronicleReceiptId, true);
			S(w, x.Chronicle, false, true); w.Write(x.ChronicleAccomplishment);
			w.Write((byte)x.ChronicleDisposition); w.Write((byte)x.ChronicleState);
			S(w, x.Ledger, false, true); w.Write((byte)x.LedgerDisposition);
			w.Write((byte)x.LedgerState); S(w, x.Message, false, true);
			w.Write((byte)x.MessageDisposition); w.Write((byte)x.MessageState);
			S(w, x.Deed, false, true); w.Write((byte)x.DeedDisposition);
			w.Write((byte)x.DeedState); S(w, x.GuestbookLine, false, true);
			w.Write((byte)x.GuestbookDisposition); w.Write((byte)x.GuestbookState);
		}

		private static KingdomLifecycleOutbox ReadOutbox(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			return new KingdomLifecycleOutbox
			{
				OperationId = S(r, true), EventId = S(r, true), ChronicleReceiptId = S(r, true),
				Chronicle = S(r, false, true), ChronicleAccomplishment = ReadExactBoolean(r),
				ChronicleDisposition = (KingdomLifecycleSinkDisposition)r.ReadByte(),
				ChronicleState = (KingdomLifecycleSinkState)r.ReadByte(),
				Ledger = S(r, false, true),
				LedgerDisposition = (KingdomLifecycleSinkDisposition)r.ReadByte(),
				LedgerState = (KingdomLifecycleSinkState)r.ReadByte(), Message = S(r, false, true),
				MessageDisposition = (KingdomLifecycleSinkDisposition)r.ReadByte(),
				MessageState = (KingdomLifecycleSinkState)r.ReadByte(), Deed = S(r, false, true),
				DeedDisposition = (KingdomLifecycleSinkDisposition)r.ReadByte(),
				DeedState = (KingdomLifecycleSinkState)r.ReadByte(), GuestbookLine = S(r, false, true),
				GuestbookDisposition = (KingdomLifecycleSinkDisposition)r.ReadByte(),
				GuestbookState = (KingdomLifecycleSinkState)r.ReadByte()
			};
		}

		private static void WriteProof(BinaryWriter w, KingdomLifecycleProof x)
		{
			if (x == null) throw new InvalidDataException("null proof");
			w.Write(x.Sequence); S(w, x.Id, true); S(w, x.PlanHash, true);
			w.Write((byte)x.Lane); w.Write((byte)x.Action); w.Write(x.Tick);
		}

		private static KingdomLifecycleProof ReadProof(BinaryReader r)
		{
			return new KingdomLifecycleProof
			{
				Sequence = r.ReadInt64(), Id = S(r, true), PlanHash = S(r, true),
				Lane = (KingdomLifecycleLane)r.ReadByte(),
				Action = (KingdomLifecycleAction)r.ReadByte(), Tick = r.ReadInt64()
			};
		}

		private static void WriteCarryOperation(BinaryWriter w, KingdomCarryOperation o)
		{
			w.Write(o != null);
			if (o == null) return;
			EnsureCount(o.Sources, KingdomLifecycleRules.MaxCarrySources, "carry sources");
			EnsureCount(o.Outputs, KingdomLifecycleRules.MaxCarryOutputs, "carry outputs");
			EnsureCount(o.SettlementIds, KingdomLifecycleRules.MaxSettlementIds,
				"frozen carry settlement ids");
			w.Write(o.Sequence); S(w, o.Id, true); S(w, o.PlanHash, true);
			w.Write((byte)o.Phase); w.Write(o.CreatedTick); w.Write(o.UpdatedTick);
			w.Write(o.SettlementIds.Count);
			for (int i = 0; i < o.SettlementIds.Count; i++) S(w, o.SettlementIds[i], true);
			S(w, o.RealmTopologyHash, true);
			S(w, o.OriginSettlementId, true); S(w, o.OriginZoneId, false);
			w.Write(o.OriginX); w.Write(o.OriginY);
			S(w, o.DestinationSettlementId, true); S(w, o.DestinationSettlementName, false);
			S(w, o.DestinationZoneId, false); w.Write((byte)o.DestinationTopology);
			S(w, o.DestinationOwnerId, true); w.Write(o.DestinationX); w.Write(o.DestinationY);
			w.Write(o.DueTick); w.Write(o.RiskFrozen); w.Write(o.LostOnRoad);
			w.Write(o.SourceIndex); w.Write(o.OutputIndex);
			WriteLease(w, o.ScheduleLease);
			S(w, o.ScheduleReceiptId, false); S(w, o.ScheduleTopologyId, false);
			w.Write(o.ScheduleBeforeMatches); w.Write(o.ScheduleAfterMatches);
			w.Write(o.ScheduleSameReference); S(w, o.ScheduleProofId, false);
			w.Write((byte)o.ScheduleReceiptState);
			w.Write(o.Sources.Count);
			for (int i = 0; i < o.Sources.Count; i++) WriteCarrySource(w, o.Sources[i]);
			w.Write(o.Outputs.Count);
			for (int i = 0; i < o.Outputs.Count; i++) WriteProjection(w, o.Outputs[i]);
			WriteSix(w, o.Mud, o.Brush, o.Timber, o.Stone, o.Marble, o.Scrap);
			WriteSix(w, o.EscrowMud, o.EscrowBrush, o.EscrowTimber, o.EscrowStone,
				o.EscrowMarble, o.EscrowScrap);
			WriteSix(w, o.DeliveredMud, o.DeliveredBrush, o.DeliveredTimber, o.DeliveredStone,
				o.DeliveredMarble, o.DeliveredScrap);
			WriteSix(w, o.LostMud, o.LostBrush, o.LostTimber, o.LostStone, o.LostMarble, o.LostScrap);
			WriteOutbox(w, o.Outbox); S(w, o.Fault, false, true);
		}

		private static KingdomCarryOperation ReadCarryOperation(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			KingdomCarryOperation o = new KingdomCarryOperation();
			o.Sequence = r.ReadInt64(); o.Id = S(r, true); o.PlanHash = S(r, true);
			o.Phase = (KingdomLifecyclePhase)r.ReadByte(); o.CreatedTick = r.ReadInt64();
			o.UpdatedTick = r.ReadInt64();
			int settlements = ReadCount(r, KingdomLifecycleRules.MaxSettlementIds);
			o.SettlementIds = new List<string>(settlements);
			for (int i = 0; i < settlements; i++) o.SettlementIds.Add(S(r, true));
			o.RealmTopologyHash = S(r, true); o.OriginSettlementId = S(r, true);
			o.OriginZoneId = S(r, false);
			o.OriginX = r.ReadInt32(); o.OriginY = r.ReadInt32();
			o.DestinationSettlementId = S(r, true); o.DestinationSettlementName = S(r, false);
			o.DestinationZoneId = S(r, false);
			o.DestinationTopology = (KingdomLifecycleTopology)r.ReadByte();
			o.DestinationOwnerId = S(r, true);
			o.DestinationX = r.ReadInt32(); o.DestinationY = r.ReadInt32();
			o.DueTick = r.ReadInt64(); o.RiskFrozen = ReadExactBoolean(r);
			o.LostOnRoad = ReadExactBoolean(r);
			o.SourceIndex = r.ReadInt32(); o.OutputIndex = r.ReadInt32();
			o.ScheduleLease = ReadLease(r, true);
			o.ScheduleReceiptId = S(r, false); o.ScheduleTopologyId = S(r, false);
			o.ScheduleBeforeMatches = r.ReadInt32(); o.ScheduleAfterMatches = r.ReadInt32();
			o.ScheduleSameReference = ReadExactBoolean(r); o.ScheduleProofId = S(r, false);
			o.ScheduleReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte();
			int sources = ReadCount(r, KingdomLifecycleRules.MaxCarrySources);
			o.Sources = new List<KingdomCarrySource>(sources);
			for (int i = 0; i < sources; i++) o.Sources.Add(ReadCarrySource(r));
			int outputs = ReadCount(r, KingdomLifecycleRules.MaxCarryOutputs);
			o.Outputs = new List<KingdomLifecycleProjection>(outputs);
			for (int i = 0; i < outputs; i++) o.Outputs.Add(ReadProjection(r));
			ReadSix(r, out o.Mud, out o.Brush, out o.Timber, out o.Stone, out o.Marble, out o.Scrap);
			ReadSix(r, out o.EscrowMud, out o.EscrowBrush, out o.EscrowTimber, out o.EscrowStone,
				out o.EscrowMarble, out o.EscrowScrap);
			ReadSix(r, out o.DeliveredMud, out o.DeliveredBrush, out o.DeliveredTimber,
				out o.DeliveredStone, out o.DeliveredMarble, out o.DeliveredScrap);
			ReadSix(r, out o.LostMud, out o.LostBrush, out o.LostTimber, out o.LostStone,
				out o.LostMarble, out o.LostScrap);
			o.Outbox = ReadOutbox(r); o.Fault = S(r, false, true);
			return o;
		}

		private static void WriteCarrySource(BinaryWriter w, KingdomCarrySource x)
		{
			if (x == null) throw new InvalidDataException("null carry source");
			S(w, x.OperationId, true); S(w, x.SourceEventId, true); S(w, x.ObjectId, true);
			S(w, x.Blueprint, false); w.Write((byte)x.Topology); S(w, x.OwnerId, true);
			S(w, x.ZoneId, false); w.Write(x.X); w.Write(x.Y); w.Write(x.Material);
			w.Write(x.OriginalCount); w.Write(x.PlannedCount); w.Write(x.Removed); w.Write(x.UnitCursor);
			w.Write(x.UnitBefore); w.Write(x.UnitAfter); S(w, x.UnitEventId, true);
			w.Write((byte)x.UnitState); S(w, x.ReceiptId, false); S(w, x.ReceiptTopologyId, false);
			w.Write(x.ReceiptBeforeIdMatches); w.Write(x.ReceiptAfterIdMatches);
			w.Write(x.ReceiptBeforeCount); w.Write(x.ReceiptAfterCount);
			w.Write(x.ReceiptSameReference); S(w, x.ReceiptProofId, false);
			S(w, x.ReceiptChainId, false); w.Write(x.ReceiptChainCount);
			w.Write((byte)x.ReceiptState); w.Write((byte)x.State);
		}

		private static KingdomCarrySource ReadCarrySource(BinaryReader r)
		{
			return new KingdomCarrySource
			{
				OperationId = S(r, true), SourceEventId = S(r, true), ObjectId = S(r, true),
				Blueprint = S(r, false), Topology = (KingdomLifecycleTopology)r.ReadByte(),
				OwnerId = S(r, true), ZoneId = S(r, false), X = r.ReadInt32(), Y = r.ReadInt32(),
				Material = r.ReadInt32(), OriginalCount = r.ReadInt32(), PlannedCount = r.ReadInt32(),
				Removed = r.ReadInt32(),
				UnitCursor = r.ReadInt32(), UnitBefore = r.ReadInt32(), UnitAfter = r.ReadInt32(),
				UnitEventId = S(r, true), UnitState = (KingdomLifecyclePhysicalState)r.ReadByte(),
				ReceiptId = S(r, false), ReceiptTopologyId = S(r, false),
				ReceiptBeforeIdMatches = r.ReadInt32(), ReceiptAfterIdMatches = r.ReadInt32(),
				ReceiptBeforeCount = r.ReadInt32(), ReceiptAfterCount = r.ReadInt32(),
				ReceiptSameReference = ReadExactBoolean(r), ReceiptProofId = S(r, false),
				ReceiptChainId = S(r, false), ReceiptChainCount = r.ReadInt32(),
				ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte(),
				State = (KingdomLifecyclePhysicalState)r.ReadByte()
			};
		}

		private static void WriteOption(BinaryWriter w, KingdomLifecycleOptionState s, long tick)
		{
			w.Write((byte)s); w.Write(tick);
		}

		private static void ReadOption(BinaryReader r, out KingdomLifecycleOptionState s, out long tick)
		{
			s = (KingdomLifecycleOptionState)r.ReadByte(); tick = r.ReadInt64();
		}

		private static void WriteSix(BinaryWriter w, int a, int b, int c, int d, int e, int f)
		{
			w.Write(a); w.Write(b); w.Write(c); w.Write(d); w.Write(e); w.Write(f);
		}

		private static void ReadSix(BinaryReader r, out int a, out int b, out int c,
			out int d, out int e, out int f)
		{
			a = r.ReadInt32(); b = r.ReadInt32(); c = r.ReadInt32();
			d = r.ReadInt32(); e = r.ReadInt32(); f = r.ReadInt32();
		}

		private static string S(BinaryReader r, bool id, bool text = false)
		{
			return ReadString(r, text ? KingdomLifecycleRules.MaxTextBytes
				: id ? KingdomLifecycleRules.MaxIdBytes : KingdomLifecycleRules.MaxNameBytes);
		}

		private static void S(BinaryWriter w, string value, bool id, bool text = false)
		{
			WriteString(w, value, text ? KingdomLifecycleRules.MaxTextBytes
				: id ? KingdomLifecycleRules.MaxIdBytes : KingdomLifecycleRules.MaxNameBytes);
		}

		private static void EnsureCount<T>(List<T> rows, int maximum, string description)
		{
			if (rows == null || rows.Count > maximum)
				throw new InvalidDataException("invalid " + description);
		}

		private static void EnsureOuterResourceKinds(
			List<KingdomLifecycleResourceRevision> rows,
			params KingdomLifecycleOperation[] operations)
		{
			if (rows != null) for (int i = 0; i < rows.Count; i++)
				if (rows[i] == null || (byte)rows[i].Kind >
					(byte)KingdomLifecycleResourceKind.Raid)
					throw new InvalidDataException("outer resource kind exceeds v5 contract");
			if (operations == null) return;
			for (int i = 0; i < operations.Length; i++)
			{
				KingdomLifecycleOperation operation = operations[i];
				if (operation == null || operation.ResourceLeases == null) continue;
				for (int j = 0; j < operation.ResourceLeases.Count; j++)
					if (operation.ResourceLeases[j] == null ||
						(byte)operation.ResourceLeases[j].Kind >
						(byte)KingdomLifecycleResourceKind.Raid)
						throw new InvalidDataException("outer lease kind exceeds v5 contract");
			}
		}

		private static void Reject(KingdomLifecycleBook target, string fault)
		{
			target.WireRejected = true; target.Quarantined = true; target.Fault = fault;
			throw new InvalidDataException(fault);
		}

		private static void Reject(KingdomCarryBook target, string fault)
		{
			target.WireRejected = true; target.Quarantined = true; target.Fault = fault;
			throw new InvalidDataException(fault);
		}

		private static void Poison(KingdomLifecycleBook target, string fault)
		{
			target.WireRejected = true;
			target.Quarantined = true;
			if (string.IsNullOrEmpty(target.Fault)) target.Fault = fault;
			target.PlainGuest = null;
			target.NotableGuest = null;
			target.Raid = null;
			target.Petition = null;
			target.Resources = new List<KingdomLifecycleResourceRevision>();
			target.RecentProofs = new List<KingdomLifecycleProof>();
			target.Growth = PoisonGrowth("enclosing lifecycle wire was rejected");
		}

		private static void Poison(KingdomCarryBook target, string fault)
		{
			target.WireRejected = true;
			target.Quarantined = true;
			if (string.IsNullOrEmpty(target.Fault)) target.Fault = fault;
			target.Open = null;
			target.SettlementIds = new List<string>();
			target.Resources = new List<KingdomLifecycleResourceRevision>();
			target.RecentProofs = new List<KingdomLifecycleProof>();
		}

		private static void Copy(KingdomLifecycleBook a, KingdomLifecycleBook b)
		{
			b.FormatVersion = a.FormatVersion; b.LegacyIdentity = a.LegacyIdentity;
			b.LegacyMigrationKey = a.LegacyMigrationKey; b.Quarantined = a.Quarantined;
			b.Fault = a.Fault; b.SettlementId = a.SettlementId;
			b.IdentityBound = a.IdentityBound; b.IdentityProof = a.IdentityProof;
			b.PlainGuestNextSequence = a.PlainGuestNextSequence;
			b.PlainGuestRetiredThrough = a.PlainGuestRetiredThrough;
			b.NotableGuestNextSequence = a.NotableGuestNextSequence;
			b.NotableGuestRetiredThrough = a.NotableGuestRetiredThrough;
			b.RaidNextSequence = a.RaidNextSequence; b.RaidRetiredThrough = a.RaidRetiredThrough;
			b.PetitionNextSequence = a.PetitionNextSequence;
			b.PetitionRetiredThrough = a.PetitionRetiredThrough;
			b.LocusOption = a.LocusOption; b.LocusOptionTick = a.LocusOptionTick;
			b.NotableOption = a.NotableOption; b.NotableOptionTick = a.NotableOptionTick;
			b.RaidOption = a.RaidOption; b.RaidOptionTick = a.RaidOptionTick;
			b.PetitionOption = a.PetitionOption; b.PetitionOptionTick = a.PetitionOptionTick;
			b.PlainGuest = a.PlainGuest; b.NotableGuest = a.NotableGuest;
			b.Raid = a.Raid; b.Petition = a.Petition;
			b.Resources = a.Resources; b.RecentProofs = a.RecentProofs;
			b.Growth = a.Growth; b.WireRejected = false;
		}

		private static void Copy(KingdomCarryBook a, KingdomCarryBook b)
		{
			b.FormatVersion = a.FormatVersion; b.LegacyIdentity = a.LegacyIdentity;
			b.LegacyMigrationKey = a.LegacyMigrationKey; b.Quarantined = a.Quarantined;
			b.Fault = a.Fault; b.RealmId = a.RealmId; b.SettlementIds = a.SettlementIds;
			b.IdentityBound = a.IdentityBound; b.IdentityProof = a.IdentityProof;
			b.NextSequence = a.NextSequence;
			b.RetiredThrough = a.RetiredThrough; b.Open = a.Open;
			b.Resources = a.Resources; b.RecentProofs = a.RecentProofs; b.WireRejected = false;
		}
	}
}
