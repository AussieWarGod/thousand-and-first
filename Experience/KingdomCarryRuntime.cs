using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>Engine edge for carry-sign v6. CarryBook owns one frozen whole-object manifest;
	/// central logistics owns its trips; this class only scans, invokes exact callbacks, and drives
	/// the two authorities in their published order.</summary>
	internal static partial class KingdomCarryRuntime
	{
		internal sealed class PlantPlan
		{
			internal KingdomSystem System;
			internal KingdomCarryBook Book;
			internal KingdomCarryOperation Operation;
			internal GameObject Actor;
			internal GameObject Sign;
			internal Zone SourceZone;
			internal Cell SourceCell;
			internal GameObject Container;
			internal List<GameObject> Sources;
			internal KingdomLifecycleTopology SourceTopology;
			internal string SourceOwnerId;
			internal string SourceHolderObjectId;
			internal string ActorObjectId;
			internal string SignObjectId;
			internal List<string> SourceObjectIds;
			internal int TargetX;
			internal int TargetY;
			internal long RouteArrivalTick;
			internal int Days;
			internal string Description;
		}

		internal static bool HasOpenOrLegacy(KingdomSystem system)
		{
			KingdomCarryBook book = Authority(system);
			return book == null || book.Open != null || LegacyMaterialUnits(system == null
				? null : system.Haul) > 0;
		}

		/// <summary>Read-only exact scan and route preview. No reservation, body, sign, or cargo is
		/// changed before the caller has shown Description and Days to the founder.</summary>
		internal static bool TryPreparePlant(KingdomSystem system, GameObject actor,
			GameObject sign, Zone zone, Cell cell, long now, out PlantPlan plan,
			out string failure)
		{
			plan = null;
			failure = null;
			KingdomCarryBook book = Authority(system);
			if (book == null || system == null || !system.Founded)
			{
				failure = "There is no proved realm authority for a carry-sign.";
				return false;
			}
			if (book.Open != null || LegacyMaterialUnits(system.Haul) > 0)
			{
				failure = KingdomGuestRules.PlantRefusal(
					KingdomGuestRules.PlantVerdict.AlreadyInFlight);
				return false;
			}
			if (!ExactSign(actor, sign, zone))
			{
				failure = "The sign must be an unequipped carry-sign held directly in your inventory.";
				return false;
			}
			GameObject container;
			List<GameObject> sources;
			KingdomLifecycleTopology topology;
			string ownerId;
			string holderId;
			if (!TryScanDesignation(actor, sign, cell, zone, out container, out sources,
				out topology, out ownerId, out holderId, out failure)) return false;

			string destinationId = system.CurrentSettlementId;
			string destinationZone = system.SettlementIdentityFirstClaimedZone;
			if (!KingdomIdentityRules.IsSettlementId(destinationId)
				|| string.IsNullOrEmpty(destinationZone)
				|| system.ClaimedZones == null || !system.ClaimedZones.Contains(destinationZone))
			{
				failure = "The city's immutable home ground cannot be proved. The load was not marked.";
				return false;
			}
			int targetX;
			int targetY;
			KingdomCityFault fault;
			if (!KingdomCentralLogistics.TryManifestSpillAnchor(system, destinationZone,
				out targetX, out targetY, out fault))
			{
				failure = "The porters have no measured safe destination for this load yet.";
				return false;
			}
			KingdomCarryOperation operation = KingdomLifecycleRules.PrepareExactCarry(book, now);
			if (operation == null)
			{
				failure = "The carry book cannot reserve another exact manifest.";
				return false;
			}
			operation.OriginSettlementId = destinationId;
			operation.OriginZoneId = zone.ZoneID;
			operation.OriginX = cell.X;
			operation.OriginY = cell.Y;
			operation.DestinationSettlementId = destinationId;
			operation.DestinationSettlementName = system.SeatName;
			operation.DestinationTopology = KingdomLifecycleTopology.Cell;
			operation.DestinationOwnerId = null;
			operation.DestinationZoneId = destinationZone;
			operation.DestinationX = targetX;
			operation.DestinationY = targetY;
			operation.SpillZoneId = destinationZone;
			operation.SpillX = targetX;
			operation.SpillY = targetY;

			long routeArrival;
			if (!KingdomCentralLogistics.TryPreviewManifestRoute(system, zone, operation.Id,
				holderId, zone.ZoneID, cell.X, cell.Y, "", destinationZone, targetX, targetY,
				now, out routeArrival, out fault))
			{
				failure = KingdomGuestRules.PlantRefusal(KingdomGuestRules.PlantVerdict.NoRoad);
				return false;
			}
			int days;
			if (!TryDistanceDays(zone.ZoneID, destinationZone, out days)
				|| now > long.MaxValue - (long)days * KingdomRules.TicksPerDay)
			{
				failure = "The porters cannot reckon a truthful arrival for that road.";
				return false;
			}
			operation.DueTick = KingdomGuestRules.HaulDueTick(now, days);
			string description = Describe(sources);
			if (string.IsNullOrEmpty(description))
			{
				failure = KingdomGuestRules.PlantRefusal(
					KingdomGuestRules.PlantVerdict.NothingToCarry);
				return false;
			}
			plan = new PlantPlan
			{
				System = system, Book = book, Operation = operation, Actor = actor, Sign = sign,
				SourceZone = zone, SourceCell = cell, Container = container, Sources = sources,
				SourceTopology = topology, SourceOwnerId = ownerId,
				SourceHolderObjectId = holderId, TargetX = targetX, TargetY = targetY,
				RouteArrivalTick = routeArrival, Days = days, Description = description
			};
			return true;
		}

		/// <summary>After consent, repeats the exact scan, assigns identities only to that confirmed
		/// set, reserves central trips, and publishes CarryBook before any physical callback.</summary>
		internal static bool PublishPlant(PlantPlan plan, out string failure)
		{
			failure = null;
			if (plan == null || plan.System == null || plan.Book == null
				|| plan.Operation == null || !ReferenceEquals(Authority(plan.System), plan.Book)
				|| plan.Book.Open != null || LegacyMaterialUnits(plan.System.Haul) > 0
				|| !ExactSign(plan.Actor, plan.Sign, plan.SourceZone))
			{
				failure = "The sign or carry authority changed before it could be planted.";
				return false;
			}
			GameObject rescannedContainer;
			List<GameObject> rescanned;
			KingdomLifecycleTopology topology;
			string ownerId;
			string holderId;
			if (!TryScanDesignation(plan.Actor, plan.Sign, plan.SourceCell, plan.SourceZone,
				out rescannedContainer, out rescanned, out topology, out ownerId, out holderId,
				out failure) || !SameDesignation(plan, rescannedContainer, rescanned, topology,
					ownerId, holderId))
			{
				if (string.IsNullOrEmpty(failure))
					failure = "The marked pile changed after confirmation; nothing was taken.";
				return false;
			}
			if (!TryAssignConfirmedIdentities(plan, out failure)) return false;

			KingdomManifestReservation reservation;
			KingdomCityFault fault;
			if (!KingdomCentralLogistics.TryPrepareManifestReservation(plan.System,
				plan.SourceZone, plan.Operation.Id, plan.SourceHolderObjectId,
				plan.SourceZone.ZoneID, plan.SourceCell.X, plan.SourceCell.Y, "",
				plan.Operation.DestinationZoneId, plan.TargetX, plan.TargetY,
				plan.Sources.Count, plan.Operation.CreatedTick, out reservation, out fault)
				|| reservation.ArrivalTick != plan.RouteArrivalTick)
			{
				KingdomCentralLogistics.TryCancelManifestReservation(plan.System,
					plan.Operation.Id, out fault);
				failure = "The measured road changed before the porters could reserve it.";
				return false;
			}
			bool published = false;
			try
			{
				CarryWorld scheduleWorld = new CarryWorld(plan.System, plan.SourceZone,
					plan.Book, plan.Operation);
				if (!KingdomLifecycleRules.TrustedAdapter.PrepareCarrySchedule(plan.Book,
					plan.Operation, scheduleWorld))
				{
					failure = "The carry arrival clock could not be leased exactly.";
					return false;
				}
				for (int i = 0; i < plan.Sources.Count; i++)
				{
					GameObject item = plan.Sources[i];
					int x = plan.SourceTopology == KingdomLifecycleTopology.Cell
						? plan.SourceCell.X : -1;
					int y = plan.SourceTopology == KingdomLifecycleTopology.Cell
						? plan.SourceCell.Y : -1;
					KingdomCarrySource source = KingdomLifecycleRules.PrepareExactCarrySource(
						plan.Operation, i, plan.SourceObjectIds[i], item.Blueprint,
						plan.SourceTopology,
						plan.SourceOwnerId, plan.SourceZone.ZoneID, x, y, item.Count);
					if (source == null) { failure = "A cargo object could not be frozen exactly."; return false; }
					plan.Operation.Sources.Add(source);
					KingdomLifecycleProjection output = KingdomLifecycleRules
						.PrepareExactCarryOutput(plan.Operation, i, source,
							KingdomLifecycleTopology.Cell, null,
							plan.Operation.DestinationZoneId, plan.TargetX, plan.TargetY);
					if (output == null) { failure = "The safe spill destination could not be frozen."; return false; }
					plan.Operation.Outputs.Add(output);
				}
				if (!KingdomLifecycleRules.FreezeExactCarryManifest(plan.Operation,
					plan.SignObjectId, plan.Sign.Blueprint,
					KingdomLifecycleTopology.Inventory, plan.ActorObjectId,
					plan.SourceZone.ZoneID, -1, -1, plan.Sign.Count,
					reservation.JobIds, reservation.TripIds))
				{
					failure = "The exact sign and trip manifest could not be frozen.";
					return false;
				}
				plan.Operation.Outbox = CarryOutbox(plan.Operation, plan.Description);
				if (plan.Operation.Outbox == null
					|| !KingdomLifecycleRules.TryPublishCarry(plan.Book, plan.Operation))
				{
					failure = "The carry book refused the frozen manifest.";
					return false;
				}
				published = true;
				if (!KingdomCentralLogistics.TryActivateManifestReservation(plan.System,
					plan.Operation.Id, plan.Operation.ManifestVersion,
					plan.Operation.ManifestDigest, plan.Operation.ManifestRevision,
					reservation.JobIds, reservation.TripIds, reservation.ArrivalTick, out fault))
				{
					KingdomLog.Log("carry-sign: published; central activation waits: " + fault);
				}
				if (!Drive(plan.System, plan.SourceZone, plan.Operation.CreatedTick))
					KingdomLog.Log("carry-sign: exact manifest published and retained for recovery");
				return true;
			}
			finally
			{
				if (!published)
					KingdomCentralLogistics.TryCancelManifestReservation(plan.System,
						plan.Operation.Id, out fault);
			}
		}
	}
}
