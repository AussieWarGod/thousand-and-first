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
		Quarantined = 5,

		/// <summary>Construction cargo reached its frozen destination, but remains owned by the
		/// construction-input receipt until that parent durably proves consumption.</summary>
		LandedAwaitingOwner = 6
	}

	/// <summary>Who owns cargo identity and physical callback receipts for a central trip.</summary>
	internal enum KingdomDeliveryCargoAuthority : int
	{
		/// <summary>City water/food: exact source and target containers plus scalar receipt.</summary>
		ScalarStock = 0,

		/// <summary>CarryBook v6: ordered whole GameObject stacks remain exact references.</summary>
		CarryBookManifest = 1,

		/// <summary>One construction-input receipt owns liquid or exact-object cargo until the
		/// parent construction transaction acknowledges it.</summary>
		ConstructionInput = 2
	}

	/// <summary>Durable bracket around one scalar target callback. The physical target receives
	/// an exact trip/job marker before mutation; this state says that marker and the before amount
	/// are authoritative recovery evidence. CarryBookManifest keeps this neutral.</summary>
	internal enum KingdomDeliveryTargetReceiptState : int
	{
		None = 0,
		Prepared = 1
	}
}
