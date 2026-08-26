using System;
using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouse
	{
		private static bool TryExactSatellites(GameObject Root, Zone Z,
			KingdomGatehousePlan Plan, out List<GameObject> Satellites, out string Failure)
		{
			Satellites = new List<GameObject>(KingdomGatehouseRules.SatelliteCount);
			Failure = null;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
			{
				string id = Root.GetStringProperty(SatelliteIdProperty(i));
				if (string.IsNullOrEmpty(id) || !ids.Add(id)
					|| !KingdomGatehouseRules.TrySatellite(Plan, i, out KingdomGatehouseCell spec))
				{
					Failure = "The gatehouse's exact satellite receipt is absent or duplicated.";
					return false;
				}
				GameObject item = GameObject.FindByID(id);
				if (!IsOwnedSatellite(item, Root.ID, spec.Blueprint, spec.X, spec.Y, Z)
					|| item.ID != id || item.GetIntProperty(IndexProperty) != i
					|| item.GetStringProperty(SlotProperty) != spec.Slot
					|| (i == 0 && (item.GetIntProperty(ReservationProperty) != Schema
						|| !KingdomPlots.TryReadRect(item, out KingdomPlotRules.PlotRect rect)
						|| !SameRect(rect, Plan)))
					|| (i != 0 && item.HasIntProperty(KingdomPlots.PlotX2Property)))
				{
					Failure = "A gatehouse satellite was removed, moved, replaced, or changed.";
					return false;
				}
				Satellites.Add(item);
			}
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject item in survey.GatehouseSatellites)
			{
				if (IsOwnedSatellite(item, Root.ID) && !ids.Contains(item.ID))
				{
					Failure = "A new or replacement satellite entered the gatehouse receipt.";
					return false;
				}
			}
			return true;
		}

		private static GameObject FindExactScaffold(Cell Cell, KingdomConstructionJob Job)
		{
			if (Cell == null || Job == null || string.IsNullOrEmpty(Job.SubjectId)) return null;
			GameObject found = null;
			foreach (GameObject item in Cell.GetObjects())
			{
				if (GameObject.Validate(item) && item.ID == Job.SubjectId
					&& item.HasPart("r_KingdomScaffold")
					&& KingdomConstruction.HasReceipt(item, Job))
				{
					if (found != null) return null;
					found = item;
				}
			}
			return found;
		}

		private static bool AuditFootprintCell(Cell Cell, GameObject Root, GameObject Scaffold,
			out string Blocker)
		{
			Blocker = null;
			if (Cell == null)
			{
				Blocker = "the edge of the zone";
				return false;
			}
			bool hasExpected = false;
			foreach (GameObject item in Cell.GetObjects())
			{
				if (ReferenceEquals(item, Root) || ReferenceEquals(item, Scaffold))
				{
					hasExpected = true;
					continue;
				}
				if (!GameObject.Validate(item)) continue;
				if (item.IsPlayer() || item.IsCreature)
				{
					Blocker = item.IsPlayer() ? "the founder" : item.ShortDisplayNameStripped;
					return false;
				}
				if (KingdomPlots.ReadObject(item) != KingdomPlotRules.GroundKind.Bare)
				{
					Blocker = item.ShortDisplayNameStripped ?? item.Blueprint;
					return false;
				}
			}
			if (!hasExpected && (!Cell.IsPassable() || Cell.HasObjectWithPart("LiquidVolume")))
			{
				Blocker = "impassable ground";
				return false;
			}
			return true;
		}

		private static void ClearRootReceipt(GameObject Root)
		{
			if (!GameObject.Validate(Root)) return;
			Root.RemoveIntProperty(SchemaProperty);
			Root.RemoveStringProperty(PlanProperty);
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
				Root.RemoveStringProperty(SatelliteIdProperty(i));
		}

		private static bool SameRect(KingdomPlotRules.PlotRect Rect, KingdomGatehousePlan Plan)
		{
			return Plan != null && Rect.X1 == Plan.X1 && Rect.Y1 == Plan.Y1
				&& Rect.X2 == Plan.X2 && Rect.Y2 == Plan.Y2;
		}
	}
}
