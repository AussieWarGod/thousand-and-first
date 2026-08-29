namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		public static KingdomGrowthArrivalCandidate PrepareGrowthFirstGuestCandidate(
			KingdomGrowthBook Book, string Marker, string Blueprint, string EscrowKey, string ZoneId,
			long Tick, string BeforeOwnerGraphHash, string BeforeObjectGraphHash,
			string BeforeTopologyHash, int SemanticPlanVersion, string SemanticStreamId,
			uint SemanticEventKind, string PlannedOrigin, string PlannedCreed,
			string PlannedName, string PlannedArrived, int ArrivalX, int ArrivalY,
			long CauseTick, long CadenceTicks, int PopulationBefore, int PopulationCap,
			int SupportedLevel, int SupportCap, int WaterAvailable, int WaterRequired)
		{
			if (Book == null || Book.ArrivalCandidateNextSequence != 1L
				|| CauseTick != Book.NextArrivalTick
				|| (Book.ArrivalCadenceMigrationPending
					? CadenceTicks != Book.ArrivalIntervalTicks
					: Book.ArrivalOpportunity == null
						|| CauseTick != Book.ArrivalOpportunity.DueTick
						|| CadenceTicks != Book.ArrivalOpportunity.IntervalTicks)
				|| CauseTick < 0L || CauseTick > Tick
				|| PopulationBefore < 0 || PopulationCap <= PopulationBefore
				|| SupportedLevel < 0 || SupportCap <= PopulationBefore
				|| WaterRequired <= 0 || WaterAvailable < WaterRequired) return null;
			KingdomGrowthArrivalCandidate candidate = PrepareGrowthArrivalCandidate(Book,
				Marker, Blueprint, EscrowKey, ZoneId, Tick, BeforeOwnerGraphHash,
				BeforeObjectGraphHash, BeforeTopologyHash, SemanticPlanVersion, SemanticStreamId,
				SemanticEventKind, PlannedOrigin, PlannedCreed, PlannedName, PlannedArrived,
				ArrivalX, ArrivalY);
			if (candidate == null) return null;
			candidate.LegacyAutomaticRecovery = false;
			candidate.FirstGuest = new KingdomGrowthFirstGuestOpportunity
			{
				RulesVersion = 2,
				OpportunityId = GrowthFirstGuestOpportunityId(candidate.SettlementId,
					candidate.Sequence),
				CauseId = GrowthFirstGuestCauseId(candidate.SettlementId, candidate.Sequence,
					CauseTick, CadenceTicks),
				CauseTick = CauseTick, OfferedTick = Tick, CadenceTicks = CadenceTicks,
				FactsState = KingdomGrowthFirstGuestFactsState.Exact, CohortSize = 1,
				PopulationBefore = PopulationBefore, PopulationCap = PopulationCap,
				SupportedLevel = SupportedLevel, SupportCap = SupportCap,
				WaterAvailable = WaterAvailable, WaterRequired = WaterRequired,
				ChoiceState = KingdomGrowthFirstGuestChoiceState.AwaitingChoice,
				GuestPhase = KingdomGrowthFirstGuestGuestPhase.None
			};
			candidate.Phase = KingdomGrowthArrivalCandidatePhase.AwaitingChoice;
			return GrowthArrivalCandidateShape(Book, candidate, true) ? candidate : null;
		}
	}
}
