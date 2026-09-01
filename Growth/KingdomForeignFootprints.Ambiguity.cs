using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomForeignFootprints
	{
		/// <summary>Provider-local contradictions fault that provider before this point. Cross-provider
		/// overlap has no priority winner, so only the affected exact rows become refusal evidence.</summary>
		private static void RefuseCrossProviderOverlaps(
			List<KingdomForeignProviderSnapshot> Snapshots)
		{
			Dictionary<long, KingdomForeignFootprintEvidence> owners =
				new Dictionary<long, KingdomForeignFootprintEvidence>();
			HashSet<long> refused = new HashSet<long>();
			HashSet<KingdomForeignFootprintEvidence> ambiguous =
				new HashSet<KingdomForeignFootprintEvidence>();
			for (int p = 0; p < Snapshots.Count; p++)
			{
				KingdomForeignProviderSnapshot snapshot = Snapshots[p];
				if (snapshot.Status != KingdomForeignProviderStatus.Observed) continue;
				for (int r = 0; r < snapshot.Rows.Count; r++)
				{
					KingdomForeignFootprintEvidence row = snapshot.Rows[r];
					if (!row.IsRefused) continue;
					for (int c = 0; c < row.Cells.Count; c++)
						refused.Add(KingdomDesignationRules.Pack(
							row.Cells[c].X, row.Cells[c].Y));
				}
			}
			for (int p = 0; p < Snapshots.Count; p++)
			{
				KingdomForeignProviderSnapshot snapshot = Snapshots[p];
				if (snapshot.Status != KingdomForeignProviderStatus.Observed) continue;
				for (int r = 0; r < snapshot.Rows.Count; r++)
				{
					KingdomForeignFootprintEvidence row = snapshot.Rows[r];
					if (row.IsRefused) continue;
					for (int c = 0; c < row.Cells.Count; c++)
					{
						long key = KingdomDesignationRules.Pack(row.Cells[c].X, row.Cells[c].Y);
						if (refused.Contains(key)) ambiguous.Add(row);
						else if (!owners.TryGetValue(key,
							out KingdomForeignFootprintEvidence prior)) owners.Add(key, row);
						else if (!ReferenceEquals(prior, row))
						{
							ambiguous.Add(prior); ambiguous.Add(row);
						}
					}
				}
			}
			foreach (KingdomForeignFootprintEvidence row in ambiguous)
				row.Refusal = "foreign footprint overlaps another provider row";
		}
	}
}
