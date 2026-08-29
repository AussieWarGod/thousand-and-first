using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocationRules
	{
		public static bool Valid(KingdomRelocationReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (Receipt == null || Receipt.Schema != Schema
				|| !Id(Receipt.PlanId) || !Key(Receipt.ZoneId) || !Key(Receipt.RealmId)
				|| !Id(Receipt.HeartId) || !Key(Receipt.SuccessorKey)
				|| !Rect(Receipt.HeartGround) || Receipt.CreatedTick < 0L
				|| Receipt.Generation < 1 || Receipt.Moves == null
				|| Receipt.Moves.Count < 1 || Receipt.Moves.Count > MaxMoves
				|| Receipt.CurrentMove < 0 || Receipt.CurrentMove > Receipt.Moves.Count
				|| (Receipt.Phase != KingdomRelocationPhase.Active
					&& Receipt.Phase != KingdomRelocationPhase.Complete
					&& Receipt.Phase != KingdomRelocationPhase.Quarantined)
				|| !Optional(Receipt.Failure, MaxFailureChars))
				return Fail("relocation header is malformed", out Failure);
			HashSet<string> ids = new HashSet<string>(System.StringComparer.Ordinal)
				{ Receipt.HeartId };
			HashSet<string> lots = new HashSet<string>(System.StringComparer.Ordinal);
			for (int i = 0; i < Receipt.Moves.Count; i++)
			{
				if (!ValidMove(Receipt.Moves[i], i, ids, lots, out Failure)) return false;
				if (Overlaps(Receipt.Moves[i].Source, Receipt.Moves[i].Destination))
					return Fail("relocation source overlaps its receiving frame", out Failure);
				if (Overlaps(Receipt.Moves[i].Destination, Receipt.HeartGround))
					return Fail("relocation destination occupies heart ground", out Failure);
				for (int j = 0; j < i; j++)
				{
					if (Overlaps(Receipt.Moves[i].Source, Receipt.Moves[j].Source))
						return Fail("relocation sources overlap", out Failure);
					if (Overlaps(Receipt.Moves[i].Destination, Receipt.Moves[j].Destination))
						return Fail("relocation destinations overlap", out Failure);
				}
				for (int j = i + 1; j < Receipt.Moves.Count; j++)
					if (Receipt.Moves[j] != null && Overlaps(Receipt.Moves[i].Destination,
						Receipt.Moves[j].Source))
						return Fail("relocation destination obstructs a later source", out Failure);
			}
			for (int i = 0; i < Receipt.CurrentMove; i++)
				if (Receipt.Moves[i].Phase != KingdomRelocationMovePhase.Complete)
					return Fail("completed relocation prefix is discontinuous", out Failure);
			if (Receipt.CurrentMove < Receipt.Moves.Count
				&& Receipt.Moves[Receipt.CurrentMove].Phase == KingdomRelocationMovePhase.Complete)
				return Fail("current relocation index trails a completed move", out Failure);
			if (Receipt.Phase == KingdomRelocationPhase.Complete
				&& Receipt.CurrentMove != Receipt.Moves.Count)
				return Fail("completed ring call retains unfinished moves", out Failure);
			if (Receipt.Phase == KingdomRelocationPhase.Active
				&& Receipt.CurrentMove == Receipt.Moves.Count)
				return Fail("active ring call has no current move", out Failure);
			return true;
		}

		private static bool ValidMove(KingdomRelocationMove Move, int Index,
			HashSet<string> Ids, HashSet<string> Lots, out string Failure)
		{
			Failure = null;
			if (Move == null || !Id(Move.RootId) || !Key(Move.PlotId)
				|| !Lots.Add(Move.PlotId) || !Key(Move.BuildKey)
				|| !Optional(Move.DisplayName, MaxNameChars) || !Rect(Move.Source)
				|| !Rect(Move.Destination) || Move.Source.Width != Move.Destination.Width
				|| Move.Source.Height != Move.Destination.Height || !Rect(Move.Footprint)
				|| !Move.Source.Contains(Move.Footprint.X1, Move.Footprint.Y1)
				|| !Move.Source.Contains(Move.Footprint.X2, Move.Footprint.Y2)
				|| Move.Roof < 0 || Move.Roof > 3 || Move.StartedTick < 0L
				|| Move.LastTick < Move.StartedTick || Move.RequiredTicks < 1L
				|| Move.RemainingTicks < 0L || Move.RemainingTicks > Move.RequiredTicks
				|| Move.CompletionTick < 0L || !Id(Move.FrameId)
				|| Move.Phase < KingdomRelocationMovePhase.Waiting
				|| Move.Phase > KingdomRelocationMovePhase.RolledBack
				|| SameRect(Move.Source, Move.Destination)
				|| !Ids.Add(Move.FrameId) || Move.StakeIds == null
				|| Move.StakeIds.Length != MaxStakeIds || Move.Rows == null
				|| Move.Rows.Count < 1 || Move.Rows.Count > MaxRowsPerMove
				|| Move.Clearance == null || Move.Clearance.Count > MaxClearRowsPerMove)
				return Fail("relocation move " + Index + " is malformed", out Failure);
			for (int i = 0; i < Move.StakeIds.Length; i++)
				if (!Id(Move.StakeIds[i]) || !Ids.Add(Move.StakeIds[i]))
					return Fail("relocation frame identity is duplicated", out Failure);
			int roots = 0;
			for (int i = 0; i < Move.Rows.Count; i++)
			{
				KingdomRelocationRow row = Move.Rows[i];
				if (row == null || !Id(row.ObjectId) || !Key(row.Blueprint)
					|| !Ids.Add(row.ObjectId) || row.OffsetX < 0 || row.OffsetY < 0
					|| row.OffsetX >= Move.Source.Width || row.OffsetY >= Move.Source.Height
					|| row.State < KingdomRelocationRowState.Source
					|| row.State > KingdomRelocationRowState.Destination)
					return Fail("relocation object row is malformed or duplicated", out Failure);
				if (row.Root) { roots++; if (row.ObjectId != Move.RootId) return Fail(
					"relocation root row disagrees with move root", out Failure); }
			}
			if (roots != 1) return Fail("relocation move needs exactly one root", out Failure);
			for (int i = 0; i < Move.Clearance.Count; i++)
			{
				KingdomRelocationClearRow row = Move.Clearance[i];
				if (row == null || !Id(row.ObjectId) || !Key(row.Blueprint)
					|| !Ids.Add(row.ObjectId) || !Move.Destination.Contains(row.X, row.Y)
					|| row.State < KingdomRelocationClearState.Standing
					|| row.State > KingdomRelocationClearState.Removed)
					return Fail("relocation clearance row is malformed or duplicated", out Failure);
			}
			if (!PhaseRows(Move, out Failure)) return false;
			return ValidArchitecture(Move.Architecture, Move, out Failure);
		}

		private static bool PhaseRows(KingdomRelocationMove Move, out string Failure)
		{
			Failure = null;
			bool source = true, destination = true, standing = true, removed = true;
			for (int i = 0; i < Move.Rows.Count; i++)
			{
				source &= Move.Rows[i].State == KingdomRelocationRowState.Source;
				destination &= Move.Rows[i].State == KingdomRelocationRowState.Destination;
			}
			for (int i = 0; i < Move.Clearance.Count; i++)
			{
				standing &= Move.Clearance[i].State == KingdomRelocationClearState.Standing;
				removed &= Move.Clearance[i].State == KingdomRelocationClearState.Removed;
			}
			if ((Move.Phase == KingdomRelocationMovePhase.Waiting
					|| Move.Phase == KingdomRelocationMovePhase.Working)
				&& (!source || !standing || Move.RemainingTicks < 1L
					|| Move.CompletionTick != 0L))
				return Fail("working relocation has crossed handover state", out Failure);
			if (Move.Phase == KingdomRelocationMovePhase.Waiting
				&& Move.RemainingTicks != Move.RequiredTicks)
				return Fail("waiting relocation has already spent labour", out Failure);
			if ((Move.Phase == KingdomRelocationMovePhase.Handover
					|| Move.Phase == KingdomRelocationMovePhase.Complete)
				&& (Move.RemainingTicks != 0L || Move.CompletionTick < Move.StartedTick))
				return Fail("handover relocation lacks completed labour", out Failure);
			if (Move.Phase == KingdomRelocationMovePhase.Complete
				&& (!destination || !removed))
				return Fail("completed relocation is not physically complete", out Failure);
			if (Move.Phase == KingdomRelocationMovePhase.RolledBack
				&& (!source || !standing))
				return Fail("rolled-back relocation is not physically restored", out Failure);
			return true;
		}

		private static bool ValidArchitecture(KingdomRelocationArchitecture A,
			KingdomRelocationMove Move, out string Failure)
		{
			Failure = null;
			if (A == null) return true;
			if (A.Schema < 1 || !Key(A.BuildKey) || !Key(A.PlanKey) || !Key(A.BindingKey)
				|| !Key(A.TierKey) || !Key(A.VariantKey) || !Key(A.PaletteKey)
				|| !Key(A.LotType) || A.LotSize < 0 || A.LotSize > 16
				|| A.Facing < 0 || A.Facing > 3 || !Optional(A.Snapshot, MaxSnapshotChars)
				|| string.IsNullOrEmpty(A.Snapshot) || !Hash(A.Hash)
				|| !Move.Source.Contains(A.MainX, A.MainY))
				return Fail("relocation architecture authority is malformed", out Failure);
			return true;
		}

		private static bool Rect(KingdomRelocationRect R)
		{
			return R.X1 >= 0 && R.Y1 >= 0 && R.X2 >= R.X1 && R.Y2 >= R.Y1
				&& R.X2 <= MaxCoordinate && R.Y2 <= MaxCoordinate
				&& (long)R.Width * R.Height <= 16384L;
		}

		private static bool Id(string Value) { return Text(Value, MaxIdChars); }
		private static bool Key(string Value) { return Text(Value, MaxKeyChars); }
		private static bool Optional(string Value, int Maximum)
		{
			return Value == null || (Value.Length <= Maximum && !Controls(Value));
		}
		private static bool Text(string Value, int Maximum)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= Maximum && !Controls(Value);
		}
		private static bool Controls(string Value)
		{
			for (int i = 0; Value != null && i < Value.Length; i++)
				if (char.IsControl(Value[i])) return true;
			return false;
		}
		private static bool Hash(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9')
					|| (Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}
		private static bool Fail(string Text, out string Failure)
		{
			Failure = Text; return false;
		}
	}
}
