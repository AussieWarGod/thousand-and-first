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
	internal static class KingdomCarryRuntime
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

		/// <summary>After consent, repeats the exact scan, reserves central trips, freezes the
		/// manifest and publishes CarryBook before any sign/cargo callback.</summary>
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
						plan.Operation, i, item.ID, item.Blueprint, plan.SourceTopology,
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
					plan.Sign.ID, plan.Sign.Blueprint, KingdomLifecycleTopology.Inventory,
					plan.Actor.ID, plan.SourceZone.ZoneID, -1, -1, plan.Sign.Count,
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

		internal static bool Drive(KingdomSystem system, Zone zone, long now)
		{
			KingdomCarryBook book = Authority(system);
			if (book == null || zone == null) return false;
			for (int guard = 0; guard < 256; guard++)
			{
				KingdomCarryOperation op = book.Open;
				if (op == null) return true;
				if (op.AuthorityKind != KingdomCarryAuthorityKind.ExactManifest
					|| op.Phase == KingdomLifecyclePhase.Quarantined) return false;
				try
				{
					switch (op.Phase)
					{
					case KingdomLifecyclePhase.Prepared:
						if (!ActivateReservation(system, op)) return false;
						if (!KingdomLifecycleRules.TrustedAdapter.ProveExactCarrySign(book,
							op, new CarryWorld(system, zone, book, op))) return false;
						if (!KingdomLifecycleRules.AdvanceCarryPhase(book, op,
							KingdomLifecyclePhase.RemovalIntent, now)) return false;
						break;
					case KingdomLifecyclePhase.RemovalIntent:
						if (!SettlePickups(system, zone, book, op, now)) return false;
						if (op.SourceIndex < op.Sources.Count) return true;
						if (!KingdomLifecycleRules.AdvanceCarryPhase(book, op,
							KingdomLifecyclePhase.Removed, now)) return false;
						break;
					case KingdomLifecyclePhase.Removed:
						if (!KingdomLifecycleRules.AdvanceCarryPhase(book, op,
							KingdomLifecyclePhase.ScheduleIntent, now)) return false;
						break;
					case KingdomLifecyclePhase.ScheduleIntent:
						if (!KingdomLifecycleRules.TrustedAdapter.ProveCarrySchedule(book, op,
							new CarryWorld(system, zone, book, op))) return false;
						if (!KingdomLifecycleRules.AdvanceCarryPhase(book, op,
							KingdomLifecyclePhase.ProjectionIntent, now)) return false;
						break;
					case KingdomLifecyclePhase.ProjectionIntent:
						if (!string.Equals(zone.ZoneID, op.DestinationZoneId,
							StringComparison.Ordinal) || now < op.DueTick) return true;
						if (ThreatPresent(system, zone))
						{
							if (!op.DestinationSafetyWaiting
								&& !KingdomLifecycleRules.TrustedAdapter
									.SetExactCarryDestinationSafety(book, op, true, now)) return false;
							return true;
						}
						if (op.DestinationSafetyWaiting
							&& !KingdomLifecycleRules.TrustedAdapter
								.SetExactCarryDestinationSafety(book, op, false, now)) return false;
						if (!SettleDestinations(system, zone, book, op, now)) return false;
						if (op.OutputIndex < op.Outputs.Count) return true;
						if (!KingdomLifecycleRules.AdvanceCarryPhase(book, op,
							KingdomLifecyclePhase.Projected, now)) return false;
						break;
					case KingdomLifecyclePhase.Projected:
						if (!KingdomLifecycleRules.AdvanceCarryPhase(book, op,
							KingdomLifecyclePhase.Sinks, now)) return false;
						break;
					case KingdomLifecyclePhase.Sinks:
						if (!SettleSinks(system, book, op)) return false;
						if (!KingdomLifecycleRules.AdvanceCarryPhase(book, op,
							KingdomLifecyclePhase.Terminal, now)) return false;
						break;
					case KingdomLifecyclePhase.Terminal:
						return KingdomLifecycleRules.RetireCarry(book, op, now);
					default:
						return false;
					}
				}
				catch (Exception error)
				{
					MetricsManager.LogError("ThousandAndFirst carry-sign lifecycle", error);
					return false;
				}
			}
			return false;
		}

		private static bool SettlePickups(KingdomSystem system, Zone zone,
			KingdomCarryBook book, KingdomCarryOperation op, long now)
		{
			if (!string.Equals(zone.ZoneID, op.OriginZoneId, StringComparison.Ordinal)) return true;
			if (!ActivateReservation(system, op)) return false;
			KingdomPorters.Render(system, zone, now);
			while (op.SourceIndex < op.Sources.Count)
			{
				int ordinal = op.SourceIndex;
				KingdomManifestTripView trip;
				if (!KingdomCentralLogistics.TryManifestTrip(system, op.Id, ordinal, out trip)
					|| trip.Phase != KingdomDeliveryPhase.SourceDebitPrepared
					|| !trip.CarrierAvailable || string.IsNullOrEmpty(trip.CarrierObjectId)
					|| !string.Equals(trip.CarrierZoneId, zone.ZoneID,
						StringComparison.Ordinal)) return true;
				if (!KingdomLifecycleRules.TrustedAdapter.ProveExactCarryPickup(book, op,
					op.Sources[ordinal], trip.TripId, trip.CarrierObjectId, trip.CarrierZoneId,
					new CarryWorld(system, zone, book, op))) return false;
				if (TripSourcesLoaded(op, trip))
				{
					KingdomCityFault fault;
					if (!KingdomCentralLogistics.TryAcknowledgeManifestPickup(system, op.Id,
						trip.TripId, op.ManifestRevision, out fault)) return false;
				}
			}
			return true;
		}

		private static bool SettleDestinations(KingdomSystem system, Zone zone,
			KingdomCarryBook book, KingdomCarryOperation op, long now)
		{
			while (op.OutputIndex < op.Outputs.Count)
			{
				int ordinal = op.OutputIndex;
				KingdomManifestTripView trip;
				KingdomCityFault fault;
				if (!KingdomCentralLogistics.TryManifestTrip(system, op.Id, ordinal, out trip)
					|| trip.Phase != KingdomDeliveryPhase.InFlight) return false;
				if (!KingdomCentralLogistics.TryMaterializeManifestArrival(system, op.Id,
					trip.TripId, zone, now, out fault)) return false;
				if (!KingdomCentralLogistics.TryManifestTrip(system, op.Id, ordinal, out trip)
					|| !trip.CarrierAvailable
					|| !string.Equals(trip.CarrierZoneId, op.DestinationZoneId,
						StringComparison.Ordinal)) return false;
				KingdomCarrySource source = op.Sources[ordinal];
				KingdomLifecycleProjection output = op.Outputs[ordinal];
				if (!KingdomLifecycleRules.TrustedAdapter.ProveExactCarryDestination(book,
					op, source, output, false, output.Topology, output.OwnerId, output.ZoneId,
					output.X, output.Y, new CarryWorld(system, zone, book, op))) return false;
				if (TripOutputsSettled(op, trip))
				{
					if (!KingdomCentralLogistics.TryAcknowledgeManifestDelivered(system, op.Id,
						trip.TripId, op.ManifestRevision, out fault)) return false;
				}
			}
			return true;
		}

		private static bool ActivateReservation(KingdomSystem system, KingdomCarryOperation op)
		{
			KingdomCityFault fault;
			return op != null && KingdomCentralLogistics.TryActivateManifestReservation(system,
				op.Id, op.ManifestVersion, op.ManifestDigest, op.ManifestRevision,
				op.JobIds.ToArray(), op.TripIds.ToArray(), RouteArrival(system, op), out fault);
		}

		private static long RouteArrival(KingdomSystem system, KingdomCarryOperation op)
		{
			KingdomJobTable table;
			KingdomCityFault fault;
			long arrival = 0L;
			if (system == null || system.Jobs == null || op == null
				|| !system.Jobs.TryRead(out table, out fault)) return -1L;
			for (int i = 0; i < op.JobIds.Count; i++)
			{
				KingdomJobRow row;
				KingdomLeg last;
				if (!table.TryGet(op.JobIds[i], out row) || !row.TryLeg(row.LegCount - 1, out last))
					return -1L;
				if (last.ArriveTick > arrival) arrival = last.ArriveTick;
			}
			return arrival;
		}

		private static bool TripSourcesLoaded(KingdomCarryOperation op,
			KingdomManifestTripView trip)
		{
			int end = trip.SourceStart + trip.SourceCount;
			if (op == null || trip.SourceStart < 0 || end > op.Sources.Count) return false;
			for (int i = trip.SourceStart; i < end; i++)
				if (op.Sources[i].LoadedCount != op.Sources[i].PlannedCount) return false;
			return true;
		}

		private static bool TripOutputsSettled(KingdomCarryOperation op,
			KingdomManifestTripView trip)
		{
			int end = trip.SourceStart + trip.SourceCount;
			if (op == null || trip.SourceStart < 0 || end > op.Sources.Count) return false;
			for (int i = trip.SourceStart; i < end; i++)
				if (op.Sources[i].DeliveredCount != op.Sources[i].PlannedCount) return false;
			return true;
		}

		private static bool SettleSinks(KingdomSystem system, KingdomCarryBook book,
			KingdomCarryOperation op)
		{
			if (!KingdomLifecycleRules.RecoverCarryOutbox(book, op)) return false;
			KingdomLifecycleSinkMask[] sinks =
			{
				KingdomLifecycleSinkMask.Chronicle, KingdomLifecycleSinkMask.Ledger,
				KingdomLifecycleSinkMask.Message
			};
			for (int i = 0; i < sinks.Length; i++)
			{
				KingdomLifecycleSinkMask sink = sinks[i];
				if (CarrySinkState(op.Outbox, sink) != KingdomLifecycleSinkState.Pending) continue;
				if (!KingdomLifecycleRules.BeginCarrySink(book, op, sink)) return false;
				bool delivered;
				switch (sink)
				{
				case KingdomLifecycleSinkMask.Chronicle:
					delivered = KingdomChronicle.RecordOnce(system,
						op.Outbox.ChronicleReceiptId, op.Outbox.Chronicle, false);
					break;
				case KingdomLifecycleSinkMask.Ledger:
					system.Ledger.Note(op.Outbox.Ledger); delivered = true; break;
				case KingdomLifecycleSinkMask.Message:
					MessageQueue.AddPlayerMessage(op.Outbox.Message); delivered = true; break;
				default: delivered = false; break;
				}
				if (!delivered || !KingdomLifecycleRules.CommitCarrySink(book, op, sink))
					return false;
			}
			return true;
		}

		private static KingdomLifecycleSinkState CarrySinkState(KingdomLifecycleOutbox box,
			KingdomLifecycleSinkMask sink)
		{
			if (box == null) return KingdomLifecycleSinkState.Lost;
			switch (sink)
			{
			case KingdomLifecycleSinkMask.Chronicle: return box.ChronicleState;
			case KingdomLifecycleSinkMask.Ledger: return box.LedgerState;
			case KingdomLifecycleSinkMask.Message: return box.MessageState;
			default: return KingdomLifecycleSinkState.Lost;
			}
		}

		private static KingdomLifecycleOutbox CarryOutbox(KingdomCarryOperation op,
			string description)
		{
			if (op == null || string.IsNullOrEmpty(description)) return null;
			string destination = KingdomPresentation.Rich(op.DestinationSettlementName);
			return new KingdomLifecycleOutbox
			{
				OperationId = op.Id,
				EventId = KingdomLifecycleRules.ChildId(op.Id, "outbox", 0),
				ChronicleReceiptId = KingdomLifecycleRules.ChildId(op.Id, "chronicle", 0),
				Chronicle = KingdomGuestRules.DeliveredChronicleLine(
					destination, description),
				ChronicleDisposition = KingdomLifecycleSinkDisposition.Deliver,
				ChronicleState = KingdomLifecycleSinkState.Pending,
				Ledger = KingdomGuestRules.DeliveredLedgerNote(description),
				LedgerDisposition = KingdomLifecycleSinkDisposition.Deliver,
				LedgerState = KingdomLifecycleSinkState.Pending,
				Message = "{{G|The carry-sign's exact load has reached "
					+ destination + ".}}",
				MessageDisposition = KingdomLifecycleSinkDisposition.Deliver,
				MessageState = KingdomLifecycleSinkState.Pending,
				DeedDisposition = KingdomLifecycleSinkDisposition.Skip,
				DeedState = KingdomLifecycleSinkState.Skipped,
				GuestbookDisposition = KingdomLifecycleSinkDisposition.Skip,
				GuestbookState = KingdomLifecycleSinkState.Skipped
			};
		}

		private static bool TryScanDesignation(GameObject actor, GameObject sign, Cell cell,
			Zone zone, out GameObject container, out List<GameObject> sources,
			out KingdomLifecycleTopology topology, out string ownerId, out string holderId,
			out string failure)
		{
			container = null;
			sources = new List<GameObject>();
			topology = KingdomLifecycleTopology.None;
			ownerId = null;
			holderId = "";
			failure = null;
			if (cell == null || zone == null || cell.ParentZone != zone)
			{
				failure = "There is no exact ground here to mark.";
				return false;
			}
			List<GameObject> ground = new List<GameObject>(cell.GetObjects());
			for (int i = 0; i < ground.Count; i++)
			{
				GameObject item = ground[i];
				if (!GameObject.Validate(item) || item.IsCreature || item.IsPlayer()
					|| item.Inventory == null || !ReferenceEquals(item.CurrentCell, cell)) continue;
				if (container != null)
				{
					failure = "More than one container stands here; the sign cannot guess which one you mean.";
					return false;
				}
				container = item;
			}
			if (container != null)
			{
				if (!FounderOwned(container) || container.IsImportant() || container.IsOwned())
				{
					failure = "That container is not unambiguously yours to designate.";
					return false;
				}
				if (container.Inventory.Objects.Count > KingdomLifecycleRules.MaxCarrySources)
				{
					failure = "That container holds more whole objects than one carry-sign can name.";
					return false;
				}
				for (int i = 0; i < container.Inventory.Objects.Count; i++)
				{
					GameObject item = container.Inventory.Objects[i];
					if (!ReferenceEquals(item == null ? null : item.InInventory, container)
						|| !EligibleSource(item, actor, sign, out failure)) return false;
					sources.Add(item);
				}
				topology = KingdomLifecycleTopology.Inventory;
				ownerId = container.ID;
				holderId = container.ID;
			}
			else
			{
				for (int i = 0; i < ground.Count; i++)
				{
					GameObject item = ground[i];
					if (!GameObject.Validate(item) || ReferenceEquals(item, actor)
						|| ReferenceEquals(item, sign) || item.IsCreature || item.IsPlayer()
						|| !ReferenceEquals(item.CurrentCell, cell) || item.InInventory != null)
						continue;
					if (!CargoShaped(item)) continue;
					if (!EligibleSource(item, actor, sign, out failure)) return false;
					sources.Add(item);
					if (sources.Count > KingdomLifecycleRules.MaxCarrySources)
					{
						failure = "That pile holds more whole objects than one carry-sign can name.";
						return false;
					}
				}
				topology = KingdomLifecycleTopology.Cell;
			}
			if (sources.Count == 0)
			{
				failure = KingdomGuestRules.PlantRefusal(
					KingdomGuestRules.PlantVerdict.NothingToCarry);
				return false;
			}
			sources.Sort(delegate(GameObject left, GameObject right)
			{
				return string.Compare(left.ID, right.ID, StringComparison.Ordinal);
			});
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < sources.Count; i++)
			{
				if (string.IsNullOrEmpty(sources[i].ID) || !ids.Add(sources[i].ID))
				{
					failure = "Two cargo objects share an ambiguous identity; nothing was taken.";
					return false;
				}
			}
			return true;
		}

		private static bool EligibleSource(GameObject item, GameObject actor, GameObject sign,
			out string failure)
		{
			failure = null;
			if (!GameObject.Validate(item) || ReferenceEquals(item, actor) || ReferenceEquals(item, sign)
				|| item.IsCreature || item.IsPlayer())
				failure = "A creature cannot be cargo for a carry-sign.";
			else if (item.IsImportant())
				failure = "An important object in the designation must be removed first.";
			else if (item.Equipped != null)
				failure = "Equipped objects cannot be designated as cargo.";
			else if (!item.IsTakeable())
				failure = "An untakeable object in the designation must be removed first.";
			else if (!FounderOwned(item) || item.IsOwned())
				failure = "Every carried object must be unambiguously yours.";
			else if (string.IsNullOrEmpty(item.ID) || string.IsNullOrEmpty(item.Blueprint)
				|| item.Count <= 0 || item.Count > 4096)
				failure = "A cargo object's identity or whole-stack count cannot be proved.";
			return failure == null;
		}

		private static bool CargoShaped(GameObject item)
		{
			return GameObject.Validate(item) && (item.IsTakeable() || item.OwnedByPlayer
				|| item.GetIntProperty("DroppedByPlayer") > 0 || item.IsImportant()
				|| item.IsOwned() || item.Equipped != null);
		}

		private static bool FounderOwned(GameObject item)
		{
			return GameObject.Validate(item) && (item.OwnedByPlayer
				|| item.GetIntProperty("DroppedByPlayer") > 0);
		}

		private static bool ExactSign(GameObject actor, GameObject sign, Zone zone)
		{
			if (!GameObject.Validate(actor) || !actor.IsPlayer() || actor.Inventory == null
				|| !GameObject.Validate(sign) || sign.GetPart<r_KingdomCarrySign>() == null
				|| sign.InInventory != actor || sign.Equipped != null || sign.IsImportant()
				|| string.IsNullOrEmpty(sign.ID) || string.IsNullOrEmpty(sign.Blueprint)
				|| sign.Count <= 0 || sign.Count > 4096 || actor.CurrentZone != zone) return false;
			return ReferenceCount(actor.Inventory.Objects, sign) == 1;
		}

		private static int ReferenceCount(List<GameObject> objects, GameObject wanted)
		{
			int count = 0;
			for (int i = 0; objects != null && i < objects.Count; i++)
				if (ReferenceEquals(objects[i], wanted)) count++;
			return count;
		}

		private static bool SameDesignation(PlantPlan plan, GameObject container,
			List<GameObject> sources, KingdomLifecycleTopology topology, string ownerId,
			string holderId)
		{
			if (plan == null || !ReferenceEquals(plan.Container, container)
				|| plan.SourceTopology != topology
				|| !string.Equals(plan.SourceOwnerId, ownerId, StringComparison.Ordinal)
				|| !string.Equals(plan.SourceHolderObjectId, holderId, StringComparison.Ordinal)
				|| sources == null || sources.Count != plan.Sources.Count) return false;
			for (int i = 0; i < sources.Count; i++)
				if (!ReferenceEquals(sources[i], plan.Sources[i])) return false;
			return true;
		}

		private static string Describe(List<GameObject> sources)
		{
			StringBuilder text = new StringBuilder();
			for (int i = 0; sources != null && i < sources.Count; i++)
			{
				GameObject item = sources[i];
				string name = item == null ? null
					: KingdomPresentation.Rich(item.BaseDisplayNameStripped);
				if (string.IsNullOrEmpty(name)) name = item == null ? "object" : item.Blueprint;
				if (name.Length > 96) name = name.Substring(0, 96);
				string entry = item.Count + "\u00d7 " + name;
				if (text.Length + entry.Length + 2 > 3000) return null;
				if (text.Length > 0) text.Append(", ");
				text.Append(entry);
			}
			return text.ToString();
		}

		private static bool TryDistanceDays(string sourceZoneId, string destinationZoneId,
			out int days)
		{
			days = 0;
			string sourceWorld;
			string targetWorld;
			int sx, sy, sz, tx, ty, tz;
			if (!KingdomRules.TryParseZoneID(sourceZoneId, out sourceWorld, out sx, out sy, out sz)
				|| !KingdomRules.TryParseZoneID(destinationZoneId, out targetWorld,
					out tx, out ty, out tz)
				|| !string.Equals(sourceWorld, targetWorld, StringComparison.Ordinal)) return false;
			days = KingdomGuestRules.HaulDays(KingdomGuestRules.ZoneGridDistance(
				sx, sy, sz, tx, ty, tz));
			return days > 0;
		}

		private static bool ThreatPresent(KingdomSystem system, Zone zone)
		{
			if (system == null || zone == null || system.RaidState == 1) return true;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(zone);
			if (survey != null) return survey.Raiders.Count > 0;
			foreach (GameObject item in zone.GetObjects())
				if (GameObject.Validate(item) && item.GetIntProperty("KingdomRaider") == 1)
					return true;
			return false;
		}

		private static int LegacyMaterialUnits(KingdomCarryHaul haul)
		{
			if (haul == null) return 0;
			long total = (long)Math.Max(0, haul.Mud) + Math.Max(0, haul.Brush)
				+ Math.Max(0, haul.Timber) + Math.Max(0, haul.Stone)
				+ Math.Max(0, haul.Marble) + Math.Max(0, haul.Scrap);
			return total > int.MaxValue ? int.MaxValue : (int)total;
		}

		private static KingdomCarryBook Authority(KingdomSystem system)
		{
			if (system == null || system.CarryBook == null) return null;
			KingdomLifecycleRules.Normalize(system.CarryBook);
			return KingdomLifecycleRules.CanOwnAuthority(system.CarryBook)
				? system.CarryBook : null;
		}

		private sealed class CarryWorld : IKingdomLifecycleTrustedWorld
		{
			private readonly KingdomSystem System;
			private readonly Zone Zone;
			private readonly KingdomCarryBook Book;
			private readonly KingdomCarryOperation Operation;
			private readonly ScheduleReference Schedule = new ScheduleReference();
			private List<IKingdomLifecycleTrustedObservation> Cached;

			internal CarryWorld(KingdomSystem system, Zone zone, KingdomCarryBook book,
				KingdomCarryOperation operation)
			{
				System = system; Zone = zone; Book = book; Operation = operation;
				KingdomLifecycleResourceRevision row = ScheduleRow(book, operation);
				KingdomLifecycleResourceLease lease = operation == null
					? null : operation.ScheduleLease;
				bool applied = MatchesScheduleProjection(system == null ? null : system.Haul,
					operation);
				if (lease != null && applied && (lease.State == KingdomLifecycleLeaseState.Intent
					|| lease.State == KingdomLifecycleLeaseState.Proved))
				{
					Schedule.Value = lease.After;
					Schedule.Revision = lease.AfterRevision;
					Schedule.LastOperationId = operation.Id;
				}
				else
				{
					Schedule.Value = lease == null ? PriorScheduleValue(system == null
						? null : system.Haul) : lease.Before;
					Schedule.Revision = lease == null ? (row == null ? 0L : row.Revision)
						: lease.BeforeRevision;
					Schedule.LastOperationId = row == null ? null : row.LastOperationId;
				}
			}

			public int ObservationCount { get { Cached = Build(); return Cached.Count; } }

			public IKingdomLifecycleTrustedObservation Observe(int index) { return Cached[index]; }

			public object InvokeCarryOutput(KingdomLifecycleProjection output) { return null; }

			public object InvokeWater(object vesselReference, int amount) { return null; }

			public object InvokeSchedule(object scheduleReference, long dueTick,
				string operationId)
			{
				if (!ReferenceEquals(scheduleReference, Schedule) || Operation == null
					|| !string.Equals(Operation.Id, operationId, StringComparison.Ordinal)
					|| Operation.ScheduleLease == null
					|| dueTick != Operation.ScheduleLease.After
					|| LegacyMaterialUnits(System.Haul) > 0) return null;
				System.Haul = new KingdomCarryHaul
				{
					OriginZoneID = Operation.OriginZoneId, OriginX = Operation.OriginX,
					OriginY = Operation.OriginY,
					DestinationSettlementId = Operation.DestinationSettlementId,
					DestinationSettlementName = Operation.DestinationSettlementName,
					PlantedTick = Operation.CreatedTick, DueTick = dueTick
				};
				Schedule.Value = dueTick;
				Schedule.Revision = Operation.ScheduleLease.AfterRevision;
				Schedule.LastOperationId = operationId;
				return Schedule;
			}

			public object InvokeCarryRemoval(object sourceReference, int count,
				string eventId) { return null; }

			public object InvokeCarrySignRemoval(object signReference, int count,
				string receiptId)
			{
				GameObject sign = signReference as GameObject;
				GameObject owner = FindOwner(Operation.SignOwnerId, Operation.SignZoneId);
				if (!GameObject.Validate(sign) || owner == null || owner.Inventory == null
					|| count != 1 || !string.Equals(receiptId, Operation.SignReceiptId,
						StringComparison.Ordinal)
					|| sign.ID != Operation.SignObjectId || sign.Blueprint != Operation.SignBlueprint
					|| sign.InInventory != owner || sign.Equipped != null
					|| sign.Count != Operation.SignCount
					|| ReferenceCount(owner.Inventory.Objects, sign) != 1) return null;
				int before = sign.Count;
				Zone ownerZone = owner.CurrentZone;
				try { sign.Destroy(null, Silent: true); }
				finally { KingdomSurvey.ObserveCurrentTopologyInActive(ownerZone, owner); }
				if (GameObject.Validate(sign) ? sign.Count != before - 1 : before != 1) return null;
				return sign;
			}

			public object InvokeCarryMove(object sourceReference, int tripId,
				KingdomLifecycleTopology targetTopology, string targetOwnerId,
				string targetZoneId, int targetX, int targetY, string receiptId)
			{
				GameObject item = sourceReference as GameObject;
				KingdomCarrySource source = SourceFor(item, receiptId);
				if (source == null || !GameObject.Validate(item) || item.IsImportant()
					|| item.Equipped != null || !item.IsTakeable() || item.Count != source.PlannedCount
					|| source.CurrentTripId != tripId) return null;
				Zone beforeZone = item.CurrentZone;
				GameObject beforeOwner = item.InInventory;
				GameObject accepted = null;
				if (targetTopology == KingdomLifecycleTopology.Inventory)
				{
					GameObject owner = FindOwner(targetOwnerId, targetZoneId);
					if (!GameObject.Validate(owner) || !owner.IsAlive
						|| owner.GetIntProperty(KingdomResidents.JobIdProperty) != tripId
						|| owner.Inventory == null
						|| ReferenceCount(owner.Inventory.Objects, item) != 0) return null;
					try { accepted = owner.Inventory.AddObject(item, null, Silent: true, NoStack: true); }
					finally
					{
						KingdomSurvey.ObserveCurrentTopologyInActive(beforeZone, beforeOwner);
						KingdomSurvey.ObserveCurrentTopologyInActive(owner.CurrentZone, owner);
						KingdomSurvey.ObserveAddResultInActive(owner.CurrentZone, item, accepted);
						if (!ReferenceEquals(beforeZone, owner.CurrentZone))
							KingdomSurvey.ObserveAddResultInActive(beforeZone, item, accepted);
					}
					if (!ReferenceEquals(accepted, item) || item.InInventory != owner
						|| ReferenceCount(owner.Inventory.Objects, item) != 1) return null;
				}
				else if (targetTopology == KingdomLifecycleTopology.Cell)
				{
					if (Zone == null || !string.Equals(Zone.ZoneID, targetZoneId,
						StringComparison.Ordinal)) return null;
					Cell cell = Zone.GetCell(targetX, targetY);
					if (cell == null || ReferenceCount(cell.GetObjects(), item) != 0) return null;
					try { accepted = cell.AddObject(item, NoStack: true, Silent: true); }
					finally
					{
						KingdomSurvey.ObserveCurrentTopologyInActive(beforeZone, beforeOwner);
						KingdomSurvey.ObserveAddResultInActive(beforeZone, item, accepted);
						if (!ReferenceEquals(beforeZone, Zone))
							KingdomSurvey.ObserveAddResultInActive(Zone, item, accepted);
					}
					if (!ReferenceEquals(accepted, item) || item.InInventory != null
						|| !ReferenceEquals(item.CurrentCell, cell)
						|| ReferenceCount(cell.GetObjects(), item) != 1) return null;
				}
				else return null;
				return item;
			}

			public object InvokeLifecycleProjection(KingdomLifecycleProjection projection)
			{
				return null;
			}

			public object InvokeLifecycleRemoval(object objectReference, int count,
				string operationId) { return null; }

			private List<IKingdomLifecycleTrustedObservation> Build()
			{
				List<IKingdomLifecycleTrustedObservation> rows =
					new List<IKingdomLifecycleTrustedObservation>();
				if (Operation != null)
				{
					string scheduleKey = Operation.ScheduleLease == null
						? KingdomLifecycleRules.ResourceKey(KingdomLifecycleResourceKind.Schedule,
							Book.RealmId, Operation.DestinationSettlementId)
						: Operation.ScheduleLease.Key;
					rows.Add(new Observation(Schedule, scheduleKey, "Schedule",
						Operation.DestinationSettlementId, Operation.DestinationOwnerId,
						Operation.DestinationZoneId, Operation.DestinationTopology,
						Operation.DestinationX, Operation.DestinationY, 0,
						Schedule.Value, Schedule.Revision, Schedule.LastOperationId));
				}
				HashSet<GameObject> seen = new HashSet<GameObject>();
				AddAt(rows, seen, Operation.SignObjectId, Operation.SignTopology,
					Operation.SignOwnerId, Operation.SignZoneId, Operation.SignX, Operation.SignY);
				for (int i = 0; Operation.Sources != null && i < Operation.Sources.Count; i++)
				{
					KingdomCarrySource source = Operation.Sources[i];
					AddAt(rows, seen, source.ObjectId, source.CurrentTopology,
						source.CurrentOwnerId, source.CurrentZoneId, source.CurrentX, source.CurrentY);
					if (source.PendingTransfer != KingdomCarryTransferKind.None)
						AddAt(rows, seen, source.ObjectId, source.PendingTopology,
							source.PendingOwnerId, source.PendingZoneId,
							source.PendingX, source.PendingY);
				}
				return rows;
			}

			private void AddAt(List<IKingdomLifecycleTrustedObservation> rows,
				HashSet<GameObject> seen, string objectId, KingdomLifecycleTopology topology,
				string ownerId, string zoneId, int x, int y)
			{
				if (string.IsNullOrEmpty(objectId)) return;
				if (topology == KingdomLifecycleTopology.Inventory)
				{
					GameObject owner = FindOwner(ownerId, zoneId);
					for (int i = 0; owner != null && owner.Inventory != null
						&& i < owner.Inventory.Objects.Count; i++)
					{
						GameObject item = owner.Inventory.Objects[i];
						if (GameObject.Validate(item) && item.ID == objectId && seen.Add(item))
							rows.Add(ObjectObservation(item, topology, ownerId, zoneId, -1, -1));
					}
				}
				else if (topology == KingdomLifecycleTopology.Cell && Zone != null
					&& string.Equals(Zone.ZoneID, zoneId, StringComparison.Ordinal))
				{
					Cell cell = Zone.GetCell(x, y);
					List<GameObject> found = cell == null ? null : cell.GetObjects();
					for (int i = 0; found != null && i < found.Count; i++)
					{
						GameObject item = found[i];
						if (GameObject.Validate(item) && item.ID == objectId && seen.Add(item))
							rows.Add(ObjectObservation(item, topology, null, zoneId, x, y));
					}
				}
			}

			private Observation ObjectObservation(GameObject item,
				KingdomLifecycleTopology topology, string ownerId, string zoneId, int x, int y)
			{
				return new Observation(item, item.ID, item.Blueprint,
					Operation.DestinationSettlementId, ownerId, zoneId, topology,
					x, y, item.Count, 0L, 0L, null);
			}

			private GameObject FindOwner(string ownerId, string zoneId)
			{
				if (string.IsNullOrEmpty(ownerId)) return null;
				if (The.Player != null && The.Player.ID == ownerId
					&& The.Player.CurrentZone != null
					&& The.Player.CurrentZone.ZoneID == zoneId) return The.Player;
				GameObject found = GameObject.FindByID(ownerId);
				return GameObject.Validate(found) && found.CurrentZone != null
					&& found.CurrentZone.ZoneID == zoneId ? found : null;
			}

			private KingdomCarrySource SourceFor(GameObject item, string receiptId)
			{
				for (int i = 0; item != null && Operation.Sources != null
					&& i < Operation.Sources.Count; i++)
				{
					KingdomCarrySource source = Operation.Sources[i];
					if (source != null && source.ObjectId == item.ID
						&& (source.ReceiptId == receiptId
							|| i < Operation.Outputs.Count
								&& Operation.Outputs[i].ReceiptId == receiptId)) return source;
				}
				return null;
			}
		}

		private static KingdomLifecycleResourceRevision ScheduleRow(KingdomCarryBook book,
			KingdomCarryOperation op)
		{
			if (book == null || op == null || book.Resources == null) return null;
			string key = KingdomLifecycleRules.ResourceKey(KingdomLifecycleResourceKind.Schedule,
				book.RealmId, op.DestinationSettlementId);
			for (int i = 0; i < book.Resources.Count; i++)
				if (book.Resources[i] != null && book.Resources[i].Key == key) return book.Resources[i];
			return null;
		}

		private static long PriorScheduleValue(KingdomCarryHaul haul)
		{
			return haul != null && LegacyMaterialUnits(haul) == 0 && haul.DueTick >= 0L
				? haul.DueTick : 0L;
		}

		private static bool MatchesScheduleProjection(KingdomCarryHaul haul,
			KingdomCarryOperation op)
		{
			return haul != null && op != null && LegacyMaterialUnits(haul) == 0
				&& haul.OriginZoneID == op.OriginZoneId && haul.OriginX == op.OriginX
				&& haul.OriginY == op.OriginY
				&& haul.DestinationSettlementId == op.DestinationSettlementId
				&& haul.PlantedTick == op.CreatedTick && haul.DueTick == op.DueTick;
		}

		private sealed class ScheduleReference
		{
			internal long Value;
			internal long Revision;
			internal string LastOperationId;
		}

		private sealed class Observation : IKingdomLifecycleTrustedObservation
		{
			public object Reference { get; private set; }
			public string ObjectId { get; private set; }
			public string Marker { get { return null; } }
			public string Blueprint { get; private set; }
			public string SettlementId { get; private set; }
			public string OwnerId { get; private set; }
			public string ZoneId { get; private set; }
			public KingdomLifecycleTopology Topology { get; private set; }
			public int X { get; private set; }
			public int Y { get; private set; }
			public int Count { get; private set; }
			public int Capacity { get { return 0; } }
			public string Composition { get { return null; } }
			public long Value { get; private set; }
			public long Revision { get; private set; }
			public string LastOperationId { get; private set; }

			internal Observation(object reference, string objectId, string blueprint,
				string settlementId, string ownerId, string zoneId,
				KingdomLifecycleTopology topology, int x, int y, int count,
				long value, long revision, string lastOperationId)
			{
				Reference = reference; ObjectId = objectId; Blueprint = blueprint;
				SettlementId = settlementId; OwnerId = ownerId; ZoneId = zoneId;
				Topology = topology; X = x; Y = y; Count = count; Value = value;
				Revision = revision; LastOperationId = lastOperationId;
			}
		}
	}
}
