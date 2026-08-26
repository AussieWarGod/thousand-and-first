using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Conversations;
using XRL.World.Parts;

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
				|| tick < growth.NextArrivalTick) return ArrivalResult.Failed;
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
			long sequence = growth.ArrivalCandidateNextSequence;
			KingdomSemanticPersonPlan person;
			string semanticFailure;
			if (!KingdomSemanticSelection.TryPrepareGrowthArrival(system, zone, sequence,
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
			KingdomGrowthArrivalCandidate candidate =
				KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth, marker, blueprint,
					escrow, zone.ZoneID, tick, beforeOwner, beforeObject, beforeTopology,
					person.RulesVersion, person.StreamId, person.EventKind, person.Origin,
					string.IsNullOrEmpty(person.Creed) ? "-" : person.Creed, person.Name,
					person.Arrived, person.X, person.Y);
			if (candidate == null || !KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(
				growth, candidate)) return ArrivalResult.Failed;
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

		private static ArrivalResult ReconcileArrival(KingdomSystem system, Zone zone,
			KingdomSurvey survey, long tick, out ArrivalRefusal refusal, Cell preferred = null,
			bool AllowCandidateConsumption = true)
		{
			refusal = default(ArrivalRefusal);
			KingdomGrowthBook growth = system.LifecycleBook.Growth;
			try
			{
				KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
				if (candidate == null)
				{
					if (growth.ArrivalOp == null) return ArrivalResult.Failed;
					return CompleteArrivalOperation(system, zone, survey, growth.ArrivalOp,
						null, tick, out refusal);
				}
				if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.Quarantined)
					return ArrivalResult.Failed;
				if (candidate.LegacyGrowthV1UnboundZone
					&& !KingdomLifecycleRules.BindLegacyGrowthArrivalCandidateZone(growth,
						candidate, zone.ZoneID, tick))
					return CandidateFault(growth, candidate,
						"historical candidate origin zone could not bind");
				if (candidate.LegacySemanticPlan)
				{
					string migrationFailure;
					if (!TryMigrateArrivalSemanticPlan(system, zone, growth, candidate, tick,
						out migrationFailure))
					{
						if (string.Equals(migrationFailure,
							KingdomSemanticSelection.NoArrivalGroundFailure,
							StringComparison.Ordinal)) return ArrivalResult.Deferred;
						return CandidateFault(growth, candidate,
							migrationFailure ?? "historical semantic plan could not migrate");
					}
				}
				if (!string.Equals(candidate.LodgingZoneId, zone.ZoneID,
					StringComparison.Ordinal)) return ArrivalResult.Deferred;
				GameObject settler = null;
				if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.Prepared
					|| candidate.Phase == KingdomGrowthArrivalCandidatePhase.CreateIntent)
				{
					if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.Prepared
						&& !KingdomLifecycleRules.BeginGrowthArrivalCandidateCreate(growth,
							candidate, tick)) return CandidateFault(growth, candidate,
							"candidate create intent could not publish");
					if (!TryExactArrivalRoot(candidate, out settler))
					{
						settler = GameObject.Create(candidate.Blueprint);
						if (!GameObject.Validate(settler) || settler.Count != 1)
							return CandidateFault(growth, candidate,
								"candidate create callback did not make one exact object");
						settler.SetStringProperty(ArrivalMarkerProperty, candidate.Marker);
						if (!RootArrivalCandidate(candidate, settler))
							return CandidateFault(growth, candidate,
								"candidate escrow root did not retain the exact object");
					}
					if (!ExactFreshEscrowedCandidate(candidate, settler))
						return CandidateFault(growth, candidate,
							"candidate escrow object is missing, replaced, stacked, or placed");
					if (!PrepareArrivalPersonPlan(system, settler, candidate))
						return CandidateFault(growth, candidate,
							"candidate person plan could not freeze before creation receipt");
					string afterOwner = ArrivalObjectHash(candidate, settler,
						KingdomGrowthLocationKind.Escrow, -1, -1);
					string afterObject = ArrivalPersonHash(settler);
					string afterTopology = ArrivalZoneIdentityHash(zone, settler,
						candidate.Marker, candidate.EscrowKey,
						KingdomGrowthLocationKind.Escrow, -1, -1);
					if (!KingdomLifecycleRules.CommitGrowthArrivalCandidateCreate(growth,
						candidate, settler.ID, afterOwner, afterObject, afterTopology,
						ReferenceHash("candidate-create", candidate, settler), true, tick))
						return CandidateFault(growth, candidate,
							"candidate create receipt did not commit");
				}
				if (!TryArrivalObject(candidate, zone, out settler))
					return CandidateFault(growth, candidate,
						"saved candidate phase cannot prove its exact object");
				if ((candidate.Phase == KingdomGrowthArrivalCandidatePhase.Escrowed
					|| candidate.Phase == KingdomGrowthArrivalCandidatePhase.LodgingIntent
					|| candidate.Phase == KingdomGrowthArrivalCandidatePhase.Observed)
					&& !ExactCreatedCandidate(candidate, settler, zone))
					return CandidateFault(growth, candidate,
						"candidate person plan or creation endpoint changed after receipt");
				if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.Escrowed)
				{
					Cell cell = zone.GetCell(candidate.ArrivalX, candidate.ArrivalY);
					if (cell == null || !cell.IsEmpty() || !cell.IsPassable()
						|| cell.HasObjectWithPart("LiquidVolume")) return ArrivalResult.Deferred;
					KingdomLodgingRules.UnhousedReason ignoredReason;
					string before;
					KingdomLodging.ObservePreparedArrival(system, zone, settler,
						PlannedCreed(settler),
						out ignoredReason, out before);
					if (before == null)
						return CandidateFault(growth, candidate,
							"lodging observation snapshot could not be frozen");
					if (!KingdomLifecycleRules.BeginGrowthArrivalLodgingObservation(growth,
						candidate, zone.ZoneID, cell.X, cell.Y, before, tick))
						return CandidateFault(growth, candidate,
							"lodging observation intent did not publish");
				}
				if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.LodgingIntent)
				{
					KingdomLodgingRules.UnhousedReason reason;
					string observed;
					bool joined = KingdomLodging.ObservePreparedArrival(system, zone, settler,
						PlannedCreed(settler),
						out reason, out observed);
					if (!string.Equals(candidate.LodgingZoneId, zone.ZoneID,
						StringComparison.Ordinal) || zone.GetCell(candidate.LodgingX,
						candidate.LodgingY) == null || !string.Equals(candidate.LodgingBeforeGraphHash,
						observed, StringComparison.Ordinal))
						return CandidateFault(growth, candidate,
							"saved lodging observation no longer matches the real settlement");
					KingdomGrowthArrivalDisposition disposition = joined
						? KingdomGrowthArrivalDisposition.Joined
						: KingdomGrowthArrivalDisposition.NoAcceptableHome;
					KingdomGrowthArrivalRefusalReason frozen = joined
						? KingdomGrowthArrivalRefusalReason.None : ArrivalRefusalReason(reason);
					string receipt = HashText("arrival-lodging-observation",
						candidate.LodgingBeforeGraphHash, disposition.ToString(), frozen.ToString());
					if (!KingdomLifecycleRules.CommitGrowthArrivalLodgingObservation(growth,
						candidate, disposition, frozen, receipt,
						ReferenceHash("candidate-lodging", candidate, settler), true, tick))
						return CandidateFault(growth, candidate,
							"lodging observation receipt did not commit");
				}
				if (candidate.Disposition == KingdomGrowthArrivalDisposition.NoAcceptableHome)
				{
					refusal.NoAcceptableHome = true;
					refusal.Reason = LodgingRefusalReason(candidate.RefusalReason);
				}
				if (growth.ArrivalOp == null
					&& candidate.Phase == KingdomGrowthArrivalCandidatePhase.Observed)
				{
					if (!AllowCandidateConsumption || growth.WorkPaused)
						return ArrivalResult.Deferred;
					if (!PrepareCandidateArrivalOperation(system, zone, survey, candidate,
						settler, tick)) return CandidateFault(growth, candidate,
							"candidate arrival operation could not publish");
				}
				if (growth.ArrivalOp == null)
				{
					if (candidate.Phase != KingdomGrowthArrivalCandidatePhase.Settled)
						return CandidateFault(growth, candidate,
							"candidate lost its consuming arrival operation");
					return RetireArrivalCandidate(system, zone, growth, candidate)
						? CandidateResult(candidate) : ArrivalResult.Failed;
				}
				return CompleteArrivalOperation(system, zone, survey, growth.ArrivalOp,
					candidate, tick, out refusal);
			}
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst transactional arrival", error);
				return QuarantineArrival(growth, "arrival callback threw: " + error.Message);
			}
		}
	}
}
