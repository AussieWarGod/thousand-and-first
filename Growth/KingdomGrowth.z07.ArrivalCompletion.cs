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

		private static ArrivalResult CompleteArrivalOperation(KingdomSystem system, Zone zone,
			KingdomSurvey survey, KingdomGrowthOperation operation,
			KingdomGrowthArrivalCandidate candidate, long tick, out ArrivalRefusal refusal)
		{
			refusal = default(ArrivalRefusal);
			KingdomGrowthBook growth = system.LifecycleBook.Growth;
			if (operation == null || operation.Phase == KingdomGrowthPhase.Quarantined)
				return ArrivalResult.Failed;
			if (candidate != null && candidate.Disposition ==
				KingdomGrowthArrivalDisposition.NoAcceptableHome)
			{
				refusal.NoAcceptableHome = true;
				refusal.Reason = LodgingRefusalReason(candidate.RefusalReason);
			}
			if (operation.Phase == KingdomGrowthPhase.Prepared
				&& operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.Joined
				&& !KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
					KingdomGrowthPhase.WaterIntent, tick))
				return OperationFault(growth, operation, "water phase did not open");
			if (operation.Phase == KingdomGrowthPhase.WaterIntent)
			{
				if (!ReconcileArrivalWater(growth, operation, zone, survey))
					return OperationFault(growth, operation,
						"real water vessels did not match the saved arrival debit");
				if (!KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
					KingdomGrowthPhase.WaterSettled, tick))
					return OperationFault(growth, operation, "water settlement did not publish");
			}
			if (candidate != null && candidate.Phase !=
				KingdomGrowthArrivalCandidatePhase.Settled)
			{
				if (!ReconcileCandidateDisposition(growth, operation, candidate, zone, tick))
					return OperationFault(growth, operation,
						"candidate disposition did not prove one exact object");
			}
			if (operation.Phase == KingdomGrowthPhase.Prepared
				|| operation.Phase == KingdomGrowthPhase.WaterSettled)
			{
				KingdomGrowthPhase next = operation.ArrivalDisposition ==
					KingdomGrowthArrivalDisposition.Joined
						? KingdomGrowthPhase.DomainIntent : KingdomGrowthPhase.ClockIntent;
				if (!KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation, next, tick))
					return OperationFault(growth, operation, "arrival domain/clock phase did not open");
			}
			if (operation.Phase == KingdomGrowthPhase.DomainIntent)
			{
				GameObject settler;
				if (candidate == null || !TryArrivalObject(candidate, zone, out settler)
					|| !ReconcileArrivalDomains(system, growth, operation, settler))
					return OperationFault(growth, operation,
						"arrival domain CAS found a third real-world state");
				if (!KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
					KingdomGrowthPhase.DomainSettled, tick))
					return OperationFault(growth, operation, "arrival domain settlement did not publish");
			}
			if (operation.Phase == KingdomGrowthPhase.DomainSettled
				&& operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.Joined)
			{
				GameObject settler;
				Simulation.City.KingdomCityBook residentBook;
				int residentId;
				if (candidate == null || !TryArrivalObject(candidate, zone, out settler)
					|| !Simulation.City.KingdomResidents.TryEnsureRow(system, settler,
						settler.GetStringProperty(ArrivalOriginPlanProperty),
						settler.GetStringProperty(ArrivalDatePlanProperty), operation.CreatedTick,
						out residentBook, out residentId))
					return OperationFault(growth, operation,
						"accepted arrival did not publish one resident row and binding");
				// The body entered the cell while it was still an un-enrolled candidate. Domain
				// settlement and resident binding change every civic index that later lanes consume;
				// publish that final identity before AssignWork, lodging, offices, or faith read it.
				if (survey == null || !survey.ObserveChanged(settler))
					return OperationFault(growth, operation,
						"accepted arrival could not refresh the active civic survey");
			}
			if (operation.Phase == KingdomGrowthPhase.DomainSettled
				&& !KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
					KingdomGrowthPhase.ClockIntent, tick))
				return OperationFault(growth, operation, "arrival clock phase did not open");
			if (operation.Phase == KingdomGrowthPhase.ClockIntent
				&& !ReconcileArrivalClock(system, growth, operation))
				return OperationFault(growth, operation,
					"real arrival clock did not match its saved before/after CAS");
			if (operation.Phase == KingdomGrowthPhase.ClockIntent
				&& !KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
					KingdomGrowthPhase.Sinks, tick))
				return OperationFault(growth, operation, "arrival outbox phase did not open");
			if (operation.Phase == KingdomGrowthPhase.Sinks)
			{
				if (!ReconcileArrivalOutbox(system, growth, operation))
					return OperationFault(growth, operation,
						"arrival outbox did not match its saved before/after lists");
				if (!KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
					KingdomGrowthPhase.Terminal, tick))
					return OperationFault(growth, operation, "arrival terminal did not publish");
			}
			ArrivalResult result = OperationResult(operation.ArrivalDisposition);
			if (operation.Phase == KingdomGrowthPhase.Terminal)
			{
				if (!KingdomLifecycleRules.RetireGrowth(growth, operation, tick))
					return OperationFault(growth, operation,
						"arrival operation retirement failed");
				system.NextArrivalTick = growth.NextArrivalTick;
			}
			if (candidate != null)
			{
				if (!RetireArrivalCandidate(system, zone, growth, candidate))
					return ArrivalResult.Failed;
				if (result == ArrivalResult.Refused) system.NoRoomAnnounced = true;
				else if (result == ArrivalResult.Joined) system.NoRoomAnnounced = false;
			}
			else if (result == ArrivalResult.NoGround) system.NoRoomAnnounced = true;
			return result;
		}

		private static bool ReconcileArrivalWater(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, Zone zone, KingdomSurvey survey)
		{
			while (operation.WaterCursor < operation.WaterLegs.Count)
			{
				int ordinal = operation.WaterCursor;
				KingdomGrowthWaterLeg leg = operation.WaterLegs[ordinal];
				GameObject owner = zone?.FindObjectByID(leg.ContainerId);
				LiquidVolume vessel = owner?.GetPart<LiquidVolume>();
				if (!ExactWaterEndpoint(zone, owner, vessel, leg, leg.Before))
				{
					if (leg.State == KingdomLifecyclePhysicalState.Intent
						&& ExactWaterEndpoint(zone, owner, vessel, leg, leg.After))
					{
						if (!KingdomLifecycleRules.CommitGrowthWaterCallback(growth,
							operation, ordinal, leg.ContainerId,
							ReferenceHash("arrival-water", leg, vessel), true,
							leg.AfterOwnerGraphHash, leg.AfterPartGraphHash,
							leg.AfterTopologyHash)) return false;
						continue;
					}
					return false;
				}
				if (leg.State == KingdomLifecyclePhysicalState.Prepared
					&& !KingdomLifecycleRules.BeginGrowthWaterCallback(growth, operation,
						ordinal)) return false;
				int removed = KingdomLiquids.Drain(vessel, leg.Delta);
				if (removed != leg.Delta || !ExactWaterEndpoint(zone, owner, vessel, leg,
					leg.After)) return false;
				if (survey != null && survey.Stores.Contains(vessel))
				{
					survey.StoredWater -= removed;
					survey.StorageSpace += removed;
				}
				if (!KingdomLifecycleRules.CommitGrowthWaterCallback(growth, operation,
					ordinal, leg.ContainerId, ReferenceHash("arrival-water", leg, vessel),
					true, leg.AfterOwnerGraphHash, leg.AfterPartGraphHash,
					leg.AfterTopologyHash)) return false;
			}
			return operation.WaterCursor == operation.WaterLegs.Count;
		}

		private static bool ReconcileCandidateDisposition(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, KingdomGrowthArrivalCandidate candidate,
			Zone zone, long tick)
		{
			GameObject settler;
			TryArrivalObject(candidate, zone, out settler);
			bool joined = candidate.Disposition == KingdomGrowthArrivalDisposition.Joined;
			if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.Observed)
			{
				if (!ExactEscrowedCandidate(candidate, settler)) return false;
				string beforeOwner = ArrivalObjectHash(candidate, settler,
					KingdomGrowthLocationKind.Escrow, -1, -1);
				string beforeObject = ArrivalPersonHash(settler);
				string beforeTopology = ArrivalZoneIdentityHash(zone, settler,
					candidate.Marker, candidate.EscrowKey,
					KingdomGrowthLocationKind.Escrow, -1, -1);
				string afterOwner = joined ? ArrivalObjectHash(candidate, settler,
					KingdomGrowthLocationKind.Cell, candidate.LodgingX, candidate.LodgingY)
					: HashText("arrival-object-absent", candidate.ObjectId, candidate.Marker,
						candidate.Blueprint);
				string afterObject = joined ? beforeObject : HashText(
					"arrival-person-absent", candidate.ObjectId, candidate.Marker,
					candidate.Blueprint);
				string afterTopology = ArrivalTopologyHash(zone, candidate.ObjectId,
					candidate.Marker, candidate.EscrowKey,
					joined ? KingdomGrowthLocationKind.Cell
						: KingdomGrowthLocationKind.Graveyard,
					joined ? candidate.LodgingX : -1, joined ? candidate.LodgingY : -1);
				if (!KingdomLifecycleRules.BeginGrowthArrivalCandidateDisposition(growth,
					candidate, operation.Id, joined
						? KingdomGrowthObjectMutationKind.CellAdd
						: KingdomGrowthObjectMutationKind.Obliterate,
					joined ? KingdomGrowthLocationKind.Cell
						: KingdomGrowthLocationKind.Graveyard,
					null, joined ? candidate.LodgingZoneId : null,
					joined ? candidate.LodgingX : -1, joined ? candidate.LodgingY : -1,
					beforeOwner, afterOwner, beforeObject, afterObject,
					beforeTopology, afterTopology, tick)) return false;
			}
			KingdomGrowthArrivalCandidatePhase phase = candidate.Phase;
			if (phase != KingdomGrowthArrivalCandidatePhase.ConsumeIntent
				&& phase != KingdomGrowthArrivalCandidatePhase.RefusalIntent) return false;
			KingdomGrowthObjectCallbackStep step = candidate.DispositionStep;
			bool beforeEndpoint = ExactDispositionEndpoint(candidate, settler, zone, step, false);
			bool afterEndpoint = ExactDispositionEndpoint(candidate, settler, zone, step, true);
			if (!beforeEndpoint && !afterEndpoint) return false;
			if (beforeEndpoint && joined)
			{
				if (!ArrivalCellIsStillOpen(zone?.GetCell(candidate.LodgingX,
					candidate.LodgingY))) return false;
				Cell cell = zone?.GetCell(candidate.LodgingX, candidate.LodgingY);
				GameObject accepted = null;
				try
				{
					accepted = cell.AddObject(settler, NoStack: true, Silent: true);
				}
				finally
				{
					KingdomSurvey.ObserveAddResultInActive(zone, settler, accepted);
				}
				if (!ReferenceEquals(accepted, settler)) return false;
				settler.MakeActive();
			}
			else if (beforeEndpoint)
			{
				try { settler.Obliterate(); }
				finally { KingdomSurvey.ObserveCurrentTopologyInActive(zone, settler); }
			}
			if (!ExactDispositionEndpoint(candidate, settler, zone, step, true)) return false;
			string callbackReference = joined
				? ReferenceHash("candidate-disposition", candidate, settler)
				: HashText("candidate-disposition-absence", candidate.Id, candidate.ObjectId,
					candidate.Marker, candidate.Blueprint);
			return KingdomLifecycleRules.CommitGrowthArrivalCandidateDisposition(growth,
				candidate, callbackReference, joined,
				tick);
		}
	}
}
