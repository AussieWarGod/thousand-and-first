using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		private static bool GrowthVariantScalarsValid(KingdomGrowthOperation operation)
		{
			if (!Enum.IsDefined(typeof(KingdomGrowthArrivalDisposition),
				operation.ArrivalDisposition)
				|| !Enum.IsDefined(typeof(KingdomGrowthDeliveryMode), operation.DeliveryMode)
				|| !Enum.IsDefined(typeof(KingdomGrowthDepartureCauseKind),
					operation.DepartureCauseKind)
				|| !KnownOption(operation.ScarcityOptionState)
				|| operation.ScarcityOptionTick < 0L
				|| TooLong(operation.DepartureCause, MaxNameChars)
				|| TooLong(operation.DepartureNote, MaxTextChars)
				|| TooLong(operation.DepartureName, MaxNameChars)
				|| TooLong(operation.DepartureOrigin, MaxNameChars)
				|| TooLong(operation.DepartureCreed, MaxNameChars)
				|| TooLong(operation.MillCropBlueprint, MaxNameChars)
				|| TooLong(operation.MillStapleBlueprint, MaxNameChars)
				|| (operation.TriggeredByOperationId != null
					&& !ValidGeneratedId(operation.TriggeredByOperationId))) return false;
			if (operation.Action == KingdomGrowthAction.Arrival)
			{
				if (operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.None) return false;
			}
			else if (operation.ArrivalDisposition != KingdomGrowthArrivalDisposition.None
				|| operation.ArrivalCandidateId != null) return false;
			if (operation.Action == KingdomGrowthAction.Delivery)
			{
				if (operation.DeliveryMode != KingdomGrowthDeliveryMode.PlainLarder) return false;
			}
			else if (operation.DeliveryMode != KingdomGrowthDeliveryMode.None) return false;
			if (operation.Action == KingdomGrowthAction.Mill)
			{
				if (!ValidName(operation.MillCropBlueprint)
					|| !ValidName(operation.MillStapleBlueprint)) return false;
			}
			else if (operation.MillCropBlueprint != null
				|| operation.MillStapleBlueprint != null) return false;
			bool departure = operation.Action == KingdomGrowthAction.Departure
				|| operation.Action == KingdomGrowthAction.Heartbeat
					&& operation.PopulationDelta < 0;
			if (departure)
			{
				if (operation.DepartureCauseKind == KingdomGrowthDepartureCauseKind.None
					|| !GrowthBoundedPresentString(operation.DepartureCause)
					|| !GrowthBoundedPresentString(operation.DepartureName)
					|| !GrowthBoundedPresentString(operation.DepartureOrigin)
					|| operation.DepartureArrivedTick < 0L
					|| !GrowthBoundedPresentString(operation.DepartureCreed)) return false;
			}
			else if (operation.DepartureCauseKind != KingdomGrowthDepartureCauseKind.None
				|| operation.DepartureCause != null || operation.DepartureNote != null
				|| operation.DepartureName != null || operation.DepartureOrigin != null
				|| operation.DepartureArrivedTick != 0L || operation.DepartureCreed != null
				|| operation.DepartureChronicled || operation.TriggeredByOperationId != null)
				return false;
			return true;
		}

		private static bool GrowthTargetShape(KingdomGrowthOperation operation,
			KingdomGrowthSlotKind slot)
		{
			bool empty = operation.TargetId == null
				&& operation.TargetMarker == null
				&& operation.Blueprint == null && operation.ZoneId == null
				&& operation.TargetTopology == KingdomLifecycleTopology.None
				&& operation.TargetLocation == KingdomGrowthLocationKind.None
				&& operation.TargetOwnerId == null
				&& operation.TargetX == -1 && operation.TargetY == -1;
			if (empty) return slot != KingdomGrowthSlotKind.Field
				&& operation.Action != KingdomGrowthAction.Departure
				&& !(operation.Action == KingdomGrowthAction.Heartbeat
					&& operation.PopulationDelta < 0);
			return ValidRootId(operation.TargetId) && ValidRootId(operation.TargetMarker)
				&& ValidName(operation.Blueprint) && GrowthTopologyValid(operation.TargetTopology,
					operation.TargetOwnerId, operation.ZoneId, operation.TargetX, operation.TargetY)
				&& operation.TargetLocation == GrowthLocationFromTopology(operation.TargetTopology);
		}

		private static bool GrowthGroupsMatchAction(KingdomGrowthOperation operation)
		{
			bool groups;
			switch (operation.Action)
			{
			case KingdomGrowthAction.Heartbeat:
				groups = operation.Outputs.Count == 0
					&& GrowthAllWaterKinds(operation, KingdomGrowthWaterMutationKind.Drain)
					&& GrowthHeartbeatSourcesShape(operation)
					&& (operation.ScarcityOptionState == KingdomLifecycleOptionState.Enabled
						|| operation.ScarcityOptionState == KingdomLifecycleOptionState.Disabled
							&& operation.WaterLegs.Count == 0 && operation.Sources.Count == 0
							&& operation.PopulationDelta == 0);
				break;
			case KingdomGrowthAction.Fetch:
				groups = operation.WaterLegs.Count >= 2 && operation.Sources.Count == 0
					&& operation.Outputs.Count == 0 && GrowthFetchWaterShape(operation);
				break;
			case KingdomGrowthAction.Mill:
				groups = operation.WaterLegs.Count == 0 && operation.Sources.Count > 0
					&& operation.Outputs.Count > 0
					&& GrowthAllObjectKinds(operation.Sources,
						KingdomGrowthObjectMutationKind.DestroyOne)
					&& GrowthAllObjectKinds(operation.Outputs,
						KingdomGrowthObjectMutationKind.Create)
					&& GrowthAllObjectBlueprints(operation.Sources,
						operation.MillCropBlueprint)
					&& GrowthAllObjectBlueprints(operation.Outputs,
						operation.MillStapleBlueprint);
				break;
			case KingdomGrowthAction.Arrival:
				if (operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.Joined)
					groups = operation.WaterLegs.Count > 0 && operation.Sources.Count == 0
						&& operation.Outputs.Count == 0
						&& ValidGeneratedId(operation.ArrivalCandidateId)
						&& GrowthAllWaterKinds(operation, KingdomGrowthWaterMutationKind.Drain);
				else groups = operation.ArrivalDisposition != KingdomGrowthArrivalDisposition.None
					&& operation.WaterLegs.Count == 0 && operation.Sources.Count == 0
					&& operation.Outputs.Count == 0
					&& (operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.NoGround
						|| operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.WaterUnavailable
						|| operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.PopulationCap
						|| operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.SupportCap
							? operation.ArrivalCandidateId == null
							: ValidGeneratedId(operation.ArrivalCandidateId));
				break;
			case KingdomGrowthAction.Departure:
				groups = operation.WaterLegs.Count == 0 && operation.Sources.Count == 1
					&& operation.Outputs.Count == 0
					&& operation.Sources[0].MutationKind ==
						KingdomGrowthObjectMutationKind.Obliterate
					&& operation.Sources[0].BeforeCount == 1
					&& operation.Sources[0].AfterCount == 0
					&& string.Equals(operation.Sources[0].ObjectId, operation.TargetId,
						StringComparison.Ordinal)
					&& string.Equals(operation.Sources[0].Marker, operation.TargetMarker,
						StringComparison.Ordinal)
					&& string.Equals(operation.Sources[0].Blueprint, operation.Blueprint,
						StringComparison.Ordinal)
					&& string.Equals(operation.Sources[0].ZoneId, operation.ZoneId,
						StringComparison.Ordinal)
					&& operation.Sources[0].X == operation.TargetX
					&& operation.Sources[0].Y == operation.TargetY;
				break;
			case KingdomGrowthAction.Delivery:
				groups = operation.WaterLegs.Count == 0 && operation.Sources.Count == 0
					&& operation.Outputs.Count > 0
					&& GrowthAllObjectKinds(operation.Outputs,
						KingdomGrowthObjectMutationKind.Create)
					&& GrowthDeliveryOutputsShape(operation)
					&& operation.DeliveryMode == KingdomGrowthDeliveryMode.PlainLarder;
				break;
			case KingdomGrowthAction.Sow:
				groups = operation.WaterLegs.Count > 0 && operation.Sources.Count > 0
					&& operation.Outputs.Count > 0
					&& GrowthAllWaterKinds(operation, KingdomGrowthWaterMutationKind.Drain)
					&& operation.Sources.Count == 1
					&& operation.Sources[0].MutationKind ==
						KingdomGrowthObjectMutationKind.DestroyOne
					&& GrowthAllObjectKinds(operation.Outputs,
						KingdomGrowthObjectMutationKind.Create);
				break;
			case KingdomGrowthAction.Withdraw:
				groups = operation.WaterLegs.Count == 0 && operation.Sources.Count > 0
					&& operation.Outputs.Count <= 1
					&& GrowthAllObjectKinds(operation.Sources,
						KingdomGrowthObjectMutationKind.Obliterate)
					&& (operation.Outputs.Count == 0 || operation.Outputs[0].MutationKind ==
						KingdomGrowthObjectMutationKind.Create);
				break;
			case KingdomGrowthAction.Ripen:
				groups = operation.WaterLegs.Count == 0 && operation.Sources.Count > 0
					&& operation.Outputs.Count == 0
					&& GrowthAllObjectKinds(operation.Sources,
						KingdomGrowthObjectMutationKind.HarvestableRipeSet);
				break;
			case KingdomGrowthAction.Harvest:
				groups = operation.WaterLegs.Count == 0 && operation.Sources.Count > 0
					&& GrowthAllObjectKinds(operation.Sources,
						KingdomGrowthObjectMutationKind.HarvestableRipeSet)
					&& GrowthAllObjectKinds(operation.Outputs,
						KingdomGrowthObjectMutationKind.Create);
				break;
			case KingdomGrowthAction.Irrigate:
				groups = operation.WaterLegs.Count == 0 && operation.Sources.Count == 0
					&& operation.Outputs.Count == 0;
				break;
			default: return false;
			}
			return groups && GrowthDomainSetMatchesAction(operation)
				&& GrowthActionConservationShape(operation);
		}

		private static bool GrowthArrivalCandidateBindingShape(KingdomGrowthBook book,
			KingdomGrowthOperation operation, bool publication)
		{
			if (operation.Action != KingdomGrowthAction.Arrival)
				return operation.ArrivalCandidateId == null;
			bool needsCandidate = operation.ArrivalDisposition ==
				KingdomGrowthArrivalDisposition.Joined
				|| operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.NoAcceptableHome;
			if (!needsCandidate)
				return operation.ArrivalCandidateId == null && book.ArrivalCandidate == null;
			KingdomGrowthArrivalCandidate candidate = book.ArrivalCandidate;
			if (candidate == null || !ReferenceEquals(book.ArrivalCandidate, candidate)
				|| !string.Equals(candidate.Id, operation.ArrivalCandidateId,
					StringComparison.Ordinal)
				|| candidate.Disposition != operation.ArrivalDisposition) return false;
			if (candidate.Disposition == KingdomGrowthArrivalDisposition.Joined)
			{
				if (!string.Equals(operation.TargetId, candidate.ObjectId, StringComparison.Ordinal)
					|| !string.Equals(operation.TargetMarker, candidate.Marker,
						StringComparison.Ordinal)
					|| !string.Equals(operation.Blueprint, candidate.Blueprint,
						StringComparison.Ordinal)
					|| operation.TargetTopology != KingdomLifecycleTopology.Cell
					|| operation.TargetLocation != KingdomGrowthLocationKind.Cell
					|| operation.TargetOwnerId != null
					|| !string.Equals(operation.ZoneId, candidate.LodgingZoneId,
						StringComparison.Ordinal)
					|| operation.TargetX != candidate.LodgingX
					|| operation.TargetY != candidate.LodgingY) return false;
			}
			if (publication)
				return candidate.Phase == KingdomGrowthArrivalCandidatePhase.Observed
					&& candidate.ConsumingOperationId == null
					&& candidate.ConsumingOperationSequence == 0L;
			if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.Quarantined
				&& operation.Phase != KingdomGrowthPhase.Quarantined) return false;
			KingdomGrowthArrivalCandidatePhase effectivePhase = candidate.Phase ==
				KingdomGrowthArrivalCandidatePhase.Quarantined
					? candidate.EvidencePhase : candidate.Phase;
			if (effectivePhase == KingdomGrowthArrivalCandidatePhase.Observed)
				return candidate.ConsumingOperationId == null
					&& candidate.ConsumingOperationSequence == 0L
					&& (operation.Phase == KingdomGrowthPhase.Prepared
						|| operation.Phase == KingdomGrowthPhase.WaterIntent
						|| operation.Phase == KingdomGrowthPhase.WaterSettled
						|| operation.Phase == KingdomGrowthPhase.Quarantined);
			bool rightIntent = candidate.Disposition == KingdomGrowthArrivalDisposition.Joined
				? effectivePhase == KingdomGrowthArrivalCandidatePhase.ConsumeIntent
				: effectivePhase == KingdomGrowthArrivalCandidatePhase.RefusalIntent;
			return (rightIntent || effectivePhase == KingdomGrowthArrivalCandidatePhase.Settled)
				&& string.Equals(candidate.ConsumingOperationId, operation.Id,
					StringComparison.Ordinal)
				&& candidate.ConsumingOperationSequence == operation.Sequence;
		}

		private static bool GrowthHeartbeatSourcesShape(KingdomGrowthOperation operation)
		{
			if (operation.PopulationDelta != 0 && operation.PopulationDelta != -1) return false;
			int leavers = 0;
			for (int i = 0; i < operation.Sources.Count; i++)
			{
				KingdomGrowthObjectLeg leg = operation.Sources[i];
				if (operation.PopulationDelta < 0
					&& string.Equals(leg.ObjectId, operation.TargetId, StringComparison.Ordinal))
				{
					if (leg.MutationKind != KingdomGrowthObjectMutationKind.Obliterate
						|| leg.BeforeCount != 1 || leg.AfterCount != 0
						|| !string.Equals(leg.Marker, operation.TargetMarker,
							StringComparison.Ordinal)
						|| !string.Equals(leg.Blueprint, operation.Blueprint,
							StringComparison.Ordinal)
						|| !string.Equals(leg.ZoneId, operation.ZoneId,
							StringComparison.Ordinal)
						|| leg.X != operation.TargetX || leg.Y != operation.TargetY
						|| ++leavers != 1) return false;
				}
				else if (leg.MutationKind != KingdomGrowthObjectMutationKind.DestroyOne) return false;
			}
			return leavers == (operation.PopulationDelta < 0 ? 1 : 0);
		}

	}
}
