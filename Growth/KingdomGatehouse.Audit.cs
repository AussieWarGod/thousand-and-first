using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouse
	{
		/// <summary>Re-audit immediately before projection; allows only its exact root/scaffold.</summary>
		public static bool TryAudit(Zone Z, KingdomGatehousePlan Plan, GameObject Root,
			GameObject Scaffold, out string Failure)
		{
			Failure = null;
			if (Z == null || Plan == null || !KingdomGatehouseRules.TryEncode(Plan, out _))
			{
				Failure = "The frozen gatehouse footprint cannot be read.";
				return false;
			}
			KingdomPlotRules.PlotRect proposed = new KingdomPlotRules.PlotRect(
				Plan.X1, Plan.Y1, Plan.X2, Plan.Y2);
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject item in survey.PlotRoots)
			{
				if (!GameObject.Validate(item) || ReferenceEquals(item, Root)
					|| ReferenceEquals(item, Scaffold)
					|| RecognizedProjectionSatellite(item, Root, Plan, Z)) continue;
				if (KingdomPlots.TryReadRect(item, out KingdomPlotRules.PlotRect laid)
					&& KingdomPlotRules.Overlaps(proposed, laid))
				{
					Failure = "The frozen gatehouse footprint overlaps another reserved work at "
						+ item.CurrentCell.X + "," + item.CurrentCell.Y + ".";
					return false;
				}
			}
			for (int y = Plan.Y1; y <= Plan.Y2; y++)
			{
				for (int x = Plan.X1; x <= Plan.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (!AuditFootprintCell(cell, Root, Scaffold, Plan, Z,
						out string blocker))
					{
						Failure = "The gatehouse footprint is blocked at " + x + "," + y
							+ (string.IsNullOrEmpty(blocker) ? "." : (" by " + blocker + "."));
						return false;
					}
				}
			}
			for (int i = 0; i < 2; i++)
			{
				if (!KingdomGatehouseRules.TryApproach(Plan, i, out KingdomGatehouseCell approach))
					return false;
				Cell cell = Z.GetCell(approach.X, approach.Y);
				if (cell == null || !cell.IsPassable() || cell.HasObjectWithPart("LiquidVolume"))
				{
					Failure = "The " + approach.Slot + " is not passable at "
						+ approach.X + "," + approach.Y + ".";
					return false;
				}
			}
			return true;
		}

	}
}
