using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		private static bool TryCivicStoryAllowsFirstGuest(KingdomSystem system, long tick,
			out bool enabled)
		{
			enabled = false;
			if (system == null || !KingdomExperienceRuntime.TryObserveConfiguredOptions(system,
				tick, out string _)) return false;
			enabled = KingdomExperienceRules.CanEmit(system.Experience,
				KingdomExperienceOptionKind.CivicStory, tick);
			if (!enabled) KingdomExperienceRuntime.TryRecord(system,
				KingdomExperienceExperiment.FirstGuestCorrespondence,
				KingdomExperienceTrialArm.FactsOnly,
				KingdomExperienceObservationKind.Closed, 0);
			return true;
		}

		private static ArrivalResult StartFirstGuestOpportunity(KingdomSystem system,
			Zone zone, KingdomSurvey survey, long tick)
		{
			KingdomGrowthBook growth = system.LifecycleBook.Growth;
			KingdomGrowthArrivalOpportunity opportunity = growth.ArrivalOpportunity;
			long sequence = growth.ArrivalCandidateNextSequence;
			Cell cell;
			string semanticFailure = null;
			if (opportunity == null || !opportunity.FirstGuest
				|| !KingdomSemanticSelection.TryLocateGrowthArrival(system, zone,
					opportunity.RulesVersionAtCreation, opportunity.Ordinal, out cell,
					out semanticFailure))
			{
				if (string.Equals(semanticFailure,
					KingdomSemanticSelection.NoArrivalGroundFailure, StringComparison.Ordinal))
					return StartSimpleArrival(system, zone, tick,
						KingdomGrowthArrivalDisposition.NoGround);
				KingdomLog.Log("first-guest semantic plan refused: " + semanticFailure);
				return ArrivalResult.Failed;
			}
			string id = KingdomLifecycleRules.GrowthArrivalCandidateId(growth.SettlementId,
				sequence);
			string marker = StableId("arrival-marker", id);
			string escrow = "r_TAF_GrowthArrivalEscrow:" + StableId("arrival-escrow", id);
			string beforeOwner = HashText("arrival-create-owner-before", escrow, zone.ZoneID);
			string beforeObject = HashText("arrival-create-object-before", marker,
				opportunity.Blueprint);
			string beforeTopology = ArrivalZoneIdentityHash(zone, null, marker, escrow,
				KingdomGrowthLocationKind.Absent, -1, -1);
			int supportCap = system.SupportedLevel > 0
				? KingdomSubsidenceRules.SlideBeginsAbove(system.SupportedLevel) : int.MaxValue;
			KingdomGrowthArrivalCandidate candidate =
				KingdomLifecycleRules.PrepareGrowthFirstGuestCandidate(growth, marker,
					opportunity.Blueprint, escrow, zone.ZoneID, tick, beforeOwner, beforeObject,
					beforeTopology, opportunity.RulesVersionAtCreation,
					opportunity.EventStreamId, opportunity.EventKindCode, opportunity.Origin,
					opportunity.Creed, opportunity.PersonName, opportunity.Arrived, cell.X, cell.Y,
					opportunity.DueTick, opportunity.IntervalTicks,
					system.Population, KingdomRules.MaxPopulation,
					system.SupportedLevel, supportCap, survey.StoredWater,
					KingdomRules.DramsPerArrival);
			if (candidate == null || !KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(
				growth, candidate)) return ArrivalResult.Failed;
			return ArrivalResult.Deferred;
		}
	}
}
