using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Engine-free, fair admission for already normalized provider snapshots.</summary>
	internal static class KingdomForeignFootprintBudgetRules
	{
		internal static void Apply(List<KingdomForeignProviderSnapshot> Snapshots)
		{
			if (Snapshots == null) return;
			Snapshots.Sort((a, b) => string.CompareOrdinal(a?.ProviderId, b?.ProviderId));
			List<KingdomForeignFootprintEvidence>[] candidates =
				new List<KingdomForeignFootprintEvidence>[Snapshots.Count];
			int maximumDepth = 0;
			for (int i = 0; i < Snapshots.Count; i++)
			{
				KingdomForeignProviderSnapshot snapshot = Snapshots[i];
				candidates[i] = snapshot?.Rows == null
					? new List<KingdomForeignFootprintEvidence>()
					: new List<KingdomForeignFootprintEvidence>(snapshot.Rows);
				if (snapshot?.Rows != null) snapshot.Rows.Clear();
				if (candidates[i].Count > maximumDepth) maximumDepth = candidates[i].Count;
			}
			int rows = 0; long cells = 0L;
			for (int depth = 0; depth < maximumDepth
				&& rows < KingdomForeignFootprintSnapshotRules.MaxRows; depth++)
			{
				for (int p = 0; p < Snapshots.Count
					&& rows < KingdomForeignFootprintSnapshotRules.MaxRows; p++)
				{
					if (depth >= candidates[p].Count || Snapshots[p]?.Rows == null) continue;
					KingdomForeignFootprintEvidence row = candidates[p][depth];
					int rowCells = row?.Cells?.Count ?? int.MaxValue;
					if (rowCells < 1 || cells + rowCells > KingdomForeignFootprintSnapshotRules.MaxCells)
					{
						AddFault(Snapshots[p], "global cell budget omitted a footprint row");
						continue;
					}
					Snapshots[p].Rows.Add(row); rows++; cells += rowCells;
				}
			}
			for (int p = 0; p < Snapshots.Count; p++)
				if (Snapshots[p] != null && Snapshots[p].Rows != null
					&& Snapshots[p].Rows.Count < candidates[p].Count)
					AddFault(Snapshots[p],
						"global row or cell budget omitted additional footprint rows");
		}

		private static void AddFault(KingdomForeignProviderSnapshot Snapshot, string Failure)
		{
			if (Snapshot?.RowFaults == null) return;
			int maximum = KingdomForeignFootprintSnapshotRules.MaxFaultsPerProvider;
			if (Snapshot.RowFaults.Count < maximum - 1) Snapshot.RowFaults.Add(Failure);
			else if (Snapshot.RowFaults.Count == maximum - 1)
				Snapshot.RowFaults.Add("additional foreign footprint row faults omitted");
		}
	}
}
