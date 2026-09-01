namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRules
	{
		/// <summary>
		/// Proves that aggregate catalogue shelter does not contradict local authored fabric.
		/// Mixed courts and subworks are intentional: Walled is allowed to contain every local
		/// cover, and Open may contain canvas or a small enclosed work. Soft and Carved are the
		/// special physical claims and therefore carry stricter local evidence.
		/// </summary>
		private static bool TryValidateCurrentRoof(ArchitectureLayoutSnapshot Snapshot,
			out string Failure)
		{
			Failure = null;
			bool soft = false;
			bool natural = false;
			for (int i = 0; i < Snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = Snapshot.Cells[i];
				if (cell == null) return Fail("snapshot roof fabric is malformed", out Failure);
				bool relevant = cell.Claim == ArchitectureClaim.Building
					|| (cell.X == Snapshot.MainX && cell.Y == Snapshot.MainY);
				if (!relevant) continue;
				soft |= cell.Cover == ArchitectureCover.Soft;
				natural |= cell.Cover == ArchitectureCover.Natural;
				if (Snapshot.BaseRoof == KingdomPlotRules.RoofState.Soft
					&& (cell.Cover == ArchitectureCover.Walled
						|| cell.Cover == ArchitectureCover.Natural))
					return Fail("soft catalogue roof contradicts walled or natural building fabric",
						out Failure);
				if (Snapshot.BaseRoof == KingdomPlotRules.RoofState.Open
					&& cell.Cover == ArchitectureCover.Natural)
					return Fail("open catalogue roof contradicts natural building fabric", out Failure);
				if (Snapshot.BaseRoof == KingdomPlotRules.RoofState.Carved
					&& cell.Cover == ArchitectureCover.Walled)
					return Fail("carved catalogue roof contradicts raised-wall building fabric",
						out Failure);
			}
			if (Snapshot.BaseRoof == KingdomPlotRules.RoofState.Soft && !soft)
				return Fail("soft catalogue roof has no local soft building fabric", out Failure);
			if (Snapshot.BaseRoof == KingdomPlotRules.RoofState.Carved && !natural)
				return Fail("carved catalogue roof has no local natural building fabric", out Failure);
			return true;
		}
	}
}
