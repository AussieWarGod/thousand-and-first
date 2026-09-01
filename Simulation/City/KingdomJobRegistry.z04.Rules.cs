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
	/// The pure half of the porter: planning an itinerary, reading a carrier's cargo off it, and
	/// the two draws a delivery is allowed.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.7 and &sect;3.10. Engine-free and total. <b>No draw
	/// anywhere in the routing</b> &mdash; routing is arithmetic, not chance (&sect;3.10(4),
	/// <c>KingdomBudgetRules.PlannerMaxDraws</c> is zero) &mdash; and the two draws that do exist
	/// are flavour: which edge cell the carrier walks in by, and where they say they are from.
	/// </para>
	/// </summary>
	internal static partial class KingdomJobRules
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
		/// frozen salvage draw; a prepared result may carry any final answer, including recall.</summary>
		internal static bool ValidExpeditionOutcomeForPhase(int phase, int outcome)
		{
			if (phase == (int)KingdomExpeditionPhase.ResolutionPrepared)
				return outcome >= (int)KingdomExpeditionOutcome.PickedClean
					&& outcome <= (int)KingdomExpeditionOutcome.ResidentJoinedFounder;
			return outcome >= (int)KingdomExpeditionOutcome.PickedClean
				&& outcome <= (int)KingdomExpeditionOutcome.RichFind;
		}

		internal static bool ValidExpeditionResultReceipt(KingdomJobRow row)
		{
			return ValidExpeditionResultReceipt(row.Kind, row.OriginCode, row.OutcomeCode,
				row.ExpeditionDeedDisposition, row.ExpeditionDeedPolityId,
				row.ExpeditionDeedCauseRef, row.ExpeditionDeedFigureRef);
		}

		internal static bool ValidExpeditionResultReceipt(KingdomJobKind kind, int phase,
			int outcome, KingdomExpeditionDeedDisposition disposition, string polityId,
			string causeRef, string figureRef)
		{
			bool empty = string.IsNullOrEmpty(polityId) && string.IsNullOrEmpty(causeRef)
				&& string.IsNullOrEmpty(figureRef);
			if (kind != KingdomJobKind.Expedition)
				return disposition == KingdomExpeditionDeedDisposition.Legacy
					&& empty;
			if (phase != (int)KingdomExpeditionPhase.ResolutionPrepared)
				return disposition == KingdomExpeditionDeedDisposition.Legacy
					&& empty;
			if (disposition == KingdomExpeditionDeedDisposition.Legacy)
				return empty && outcome >=
					(int)KingdomExpeditionOutcome.ResidentDiedOnGround;
			if (disposition == KingdomExpeditionDeedDisposition.NotApplicable)
				return empty;
			return outcome == (int)KingdomExpeditionOutcome.RichFind
				&& (disposition == KingdomExpeditionDeedDisposition.Promote
					|| disposition == KingdomExpeditionDeedDisposition.Skip)
				&& !string.IsNullOrEmpty(polityId) && !string.IsNullOrEmpty(causeRef)
				&& !string.IsNullOrEmpty(figureRef);
		}

		internal static bool IsDeliveryPhase(int stored)
		{
			return stored == (int)KingdomDeliveryPhase.Legacy
				|| stored == (int)KingdomDeliveryPhase.Planned
				|| stored == (int)KingdomDeliveryPhase.SourceDebitPrepared
				|| stored == (int)KingdomDeliveryPhase.InFlight
				|| stored == (int)KingdomDeliveryPhase.ReservationPrepared
				|| stored == (int)KingdomDeliveryPhase.Quarantined
				|| stored == (int)KingdomDeliveryPhase.LandedAwaitingOwner;
		}

		/// <summary>True when capacity and overlap are denominated in exact source-object
		/// ordinals rather than scalar resource units.</summary>
		internal static bool UsesExactObjectRange(KingdomDeliveryCargoAuthority authority,
			KingdomStockKind cargo)
		{
			return authority == KingdomDeliveryCargoAuthority.CarryBookManifest
				|| authority == KingdomDeliveryCargoAuthority.ConstructionInput;
		}

		/// <summary>One trip's conserved carrier load. Existing scalar and CarryBook meanings are
		/// unchanged; every construction input is one exact-object manifest slice.</summary>
		internal static long DeliveryCapacityLoad(KingdomDeliveryCargoAuthority authority,
			KingdomStockKind cargo, int cargoAmount, int manifestSourceCount)
		{
			return UsesExactObjectRange(authority, cargo)
				? manifestSourceCount : cargoAmount;
		}

		internal static bool IsCentralDelivery(KingdomJobRow row)
		{
			return row.Kind == KingdomJobKind.Delivery
				&& row.DeliveryPhase != KingdomDeliveryPhase.Legacy;
		}
	}
}
