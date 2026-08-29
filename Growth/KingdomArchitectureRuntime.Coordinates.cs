using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRuntime
	{
		public static bool TryWorldCell(ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, ArchitectureCellState Cell,
			out int WorldX, out int WorldY, out string Failure)
		{
			WorldX = 0;
			WorldY = 0;
			if (Cell == null || !ContainsCell(Snapshot, Cell))
				return Fail("cell is not an exact member of the snapshot", out Failure);
			return TryWorldCoordinate(Snapshot, Rect, Cell.X, Cell.Y,
				out WorldX, out WorldY, out Failure);
		}

		public static bool TryWorldPlacement(ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, ArchitecturePlacement Placement,
			out int WorldX, out int WorldY, out string Failure)
		{
			WorldX = 0;
			WorldY = 0;
			if (Placement == null || !ContainsPlacement(Snapshot, Placement))
				return Fail("placement is not an exact member of the snapshot", out Failure);
			return TryWorldCoordinate(Snapshot, Rect, Placement.X, Placement.Y,
				out WorldX, out WorldY, out Failure);
		}

		public static bool TryWorldAnchor(ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, ArchitectureAnchor Anchor,
			out int WorldX, out int WorldY, out string Failure)
		{
			WorldX = 0;
			WorldY = 0;
			if (Anchor == null || !ContainsAnchor(Snapshot, Anchor))
				return Fail("anchor is not an exact member of the snapshot", out Failure);
			return TryWorldCoordinate(Snapshot, Rect, Anchor.X, Anchor.Y,
				out WorldX, out WorldY, out Failure);
		}

		private static bool TryWorldCoordinate(ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, int X, int Y,
			out int WorldX, out int WorldY, out string Failure)
		{
			WorldX = 0;
			WorldY = 0;
			if (Snapshot == null || !ValidRect(Rect))
				return Fail("snapshot or exact world rectangle is malformed", out Failure);
			int worldWidth;
			int worldHeight;
			if (!KingdomArchitectureRules.TryWorldDimensions(Snapshot.Width, Snapshot.Height,
				Snapshot.Facing, out worldWidth, out worldHeight)
				|| Rect.Width != worldWidth || Rect.Height != worldHeight)
				return Fail("world rectangle does not exactly fit the snapshot pose", out Failure);
			if (!KingdomArchitectureRules.TryToWorld(Rect.X1, Rect.Y1, Snapshot.Width,
				Snapshot.Height, Snapshot.Facing, X, Y, out WorldX, out WorldY)
				|| !Rect.Contains(WorldX, WorldY))
				return Fail("snapshot coordinate does not transform inside its exact rectangle", out Failure);
			Failure = null;
			return true;
		}

		private static bool ContainsCell(ArchitectureLayoutSnapshot Snapshot,
			ArchitectureCellState Cell)
		{
			if (Snapshot == null || Snapshot.Cells == null) return false;
			for (int i = 0; i < Snapshot.Cells.Count; i++)
			{
				ArchitectureCellState candidate = Snapshot.Cells[i];
				if (candidate != null && candidate.X == Cell.X && candidate.Y == Cell.Y
					&& candidate.Claim == Cell.Claim
					&& candidate.Passability == Cell.Passability && candidate.Cover == Cell.Cover)
					return true;
			}
			return false;
		}

		private static bool ContainsPlacement(ArchitectureLayoutSnapshot Snapshot,
			ArchitecturePlacement Placement)
		{
			if (Snapshot == null || Snapshot.Placements == null) return false;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement candidate = Snapshot.Placements[i];
				if (candidate != null && candidate.Layer == Placement.Layer
					&& candidate.X == Placement.X && candidate.Y == Placement.Y
					&& candidate.Blueprint == Placement.Blueprint && candidate.Slot == Placement.Slot
					&& candidate.StatefulAnchor == Placement.StatefulAnchor
					&& candidate.Material == Placement.Material
					&& candidate.MinTech == Placement.MinTech
					&& candidate.Knowledge == Placement.Knowledge
					&& candidate.Power == Placement.Power
					&& candidate.Natural == Placement.Natural
					&& candidate.ExistingAuthority == Placement.ExistingAuthority) return true;
			}
			return false;
		}

		private static bool ContainsAnchor(ArchitectureLayoutSnapshot Snapshot,
			ArchitectureAnchor Anchor)
		{
			if (Snapshot == null || Snapshot.Anchors == null) return false;
			for (int i = 0; i < Snapshot.Anchors.Count; i++)
			{
				ArchitectureAnchor candidate = Snapshot.Anchors[i];
				if (candidate != null && candidate.Key == Anchor.Key && candidate.X == Anchor.X
					&& candidate.Y == Anchor.Y && candidate.Access == Anchor.Access) return true;
			}
			return false;
		}

		// --- Small receipt helpers ---------------------------------------------------------

		private static bool ReadString(GameObject Source, string Property, int Maximum,
			out string Value, out string Failure)
		{
			Value = null;
			if (!Source.HasStringProperty(Property) || Source.HasIntProperty(Property))
				return Fail("architecture receipt property " + Property + " is absent or has the wrong type",
					out Failure);
			Value = Source.GetStringProperty(Property, null);
			if (string.IsNullOrEmpty(Value) || Value.Length > Maximum || HasControl(Value))
				return Fail("architecture receipt property " + Property + " is malformed", out Failure);
			Failure = null;
			return true;
		}

		private static bool ReadInt(GameObject Source, string Property,
			out int Value, out string Failure)
		{
			Value = 0;
			if (!Source.HasIntProperty(Property) || Source.HasStringProperty(Property))
				return Fail("architecture receipt property " + Property + " is absent or has the wrong type",
					out Failure);
			Value = Source.GetIntProperty(Property);
			Failure = null;
			return true;
		}

		private static bool MatchesMapping(ArchitectureLayoutSnapshot Snapshot,
			KingdomArchitectureMapping Mapping)
		{
			return Snapshot != null && Mapping != null && Snapshot.BuildKey == Mapping.BuildKey
				&& Snapshot.PlanKey == Mapping.PlanKey && Snapshot.BindingKey == Mapping.BindingKey
				&& Snapshot.TierKey == Mapping.TierKey && Snapshot.LotType == Mapping.TypeKey
				&& Snapshot.LotSize == Mapping.LotSize;
		}

		private static bool ValidRectInZone(KingdomPlotRules.PlotRect Rect, Zone Z)
		{
			return ValidRect(Rect) && Z != null && Rect.X1 >= 0 && Rect.Y1 >= 0
				&& Rect.X2 < Z.Width && Rect.Y2 < Z.Height;
		}

		private static bool ValidRect(KingdomPlotRules.PlotRect Rect)
		{
			if (Rect.X2 < Rect.X1 || Rect.Y2 < Rect.Y1) return false;
			long width = (long)Rect.X2 - Rect.X1 + 1L;
			long height = (long)Rect.Y2 - Rect.Y1 + 1L;
			return width > 0 && height > 0
				&& width * height <= KingdomArchitectureRules.MaxMapArea;
		}

		private static bool ValidKey(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= KingdomArchitectureRules.MaxKeyChars
				&& Value == Value.Trim() && !HasControl(Value);
		}

		private static bool CanonicalHash(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9')
					|| (Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}

		private static bool HasControl(string Value)
		{
			if (Value == null) return false;
			for (int i = 0; i < Value.Length; i++) if (char.IsControl(Value[i])) return true;
			return false;
		}

		private static bool Fail(string Message, out string Failure)
		{
			if (string.IsNullOrEmpty(Message)) Message = "architecture runtime failed";
			char[] cleaned = null;
			for (int i = 0; i < Message.Length; i++)
				if (char.IsControl(Message[i]))
				{
					if (cleaned == null) cleaned = Message.ToCharArray();
					cleaned[i] = ' ';
				}
			if (cleaned != null) Message = new string(cleaned);
			if (Message.Length > MaxFailureChars) Message = Message.Substring(0, MaxFailureChars);
			Failure = Message;
			return false;
		}
	}
}
