using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		/// <summary>
		/// Exact cells an authored renovation would newly occupy or refurnish. Standing yard work
		/// outside this set is not an obstruction merely because the lot map also describes its yard.
		/// </summary>
		private static bool TryUpgradeImpact(KingdomArchitectureIntent BeforeIntent,
			KingdomArchitectureIntent AfterIntent, ArchitectureLayoutDelta Delta, Zone Z,
			out HashSet<int> Impacted, out string Failure)
		{
			Impacted = null;
			Failure = null;
			if (Z == null || Delta == null || Delta.After == null
				|| !KingdomArchitectureRuntime.TryWorldFootprint(BeforeIntent,
					out KingdomPlotRules.PlotRect beforeFootprint, out Failure)
				|| !KingdomArchitectureRuntime.TryWorldFootprint(AfterIntent,
					out KingdomPlotRules.PlotRect afterFootprint, out Failure)) return false;

			HashSet<int> result = new HashSet<int>();
			for (int y = afterFootprint.Y1; y <= afterFootprint.Y2; y++)
				for (int x = afterFootprint.X1; x <= afterFootprint.X2; x++)
					if (!beforeFootprint.Contains(x, y)
						&& !TryAddUpgradeImpact(result, Z, x, y, out Failure)) return false;
			for (int i = 0; i < Delta.Added.Count; i++)
			{
				if (!KingdomArchitectureRuntime.TryWorldPlacement(Delta.After,
					AfterIntent.Rect, Delta.Added[i], out int x, out int y, out Failure)) return false;
				if (!TryAddUpgradeImpact(result, Z, x, y, out Failure)) return false;
			}
			for (int i = 0; i < Delta.Cells.Count; i++)
			{
				ArchitectureCellState after = Delta.Cells[i].After;
				if (after == null || !KingdomArchitectureRules.IsClaimed(after.Claim)) continue;
				if (!KingdomArchitectureRuntime.TryWorldCell(Delta.After,
					AfterIntent.Rect, after, out int x, out int y, out Failure)) return false;
				if (!TryAddUpgradeImpact(result, Z, x, y, out Failure)) return false;
			}
			Impacted = result;
			return true;
		}

		private static bool TryAddUpgradeImpact(HashSet<int> Impacted, Zone Z, int X, int Y,
			out string Failure)
		{
			Failure = null;
			if (Impacted == null || Z == null || X < 0 || Y < 0 || X >= Z.Width || Y >= Z.Height)
				return Fail("authored upgrade impact leaves its exact zone", out Failure);
			Impacted.Add(Y * Z.Width + X);
			return true;
		}
	}
}
