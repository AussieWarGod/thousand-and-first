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
	/// One job and the timed itinerary that answers for it.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.7: <b>a job is a timed itinerary, computed once, at
	/// creation.</b> From this row one pure function answers where the carrier is and what is on
	/// them at any tick, and every zone renders that same answer &mdash; which is invariant I5, and
	/// which is why the body never has to literally traverse anything.
	/// </para>
	/// </summary>
	internal readonly partial struct KingdomJobRow
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

		/// <summary>Exact final rich-find publication decision, frozen with the final outcome.</summary>
		internal readonly KingdomExpeditionDeedDisposition ExpeditionDeedDisposition;

		internal readonly string ExpeditionDeedPolityId;

		internal readonly string ExpeditionDeedCauseRef;

		internal readonly string ExpeditionDeedFigureRef;

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
				= KingdomDeliveryTargetReceiptState.None,
			KingdomExpeditionDeedDisposition expeditionDeedDisposition
				= KingdomExpeditionDeedDisposition.Legacy,
			string expeditionDeedPolityId = null,
			string expeditionDeedCauseRef = null,
			string expeditionDeedFigureRef = null)
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
			ExpeditionDeedDisposition = expeditionDeedDisposition;
			ExpeditionDeedPolityId = expeditionDeedPolityId;
			ExpeditionDeedCauseRef = expeditionDeedCauseRef;
			ExpeditionDeedFigureRef = expeditionDeedFigureRef;
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
	}
}
