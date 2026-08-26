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
		internal static partial class TrustedAdapter
		{
			internal static bool ProveCarryOutput(KingdomCarryBook book,
				KingdomCarryOperation operation, KingdomLifecycleProjection output,
				IKingdomLifecycleTrustedWorld world)
			{
				int idBefore;
				int markerBefore;
				ScanOutput(world, output, out idBefore, out markerBefore);
				if (idBefore != 0 || markerBefore != 0
					|| !BeginCarryOutputCore(book, operation, output)) return false;
				object returned;
				try { returned = world.InvokeCarryOutput(output); }
				catch (Exception) { return false; }
				if (returned == null) return false;
				int idAfter;
				int markerAfter;
				Snapshot after = ScanOutput(world, output,
					out idAfter, out markerAfter);
				CallbackReceipt receipt = CallbackReceipt.Create(null, after, returned);
				if (idAfter != 1 || markerAfter != 1 || receipt.After == null
					|| !ReferenceEquals(receipt.After.Reference, receipt.Returned)
					|| !string.Equals(receipt.After.Marker, output.Marker, StringComparison.Ordinal)
					|| !string.Equals(receipt.After.Blueprint, output.Blueprint, StringComparison.Ordinal)
					|| receipt.After.Count != output.Count || !ExactTopology(receipt.After,
						output.Topology, output.OwnerId, output.ZoneId, output.X, output.Y)) return false;
				if (!ConfirmCarryOutputCore(book, operation, output)) return false;
				output.ReceiptAfterIdMatches = 1;
				output.ReceiptAfterMarkerMatches = 1;
				output.ReceiptAfterCount = receipt.After.Count;
				output.ReceiptSameReference = true;
				output.ReceiptProofId = CarryOutputReceiptProof(operation, output, false);
				output.ReceiptState = KingdomLifecyclePhysicalState.Proved;
				output.LiveAuthority = returned;
				return CarryOutputShape(output, operation.Id,
					IndexOfOutput(operation, output), false);
			}

			internal static bool ProveCarryRoadAbsence(KingdomCarryBook book,
				KingdomCarryOperation operation, KingdomLifecycleProjection output,
				IKingdomLifecycleTrustedWorld world)
			{
				int ids;
				int markers;
				ScanOutput(world, output, out ids, out markers);
				return ids == 0 && markers == 0
					&& SkipCarryOutputOnRoadCore(book, operation, output);
			}

			internal static bool ProveWater(KingdomLifecycleBook book,
				KingdomLifecycleResourceLease lease, KingdomLifecycleWaterLeg leg,
				IKingdomLifecycleTrustedWorld world)
			{
				int beforeMatches;
				Snapshot before = ExactWaterObservation(world, leg,
					leg.Before, out beforeMatches);
				if (beforeMatches != 1 || before == null || before.Reference == null
					|| !ExactWaterFields(before, leg, leg.Before)
					|| !BeginWaterLeaseCore(book, lease, leg, before.Value)) return false;
				leg.ReceiptBeforeMatches = 1;
				leg.LiveAuthority = before.Reference;
				object returned;
				try { returned = world.InvokeWater(before.Reference, leg.Delta); }
				catch (Exception) { return false; }
				if (returned == null) return false;
				int afterMatches;
				Snapshot after = ExactWaterObservation(world, leg,
					leg.After, out afterMatches);
				CallbackReceipt receipt = CallbackReceipt.Create(before, after, returned);
				if (afterMatches != 1 || receipt.After == null
					|| !ExactWaterFields(receipt.After, leg, leg.After)
					|| !ReferenceEquals(receipt.Before.Reference, receipt.Returned)
					|| !ReferenceEquals(receipt.After.Reference, receipt.Returned)) return false;
				KingdomLifecycleOperation operation = FindOpenOperation(book, lease.OperationId);
				if (!ConfirmWaterLeaseCore(book, lease, leg, after.Value)) return false;
				leg.ReceiptAfterMatches = 1;
				leg.ReceiptSameReference = true;
				leg.ReceiptProofId = WaterReceiptProof(operation, lease, leg);
				leg.ReceiptState = KingdomLifecyclePhysicalState.Proved;
				return ExactWaterReceipt(operation, lease, leg);
			}

			private static Snapshot ExactScheduleObservation(
				IKingdomLifecycleTrustedWorld world, KingdomCarryOperation operation,
				long value, long revision, string lastOperationId, out int matches)
			{
				return ExactObservation(world, delegate(Snapshot x)
				{
					return string.Equals(x.ObjectId, operation.ScheduleLease.Key, StringComparison.Ordinal);
				}, out matches);
			}

			private static Snapshot ExactWaterObservation(
				IKingdomLifecycleTrustedWorld world, KingdomLifecycleWaterLeg leg,
				long value, out int matches)
			{
				return ExactObservation(world, delegate(Snapshot x)
				{
					return string.Equals(x.ObjectId, leg.OwnerId, StringComparison.Ordinal);
				}, out matches);
			}

			private static Snapshot ScanOutput(
				IKingdomLifecycleTrustedWorld world, KingdomLifecycleProjection output,
				out int idMatches, out int markerMatches)
			{
				idMatches = 0;
				markerMatches = 0;
				Snapshot exact = null;
				List<Snapshot> observations;
				if (!TrySnapshots(world, out observations))
				{
					idMatches = -1; markerMatches = -1; return null;
				}
				for (int i = 0; i < observations.Count; i++)
				{
					Snapshot x = observations[i];
					if (string.Equals(x.ObjectId, output.ObjectId, StringComparison.Ordinal))
					{
						idMatches++;
						exact = x;
					}
					if (string.Equals(x.Marker, output.Marker, StringComparison.Ordinal)) markerMatches++;
				}
				return exact;
			}

			private static Snapshot ExactObservation(
				IKingdomLifecycleTrustedWorld world,
				Predicate<Snapshot> predicate, out int matches)
			{
				matches = 0;
				Snapshot exact = null;
				List<Snapshot> observations;
				if (predicate == null || !TrySnapshots(world, out observations)) return null;
				for (int i = 0; i < observations.Count; i++)
				{
					Snapshot x = observations[i];
					if (!predicate(x)) continue;
					matches++;
					exact = x;
				}
				return exact;
			}

			private static bool TrySnapshots(IKingdomLifecycleTrustedWorld world,
				out List<Snapshot> snapshots)
			{
				snapshots = null;
				if (world == null) return false;
				try
				{
					int count = world.ObservationCount;
					if (count < 0 || count > MaxPhysicalCount) return false;
					List<Snapshot> captured = new List<Snapshot>(count);
					for (int i = 0; i < count; i++)
					{
						Snapshot value = Snapshot.Capture(world.Observe(i));
						if (!ObservationShape(value)) return false;
						captured.Add(value);
					}
					snapshots = captured;
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			private static bool ObservationShape(Snapshot value)
			{
				return value != null && value.Reference != null
					&& !TooLong(value.ObjectId, MaxIdChars)
					&& !TooLong(value.Marker, MaxIdChars)
					&& !TooLong(value.Blueprint, MaxNameChars)
					&& !TooLong(value.SettlementId, MaxIdChars)
					&& !TooLong(value.OwnerId, MaxIdChars)
					&& !TooLong(value.ZoneId, MaxNameChars)
					&& !TooLong(value.Composition, MaxTextChars)
					&& Enum.IsDefined(typeof(KingdomLifecycleTopology), value.Topology)
					&& value.X >= -1 && value.X <= MaxCoordinate
					&& value.Y >= -1 && value.Y <= MaxCoordinate
					&& value.Count >= 0 && value.Count <= MaxPhysicalCount
					&& value.Capacity >= 0 && value.Capacity <= MaxPhysicalCount
					&& value.Revision >= 0L;
			}

			private static bool ExactTopology(Snapshot x,
				KingdomLifecycleTopology topology, string ownerId, string zoneId, int px, int py)
			{
				return x != null && x.Topology == topology
					&& string.Equals(x.OwnerId, ownerId, StringComparison.Ordinal)
					&& string.Equals(x.ZoneId, zoneId, StringComparison.Ordinal)
					&& x.X == px && x.Y == py;
			}

			private static bool ExactScheduleFields(Snapshot x,
				KingdomCarryOperation operation, long value, long revision, string lastOperationId)
			{
				return x != null && string.Equals(x.SettlementId,
					operation.DestinationSettlementId, StringComparison.Ordinal)
					&& string.Equals(x.Blueprint, ScheduleBlueprint, StringComparison.Ordinal)
					&& x.Value == value && x.Revision == revision
					&& string.Equals(x.LastOperationId, lastOperationId, StringComparison.Ordinal)
					&& ExactTopology(x, operation.DestinationTopology,
						operation.DestinationOwnerId, operation.DestinationZoneId,
						operation.DestinationX, operation.DestinationY);
			}

			private static bool ExactLifecycleScheduleFields(Snapshot x,
				KingdomLifecycleOperation operation, long value, long revision,
				string lastOperationId)
			{
				return x != null
					&& string.Equals(x.Blueprint, ScheduleBlueprint, StringComparison.Ordinal)
					&& string.Equals(x.SettlementId, operation.SettlementId,
						StringComparison.Ordinal)
					&& string.Equals(x.ZoneId, operation.ZoneId, StringComparison.Ordinal)
					&& x.Value == value && x.Revision == revision
					&& string.Equals(x.LastOperationId, lastOperationId, StringComparison.Ordinal)
					&& TopologyValid(x.Topology, x.OwnerId, x.ZoneId, x.X, x.Y);
			}

			private static bool ExactCarrySourceFields(Snapshot x,
				KingdomCarrySource source, int count)
			{
				return x != null && source != null
					&& string.Equals(x.ObjectId, source.ObjectId, StringComparison.Ordinal)
					&& string.Equals(x.Blueprint, source.Blueprint, StringComparison.Ordinal)
					&& x.Count == count && ExactTopology(x, source.Topology, source.OwnerId,
						source.ZoneId, source.X, source.Y);
			}

			private static bool ExactSignBefore(Snapshot x, KingdomCarryOperation operation)
			{
				return x != null && operation != null
					&& string.Equals(x.ObjectId, operation.SignObjectId, StringComparison.Ordinal)
					&& string.Equals(x.Blueprint, operation.SignBlueprint, StringComparison.Ordinal)
					&& x.Count == operation.SignCount && ExactTopology(x,
						operation.SignTopology, operation.SignOwnerId, operation.SignZoneId,
						operation.SignX, operation.SignY);
			}

			private static bool ExactSignAfter(Snapshot x, int matches,
				KingdomCarryOperation operation)
			{
				if (operation == null || operation.SignCount <= 0) return false;
				int after = operation.SignCount - 1;
				return after == 0 ? matches == 0 && x == null
					: matches == 1 && x != null
						&& string.Equals(x.ObjectId, operation.SignObjectId,
							StringComparison.Ordinal)
						&& string.Equals(x.Blueprint, operation.SignBlueprint,
							StringComparison.Ordinal)
						&& x.Count == after && ExactTopology(x, operation.SignTopology,
							operation.SignOwnerId, operation.SignZoneId,
							operation.SignX, operation.SignY);
			}

			private static bool ExactCurrentSource(Snapshot x, KingdomCarrySource source)
			{
				return source != null && ExactSourceAt(x, source, source.CurrentTopology,
					source.CurrentOwnerId, source.CurrentZoneId, source.CurrentX, source.CurrentY);
			}

		}
	}
}
