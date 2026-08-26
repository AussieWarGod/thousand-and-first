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
		private static bool ExactCarrySignShape(KingdomCarryOperation op, bool publication)
		{
			if (op == null || !ValidRootId(op.SignObjectId) || !ValidName(op.SignBlueprint)
				|| !TopologyValid(op.SignTopology, op.SignOwnerId, op.SignZoneId, op.SignX, op.SignY)
				|| op.SignCount <= 0 || op.SignCount > MaxPhysicalCount
				|| !string.Equals(op.SignReceiptId, ChildId(op.Id, "sign-receipt", 0),
					StringComparison.Ordinal)) return false;
			bool prepared = op.SignReceiptState == KingdomLifecyclePhysicalState.Prepared
				&& op.SignReceiptBeforeMatches == -1 && op.SignReceiptAfterMatches == -1
				&& op.SignReceiptBeforeCount == -1 && op.SignReceiptAfterCount == -1
				&& !op.SignReceiptSameReference
				&& string.IsNullOrEmpty(op.SignReceiptProofId);
			if (publication || prepared) return prepared;
			if (op.SignReceiptState == KingdomLifecyclePhysicalState.Intent)
				return op.SignReceiptBeforeMatches == 1 && op.SignReceiptAfterMatches == -1
					&& op.SignReceiptBeforeCount == op.SignCount
					&& op.SignReceiptAfterCount == -1 && !op.SignReceiptSameReference
					&& string.IsNullOrEmpty(op.SignReceiptProofId);
			return op.SignReceiptState == KingdomLifecyclePhysicalState.Proved
				&& op.SignReceiptBeforeMatches == 1
				&& op.SignReceiptAfterMatches == (op.SignCount == 1 ? 0 : 1)
				&& op.SignReceiptBeforeCount == op.SignCount
				&& op.SignReceiptAfterCount == op.SignCount - 1
				&& op.SignReceiptSameReference
				&& string.Equals(op.SignReceiptProofId, ExactCarrySignProof(op),
					StringComparison.Ordinal);
		}

		private static bool ExactCarryDeliveredTopology(KingdomCarryOperation op,
			KingdomCarrySource source, int ordinal)
		{
			if (op == null || source == null || op.Outputs == null
				|| ordinal < 0 || ordinal >= op.Outputs.Count) return false;
			KingdomLifecycleProjection output = op.Outputs[ordinal];
			bool target = output != null && source.CurrentTopology == output.Topology
				&& string.Equals(source.CurrentOwnerId, output.OwnerId, StringComparison.Ordinal)
				&& string.Equals(source.CurrentZoneId, output.ZoneId, StringComparison.Ordinal)
				&& source.CurrentX == output.X && source.CurrentY == output.Y;
			bool spill = source.CurrentTopology == KingdomLifecycleTopology.Cell
				&& source.CurrentOwnerId == null
				&& string.Equals(source.CurrentZoneId, op.SpillZoneId, StringComparison.Ordinal)
				&& source.CurrentX == op.SpillX && source.CurrentY == op.SpillY;
			return target || spill;
		}

		private static bool TripMember(KingdomCarryOperation op, int tripId)
		{
			if (op == null || op.TripIds == null || tripId <= 0) return false;
			return op.TripIds.BinarySearch(tripId) >= 0;
		}

		private static string ExactCarryPickupProof(KingdomCarryOperation op,
			KingdomCarrySource source, int ordinal)
		{
			return HashId("carry-exact-pickup", delegate(BinaryWriter w)
			{
				CanonicalString(w, op == null ? null : op.Id); w.Write(ordinal);
				CanonicalString(w, source == null ? null : source.ObjectId);
				CanonicalString(w, source == null ? null : source.ReceiptId);
				w.Write(source == null ? 0 : source.CurrentTripId);
				CanonicalString(w, source == null ? null : source.ReceiptChainId);
				w.Write(source == null ? 0 : source.ReceiptChainCount);
				w.Write(source == null ? -1 : source.ReceiptBeforeCount);
				w.Write(source == null ? -1 : source.ReceiptAfterCount);
				w.Write(source != null && source.ReceiptSameReference);
			});
		}

		private static string ExactCarryDestinationProof(KingdomCarryOperation op,
			KingdomCarrySource source, KingdomLifecycleProjection output, int ordinal, bool lost)
		{
			return HashId("carry-exact-destination", delegate(BinaryWriter w)
			{
				CanonicalString(w, op == null ? null : op.Id); w.Write(ordinal); w.Write(lost);
				CanonicalString(w, source == null ? null : source.ObjectId);
				w.Write(source == null ? (byte)0 : (byte)source.CurrentTopology);
				CanonicalString(w, source == null ? null : source.CurrentOwnerId);
				CanonicalString(w, source == null ? null : source.CurrentZoneId);
				w.Write(source == null ? -1 : source.CurrentX);
				w.Write(source == null ? -1 : source.CurrentY);
				CanonicalString(w, output == null ? null : output.ReceiptId);
				w.Write(output == null ? -1 : output.ReceiptBeforeCount);
				w.Write(output == null ? -1 : output.ReceiptAfterCount);
				w.Write(output != null && output.ReceiptSameReference);
			});
		}

		private static string ExactCarrySignProof(KingdomCarryOperation op)
		{
			return HashId("carry-exact-sign", delegate(BinaryWriter w)
			{
				CanonicalString(w, op == null ? null : op.Id);
				CanonicalString(w, op == null ? null : op.SignObjectId);
				CanonicalString(w, op == null ? null : op.SignReceiptId);
				w.Write(op == null ? -1 : op.SignReceiptBeforeCount);
				w.Write(op == null ? -1 : op.SignReceiptAfterCount);
				w.Write(op != null && op.SignReceiptSameReference);
			});
		}

		private static bool CarryOutboxShape(KingdomCarryOperation op, bool Publication)
		{
			KingdomLifecycleOutbox b = op.Outbox;
			return b != null && b.OperationId == op.Id
				&& b.EventId == ChildId(op.Id, "outbox", 0)
				&& b.ChronicleReceiptId == ChildId(op.Id, "chronicle", 0)
				&& !string.IsNullOrEmpty(b.Chronicle) && !string.IsNullOrEmpty(b.Ledger)
				&& !string.IsNullOrEmpty(b.Message)
				&& b.ChronicleDisposition == KingdomLifecycleSinkDisposition.Deliver
				&& b.LedgerDisposition == KingdomLifecycleSinkDisposition.Deliver
				&& b.MessageDisposition == KingdomLifecycleSinkDisposition.Deliver
				&& SinkTextShape(b.Chronicle, b.ChronicleDisposition,
					b.ChronicleState, Publication)
				&& SinkTextShape(b.Ledger, b.LedgerDisposition, b.LedgerState, Publication)
				&& SinkTextShape(b.Message, b.MessageDisposition, b.MessageState, Publication)
				&& SinkTextShape(b.Deed, b.DeedDisposition, b.DeedState, Publication)
				&& SinkTextShape(b.GuestbookLine, b.GuestbookDisposition,
					b.GuestbookState, Publication);
		}

		private static bool CarryOutboxTerminal(KingdomCarryOperation op)
		{
			if (!CarryOutboxShape(op, false)) return false;
			KingdomLifecycleOutbox b = op.Outbox;
			return b.ChronicleState == KingdomLifecycleSinkState.Delivered
				&& b.LedgerState == KingdomLifecycleSinkState.Delivered
				&& b.MessageState == KingdomLifecycleSinkState.Delivered
				&& SinkSettled(b.DeedState) && SinkSettled(b.GuestbookState);
		}

		private static bool CarryTerminalComponentsSettled(KingdomCarryOperation op)
		{
			if (op == null || !CarryPlanShape(op, false) || !CarryConserved(op)
				|| !(op.AuthorityKind == KingdomCarryAuthorityKind.ExactManifest
					? AllExactSourcesLoaded(op) : AllSourcesProved(op))
				|| !(op.AuthorityKind == KingdomCarryAuthorityKind.ExactManifest
					? ExactOutputsSettled(op) : OutputsSettledForRoad(op))
				|| CarryEscrow(op) != 0 || !CarryOutboxTerminal(op)) return false;
			for (int material = 0; material < 6; material++)
				if (MaterialValue(op, material, 0) != MaterialValue(op, material, 2)
					+ MaterialValue(op, material, 3)) return false;
			return true;
		}

		private static bool AllExactSourcesLoaded(KingdomCarryOperation op)
		{
			if (op == null || op.Sources == null || op.SourceIndex != op.Sources.Count) return false;
			for (int i = 0; i < op.Sources.Count; i++)
				if (op.Sources[i] == null
					|| op.Sources[i].LoadedCount != op.Sources[i].PlannedCount
					|| op.Sources[i].State != KingdomLifecyclePhysicalState.Proved) return false;
			return true;
		}

		private static bool ExactOutputsSettled(KingdomCarryOperation op)
		{
			if (op == null || op.Sources == null || op.Outputs == null
				|| op.Sources.Count != op.Outputs.Count || op.OutputIndex != op.Outputs.Count)
				return false;
			bool anyLost = false;
			for (int i = 0; i < op.Sources.Count; i++)
			{
				KingdomCarrySource source = op.Sources[i];
				KingdomLifecycleProjection output = op.Outputs[i];
				bool delivered = source != null && output != null
					&& source.DeliveredCount == source.PlannedCount && source.LostCount == 0
					&& output.State == KingdomLifecyclePhysicalState.Proved
					&& output.ReceiptState == KingdomLifecyclePhysicalState.Proved;
				bool lost = source != null && output != null
					&& source.LostCount == source.PlannedCount && source.DeliveredCount == 0
					&& output.State == KingdomLifecyclePhysicalState.Lost
					&& output.ReceiptState == KingdomLifecyclePhysicalState.Lost;
				if (!delivered && !lost) return false;
				anyLost |= lost;
			}
			return op.LostOnRoad == anyLost;
		}

		private static bool AllSourcesProved(KingdomCarryOperation op)
		{
			if (op == null || op.Sources == null || op.SourceIndex != op.Sources.Count) return false;
			for (int i = 0; i < op.Sources.Count; i++)
				if (op.Sources[i].State != KingdomLifecyclePhysicalState.Proved
					|| op.Sources[i].Removed != op.Sources[i].PlannedCount) return false;
			return true;
		}

		private static bool OutputsSettledForRoad(KingdomCarryOperation op)
		{
			if (op == null || op.Outputs == null || op.OutputIndex != op.Outputs.Count) return false;
			long[] proved = new long[6];
			for (int i = 0; i < op.Outputs.Count; i++)
			{
				KingdomLifecycleProjection p = op.Outputs[i];
				KingdomLifecyclePhysicalState expected = op.LostOnRoad
					? KingdomLifecyclePhysicalState.Skipped : KingdomLifecyclePhysicalState.Proved;
				if (p.State != expected || p.ReceiptState != expected
					|| !CarryOutputShape(p, op.Id, i, false)
					|| !CheckedAccumulate(proved, p.Material,
					op.LostOnRoad ? 0 : p.Count)) return false;
			}
			for (int material = 0; material < 6; material++)
				if (proved[material] != MaterialValue(op, material, 2)) return false;
			return true;
		}

		private static bool CarryPhaseAllowed(KingdomLifecyclePhase phase)
		{
			return phase == KingdomLifecyclePhase.Prepared
				|| phase == KingdomLifecyclePhase.RemovalIntent
				|| phase == KingdomLifecyclePhase.Removed
				|| phase == KingdomLifecyclePhase.ScheduleIntent
				|| phase == KingdomLifecyclePhase.ProjectionIntent
				|| phase == KingdomLifecyclePhase.Projected
				|| phase == KingdomLifecyclePhase.Sinks
				|| phase == KingdomLifecyclePhase.Terminal
				|| phase == KingdomLifecyclePhase.Quarantined;
		}

		private static bool CarryCountsValid(KingdomCarryOperation op)
		{
			for (int material = 0; material < 6; material++)
				for (int group = 0; group < 4; group++)
					if (!ValidCount(MaterialValue(op, material, group))) return false;
			return true;
		}

		private static bool AddMaterial(KingdomCarryOperation op, int material,
			int escrowDelta, int deliveredDelta, int lostDelta)
		{
			int escrow = MaterialValue(op, material, 1);
			int delivered = MaterialValue(op, material, 2);
			int lost = MaterialValue(op, material, 3);
			int e, d, l;
			if (!CheckedAdd(escrow, escrowDelta, out e) || !CheckedAdd(delivered, deliveredDelta, out d)
				|| !CheckedAdd(lost, lostDelta, out l) || !ValidCount(e) || !ValidCount(d)
				|| !ValidCount(l)) return false;
			SetMaterial(op, material, 1, e); SetMaterial(op, material, 2, d);
			SetMaterial(op, material, 3, l);
			return true;
		}

		private static int MaterialValue(KingdomCarryOperation op, int material, int group)
		{
			if (op == null || material < 0 || material >= 6 || group < 0 || group > 3) return -1;
			if (group == 0)
			{
				switch (material) { case 0: return op.Mud; case 1: return op.Brush;
				case 2: return op.Timber; case 3: return op.Stone; case 4: return op.Marble;
				default: return op.Scrap; }
			}
			if (group == 1)
			{
				switch (material) { case 0: return op.EscrowMud; case 1: return op.EscrowBrush;
				case 2: return op.EscrowTimber; case 3: return op.EscrowStone;
				case 4: return op.EscrowMarble; default: return op.EscrowScrap; }
			}
			if (group == 2)
			{
				switch (material) { case 0: return op.DeliveredMud; case 1: return op.DeliveredBrush;
				case 2: return op.DeliveredTimber; case 3: return op.DeliveredStone;
				case 4: return op.DeliveredMarble; default: return op.DeliveredScrap; }
			}
			switch (material) { case 0: return op.LostMud; case 1: return op.LostBrush;
			case 2: return op.LostTimber; case 3: return op.LostStone;
			case 4: return op.LostMarble; default: return op.LostScrap; }
		}

	}
}
