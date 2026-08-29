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
	internal static partial class KingdomCarryRuntime
	{

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
	}
}
