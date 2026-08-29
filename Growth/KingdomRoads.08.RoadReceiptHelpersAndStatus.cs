using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRoads
	{
		private static KingdomPhysicalLookupState FindRoadId(Zone Z, string Id,
			out GameObject Exact)
		{
			return KingdomConstruction.FindExactId(Z, Id, out Exact);
		}

		private static bool ExactRoadOld(GameObject Old, Cell Cell, RoadRow Row)
		{
			GameObject global;
			return GameObject.Validate(Old) && Cell != null && Old.IDIfAssigned == Row.OldId
				&& Old.CurrentCell == Cell
				&& Old.Blueprint == Row.OldBlueprint
				&& Old.GetIntProperty(PathStateProperty) == (int)KingdomRoadRules.WearState.Path
				&& FindRoadId(Cell.ParentZone, Row.OldId, out global)
					== KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(global, Old);
		}

		private static bool ExactRoadFloor(Zone Z, RoadRow Row, string Blueprint,
			KingdomConstructionJob Job, bool RequireOldAbsent)
		{
			GameObject floor;
			if (FindRoadId(Z, Row.NewId, out floor) != KingdomPhysicalLookupState.Exact
				|| !GameObject.Validate(floor) || floor.CurrentCell != Z.GetCell(Row.X, Row.Y)
				|| floor.Blueprint != Blueprint
				|| floor.GetIntProperty(PathStateProperty) != (int)KingdomRoadRules.WearState.Paved
				|| !KingdomConstruction.HasReceipt(floor, Job)) return false;
			if (RequireOldAbsent && FindRoadId(Z, Row.OldId, out _)
				!= KingdomPhysicalLookupState.Absent) return false;
			foreach (GameObject item in floor.CurrentCell.GetObjects())
				if (item != floor && item.GetIntProperty(PathStateProperty) > 0) return false;
			return true;
		}

		private static bool RemoveRoadObject(GameObject Object, Zone Z)
		{
			if (!GameObject.Validate(Object))
			{
				KingdomSurvey.ObserveRemovedFromActive(Z, Object);
				return true;
			}
			try
			{
				return Object.Obliterate(null, Silent: true) && !GameObject.Validate(Object);
			}
			catch { return false; }
			finally
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Z, Object);
			}
		}

		private static bool CurrentRoadOwner(Zone Z, KingdomConstructionJob Job)
		{
			KingdomSystem system = The.Game == null
				? null : The.Game.RequireSystem<KingdomSystem>();
			return KingdomConstruction.Owns(system, Z, Job)
				&& KingdomConstruction.IsCurrent(Job);
		}

		/// <summary>
		/// One line for the status report: what the settlement's own feet have made of its
		/// ground. Never null, and never silent about a full tally (STANDARDS 7b).
		/// </summary>
		/// <param name="Z">The zone. Null answers the line for ground nobody walks.</param>
		public static string WornLine(Zone Z)
		{
			if (!Enabled)
			{
				return "Ground here does not wear. (Options: the settlement's ways)";
			}
			if (Z == null)
			{
				return "No ground here is walked enough to show it.";
			}
			int paths = 0;
			int paved = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				int state = (item == null) ? 0 : item.GetIntProperty(PathStateProperty);
				if (state == (int)KingdomRoadRules.WearState.Path)
				{
					paths++;
				}
				else if (state == (int)KingdomRoadRules.WearState.Paved)
				{
					paved++;
				}
			}
			int worn = ReadTally(Z).Count;
			if (paths == 0 && paved == 0 && worn == 0)
			{
				return "No ground here is walked enough to show it.";
			}
			string line = "The ground shows " + worn + ((worn == 1) ? " cell" : " cells") + " of wearing, "
				+ paths + ((paths == 1) ? " cell" : " cells") + " of path, and "
				+ paved + ((paved == 1) ? " cell" : " cells") + " of paving.";
			if (paths > 0)
			{
				line += " (Charter: pave a worn path)";
			}
			return line;
		}
	}
}
