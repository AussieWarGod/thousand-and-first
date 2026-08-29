using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomGrowthOperation
	{
		public long Sequence;
		public string Id;
		public string PlanHash;
		/// <summary>Compatibility proof for an operation decoded from the exact Growth-v1
		/// plan-hash domain. New v2 operations always leave this false.</summary>
		public bool LegacyGrowthV1Plan;
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
		public ulong ArrivalOpportunityOrdinal;
		public long ArrivalOpportunityDueTick;
		public long ArrivalOpportunityRateEpoch;
		public string ArrivalOpportunityPayloadHash;
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
}
