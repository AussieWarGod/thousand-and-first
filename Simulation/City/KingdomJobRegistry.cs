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
	/// <summary>
	/// What a carrier is out doing. LIVING-CITY-ARCHITECTURE &sect;3.7 names three &mdash;
	/// deliveries, repair crews and messengers &mdash; and rules that they are <i>"the same four
	/// steps with a different <c>DelegateGoal</c>"</i>.
	/// <para>
	/// <b>W3 ships exactly one.</b> &sect;7.4 gives W3 the itinerary, the matrix and the roads
	/// discount, and gives W6 <i>"nearest-holder sourcing and capacity-bound batching &hellip;
	/// because both only bite once many jobs compete over many holders"</i>. Minting a second and
	/// third kind before there is a planner to arbitrate between them would be the empty room
	/// &sect;7.4 says not to optimise. The enum names the one that ships and nothing else, so a
	/// later wave adds a member rather than reinterpreting one.
	/// </para>
	/// </summary>
	public enum KingdomJobKind : byte
	{
		/// <summary>Not a job.</summary>
		None = 0,

		/// <summary>A load of the city's own stock, carried to a store the founder is standing
		/// beside. Addendum 12(c)'s canonical image, and G2's pending harvest is the flow that
		/// already had the model credit this renders.</summary>
		Delivery = 1,

		/// <summary>A named resident sent to one founder-visited site. Unlike a porter this is
		/// not a transient rendering: the row dates the absence while the resident binding keeps
		/// authority over the one real body.</summary>
		Expedition = 2
	}

	/// <summary>Where a job stands. LIVING-CITY-ARCHITECTURE &sect;3.7.</summary>
	public enum KingdomJobStatus : byte
	{
		/// <summary>Running. Exactly the jobs the registry holds a row for.</summary>
		Open = 0,

		/// <summary>The cargo reached the store. The row closes and its binding is evicted.</summary>
		Delivered = 1,

		/// <summary>The job outlived twice its projected duration, or its carrier died. Told, never
		/// silently dropped.</summary>
		Failed = 2
	}

	/// <summary>Durable expedition transaction state stored in a job's otherwise-unused
	/// <see cref="KingdomJobRow.OriginCode"/> column.</summary>
	internal enum KingdomExpeditionPhase : int
	{
		/// <summary>Development-wire migration: old rows published only after both debits.</summary>
		LegacyPrepared = 0,
		Prepared = 10,
		Paid = 11,
		Dispatched = 12,

		/// <summary>A no-body terminal outcome and its dated tick are frozen. Resident standing and
		/// binding cleanup may now resume without rediscovering a body that the first pass released.</summary>
		ResolutionPrepared = 13
	}

	/// <summary>Durable exact-delivery transaction phase. A source debit is bracketed by a
	/// persisted before-receipt so reload can distinguish "not debited" from "already debited"
	/// without guessing from a zone-row proxy.</summary>
	internal enum KingdomDeliveryPhase : int
	{
		/// <summary>Pre-v4 delivery row. New logistics columns must all be neutral.</summary>
		Legacy = 0,

		/// <summary>Exact source and target are frozen; no physical source debit happened.</summary>
		Planned = 1,

		/// <summary>Full trip/route and source-before receipt published before physical debit.</summary>
		SourceDebitPrepared = 2,

		/// <summary>Physical debit proved; cargo belongs to one persisted carrier trip.</summary>
		InFlight = 3,

		/// <summary>Manifest route/arrival/ids are frozen, but CarryBook authority has not yet
		/// published its digest. No body or cargo may move in this phase.</summary>
		ReservationPrepared = 4,

		/// <summary>Cross-authority mismatch. Retained for diagnosis; never rendered or moved.</summary>
		Quarantined = 5
	}

	/// <summary>Who owns cargo identity and physical callback receipts for a central trip.</summary>
	internal enum KingdomDeliveryCargoAuthority : int
	{
		/// <summary>City water/food: exact source and target containers plus scalar receipt.</summary>
		ScalarStock = 0,

		/// <summary>CarryBook v6: ordered whole GameObject stacks remain exact references.</summary>
		CarryBookManifest = 1
	}

	/// <summary>Durable bracket around one scalar target callback. The physical target receives
	/// an exact trip/job marker before mutation; this state says that marker and the before amount
	/// are authoritative recovery evidence. CarryBookManifest keeps this neutral.</summary>
	internal enum KingdomDeliveryTargetReceiptState : int
	{
		None = 0,
		Prepared = 1
	}

	/// <summary>
	/// One waypoint pair the planner turns into a leg: which ground, in by which cell, out by which
	/// cell, and how sinuous and how paved the ground between them is.
	/// </summary>
	internal readonly struct KingdomLegPlan
	{
		internal readonly string ZoneId;

		internal readonly short EnterX;

		internal readonly short EnterY;

		internal readonly short ExitX;

		internal readonly short ExitY;

		/// <summary>From <c>KingdomItineraryRules.SinuosityOpenPercent</c> or
		/// <c>SinuosityBuiltPercent</c>, by district.</summary>
		internal readonly int SinuosityPercent;

		/// <summary><c>KingdomItineraryRules.RoadDiscountPercent</c> where a road is laid along
		/// this leg, <c>NoRoadDiscountPercent</c> where none is.</summary>
		internal readonly int RoadDiscountPercent;

		internal KingdomLegPlan(string zoneId, short enterX, short enterY, short exitX, short exitY, int sinuosityPercent, int roadDiscountPercent)
		{
			ZoneId = zoneId;
			EnterX = enterX;
			EnterY = enterY;
			ExitX = exitX;
			ExitY = exitY;
			SinuosityPercent = sinuosityPercent;
			RoadDiscountPercent = roadDiscountPercent;
		}
	}

	/// <summary>
	/// One job and the timed itinerary that answers for it.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.7: <b>a job is a timed itinerary, computed once, at
	/// creation.</b> From this row one pure function answers where the carrier is and what is on
	/// them at any tick, and every zone renders that same answer &mdash; which is invariant I5, and
	/// which is why the body never has to literally traverse anything.
	/// </para>
	/// </summary>
	internal readonly struct KingdomJobRow
	{
		internal readonly int JobId;

		internal readonly KingdomJobKind Kind;

		internal readonly KingdomStockKind Cargo;

		internal readonly int CargoAmount;

		/// <summary>Where the load came from, for the exact graph route home and for the register.
		/// The load begins already in flight at the attended destination, then every claimed
		/// intermediate ground on the frozen return path is carried as a real itinerary leg.</summary>
		internal readonly string SourceZoneId;

		internal readonly string DestZoneId;

		internal readonly long StartTick;

		/// <summary>The carrier's real per-cell tick cost. At Speed 100 an actor covers exactly one
		/// cell per tick, so a founder walking beside a porter neither outpaces them nor falls
		/// behind (&sect;3.7).</summary>
		internal readonly int WalkTicksPerCell;

		internal readonly KingdomJobStatus Status;

		/// <summary>The carrier's origin, from <c>KingdomRules.Origins</c>, drawn once on the
		/// delivery lane so the same delivery yields the same carrier.</summary>
		internal readonly int OriginCode;

		/// <summary>The leg at whose END the cargo lands. Everything before it is carried;
		/// everything after it is the walk home.</summary>
		internal readonly int DepositLegIndex;

		/// <summary>Resident identity for a named-person job; zero for transient deliveries.</summary>
		internal readonly int SubjectId;

		/// <summary>Founder-facing name frozen when the job opens. Presentation only; identity is
		/// always <see cref="SubjectId"/>.</summary>
		internal readonly string SubjectName;

		/// <summary>Founder-facing destination name frozen from the visited journal note.</summary>
		internal readonly string TargetName;

		/// <summary>Absolute world tick at which the bounded expedition may resolve.</summary>
		internal readonly long DueTick;

		/// <summary>Exact physical dispatch prices, retained so status and homecoming agree.</summary>
		internal readonly int WaterCost;

		internal readonly int ProvisionCost;

		/// <summary>Frozen <c>KingdomExpeditionOutcome</c> ordinal. Never redrawn on retry.</summary>
		internal readonly int OutcomeCode;

		/// <summary>Stable endpoint hash for the exact physical source container. This is not a
		/// work-row id: it is <c>StableId(GameObject.ID)</c>, paired with the full object id below;
		/// the live cache refuses collisions.</summary>
		internal readonly int DeliverySourceEndpointId;

		internal readonly string DeliverySourceObjectId;

		internal readonly int DeliverySourceX;

		internal readonly int DeliverySourceY;

		/// <summary>Stable endpoint hash and exact engine id for the one physical receipt target.</summary>
		internal readonly int DeliveryTargetEndpointId;

		internal readonly string DeliveryTargetObjectId;

		internal readonly int DeliveryTargetX;

		internal readonly int DeliveryTargetY;

		/// <summary>Amount observed in the exact source immediately before a prepared debit.</summary>
		internal readonly long DeliverySourceBeforeAmount;

		/// <summary>One physical carrier/binding key shared by every ordered stop in a trip.</summary>
		internal readonly int DeliveryTripId;

		internal readonly int DeliveryStopOrdinal;

		internal readonly KingdomDeliveryPhase DeliveryPhase;

		internal readonly KingdomDeliveryCargoAuthority DeliveryCargoAuthority;

		internal readonly string DeliveryOwnerOperationId;

		internal readonly int DeliveryOwnerManifestVersion;

		internal readonly string DeliveryOwnerManifestDigest;

		internal readonly long DeliveryOwnerManifestRevision;

		/// <summary>Ordered whole-source-object range in the owner's immutable manifest. Capacity
		/// counts these objects, not units inside a stack; a stack of 500 keeps one identity.</summary>
		internal readonly int DeliveryManifestSourceStart;

		internal readonly int DeliveryManifestSourceCount;

		internal readonly long DeliveryTargetBeforeAmount;

		internal readonly KingdomDeliveryTargetReceiptState DeliveryTargetReceiptState;

		private readonly KingdomLeg[] legs;

		private readonly int legCount;

		internal KingdomJobRow(
			int jobId,
			KingdomJobKind kind,
			KingdomStockKind cargo,
			int cargoAmount,
			string sourceZoneId,
			string destZoneId,
			long startTick,
			int walkTicksPerCell,
			KingdomJobStatus status,
			int originCode,
			int depositLegIndex,
			KingdomLeg[] legs,
			int legCount,
			int subjectId = 0,
			string subjectName = null,
			string targetName = null,
			long dueTick = 0L,
			int waterCost = 0,
			int provisionCost = 0,
			int outcomeCode = 0,
			int deliverySourceEndpointId = 0,
			string deliverySourceObjectId = null,
			int deliverySourceX = -1,
			int deliverySourceY = -1,
			int deliveryTargetEndpointId = 0,
			string deliveryTargetObjectId = null,
			int deliveryTargetX = -1,
			int deliveryTargetY = -1,
			long deliverySourceBeforeAmount = 0L,
			int deliveryTripId = 0,
			int deliveryStopOrdinal = 0,
			KingdomDeliveryPhase deliveryPhase = KingdomDeliveryPhase.Legacy,
			KingdomDeliveryCargoAuthority deliveryCargoAuthority
				= KingdomDeliveryCargoAuthority.ScalarStock,
			string deliveryOwnerOperationId = null,
			int deliveryOwnerManifestVersion = 0,
			string deliveryOwnerManifestDigest = null,
			long deliveryOwnerManifestRevision = 0L,
			int deliveryManifestSourceStart = 0,
			int deliveryManifestSourceCount = 0,
			long deliveryTargetBeforeAmount = 0L,
			KingdomDeliveryTargetReceiptState deliveryTargetReceiptState
				= KingdomDeliveryTargetReceiptState.None)
		{
			JobId = jobId;
			Kind = kind;
			Cargo = cargo;
			CargoAmount = cargoAmount;
			SourceZoneId = sourceZoneId;
			DestZoneId = destZoneId;
			StartTick = startTick;
			WalkTicksPerCell = walkTicksPerCell;
			Status = status;
			OriginCode = originCode;
			DepositLegIndex = depositLegIndex;
			SubjectId = subjectId;
			SubjectName = subjectName;
			TargetName = targetName;
			DueTick = dueTick;
			WaterCost = waterCost;
			ProvisionCost = provisionCost;
			OutcomeCode = outcomeCode;
			DeliverySourceEndpointId = deliverySourceEndpointId;
			DeliverySourceObjectId = deliverySourceObjectId;
			DeliverySourceX = deliverySourceX;
			DeliverySourceY = deliverySourceY;
			DeliveryTargetEndpointId = deliveryTargetEndpointId;
			DeliveryTargetObjectId = deliveryTargetObjectId;
			DeliveryTargetX = deliveryTargetX;
			DeliveryTargetY = deliveryTargetY;
			DeliverySourceBeforeAmount = deliverySourceBeforeAmount;
			DeliveryTripId = deliveryTripId;
			DeliveryStopOrdinal = deliveryStopOrdinal;
			DeliveryPhase = deliveryPhase;
			DeliveryCargoAuthority = deliveryCargoAuthority;
			DeliveryOwnerOperationId = deliveryOwnerOperationId;
			DeliveryOwnerManifestVersion = deliveryOwnerManifestVersion;
			DeliveryOwnerManifestDigest = deliveryOwnerManifestDigest;
			DeliveryOwnerManifestRevision = deliveryOwnerManifestRevision;
			DeliveryManifestSourceStart = deliveryManifestSourceStart;
			DeliveryManifestSourceCount = deliveryManifestSourceCount;
			DeliveryTargetBeforeAmount = deliveryTargetBeforeAmount;
			DeliveryTargetReceiptState = deliveryTargetReceiptState;
			this.legs = legs;
			this.legCount = legCount;
		}

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

	/// <summary>
	/// The pure half of the porter: planning an itinerary, reading a carrier's cargo off it, and
	/// the two draws a delivery is allowed.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.7 and &sect;3.10. Engine-free and total. <b>No draw
	/// anywhere in the routing</b> &mdash; routing is arithmetic, not chance (&sect;3.10(4),
	/// <c>KingdomBudgetRules.PlannerMaxDraws</c> is zero) &mdash; and the two draws that do exist
	/// are flavour: which edge cell the carrier walks in by, and where they say they are from.
	/// </para>
	/// </summary>
	internal static class KingdomJobRules
	{
		/// <summary>LIVING-CITY-ARCHITECTURE &sect;3.8: sixteen open jobs, realm-wide.</summary>
		internal const int MaxOpenJobs = KingdomCityMemoryRules.MaxOpenJobs;

		/// <summary>LIVING-CITY-ARCHITECTURE &sect;3.7, the delivery lane. Frozen at creation with
		/// the rules version, so the same delivery yields the same carrier whether the founder
		/// watches it or reads about it afterwards.</summary>
		internal const string DeliveryStreamId = "taf:stream:delivery";

		internal const uint DeliveryKindCode = 1u;

		/// <summary>Which cell along the entry edge the carrier walks in by.</summary>
		internal const uint EntryCellDrawIndex = 0u;

		/// <summary>Where the carrier says they are from.</summary>
		internal const uint OriginDrawIndex = 1u;

		/// <summary>
		/// LIVING-CITY-ARCHITECTURE &sect;3.7: <i>"a porter is two units"</i> &mdash; one body mint
		/// and one container fill, both out of the ordinary eight-unit budget.
		/// </summary>
		internal const int PorterUnits = 2;

		internal const int MaxExpeditionWaterCost = 90;

		internal const int MaxExpeditionProvisionCost = 30;

		/// <summary>Resolves the exact level-1 route a porter must freeze. Both ends are included;
		/// every intermediate graph node remains present. The delivery uses one inbound destination
		/// leg plus one leg for each node on the return path, so a journey exceeding the durable
		/// six-leg row is refused whole and never shortened.</summary>
		internal static bool TryPorterPath(KingdomZoneGraph graph, string destinationZoneId,
			string sourceZoneId, out int[] path, out int count, out KingdomCityFault fault)
		{
			path = null;
			count = 0;
			if (graph == null || string.IsNullOrEmpty(destinationZoneId)
				|| string.IsNullOrEmpty(sourceZoneId))
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			int destination;
			int source;
			if (!graph.TryIndexOf(destinationZoneId, out destination)
				|| !graph.TryIndexOf(sourceZoneId, out source))
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			int[] resolved = new int[KingdomDistanceRules.MaxNodes];
			if (!graph.TryPath(destination, source, resolved, out count, out fault)) return false;
			if (count <= 0 || count + 1 > KingdomItineraryRules.MaxLegs)
			{
				count = 0;
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			path = new int[count];
			Array.Copy(resolved, path, count);
			fault = KingdomCityFault.None;
			return true;
		}

		internal static bool IsExpeditionPhase(int stored)
		{
			return stored == (int)KingdomExpeditionPhase.LegacyPrepared
				|| stored == (int)KingdomExpeditionPhase.Prepared
				|| stored == (int)KingdomExpeditionPhase.Paid
				|| stored == (int)KingdomExpeditionPhase.Dispatched
				|| stored == (int)KingdomExpeditionPhase.ResolutionPrepared;
		}

		/// <summary>Expedition result grammar is phase-sensitive. Ordinary open work retains its
		/// frozen salvage draw; only terminal-resolution authority may carry a body-loss outcome.</summary>
		internal static bool ValidExpeditionOutcomeForPhase(int phase, int outcome)
		{
			if (phase == (int)KingdomExpeditionPhase.ResolutionPrepared)
				return outcome >= (int)KingdomExpeditionOutcome.ResidentDiedOnGround
					&& outcome <= (int)KingdomExpeditionOutcome.ResidentJoinedFounder;
			return outcome >= (int)KingdomExpeditionOutcome.PickedClean
				&& outcome <= (int)KingdomExpeditionOutcome.RichFind;
		}

		internal static bool IsDeliveryPhase(int stored)
		{
			return stored == (int)KingdomDeliveryPhase.Legacy
				|| stored == (int)KingdomDeliveryPhase.Planned
				|| stored == (int)KingdomDeliveryPhase.SourceDebitPrepared
				|| stored == (int)KingdomDeliveryPhase.InFlight
				|| stored == (int)KingdomDeliveryPhase.ReservationPrepared
				|| stored == (int)KingdomDeliveryPhase.Quarantined;
		}

		internal static bool IsCentralDelivery(KingdomJobRow row)
		{
			return row.Kind == KingdomJobKind.Delivery
				&& row.DeliveryPhase != KingdomDeliveryPhase.Legacy;
		}

		/// <summary>
		/// Turns a run of waypoints into a dated itinerary.
		/// <para>
		/// Each leg's length is <c>Chebyshev &times; Sinuosity &times; RoadDiscount</c> in integer
		/// percent, with <b>zero zone access</b> &mdash; that is &sect;3.7's absolute cost bound,
		/// and the reason the estimate is a prior that reality corrects rather than a pathfind.
		/// A leg of zero cells still costs one tick, because a carrier that arrives on the tick it
		/// departs has not walked.
		/// </para>
		/// </summary>
		internal static bool TryBuildLegs(KingdomLegPlan[] plans, int count, long startTick, int walkTicksPerCell, out KingdomLeg[] legs, out KingdomCityFault fault)
		{
			legs = null;
			if (plans == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > plans.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (count > KingdomItineraryRules.MaxLegs)
			{
				// §3.7: a job that wants more than six legs is refused at planning and told. It is
				// never truncated, because a truncated route is a carrier arriving somewhere else.
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			if (startTick < 0L || walkTicksPerCell <= 0)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			KingdomLeg[] built = new KingdomLeg[count];
			long depart = startTick;
			for (int i = 0; i < count; i++)
			{
				KingdomLegPlan plan = plans[i];
				if (string.IsNullOrEmpty(plan.ZoneId))
				{
					fault = KingdomCityFault.NullArgument;
					return false;
				}
				int chebyshev;
				if (!KingdomItineraryRules.TryChebyshev(plan.EnterX, plan.EnterY, plan.ExitX, plan.ExitY, out chebyshev, out fault))
				{
					return false;
				}
				int length;
				if (!KingdomItineraryRules.TryEstimatePathLength(chebyshev, plan.SinuosityPercent, plan.RoadDiscountPercent, out length, out fault))
				{
					return false;
				}
				long walk = (long)length * walkTicksPerCell;
				if (walk < 1L)
				{
					walk = 1L;
				}
				long arrive = depart + walk;
				if (arrive < depart)
				{
					fault = KingdomCityFault.ArithmeticOverflow;
					return false;
				}
				built[i] = new KingdomLeg(plan.ZoneId, plan.EnterX, plan.EnterY, plan.ExitX, plan.ExitY, length, depart, arrive);
				depart = arrive;
			}
			if (!KingdomItineraryRules.TryValidate(built, count, out fault))
			{
				return false;
			}
			legs = built;
			return true;
		}

		/// <summary>
		/// What is still on the carrier's back at this fix.
		/// <para>
		/// The one-event-two-renderings invariant in arithmetic: the cargo is on them until the
		/// deposit leg is finished and gone afterwards. <b>The stores were credited at the dated
		/// tick either way</b> &mdash; the porter is carrying goods that are already the city's, so
		/// this figure is what a zone DRAWS, never what the city OWNS.
		/// </para>
		/// </summary>
		internal static int CargoAt(KingdomJobRow job, KingdomItineraryFix fix)
		{
			if (job.CargoAmount <= 0)
			{
				return 0;
			}
			if (fix.Phase == KingdomItineraryPhase.Delivered)
			{
				return 0;
			}
			if (fix.Phase == KingdomItineraryPhase.Pending)
			{
				return job.CargoAmount;
			}
			// Handoff reports the PREVIOUS leg's exit, so a handoff at the deposit leg's end is a
			// carrier who has just put the load down.
			if (fix.LegIndex > job.DepositLegIndex)
			{
				return 0;
			}
			if (fix.LegIndex == job.DepositLegIndex && fix.Phase == KingdomItineraryPhase.Handoff)
			{
				return 0;
			}
			return job.CargoAmount;
		}

		/// <summary>Whether the carrier has finished the leg the load lands at the end of.</summary>
		internal static bool Deposited(KingdomJobRow job, long nowTick)
		{
			KingdomLeg leg;
			if (!job.TryLeg(job.DepositLegIndex, out leg))
			{
				return false;
			}
			return nowTick >= leg.ArriveTick;
		}

		/// <summary>The key every draw about one delivery hangs off. <c>rulesVersion</c> frozen at
		/// creation, the settlement's id, the delivery lane, and the job id as the occurrence
		/// ordinal (&sect;2.4).</summary>
		internal static bool TryKey(string settlementId, int jobId, out SemanticEventKey key, out KingdomCityFault fault)
		{
			key = default(SemanticEventKey);
			if (jobId <= 0)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			KernelFaultCode kernelFault;
			if (!SemanticEventKey.TryCreate(KingdomCityRules.RulesVersion, settlementId, DeliveryStreamId, DeliveryKindCode, (ulong)jobId, out key, out kernelFault))
			{
				fault = KingdomCityFaults.FromKernel(kernelFault);
				return false;
			}
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// Which cell along an edge the carrier walks in by, drawn on the delivery lane.
		/// <para>
		/// <b>The edge itself is not drawn</b> &mdash; it is the one facing the source, which is a
		/// fact rather than a choice, and a fact cannot disagree with where the founder comes out.
		/// What is drawn is where along that edge, which is flavour and is therefore allowed one.
		/// </para>
		/// </summary>
		internal static bool TryDrawEntryCell(KernelSeed128 seed, string settlementId, int jobId, KingdomZoneStep edge, int width, int height, out short x, out short y, out KingdomCityFault fault)
		{
			x = 0;
			y = 0;
			if (width <= 2 || height <= 2 || (edge != KingdomZoneStep.North
				&& edge != KingdomZoneStep.South && edge != KingdomZoneStep.East
				&& edge != KingdomZoneStep.West))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			SemanticEventKey key;
			if (!TryKey(settlementId, jobId, out key, out fault))
			{
				return false;
			}
			bool vertical = (edge == KingdomZoneStep.North || edge == KingdomZoneStep.South);
			int span = vertical ? width : height;
			ulong along;
			KernelFaultCode kernelFault;
			if (!CounterRandom.TryDrawBelow(seed, key, EntryCellDrawIndex, (ulong)(span - 2), out along, out kernelFault))
			{
				fault = KingdomCityFaults.FromKernel(kernelFault);
				return false;
			}
			int offset = (int)along + 1;
			switch (edge)
			{
			case KingdomZoneStep.North:
				x = (short)offset;
				y = 0;
				break;
			case KingdomZoneStep.South:
				x = (short)offset;
				y = (short)(height - 1);
				break;
			case KingdomZoneStep.West:
				x = 0;
				y = (short)offset;
				break;
			default:
				// East. Invalid and vertical steps were refused before the draw.
				x = (short)(width - 1);
				y = (short)offset;
				break;
			}
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>A vanilla zone's own dimensions, for the edge arithmetic. Read off the live
		/// zone wherever one is in hand; these are what a zone that will not answer is taken to be.
		/// </summary>
		internal const int ZoneWidth = 80;

		internal const int ZoneHeight = 25;

		/// <summary>
		/// The cell the engine's own zone connection maps an exit cell to. Not a choice, so it
		/// needs no draw and cannot disagree with where the founder comes out (&sect;3.7).
		/// </summary>
		internal static void Mirror(short x, short y, KingdomZoneStep edge, int width, int height, out short mirrorX, out short mirrorY)
		{
			if (!TryMirror(x, y, edge, width, height, out mirrorX, out mirrorY))
			{
				mirrorX = x;
				mirrorY = y;
			}
		}

		/// <summary>Total horizontal connection mapping. Vertical travel uses the exact paired
		/// shaft receipt and no wall fallback; an unknown step refuses rather than becoming east.</summary>
		internal static bool TryMirror(short x, short y, KingdomZoneStep edge, int width, int height,
			out short mirrorX, out short mirrorY)
		{
			int w = (width > 2) ? width : ZoneWidth;
			int h = (height > 2) ? height : ZoneHeight;
			switch (edge)
			{
			case KingdomZoneStep.North:
				mirrorX = x;
				mirrorY = (short)(h - 1);
				return true;
			case KingdomZoneStep.South:
				mirrorX = x;
				mirrorY = 0;
				return true;
			case KingdomZoneStep.West:
				mirrorX = (short)(w - 1);
				mirrorY = y;
				return true;
			case KingdomZoneStep.East:
				mirrorX = 0;
				mirrorY = y;
				return true;
			default:
				mirrorX = x;
				mirrorY = y;
				return false;
			}
		}

		/// <summary>Which horizontal edge joins two exact neighbouring zones. Non-neighbours,
		/// malformed ids, and vertical pairs return <see cref="KingdomZoneStep.None"/>. A shaft is
		/// an exact authored cell and must never be laundered into a west-wall fallback.</summary>
		internal static KingdomZoneStep EdgeToward(string here, string source)
		{
			string world;
			int hx;
			int hy;
			int hz;
			string otherWorld;
			int sx;
			int sy;
			int sz;
			if (string.IsNullOrEmpty(source)
				|| !KingdomRules.TryParseZoneID(here, out world, out hx, out hy, out hz)
				|| !KingdomRules.TryParseZoneID(source, out otherWorld, out sx, out sy, out sz)
				|| !string.Equals(world, otherWorld, StringComparison.Ordinal))
			{
				return KingdomZoneStep.None;
			}
			KingdomZoneStep step = KingdomDistanceRules.StepBetween(
				new KingdomZoneNode(here, hx, hy, hz),
				new KingdomZoneNode(source, sx, sy, sz));
			return (step == KingdomZoneStep.Up || step == KingdomZoneStep.Down)
				? KingdomZoneStep.None : step;
		}

		/// <summary>Where the carrier says they are from, drawn on the same key at its own draw
		/// index &mdash; so adding or removing this draw cannot perturb the entry cell.</summary>
		internal static bool TryDrawOrigin(KernelSeed128 seed, string settlementId, int jobId, int originCount, out int originCode, out KingdomCityFault fault)
		{
			originCode = KingdomResidentRules.NoOrigin;
			if (originCount <= 0)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			SemanticEventKey key;
			if (!TryKey(settlementId, jobId, out key, out fault))
			{
				return false;
			}
			ulong drawn;
			KernelFaultCode kernelFault;
			if (!CounterRandom.TryDrawBelow(seed, key, OriginDrawIndex, (ulong)originCount, out drawn, out kernelFault))
			{
				fault = KingdomCityFaults.FromKernel(kernelFault);
				return false;
			}
			originCode = (int)drawn + 1;
			fault = KingdomCityFault.None;
			return true;
		}
	}

	/// <summary>
	/// The open jobs as the rules layer works on them: frozen, total, copy-on-write.
	/// <para>
	/// <b>Realm-scope, beside the binding registry rather than inside a city book</b>, and the
	/// reason is &sect;3.8's own: a carrier's legs can cross into the other city's ground or off the
	/// map, and a job a city carried would be lost on a seat swap exactly as a binding would.
	/// LIVING-CITY-ARCHITECTURE &sect;0.0(c) already prices the job rows <b>realm-wide</b> and
	/// &sect;3.8 caps them <b>per realm</b>, so this is where the budget already puts them.
	/// </para>
	/// <para>
	/// A closed job is evicted at once, so absence from this table is proof of closure &mdash; the
	/// same rule the binding registry keeps, for the same reason: there is no second list to fall
	/// out of step with the first.
	/// </para>
	/// </summary>
	internal sealed class KingdomJobTable
	{
		private readonly KingdomJobRow[] rows;

		private KingdomJobTable(KingdomJobRow[] rows)
		{
			this.rows = rows;
		}

		internal int Count
		{
			get { return rows.Length; }
		}

		internal static bool TryCreate(KingdomJobRow[] source, out KingdomJobTable table, out KingdomCityFault fault)
		{
			table = null;
			if (source == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (source.Length > KingdomJobRules.MaxOpenJobs)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			KingdomJobRow[] kept = new KingdomJobRow[source.Length];
			for (int i = 0; i < source.Length; i++)
			{
				if (source[i].JobId <= 0 || !ValidDeliveryEnvelope(source[i])
					|| (source[i].Kind == KingdomJobKind.Expedition
					&& (source[i].SubjectId <= 0
						|| !KingdomJobRules.IsExpeditionPhase(source[i].OriginCode)
						|| !KingdomJobRules.ValidExpeditionOutcomeForPhase(
							source[i].OriginCode, source[i].OutcomeCode))))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				for (int j = 0; j < i; j++)
				{
					if (kept[j].JobId == source[i].JobId)
					{
						fault = KingdomCityFault.DuplicateBinding;
						return false;
					}
					if (source[i].Kind == KingdomJobKind.Expedition
						&& kept[j].Kind == KingdomJobKind.Expedition
						&& kept[j].SubjectId == source[i].SubjectId)
					{
						fault = KingdomCityFault.DuplicateBinding;
						return false;
					}
				}
				kept[i] = source[i];
			}
			if (!ValidTrips(kept))
			{
				fault = KingdomCityFault.InvalidLegOrder;
				return false;
			}
			table = new KingdomJobTable(kept);
			fault = KingdomCityFault.None;
			return true;
		}

		private static bool ValidDeliveryEnvelope(KingdomJobRow row)
		{
			bool neutral = row.DeliverySourceEndpointId == 0
				&& string.IsNullOrEmpty(row.DeliverySourceObjectId)
				&& row.DeliverySourceX == -1 && row.DeliverySourceY == -1
				&& row.DeliveryTargetEndpointId == 0
				&& string.IsNullOrEmpty(row.DeliveryTargetObjectId)
				&& row.DeliveryTargetX == -1 && row.DeliveryTargetY == -1
				&& row.DeliverySourceBeforeAmount == 0L && row.DeliveryTripId == 0
				&& row.DeliveryStopOrdinal == 0
				&& row.DeliveryPhase == KingdomDeliveryPhase.Legacy
				&& row.DeliveryCargoAuthority == KingdomDeliveryCargoAuthority.ScalarStock
				&& string.IsNullOrEmpty(row.DeliveryOwnerOperationId)
				&& row.DeliveryOwnerManifestVersion == 0
				&& string.IsNullOrEmpty(row.DeliveryOwnerManifestDigest)
				&& row.DeliveryOwnerManifestRevision == 0L
				&& row.DeliveryManifestSourceStart == 0
				&& row.DeliveryManifestSourceCount == 0
				&& row.DeliveryTargetBeforeAmount == 0L
				&& row.DeliveryTargetReceiptState == KingdomDeliveryTargetReceiptState.None;
			if (row.Kind != KingdomJobKind.Delivery) return neutral;
			if (row.DeliveryPhase == KingdomDeliveryPhase.Legacy) return neutral;
			if (!KingdomJobRules.IsDeliveryPhase((int)row.DeliveryPhase)
				|| row.CargoAmount < 0 || string.IsNullOrEmpty(row.SourceZoneId)
				|| string.IsNullOrEmpty(row.DestZoneId)
				|| row.DeliverySourceEndpointId <= 0
				|| row.DeliveryTargetEndpointId <= 0
				|| row.DeliverySourceX < 0 || row.DeliverySourceX >= KingdomJobRules.ZoneWidth
				|| row.DeliverySourceY < 0 || row.DeliverySourceY >= KingdomJobRules.ZoneHeight
				|| row.DeliveryTargetX < 0 || row.DeliveryTargetX >= KingdomJobRules.ZoneWidth
				|| row.DeliveryTargetY < 0 || row.DeliveryTargetY >= KingdomJobRules.ZoneHeight)
				return false;
			if (row.DeliveryTargetBeforeAmount < 0L
				|| (row.DeliveryTargetReceiptState != KingdomDeliveryTargetReceiptState.None
					&& row.DeliveryTargetReceiptState
						!= KingdomDeliveryTargetReceiptState.Prepared)) return false;
			bool scalar = row.DeliveryCargoAuthority
				== KingdomDeliveryCargoAuthority.ScalarStock;
			bool manifest = row.DeliveryCargoAuthority
				== KingdomDeliveryCargoAuthority.CarryBookManifest;
			if (!scalar && !manifest) return false;
			if (scalar && ((row.Cargo != KingdomStockKind.Water
					&& row.Cargo != KingdomStockKind.Food)
				|| string.IsNullOrEmpty(row.DeliverySourceObjectId)
				|| string.IsNullOrEmpty(row.DeliveryTargetObjectId)
				|| !string.IsNullOrEmpty(row.DeliveryOwnerOperationId)
				|| row.DeliveryOwnerManifestVersion != 0
				|| !string.IsNullOrEmpty(row.DeliveryOwnerManifestDigest)
				|| row.DeliveryOwnerManifestRevision != 0L
				|| row.DeliveryManifestSourceStart != 0
				|| row.DeliveryManifestSourceCount != 0)) return false;
			bool reservation = manifest && (row.DeliveryPhase
				== KingdomDeliveryPhase.ReservationPrepared
				|| row.DeliveryPhase == KingdomDeliveryPhase.Quarantined);
			if (manifest && (row.Cargo != KingdomStockKind.OpaqueManifest
				|| string.IsNullOrEmpty(row.DeliveryOwnerOperationId)
				|| row.DeliveryManifestSourceStart < 0
				|| row.DeliveryManifestSourceCount <= 0
				|| row.DeliveryManifestSourceCount > KingdomLogisticsRules.CarrierCapacity
				|| row.CargoAmount != row.DeliveryManifestSourceCount
				|| row.DeliverySourceBeforeAmount != 0L
				|| row.DeliveryTargetBeforeAmount != 0L
				|| row.DeliveryTargetReceiptState
					!= KingdomDeliveryTargetReceiptState.None
				|| (reservation && (row.DeliveryOwnerManifestVersion != 0
					|| !string.IsNullOrEmpty(row.DeliveryOwnerManifestDigest)
					|| row.DeliveryOwnerManifestRevision != 0L))
				|| (!reservation && (row.DeliveryOwnerManifestVersion <= 0
					|| string.IsNullOrEmpty(row.DeliveryOwnerManifestDigest)
					|| row.DeliveryOwnerManifestRevision < 0L)))) return false;
			if (row.DeliveryPhase == KingdomDeliveryPhase.Planned)
				return scalar && row.CargoAmount > 0 && row.DeliverySourceBeforeAmount == 0L
					&& row.DeliveryTripId == 0 && row.DeliveryStopOrdinal == 0
					&& row.LegCount == 0 && row.DeliveryTargetBeforeAmount == 0L
					&& row.DeliveryTargetReceiptState == KingdomDeliveryTargetReceiptState.None;
			if (row.DeliveryPhase == KingdomDeliveryPhase.SourceDebitPrepared
				&& row.DeliveryTargetReceiptState != KingdomDeliveryTargetReceiptState.None)
				return false;
			if (scalar && row.CargoAmount == 0
				&& row.DeliveryTargetReceiptState != KingdomDeliveryTargetReceiptState.Prepared)
				return false;
			return (manifest || row.DeliverySourceBeforeAmount > 0L)
				&& row.DeliveryTripId > 0
				&& row.DeliveryStopOrdinal > 0
				&& row.DeliveryStopOrdinal <= KingdomLogisticsRules.MaxStopsPerTrip
				&& row.LegCount > 0 && row.LegCount <= KingdomItineraryRules.MaxLegs;
		}

		private static bool ValidTrips(KingdomJobRow[] source)
		{
			for (int i = 0; i < source.Length; i++)
			{
				KingdomJobRow seed = source[i];
				if (!KingdomJobRules.IsCentralDelivery(seed)
					|| seed.DeliveryPhase == KingdomDeliveryPhase.Planned) continue;
				if (seed.DeliveryTripId != seed.JobId) continue;
				int count = 0;
				long load = 0L;
				long before = seed.DeliverySourceBeforeAmount;
				long priorArrival = -1L;
				string priorDestination = null;
				for (int ordinal = 1; ordinal <= KingdomLogisticsRules.MaxStopsPerTrip; ordinal++)
				{
					int found = -1;
					for (int j = 0; j < source.Length; j++)
						if (source[j].DeliveryTripId == seed.DeliveryTripId
							&& source[j].DeliveryStopOrdinal == ordinal) { found = j; break; }
					if (found < 0) break;
					KingdomJobRow row = source[found];
					KingdomLeg first;
					KingdomLeg last;
					if (row.DeliveryPhase != seed.DeliveryPhase
						|| row.DeliverySourceEndpointId != seed.DeliverySourceEndpointId
						|| !string.Equals(row.DeliverySourceObjectId,
							seed.DeliverySourceObjectId, StringComparison.Ordinal)
						|| row.DeliverySourceX != seed.DeliverySourceX
						|| row.DeliverySourceY != seed.DeliverySourceY
						|| !string.Equals(row.SourceZoneId, seed.SourceZoneId, StringComparison.Ordinal)
						|| row.Cargo != seed.Cargo || row.DeliverySourceBeforeAmount != before
						|| row.DeliveryCargoAuthority != seed.DeliveryCargoAuthority
						|| !string.Equals(row.DeliveryOwnerOperationId,
							seed.DeliveryOwnerOperationId, StringComparison.Ordinal)
						|| row.DeliveryOwnerManifestVersion != seed.DeliveryOwnerManifestVersion
						|| !string.Equals(row.DeliveryOwnerManifestDigest,
							seed.DeliveryOwnerManifestDigest, StringComparison.Ordinal)
						|| row.DeliveryOwnerManifestRevision != seed.DeliveryOwnerManifestRevision
						|| !row.TryLeg(0, out first) || !row.TryLeg(row.LegCount - 1, out last)
						|| !string.Equals(last.ZoneId, row.DestZoneId, StringComparison.Ordinal)
						|| (ordinal == 1 && !string.Equals(first.ZoneId,
							row.SourceZoneId, StringComparison.Ordinal))
						|| (ordinal > 1 && (!string.Equals(first.ZoneId, priorDestination,
							StringComparison.Ordinal) || first.DepartTick < priorArrival))) return false;
					load += row.DeliveryCargoAuthority
						== KingdomDeliveryCargoAuthority.CarryBookManifest
						? row.DeliveryManifestSourceCount : row.CargoAmount;
					if (load > KingdomLogisticsRules.CarrierCapacity) return false;
					priorDestination = row.DestZoneId;
					priorArrival = last.ArriveTick;
					count++;
				}
				if (count <= 0) return false;
				for (int j = 0; j < source.Length; j++)
					if (source[j].DeliveryTripId == seed.DeliveryTripId
						&& (source[j].DeliveryStopOrdinal < 1
							|| source[j].DeliveryStopOrdinal > count)) return false;
			}
			// Every prepared/in-flight row must point at one leader row; otherwise a child could
			// survive alone and mint a second body after reload.
			for (int i = 0; i < source.Length; i++)
			{
				KingdomJobRow row = source[i];
				if (!KingdomJobRules.IsCentralDelivery(row)
					|| row.DeliveryPhase == KingdomDeliveryPhase.Planned) continue;
				bool leader = false;
				for (int j = 0; j < source.Length; j++)
					if (source[j].JobId == row.DeliveryTripId
						&& source[j].DeliveryTripId == row.DeliveryTripId) { leader = true; break; }
				if (!leader) return false;
			}
			// One exact whole-stack source ordinal may belong to only one open trip. Overlap would
			// authorize two carriers to move the same GameObject reference after reload.
			for (int i = 0; i < source.Length; i++)
			{
				KingdomJobRow left = source[i];
				if (left.DeliveryCargoAuthority
					!= KingdomDeliveryCargoAuthority.CarryBookManifest) continue;
				long leftEnd = (long)left.DeliveryManifestSourceStart
					+ left.DeliveryManifestSourceCount;
				if (leftEnd > int.MaxValue) return false;
				for (int j = i + 1; j < source.Length; j++)
				{
					KingdomJobRow right = source[j];
					if (right.DeliveryCargoAuthority
						!= KingdomDeliveryCargoAuthority.CarryBookManifest
						|| !string.Equals(left.DeliveryOwnerOperationId,
							right.DeliveryOwnerOperationId, StringComparison.Ordinal)) continue;
					if (left.DeliveryOwnerManifestVersion != right.DeliveryOwnerManifestVersion
						|| !string.Equals(left.DeliveryOwnerManifestDigest,
							right.DeliveryOwnerManifestDigest, StringComparison.Ordinal)) return false;
					long rightEnd = (long)right.DeliveryManifestSourceStart
						+ right.DeliveryManifestSourceCount;
					if (rightEnd > int.MaxValue
						|| (left.DeliveryManifestSourceStart < rightEnd
							&& right.DeliveryManifestSourceStart < leftEnd)) return false;
				}
			}
			return true;
		}

		internal bool TryAt(int index, out KingdomJobRow row)
		{
			row = default(KingdomJobRow);
			if (index < 0 || index >= rows.Length)
			{
				return false;
			}
			row = rows[index];
			return true;
		}

		internal bool TryGet(int jobId, out KingdomJobRow row)
		{
			row = default(KingdomJobRow);
			for (int i = 0; i < rows.Length; i++)
			{
				if (rows[i].JobId == jobId)
				{
					row = rows[i];
					return true;
				}
			}
			return false;
		}

		internal bool Holds(int jobId)
		{
			KingdomJobRow row;
			return TryGet(jobId, out row);
		}

		/// <summary>Opens a job. Refuses a duplicate id and refuses past the cap; publishes nothing
		/// on either, so the caller's table stays byte-identical.</summary>
		internal bool TryOpen(KingdomJobRow row, out KingdomJobTable next, out KingdomCityFault fault)
		{
			next = null;
			if (row.JobId <= 0 || row.Kind == KingdomJobKind.None)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (Holds(row.JobId))
			{
				fault = KingdomCityFault.DuplicateBinding;
				return false;
			}
			if (rows.Length >= KingdomJobRules.MaxOpenJobs)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			KingdomJobRow[] grown = new KingdomJobRow[rows.Length + 1];
			Array.Copy(rows, grown, rows.Length);
			grown[rows.Length] = row;
			return TryCreate(grown, out next, out fault);
		}

		/// <summary>Rewrites one job's row in place of the old one &mdash; a re-projection, a
		/// landed cargo, a status.</summary>
		internal bool TryReplace(KingdomJobRow row, out KingdomJobTable next, out KingdomCityFault fault)
		{
			next = null;
			if (!Holds(row.JobId))
			{
				fault = KingdomCityFault.UnknownBinding;
				return false;
			}
			KingdomJobRow[] rewritten = new KingdomJobRow[rows.Length];
			for (int i = 0; i < rows.Length; i++)
			{
				rewritten[i] = (rows[i].JobId == row.JobId) ? row : rows[i];
			}
			return TryCreate(rewritten, out next, out fault);
		}

		/// <summary>Publishes one frozen trip transition atomically: every stop changes phase/route
		/// together or no row changes.</summary>
		internal bool TryRewrite(KingdomJobRow[] replacements, int count,
			out KingdomJobTable next, out KingdomCityFault fault)
		{
			next = null;
			if (replacements == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > replacements.Length || count > rows.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			for (int i = 0; i < count; i++)
			{
				if (!Holds(replacements[i].JobId))
				{
					fault = KingdomCityFault.UnknownBinding;
					return false;
				}
				for (int j = 0; j < i; j++)
					if (replacements[j].JobId == replacements[i].JobId)
					{
						fault = KingdomCityFault.DuplicateBinding;
						return false;
					}
			}
			KingdomJobRow[] rewritten = new KingdomJobRow[rows.Length];
			Array.Copy(rows, rewritten, rows.Length);
			for (int i = 0; i < rewritten.Length; i++)
				for (int j = 0; j < count; j++)
					if (rewritten[i].JobId == replacements[j].JobId)
						rewritten[i] = replacements[j];
			return TryCreate(rewritten, out next, out fault);
		}

		/// <summary>Evicts every row owned by one central trip in one table publication.</summary>
		internal bool TryCloseTrip(int tripId, out KingdomJobTable next,
			out KingdomJobRow[] closed, out KingdomCityFault fault)
		{
			next = null;
			closed = null;
			int count = 0;
			for (int i = 0; i < rows.Length; i++)
				if (rows[i].DeliveryTripId == tripId) count++;
			if (tripId <= 0 || count <= 0)
			{
				fault = KingdomCityFault.UnknownBinding;
				return false;
			}
			closed = new KingdomJobRow[count];
			KingdomJobRow[] kept = new KingdomJobRow[rows.Length - count];
			int c = 0;
			int k = 0;
			for (int i = 0; i < rows.Length; i++)
			{
				if (rows[i].DeliveryTripId == tripId) closed[c++] = rows[i];
				else kept[k++] = rows[i];
			}
			return TryCreate(kept, out next, out fault);
		}

		/// <summary>Evicts a job. There is no closed list: the eviction IS the closure.</summary>
		internal bool TryClose(int jobId, out KingdomJobTable next, out KingdomJobRow closed, out KingdomCityFault fault)
		{
			next = null;
			closed = default(KingdomJobRow);
			if (!TryGet(jobId, out closed))
			{
				fault = KingdomCityFault.UnknownBinding;
				return false;
			}
			KingdomJobRow[] shrunk = new KingdomJobRow[rows.Length - 1];
			int at = 0;
			for (int i = 0; i < rows.Length; i++)
			{
				if (rows[i].JobId != jobId)
				{
					shrunk[at++] = rows[i];
				}
			}
			return TryCreate(shrunk, out next, out fault);
		}

		/// <summary>Every open job's id, oldest first. The order the pump renders them in, and
		/// stable so a save and reload resumes in exactly the same place.</summary>
		internal int[] OpenIds()
		{
			int[] ids = new int[rows.Length];
			for (int i = 0; i < rows.Length; i++)
			{
				ids[i] = rows[i].JobId;
			}
			return ids;
		}
	}

	/// <summary>
	/// The realm's open jobs, in the shape a save can hold them.
	/// <para>
	/// The same carrier/rules pairing <see cref="KingdomBindingRegistry"/> has, for the same reason
	/// (&sect;1.3): a named-field reader must assign fields and the rules layer must not. Legs are
	/// flattened into their own columns and each job says how many of them are its own, because a
	/// jagged list of lists is not something a named-field writer can hold.
	/// </para>
	/// </summary>
	[Serializable]
	public class KingdomJobRegistry
#if !TAF_TESTS
		: IComposite
#endif
	{
		/// <summary>The realm's job id counter. Never reused, never drawn: a job id is the ordinal
		/// its delivery's draws hang off (&sect;2.4), and a seeded id would make which carrier walks
		/// in depend on how many other things had been rolled first.</summary>
		public int JobCounter;

		public List<int> JobIds = new List<int>();

		public List<int> Kinds = new List<int>();

		public List<int> Cargos = new List<int>();

		public List<int> CargoAmounts = new List<int>();

		public List<string> SourceZoneIds = new List<string>();

		public List<string> DestZoneIds = new List<string>();

		public List<long> StartTicks = new List<long>();

		public List<int> WalkTicksPerCell = new List<int>();

		public List<int> Statuses = new List<int>();

		public List<int> OriginCodes = new List<int>();

		public List<int> DepositLegIndexes = new List<int>();

		// ---- Named-person mission payload -----------------------------------------------
		// Additive named-field columns. A save from before expeditions has all seven absent;
		// Normalize pads that whole legacy envelope with neutral values before taking the
		// shortest row count. A partially present current envelope remains malformed and is
		// truncated instead of guessing mission authority.

		public List<int> SubjectIds = new List<int>();

		public List<string> SubjectNames = new List<string>();

		public List<string> TargetNames = new List<string>();

		public List<long> DueTicks = new List<long>();

		public List<int> WaterCosts = new List<int>();

		public List<int> ProvisionCosts = new List<int>();

		public List<int> OutcomeCodes = new List<int>();

		// ---- Exact central-delivery payload --------------------------------------------
		// Additive v4 named columns. Endpoint ids are stable hashes used by the sparse
		// distance matrix; full engine object ids bind physical debit and receipt exactly.

		public List<int> DeliverySourceEndpointIds = new List<int>();

		public List<string> DeliverySourceObjectIds = new List<string>();

		public List<int> DeliverySourceXs = new List<int>();

		public List<int> DeliverySourceYs = new List<int>();

		public List<int> DeliveryTargetEndpointIds = new List<int>();

		public List<string> DeliveryTargetObjectIds = new List<string>();

		public List<int> DeliveryTargetXs = new List<int>();

		public List<int> DeliveryTargetYs = new List<int>();

		public List<long> DeliverySourceBeforeAmounts = new List<long>();

		public List<int> DeliveryTripIds = new List<int>();

		public List<int> DeliveryStopOrdinals = new List<int>();

		public List<int> DeliveryPhases = new List<int>();

		public List<int> DeliveryCargoAuthorityKinds = new List<int>();

		public List<string> DeliveryOwnerOperationIds = new List<string>();

		public List<int> DeliveryOwnerManifestVersions = new List<int>();

		public List<string> DeliveryOwnerManifestDigests = new List<string>();

		public List<long> DeliveryOwnerManifestRevisions = new List<long>();

		public List<int> DeliveryManifestSourceStarts = new List<int>();

		public List<int> DeliveryManifestSourceCounts = new List<int>();

		public List<long> DeliveryTargetBeforeAmounts = new List<long>();

		public List<int> DeliveryTargetReceiptStates = new List<int>();

		public List<int> LegCounts = new List<int>();

		// ---- Legs, flattened in job order -----------------------------------------------------

		public List<string> LegZoneIds = new List<string>();

		public List<int> LegEnterX = new List<int>();

		public List<int> LegEnterY = new List<int>();

		public List<int> LegExitX = new List<int>();

		public List<int> LegExitY = new List<int>();

		public List<int> LegLengths = new List<int>();

		public List<long> LegDepartTicks = new List<long>();

		public List<long> LegArriveTicks = new List<long>();

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomJobRegistry));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomJobRegistry));
			Normalize();
		}
#endif

		public int Count => JobIds.Count;

		/// <summary>
		/// Repairs a registry read from a save written by an older build. Null columns become
		/// empty; ragged columns are truncated to the shortest; a duplicate or zero job id is
		/// dropped; a job whose declared legs are not all present is dropped whole, because half an
		/// itinerary is a carrier with no answer to where they are.
		/// </summary>
		public void Normalize()
		{
			JobIds = Repair(JobIds);
			Kinds = Repair(Kinds);
			Cargos = Repair(Cargos);
			CargoAmounts = Repair(CargoAmounts);
			SourceZoneIds = Repair(SourceZoneIds);
			DestZoneIds = Repair(DestZoneIds);
			StartTicks = Repair(StartTicks);
			WalkTicksPerCell = Repair(WalkTicksPerCell);
			Statuses = Repair(Statuses);
			OriginCodes = Repair(OriginCodes);
			DepositLegIndexes = Repair(DepositLegIndexes);
			SubjectIds = Repair(SubjectIds);
			SubjectNames = Repair(SubjectNames);
			TargetNames = Repair(TargetNames);
			DueTicks = Repair(DueTicks);
			WaterCosts = Repair(WaterCosts);
			ProvisionCosts = Repair(ProvisionCosts);
			OutcomeCodes = Repair(OutcomeCodes);
			DeliverySourceEndpointIds = Repair(DeliverySourceEndpointIds);
			DeliverySourceObjectIds = Repair(DeliverySourceObjectIds);
			DeliverySourceXs = Repair(DeliverySourceXs);
			DeliverySourceYs = Repair(DeliverySourceYs);
			DeliveryTargetEndpointIds = Repair(DeliveryTargetEndpointIds);
			DeliveryTargetObjectIds = Repair(DeliveryTargetObjectIds);
			DeliveryTargetXs = Repair(DeliveryTargetXs);
			DeliveryTargetYs = Repair(DeliveryTargetYs);
			DeliverySourceBeforeAmounts = Repair(DeliverySourceBeforeAmounts);
			DeliveryTripIds = Repair(DeliveryTripIds);
			DeliveryStopOrdinals = Repair(DeliveryStopOrdinals);
			DeliveryPhases = Repair(DeliveryPhases);
			DeliveryCargoAuthorityKinds = Repair(DeliveryCargoAuthorityKinds);
			DeliveryOwnerOperationIds = Repair(DeliveryOwnerOperationIds);
			DeliveryOwnerManifestVersions = Repair(DeliveryOwnerManifestVersions);
			DeliveryOwnerManifestDigests = Repair(DeliveryOwnerManifestDigests);
			DeliveryOwnerManifestRevisions = Repair(DeliveryOwnerManifestRevisions);
			DeliveryManifestSourceStarts = Repair(DeliveryManifestSourceStarts);
			DeliveryManifestSourceCounts = Repair(DeliveryManifestSourceCounts);
			DeliveryTargetBeforeAmounts = Repair(DeliveryTargetBeforeAmounts);
			DeliveryTargetReceiptStates = Repair(DeliveryTargetReceiptStates);
			LegCounts = Repair(LegCounts);
			LegZoneIds = Repair(LegZoneIds);
			LegEnterX = Repair(LegEnterX);
			LegEnterY = Repair(LegEnterY);
			LegExitX = Repair(LegExitX);
			LegExitY = Repair(LegExitY);
			LegLengths = Repair(LegLengths);
			LegDepartTicks = Repair(LegDepartTicks);
			LegArriveTicks = Repair(LegArriveTicks);
			if (JobCounter < 0)
			{
				JobCounter = 0;
			}

			int legacyJobs = Shortest(new int[]
			{
				JobIds.Count, Kinds.Count, Cargos.Count, CargoAmounts.Count, SourceZoneIds.Count,
				DestZoneIds.Count, StartTicks.Count, WalkTicksPerCell.Count, Statuses.Count,
				OriginCodes.Count, DepositLegIndexes.Count, LegCounts.Count
			});
			bool legacyMissionEnvelope = SubjectIds.Count == 0 && SubjectNames.Count == 0
				&& TargetNames.Count == 0 && DueTicks.Count == 0 && WaterCosts.Count == 0
				&& ProvisionCosts.Count == 0 && OutcomeCodes.Count == 0;
			if (legacyMissionEnvelope)
			{
				Pad(SubjectIds, legacyJobs, 0);
				Pad(SubjectNames, legacyJobs, "");
				Pad(TargetNames, legacyJobs, "");
				Pad(DueTicks, legacyJobs, 0L);
				Pad(WaterCosts, legacyJobs, 0);
				Pad(ProvisionCosts, legacyJobs, 0);
				Pad(OutcomeCodes, legacyJobs, 0);
			}
			int missionJobs = Shortest(new int[]
			{
				legacyJobs, SubjectIds.Count, SubjectNames.Count, TargetNames.Count, DueTicks.Count,
				WaterCosts.Count, ProvisionCosts.Count, OutcomeCodes.Count
			});
			bool legacyDeliveryEnvelope = DeliverySourceEndpointIds.Count == 0
				&& DeliverySourceObjectIds.Count == 0 && DeliveryTargetEndpointIds.Count == 0
				&& DeliverySourceXs.Count == 0 && DeliverySourceYs.Count == 0
				&& DeliveryTargetObjectIds.Count == 0 && DeliveryTargetXs.Count == 0
				&& DeliveryTargetYs.Count == 0 && DeliverySourceBeforeAmounts.Count == 0
				&& DeliveryTripIds.Count == 0 && DeliveryStopOrdinals.Count == 0
				&& DeliveryPhases.Count == 0 && DeliveryCargoAuthorityKinds.Count == 0
				&& DeliveryOwnerOperationIds.Count == 0
				&& DeliveryOwnerManifestVersions.Count == 0
				&& DeliveryOwnerManifestDigests.Count == 0
				&& DeliveryOwnerManifestRevisions.Count == 0
				&& DeliveryManifestSourceStarts.Count == 0
				&& DeliveryManifestSourceCounts.Count == 0
				&& DeliveryTargetBeforeAmounts.Count == 0
				&& DeliveryTargetReceiptStates.Count == 0;
			if (legacyDeliveryEnvelope)
			{
				Pad(DeliverySourceEndpointIds, missionJobs, 0);
				Pad(DeliverySourceObjectIds, missionJobs, "");
				Pad(DeliverySourceXs, missionJobs, -1);
				Pad(DeliverySourceYs, missionJobs, -1);
				Pad(DeliveryTargetEndpointIds, missionJobs, 0);
				Pad(DeliveryTargetObjectIds, missionJobs, "");
				Pad(DeliveryTargetXs, missionJobs, -1);
				Pad(DeliveryTargetYs, missionJobs, -1);
				Pad(DeliverySourceBeforeAmounts, missionJobs, 0L);
				Pad(DeliveryTripIds, missionJobs, 0);
				Pad(DeliveryStopOrdinals, missionJobs, 0);
				Pad(DeliveryPhases, missionJobs, 0);
				Pad(DeliveryCargoAuthorityKinds, missionJobs, 0);
				Pad(DeliveryOwnerOperationIds, missionJobs, "");
				Pad(DeliveryOwnerManifestVersions, missionJobs, 0);
				Pad(DeliveryOwnerManifestDigests, missionJobs, "");
				Pad(DeliveryOwnerManifestRevisions, missionJobs, 0L);
				Pad(DeliveryManifestSourceStarts, missionJobs, 0);
				Pad(DeliveryManifestSourceCounts, missionJobs, 0);
				Pad(DeliveryTargetBeforeAmounts, missionJobs, 0L);
				Pad(DeliveryTargetReceiptStates, missionJobs, 0);
			}
			int jobs = Shortest(new int[]
			{
				missionJobs, DeliverySourceEndpointIds.Count, DeliverySourceObjectIds.Count,
				DeliverySourceXs.Count, DeliverySourceYs.Count, DeliveryTargetEndpointIds.Count,
				DeliveryTargetObjectIds.Count, DeliveryTargetXs.Count, DeliveryTargetYs.Count,
				DeliverySourceBeforeAmounts.Count, DeliveryTripIds.Count,
				DeliveryStopOrdinals.Count, DeliveryPhases.Count,
				DeliveryCargoAuthorityKinds.Count, DeliveryOwnerOperationIds.Count,
				DeliveryOwnerManifestVersions.Count, DeliveryOwnerManifestDigests.Count,
				DeliveryOwnerManifestRevisions.Count, DeliveryManifestSourceStarts.Count,
				DeliveryManifestSourceCounts.Count, DeliveryTargetBeforeAmounts.Count,
				DeliveryTargetReceiptStates.Count
			});
			Trim(JobIds, jobs);
			Trim(Kinds, jobs);
			Trim(Cargos, jobs);
			Trim(CargoAmounts, jobs);
			Trim(SourceZoneIds, jobs);
			Trim(DestZoneIds, jobs);
			Trim(StartTicks, jobs);
			Trim(WalkTicksPerCell, jobs);
			Trim(Statuses, jobs);
			Trim(OriginCodes, jobs);
			Trim(DepositLegIndexes, jobs);
			Trim(SubjectIds, jobs);
			Trim(SubjectNames, jobs);
			Trim(TargetNames, jobs);
			Trim(DueTicks, jobs);
			Trim(WaterCosts, jobs);
			Trim(ProvisionCosts, jobs);
			Trim(OutcomeCodes, jobs);
			Trim(DeliverySourceEndpointIds, jobs);
			Trim(DeliverySourceObjectIds, jobs);
			Trim(DeliverySourceXs, jobs);
			Trim(DeliverySourceYs, jobs);
			Trim(DeliveryTargetEndpointIds, jobs);
			Trim(DeliveryTargetObjectIds, jobs);
			Trim(DeliveryTargetXs, jobs);
			Trim(DeliveryTargetYs, jobs);
			Trim(DeliverySourceBeforeAmounts, jobs);
			Trim(DeliveryTripIds, jobs);
			Trim(DeliveryStopOrdinals, jobs);
			Trim(DeliveryPhases, jobs);
			Trim(DeliveryCargoAuthorityKinds, jobs);
			Trim(DeliveryOwnerOperationIds, jobs);
			Trim(DeliveryOwnerManifestVersions, jobs);
			Trim(DeliveryOwnerManifestDigests, jobs);
			Trim(DeliveryOwnerManifestRevisions, jobs);
			Trim(DeliveryManifestSourceStarts, jobs);
			Trim(DeliveryManifestSourceCounts, jobs);
			Trim(DeliveryTargetBeforeAmounts, jobs);
			Trim(DeliveryTargetReceiptStates, jobs);
			Trim(LegCounts, jobs);

			int legs = Shortest(new int[]
			{
				LegZoneIds.Count, LegEnterX.Count, LegEnterY.Count, LegExitX.Count, LegExitY.Count,
				LegLengths.Count, LegDepartTicks.Count, LegArriveTicks.Count
			});
			Trim(LegZoneIds, legs);
			Trim(LegEnterX, legs);
			Trim(LegEnterY, legs);
			Trim(LegExitX, legs);
			Trim(LegExitY, legs);
			Trim(LegLengths, legs);
			Trim(LegDepartTicks, legs);
			Trim(LegArriveTicks, legs);

			int consumed = 0;
			for (int i = 0; i < JobIds.Count; i++)
			{
				int count = LegCounts[i];
				if (count < 0 || count > KingdomItineraryRules.MaxLegs)
				{
					count = 0;
					LegCounts[i] = 0;
				}
				bool broken = JobIds[i] <= 0 || Duplicated(i) || consumed + count > legs;
				if (!broken)
				{
					if (SourceZoneIds[i] == null) { SourceZoneIds[i] = ""; }
					if (DestZoneIds[i] == null) { DestZoneIds[i] = ""; }
					if (StartTicks[i] < 0L) { StartTicks[i] = 0L; }
					if (SubjectNames[i] == null) { SubjectNames[i] = ""; }
					if (TargetNames[i] == null) { TargetNames[i] = ""; }
					if (SubjectIds[i] < 0) { SubjectIds[i] = 0; }
					if (DueTicks[i] < 0L) { DueTicks[i] = 0L; }
					if (WaterCosts[i] < 0) { WaterCosts[i] = 0; }
					if (ProvisionCosts[i] < 0) { ProvisionCosts[i] = 0; }
					if (OutcomeCodes[i] < 0) { OutcomeCodes[i] = 0; }
					if (DeliverySourceObjectIds[i] == null) { DeliverySourceObjectIds[i] = ""; }
					if (DeliveryTargetObjectIds[i] == null) { DeliveryTargetObjectIds[i] = ""; }
					if (DeliverySourceEndpointIds[i] < 0) { DeliverySourceEndpointIds[i] = 0; }
					if (DeliveryTargetEndpointIds[i] < 0) { DeliveryTargetEndpointIds[i] = 0; }
					if (DeliverySourceBeforeAmounts[i] < 0L) { DeliverySourceBeforeAmounts[i] = 0L; }
					if (DeliveryTripIds[i] < 0) { DeliveryTripIds[i] = 0; }
					if (DeliveryStopOrdinals[i] < 0) { DeliveryStopOrdinals[i] = 0; }
					if (!KingdomJobRules.IsDeliveryPhase(DeliveryPhases[i])) { DeliveryPhases[i] = 0; }
					if (DeliveryCargoAuthorityKinds[i] < 0
						|| DeliveryCargoAuthorityKinds[i] > 1) { DeliveryCargoAuthorityKinds[i] = 0; }
					if (DeliveryOwnerOperationIds[i] == null) { DeliveryOwnerOperationIds[i] = ""; }
					if (DeliveryOwnerManifestVersions[i] < 0) { DeliveryOwnerManifestVersions[i] = 0; }
					if (DeliveryOwnerManifestDigests[i] == null) { DeliveryOwnerManifestDigests[i] = ""; }
					if (DeliveryOwnerManifestRevisions[i] < 0L) { DeliveryOwnerManifestRevisions[i] = 0L; }
					if (DeliveryManifestSourceStarts[i] < 0) { DeliveryManifestSourceStarts[i] = 0; }
					if (DeliveryManifestSourceCounts[i] < 0) { DeliveryManifestSourceCounts[i] = 0; }
					if (DeliveryTargetBeforeAmounts[i] < 0L) { DeliveryTargetBeforeAmounts[i] = 0L; }
					if (DeliveryTargetReceiptStates[i] < 0
						|| DeliveryTargetReceiptStates[i] > 1) { DeliveryTargetReceiptStates[i] = 0; }
					if (WalkTicksPerCell[i] <= 0) { WalkTicksPerCell[i] = KingdomItineraryRules.WalkTicksPerCellDefault; }
					if (DepositLegIndexes[i] < 0) { DepositLegIndexes[i] = 0; }
					consumed += count;
					continue;
				}
				RemoveLegs(consumed, count);
				RemoveJob(i);
				legs -= count;
				i--;
			}
			// Legs past the last job's share belong to no job and are dropped rather than kept for
			// a job that might be added later: an itinerary nobody claims is not an itinerary.
			if (LegZoneIds.Count > consumed)
			{
				RemoveLegs(consumed, LegZoneIds.Count - consumed);
			}
			DropOverCap();
		}

		/// <summary>The registry as the frozen table the rules layer works on.</summary>
		internal bool TryRead(out KingdomJobTable table, out KingdomCityFault fault)
		{
			Normalize();
			KingdomJobRow[] rows = new KingdomJobRow[JobIds.Count];
			int at = 0;
			for (int i = 0; i < rows.Length; i++)
			{
				int count = LegCounts[i];
				KingdomLeg[] legs = new KingdomLeg[count];
				for (int j = 0; j < count; j++)
				{
					legs[j] = new KingdomLeg(
						LegZoneIds[at + j] ?? "",
						(short)LegEnterX[at + j], (short)LegEnterY[at + j],
						(short)LegExitX[at + j], (short)LegExitY[at + j],
						LegLengths[at + j], LegDepartTicks[at + j], LegArriveTicks[at + j]);
				}
				at += count;
				rows[i] = new KingdomJobRow(
					JobIds[i],
					KindOf(Kinds[i]),
					CargoOf(Cargos[i]),
					CargoAmounts[i],
					SourceZoneIds[i],
					DestZoneIds[i],
					StartTicks[i],
					WalkTicksPerCell[i],
					StatusOf(Statuses[i]),
					OriginCodes[i],
					DepositLegIndexes[i],
					legs,
					count,
					SubjectIds[i],
					SubjectNames[i],
					TargetNames[i],
					DueTicks[i],
					WaterCosts[i],
					ProvisionCosts[i],
					OutcomeCodes[i],
					DeliverySourceEndpointIds[i],
					DeliverySourceObjectIds[i],
					DeliverySourceXs[i],
					DeliverySourceYs[i],
					DeliveryTargetEndpointIds[i],
					DeliveryTargetObjectIds[i],
					DeliveryTargetXs[i],
					DeliveryTargetYs[i],
					DeliverySourceBeforeAmounts[i],
					DeliveryTripIds[i],
					DeliveryStopOrdinals[i],
					(KingdomDeliveryPhase)DeliveryPhases[i],
					(KingdomDeliveryCargoAuthority)DeliveryCargoAuthorityKinds[i],
					DeliveryOwnerOperationIds[i], DeliveryOwnerManifestVersions[i],
					DeliveryOwnerManifestDigests[i], DeliveryOwnerManifestRevisions[i],
					DeliveryManifestSourceStarts[i], DeliveryManifestSourceCounts[i],
					DeliveryTargetBeforeAmounts[i],
					(KingdomDeliveryTargetReceiptState)DeliveryTargetReceiptStates[i]);
			}
			return KingdomJobTable.TryCreate(rows, out table, out fault);
		}

		/// <summary>Writes one frozen table into the columns, in one call and after the rules have
		/// succeeded. The single publisher &sect;1.3 requires.</summary>
		internal bool TryPublish(KingdomJobTable table, out KingdomCityFault fault)
		{
			if (table == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			JobIds.Clear(); Kinds.Clear(); Cargos.Clear(); CargoAmounts.Clear();
			SourceZoneIds.Clear(); DestZoneIds.Clear(); StartTicks.Clear(); WalkTicksPerCell.Clear();
			Statuses.Clear(); OriginCodes.Clear(); DepositLegIndexes.Clear(); LegCounts.Clear();
			SubjectIds.Clear(); SubjectNames.Clear(); TargetNames.Clear(); DueTicks.Clear();
			WaterCosts.Clear(); ProvisionCosts.Clear(); OutcomeCodes.Clear();
			DeliverySourceEndpointIds.Clear(); DeliverySourceObjectIds.Clear();
			DeliverySourceXs.Clear(); DeliverySourceYs.Clear();
			DeliveryTargetEndpointIds.Clear(); DeliveryTargetObjectIds.Clear();
			DeliveryTargetXs.Clear(); DeliveryTargetYs.Clear();
			DeliverySourceBeforeAmounts.Clear(); DeliveryTripIds.Clear();
			DeliveryStopOrdinals.Clear(); DeliveryPhases.Clear();
			DeliveryCargoAuthorityKinds.Clear(); DeliveryOwnerOperationIds.Clear();
			DeliveryOwnerManifestVersions.Clear(); DeliveryOwnerManifestDigests.Clear();
			DeliveryOwnerManifestRevisions.Clear(); DeliveryManifestSourceStarts.Clear();
			DeliveryManifestSourceCounts.Clear(); DeliveryTargetBeforeAmounts.Clear();
			DeliveryTargetReceiptStates.Clear();
			LegZoneIds.Clear(); LegEnterX.Clear(); LegEnterY.Clear(); LegExitX.Clear();
			LegExitY.Clear(); LegLengths.Clear(); LegDepartTicks.Clear(); LegArriveTicks.Clear();
			for (int i = 0; i < table.Count; i++)
			{
				KingdomJobRow row;
				if (!table.TryAt(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				JobIds.Add(row.JobId);
				Kinds.Add((int)row.Kind);
				Cargos.Add((int)row.Cargo);
				CargoAmounts.Add(row.CargoAmount);
				SourceZoneIds.Add(row.SourceZoneId ?? "");
				DestZoneIds.Add(row.DestZoneId ?? "");
				StartTicks.Add(row.StartTick);
				WalkTicksPerCell.Add(row.WalkTicksPerCell);
				Statuses.Add((int)row.Status);
				OriginCodes.Add(row.OriginCode);
				DepositLegIndexes.Add(row.DepositLegIndex);
				SubjectIds.Add(row.SubjectId);
				SubjectNames.Add(row.SubjectName ?? "");
				TargetNames.Add(row.TargetName ?? "");
				DueTicks.Add(row.DueTick);
				WaterCosts.Add(row.WaterCost);
				ProvisionCosts.Add(row.ProvisionCost);
				OutcomeCodes.Add(row.OutcomeCode);
				DeliverySourceEndpointIds.Add(row.DeliverySourceEndpointId);
				DeliverySourceObjectIds.Add(row.DeliverySourceObjectId ?? "");
				DeliverySourceXs.Add(row.DeliverySourceX);
				DeliverySourceYs.Add(row.DeliverySourceY);
				DeliveryTargetEndpointIds.Add(row.DeliveryTargetEndpointId);
				DeliveryTargetObjectIds.Add(row.DeliveryTargetObjectId ?? "");
				DeliveryTargetXs.Add(row.DeliveryTargetX);
				DeliveryTargetYs.Add(row.DeliveryTargetY);
				DeliverySourceBeforeAmounts.Add(row.DeliverySourceBeforeAmount);
				DeliveryTripIds.Add(row.DeliveryTripId);
				DeliveryStopOrdinals.Add(row.DeliveryStopOrdinal);
				DeliveryPhases.Add((int)row.DeliveryPhase);
				DeliveryCargoAuthorityKinds.Add((int)row.DeliveryCargoAuthority);
				DeliveryOwnerOperationIds.Add(row.DeliveryOwnerOperationId ?? "");
				DeliveryOwnerManifestVersions.Add(row.DeliveryOwnerManifestVersion);
				DeliveryOwnerManifestDigests.Add(row.DeliveryOwnerManifestDigest ?? "");
				DeliveryOwnerManifestRevisions.Add(row.DeliveryOwnerManifestRevision);
				DeliveryManifestSourceStarts.Add(row.DeliveryManifestSourceStart);
				DeliveryManifestSourceCounts.Add(row.DeliveryManifestSourceCount);
				DeliveryTargetBeforeAmounts.Add(row.DeliveryTargetBeforeAmount);
				DeliveryTargetReceiptStates.Add((int)row.DeliveryTargetReceiptState);
				LegCounts.Add(row.LegCount);
				for (int j = 0; j < row.LegCount; j++)
				{
					KingdomLeg leg;
					if (!row.TryLeg(j, out leg))
					{
						fault = KingdomCityFault.InvalidIndex;
						return false;
					}
					LegZoneIds.Add(leg.ZoneId ?? "");
					LegEnterX.Add(leg.EnterX);
					LegEnterY.Add(leg.EnterY);
					LegExitX.Add(leg.ExitX);
					LegExitY.Add(leg.ExitY);
					LegLengths.Add(leg.PathLength);
					LegDepartTicks.Add(leg.DepartTick);
					LegArriveTicks.Add(leg.ArriveTick);
				}
			}
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>The next job id, minted off the realm's counter.</summary>
		public int MintJobId()
		{
			JobCounter++;
			return JobCounter;
		}

		private static KingdomJobKind KindOf(int stored)
		{
			if (stored == (int)KingdomJobKind.Delivery) { return KingdomJobKind.Delivery; }
			if (stored == (int)KingdomJobKind.Expedition) { return KingdomJobKind.Expedition; }
			return KingdomJobKind.None;
		}

		private static KingdomStockKind CargoOf(int stored)
		{
			if (stored == (int)KingdomStockKind.Food) { return KingdomStockKind.Food; }
			if (stored == (int)KingdomStockKind.Materials) { return KingdomStockKind.Materials; }
			if (stored == (int)KingdomStockKind.OpaqueManifest) { return KingdomStockKind.OpaqueManifest; }
			return KingdomStockKind.Water;
		}

		private static KingdomJobStatus StatusOf(int stored)
		{
			if (stored == (int)KingdomJobStatus.Delivered) { return KingdomJobStatus.Delivered; }
			if (stored == (int)KingdomJobStatus.Failed) { return KingdomJobStatus.Failed; }
			return KingdomJobStatus.Open;
		}

		private bool Duplicated(int index)
		{
			for (int i = 0; i < index; i++)
			{
				if (JobIds[i] == JobIds[index])
				{
					return true;
				}
			}
			return false;
		}

		private void DropOverCap()
		{
			int consumed = 0;
			for (int i = 0; i < JobIds.Count; i++)
			{
				if (i < KingdomJobRules.MaxOpenJobs)
				{
					consumed += LegCounts[i];
					continue;
				}
				RemoveLegs(consumed, LegCounts[i]);
				RemoveJob(i);
				i--;
			}
		}

		private void RemoveJob(int index)
		{
			JobIds.RemoveAt(index);
			Kinds.RemoveAt(index);
			Cargos.RemoveAt(index);
			CargoAmounts.RemoveAt(index);
			SourceZoneIds.RemoveAt(index);
			DestZoneIds.RemoveAt(index);
			StartTicks.RemoveAt(index);
			WalkTicksPerCell.RemoveAt(index);
			Statuses.RemoveAt(index);
			OriginCodes.RemoveAt(index);
			DepositLegIndexes.RemoveAt(index);
			SubjectIds.RemoveAt(index);
			SubjectNames.RemoveAt(index);
			TargetNames.RemoveAt(index);
			DueTicks.RemoveAt(index);
			WaterCosts.RemoveAt(index);
			ProvisionCosts.RemoveAt(index);
			OutcomeCodes.RemoveAt(index);
			DeliverySourceEndpointIds.RemoveAt(index);
			DeliverySourceObjectIds.RemoveAt(index);
			DeliverySourceXs.RemoveAt(index);
			DeliverySourceYs.RemoveAt(index);
			DeliveryTargetEndpointIds.RemoveAt(index);
			DeliveryTargetObjectIds.RemoveAt(index);
			DeliveryTargetXs.RemoveAt(index);
			DeliveryTargetYs.RemoveAt(index);
			DeliverySourceBeforeAmounts.RemoveAt(index);
			DeliveryTripIds.RemoveAt(index);
			DeliveryStopOrdinals.RemoveAt(index);
			DeliveryPhases.RemoveAt(index);
			DeliveryCargoAuthorityKinds.RemoveAt(index);
			DeliveryOwnerOperationIds.RemoveAt(index);
			DeliveryOwnerManifestVersions.RemoveAt(index);
			DeliveryOwnerManifestDigests.RemoveAt(index);
			DeliveryOwnerManifestRevisions.RemoveAt(index);
			DeliveryManifestSourceStarts.RemoveAt(index);
			DeliveryManifestSourceCounts.RemoveAt(index);
			DeliveryTargetBeforeAmounts.RemoveAt(index);
			DeliveryTargetReceiptStates.RemoveAt(index);
			LegCounts.RemoveAt(index);
		}

		private void RemoveLegs(int from, int count)
		{
			if (count <= 0 || from < 0 || from >= LegZoneIds.Count)
			{
				return;
			}
			int take = (from + count > LegZoneIds.Count) ? (LegZoneIds.Count - from) : count;
			LegZoneIds.RemoveRange(from, take);
			LegEnterX.RemoveRange(from, take);
			LegEnterY.RemoveRange(from, take);
			LegExitX.RemoveRange(from, take);
			LegExitY.RemoveRange(from, take);
			LegLengths.RemoveRange(from, take);
			LegDepartTicks.RemoveRange(from, take);
			LegArriveTicks.RemoveRange(from, take);
		}

		private static List<T> Repair<T>(List<T> column)
		{
			return column ?? new List<T>();
		}

		private static int Shortest(int[] counts)
		{
			int shortest = int.MaxValue;
			for (int i = 0; i < counts.Length; i++)
			{
				if (counts[i] < shortest)
				{
					shortest = counts[i];
				}
			}
			return (shortest == int.MaxValue) ? 0 : shortest;
		}

		private static void Trim<T>(List<T> column, int count)
		{
			if (column.Count > count)
			{
				column.RemoveRange(count, column.Count - count);
			}
		}

		private static void Pad<T>(List<T> column, int count, T value)
		{
			while (column.Count < count)
			{
				column.Add(value);
			}
		}
	}

#if TAF_TESTS
	/// <summary>Engine-free fixture for the exact realm-archive job segment. Production owns the
	/// same field order in <c>KingdomRealmArchive.WriteJobs/ReadJobs</c>; this seam freezes v2 bytes
	/// and executes v2 padding, v3 rewrite, and repeated cold reads without mocking Qud's serializer.</summary>
	internal static class KingdomRealmJobWireFixture
	{
		internal const int LegacyVersion = 2;
		internal const int MissionVersion = 3;
		internal const int CurrentVersion = 4;
		private const int MaxJobs = 16;
		private const int MaxLegs = 96;
		private const int MaxChars = 512;
		private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

		internal static bool TryEncode(KingdomJobRegistry value, int version, out byte[] payload)
		{
			payload = null;
			if (value == null || (version != LegacyVersion && version != MissionVersion
				&& version != CurrentVersion)) return false;
			try
			{
				KingdomJobTable table;
				KingdomCityFault fault;
				if (!value.TryRead(out table, out fault)) return false;
				KingdomJobRegistry canonical = new KingdomJobRegistry { JobCounter = value.JobCounter };
				if (!canonical.TryPublish(table, out fault)) return false;
				if (version == LegacyVersion)
				{
					for (int i = 0; i < canonical.Count; i++)
						if (canonical.Kinds[i] != (int)KingdomJobKind.Delivery) return false;
				}
				if (version < CurrentVersion)
				{
					for (int i = 0; i < canonical.Count; i++)
						if (canonical.DeliveryPhases[i] != (int)KingdomDeliveryPhase.Legacy)
							return false;
				}
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(canonical.JobCounter);
					writer.Write(canonical.Count);
					for (int i = 0; i < canonical.Count; i++)
					{
						writer.Write(canonical.JobIds[i]); writer.Write(canonical.Kinds[i]);
						writer.Write(canonical.Cargos[i]); writer.Write(canonical.CargoAmounts[i]);
						WriteText(writer, canonical.SourceZoneIds[i]);
						WriteText(writer, canonical.DestZoneIds[i]);
						writer.Write(canonical.StartTicks[i]);
						writer.Write(canonical.WalkTicksPerCell[i]);
						writer.Write(canonical.Statuses[i]); writer.Write(canonical.OriginCodes[i]);
						writer.Write(canonical.DepositLegIndexes[i]);
						if (version >= MissionVersion)
						{
							writer.Write(canonical.SubjectIds[i]);
							WriteText(writer, canonical.SubjectNames[i]);
							WriteText(writer, canonical.TargetNames[i]);
							writer.Write(canonical.DueTicks[i]); writer.Write(canonical.WaterCosts[i]);
							writer.Write(canonical.ProvisionCosts[i]); writer.Write(canonical.OutcomeCodes[i]);
						}
						if (version >= CurrentVersion)
						{
							writer.Write(canonical.DeliverySourceEndpointIds[i]);
							WriteText(writer, canonical.DeliverySourceObjectIds[i]);
							writer.Write(canonical.DeliverySourceXs[i]);
							writer.Write(canonical.DeliverySourceYs[i]);
							writer.Write(canonical.DeliveryTargetEndpointIds[i]);
							WriteText(writer, canonical.DeliveryTargetObjectIds[i]);
							writer.Write(canonical.DeliveryTargetXs[i]);
							writer.Write(canonical.DeliveryTargetYs[i]);
							writer.Write(canonical.DeliverySourceBeforeAmounts[i]);
							writer.Write(canonical.DeliveryTripIds[i]);
							writer.Write(canonical.DeliveryStopOrdinals[i]);
							writer.Write(canonical.DeliveryPhases[i]);
							writer.Write(canonical.DeliveryCargoAuthorityKinds[i]);
							WriteText(writer, canonical.DeliveryOwnerOperationIds[i]);
							writer.Write(canonical.DeliveryOwnerManifestVersions[i]);
							WriteText(writer, canonical.DeliveryOwnerManifestDigests[i]);
							writer.Write(canonical.DeliveryOwnerManifestRevisions[i]);
							writer.Write(canonical.DeliveryManifestSourceStarts[i]);
							writer.Write(canonical.DeliveryManifestSourceCounts[i]);
							writer.Write(canonical.DeliveryTargetBeforeAmounts[i]);
							writer.Write(canonical.DeliveryTargetReceiptStates[i]);
						}
						writer.Write(canonical.LegCounts[i]);
					}
					writer.Write(canonical.LegZoneIds.Count);
					for (int i = 0; i < canonical.LegZoneIds.Count; i++)
					{
						WriteText(writer, canonical.LegZoneIds[i]);
						writer.Write(canonical.LegEnterX[i]); writer.Write(canonical.LegEnterY[i]);
						writer.Write(canonical.LegExitX[i]); writer.Write(canonical.LegExitY[i]);
						writer.Write(canonical.LegLengths[i]);
						writer.Write(canonical.LegDepartTicks[i]); writer.Write(canonical.LegArriveTicks[i]);
					}
					writer.Flush();
					payload = stream.ToArray();
					return true;
				}
			}
			catch { payload = null; return false; }
		}

		internal static bool TryDecode(byte[] payload, int version, out KingdomJobRegistry value)
		{
			value = null;
			if (payload == null || (version != LegacyVersion && version != MissionVersion
				&& version != CurrentVersion)) return false;
			try
			{
				KingdomJobRegistry read = new KingdomJobRegistry();
				using (MemoryStream stream = new MemoryStream(payload, false))
				using (BinaryReader reader = new BinaryReader(stream, StrictUtf8, true))
				{
					read.JobCounter = reader.ReadInt32();
					int jobs = reader.ReadInt32();
					if (read.JobCounter < 0 || jobs < 0 || jobs > MaxJobs) return false;
					for (int i = 0; i < jobs; i++)
					{
						read.JobIds.Add(reader.ReadInt32()); read.Kinds.Add(reader.ReadInt32());
						read.Cargos.Add(reader.ReadInt32()); read.CargoAmounts.Add(reader.ReadInt32());
						read.SourceZoneIds.Add(ReadText(reader)); read.DestZoneIds.Add(ReadText(reader));
						read.StartTicks.Add(reader.ReadInt64()); read.WalkTicksPerCell.Add(reader.ReadInt32());
						read.Statuses.Add(reader.ReadInt32()); read.OriginCodes.Add(reader.ReadInt32());
						read.DepositLegIndexes.Add(reader.ReadInt32());
						if (version >= MissionVersion)
						{
							read.SubjectIds.Add(reader.ReadInt32()); read.SubjectNames.Add(ReadText(reader));
							read.TargetNames.Add(ReadText(reader)); read.DueTicks.Add(reader.ReadInt64());
							read.WaterCosts.Add(reader.ReadInt32()); read.ProvisionCosts.Add(reader.ReadInt32());
							read.OutcomeCodes.Add(reader.ReadInt32());
						}
						if (version >= CurrentVersion)
						{
							read.DeliverySourceEndpointIds.Add(reader.ReadInt32());
							read.DeliverySourceObjectIds.Add(ReadText(reader));
							read.DeliverySourceXs.Add(reader.ReadInt32());
							read.DeliverySourceYs.Add(reader.ReadInt32());
							read.DeliveryTargetEndpointIds.Add(reader.ReadInt32());
							read.DeliveryTargetObjectIds.Add(ReadText(reader));
							read.DeliveryTargetXs.Add(reader.ReadInt32());
							read.DeliveryTargetYs.Add(reader.ReadInt32());
							read.DeliverySourceBeforeAmounts.Add(reader.ReadInt64());
							read.DeliveryTripIds.Add(reader.ReadInt32());
							read.DeliveryStopOrdinals.Add(reader.ReadInt32());
							read.DeliveryPhases.Add(reader.ReadInt32());
							read.DeliveryCargoAuthorityKinds.Add(reader.ReadInt32());
							read.DeliveryOwnerOperationIds.Add(ReadText(reader));
							read.DeliveryOwnerManifestVersions.Add(reader.ReadInt32());
							read.DeliveryOwnerManifestDigests.Add(ReadText(reader));
							read.DeliveryOwnerManifestRevisions.Add(reader.ReadInt64());
							read.DeliveryManifestSourceStarts.Add(reader.ReadInt32());
							read.DeliveryManifestSourceCounts.Add(reader.ReadInt32());
							read.DeliveryTargetBeforeAmounts.Add(reader.ReadInt64());
							read.DeliveryTargetReceiptStates.Add(reader.ReadInt32());
						}
						read.LegCounts.Add(reader.ReadInt32());
					}
					int legs = reader.ReadInt32();
					if (legs < 0 || legs > MaxLegs) return false;
					for (int i = 0; i < legs; i++)
					{
						read.LegZoneIds.Add(ReadText(reader)); read.LegEnterX.Add(reader.ReadInt32());
						read.LegEnterY.Add(reader.ReadInt32()); read.LegExitX.Add(reader.ReadInt32());
						read.LegExitY.Add(reader.ReadInt32()); read.LegLengths.Add(reader.ReadInt32());
						read.LegDepartTicks.Add(reader.ReadInt64()); read.LegArriveTicks.Add(reader.ReadInt64());
					}
					if (stream.Position != stream.Length) return false;
				}
				read.Normalize();
				KingdomJobTable table;
				KingdomCityFault fault;
				if (!read.TryRead(out table, out fault)) return false;
				value = read;
				return true;
			}
			catch { value = null; return false; }
		}

		private static void WriteText(BinaryWriter writer, string value)
		{
			if (value == null) { writer.Write(-1); return; }
			if (value.Length > MaxChars) throw new InvalidDataException();
			byte[] bytes = StrictUtf8.GetBytes(value);
			writer.Write(bytes.Length);
			writer.Write(bytes);
		}

		private static string ReadText(BinaryReader reader)
		{
			int length = reader.ReadInt32();
			if (length == -1) return null;
			if (length < 0 || length > MaxChars * 4) throw new InvalidDataException();
			byte[] bytes = reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			string value = StrictUtf8.GetString(bytes);
			if (value.Length > MaxChars) throw new InvalidDataException();
			return value;
		}
	}
#endif
}
