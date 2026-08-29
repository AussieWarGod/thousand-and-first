using System;
using System.Collections.Generic;

using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public partial class KingdomArchitectureGalleryWishes
	{
		private static bool TryValidateVisualActive(Zone Zone, VisualCase Case, int Total,
			out List<VisualCreated> Items, out KingdomPlotRules.PlotRect Rect, out string Failure)
		{
			Items = null;
			Rect = default(KingdomPlotRules.PlotRect);
			Failure = null;
			GameObject anchor = The.Player;
			string receipt = anchor?.GetStringProperty(VisualReceiptProperty);
			if (!VisualGalleryActive() || Zone == null || Case == null
				|| anchor.GetStringProperty(VisualZoneProperty) != Zone.ZoneID
				|| anchor.GetIntProperty(VisualNumberProperty) != Case.Number
				|| anchor.GetStringProperty(VisualCaseProperty) != Case.Key
				|| anchor.GetIntProperty(VisualExpectedCountProperty) != Case.ExpectedObjects
				|| anchor.GetStringProperty(VisualExpectedScreenshotProperty)
					!= VisualScreenshot(Case.Number, Total)
				|| string.IsNullOrEmpty(receipt))
				return Fail("The active visual-gallery anchor is absent or malformed.", out Failure);
			if (!TryVisualRect(Case, out Rect, out Failure)
				|| Rect.X2 >= Zone.Width || Rect.Y2 >= Zone.Height
				|| !TryVisualItems(Zone, Case, receipt, out Items, out Failure)) return false;
			if (Case.Kind == VisualCaseKind.Gatehouse
				&& !ValidateVisualGatehouse(Zone, Items, out Failure)) return false;
			if (Case.Kind == VisualCaseKind.RoadWorn)
			{
				List<KingdomRoadRules.WornCell> tally = KingdomRoads.ReadTally(Zone);
				if (tally.Count != 1 || tally[0].X != Rect.CenterX || tally[0].Y != Rect.CenterY
					|| tally[0].Traffic != KingdomRoadRules.WornTraffic
					|| KingdomRoads.FindOurFloor(Zone.GetCell(Rect.CenterX, Rect.CenterY), out _)
						!= KingdomPhysicalLookupState.Absent)
					return Fail("The exact worn-ground visual state changed.", out Failure);
			}
			string extra = Case.Kind == VisualCaseKind.RoadWorn
				? Zone.GetZoneProperty(KingdomRoads.TallyProperty, "") : "";
			if (VisualDigest(Case, Zone, Rect, Items, extra)
				!= anchor.GetStringProperty(VisualDigestProperty))
				return Fail("The exact visual-gallery digest changed; cleanup stopped.", out Failure);
			return true;
		}

		private static bool ValidateVisualGatehouse(Zone Zone, List<VisualCreated> Items,
			out string Failure)
		{
			Failure = null;
			GameObject root = null;
			for (int i = 0; i < Items.Count; i++)
				if (Items[i].Role == "gate-root") root = Items[i].Item;
			if (!GameObject.Validate(root) || root.GetPart<XRL.World.Parts.r_KingdomGatehouse>() == null
				|| !KingdomGatehouse.TryReadPlan(root, out KingdomGatehousePlan plan, out Failure)) return false;
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
			{
				KingdomGatehouseCell spec;
				if (!KingdomGatehouseRules.TrySatellite(plan, i, out spec))
					return Fail("The visual gatehouse plan lost a satellite.", out Failure);
				GameObject match = null;
				string role = "gate-" + spec.Slot;
				for (int p = 0; p < Items.Count; p++) if (Items[p].Role == role) match = Items[p].Item;
				if (!KingdomGatehouse.IsOwnedSatellite(match, root.ID, spec.Blueprint,
					spec.X, spec.Y, Zone))
					return Fail("The visual gatehouse topology changed at " + spec.Slot + ".", out Failure);
			}
			return true;
		}

		private static bool TryClearVisual(Zone Zone, VisualCase Case, int Total, out string Failure)
		{
			List<VisualCreated> items;
			KingdomPlotRules.PlotRect rect;
			if (!TryValidateVisualActive(Zone, Case, Total, out items, out rect, out Failure)) return false;
			if (Case.Kind == VisualCaseKind.RoadWorn)
			{
				bool present = The.Player.GetIntProperty(VisualPriorTallyPresentProperty) == 1;
				string prior = The.Player.GetStringProperty(VisualPriorTallyProperty);
				RestoreVisualTally(Zone, present, prior);
				if (Zone.HasZoneProperty(KingdomRoads.TallyProperty) != present
					|| (present && Zone.GetZoneProperty(KingdomRoads.TallyProperty, null) != prior))
					return Fail("The prior worn-ground tally could not be restored exactly.", out Failure);
			}
			else
			{
				GameObject root = null;
				for (int i = items.Count - 1; i >= 0; i--)
				{
					if (items[i].Role == "gate-root") { root = items[i].Item; continue; }
					if (!RemoveVisualItem(items[i].Item, Zone))
						return Fail("An exact visual-gallery object refused removal.", out Failure);
				}
				if (root != null && !RemoveVisualItem(root, Zone))
					return Fail("The exact visual gatehouse root refused removal.", out Failure);
			}
			string receipt = The.Player.GetStringProperty(VisualReceiptProperty);
			foreach (GameObject item in Zone.GetObjects())
				if (GameObject.Validate(item) && !item.IsPlayer()
					&& item.GetIntProperty(VisualSchemaProperty) == VisualGallerySchema
					&& item.GetStringProperty(VisualReceiptProperty) == receipt)
					return Fail("A visual-gallery receipt remains after cleanup.", out Failure);
			ClearVisualAnchor();
			return true;
		}

		private static bool RemoveVisualItem(GameObject Item, Zone Zone)
		{
			if (!GameObject.Validate(Item)) return false;
			bool removed = false;
			try { removed = Item.Obliterate(null, Silent: true); }
			catch { }
			finally { KingdomSurvey.ObserveCurrentTopologyInActive(Zone, Item); }
			return removed && !GameObject.Validate(Item);
		}
	}
}
