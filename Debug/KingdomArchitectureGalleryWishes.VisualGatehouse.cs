using System;
using System.Collections.Generic;

using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomArchitectureGalleryWishes
	{
		private static bool TryStageVisualGatehouse(Zone Zone, VisualCase Case,
			KingdomPlotRules.PlotRect Rect, List<VisualCreated> Created, out string Failure)
		{
			Failure = null;
			KingdomGatehousePlan plan = new KingdomGatehousePlan
			{
				Orientation = KingdomGatehouseOrientation.North,
				GateX = Rect.CenterX,
				GateY = Rect.Y1,
				X1 = Rect.X1,
				Y1 = Rect.Y1,
				X2 = Rect.X2,
				Y2 = Rect.Y2
			};
			string encoded;
			if (!KingdomGatehouseRules.TryEncode(plan, out encoded))
				return Fail("The isolated gatehouse plan is not canonical.", out Failure);
			for (int i = 0; i < 2; i++)
			{
				KingdomGatehouseCell approach;
				if (!KingdomGatehouseRules.TryApproach(plan, i, out approach)
					|| !Zone.GetCell(approach.X, approach.Y).IsPassable())
					return Fail("The isolated gatehouse has no clear road approach.", out Failure);
			}

			GameObject root = GameObject.Create("r_KingdomGatehouse");
			if (!GameObject.Validate(root) || root.Blueprint != "r_KingdomGatehouse")
				return Fail("The gatehouse root blueprint could not be created exactly.", out Failure);
			Created.Add(new VisualCreated { Item = root, Role = "gate-root" });
			r_KingdomGatehouse projection = root.GetPart<r_KingdomGatehouse>();
			if (projection == null)
				return Fail("The gatehouse root has no production projection part.", out Failure);
			root.RemovePart(projection);
			root.SetStringProperty(KingdomUpgrade.BuildKeyProperty, KingdomGatehouseRules.BuildKey);
			Cell gate = Zone.GetCell(plan.GateX, plan.GateY);
			GameObject accepted = gate.AddObject(root, NoStack: true, Silent: true);
			if (!ReferenceEquals(accepted, root)
				|| !ReferenceEquals(root.CurrentCell, gate) || root.InInventory != null)
				return Fail("The engine refused, replaced, or displaced the exact gatehouse root.", out Failure);

			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
			{
				KingdomGatehouseCell spec;
				if (!KingdomGatehouseRules.TrySatellite(plan, i, out spec))
					return Fail("The gatehouse satellite topology is incomplete.", out Failure);
				GameObject item = GameObject.Create(spec.Blueprint);
				if (!GameObject.Validate(item) || item.Blueprint != spec.Blueprint)
					return Fail("A gatehouse satellite blueprint could not be created exactly.", out Failure);
				Created.Add(new VisualCreated { Item = item, Role = "gate-" + spec.Slot });
				item.SetIntProperty(KingdomGatehouse.SatelliteProperty, 1);
				item.SetStringProperty(KingdomGatehouse.OwnerProperty, root.ID);
				item.SetIntProperty(KingdomGatehouse.IndexProperty, i);
				item.SetStringProperty(KingdomGatehouse.SlotProperty, spec.Slot);
				if (i == 0)
				{
					KingdomPlots.StampRect(item, Rect);
					item.SetIntProperty(KingdomGatehouse.ReservationProperty, KingdomGatehouse.Schema);
				}
				root.SetStringProperty(KingdomGatehouse.SatelliteIdProperty(i), item.ID);
				accepted = Zone.GetCell(spec.X, spec.Y).AddObject(item, NoStack: true, Silent: true);
				if (!ReferenceEquals(accepted, item)
					|| !KingdomGatehouse.IsOwnedSatellite(item, root.ID, spec.Blueprint,
						spec.X, spec.Y, Zone))
					return Fail("A gatehouse satellite changed during exact placement.", out Failure);
			}
			root.SetStringProperty(KingdomGatehouse.PlanProperty, encoded);
			root.AddPart(new r_KingdomGatehouse());
			if (root.GetPart<r_KingdomGatehouse>() == null)
				return Fail("The gatehouse production projection part did not reattach.", out Failure);
			root.SetIntProperty(KingdomGatehouse.SchemaProperty, KingdomGatehouse.Schema);
			root.MakeActive();
			KingdomGatehousePlan observed;
			if (!KingdomGatehouse.TryReadPlan(root, out observed, out Failure)) return false;
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
			{
				KingdomGatehouseCell spec;
				KingdomGatehouseRules.TrySatellite(observed, i, out spec);
				GameObject item = Created[i + 1].Item;
				if (!KingdomGatehouse.IsOwnedSatellite(item, root.ID, spec.Blueprint,
					spec.X, spec.Y, Zone))
					return Fail("The completed gatehouse topology did not read back exactly.", out Failure);
			}
			return true;
		}
	}
}
