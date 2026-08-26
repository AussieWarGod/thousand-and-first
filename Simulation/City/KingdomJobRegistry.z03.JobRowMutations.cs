using System;
using System.Collections.Generic;
#if TAF_TESTS
using System.IO;
using System.Text;
#endif

using ThousandAndFirst.Simulation.Kernel;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	internal readonly partial struct KingdomJobRow
	{
		internal int LegCount
		{
			get { return legCount; }
		}

		internal bool TryLeg(int index, out KingdomLeg leg)
		{
			leg = default(KingdomLeg);
			if (legs == null || index < 0 || index >= legCount || index >= legs.Length)
			{
				return false;
			}
			leg = legs[index];
			return true;
		}

		/// <summary>The legs as the itinerary rules take them. A copy, because a row that handed out
		/// its own array would be a frozen row anybody could edit.</summary>
		internal KingdomLeg[] Legs()
		{
			KingdomLeg[] copy = new KingdomLeg[legCount];
			for (int i = 0; i < legCount && legs != null && i < legs.Length; i++)
			{
				copy[i] = legs[i];
			}
			return copy;
		}

		internal KingdomJobRow WithStatus(KingdomJobStatus status)
		{
			return new KingdomJobRow(JobId, Kind, Cargo, CargoAmount, SourceZoneId, DestZoneId, StartTick,
				WalkTicksPerCell, status, OriginCode, DepositLegIndex, legs, legCount, SubjectId,
				SubjectName, TargetName, DueTick, WaterCost, ProvisionCost, OutcomeCode,
				DeliverySourceEndpointId, DeliverySourceObjectId, DeliverySourceX, DeliverySourceY,
				DeliveryTargetEndpointId, DeliveryTargetObjectId, DeliveryTargetX, DeliveryTargetY,
				DeliverySourceBeforeAmount, DeliveryTripId,
				DeliveryStopOrdinal, DeliveryPhase, DeliveryCargoAuthority,
				DeliveryOwnerOperationId, DeliveryOwnerManifestVersion,
				DeliveryOwnerManifestDigest, DeliveryOwnerManifestRevision,
				DeliveryManifestSourceStart, DeliveryManifestSourceCount,
				DeliveryTargetBeforeAmount, DeliveryTargetReceiptState);
		}

		internal KingdomJobRow WithLegs(KingdomLeg[] next, int count)
		{
			return new KingdomJobRow(JobId, Kind, Cargo, CargoAmount, SourceZoneId, DestZoneId, StartTick,
				WalkTicksPerCell, Status, OriginCode, DepositLegIndex, next, count, SubjectId,
				SubjectName, TargetName, DueTick, WaterCost, ProvisionCost, OutcomeCode,
				DeliverySourceEndpointId, DeliverySourceObjectId, DeliverySourceX, DeliverySourceY,
				DeliveryTargetEndpointId, DeliveryTargetObjectId, DeliveryTargetX, DeliveryTargetY,
				DeliverySourceBeforeAmount, DeliveryTripId,
				DeliveryStopOrdinal, DeliveryPhase, DeliveryCargoAuthority,
				DeliveryOwnerOperationId, DeliveryOwnerManifestVersion,
				DeliveryOwnerManifestDigest, DeliveryOwnerManifestRevision,
				DeliveryManifestSourceStart, DeliveryManifestSourceCount,
				DeliveryTargetBeforeAmount, DeliveryTargetReceiptState);
		}

		/// <summary>Rewrites delivery flavour, or the expedition dispatch phase carried in this
		/// otherwise-unused column, without changing identity, quote, cargo, or itinerary.</summary>
		internal KingdomJobRow WithOriginCode(int originCode)
		{
			return new KingdomJobRow(JobId, Kind, Cargo, CargoAmount, SourceZoneId, DestZoneId,
				StartTick, WalkTicksPerCell, Status, originCode, DepositLegIndex, legs, legCount,
				SubjectId, SubjectName, TargetName, DueTick, WaterCost, ProvisionCost, OutcomeCode,
				DeliverySourceEndpointId, DeliverySourceObjectId, DeliverySourceX, DeliverySourceY,
				DeliveryTargetEndpointId, DeliveryTargetObjectId, DeliveryTargetX, DeliveryTargetY,
				DeliverySourceBeforeAmount, DeliveryTripId,
				DeliveryStopOrdinal, DeliveryPhase, DeliveryCargoAuthority,
				DeliveryOwnerOperationId, DeliveryOwnerManifestVersion,
				DeliveryOwnerManifestDigest, DeliveryOwnerManifestRevision,
				DeliveryManifestSourceStart, DeliveryManifestSourceCount,
				DeliveryTargetBeforeAmount, DeliveryTargetReceiptState);
		}

		/// <summary>Freezes one no-body terminal expedition result before resident standing or
		/// binding authority changes. <paramref name="resolutionTick"/> replaces the now-irrelevant
		/// return due date and becomes the immutable date used by every telling retry.</summary>
		internal KingdomJobRow WithExpeditionResolution(int outcomeCode, long resolutionTick,
			string resolutionZoneId)
		{
			return new KingdomJobRow(JobId, Kind, Cargo, CargoAmount, SourceZoneId,
				resolutionZoneId,
				StartTick, WalkTicksPerCell, Status,
				(int)KingdomExpeditionPhase.ResolutionPrepared, DepositLegIndex, legs, legCount,
				SubjectId, SubjectName, TargetName, resolutionTick, WaterCost, ProvisionCost,
				outcomeCode, DeliverySourceEndpointId, DeliverySourceObjectId,
				DeliverySourceX, DeliverySourceY, DeliveryTargetEndpointId,
				DeliveryTargetObjectId, DeliveryTargetX, DeliveryTargetY,
				DeliverySourceBeforeAmount, DeliveryTripId, DeliveryStopOrdinal, DeliveryPhase,
				DeliveryCargoAuthority, DeliveryOwnerOperationId, DeliveryOwnerManifestVersion,
				DeliveryOwnerManifestDigest, DeliveryOwnerManifestRevision,
				DeliveryManifestSourceStart, DeliveryManifestSourceCount,
				DeliveryTargetBeforeAmount, DeliveryTargetReceiptState);
		}

		/// <summary>
		/// The same job carrying more.
		/// <para>
		/// W6, LIVING-CITY-ARCHITECTURE &sect;3.10(4): capacity-bound batching is not a second
		/// planner bolted on beside the itinerary — it is a load added to a trip that is already
		/// running to the same store. The legs are untouched, because the route did not change;
		/// only what is on the carrier's back did.
		/// </para>
		/// </summary>
		internal KingdomJobRow WithCargo(int cargoAmount)
		{
			return new KingdomJobRow(JobId, Kind, Cargo, cargoAmount, SourceZoneId, DestZoneId, StartTick,
				WalkTicksPerCell, Status, OriginCode, DepositLegIndex, legs, LegCount, SubjectId,
				SubjectName, TargetName, DueTick, WaterCost, ProvisionCost, OutcomeCode,
				DeliverySourceEndpointId, DeliverySourceObjectId, DeliverySourceX, DeliverySourceY,
				DeliveryTargetEndpointId, DeliveryTargetObjectId, DeliveryTargetX, DeliveryTargetY,
				DeliverySourceBeforeAmount, DeliveryTripId,
				DeliveryStopOrdinal, DeliveryPhase, DeliveryCargoAuthority,
				DeliveryOwnerOperationId, DeliveryOwnerManifestVersion,
				DeliveryOwnerManifestDigest, DeliveryOwnerManifestRevision,
				DeliveryManifestSourceStart, DeliveryManifestSourceCount,
				DeliveryTargetBeforeAmount, DeliveryTargetReceiptState);
		}

		/// <summary>The cargo has landed once the deposit leg has been finished. Copy-on-write, so
		/// the amount on the row is what LEFT and this is what is still on the carrier's back.</summary>
		internal KingdomJobRow WithCargoLanded()
		{
			return new KingdomJobRow(JobId, Kind, Cargo, 0, SourceZoneId, DestZoneId, StartTick,
				WalkTicksPerCell, Status, OriginCode, DepositLegIndex, legs, legCount, SubjectId,
				SubjectName, TargetName, DueTick, WaterCost, ProvisionCost, OutcomeCode,
				DeliverySourceEndpointId, DeliverySourceObjectId, DeliverySourceX, DeliverySourceY,
				DeliveryTargetEndpointId, DeliveryTargetObjectId, DeliveryTargetX, DeliveryTargetY,
				DeliverySourceBeforeAmount, DeliveryTripId,
				DeliveryStopOrdinal, DeliveryPhase, DeliveryCargoAuthority,
				DeliveryOwnerOperationId, DeliveryOwnerManifestVersion,
				DeliveryOwnerManifestDigest, DeliveryOwnerManifestRevision,
				DeliveryManifestSourceStart, DeliveryManifestSourceCount,
				DeliveryTargetBeforeAmount, DeliveryTargetReceiptState);
		}

		internal KingdomJobRow WithDeliveryPlan(long startTick, int originCode,
			KingdomLeg[] route, int routeCount, long sourceBeforeAmount, int tripId,
			int stopOrdinal, KingdomDeliveryPhase phase)
		{
			return new KingdomJobRow(JobId, Kind, Cargo, CargoAmount, SourceZoneId, DestZoneId,
				startTick, WalkTicksPerCell, Status, originCode, routeCount - 1, route, routeCount,
				SubjectId, SubjectName, TargetName, DueTick, WaterCost, ProvisionCost, OutcomeCode,
				DeliverySourceEndpointId, DeliverySourceObjectId, DeliverySourceX, DeliverySourceY,
				DeliveryTargetEndpointId, DeliveryTargetObjectId, DeliveryTargetX, DeliveryTargetY,
				sourceBeforeAmount, tripId, stopOrdinal, phase,
				DeliveryCargoAuthority, DeliveryOwnerOperationId, DeliveryOwnerManifestVersion,
				DeliveryOwnerManifestDigest, DeliveryOwnerManifestRevision,
				DeliveryManifestSourceStart, DeliveryManifestSourceCount,
				DeliveryTargetBeforeAmount, DeliveryTargetReceiptState);
		}

		internal KingdomJobRow WithDeliveryPhase(KingdomDeliveryPhase phase)
		{
			return new KingdomJobRow(JobId, Kind, Cargo, CargoAmount, SourceZoneId, DestZoneId,
				StartTick, WalkTicksPerCell, Status, OriginCode, DepositLegIndex, legs, legCount,
				SubjectId, SubjectName, TargetName, DueTick, WaterCost, ProvisionCost, OutcomeCode,
				DeliverySourceEndpointId, DeliverySourceObjectId, DeliverySourceX, DeliverySourceY,
				DeliveryTargetEndpointId, DeliveryTargetObjectId, DeliveryTargetX, DeliveryTargetY,
				DeliverySourceBeforeAmount, DeliveryTripId,
				DeliveryStopOrdinal, phase, DeliveryCargoAuthority,
				DeliveryOwnerOperationId, DeliveryOwnerManifestVersion,
				DeliveryOwnerManifestDigest, DeliveryOwnerManifestRevision,
				DeliveryManifestSourceStart, DeliveryManifestSourceCount,
				DeliveryTargetBeforeAmount, DeliveryTargetReceiptState);
		}

		internal KingdomJobRow WithManifestRevision(long revision,
			KingdomDeliveryPhase phase)
		{
			return new KingdomJobRow(JobId, Kind, Cargo, CargoAmount, SourceZoneId, DestZoneId,
				StartTick, WalkTicksPerCell, Status, OriginCode, DepositLegIndex, legs, legCount,
				SubjectId, SubjectName, TargetName, DueTick, WaterCost, ProvisionCost, OutcomeCode,
				DeliverySourceEndpointId, DeliverySourceObjectId, DeliverySourceX, DeliverySourceY,
				DeliveryTargetEndpointId, DeliveryTargetObjectId, DeliveryTargetX, DeliveryTargetY,
				DeliverySourceBeforeAmount, DeliveryTripId, DeliveryStopOrdinal, phase,
				DeliveryCargoAuthority, DeliveryOwnerOperationId, DeliveryOwnerManifestVersion,
				DeliveryOwnerManifestDigest, revision, DeliveryManifestSourceStart,
				DeliveryManifestSourceCount, DeliveryTargetBeforeAmount,
				DeliveryTargetReceiptState);
		}

		internal KingdomJobRow WithManifestAuthority(int version, string digest, long revision,
			KingdomDeliveryPhase phase)
		{
			return new KingdomJobRow(JobId, Kind, Cargo, CargoAmount, SourceZoneId, DestZoneId,
				StartTick, WalkTicksPerCell, Status, OriginCode, DepositLegIndex, legs, legCount,
				SubjectId, SubjectName, TargetName, DueTick, WaterCost, ProvisionCost, OutcomeCode,
				DeliverySourceEndpointId, DeliverySourceObjectId, DeliverySourceX, DeliverySourceY,
				DeliveryTargetEndpointId, DeliveryTargetObjectId, DeliveryTargetX, DeliveryTargetY,
				DeliverySourceBeforeAmount, DeliveryTripId, DeliveryStopOrdinal, phase,
				DeliveryCargoAuthority, DeliveryOwnerOperationId, version, digest, revision,
				DeliveryManifestSourceStart, DeliveryManifestSourceCount,
				DeliveryTargetBeforeAmount, DeliveryTargetReceiptState);
		}

		internal KingdomJobRow WithTargetReceipt(long beforeAmount,
			KingdomDeliveryTargetReceiptState receiptState)
		{
			return new KingdomJobRow(JobId, Kind, Cargo, CargoAmount, SourceZoneId, DestZoneId,
				StartTick, WalkTicksPerCell, Status, OriginCode, DepositLegIndex, legs, legCount,
				SubjectId, SubjectName, TargetName, DueTick, WaterCost, ProvisionCost, OutcomeCode,
				DeliverySourceEndpointId, DeliverySourceObjectId, DeliverySourceX, DeliverySourceY,
				DeliveryTargetEndpointId, DeliveryTargetObjectId, DeliveryTargetX, DeliveryTargetY,
				DeliverySourceBeforeAmount, DeliveryTripId,
				DeliveryStopOrdinal, DeliveryPhase, DeliveryCargoAuthority,
				DeliveryOwnerOperationId, DeliveryOwnerManifestVersion,
				DeliveryOwnerManifestDigest, DeliveryOwnerManifestRevision,
				DeliveryManifestSourceStart, DeliveryManifestSourceCount,
				beforeAmount, receiptState);
		}
	}
}
