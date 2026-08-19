using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.World;

namespace ThousandAndFirst
{
	public static class KingdomCommission
	{
		public static bool Commission(KingdomSystem System, string Key, out string Failure)
		{
			Failure = null;
			Zone zone = The.Player?.CurrentZone;
			if (!System.Founded || zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Failure = "Commissions are issued on the kingdom's own ground.";
				return false;
			}
			if (!KingdomData.TryGetBuilding(Key, out var entry) || !KingdomRules.StyleAllows(entry.Styles, System.Style))
			{
				Failure = "No such design.";
				return false;
			}
			int built = 0;
			foreach (GameObject item in zone.GetObjects())
			{
				if (item.GetIntProperty("KingdomBuilt") == 1 || item.HasPart("r_KingdomScaffold"))
				{
					built++;
				}
			}
			if (built >= KingdomRules.MaxBuildings)
			{
				Failure = "There is no more room in the plan. " + System.KingdomDisplayName + " is as built-up as this ground allows.";
				return false;
			}
			if (KingdomGrowth.CountStoredWater(zone) < entry.CostDrams)
			{
				Failure = "The work would cost {{C|" + entry.CostDrams + " drams}} from the stores, and the stores cannot bear it.";
				return false;
			}
			Cell cell = FindBuildCell(zone);
			if (cell == null)
			{
				Failure = "There is no clear ground for it here.";
				return false;
			}
			KingdomGrowth.ConsumeStoredWater(zone, entry.CostDrams);
			GameObject gameObject = GameObject.Create("r_KingdomScaffold");
			if (gameObject == null)
			{
				Failure = "The scaffold could not be raised.";
				return false;
			}
			r_KingdomScaffold part = gameObject.GetPart<r_KingdomScaffold>();
			if (part != null)
			{
				part.TargetBlueprint = entry.Blueprint;
				part.TargetDisplayName = entry.Name;
				part.CompleteTick = The.Game.TimeTicks + entry.BuildTicks;
				part.StaffNeeded = entry.Staff;
				part.ThresholdManning = KingdomRules.IsThresholdManning(entry.Manning);
				part.Defence = entry.Defence;
			}
			cell.AddObject(gameObject);
			KingdomChronicle.Record(System, XRL.Language.Grammar.A(entry.Name) + " was commissioned at " + System.KingdomDisplayName);
			MessageQueue.AddPlayerMessage("{{G|The " + entry.Name + " is commissioned. Scaffolding rises.}}");
			return true;
		}

		public static Cell FindBuildCell(Zone Z)
		{
			Cell playerCell = The.Player?.CurrentCell;
			if (playerCell != null)
			{
				List<Cell> adjacent = playerCell.GetLocalAdjacentCells();
				for (int i = 0; i < adjacent.Count; i++)
				{
					if (adjacent[i].IsEmpty() && adjacent[i].IsPassable() && !adjacent[i].HasObjectWithPart("LiquidVolume"))
					{
						return adjacent[i];
					}
				}
			}
			List<Cell> emptyCells = Z.GetEmptyCells();
			if (emptyCells != null && emptyCells.Count > 0)
			{
				return emptyCells.GetRandomElement();
			}
			return null;
		}
	}
}
