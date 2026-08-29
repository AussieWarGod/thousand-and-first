using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		private static ArrivalResult ResolveOrStartArrival(KingdomSystem system, Zone zone,
			KingdomSurvey survey, long tick, out ArrivalRefusal refusal)
		{
			refusal = default(ArrivalRefusal);
			KingdomGrowthBook growth = system?.LifecycleBook?.Growth;
			string settlementId = system?.CurrentSettlementId;
			if (growth == null || string.IsNullOrEmpty(settlementId)
				|| !KingdomLifecycleRules.CanOwnGrowthAuthority(system.LifecycleBook)
				|| !KingdomLifecycleRules.CanOwnGrowthAuthority(growth, settlementId)
				|| system.NextArrivalTick != growth.NextArrivalTick
				|| tick < growth.NextArrivalTick
				|| !growth.ArrivalCadenceMigrationPending
					&& growth.ArrivalOpportunity == null) return ArrivalResult.Failed;
			if (growth.ArrivalCandidate != null || growth.ArrivalOp != null)
				return ReconcileArrival(system, zone, survey, tick, out refusal);
			if (system.Population >= KingdomRules.MaxPopulation)
				return StartSimpleArrival(system, zone, tick,
					KingdomGrowthArrivalDisposition.PopulationCap);
			if (system.SupportedLevel > 0 && system.Population >=
				KingdomSubsidenceRules.SlideBeginsAbove(system.SupportedLevel))
				return StartSimpleArrival(system, zone, tick,
					KingdomGrowthArrivalDisposition.SupportCap);
			if (survey == null || survey.StoredWater < KingdomRules.DramsPerArrival)
				return StartSimpleArrival(system, zone, tick,
					KingdomGrowthArrivalDisposition.WaterUnavailable);
			if (!growth.ArrivalCadenceMigrationPending && growth.ArrivalOpportunity.FirstGuest)
				return StartFirstGuestOpportunity(system, zone, survey, tick);
			long sequence = growth.ArrivalCandidateNextSequence;
			KingdomSemanticPersonPlan person;
			string semanticFailure;
			if (!growth.ArrivalCadenceMigrationPending)
			{
				KingdomGrowthArrivalOpportunity opportunity = growth.ArrivalOpportunity;
				Cell chosen;
				if (!KingdomSemanticSelection.TryLocateGrowthArrival(system, zone,
					opportunity.RulesVersionAtCreation, opportunity.Ordinal, out chosen,
					out semanticFailure))
				{
					if (string.Equals(semanticFailure,
						KingdomSemanticSelection.NoArrivalGroundFailure, StringComparison.Ordinal))
						return StartSimpleArrival(system, zone, tick,
							KingdomGrowthArrivalDisposition.NoGround);
					KingdomLog.Log("growth arrival placement refused: " + semanticFailure);
					return ArrivalResult.Failed;
				}
				person = new KingdomSemanticPersonPlan
				{
					RulesVersion = opportunity.RulesVersionAtCreation,
					Sequence = opportunity.Ordinal > (ulong)long.MaxValue ? 0L
						: (long)opportunity.Ordinal,
					StreamId = opportunity.EventStreamId, EventKind = opportunity.EventKindCode,
					Blueprint = opportunity.Blueprint, Origin = opportunity.Origin,
					Creed = opportunity.Creed, Name = opportunity.PersonName,
					Arrived = opportunity.Arrived, X = chosen.X, Y = chosen.Y
				};
			}
			else if (!KingdomSemanticSelection.TryPrepareGrowthArrival(system, zone, sequence,
				tick, out person, out semanticFailure))
			{
				if (string.Equals(semanticFailure,
					KingdomSemanticSelection.NoArrivalGroundFailure, StringComparison.Ordinal))
					return StartSimpleArrival(system, zone, tick,
						KingdomGrowthArrivalDisposition.NoGround);
				KingdomLog.Log("growth arrival semantic plan refused: " + semanticFailure);
				return ArrivalResult.Failed;
			}
			Cell cell = zone.GetCell(person.X, person.Y);
			if (cell == null) return ArrivalResult.Failed;
			string id = KingdomLifecycleRules.GrowthArrivalCandidateId(growth.SettlementId,
				sequence);
			string marker = StableId("arrival-marker", id);
			string escrow = "r_TAF_GrowthArrivalEscrow:" + StableId("arrival-escrow", id);
			string blueprint = person.Blueprint;
			string beforeOwner = HashText("arrival-create-owner-before", escrow, zone.ZoneID);
			string beforeObject = HashText("arrival-create-object-before", marker, blueprint);
			string beforeTopology = ArrivalZoneIdentityHash(zone, null, marker, escrow,
				KingdomGrowthLocationKind.Absent, -1, -1);
			KingdomGrowthArrivalCandidate ordinary =
				KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth, marker, blueprint,
					escrow, zone.ZoneID, tick, beforeOwner, beforeObject, beforeTopology,
					person.RulesVersion, person.StreamId, person.EventKind, person.Origin,
					string.IsNullOrEmpty(person.Creed) ? "-" : person.Creed, person.Name,
					person.Arrived, person.X, person.Y);
			if (ordinary == null || !KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(
				growth, ordinary)) return ArrivalResult.Failed;
			return ReconcileArrival(system, zone, survey, tick, out refusal, cell);
		}

		private static ArrivalResult StartSimpleArrival(KingdomSystem system, Zone zone,
			long tick, KingdomGrowthArrivalDisposition disposition)
		{
			KingdomGrowthBook growth = system.LifecycleBook.Growth;
			KingdomGrowthOperation operation = KingdomLifecycleRules.PrepareGrowthOperation(
				growth, KingdomGrowthAction.Arrival, null, tick);
			if (operation == null) return ArrivalResult.Failed;
			operation.ArrivalDisposition = disposition;
			if (disposition == KingdomGrowthArrivalDisposition.NoGround
				&& !system.NoRoomAnnounced
				&& !AppendArrivalOutbox(system, operation, "no-ground",
					"a settler reached " + KingdomPresentation.Rich(system.KingdomDisplayName)
						+ " and found nowhere to stand",
					"{{r|A settler came and found nowhere to stand. There is no open ground left here.}}"))
				return ArrivalResult.Failed;
			if (!KingdomLifecycleRules.TryPublishGrowth(growth, operation))
				return ArrivalResult.Failed;
			ArrivalResult result = ReconcileArrival(system, zone, null, tick,
				out ArrivalRefusal ignored);
			if (result == ArrivalResult.NoGround) system.NoRoomAnnounced = true;
			return result;
		}
	}
}
