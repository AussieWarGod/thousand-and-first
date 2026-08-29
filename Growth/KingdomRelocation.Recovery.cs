using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		private static bool ReconcileCurrent(Zone Zone, ref string Expected,
			KingdomRelocationReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (Receipt.CurrentMove < 0 || Receipt.CurrentMove >= Receipt.Moves.Count)
				return Quarantine(Zone, Expected, Receipt,
					"The current relocation move index is outside its receipt.");
			KingdomRelocationMove move = Receipt.Moves[Receipt.CurrentMove];
			for (int i = 0; i < move.Rows.Count; i++)
			{
				KingdomRelocationRow row = move.Rows[i];
				KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(Zone,
					row.ObjectId, out GameObject exact);
				GameObject escrow = Escrow(Receipt, row.ObjectId, false);
				if (state == KingdomPhysicalLookupState.Ambiguous
					|| (escrow != null && exact != null && !ReferenceEquals(escrow, exact)))
					return QuarantineFailure(Zone, Expected, Receipt,
						"A relocating lot object identity is duplicated.", out Failure);
				bool source = ExactAt(exact, Zone, row.Blueprint,
					move.Source.X1 + row.OffsetX, move.Source.Y1 + row.OffsetY);
				bool destination = ExactAt(exact, Zone, row.Blueprint,
					move.Destination.X1 + row.OffsetX, move.Destination.Y1 + row.OffsetY);
				if (state == KingdomPhysicalLookupState.Absent && escrow != null
					&& escrow.CurrentCell != null)
					return QuarantineFailure(Zone, Expected, Receipt,
						"A relocating lot escrow points to foreign ground.", out Failure);
				if ((move.Phase == KingdomRelocationMovePhase.Waiting
						|| move.Phase == KingdomRelocationMovePhase.Working)
					&& (!source || destination || escrow != null))
					return QuarantineFailure(Zone, Expected, Receipt,
						"A lot object crossed before its receiving frame was complete.", out Failure);
				if (row.State == KingdomRelocationRowState.Source)
				{
					if (destination) row.State = KingdomRelocationRowState.Destination;
					else if (escrow != null) row.State = KingdomRelocationRowState.Rooted;
					else if (!source) return QuarantineFailure(Zone, Expected, Receipt,
						"A source lot object is absent or displaced.", out Failure);
				}
				else if (row.State == KingdomRelocationRowState.Rooted)
				{
					if (destination) row.State = KingdomRelocationRowState.Destination;
					else if (!source && escrow == null) return QuarantineFailure(Zone,
						Expected, Receipt, "A rooted lot object is absent.", out Failure);
				}
				else if (!destination) return QuarantineFailure(Zone, Expected, Receipt,
					"A handed-over lot object is absent or displaced.", out Failure);
				if (row.State == KingdomRelocationRowState.Destination && escrow != null)
					ClearEscrow(Receipt, row.ObjectId, false, escrow);
			}
			for (int i = 0; i < move.Clearance.Count; i++)
			{
				KingdomRelocationClearRow row = move.Clearance[i];
				KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(Zone,
					row.ObjectId, out GameObject exact);
				GameObject escrow = Escrow(Receipt, row.ObjectId, true);
				if (state == KingdomPhysicalLookupState.Ambiguous
					|| (escrow != null && exact != null && !ReferenceEquals(escrow, exact)))
					return QuarantineFailure(Zone, Expected, Receipt,
						"A destination-clearance identity is duplicated.", out Failure);
				bool standing = ExactAt(exact, Zone, row.Blueprint, row.X, row.Y);
				if (state == KingdomPhysicalLookupState.Absent && escrow != null
					&& escrow.CurrentCell != null)
					return QuarantineFailure(Zone, Expected, Receipt,
						"Destination-clearance escrow points to foreign ground.", out Failure);
				if ((move.Phase == KingdomRelocationMovePhase.Waiting
						|| move.Phase == KingdomRelocationMovePhase.Working)
					&& (!standing || escrow != null))
					return QuarantineFailure(Zone, Expected, Receipt,
						"Destination ground changed before handover authority.", out Failure);
				if (row.State == KingdomRelocationClearState.Standing)
				{
					if (escrow != null && !standing)
						row.State = KingdomRelocationClearState.Removed;
					else if (escrow != null)
						row.State = KingdomRelocationClearState.RemovalPending;
					else if (!standing) return QuarantineFailure(Zone, Expected, Receipt,
						"Frozen natural destination ground is absent or displaced.", out Failure);
				}
				else if (row.State == KingdomRelocationClearState.RemovalPending)
				{
					if (escrow != null && !standing) row.State = KingdomRelocationClearState.Removed;
					else if (!standing) return QuarantineFailure(Zone, Expected, Receipt,
						"Pending destination clearance lost its exact object.", out Failure);
				}
				else if (escrow == null || standing) return QuarantineFailure(Zone,
					Expected, Receipt, "Removed destination ground lost escrow authority.",
					out Failure);
			}
			string encoded;
			if (!KingdomRelocationCodec.TryEncode(Receipt, out encoded, out Failure)) return false;
			if (encoded == Expected) return true;
			return TryPublish(Zone, Expected, Receipt, out Expected, out Failure);
		}

		private static bool QuarantineFailure(Zone Zone, string Expected,
			KingdomRelocationReceipt Receipt, string Text, out string Failure)
		{
			Failure = Text; Quarantine(Zone, Expected, Receipt, Text); return false;
		}
	}
}
