using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Pure bounded flood law shared by ordinary boundary ingress and explicit
	/// inter-zone circulation ingress.</summary>
	public static class KingdomBenefitAccessRules
	{
		private static readonly int[] Dx = { 1, -1, 0, 0 };
		private static readonly int[] Dy = { 0, 0, 1, -1 };

		public static bool TryReachable(IReadOnlyList<KingdomBenefitCell> Cells,
			Func<int, int, bool> Passable, out HashSet<long> Reachable)
		{
			Reachable = null;
			if (Cells == null || Cells.Count < 1
				|| Cells.Count > KingdomDesignationRules.MaxCellsPerDesignation
				|| Passable == null) return false;
			Dictionary<long, KingdomBenefitCell> exact =
				new Dictionary<long, KingdomBenefitCell>();
			for (int i = 0; i < Cells.Count; i++)
			{
				long key = KingdomDesignationRules.Pack(Cells[i].X, Cells[i].Y);
				if (exact.ContainsKey(key)) return false;
				exact.Add(key, Cells[i]);
			}
			HashSet<long> reached = new HashSet<long>();
			Queue<long> frontier = new Queue<long>();
			try
			{
				foreach (KeyValuePair<long, KingdomBenefitCell> pair in exact)
				{
					KingdomBenefitCell cell = pair.Value;
					if (!Passable(cell.X, cell.Y)) continue;
					bool seed = (cell.Use & KingdomBenefitCellUse.Ingress) != 0;
					for (int d = 0; !seed && d < 4; d++)
					{
						int x = cell.X + Dx[d], y = cell.Y + Dy[d];
						seed = !exact.ContainsKey(KingdomDesignationRules.Pack(x, y))
							&& Passable(x, y);
					}
					if (seed && reached.Add(pair.Key)) frontier.Enqueue(pair.Key);
				}
				while (frontier.Count > 0)
				{
					long packed = frontier.Dequeue();
					int x = (int)(packed >> 32), y = (int)packed;
					for (int d = 0; d < 4; d++)
					{
						int nx = x + Dx[d], ny = y + Dy[d];
						long next = KingdomDesignationRules.Pack(nx, ny);
						if (!reached.Contains(next) && exact.ContainsKey(next)
							&& Passable(nx, ny))
						{
							reached.Add(next); frontier.Enqueue(next);
						}
					}
				}
			}
			catch { return false; }
			Reachable = reached; return true;
		}
	}
}
