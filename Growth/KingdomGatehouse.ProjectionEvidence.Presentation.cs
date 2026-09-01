using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouse
	{
		private static void StampProjectionSatellite(GameObject Item, GameObject Root,
			KingdomGatehousePlan Plan, int Index, KingdomGatehouseCell Spec)
		{
			Item.SetIntProperty(SatelliteProperty, 1);
			Item.SetStringProperty(OwnerProperty, Root.IDIfAssigned);
			Item.SetIntProperty(IndexProperty, Index);
			Item.SetStringProperty(SlotProperty, Spec.Slot);
			if (Index != 0) return;
			KingdomPlots.StampRect(Item, new KingdomPlotRules.PlotRect(
				Plan.X1, Plan.Y1, Plan.X2, Plan.Y2));
			Item.SetIntProperty(ReservationProperty, Schema);
		}

		private static bool TryApplySatellitePalette(GameObject Item,
			KingdomGatehousePlan Plan, int Index)
		{
			if (!GameObject.Validate(Item) || Plan == null) return false;
			if (Plan.ReceiptVersion != 2) return true;
			if (!KingdomGatehouseRules.TrySatelliteRender(Plan, Index,
				out string glyph, out string color, out string tileColor,
				out string detail, out string tile)) return false;
			Render render = Item.GetPart<Render>();
			if (render == null) return false;
			render.RenderString = glyph;
			render.ColorString = color;
			render.TileColor = tileColor;
			render.DetailColor = detail;
			if (!string.IsNullOrEmpty(tile)) render.Tile = tile;
			return ExactSatellitePalette(Item, Plan, Index);
		}

		private static bool ExactSatellitePalette(GameObject Item,
			KingdomGatehousePlan Plan, int Index)
		{
			if (!GameObject.Validate(Item) || Plan == null) return false;
			if (Plan.ReceiptVersion != 2) return true;
			Render render = Item.GetPart<Render>();
			return render != null
				&& KingdomGatehouseRules.TrySatelliteRender(Plan, Index,
					out string glyph, out string color, out string tileColor,
					out string detail, out string tile)
				&& render.RenderString == glyph && render.ColorString == color
				&& render.TileColor == tileColor && render.DetailColor == detail
				&& (string.IsNullOrEmpty(tile) || render.Tile == tile);
		}

		private static bool AllProjectionSlotsSettled(GameObject Root,
			IPart Part)
		{
			if (!GameObject.Validate(Root)) return false;
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
				if (Root.HasStringProperty(SatelliteStateProperty(i))
					|| !Root.HasIntProperty(SatelliteStateProperty(i))
					|| Root.GetIntProperty(SatelliteStateProperty(i))
						!= (int)KingdomGatehouseSlotState.Settled
					|| ProjectionCustody(Part, i) != null) return false;
			return true;
		}

		private static bool ContestSlot(GameObject Root, int Index, string Reason,
			out string Failure)
		{
			Failure = string.IsNullOrEmpty(Reason)
				? "A gatehouse satellite carries contested evidence." : Reason;
			if (GameObject.Validate(Root)) Root.SetIntProperty(SatelliteStateProperty(Index),
				(int)KingdomGatehouseSlotState.Contested);
			return false;
		}

		private static bool FailSlot(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}
	}
}
