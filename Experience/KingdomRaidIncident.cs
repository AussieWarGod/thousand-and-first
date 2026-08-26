using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomRaidIncident
	{
		public string Id;
		public string GrievanceId;
		public string CauseSnapshot;
		public string SettlementId;
		public string TargetZoneId;
		public string AttackerFactionId;
		public string SourceKind;
		public string SourceLocator;
		public string ReachRule;
		public KingdomRaidIncidentState State;
		public long Seed;
		public long RumorTick;
		public long DeliveredTick;
		public long DueTick;
		public long DemandLeadTicks;
		public long RemainingLeadTicks;
		public string DemandChannelId;
		public string DemandObjectId;
		public KingdomRaidChannelState ChannelState;
		public int ChannelRevision;
		public KingdomRaidResponse Response;
		public int DisclosedStake;
		public int MaximumPlunder;
		public string ForceProfileId;
		public int DefenceEstimate;
		public string ObjectiveCode;
		public string ObjectiveObjectId;
		public int ObjectiveX = -1;
		public int ObjectiveY = -1;
		public int PlannedPartySize;
		public int SpawnedPartySize;
		public int PlunderProved;
		public string DefenceCommitment;
		public int DefenceReservationVersion;
		public List<KingdomRaidDefenceReservation> DefenceReservations =
			new List<KingdomRaidDefenceReservation>();
		public long FortifyOrderedTick;
		public bool TalkObligation;
		public string TalkObligationDischargedBy;
		public string LastNotice;
		public KingdomRaidResolution Resolution;
		public string AttackOperationId;
		public string ResolutionOperationId;
		public long ResolvedTick;
		public KingdomRaidRecoveryState RecoveryState;
		public string RecoveryQuestId;
		public string RecoveryStepId;
		public long RecoveryOpenedTick;
		public long RecoveryResolvedTick;
		public string RecoveryNotice;
	}
}
