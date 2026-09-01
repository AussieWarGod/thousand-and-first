namespace ThousandAndFirst
{
	/// <summary>Bounded compatibility for already-paid a3 improvements.</summary>
	public static partial class KingdomPlots
	{
		/// <summary>
		/// Replays the footprint inference frozen into the a3 completion path. New work never enters
		/// here: a3 could not serialize building versus yard or catalogue roof authority.
		/// </summary>
		private static bool TryLegacyManagedGrowthTruth(ArchitectureLayoutSnapshot Snapshot,
			KingdomArchitectureIntent Intent, out KingdomPlotRules.PlotRect Footprint,
			out KingdomPlotRules.RoofState Roof, out string Failure)
		{
			Footprint = default(KingdomPlotRules.PlotRect);
			Roof = KingdomPlotRules.RoofState.Open;
			Failure = null;
			if (Snapshot == null || Intent == null
				|| !KingdomArchitectureRules.IsManagedSnapshotEncoding(Intent.EncodedSnapshot)
				|| KingdomArchitectureRules.IsLatestSnapshotEncoding(Intent.EncodedSnapshot))
			{
				Failure = "Only an already-paid save-era authored improvement may use legacy growth truth.";
				return false;
			}

			bool coveredOnly = false;
			for (int i = 0; i < Snapshot.Cells.Count; i++)
				if (KingdomArchitectureRules.IsClaimed(Snapshot.Cells[i].Claim)
					&& Snapshot.Cells[i].Cover != ArchitectureCover.Open)
				{
					coveredOnly = true;
					break;
				}
			int x1 = int.MaxValue;
			int y1 = int.MaxValue;
			int x2 = int.MinValue;
			int y2 = int.MinValue;
			for (int i = 0; i < Snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = Snapshot.Cells[i];
				if (!KingdomArchitectureRules.IsClaimed(cell.Claim)
					|| (coveredOnly && cell.Cover == ArchitectureCover.Open)) continue;
				if (!KingdomArchitectureRuntime.TryWorldCell(Snapshot, Intent.Rect, cell,
					out int x, out int y, out Failure)) return false;
				if (x < x1) x1 = x;
				if (y < y1) y1 = y;
				if (x > x2) x2 = x;
				if (y > y2) y2 = y;
				if (cell.Cover == ArchitectureCover.Natural)
					Roof = KingdomPlotRules.RoofState.Carved;
				else if (cell.Cover == ArchitectureCover.Walled
					&& Roof != KingdomPlotRules.RoofState.Carved)
					Roof = KingdomPlotRules.RoofState.Walled;
				else if (cell.Cover == ArchitectureCover.Soft
					&& Roof == KingdomPlotRules.RoofState.Open)
					Roof = KingdomPlotRules.RoofState.Soft;
			}
			if (x1 == int.MaxValue)
			{
				Failure = "Save-era authored successor has no claimed plot ground.";
				return false;
			}
			Footprint = new KingdomPlotRules.PlotRect(x1, y1, x2, y2);
			return true;
		}
	}
}
